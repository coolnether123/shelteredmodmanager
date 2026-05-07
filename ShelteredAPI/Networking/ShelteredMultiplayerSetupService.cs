using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Networking;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Events;
using ShelteredAPI.Harmony;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerSetupService : IDisposable
    {
        public const ushort BeginSetupMessageType = SessionMessageTypes.FirstApplicationMessageType + 32;
        public const ushort SetupLoadedMessageType = SessionMessageTypes.FirstApplicationMessageType + 33;
        public const ushort ReleaseStartMessageType = SessionMessageTypes.FirstApplicationMessageType + 34;

        private const string LogComponent = "Setup";

        private readonly NetworkSession _session;
        private readonly SetupLogSink _log;
        private readonly HashSet<byte> _loadedPeers = new HashSet<byte>();
        private readonly HashSet<byte> _expectedPeers = new HashSet<byte>();

        private MultiplayerSetupMessage _currentSetup;
        private string _status = "idle";
        private string _lastError = string.Empty;
        private bool _localLoaded;
        private bool _released;
        private bool _disposed;

        internal ShelteredMultiplayerSetupService(NetworkSession session, SetupLogSink log)
        {
            _session = session;
            _log = log;
            GameEvents.OnSessionStarted += OnSessionStarted;
        }

        internal delegate void SetupLogSink(MMLog.LogLevel level, string component, string message);

        public string Status
        {
            get { return _status; }
        }

        public string LastError
        {
            get { return _lastError; }
        }

        public static bool IsSetupMessage(ushort messageType)
        {
            return messageType == BeginSetupMessageType
                || messageType == SetupLoadedMessageType
                || messageType == ReleaseStartMessageType;
        }

        public void BeginHostSetup(int hostAbsoluteSlot)
        {
            if (_disposed || _session == null || _session.Mode != NetworkSessionMode.Host)
                return;

            if (hostAbsoluteSlot <= 0)
                hostAbsoluteSlot = GetNextAvailableStandardSlot();

            _currentSetup = MultiplayerSetupMessage.CreateDefault(_session.SessionId, hostAbsoluteSlot);
            _loadedPeers.Clear();
            _expectedPeers.Clear();
            _localLoaded = false;
            _released = false;
            ShelteredMultiplayerHookService.Instance.SetWorldStartBlocked(true, "setup-begin-host");

            RememberConnectedPeers();
            BroadcastBeginSetup();
            SetStatus("setup started; waiting for players to load");
            WriteLog(MMLog.LogLevel.Info, "Host setup started. Slot=" + hostAbsoluteSlot + ".");
        }

        public void HandlePeerConnected(NetworkPeer peer)
        {
            if (_disposed || peer == null || _session == null || _session.Mode != NetworkSessionMode.Host)
                return;

            if (_currentSetup == null || _released)
                return;

            _expectedPeers.Add(peer.PeerId);
            SendBeginSetup(peer.PeerId);
        }

        public void HandlePeerDisconnected(NetworkPeer peer)
        {
            if (_disposed || peer == null)
                return;

            _loadedPeers.Remove(peer.PeerId);
            if (_currentSetup != null && !_released && _expectedPeers.Contains(peer.PeerId))
            {
                SetStatus("waiting for peer " + peer.PeerId + " to reconnect and load");
                WriteLog(MMLog.LogLevel.Warning, "Peer " + peer.PeerId
                    + " disconnected during setup; keeping world start blocked.");
            }
        }

        public bool TryHandleMessage(NetworkPeer peer, ushort messageType, byte[] payload)
        {
            if (!IsSetupMessage(messageType))
                return false;

            try
            {
                if (messageType == BeginSetupMessageType)
                    HandleBeginSetup(payload);
                else if (messageType == SetupLoadedMessageType)
                    HandleSetupLoaded(peer, payload);
                else if (messageType == ReleaseStartMessageType)
                    HandleReleaseStart(payload);
            }
            catch (Exception ex)
            {
                SetError("Setup message failed: " + ex.Message);
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            GameEvents.OnSessionStarted -= OnSessionStarted;
            _loadedPeers.Clear();
            _expectedPeers.Clear();
            _disposed = true;
        }

        public void Update()
        {
            if (_disposed || _currentSetup == null || _released)
                return;

            if (_session != null
                && _session.Mode == NetworkSessionMode.Client
                && AutoLoadFlow.PendingNewSave)
            {
                AutoLoadFlow.TryAdvanceFromActiveMainMenu("Multiplayer setup");
            }
        }

        private void BroadcastBeginSetup()
        {
            NetworkPeer[] peers = _session.GetPeers();
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer != null && peer.State == NetworkConnectionState.Connected)
                    SendBeginSetup(peer.PeerId);
            }
        }

        private void SendBeginSetup(byte peerId)
        {
            if (_currentSetup == null)
                return;

            _session.SendToPeer(peerId, BeginSetupMessageType, NetworkChannel.Reliable, _currentSetup.ToPayload());
            WriteLog(MMLog.LogLevel.Info, "Sent setup begin to peer " + peerId + ".");
        }

        private void HandleBeginSetup(byte[] payload)
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Client)
                return;

            _currentSetup = MultiplayerSetupMessage.FromPayload(payload);
            _localLoaded = false;
            _released = false;

            int ignoredSeed;
            string ignoredError;
            ShelteredMultiplayerSessionSeed.TryApply(_currentSetup.SessionId, out ignoredSeed, out ignoredError);
            ApplyDifficulty(_currentSetup);
            ShelteredMultiplayerHookService.Instance.SetWorldStartBlocked(true, "setup-begin-client");

            ShelteredDeferredPatchTriggers.ApplySaveFlowCritical("Multiplayer setup client auto-new-save");
            ShelteredDeferredPatchTriggers.ApplyGameplayDeferred("Multiplayer setup client auto-new-save");
            AutoLoadFlow.BeginNewSave(_currentSetup.ClientSuggestedSlot);
            AutoLoadFlow.TryAdvanceFromActiveMainMenu("Multiplayer setup begin");
            SetStatus("setup received; starting client new-save flow");
            WriteLog(MMLog.LogLevel.Info, "Received setup begin. SuggestedSlot="
                + _currentSetup.ClientSuggestedSlot + ".");
        }

        private void HandleSetupLoaded(NetworkPeer peer, byte[] payload)
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host || peer == null)
                return;

            SetupLoadedMessage loaded = SetupLoadedMessage.FromPayload(payload);
            _loadedPeers.Add(peer.PeerId);
            WriteLog(MMLog.LogLevel.Info, "Peer " + peer.PeerId + " loaded setup slot "
                + loaded.AbsoluteSlot + ".");
            TryReleaseIfReady();
        }

        private void HandleReleaseStart(byte[] payload)
        {
            ReleaseStartMessage release = ReleaseStartMessage.FromPayload(payload);
            _released = true;
            ShelteredMultiplayerHookService.Instance.SetWorldStartBlocked(false, "setup-release-" + release.WorldTick);
            SetStatus("released");
            WriteLog(MMLog.LogLevel.Info, "Setup released by host at tick " + release.WorldTick + ".");
        }

        private void OnSessionStarted()
        {
            if (_disposed || _currentSetup == null || _released || _localLoaded)
                return;

            _localLoaded = true;
            ShelteredMultiplayerHookService.Instance.SetWorldStartBlocked(true, "setup-local-loaded");

            if (_session == null)
                return;

            int slot = ResolveCurrentSlot();
            if (_session.Mode == NetworkSessionMode.Client)
            {
                SetupLoadedMessage loaded = new SetupLoadedMessage();
                loaded.SessionId = _session.SessionId;
                loaded.AbsoluteSlot = slot;
                _session.SendToHost(SetupLoadedMessageType, NetworkChannel.Reliable, loaded.ToPayload());
                SetStatus("loaded; waiting for host release");
                WriteLog(MMLog.LogLevel.Info, "Client reported setup loaded. Slot=" + slot + ".");
                return;
            }

            if (_session.Mode == NetworkSessionMode.Host)
            {
                SetStatus("host loaded; waiting for peers");
                TryReleaseIfReady();
            }
        }

        private void TryReleaseIfReady()
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host || _currentSetup == null || _released)
                return;
            if (!_localLoaded)
                return;

            if (_expectedPeers.Count == 0)
            {
                SetStatus("host loaded; waiting for peers");
                return;
            }

            NetworkPeer[] peers = _session.GetPeers();
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer != null && peer.State == NetworkConnectionState.Connected)
                    _expectedPeers.Add(peer.PeerId);
            }

            foreach (byte peerId in _expectedPeers)
            {
                if (!_loadedPeers.Contains(peerId))
                {
                    SetStatus("waiting for " + (_expectedPeers.Count - _loadedPeers.Count) + " peer(s)");
                    return;
                }
            }

            ReleaseStartMessage release = new ReleaseStartMessage();
            release.SessionId = _session.SessionId;
            long worldTick = ShelteredMultiplayer.Hooks.CurrentWorldTick;
            if (worldTick > int.MaxValue)
                worldTick = int.MaxValue;
            if (worldTick < 0)
                worldTick = 0;
            release.WorldTick = (int)worldTick;
            byte[] payload = release.ToPayload();
            _session.Broadcast(ReleaseStartMessageType, NetworkChannel.Reliable, payload);
            _released = true;
            ShelteredMultiplayerHookService.Instance.SetWorldStartBlocked(false, "setup-release-host");
            SetStatus("released");
            WriteLog(MMLog.LogLevel.Info, "All players loaded. Released multiplayer start.");
        }

        private void RememberConnectedPeers()
        {
            NetworkPeer[] peers = _session.GetPeers();
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer != null && peer.State == NetworkConnectionState.Connected)
                    _expectedPeers.Add(peer.PeerId);
            }
        }

        private static void ApplyDifficulty(MultiplayerSetupMessage setup)
        {
            DifficultyManager.StoreMenuDifficultySettings(
                setup.RainDifficulty,
                setup.ResourceDifficulty,
                setup.BreachDifficulty,
                setup.FactionDifficulty,
                setup.MoodDifficulty,
                setup.MapSize,
                setup.Fog);
        }

        private static int ResolveCurrentSlot()
        {
            try
            {
                IModSaveContext context = new ShelteredAPI.Core.ShelteredSaveRuntimeAdapter().GetCurrentSaveContext();
                return context != null ? context.SlotIndex : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static int GetNextAvailableStandardSlot()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                if (SaveRegistryCore.ReadVanillaSaveInfo(slot) == null)
                    return slot;
            }

            int customSlot = 4;
            while (ExpandedVanillaSaves.GetBySlot(customSlot) != null)
                customSlot++;
            return customSlot;
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

        private sealed class MultiplayerSetupMessage
        {
            public string SessionId = string.Empty;
            public int HostAbsoluteSlot;
            public int ClientSuggestedSlot;
            public int RainDifficulty = 1;
            public int ResourceDifficulty = 1;
            public int BreachDifficulty = 1;
            public int FactionDifficulty = 1;
            public int MoodDifficulty = 1;
            public int MapSize;
            public bool Fog;

            public static MultiplayerSetupMessage CreateDefault(string sessionId, int hostAbsoluteSlot)
            {
                MultiplayerSetupMessage message = new MultiplayerSetupMessage();
                message.SessionId = sessionId ?? string.Empty;
                message.HostAbsoluteSlot = hostAbsoluteSlot;
                message.ClientSuggestedSlot = hostAbsoluteSlot > 0 ? hostAbsoluteSlot : GetNextAvailableStandardSlot();
                return message;
            }

            public byte[] ToPayload()
            {
                byte[] buffer = new byte[512];
                BitWriter writer = new BitWriter(buffer);
                writer.WriteString(SessionId ?? string.Empty);
                writer.WriteInt32(HostAbsoluteSlot);
                writer.WriteInt32(ClientSuggestedSlot);
                writer.WriteInt32(RainDifficulty);
                writer.WriteInt32(ResourceDifficulty);
                writer.WriteInt32(BreachDifficulty);
                writer.WriteInt32(FactionDifficulty);
                writer.WriteInt32(MoodDifficulty);
                writer.WriteInt32(MapSize);
                writer.WriteBool(Fog);
                byte[] payload = new byte[writer.Position];
                Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
                return payload;
            }

            public static MultiplayerSetupMessage FromPayload(byte[] payload)
            {
                BitReader reader = new BitReader(payload, 0, payload != null ? payload.Length : 0);
                MultiplayerSetupMessage message = new MultiplayerSetupMessage();
                message.SessionId = reader.ReadString();
                message.HostAbsoluteSlot = reader.ReadInt32();
                message.ClientSuggestedSlot = reader.ReadInt32();
                message.RainDifficulty = reader.ReadInt32();
                message.ResourceDifficulty = reader.ReadInt32();
                message.BreachDifficulty = reader.ReadInt32();
                message.FactionDifficulty = reader.ReadInt32();
                message.MoodDifficulty = reader.ReadInt32();
                message.MapSize = reader.ReadInt32();
                message.Fog = reader.ReadBool();
                return message;
            }
        }

        private sealed class SetupLoadedMessage
        {
            public string SessionId = string.Empty;
            public int AbsoluteSlot;

            public byte[] ToPayload()
            {
                byte[] buffer = new byte[256];
                BitWriter writer = new BitWriter(buffer);
                writer.WriteString(SessionId ?? string.Empty);
                writer.WriteInt32(AbsoluteSlot);
                byte[] payload = new byte[writer.Position];
                Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
                return payload;
            }

            public static SetupLoadedMessage FromPayload(byte[] payload)
            {
                BitReader reader = new BitReader(payload, 0, payload != null ? payload.Length : 0);
                SetupLoadedMessage message = new SetupLoadedMessage();
                message.SessionId = reader.ReadString();
                message.AbsoluteSlot = reader.ReadInt32();
                return message;
            }
        }

        private sealed class ReleaseStartMessage
        {
            public string SessionId = string.Empty;
            public int WorldTick;

            public byte[] ToPayload()
            {
                byte[] buffer = new byte[256];
                BitWriter writer = new BitWriter(buffer);
                writer.WriteString(SessionId ?? string.Empty);
                writer.WriteInt32(WorldTick);
                byte[] payload = new byte[writer.Position];
                Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
                return payload;
            }

            public static ReleaseStartMessage FromPayload(byte[] payload)
            {
                BitReader reader = new BitReader(payload, 0, payload != null ? payload.Length : 0);
                ReleaseStartMessage message = new ReleaseStartMessage();
                message.SessionId = reader.ReadString();
                message.WorldTick = reader.ReadInt32();
                return message;
            }
        }
    }
}
