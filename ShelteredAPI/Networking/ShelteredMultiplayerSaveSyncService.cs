using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Networking;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Sessions;
using ModAPI.Networking.Snapshots;
using ShelteredAPI.Core;
using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Runtime;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerSaveSyncService : IDisposable
    {
        public const ushort SaveSyncRequestMessageType = SessionMessageTypes.FirstApplicationMessageType + 16;
        public const ushort SaveSyncChunkMessageType = SessionMessageTypes.FirstApplicationMessageType + 17;
        public const ushort SaveSyncAckMessageType = SessionMessageTypes.FirstApplicationMessageType + 18;

        private const int MaxChunkPayloadBytes = 720;
        private const int MaxChunksPerUpdate = 8;
        private const string MultiplayerModId = "ShelteredAPI.Multiplayer";
        private const string LogComponent = "SaveSync";

        private readonly NetworkSession _session;
        private readonly SaveSyncLogSink _log;
        private readonly Dictionary<string, NetworkSnapshotTransferAssembler> _assemblers =
            new Dictionary<string, NetworkSnapshotTransferAssembler>(StringComparer.Ordinal);
        private readonly Queue<OutboundChunk> _outboundChunks = new Queue<OutboundChunk>();

        private SaveBackupRecord _clientBackup;
        private string _status = "idle";
        private string _lastError = string.Empty;
        private bool _disposed;

        internal ShelteredMultiplayerSaveSyncService(NetworkSession session, SaveSyncLogSink log)
        {
            _session = session;
            _log = log;
            ShelteredAPI.Saves.Events.OnAfterSave += OnAfterSave;
        }

        internal delegate void SaveSyncLogSink(MMLog.LogLevel level, string component, string message);

        public string Status
        {
            get { return _status; }
        }

        public string LastError
        {
            get { return _lastError; }
        }

        private string CurrentSessionId
        {
            get
            {
                ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
                return context != null ? context.SessionId : string.Empty;
            }
        }

        public static bool IsSaveSyncMessage(ushort messageType)
        {
            return messageType == SaveSyncRequestMessageType
                || messageType == SaveSyncChunkMessageType
                || messageType == SaveSyncAckMessageType;
        }

        public void Update()
        {
            if (_disposed)
                return;

            int sent = 0;
            while (sent < MaxChunksPerUpdate && _outboundChunks.Count > 0)
            {
                OutboundChunk item = _outboundChunks.Dequeue();
                bool queued = false;

                try
                {
                    if (_session.Mode == NetworkSessionMode.Host)
                        queued = _session.SendToPeer(item.PeerId, SaveSyncChunkMessageType, NetworkChannel.Reliable, item.Payload);
                    else if (_session.Mode == NetworkSessionMode.Client)
                        queued = _session.SendToHost(SaveSyncChunkMessageType, NetworkChannel.Reliable, item.Payload);
                }
                catch (Exception ex)
                {
                    SetError("Failed to queue save-sync chunk: " + ex.Message);
                }

                if (queued)
                    sent++;
            }
        }

        public void HandlePeerConnected(NetworkPeer peer)
        {
            if (_disposed || peer == null)
                return;

            if (_session.Mode == NetworkSessionMode.Host)
            {
                QueueSnapshotForPeer(peer.PeerId, "join");
                return;
            }

            if (_session.Mode == NetworkSessionMode.Client)
                SendSnapshotRequest("join");
        }

        public void HandlePeerDisconnected(NetworkPeer peer)
        {
            if (_disposed)
                return;

            if (_session.Mode == NetworkSessionMode.Client)
                RestoreClientBackup("disconnect");
        }

        public void HandleLocalSessionEnding(string reason)
        {
            if (_disposed)
                return;

            if (_session.Mode == NetworkSessionMode.Client)
                RestoreClientBackup(string.IsNullOrEmpty(reason) ? "local-disconnect" : reason);
        }

        public bool TryHandleMessage(NetworkPeer peer, ushort messageType, byte[] payload)
        {
            if (!IsSaveSyncMessage(messageType))
                return false;

            try
            {
                if (messageType == SaveSyncRequestMessageType)
                    HandleSnapshotRequest(peer, payload);
                else if (messageType == SaveSyncChunkMessageType)
                    HandleSnapshotChunk(peer, payload);
                else if (messageType == SaveSyncAckMessageType)
                    HandleSnapshotAck(peer, payload);
            }
            catch (Exception ex)
            {
                SetError("Save-sync message failed: " + ex.Message);
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ShelteredAPI.Saves.Events.OnAfterSave -= OnAfterSave;
            _assemblers.Clear();
            _outboundChunks.Clear();
            _disposed = true;
        }

        private void OnAfterSave(SaveEntry entry)
        {
            if (_disposed || _session == null)
                return;

            if (_session.Mode == NetworkSessionMode.Host)
            {
                QueueSnapshotForAll("save");
                return;
            }

            if (_session.Mode == NetworkSessionMode.Client)
                UpdateClientBackupFromActiveSave("successful-save");
        }

        private void SendSnapshotRequest(string reason)
        {
            byte[] payload = CreateRequestPayload(reason);
            if (!_session.SendToHost(SaveSyncRequestMessageType, NetworkChannel.Reliable, payload))
            {
                SetError("Unable to request host save snapshot; client is not connected.");
                return;
            }

            SetStatus("requested host save snapshot");
            WriteLog(MMLog.LogLevel.Info, "Requested host save snapshot. Reason=" + reason + ".");
        }

        private void HandleSnapshotRequest(NetworkPeer peer, byte[] payload)
        {
            if (_session.Mode != NetworkSessionMode.Host || peer == null)
                return;

            string reason = ReadReasonPayload(payload);
            QueueSnapshotForPeer(peer.PeerId, string.IsNullOrEmpty(reason) ? "request" : reason);
        }

        private void QueueSnapshotForAll(string reason)
        {
            NetworkPeer[] peers = _session.GetPeers();
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer != null && peer.State == NetworkConnectionState.Connected)
                    QueueSnapshotForPeer(peer.PeerId, reason);
            }
        }

        private void QueueSnapshotForPeer(byte peerId, string reason)
        {
            byte[] payload;
            string error;
            if (!TryCapturePackage(reason, out payload, out error))
            {
                SetError(error);
                SendAckToPeer(peerId, string.Empty, false, error);
                return;
            }

            string transferId = BuildTransferId(reason);
            NetworkSnapshotChunk[] chunks = NetworkSnapshotTransfer.CreateChunks(transferId, payload, MaxChunkPayloadBytes);
            for (int i = 0; i < chunks.Length; i++)
            {
                byte[] chunkPayload = SerializeChunk(chunks[i]);
                _outboundChunks.Enqueue(new OutboundChunk(peerId, chunkPayload));
            }

            SetStatus("queued " + chunks.Length + " save snapshot chunk(s)");
            WriteLog(MMLog.LogLevel.Info, "Queued host save snapshot for peer " + peerId
                + ". Reason=" + reason + ", bytes=" + payload.Length + ", chunks=" + chunks.Length + ".");
        }

        private bool TryCapturePackage(string reason, out byte[] payload, out string error)
        {
            payload = null;
            error = string.Empty;

            ActiveSaveLocation location;
            if (!ActiveSaveLocation.TryGetCurrent(out location, out error))
                return false;

            if (!location.HasPlayableSave)
            {
                error = "No active save data was found. Load or create a save before hosting multiplayer.";
                return false;
            }

            try
            {
                WriteSessionMetadata(location, reason);
                ShelteredMultiplayerSavePackage package = ShelteredMultiplayerSavePackage.Capture(
                    location,
                    CurrentSessionId,
                    reason);
                payload = package.ToBytes();
                return true;
            }
            catch (Exception ex)
            {
                error = "Failed to capture active save snapshot: " + ex.Message;
                return false;
            }
        }

        private void HandleSnapshotChunk(NetworkPeer peer, byte[] payload)
        {
            NetworkSnapshotChunk chunk = DeserializeChunk(payload);
            string key = BuildAssemblerKey(peer, chunk.TransferId);

            NetworkSnapshotTransferAssembler assembler;
            if (!_assemblers.TryGetValue(key, out assembler))
            {
                assembler = new NetworkSnapshotTransferAssembler(chunk);
                _assemblers[key] = assembler;
            }
            else
            {
                assembler.AddChunk(chunk);
            }

            SetStatus("receiving host save snapshot " + assembler.ReceivedCount + "/" + assembler.ChunkCount);

            byte[] rebuilt;
            if (!assembler.TryBuild(out rebuilt))
                return;

            _assemblers.Remove(key);
            ApplyCompletedSnapshot(peer, chunk.TransferId, rebuilt);
        }

        private void ApplyCompletedSnapshot(NetworkPeer peer, string transferId, byte[] payload)
        {
            ShelteredMultiplayerSavePackage package = ShelteredMultiplayerSavePackage.FromBytes(payload);
            ActiveSaveLocation target = ActiveSaveLocation.FromPackage(package);

            try
            {
                if (_session.Mode == NetworkSessionMode.Client)
                    _clientBackup = SaveBackupRecord.CreateOrUpdate(CurrentSessionId, target, "pre-apply");

                package.ApplyTo(target);
                WriteSessionMetadata(target, package.Reason);
                TryQueueLoadAppliedSnapshot(target);

                if (_session.Mode == NetworkSessionMode.Client
                    && string.Equals(package.Reason, "save", StringComparison.OrdinalIgnoreCase))
                {
                    _clientBackup = SaveBackupRecord.CreateOrUpdate(CurrentSessionId, target, "successful-save");
                }

                SetStatus("host save snapshot applied");
                WriteLog(MMLog.LogLevel.Info, "Applied host save snapshot. Scenario=" + target.ScenarioId
                    + ", slot=" + target.SlotIndex + ", reason=" + package.Reason + ".");
                SendAck(peer, transferId, true, "applied");
            }
            catch (Exception ex)
            {
                SetError("Failed to apply host save snapshot: " + ex.Message);
                SendAck(peer, transferId, false, ex.Message);
            }
        }

        private void HandleSnapshotAck(NetworkPeer peer, byte[] payload)
        {
            SnapshotAck ack = SnapshotAck.FromPayload(payload);
            if (ack.Success)
            {
                WriteLog(MMLog.LogLevel.Info, "Peer " + (peer != null ? peer.PeerId.ToString() : "?")
                    + " applied save snapshot " + ack.TransferId + ".");
                return;
            }

            SetError("Peer failed save snapshot " + ack.TransferId + ": " + ack.Message);
        }

        private void SendAck(NetworkPeer peer, string transferId, bool success, string message)
        {
            if (_session.Mode == NetworkSessionMode.Client)
            {
                _session.SendToHost(SaveSyncAckMessageType, NetworkChannel.Reliable,
                    new SnapshotAck(transferId, success, message).ToPayload());
                return;
            }

            if (_session.Mode == NetworkSessionMode.Host && peer != null)
                SendAckToPeer(peer.PeerId, transferId, success, message);
        }

        private void SendAckToPeer(byte peerId, string transferId, bool success, string message)
        {
            try
            {
                _session.SendToPeer(peerId, SaveSyncAckMessageType, NetworkChannel.Reliable,
                    new SnapshotAck(transferId, success, message).ToPayload());
            }
            catch
            {
                // GuardrailAllow: SilentCatch - snapshot ack send failures are already reflected by transport/session state.
            }
        }

        private void RestoreClientBackup(string reason)
        {
            if (_clientBackup == null)
                return;

            try
            {
                _clientBackup.Restore();
                TryQueueLoadAppliedSnapshot(_clientBackup.Target);
                SetStatus("restored multiplayer backup");
                WriteLog(MMLog.LogLevel.Info, "Restored multiplayer backup after " + reason + ".");
            }
            catch (Exception ex)
            {
                SetError("Failed to restore multiplayer backup: " + ex.Message);
            }
        }

        private void UpdateClientBackupFromActiveSave(string reason)
        {
            ActiveSaveLocation location;
            string error;
            if (!ActiveSaveLocation.TryGetCurrent(out location, out error))
            {
                SetError(error);
                return;
            }

            try
            {
                _clientBackup = SaveBackupRecord.CreateOrUpdate(CurrentSessionId, location, reason);
                WriteLog(MMLog.LogLevel.Info, "Updated multiplayer backup from successful save state.");
            }
            catch (Exception ex)
            {
                SetError("Failed to update multiplayer backup: " + ex.Message);
            }
        }

        private void WriteSessionMetadata(ActiveSaveLocation location, string reason)
        {
            if (location == null || string.IsNullOrEmpty(location.SlotRoot))
                return;

            string modFolder = Path.Combine(Path.Combine(location.SlotRoot, "mods"), MultiplayerModId);
            if (!Directory.Exists(modFolder))
                Directory.CreateDirectory(modFolder);

            string path = Path.Combine(modFolder, "session.json");
            string json = "{"
                + "\"version\":1,"
                + "\"sessionId\":\"" + EscapeJson(CurrentSessionId) + "\","
                + "\"sessionNonce\":\"" + EscapeJson(_session.SessionNonce) + "\","
                + "\"mode\":\"" + EscapeJson(_session.Mode.ToString()) + "\","
                + "\"localPeerId\":" + _session.LocalPeerId + ","
                + "\"scenarioId\":\"" + EscapeJson(location.ScenarioId) + "\","
                + "\"saveId\":\"" + EscapeJson(location.SaveId) + "\","
                + "\"slotIndex\":" + location.SlotIndex + ","
                + "\"reason\":\"" + EscapeJson(reason) + "\","
                + "\"updatedUtc\":\"" + DateTime.UtcNow.ToString("o") + "\""
                + "}";
            File.WriteAllText(path, json);
        }

        private void TryQueueLoadAppliedSnapshot(ActiveSaveLocation target)
        {
            if (_session.Mode != NetworkSessionMode.Client || target == null)
                return;

            try
            {
                SaveManager manager = SaveManager.instance;
                if (manager == null || manager.isSaving || manager.isLoading)
                    return;

                if (target.IsVanillaPlatformSlot && target.SlotIndex >= 1 && target.SlotIndex <= 3)
                {
                    SaveInfo vanillaInfo = SaveRegistryCore.ReadVanillaSaveInfo(target.SlotIndex);
                    if (vanillaInfo != null)
                    {
                        DifficultyManager.StoreMenuDifficultySettings(
                            vanillaInfo.rainDiff,
                            vanillaInfo.resourceDiff,
                            vanillaInfo.breachDiff,
                            vanillaInfo.factionDiff,
                            vanillaInfo.moodDiff,
                            vanillaInfo.mapSize,
                            vanillaInfo.fog);
                    }

                    manager.SetSlotToLoad(target.SlotIndex);
                    WriteLog(MMLog.LogLevel.Info, "Queued load for synced vanilla slot " + target.SlotIndex + ".");
                    return;
                }

                SaveEntry entry = ResolveSaveEntry(target);
                if (entry == null)
                {
                    WriteLog(MMLog.LogLevel.Warning, "Synced save was written, but no save entry could be resolved for auto-load.");
                    return;
                }

                PlatformSaveProxy.SetNextLoad(SaveManager.SaveType.Slot1, target.ScenarioId, entry.id);
                if (entry.saveInfo != null)
                {
                    DifficultyManager.StoreMenuDifficultySettings(
                        entry.saveInfo.rainDiff,
                        entry.saveInfo.resourceDiff,
                        entry.saveInfo.breachDiff,
                        entry.saveInfo.factionDiff,
                        entry.saveInfo.moodDiff,
                        entry.saveInfo.mapSize,
                        entry.saveInfo.fog);
                }

                manager.SetSlotToLoad(1);
                WriteLog(MMLog.LogLevel.Info, "Queued load for synced save " + entry.id + " via slot 1.");
            }
            catch (Exception ex)
            {
                WriteLog(MMLog.LogLevel.Warning, "Synced save was written, but automatic load failed: " + ex.Message);
            }
        }

        private static SaveEntry ResolveSaveEntry(ActiveSaveLocation target)
        {
            if (target == null)
                return null;

            if (string.Equals(target.ScenarioId, "Standard", StringComparison.OrdinalIgnoreCase))
                return ExpandedVanillaSaves.GetBySlot(target.SlotIndex);

            return ScenarioSaves.GetTrustedRegistry(target.ScenarioId).GetSaveBySlot(target.SlotIndex);
        }

        private static byte[] CreateRequestPayload(string reason)
        {
            byte[] buffer = new byte[256];
            BitWriter writer = new BitWriter(buffer);
            writer.WriteString(reason ?? string.Empty);
            byte[] payload = new byte[writer.Position];
            Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
            return payload;
        }

        private static string ReadReasonPayload(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return string.Empty;

            BitReader reader = new BitReader(payload, 0, payload.Length);
            return reader.ReadString();
        }

        private static byte[] SerializeChunk(NetworkSnapshotChunk chunk)
        {
            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            BitWriter writer = new BitWriter(buffer);
            chunk.WriteTo(ref writer);
            byte[] payload = new byte[writer.Position];
            Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
            return payload;
        }

        private static NetworkSnapshotChunk DeserializeChunk(byte[] payload)
        {
            BitReader reader = new BitReader(payload, 0, payload != null ? payload.Length : 0);
            return NetworkSnapshotChunk.ReadFrom(ref reader);
        }

        private static string BuildTransferId(string reason)
        {
            return "save-" + (string.IsNullOrEmpty(reason) ? "sync" : reason) + "-" + DateTime.UtcNow.Ticks.ToString("x");
        }

        private static string BuildAssemblerKey(NetworkPeer peer, string transferId)
        {
            byte peerId = peer != null ? peer.PeerId : NetworkDefaults.UnassignedPeerId;
            return peerId + ":" + (transferId ?? string.Empty);
        }

        private void SetStatus(string status)
        {
            _status = status ?? string.Empty;
            _lastError = string.Empty;
        }

        private void SetError(string error)
        {
            _lastError = error ?? string.Empty;
            _status = "error";
            WriteLog(MMLog.LogLevel.Error, _lastError);
        }

        private void WriteLog(MMLog.LogLevel level, string message)
        {
            if (_log != null)
                _log(level, LogComponent, message ?? string.Empty);
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class OutboundChunk
        {
            public OutboundChunk(byte peerId, byte[] payload)
            {
                PeerId = peerId;
                Payload = payload ?? new byte[0];
            }

            public readonly byte PeerId;
            public readonly byte[] Payload;
        }

        private sealed class SnapshotAck
        {
            private const int MaxMessageLength = 900;

            public SnapshotAck(string transferId, bool success, string message)
            {
                TransferId = transferId ?? string.Empty;
                Success = success;
                Message = Truncate(message ?? string.Empty, MaxMessageLength);
            }

            public readonly string TransferId;
            public readonly bool Success;
            public readonly string Message;

            public byte[] ToPayload()
            {
                byte[] buffer = new byte[1100];
                BitWriter writer = new BitWriter(buffer);
                writer.WriteString(TransferId);
                writer.WriteBool(Success);
                writer.WriteString(Message);
                byte[] payload = new byte[writer.Position];
                Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
                return payload;
            }

            public static SnapshotAck FromPayload(byte[] payload)
            {
                BitReader reader = new BitReader(payload, 0, payload != null ? payload.Length : 0);
                string transferId = reader.ReadString();
                bool success = reader.ReadBool();
                string message = reader.ReadString();
                return new SnapshotAck(transferId, success, message);
            }

            private static string Truncate(string value, int maxLength)
            {
                if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                    return value ?? string.Empty;

                return value.Substring(0, maxLength);
            }
        }

        private sealed class ActiveSaveLocation
        {
            public string ScenarioId;
            public string SaveId;
            public int SlotIndex;
            public string SlotRoot;
            public string VanillaSavePath;
            public bool IsVanillaPlatformSlot;

            public bool HasPlayableSave
            {
                get
                {
                    if (IsVanillaPlatformSlot && !string.IsNullOrEmpty(VanillaSavePath) && File.Exists(VanillaSavePath))
                        return true;

                    string saveData = !string.IsNullOrEmpty(SlotRoot) ? Path.Combine(SlotRoot, "SaveData.xml") : null;
                    return !string.IsNullOrEmpty(saveData) && File.Exists(saveData);
                }
            }

            public static bool TryGetCurrent(out ActiveSaveLocation location, out string error)
            {
                location = null;
                error = string.Empty;

                IModSaveContext context = new ShelteredSaveRuntimeAdapter().GetCurrentSaveContext();
                if (context == null || string.IsNullOrEmpty(context.SlotPath) || context.SlotIndex <= 0)
                {
                    error = "No active Sheltered save slot is available for multiplayer save sync.";
                    return false;
                }

                string scenarioId = string.IsNullOrEmpty(context.SaveScopeId) ? "Standard" : context.SaveScopeId;
                location = new ActiveSaveLocation();
                location.ScenarioId = scenarioId;
                location.SaveId = string.IsNullOrEmpty(context.SaveId) ? scenarioId + "_" + context.SlotIndex : context.SaveId;
                location.SlotIndex = context.SlotIndex;
                location.SlotRoot = context.SlotPath;
                location.IsVanillaPlatformSlot = context.HostSaveDescriptor == null && context.SlotIndex >= 1 && context.SlotIndex <= 5;
                if (location.IsVanillaPlatformSlot)
                    location.VanillaSavePath = SaveRegistryCore.GetVanillaSavePath(context.SlotIndex);

                return true;
            }

            public static ActiveSaveLocation FromPackage(ShelteredMultiplayerSavePackage package)
            {
                if (package == null)
                    throw new ArgumentNullException("package");

                ActiveSaveLocation location = new ActiveSaveLocation();
                location.ScenarioId = string.IsNullOrEmpty(package.ScenarioId) ? "Standard" : package.ScenarioId;
                location.SaveId = string.IsNullOrEmpty(package.SaveId) ? location.ScenarioId + "_" + package.SlotIndex : package.SaveId;
                location.SlotIndex = package.SlotIndex;
                location.SlotRoot = DirectoryProvider.SlotRoot(location.ScenarioId, location.SlotIndex, true);
                location.IsVanillaPlatformSlot = package.HasVanillaFile;
                if (location.IsVanillaPlatformSlot)
                    location.VanillaSavePath = SaveRegistryCore.GetVanillaSavePath(location.SlotIndex);
                return location;
            }
        }

        private sealed class ShelteredMultiplayerSavePackage
        {
            private const int Magic = 0x53504D53; // SMPS
            private const int Version = 1;

            public string SessionId;
            public string Reason;
            public string ScenarioId;
            public string SaveId;
            public int SlotIndex;
            public bool HasVanillaFile;
            public string VanillaFileName;
            public byte[] VanillaBytes;
            public readonly List<PackageFile> Files = new List<PackageFile>();

            public byte[] ToBytes()
            {
                MemoryStream stream = new MemoryStream();
                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(SessionId ?? string.Empty);
                writer.Write(Reason ?? string.Empty);
                writer.Write(ScenarioId ?? string.Empty);
                writer.Write(SaveId ?? string.Empty);
                writer.Write(SlotIndex);
                writer.Write(HasVanillaFile);
                writer.Write(VanillaFileName ?? string.Empty);
                WriteBytes(writer, VanillaBytes);
                writer.Write(Files.Count);
                for (int i = 0; i < Files.Count; i++)
                {
                    writer.Write(Files[i].RelativePath ?? string.Empty);
                    WriteBytes(writer, Files[i].Bytes);
                }

                writer.Flush();
                return stream.ToArray();
            }

            public static ShelteredMultiplayerSavePackage FromBytes(byte[] bytes)
            {
                if (bytes == null)
                    throw new ArgumentNullException("bytes");

                BinaryReader reader = new BinaryReader(new MemoryStream(bytes));
                int magic = reader.ReadInt32();
                if (magic != Magic)
                    throw new InvalidDataException("Save snapshot package had an invalid header.");

                int version = reader.ReadInt32();
                if (version != Version)
                    throw new InvalidDataException("Save snapshot package version is not supported.");

                ShelteredMultiplayerSavePackage package = new ShelteredMultiplayerSavePackage();
                package.SessionId = reader.ReadString();
                package.Reason = reader.ReadString();
                package.ScenarioId = reader.ReadString();
                package.SaveId = reader.ReadString();
                package.SlotIndex = reader.ReadInt32();
                package.HasVanillaFile = reader.ReadBoolean();
                package.VanillaFileName = reader.ReadString();
                package.VanillaBytes = ReadBytes(reader);

                int count = reader.ReadInt32();
                if (count < 0 || count > 100000)
                    throw new InvalidDataException("Save snapshot package file count is invalid.");

                for (int i = 0; i < count; i++)
                {
                    string relativePath = reader.ReadString();
                    byte[] fileBytes = ReadBytes(reader);
                    package.Files.Add(new PackageFile(relativePath, fileBytes));
                }

                return package;
            }

            public static ShelteredMultiplayerSavePackage Capture(ActiveSaveLocation location, string sessionId, string reason)
            {
                if (location == null)
                    throw new ArgumentNullException("location");

                ShelteredMultiplayerSavePackage package = new ShelteredMultiplayerSavePackage();
                package.SessionId = sessionId ?? string.Empty;
                package.Reason = reason ?? string.Empty;
                package.ScenarioId = location.ScenarioId;
                package.SaveId = location.SaveId;
                package.SlotIndex = location.SlotIndex;

                if (!string.IsNullOrEmpty(location.SlotRoot) && Directory.Exists(location.SlotRoot))
                {
                    string[] files = Directory.GetFiles(location.SlotRoot, "*", SearchOption.AllDirectories);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string file = files[i];
                        if (!ShouldIncludeFile(file))
                            continue;

                        package.Files.Add(new PackageFile(ToRelativePath(location.SlotRoot, file), File.ReadAllBytes(file)));
                    }
                }

                if (location.IsVanillaPlatformSlot && !string.IsNullOrEmpty(location.VanillaSavePath) && File.Exists(location.VanillaSavePath))
                {
                    package.HasVanillaFile = true;
                    package.VanillaFileName = Path.GetFileName(location.VanillaSavePath);
                    package.VanillaBytes = File.ReadAllBytes(location.VanillaSavePath);
                }
                else
                {
                    package.VanillaBytes = new byte[0];
                }

                return package;
            }

            public void ApplyTo(ActiveSaveLocation target)
            {
                if (target == null)
                    throw new ArgumentNullException("target");

                if (target.SlotIndex <= 0)
                    throw new InvalidDataException("Save snapshot target slot is invalid.");

                DirectoryProvider.SlotRoot(target.ScenarioId, target.SlotIndex, true);
                DeleteDirectoryContents(target.SlotRoot);

                for (int i = 0; i < Files.Count; i++)
                {
                    PackageFile file = Files[i];
                    string destination = CombineSafe(target.SlotRoot, file.RelativePath);
                    string dir = Path.GetDirectoryName(destination);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(destination, file.Bytes ?? new byte[0]);
                }

                if (HasVanillaFile && !string.IsNullOrEmpty(target.VanillaSavePath))
                {
                    string vanillaDir = Path.GetDirectoryName(target.VanillaSavePath);
                    if (!Directory.Exists(vanillaDir))
                        Directory.CreateDirectory(vanillaDir);
                    File.WriteAllBytes(target.VanillaSavePath, VanillaBytes ?? new byte[0]);
                }
            }

            private static void WriteBytes(BinaryWriter writer, byte[] bytes)
            {
                bytes = bytes ?? new byte[0];
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            private static byte[] ReadBytes(BinaryReader reader)
            {
                int length = reader.ReadInt32();
                if (length < 0)
                    throw new InvalidDataException("Negative byte payload length.");

                return reader.ReadBytes(length);
            }

            private static bool ShouldIncludeFile(string path)
            {
                if (string.IsNullOrEmpty(path))
                    return false;

                string name = Path.GetFileName(path);
                if (name != null && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }
        }

        private sealed class PackageFile
        {
            public PackageFile(string relativePath, byte[] bytes)
            {
                RelativePath = relativePath ?? string.Empty;
                Bytes = bytes ?? new byte[0];
            }

            public readonly string RelativePath;
            public readonly byte[] Bytes;
        }

        private sealed class SaveBackupRecord
        {
            public ActiveSaveLocation Target;
            public string BackupRoot;
            public bool SlotExisted;
            public bool VanillaExisted;

            public static SaveBackupRecord CreateOrUpdate(string sessionId, ActiveSaveLocation target, string reason)
            {
                if (target == null)
                    throw new ArgumentNullException("target");

                string backupRoot = BuildBackupRoot(sessionId, target);
                string tempRoot = backupRoot + ".tmp";
                DeleteDirectoryIfExists(tempRoot);
                Directory.CreateDirectory(tempRoot);

                SaveBackupRecord record = new SaveBackupRecord();
                record.Target = CloneLocation(target);
                record.BackupRoot = backupRoot;
                record.SlotExisted = Directory.Exists(target.SlotRoot);
                record.VanillaExisted = !string.IsNullOrEmpty(target.VanillaSavePath) && File.Exists(target.VanillaSavePath);

                if (record.SlotExisted)
                    CopyDirectory(target.SlotRoot, Path.Combine(tempRoot, "slot"));
                if (record.VanillaExisted)
                    File.Copy(target.VanillaSavePath, Path.Combine(tempRoot, "vanilla.dat"), true);

                File.WriteAllText(Path.Combine(tempRoot, "backup.json"),
                    "{\"version\":1,\"reason\":\"" + EscapeJson(reason) + "\",\"updatedUtc\":\""
                    + DateTime.UtcNow.ToString("o") + "\"}");

                DeleteDirectoryIfExists(backupRoot);
                Directory.Move(tempRoot, backupRoot);
                return record;
            }

            public void Restore()
            {
                if (Target == null || string.IsNullOrEmpty(BackupRoot) || !Directory.Exists(BackupRoot))
                    return;

                string slotBackup = Path.Combine(BackupRoot, "slot");
                if (Directory.Exists(Target.SlotRoot))
                    DeleteDirectoryIfExists(Target.SlotRoot);

                if (SlotExisted && Directory.Exists(slotBackup))
                    CopyDirectory(slotBackup, Target.SlotRoot);

                if (!string.IsNullOrEmpty(Target.VanillaSavePath))
                {
                    if (File.Exists(Target.VanillaSavePath))
                        File.Delete(Target.VanillaSavePath);

                    string vanillaBackup = Path.Combine(BackupRoot, "vanilla.dat");
                    if (VanillaExisted && File.Exists(vanillaBackup))
                    {
                        string dir = Path.GetDirectoryName(Target.VanillaSavePath);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.Copy(vanillaBackup, Target.VanillaSavePath, true);
                    }
                }
            }

            private static string BuildBackupRoot(string sessionId, ActiveSaveLocation target)
            {
                string safeSession = SanitizePathSegment(string.IsNullOrEmpty(sessionId) ? "session" : sessionId);
                string safeSlot = SanitizePathSegment((target.ScenarioId ?? "Standard") + "_Slot_" + target.SlotIndex);
                string root = Path.Combine(Path.Combine(DirectoryProvider.SavesRoot, "_multiplayer_backups"), safeSession);
                return Path.Combine(root, safeSlot);
            }

            private static ActiveSaveLocation CloneLocation(ActiveSaveLocation source)
            {
                ActiveSaveLocation clone = new ActiveSaveLocation();
                clone.ScenarioId = source.ScenarioId;
                clone.SaveId = source.SaveId;
                clone.SlotIndex = source.SlotIndex;
                clone.SlotRoot = source.SlotRoot;
                clone.VanillaSavePath = source.VanillaSavePath;
                clone.IsVanillaPlatformSlot = source.IsVanillaPlatformSlot;
                return clone;
            }
        }

        private static string ToRelativePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Save file path was outside the slot root.");

            string relative = fullPath.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string CombineSafe(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath) || relativePath.IndexOf("..") >= 0)
                throw new InvalidDataException("Save snapshot contained an unsafe relative path.");

            string destination = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Save snapshot path escaped the target slot.");

            return destination;
        }

        private static void DeleteDirectoryContents(string root)
        {
            if (string.IsNullOrEmpty(root))
                return;

            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
                return;
            }

            string[] files = Directory.GetFiles(root);
            for (int i = 0; i < files.Length; i++)
                File.Delete(files[i]);

            string[] dirs = Directory.GetDirectories(root);
            for (int i = 0; i < dirs.Length; i++)
                Directory.Delete(dirs[i], true);
        }

        private static void CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(source))
                return;

            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
            {
                string dest = Path.Combine(destination, Path.GetFileName(files[i]));
                File.Copy(files[i], dest, true);
            }

            string[] dirs = Directory.GetDirectories(source);
            for (int i = 0; i < dirs.Length; i++)
            {
                string dest = Path.Combine(destination, Path.GetFileName(dirs[i]));
                CopyDirectory(dirs[i], dest);
            }
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                Directory.Delete(path, true);
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "empty";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (chars[i] == invalid[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars);
        }
    }
}
