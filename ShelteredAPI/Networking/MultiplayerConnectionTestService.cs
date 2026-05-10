using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ModAPI.Core;
using ModAPI.Networking;
using ModAPI.Networking.Addressing;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Discovery;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Harmony;
using ShelteredAPI.Networking.Compatibility;
using ShelteredAPI.Networking.Diagnostics;
using ShelteredAPI.Networking.Setup;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionTestService : IDisposable
    {
        public const int DefaultPort = NetworkDefaults.DefaultPort;
        public const ushort TestMessageType = SessionMessageTypes.FirstApplicationMessageType;

        private const int MaxLogLines = 32;
        private const int MaxReceivedMessages = 32;
        private const int TestConnectionTimeoutMilliseconds = 120000;
        private const int TestHandshakeTimeoutMilliseconds = 10000;
        private const int TestDiscoveryTimeoutMilliseconds = 1500;
        private const string ApplicationId = "ShelteredAPI.MultiplayerTest";
        private const string LogSourcePrefix = "ShelteredAPI.MultiplayerTest";

        private readonly object _sync = new object();
        private readonly List<string> _log = new List<string>();
        private readonly List<string> _receivedMessages = new List<string>();
        private readonly List<string> _discoveryResults = new List<string>();

        private NetworkSession _session;
        private ShelteredMultiplayerSaveSyncService _saveSync;
        private ShelteredMultiplayerSetupService _setup;
        private ShelteredMultiplayerEventSyncService _eventSync;
        private string _lastError = string.Empty;
        private string _localEndpointText = "Not bound";
        private string _lastLocalEndpointKey = string.Empty;
        private string _cachedLanEndpoint = string.Empty;
        private int _cachedLanPort = -1;
        private int _joinRequestId;
        private bool _joining;
        private bool _discovering;
        private bool _disposed;
        private static MultiplayerConnectionTestService _active;

        public MultiplayerConnectionTestService()
        {
            _active = this;
        }

        public string Status
        {
            get
            {
                NetworkSession session = _session;
                if (session == null)
                    return "stopped";

                return session.State.ToString().ToLowerInvariant();
            }
        }

        public NetworkSessionMode Mode
        {
            get
            {
                NetworkSession session = _session;
                return session != null ? session.Mode : NetworkSessionMode.None;
            }
        }

        public NetworkSessionState SessionState
        {
            get
            {
                NetworkSession session = _session;
                return session != null ? session.State : NetworkSessionState.Stopped;
            }
        }

        public bool HasActiveSession
        {
            get { return _session != null; }
        }

        public string ConfigurationSummary
        {
            get
            {
                return "connection timeout " + (TestConnectionTimeoutMilliseconds / 1000)
                    + "s, handshake timeout " + (TestHandshakeTimeoutMilliseconds / 1000)
                    + "s, discovery timeout " + (TestDiscoveryTimeoutMilliseconds / 1000.0).ToString("0.0")
                    + "s";
            }
        }

        public string LastError
        {
            get
            {
                lock (_sync)
                {
                    return _lastError;
                }
            }
        }

        public string LocalEndpointText
        {
            get { return _localEndpointText; }
        }

        public string GetLanEndpointText(int port)
        {
            MultiplayerPortValidationResult validation = MultiplayerConnectionInputValidator.ValidatePort(port);
            if (!validation.IsValid)
                return string.Empty;

            return GetCachedLanEndpoint(validation.Port);
        }

        public string SaveSyncStatus
        {
            get { return _saveSync != null ? _saveSync.Status : "inactive"; }
        }

        public string SaveSyncLastError
        {
            get { return _saveSync != null ? _saveSync.LastError : string.Empty; }
        }

        public string SetupStatus
        {
            get { return _setup != null ? _setup.Status : "inactive"; }
        }

        public string SetupLastError
        {
            get { return _setup != null ? _setup.LastError : string.Empty; }
        }

        public MultiplayerAutoLoadStatus AutoLoadStatus
        {
            get { return AutoLoadFlow.CurrentStatus; }
        }

        public string AutoLoadStatusText
        {
            get
            {
                MultiplayerAutoLoadStatus status = AutoLoadStatus;
                return status != null ? status.DetailText : string.Empty;
            }
        }

        public string AutoLoadLastError
        {
            get
            {
                MultiplayerAutoLoadStatus status = AutoLoadStatus;
                return status != null ? status.LastError : string.Empty;
            }
        }

        public bool CanReleaseSetup
        {
            get { return _setup != null && _setup.CanHostReleaseStart; }
        }

        public bool IsDiscovering
        {
            get
            {
                lock (_sync)
                {
                    return _discovering;
                }
            }
        }

        public bool IsJoining
        {
            get
            {
                lock (_sync)
                {
                    return _joining;
                }
            }
        }

        public bool CanSendTestMessage
        {
            get
            {
                NetworkSession session = _session;
                if (session == null)
                    return false;

                if (session.Mode == NetworkSessionMode.Client)
                    return session.State == NetworkSessionState.Connected;

                if (session.Mode == NetworkSessionMode.Host)
                    return session.GetPeers().Length > 0;

                return false;
            }
        }

        public void StartHost(int port)
        {
            MultiplayerPortValidationResult validation = MultiplayerConnectionInputValidator.ValidatePort(port);
            if (!validation.IsValid)
            {
                SetLastError(validation.ErrorText);
                AddWarning("Host", "Host rejected invalid port: " + port + ".");
                return;
            }

            AddDebug("Host", "Start requested on UDP port " + validation.Port + ".");
            StopSessionOnly("Replacing existing session.");

            try
            {
                NetworkConfig config = CreateConfig(validation.Port);
                _session = new NetworkSession(config);
                AttachEvents(_session);
                EnsureSetupService(_session);
                EnsureEventSyncService(_session);
                _session.StartHost(CreateOptions("Host"));
                ShelteredMultiplayerSessionCoordinator.Instance.ActivateHost(
                    _session.SessionId,
                    1,
                    _session.LocalPeerId,
                    _session.StablePeerId,
                    20,
                    "connection-test-host-start");
                RefreshLocalEndpoint();
                AppendTimeline(
                    ShelteredMultiplayerTimelineCategory.Connection,
                    ShelteredMultiplayerTimelineEventKind.HostStarted,
                    _session.LocalPeerId,
                    "port=" + validation.Port + " endpoint=" + _localEndpointText);
                AddInfo("Host", "Host listening on UDP " + validation.Port + ". Local endpoint: " + _localEndpointText + ".");
                ClearLastError();
            }
            catch (Exception ex)
            {
                SetLastError(ex.Message);
                AddError("Host", "Host failed: " + ex.Message);
                AppendTimeline(
                    ShelteredMultiplayerTimelineCategory.Connection,
                    ShelteredMultiplayerTimelineEventKind.ConnectionFailure,
                    "host start failed: " + ex.Message);
                LogException("Host", ex, "Host start failed.");
                StopSessionOnly("Host start failed.");
            }
        }

        public void Join(string endpoint)
        {
            MultiplayerEndpointValidationResult validation =
                MultiplayerConnectionInputValidator.ValidateEndpointText(endpoint, DefaultPort);
            if (!validation.IsValid)
            {
                SetLastError(validation.ErrorText);
                AddWarning("Client", "Join rejected because endpoint was invalid: " + validation.ErrorText);
                return;
            }

            string endpointText = validation.EndpointText;
            AddDebug("Client", "Join requested for endpoint '" + endpointText + "'.");
            StopSessionOnly("Replacing existing session.");
            int requestId = BeginJoinRequest();
            AppendTimeline(
                ShelteredMultiplayerTimelineCategory.Connection,
                ShelteredMultiplayerTimelineEventKind.JoinRequested,
                "endpoint=" + endpointText);

            ModThreads.RunAsync<JoinResolutionResult>(
                delegate
                {
                    return ResolveJoinEndpoint(requestId, endpointText);
                },
                delegate(JoinResolutionResult result)
                {
                    CompleteJoin(result);
                },
                delegate(Exception ex)
                {
                    CompleteJoin(JoinResolutionResult.Failed(requestId, endpointText, ex.Message));
                });
        }

        private void CompleteJoin(JoinResolutionResult result)
        {
            if (result == null || !IsCurrentJoinRequest(result.RequestId))
                return;

            EndJoinRequest(result.RequestId);

            if (_disposed)
                return;

            if (!result.Success)
            {
                FailJoin(result.EndpointText, result.ErrorMessage, null);
                return;
            }

            try
            {
                NetworkConfig config = CreateConfig(DefaultPort);
                _session = new NetworkSession(config);
                AttachEvents(_session);
                EnsureSetupService(_session);
                EnsureEventSyncService(_session);
                _session.Join(result.EndPoint, CreateOptions("Client"));
                RefreshLocalEndpoint();
                AddInfo("Client", "Connecting to " + result.EndpointText + ". Local endpoint: " + _localEndpointText + ".");
                ClearLastError();
            }
            catch (Exception ex)
            {
                FailJoin(result.EndpointText, ex.Message, ex);
            }
        }

        public void Stop()
        {
            AddInfo("Service", "Stop requested by user.");
            StopSessionOnly("Stopped by user.");
            ClearLastError();
        }

        public void BeginSetup()
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host)
            {
                SetLastError("Only the host can begin setup.");
                AddWarning("Setup", "Begin setup requested without an active host session.");
                return;
            }

            EnsureSetupService(_session);
            ShelteredDeferredPatchTriggers.ApplySaveFlowCritical("Multiplayer host auto-new-save");
            ShelteredDeferredPatchTriggers.ApplyGameplayDeferred("Multiplayer host auto-new-save");
            AutoLoadFlow.BeginNewSave(0);
            ShelteredMultiplayerTimeline.Instance.AppendAutoLoadStateChanged(
                "host-new-save-requested",
                "preferredSlot=0 reason=BeginSetup");
            AutoLoadFlow.TryAdvanceFromActiveMainMenu("Multiplayer host setup");
            AddInfo("Setup", "Host setup will begin when the host new-save slot is chosen.");
        }

        public void ReleaseSetupStart()
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host)
            {
                SetLastError("Only the host can release setup.");
                AddWarning("Setup", "Release requested without an active host session.");
                return;
            }

            EnsureSetupService(_session);
            _setup.ReleaseStartFromHost();
        }

        public static void NotifyHostNewGameSlotChosen(int absoluteSlot)
        {
            MultiplayerConnectionTestService active = _active;
            if (active == null)
                return;

            active.BeginSetupFromSaveFlow(absoluteSlot);
        }

        public static void NotifyHostDifficultySettingsChanged(
            int rain,
            int resources,
            int breach,
            int faction,
            int mood,
            int map,
            bool fog)
        {
            MultiplayerConnectionTestService active = _active;
            if (active == null)
                return;

            active.UpdateHostSetupDifficulty(rain, resources, breach, faction, mood, map, fog);
        }

        public void Update()
        {
            if (_disposed)
                return;

            NetworkSession session = _session;
            if (session == null)
                return;

            try
            {
                session.Update();
                if (_saveSync != null)
                    _saveSync.Update();
                if (_setup != null)
                    _setup.Update();
                RefreshLocalEndpoint();
            }
            catch (Exception ex)
            {
                SetLastError(ex.Message);
                AddError("Service", "Session update failed: " + ex.Message);
                LogException("Service", ex, "Session update failed.");
            }
        }

        public void StartLanDiscovery(int port)
        {
            MultiplayerPortValidationResult validation = MultiplayerConnectionInputValidator.ValidatePort(port);
            if (!validation.IsValid)
            {
                SetLastError(validation.ErrorText);
                AddWarning("Discovery", "Discovery rejected invalid port: " + port + ".");
                return;
            }

            lock (_sync)
            {
                if (_discovering)
                    return;

                _discovering = true;
                _discoveryResults.Clear();
            }

            AddInfo("Discovery", "LAN discovery started on UDP " + validation.Port + ".");

            ModThreads.RunAsync(delegate
            {
                RunDiscovery(validation.Port);
            });
        }

        public void SendTestMessage(string message)
        {
            NetworkSession session = _session;
            if (session == null)
            {
                SetLastError("No active session.");
                AddWarning("Message", "Send requested without an active session.");
                return;
            }

            string text = string.IsNullOrEmpty(message) ? "ping" : message;
            byte[] payload = Encoding.UTF8.GetBytes(text);
            AddDebug("Message", "Sending test payload. Mode=" + session.Mode + ", State=" + session.State
                + ", Bytes=" + payload.Length + ".");

            try
            {
                if (session.Mode == NetworkSessionMode.Client)
                {
                    if (!session.SendToHost(TestMessageType, NetworkChannel.Reliable, payload))
                    {
                        SetLastError("Client is not connected to the host.");
                        AddWarning("Client", "SendToHost returned false; client is not connected.");
                        return;
                    }

                    AddInfo("Client", "Sent reliable test message to host: " + text);
                    return;
                }

                if (session.Mode == NetworkSessionMode.Host)
                {
                    int sent = session.Broadcast(TestMessageType, NetworkChannel.Reliable, payload);
                    if (sent <= 0)
                    {
                        SetLastError("No connected peers to send to.");
                        AddWarning("Host", "Broadcast found no connected peers.");
                        return;
                    }

                    AddInfo("Host", "Broadcast reliable test message to " + sent + " peer(s): " + text);
                    return;
                }

                SetLastError("Session is not connected.");
                AddWarning("Message", "Send rejected because session mode is " + session.Mode + ".");
            }
            catch (Exception ex)
            {
                SetLastError(ex.Message);
                AddError("Message", "Send failed: " + ex.Message);
                LogException("Message", ex, "Send failed.");
            }
        }

        public NetworkPeer[] GetPeers()
        {
            NetworkSession session = _session;
            if (session == null)
                return new NetworkPeer[0];

            try
            {
                return session.GetPeers();
            }
            catch
            {
                return new NetworkPeer[0];
            }
        }

        public NetworkDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            NetworkSession session = _session;
            if (session == null)
                return null;

            try
            {
                return session.GetDiagnosticsSnapshot();
            }
            catch
            {
                return null;
            }
        }

        public string[] GetLogTail()
        {
            lock (_sync)
            {
                return _log.ToArray();
            }
        }

        public string[] GetReceivedMessages()
        {
            lock (_sync)
            {
                return _receivedMessages.ToArray();
            }
        }

        public string[] GetDiscoveryResults()
        {
            lock (_sync)
            {
                return _discoveryResults.ToArray();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopSessionOnly("Runtime disposed.");
            if (ReferenceEquals(_active, this))
                _active = null;
            _disposed = true;
        }

        private static NetworkConfig CreateConfig(int port)
        {
            NetworkConfig config = NetworkConfig.CreateDefault();
            config.Port = port;
            config.AllowBroadcast = true;
            config.EnableBroadcastDiscovery = true;
            config.ConnectionTimeoutMilliseconds = TestConnectionTimeoutMilliseconds;
            config.HandshakeTimeoutMilliseconds = TestHandshakeTimeoutMilliseconds;
            config.DiscoveryTimeoutMilliseconds = TestDiscoveryTimeoutMilliseconds;
            return config;
        }

        private static JoinResolutionResult ResolveJoinEndpoint(int requestId, string endpointText)
        {
            ManualEndpointParseResult parseResult = ManualEndpointParser.Parse(endpointText, DefaultPort);
            if (!parseResult.Success)
                return JoinResolutionResult.Failed(requestId, endpointText, parseResult.Message);

            EndpointResolutionResult resolution = parseResult.Endpoint.Resolve();
            if (!resolution.Success)
                return JoinResolutionResult.Failed(requestId, endpointText, resolution.Message);

            return JoinResolutionResult.Succeeded(requestId, endpointText, resolution.EndPoint);
        }

        private static NetworkSessionOptions CreateOptions(string role)
        {
            NetworkSessionOptions options = NetworkSessionOptions.CreateDefault();
            options.ApplicationId = ApplicationId;
            options.DisplayName = CreateDisplayName(role);
            options.StablePeerId = CreateStablePeerId(role);
            options.ReconnectToken = options.StablePeerId;
            options.MaxPeers = NetworkDefaults.DefaultMaxPeers;
            string compatibilityHash = new ShelteredMultiplayerCompatibilityHasher().CaptureCurrentHash();
            options.ContentSchemaHash = compatibilityHash;
            options.ModContentHash = compatibilityHash;
            return options;
        }

        private static string CreateDisplayName(string role)
        {
            string machineName = "PC";
            try
            {
                if (!string.IsNullOrEmpty(Environment.MachineName))
                    machineName = Environment.MachineName;
            }
            catch
            {
                // GuardrailAllow: SilentCatch - machine name is display-only; fallback keeps LAN discovery usable.
            }

            return "Sheltered " + role + " - " + machineName;
        }

        private static string CreateStablePeerId(string role)
        {
            string machineName = "PC";
            try
            {
                if (!string.IsNullOrEmpty(Environment.MachineName))
                    machineName = Environment.MachineName;
            }
            catch
            {
                // GuardrailAllow: SilentCatch - machine name is display-only; fallback keeps LAN discovery usable.
            }

            return "Sheltered:" + role + ":" + machineName;
        }

        private void AttachEvents(NetworkSession session)
        {
            session.PeerConnected += OnPeerConnected;
            session.PeerDisconnected += OnPeerDisconnected;
            session.ConnectionFailed += OnConnectionFailed;
            session.MessageReceived += OnMessageReceived;
            session.SessionError += OnSessionError;
            session.TransportDisconnected += OnTransportDisconnected;
            session.TransportReconnected += OnTransportReconnected;
        }

        private void EnsureSetupService(NetworkSession session)
        {
            if (_setup != null)
                return;

            _setup = new ShelteredMultiplayerSetupService(session, AddLog);
        }

        private void EnsureEventSyncService(NetworkSession session)
        {
            if (_eventSync != null)
                return;

            _eventSync = new ShelteredMultiplayerEventSyncService(session, AddLog);
        }

        private void DetachEvents(NetworkSession session)
        {
            session.PeerConnected -= OnPeerConnected;
            session.PeerDisconnected -= OnPeerDisconnected;
            session.ConnectionFailed -= OnConnectionFailed;
            session.MessageReceived -= OnMessageReceived;
            session.SessionError -= OnSessionError;
            session.TransportDisconnected -= OnTransportDisconnected;
            session.TransportReconnected -= OnTransportReconnected;
        }

        private void StopSessionOnly(string reason)
        {
            CancelPendingJoin();
            NetworkSession session = _session;
            _session = null;
            if (_saveSync != null)
            {
                _saveSync.HandleLocalSessionEnding(reason);
                _saveSync.Dispose();
                _saveSync = null;
            }
            if (_setup != null)
            {
                _setup.HandleLocalSessionEnding(reason);
                _setup.Dispose();
                _setup = null;
            }
            if (_eventSync != null)
            {
                _eventSync.Dispose();
                _eventSync = null;
            }
            _localEndpointText = "Not bound";
            _lastLocalEndpointKey = string.Empty;
            _cachedLanEndpoint = string.Empty;
            _cachedLanPort = -1;

            if (session == null)
                return;

            try
            {
                DetachEvents(session);
                session.Dispose();
                ShelteredMultiplayer.Deactivate(reason);
                AddInfo("Service", reason);
            }
            catch (Exception ex)
            {
                SetLastError(ex.Message);
                AddError("Service", "Stop failed: " + ex.Message);
                LogException("Service", ex, "Stop failed.");
            }
        }

        private int BeginJoinRequest()
        {
            lock (_sync)
            {
                _joinRequestId++;
                _joining = true;
                return _joinRequestId;
            }
        }

        private bool IsCurrentJoinRequest(int requestId)
        {
            lock (_sync)
            {
                return _joining && requestId == _joinRequestId;
            }
        }

        private void EndJoinRequest(int requestId)
        {
            lock (_sync)
            {
                if (requestId == _joinRequestId)
                    _joining = false;
            }
        }

        private void CancelPendingJoin()
        {
            lock (_sync)
            {
                _joinRequestId++;
                _joining = false;
            }
        }

        private void FailJoin(string endpointText, string message, Exception exception)
        {
            string error = string.IsNullOrEmpty(message) ? "Unknown join failure." : message;
            SetLastError(error);
            AddError("Client", "Join failed: " + error);
            AppendTimeline(
                ShelteredMultiplayerTimelineCategory.Connection,
                ShelteredMultiplayerTimelineEventKind.ConnectionFailure,
                "join failed: endpoint=" + endpointText + " error=" + error);
            if (exception != null)
                LogException("Client", exception, "Join failed.");
            StopSessionOnly("Join failed.");
        }

        private void RunDiscovery(int port)
        {
            try
            {
                NetworkConfig config = CreateConfig(port);
                NetworkDiscoveryClient client = new NetworkDiscoveryClient(config);
                NetworkDiscoveryOptions options = NetworkDiscoveryOptions.CreateDefault();
                options.ApplicationId = ApplicationId;
                options.Port = port;
                options.TimeoutMilliseconds = config.DiscoveryTimeoutMilliseconds;

                NetworkDiscoveryResult[] results = client.DiscoverBroadcast(options);
                lock (_sync)
                {
                    for (int i = 0; i < results.Length; i++)
                    {
                        NetworkDiscoveryResult result = results[i];
                        if (result == null || result.EndPoint == null)
                            continue;

                        string formatted = FormatDiscoveryResult(result);
                        _discoveryResults.Add(formatted);
                        WritePersistentLog(MMLog.LogLevel.Debug, "Discovery", "Found LAN host: " + formatted);
                    }

                    if (_discoveryResults.Count == 0)
                    {
                        _discoveryResults.Add("No hosts found.");
                        WritePersistentLog(MMLog.LogLevel.Warning, "Discovery", "LAN discovery completed with no hosts found.");
                    }
                }

                AddInfo("Discovery", "LAN discovery finished with " + results.Length + " result(s).");
                ClearLastError();
            }
            catch (Exception ex)
            {
                SetLastError(ex.Message);
                AddError("Discovery", "LAN discovery failed: " + ex.Message);
                LogException("Discovery", ex, "LAN discovery failed.");
            }
            finally
            {
                lock (_sync)
                {
                    _discovering = false;
                }
            }
        }

        private static string FormatDiscoveryResult(NetworkDiscoveryResult result)
        {
            return result.EndPoint + " | " + result.DisplayName + " | peers "
                + result.PeerCount + "/" + result.MaxPeers;
        }

        private void RefreshLocalEndpoint()
        {
            NetworkSession session = _session;
            if (session == null || session.LocalEndPoint == null)
            {
                _localEndpointText = "Not bound";
                _lastLocalEndpointKey = string.Empty;
                return;
            }

            IPEndPoint local = session.LocalEndPoint;
            string endpointKey = local.ToString();
            if (endpointKey == _lastLocalEndpointKey)
                return;

            string endpoint = endpointKey;
            if (local.Address != null && IPAddress.Any.Equals(local.Address))
            {
                string lanEndpoint = GetCachedLanEndpoint(local.Port);
                if (lanEndpoint.Length > 0)
                    endpoint = local + " (LAN: " + lanEndpoint + ")";
            }

            _localEndpointText = endpoint;
            _lastLocalEndpointKey = endpointKey;
            AddDebug("Service", "Local endpoint changed to " + endpoint + ".");
        }

        private string GetCachedLanEndpoint(int port)
        {
            if (_cachedLanPort == port)
                return _cachedLanEndpoint;

            _cachedLanPort = port;
            _cachedLanEndpoint = string.Empty;

            LocalNetworkAddressInfo lanAddress;
            if (LocalNetworkAddressHelper.TrySelectBestLanAddress(out lanAddress) && lanAddress != null && lanAddress.Address != null)
                _cachedLanEndpoint = lanAddress.Address + ":" + port;

            return _cachedLanEndpoint;
        }

        private void OnPeerConnected(object sender, NetworkPeerEventArgs e)
        {
            NetworkPeer peer = e != null ? e.Peer : null;
            if (_saveSync != null)
                _saveSync.HandlePeerConnected(peer);
            if (_setup != null)
                _setup.HandlePeerConnected(peer);
            if (_session != null && _session.Mode == NetworkSessionMode.Client)
                EnsureSetupService(_session);
            AppendTimeline(
                ShelteredMultiplayerTimelineCategory.Connection,
                ShelteredMultiplayerTimelineEventKind.PeerConnected,
                PeerIdOrNone(peer),
                FormatPeerDetails(peer));
            AddInfo(GetComponentName(), "Peer connected: " + FormatPeerDetails(peer));
        }

        private void OnPeerDisconnected(object sender, NetworkPeerDisconnectedEventArgs e)
        {
            string message = e != null ? e.Message : string.Empty;
            if (_saveSync != null)
                _saveSync.HandlePeerDisconnected(e != null ? e.Peer : null);
            if (_setup != null)
            {
                _setup.HandlePeerDisconnected(e != null ? e.Peer : null);
                if (_session != null && _session.Mode == NetworkSessionMode.Client)
                    _setup.HandleLocalSessionEnding(string.IsNullOrEmpty(message) ? "peer-disconnected" : message);
            }
            if (_session != null && _session.Mode == NetworkSessionMode.Client)
                ShelteredMultiplayer.Deactivate(string.IsNullOrEmpty(message) ? "peer-disconnected" : message);
            AppendTimeline(
                ShelteredMultiplayerTimelineCategory.Connection,
                ShelteredMultiplayerTimelineEventKind.PeerDisconnected,
                PeerIdOrNone(e != null ? e.Peer : null),
                "reason=" + (e != null ? e.Reason.ToString() : "Unknown") + " message=" + message);
            AddWarning(GetComponentName(), "Peer disconnected: " + FormatPeerDetails(e != null ? e.Peer : null)
                + " Reason=" + (e != null ? e.Reason.ToString() : "Unknown") + " Message=" + message);
            if (!string.IsNullOrEmpty(message))
                SetLastError(message);
        }

        private void OnConnectionFailed(object sender, NetworkConnectionFailedEventArgs e)
        {
            string message = e != null ? e.Message : string.Empty;
            if (e != null && e.Exception != null && string.IsNullOrEmpty(message))
                message = e.Exception.Message;

            if (string.IsNullOrEmpty(message) && e != null)
                message = e.Reason.ToString();

            SetLastError(message);
            if (_setup != null)
                _setup.HandleLocalSessionEnding(string.IsNullOrEmpty(message) ? "connection-failed" : message);
            AppendTimeline(
                ShelteredMultiplayerTimelineCategory.Connection,
                ShelteredMultiplayerTimelineEventKind.ConnectionFailure,
                "reason=" + (e != null ? e.Reason.ToString() : "Unknown") + " message=" + message);
            AddError("Client", "Connection failed: " + message);
            if (e != null && e.Exception != null)
                LogException("Client", e.Exception, "Connection failed.");
        }

        private void OnSessionError(object sender, NetworkSessionErrorEventArgs e)
        {
            string message = e != null ? e.Context : "Session error";
            if (e != null && e.Exception != null)
                message = message + ": " + e.Exception.Message;

            SetLastError(message);
            if (e != null && e.IsFatal)
                AddError(GetComponentName(), "Fatal session error: " + message);
            else
                AddWarning(GetComponentName(), "Session warning: " + message);

            if (e != null && e.Exception != null)
                LogException(GetComponentName(), e.Exception, e.Context);
        }

        private void OnTransportDisconnected(object sender, NetworkPeerDisconnectedEventArgs e)
        {
            string message = e != null ? e.Message : string.Empty;
            AddWarning(GetComponentName(), "Transport disconnected: " + FormatPeerDetails(e != null ? e.Peer : null)
                + " Message=" + message);
        }

        private void OnTransportReconnected(object sender, NetworkTransportReconnectEventArgs e)
        {
            AppendTimeline(
                ShelteredMultiplayerTimelineCategory.Connection,
                ShelteredMultiplayerTimelineEventKind.Reconnect,
                PeerIdOrNone(e != null ? e.Peer : null),
                "previousPeerId=" + (e != null ? e.PreviousPeerId.ToString() : "unknown")
                    + " " + FormatPeerDetails(e != null ? e.Peer : null));
            AddInfo(GetComponentName(), "Transport reconnected: " + FormatPeerDetails(e != null ? e.Peer : null)
                + " PreviousPeerId=" + (e != null ? e.PreviousPeerId.ToString() : "unknown"));
        }

        private void OnMessageReceived(object sender, NetworkMessageReceivedEventArgs e)
        {
            if (e == null)
                return;

            if (_saveSync != null && _saveSync.TryHandleMessage(e.Peer, e.MessageType, e.Payload))
                return;
            if (_setup != null && _setup.TryHandleMessage(e.Peer, e.MessageType, e.Payload))
                return;

            if (e.MessageType != TestMessageType)
                return;

            string text = DecodeUtf8(e.Payload);
            string line = FormatPeer(e.Peer) + ": " + text;

            lock (_sync)
            {
                AddBounded(_receivedMessages, Timestamp() + " " + line, MaxReceivedMessages);
            }

            AddInfo(GetComponentName(), "Received reliable test message from " + FormatPeer(e.Peer)
                + ". Bytes=" + (e.Payload != null ? e.Payload.Length : 0) + ".");
            AddDebug(GetComponentName(), "Received test payload: " + text);
        }

        private static string DecodeUtf8(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(payload);
            }
            catch
            {
                return "<invalid utf8 payload>";
            }
        }

        private static string FormatPeer(NetworkPeer peer)
        {
            if (peer == null)
                return "unknown peer";

            string endPoint = peer.EndPoint != null ? peer.EndPoint.ToString() : "unknown endpoint";
            return "#" + peer.PeerId + " " + endPoint;
        }

        private static string FormatPeerDetails(NetworkPeer peer)
        {
            if (peer == null)
                return "unknown peer";

            string displayName = peer.DisplayName ?? string.Empty;
            string lastError = peer.LastError ?? string.Empty;
            return FormatPeer(peer) + " State=" + peer.State + " IsHost=" + peer.IsHost
                + " DisplayName='" + displayName + "' LastError='" + lastError + "'";
        }

        private string GetComponentName()
        {
            NetworkSession session = _session;
            if (session == null)
                return "Service";

            if (session.Mode == NetworkSessionMode.Host)
                return "Host";
            if (session.Mode == NetworkSessionMode.Client)
                return "Client";

            return "Service";
        }

        private void AddDebug(string component, string line)
        {
            AddLog(MMLog.LogLevel.Debug, component, line);
        }

        private void AddInfo(string component, string line)
        {
            AddLog(MMLog.LogLevel.Info, component, line);
        }

        private void AddWarning(string component, string line)
        {
            AddLog(MMLog.LogLevel.Warning, component, line);
        }

        private void AddError(string component, string line)
        {
            AddLog(MMLog.LogLevel.Error, component, line);
        }

        private void AddLog(MMLog.LogLevel level, string component, string line)
        {
            lock (_sync)
            {
                AddBounded(_log, Timestamp() + " [" + level + "] [" + component + "] "
                    + (line ?? string.Empty), MaxLogLines);
            }

            WritePersistentLog(level, component, line);
        }

        private static void WritePersistentLog(MMLog.LogLevel level, string component, string line)
        {
            MMLog.WriteWithSource(level, MMLog.LogCategory.General, LogSourcePrefix + "." + component,
                line ?? string.Empty);
        }

        private static void LogException(string component, Exception exception, string context)
        {
            if (exception == null)
                return;

            MMLog.WriteException(exception, LogSourcePrefix + "." + component + ": " + (context ?? string.Empty),
                MMLog.LogCategory.General);
        }

        private static void AddBounded(List<string> list, string line, int max)
        {
            list.Add(line);
            while (list.Count > max)
                list.RemoveAt(0);
        }

        private void SetLastError(string message)
        {
            lock (_sync)
            {
                _lastError = message ?? string.Empty;
            }
        }

        private void BeginSetupFromSaveFlow(int absoluteSlot)
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host)
                return;

            EnsureSetupService(_session);
            ShelteredMultiplayerTimeline.Instance.AppendAutoLoadStateChanged(
                "host-slot-chosen",
                "slot=" + absoluteSlot);
            _setup.BeginHostSetup(absoluteSlot);
        }

        private void UpdateHostSetupDifficulty(
            int rain,
            int resources,
            int breach,
            int faction,
            int mood,
            int map,
            bool fog)
        {
            if (_session == null || _session.Mode != NetworkSessionMode.Host || _setup == null)
                return;

            _setup.UpdateHostDifficultySettings(
                rain,
                resources,
                breach,
                faction,
                mood,
                map,
                fog,
                "setup-difficulty-store");
        }

        private void ClearLastError()
        {
            SetLastError(string.Empty);
        }

        private static string Timestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        private static int PeerIdOrNone(NetworkPeer peer)
        {
            return peer != null ? peer.PeerId : ShelteredMultiplayerTimeline.NoNetworkPeer;
        }

        private sealed class JoinResolutionResult
        {
            private JoinResolutionResult(
                int requestId,
                string endpointText,
                IPEndPoint endPoint,
                bool success,
                string errorMessage)
            {
                RequestId = requestId;
                EndpointText = endpointText ?? string.Empty;
                EndPoint = endPoint;
                Success = success;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public int RequestId;
            public string EndpointText;
            public IPEndPoint EndPoint;
            public bool Success;
            public string ErrorMessage;

            public static JoinResolutionResult Succeeded(int requestId, string endpointText, IPEndPoint endPoint)
            {
                return new JoinResolutionResult(requestId, endpointText, endPoint, true, string.Empty);
            }

            public static JoinResolutionResult Failed(int requestId, string endpointText, string errorMessage)
            {
                return new JoinResolutionResult(requestId, endpointText, null, false, errorMessage);
            }
        }

        private static void AppendTimeline(
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            string message)
        {
            ShelteredMultiplayerTimeline.Instance.Append(category, eventKind, message);
        }

        private static void AppendTimeline(
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            int networkPeerId,
            string message)
        {
            ShelteredMultiplayerTimeline.Instance.Append(category, eventKind, networkPeerId, message);
        }
    }
}
