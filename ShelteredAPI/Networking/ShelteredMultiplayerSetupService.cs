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
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerSetupService : IDisposable
    {
        public const ushort BeginSetupMessageType = SessionMessageTypes.FirstApplicationMessageType + 32;
        public const ushort SetupLoadedMessageType = SessionMessageTypes.FirstApplicationMessageType + 33;
        public const ushort ReleaseStartMessageType = SessionMessageTypes.FirstApplicationMessageType + 34;

        private const string LogComponent = "Setup";
        private const string StatusIdle = "idle";
        private const string StatusWaitingHostStartup = "waiting for host startup to finish";
        private const string StatusWaitingPeers = "host loaded; waiting for peers";
        private const string StatusAllLoaded = "all players loaded; waiting for host release";
        private const string StatusClientLoading = "client loading";
        private const string StatusLoadedWaitingRelease = "loaded; waiting for host release";
        private const string StatusStaleSetupIgnored = "stale setup ignored";
        private const string StatusSetupLoadedIgnored = "setup-loaded ignored";
        private const string StatusReleaseIgnored = "release ignored";
        private const string StatusReleased = "released";
        private const string StatusCancelled = "cancelled";

        private readonly NetworkSession _session;
        private readonly SetupLogSink _log;
        private readonly HashSet<byte> _loadedPeers = new HashSet<byte>();
        private readonly HashSet<byte> _expectedPeers = new HashSet<byte>();

        private MultiplayerSetupMessage _currentSetup;
        private string _status = StatusIdle;
        private string _lastError = string.Empty;
        private bool _localLoaded;
        private bool _released;
        private bool _disposed;

        internal ShelteredMultiplayerSetupService(NetworkSession session, SetupLogSink log)
        {
            _session = session;
            _log = log;
            ShelteredMultiplayerHookService.Instance.EnsureInstalled();
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

        public bool CanHostReleaseStart
        {
            get { return IsHostReadyForRelease(); }
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

            ShelteredMultiplayerSessionCoordinator coordinator = ShelteredMultiplayerSessionCoordinator.Instance;
            coordinator.ActivateHost(
                _session.SessionId,
                1,
                _session.LocalPeerId,
                _session.StablePeerId,
                20,
                "setup-begin-host");
            coordinator.UpdateRoster(_session, "setup-begin-host");

            _currentSetup = MultiplayerSetupMessage.CreateDefault(coordinator.Context.SessionId, hostAbsoluteSlot);
            _loadedPeers.Clear();
            _expectedPeers.Clear();
            _localLoaded = false;
            _released = false;

            RememberConnectedPeers();
            PrepareHostSetup("setup-begin-host");
            BroadcastBeginSetup();
            SetStatus(StatusWaitingHostStartup);
            WriteLog(MMLog.LogLevel.Info, "Host setup started. Slot=" + hostAbsoluteSlot + ".");
        }

        public void HandlePeerConnected(NetworkPeer peer)
        {
            if (_disposed || peer == null || _session == null || _session.Mode != NetworkSessionMode.Host)
                return;

            if (_currentSetup == null || _released)
                return;

            _expectedPeers.Add(peer.PeerId);
            ShelteredMultiplayerSessionCoordinator.Instance.UpdateRoster(_session, "setup-peer-connected");
            PrepareHostSetup("setup-peer-connected");
            BroadcastBeginSetup();
            UpdateHostReadyStatus();
            WriteLog(MMLog.LogLevel.Info, "Peer " + peer.PeerId + " joined active setup. " + BuildSetupProgress() + ".");
        }

        public void HandlePeerDisconnected(NetworkPeer peer)
        {
            if (_disposed || peer == null)
                return;

            bool wasExpected = _expectedPeers.Contains(peer.PeerId);
            _loadedPeers.Remove(peer.PeerId);

            if (_currentSetup != null && !_released && wasExpected)
            {
                ShelteredMultiplayerSessionCoordinator.Instance.MarkPeerDisconnected(peer.PeerId, "setup-peer-disconnected");
                PrepareHostSetup("setup-peer-disconnected");
                BroadcastBeginSetup();
                UpdateHostReadyStatus();
                WriteLog(MMLog.LogLevel.Warning, "Peer " + peer.PeerId
                    + " disconnected during setup; keeping world start blocked.");
            }
            else if (_session != null && _session.Mode == NetworkSessionMode.Host && _currentSetup != null && !_released)
            {
                _expectedPeers.Remove(peer.PeerId);
                ShelteredMultiplayerSessionCoordinator.Instance.UpdateRoster(_session, "setup-peer-disconnected");
                PrepareHostSetup("setup-peer-disconnected");
                BroadcastBeginSetup();
                UpdateHostReadyStatus();
            }
        }

        public void UpdateHostDifficultySettings(
            int rain,
            int resources,
            int breach,
            int faction,
            int mood,
            int map,
            bool fog,
            string reason)
        {
            if (_disposed || _session == null || _session.Mode != NetworkSessionMode.Host || _currentSetup == null || _released)
                return;

            _currentSetup.ApplySetupSettings(new ShelteredMultiplayerSetupSettings(
                _currentSetup.HostAbsoluteSlot,
                _currentSetup.ClientSuggestedSlot,
                rain,
                resources,
                breach,
                faction,
                mood,
                map,
                fog));

            PrepareHostSetup(string.IsNullOrEmpty(reason) ? "setup-difficulty-updated" : reason);
            BroadcastBeginSetup();
            WriteLog(MMLog.LogLevel.Info, "Host setup difficulty updated and broadcast. Rain=" + rain
                + ", Resources=" + resources + ", Breach=" + breach + ", Faction=" + faction
                + ", Mood=" + mood + ", Map=" + map + ", Fog=" + fog + ".");
        }

        public void ReleaseStartFromHost()
        {
            if (_disposed || _session == null || _session.Mode != NetworkSessionMode.Host)
            {
                SetError("Only the host can release multiplayer start.");
                return;
            }

            if (!IsHostReadyForRelease())
            {
                SetStatus("not ready for host release; " + BuildWaitingReason());
                WriteLog(MMLog.LogLevel.Warning, "Host release requested before setup was ready. " + BuildSetupProgress()
                    + ", reason=" + BuildWaitingReason() + ".");
                return;
            }

            ReleaseStart("setup-release-host-confirmed");
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

            ClearSetupState();
            GameEvents.OnSessionStarted -= OnSessionStarted;
            _disposed = true;
        }

        public void HandleLocalSessionEnding(string reason)
        {
            if (_disposed)
                return;

            if (_currentSetup == null && _loadedPeers.Count == 0 && _expectedPeers.Count == 0)
                return;

            ClearSetupState();
            SetStatus(StatusCancelled);
            WriteLog(MMLog.LogLevel.Info, "Setup cancelled. Reason=" + (reason ?? string.Empty) + ".");
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

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            int localPlayerId = context.GetPlayerIdForNetworkPeer(peerId);
            _session.SendToPeer(peerId, BeginSetupMessageType, NetworkChannel.Reliable, _currentSetup.ToPayload(context, localPlayerId));
            WriteLog(MMLog.LogLevel.Info, "Sent setup begin to peer " + peerId
                + " as player " + localPlayerId + ".");
        }

        private void HandleBeginSetup(byte[] payload)
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Client)
                return;

            MultiplayerSetupMessage received = MultiplayerSetupMessage.FromPayload(payload);
            string expectedSessionId = ResolveCurrentSetupSessionId();
            if (!IsCurrentSetupSession(received.SessionId, expectedSessionId))
            {
                SetWarningStatus(StatusStaleSetupIgnored,
                    "Ignored setup for stale session '" + LoadingText(received.SessionId)
                    + "' while current session is '" + LoadingText(expectedSessionId) + "'.");
                WriteLog(MMLog.LogLevel.Warning, "Ignoring setup begin for stale session '"
                    + LoadingText(received.SessionId) + "'. Expected '" + LoadingText(expectedSessionId) + "'.");
                return;
            }

            bool shouldStartNewSave = _currentSetup == null
                || !string.Equals(_currentSetup.SessionId, received.SessionId, StringComparison.Ordinal);

            _currentSetup = received;
            if (shouldStartNewSave)
                _localLoaded = false;
            _released = false;

            ShelteredMultiplayerSessionContext context =
                ShelteredMultiplayerSessionCoordinator.Instance.ApplyReceivedSetup(
                    _currentSetup.SessionId,
                    _currentSetup.LocalPlayerId,
                    _session.LocalPeerId,
                    _session.StablePeerId,
                    _currentSetup.ToSetupSettings(),
                    _currentSetup.ToBunkerAssignments(),
                    "setup-begin-client");

            if (shouldStartNewSave)
            {
                ShelteredDeferredPatchTriggers.ApplySaveFlowCritical("Multiplayer setup client auto-new-save");
                ShelteredDeferredPatchTriggers.ApplyGameplayDeferred("Multiplayer setup client auto-new-save");
                AutoLoadFlow.BeginNewSave(context.SetupSettings.ClientSuggestedSlot);
                AutoLoadFlow.TryAdvanceFromActiveMainMenu("Multiplayer setup begin");
                SetStatus(StatusClientLoading);
            }
            else
            {
                SetStatus(_localLoaded ? StatusLoadedWaitingRelease : StatusClientLoading);
            }

            WriteLog(MMLog.LogLevel.Info, "Received setup begin. SuggestedSlot="
                + context.SetupSettings.ClientSuggestedSlot
                + ", restart=" + shouldStartNewSave
                + ", session='" + LoadingText(_currentSetup.SessionId) + "'"
                + ", localPlayer=" + _currentSetup.LocalPlayerId
                + ", hostSlot=" + _currentSetup.HostAbsoluteSlot
                + ", bunkers=" + _currentSetup.Bunkers.Count + ".");
        }

        private void HandleSetupLoaded(NetworkPeer peer, byte[] payload)
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host || peer == null)
                return;

            SetupLoadedMessage loaded = SetupLoadedMessage.FromPayload(payload);
            if (_currentSetup == null)
            {
                SetWarningStatus(StatusSetupLoadedIgnored,
                    "Ignored setup-loaded from peer " + peer.PeerId + " because setup has not started.");
                WriteLog(MMLog.LogLevel.Warning, "Ignoring setup-loaded from peer " + peer.PeerId
                    + " because setup has not started.");
                return;
            }

            if (!string.Equals(loaded.SessionId, _currentSetup.SessionId, StringComparison.Ordinal))
            {
                SetWarningStatus(StatusSetupLoadedIgnored,
                    "Ignored setup-loaded from peer " + peer.PeerId + " for stale session '"
                    + LoadingText(loaded.SessionId) + "'.");
                WriteLog(MMLog.LogLevel.Warning, "Ignoring setup-loaded from peer " + peer.PeerId
                    + " for stale session '" + loaded.SessionId + "'.");
                return;
            }

            if (!_expectedPeers.Contains(peer.PeerId))
            {
                SetWarningStatus(StatusSetupLoadedIgnored,
                    "Ignored setup-loaded from unexpected peer " + peer.PeerId + ".");
                WriteLog(MMLog.LogLevel.Warning, "Ignoring setup-loaded from unexpected peer " + peer.PeerId + ".");
                return;
            }

            _loadedPeers.Add(peer.PeerId);
            WriteLog(MMLog.LogLevel.Info, "Peer " + peer.PeerId + " loaded setup slot "
                + loaded.AbsoluteSlot + ". " + BuildSetupProgress() + ".");
            UpdateHostReadyStatus();
        }

        private void HandleReleaseStart(byte[] payload)
        {
            ReleaseStartMessage release = ReleaseStartMessage.FromPayload(payload);
            if (_currentSetup == null)
            {
                SetWarningStatus(StatusReleaseIgnored,
                    "Ignored release for session '" + LoadingText(release.SessionId) + "' because setup has not started.");
                WriteLog(MMLog.LogLevel.Warning, "Ignoring release for session '" + release.SessionId
                    + "' because setup has not started.");
                return;
            }

            if (!string.Equals(release.SessionId, _currentSetup.SessionId, StringComparison.Ordinal))
            {
                SetWarningStatus(StatusReleaseIgnored,
                    "Ignored release for stale session '" + LoadingText(release.SessionId) + "'.");
                WriteLog(MMLog.LogLevel.Warning, "Ignoring release for stale session '" + release.SessionId + "'.");
                return;
            }

            _released = true;
            ShelteredMultiplayerSessionCoordinator.Instance.ReleaseWorldStart("setup-release-" + release.WorldTick);
            SetStatus(StatusReleased);
            WriteLog(MMLog.LogLevel.Info, "Setup released by host at tick " + release.WorldTick + ".");
        }

        private void OnSessionStarted()
        {
            if (_disposed || _currentSetup == null || _released || _localLoaded)
                return;

            _localLoaded = true;
            ShelteredMultiplayerSessionContext context =
                ShelteredMultiplayerSessionCoordinator.Instance.MarkLocalWorldLoaded("setup-local-loaded");

            if (_session == null)
                return;

            int slot = ResolveCurrentSlot();
            if (_session.Mode == NetworkSessionMode.Client)
            {
                SetupLoadedMessage loaded = new SetupLoadedMessage();
                loaded.SessionId = context.SessionId;
                loaded.AbsoluteSlot = slot;
                _session.SendToHost(SetupLoadedMessageType, NetworkChannel.Reliable, loaded.ToPayload());
                SetStatus(StatusLoadedWaitingRelease);
                WriteLog(MMLog.LogLevel.Info, "Client reported setup loaded. Slot=" + slot + ".");
                return;
            }

            if (_session.Mode == NetworkSessionMode.Host)
            {
                WriteLog(MMLog.LogLevel.Info, "Host world loaded for setup. Slot=" + slot + ". " + BuildSetupProgress() + ".");
                UpdateHostReadyStatus();
            }
        }

        private void UpdateHostReadyStatus()
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host || _currentSetup == null || _released)
                return;
            if (!_localLoaded)
            {
                SetStatus(StatusWaitingHostStartup);
                WriteLog(MMLog.LogLevel.Info, "Setup waiting for host startup. " + BuildSetupProgress() + ".");
                return;
            }

            if (_expectedPeers.Count == 0)
            {
                SetStatus(StatusWaitingPeers);
                WriteLog(MMLog.LogLevel.Info, "Setup waiting for peers after host loaded. " + BuildSetupProgress() + ".");
                return;
            }

            RememberConnectedPeers();

            foreach (byte peerId in _expectedPeers)
            {
                if (!_loadedPeers.Contains(peerId))
                {
                    SetStatus("waiting for " + CountUnloadedExpectedPeers() + " peer(s)");
                    WriteLog(MMLog.LogLevel.Info, "Setup waiting for peer " + peerId + " to report loaded. " + BuildSetupProgress() + ".");
                    return;
                }
            }

            SetStatus(StatusAllLoaded);
            WriteLog(MMLog.LogLevel.Info, "All setup participants loaded; waiting for host to press Everyone Loaded. " + BuildSetupProgress() + ".");
        }

        private bool IsHostReadyForRelease()
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host || _currentSetup == null || _released || !_localLoaded)
                return false;

            if (_expectedPeers.Count == 0)
                return false;

            RememberConnectedPeers();

            foreach (byte peerId in _expectedPeers)
            {
                if (!_loadedPeers.Contains(peerId))
                    return false;
            }

            return true;
        }

        private string BuildWaitingReason()
        {
            if (_currentSetup == null)
                return "setup has not started";
            if (_released)
                return "start was already released";
            if (!_localLoaded)
                return "host is not loaded";
            if (_expectedPeers.Count == 0)
                return "no clients are connected";

            int remaining = CountUnloadedExpectedPeers();
            if (remaining > 0)
                return "waiting for " + remaining + " peer(s)";

            return "setup is not ready";
        }

        private void ReleaseStart(string reason)
        {
            ReleaseStartMessage release = new ReleaseStartMessage();
            release.SessionId = ShelteredMultiplayerSessionCoordinator.Instance.Context.SessionId;
            long worldTick = ShelteredMultiplayer.Hooks.CurrentWorldTick;
            if (worldTick > int.MaxValue)
                worldTick = int.MaxValue;
            if (worldTick < 0)
                worldTick = 0;
            release.WorldTick = (int)worldTick;
            byte[] payload = release.ToPayload();
            _session.Broadcast(ReleaseStartMessageType, NetworkChannel.Reliable, payload);
            _released = true;
            ShelteredMultiplayerSessionCoordinator.Instance.ReleaseWorldStart(reason);
            SetStatus(StatusReleased);
            WriteLog(MMLog.LogLevel.Info, "Host released multiplayer start after all players loaded.");
        }

        private void PrepareHostSetup(string reason)
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host || _currentSetup == null)
                return;

            ShelteredMultiplayerSessionContext context =
                ShelteredMultiplayerSessionCoordinator.Instance.BeginSetupPreparation(
                    _currentSetup.ToSetupSettings(),
                    reason);
            _currentSetup.SessionId = context.SessionId;
            _currentSetup.LocalPlayerId = context.LocalPlayerId;
            WriteLog(MMLog.LogLevel.Info, "Prepared " + context.BunkerAssignments.Length
                + " bunker assignment(s) for multiplayer setup. session='" + LoadingText(_currentSetup.SessionId)
                + "', reason=" + LoadingText(reason) + ".");
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

        private int CountUnloadedExpectedPeers()
        {
            int count = 0;
            foreach (byte peerId in _expectedPeers)
            {
                if (!_loadedPeers.Contains(peerId))
                    count++;
            }

            return count;
        }

        private string ResolveCurrentSetupSessionId()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context != null && context.IsMultiplayerActive && !string.IsNullOrEmpty(context.SessionId))
                return context.SessionId;

            return _session != null ? _session.SessionId : string.Empty;
        }

        private static bool IsCurrentSetupSession(string receivedSessionId, string expectedSessionId)
        {
            if (string.IsNullOrEmpty(expectedSessionId))
                return true;

            return string.Equals(receivedSessionId ?? string.Empty, expectedSessionId, StringComparison.Ordinal);
        }

        private void ClearSetupState()
        {
            _currentSetup = null;
            _loadedPeers.Clear();
            _expectedPeers.Clear();
            _localLoaded = false;
            _released = false;
        }

        private string BuildSetupProgress()
        {
            return "session='" + (_currentSetup != null ? LoadingText(_currentSetup.SessionId) : string.Empty) + "'"
                + ", localLoaded=" + _localLoaded
                + ", expectedPeers=" + FormatPeers(_expectedPeers)
                + ", loadedPeers=" + FormatPeers(_loadedPeers)
                + ", released=" + _released;
        }

        private static string FormatPeers(HashSet<byte> peers)
        {
            if (peers == null || peers.Count == 0)
                return "[]";

            string[] values = new string[peers.Count];
            int index = 0;
            foreach (byte peer in peers)
                values[index++] = peer.ToString();

            return "[" + string.Join(",", values) + "]";
        }

        private static string LoadingText(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
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

        private void SetWarningStatus(string status, string message)
        {
            _status = status ?? string.Empty;
            _lastError = message ?? string.Empty;
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
            public int LocalPlayerId = 1;
            public int RainDifficulty = 1;
            public int ResourceDifficulty = 1;
            public int BreachDifficulty = 1;
            public int FactionDifficulty = 1;
            public int MoodDifficulty = 1;
            public int MapSize;
            public bool Fog;
            public readonly List<BunkerSetupRecord> Bunkers = new List<BunkerSetupRecord>();

            public static MultiplayerSetupMessage CreateDefault(string sessionId, int hostAbsoluteSlot)
            {
                MultiplayerSetupMessage message = new MultiplayerSetupMessage();
                message.SessionId = sessionId ?? string.Empty;
                message.HostAbsoluteSlot = hostAbsoluteSlot;
                message.ClientSuggestedSlot = 0;
                message.ApplySetupSettings(CaptureCurrentSetupSettings(hostAbsoluteSlot, message.ClientSuggestedSlot));
                return message;
            }

            public void ApplySetupSettings(ShelteredMultiplayerSetupSettings setup)
            {
                if (setup == null)
                    return;

                HostAbsoluteSlot = setup.HostAbsoluteSlot;
                ClientSuggestedSlot = setup.ClientSuggestedSlot;
                RainDifficulty = setup.RainDifficulty;
                ResourceDifficulty = setup.ResourceDifficulty;
                BreachDifficulty = setup.BreachDifficulty;
                FactionDifficulty = setup.FactionDifficulty;
                MoodDifficulty = setup.MoodDifficulty;
                MapSize = setup.MapSize;
                Fog = setup.Fog;
            }

            public ShelteredMultiplayerSetupSettings ToSetupSettings()
            {
                return new ShelteredMultiplayerSetupSettings(
                    HostAbsoluteSlot,
                    ClientSuggestedSlot,
                    RainDifficulty,
                    ResourceDifficulty,
                    BreachDifficulty,
                    FactionDifficulty,
                    MoodDifficulty,
                    MapSize,
                    Fog);
            }

            public ShelteredMultiplayerBunkerAssignmentRecord[] ToBunkerAssignments()
            {
                List<ShelteredMultiplayerBunkerAssignmentRecord> assignments =
                    new List<ShelteredMultiplayerBunkerAssignmentRecord>();

                for (int i = 0; i < Bunkers.Count; i++)
                {
                    BunkerSetupRecord bunker = Bunkers[i];
                    if (bunker == null)
                        continue;

                    assignments.Add(new ShelteredMultiplayerBunkerAssignmentRecord(
                        ToNetworkPeerId(bunker.PeerId),
                        bunker.PlayerId,
                        bunker.BunkerOwnerId,
                        new Vector2(bunker.X, bunker.Y),
                        bunker.DisplayName,
                        bunker.IsOnline));
                }

                return assignments.ToArray();
            }

            public byte[] ToPayload(ShelteredMultiplayerSessionContext context, int localPlayerId)
            {
                byte[] buffer = new byte[1024];
                BitWriter writer = new BitWriter(buffer);
                ShelteredMultiplayerSetupSettings setup = context != null ? context.SetupSettings : ToSetupSettings();
                writer.WriteString(context != null ? context.SessionId : (SessionId ?? string.Empty));
                writer.WriteInt32(setup.HostAbsoluteSlot);
                writer.WriteInt32(setup.ClientSuggestedSlot);
                writer.WriteInt32(localPlayerId);
                writer.WriteInt32(setup.RainDifficulty);
                writer.WriteInt32(setup.ResourceDifficulty);
                writer.WriteInt32(setup.BreachDifficulty);
                writer.WriteInt32(setup.FactionDifficulty);
                writer.WriteInt32(setup.MoodDifficulty);
                writer.WriteInt32(setup.MapSize);
                writer.WriteBool(setup.Fog);
                ShelteredMultiplayerBunkerAssignmentRecord[] bunkers =
                    context != null ? context.BunkerAssignments : ToBunkerAssignments();
                writer.WriteInt32(bunkers.Length);
                for (int i = 0; i < bunkers.Length; i++)
                {
                    ShelteredMultiplayerBunkerAssignmentRecord bunker = bunkers[i];
                    writer.WriteInt32(bunker != null ? bunker.NetworkPeerId : NetworkDefaults.UnassignedPeerId);
                    writer.WriteInt32(bunker != null ? bunker.PlayerId : 0);
                    writer.WriteInt32(bunker != null ? bunker.BunkerOwnerId : 0);
                    writer.WriteInt32(bunker != null ? ToNetworkCoordinate(bunker.Position.x) : 0);
                    writer.WriteInt32(bunker != null ? ToNetworkCoordinate(bunker.Position.y) : 0);
                    writer.WriteString(bunker != null ? bunker.DisplayName : string.Empty);
                    writer.WriteBool(true);
                    writer.WriteBool(bunker == null || bunker.IsOnline);
                }

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
                message.LocalPlayerId = reader.ReadInt32();
                message.RainDifficulty = reader.ReadInt32();
                message.ResourceDifficulty = reader.ReadInt32();
                message.BreachDifficulty = reader.ReadInt32();
                message.FactionDifficulty = reader.ReadInt32();
                message.MoodDifficulty = reader.ReadInt32();
                message.MapSize = reader.ReadInt32();
                message.Fog = reader.ReadBool();
                int bunkerCount = reader.Remaining > 0 ? reader.ReadInt32() : 0;
                if (bunkerCount < 0 || bunkerCount > NetworkDefaults.DefaultMaxPeers)
                    throw new InvalidOperationException("Setup bunker assignment count is invalid.");

                for (int i = 0; i < bunkerCount; i++)
                {
                    int peerId = reader.ReadInt32();
                    int playerId = reader.ReadInt32();
                    int bunkerOwnerId = reader.ReadInt32();
                    float x = FromNetworkCoordinate(reader.ReadInt32());
                    float y = FromNetworkCoordinate(reader.ReadInt32());
                    string displayName = reader.ReadString();
                    bool starterHouses = reader.ReadBool();
                    bool isOnline = reader.ReadBool();
                    message.Bunkers.Add(new BunkerSetupRecord(peerId, playerId, bunkerOwnerId, x, y, displayName, starterHouses, isOnline));
                }

                return message;
            }

            private static int ToNetworkCoordinate(float value)
            {
                return (int)Math.Round(value * 1000f);
            }

            private static float FromNetworkCoordinate(int value)
            {
                return value / 1000f;
            }

            private static byte ToNetworkPeerId(int value)
            {
                return value >= 0 && value <= byte.MaxValue
                    ? (byte)value
                    : NetworkDefaults.UnassignedPeerId;
            }
        }

        private static ShelteredMultiplayerSetupSettings CaptureCurrentSetupSettings(int hostAbsoluteSlot, int clientSuggestedSlot)
        {
            try
            {
                return new ShelteredMultiplayerSetupSettings(
                    hostAbsoluteSlot,
                    clientSuggestedSlot,
                    DifficultyManager.RainChanceSetting,
                    DifficultyManager.MapResourcesSetting,
                    DifficultyManager.BreachFrequencySetting,
                    DifficultyManager.FactionDensitySetting,
                    DifficultyManager.PopulaceMoodSetting,
                    DifficultyManager.MapSizeSetting,
                    DifficultyManager.FogOfWarSetting);
            }
            catch
            {
                return new ShelteredMultiplayerSetupSettings(hostAbsoluteSlot, clientSuggestedSlot, 1, 1, 1, 1, 1, 0, false);
            }
        }

        private sealed class BunkerSetupRecord
        {
            public BunkerSetupRecord(int peerId, int playerId, int bunkerOwnerId, float x, float y, string displayName, bool enableStarterHouses, bool isOnline)
            {
                PeerId = peerId;
                PlayerId = playerId;
                BunkerOwnerId = bunkerOwnerId;
                X = x;
                Y = y;
                DisplayName = displayName ?? string.Empty;
                EnableStarterHouses = enableStarterHouses;
                IsOnline = isOnline;
            }

            public readonly int PeerId;
            public readonly int PlayerId;
            public readonly int BunkerOwnerId;
            public readonly float X;
            public readonly float Y;
            public readonly string DisplayName;
            public readonly bool EnableStarterHouses;
            public readonly bool IsOnline;
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
