using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Cortex.Bridge;
using Cortex.Host.Avalonia.Composition;
using Serilog;

namespace Cortex.Host.Avalonia.Bridge
{
    internal sealed class NamedPipeDesktopBridgeClient : IDisposable
    {
        private readonly DesktopBridgeClientOptions _options;
        private readonly string _launchToken;
        private readonly object _writeSync = new object();
        private readonly OverlayRevisionTracker _workbenchRevisionTracker = new OverlayRevisionTracker();
        private readonly OverlayRevisionTracker _overlayRevisionTracker = new OverlayRevisionTracker();
        private CancellationTokenSource _cancellation;
        private Task _connectionTask;
        private NamedPipeClientStream _stream;
        private string _sessionId = string.Empty;
        private BridgeCapabilitySet _capabilities = new BridgeCapabilitySet();
        private string _lastConnectionStatus = string.Empty;
        private string _lastFailureMessage = string.Empty;
        private string _lastOutboundFailure = string.Empty;
        private bool _loggedFirstWorkbenchSnapshot;
        private bool _loggedFirstOverlaySnapshot;

        public NamedPipeDesktopBridgeClient(DesktopBridgeClientOptions options, string launchToken)
        {
            _options = options ?? new DesktopBridgeClientOptions();
            _launchToken = launchToken ?? string.Empty;
            if (string.IsNullOrEmpty(_options.PipeName))
            {
                _options.PipeName = DesktopBridgeProtocol.DefaultPipeName;
            }

            if (string.IsNullOrEmpty(_options.ClientDisplayName))
            {
                _options.ClientDisplayName = DesktopBridgeProtocol.DefaultClientDisplayName;
            }
        }

        public event Action<string> ConnectionStatusChanged;
        public event Action<string> OperationStatusReceived;
        public event Action<WorkbenchBridgeSnapshot> SnapshotReceived;
        public event Action<OverlayPresentationSnapshot> OverlaySnapshotReceived;
        public event Action<OverlayHostLifecycleMessage> OverlayLifecycleReceived;

        public void Start()
        {
            if (_connectionTask != null)
            {
                return;
            }

            Log.Information("Starting desktop bridge client. PipeName={PipeName}, LaunchTokenPresent={LaunchTokenPresent}", _options.PipeName, !string.IsNullOrEmpty(_launchToken));
            _cancellation = new CancellationTokenSource();
            _connectionTask = Task.Run(() => RunConnectionLoopAsync(_cancellation.Token));
        }

        public bool TrySendIntent(BridgeIntentMessage intent)
        {
            if (intent == null || string.IsNullOrEmpty(_sessionId))
            {
                return false;
            }

            var stream = _stream;
            if (stream == null || !stream.IsConnected)
            {
                return false;
            }

            var envelope = new BridgeMessageEnvelope
            {
                ProtocolVersion = DesktopBridgeProtocol.Version,
                MessageId = Guid.NewGuid().ToString("N"),
                SessionId = _sessionId,
                MessageType = BridgeMessageType.UserIntent,
                Intent = intent
            };

            return TryWriteEnvelope(stream, envelope);
        }

        public bool TrySendOverlayInputIntent(OverlayInputIntentMessage intent)
        {
            return TrySendOverlayEnvelope(BridgeMessageType.OverlayInputIntent, intent, null, null);
        }

        public bool TrySendOverlayWindowStateChanged(OverlayWindowStateChangedMessage message)
        {
            return TrySendOverlayEnvelope(BridgeMessageType.OverlayWindowStateChanged, null, message, null);
        }

        public bool TrySendOverlayLifecycle(OverlayHostLifecycleMessage message)
        {
            return TrySendOverlayEnvelope(BridgeMessageType.OverlayHostLifecycle, null, null, message);
        }

        public void Dispose()
        {
            try
            {
                _cancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _stream?.Dispose();
            }
            catch
            {
            }
        }

        private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    RaiseConnectionStatus("Connecting to Cortex runtime bridge...");
                    using (var stream = new NamedPipeClientStream(".", _options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                    {
                        _stream = stream;
                        await stream.ConnectAsync(2000, cancellationToken);
                        RaiseConnectionStatus("Connected. Opening bridge session...");
                        _sessionId = string.Empty;
                        if (!TryWriteEnvelope(stream, new BridgeMessageEnvelope
                        {
                            ProtocolVersion = DesktopBridgeProtocol.Version,
                            MessageId = Guid.NewGuid().ToString("N"),
                            MessageType = BridgeMessageType.OpenSessionRequest,
                            OpenSessionRequest = new OpenSessionRequestMessage
                            {
                                ClientName = _options.ClientDisplayName,
                                RequestedProtocolVersion = DesktopBridgeProtocol.Version,
                                LaunchToken = _launchToken,
                                Capabilities = BuildCapabilities()
                            }
                        }))
                        {
                            throw new IOException("Could not open the bridge session.");
                        }

                        await ReadLoopAsync(stream, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    ReportConnectionFailure(ex);
                    RaiseConnectionStatus("Waiting for Cortex runtime bridge...");
                }
                finally
                {
                    _sessionId = string.Empty;
                    try
                    {
                        _stream?.Dispose();
                    }
                    catch
                    {
                    }

                    _stream = null;
                }

                await Task.Delay(1500, cancellationToken);
            }
        }

        private async Task ReadLoopAsync(Stream stream, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await ReadEnvelopeAsync(stream, cancellationToken);
                if (envelope == null)
                {
                    return;
                }

                switch (envelope.MessageType)
                {
                    case BridgeMessageType.SessionOpened:
                        _sessionId = envelope.SessionId ?? string.Empty;
                        _capabilities = envelope.SessionOpened != null ? envelope.SessionOpened.Capabilities ?? new BridgeCapabilitySet() : new BridgeCapabilitySet();
                        Log.Information(
                            "Desktop bridge session opened. SessionId={SessionId}, AcceptedProtocolVersion={ProtocolVersion}, CapabilityCount={CapabilityCount}, LaunchTokenPresent={LaunchTokenPresent}",
                            _sessionId,
                            envelope.SessionOpened != null ? envelope.SessionOpened.AcceptedProtocolVersion : 0,
                            _capabilities != null && _capabilities.Features != null ? _capabilities.Features.Count : 0,
                            !string.IsNullOrEmpty(envelope.SessionOpened != null ? envelope.SessionOpened.LaunchToken : string.Empty));
                        RaiseConnectionStatus(envelope.SessionOpened != null ? envelope.SessionOpened.StatusMessage : "Bridge session opened.");
                        if (HasFeature(DesktopBridgeFeatureIds.OverlayLifecycle))
                        {
                            TrySendOverlayLifecycle(new OverlayHostLifecycleMessage
                            {
                                Kind = OverlayHostLifecycleKind.Connected,
                                LaunchToken = _launchToken,
                                StatusMessage = "External overlay host connected.",
                                UtcTimestamp = DateTime.UtcNow.ToString("o")
                            });
                            TrySendOverlayLifecycle(new OverlayHostLifecycleMessage
                            {
                                Kind = OverlayHostLifecycleKind.RequestLatestSnapshot,
                                LaunchToken = _launchToken,
                                StatusMessage = "External overlay host requested the latest snapshot.",
                                UtcTimestamp = DateTime.UtcNow.ToString("o")
                            });
                        }
                        break;
                    case BridgeMessageType.WorkbenchSnapshot:
                        if (envelope.WorkbenchSnapshot != null &&
                            _workbenchRevisionTracker.ShouldAccept(envelope.WorkbenchSnapshot.Revision))
                        {
                            if (!_loggedFirstWorkbenchSnapshot)
                            {
                                _loggedFirstWorkbenchSnapshot = true;
                                Log.Information("Received first workbench snapshot. Revision={Revision}", envelope.WorkbenchSnapshot.Revision);
                            }

                            SnapshotReceived?.Invoke(envelope.WorkbenchSnapshot.Snapshot);
                        }
                        break;
                    case BridgeMessageType.OverlayPresentationSnapshot:
                        if (envelope.OverlayPresentationSnapshot != null &&
                            _overlayRevisionTracker.ShouldAccept(envelope.OverlayPresentationSnapshot.Revision))
                        {
                            if (!_loggedFirstOverlaySnapshot)
                            {
                                _loggedFirstOverlaySnapshot = true;
                                var snapshot = envelope.OverlayPresentationSnapshot.Snapshot;
                                Log.Information(
                                    "Received first overlay snapshot. Revision={Revision}, PresentationMode={PresentationMode}, SurfaceCount={SurfaceCount}",
                                    envelope.OverlayPresentationSnapshot.Revision,
                                    snapshot != null ? snapshot.PresentationModeId : string.Empty,
                                    snapshot != null && snapshot.Surfaces != null ? snapshot.Surfaces.Count : 0);
                            }

                            OverlaySnapshotReceived?.Invoke(envelope.OverlayPresentationSnapshot.Snapshot);
                        }
                        break;
                    case BridgeMessageType.OverlayHostLifecycle:
                        if (envelope.OverlayHostLifecycle != null)
                        {
                            Log.Information(
                                "Received overlay lifecycle message. Kind={Kind}, Revision={Revision}, Status={Status}",
                                envelope.OverlayHostLifecycle.Kind,
                                envelope.OverlayHostLifecycle.Revision,
                                envelope.OverlayHostLifecycle.StatusMessage ?? string.Empty);
                            OverlayLifecycleReceived?.Invoke(envelope.OverlayHostLifecycle);
                        }
                        break;
                    case BridgeMessageType.OperationResult:
                        if (envelope.OperationResult != null)
                        {
                            OperationStatusReceived?.Invoke(envelope.OperationResult.StatusMessage ?? string.Empty);
                        }
                        break;
                    case BridgeMessageType.Diagnostic:
                        if (envelope.Diagnostic != null)
                        {
                            OperationStatusReceived?.Invoke(envelope.Diagnostic.Message ?? string.Empty);
                        }
                        break;
                }
            }
        }

        private void RaiseConnectionStatus(string status)
        {
            var normalizedStatus = status ?? string.Empty;
            if (!string.Equals(_lastConnectionStatus, normalizedStatus, StringComparison.Ordinal))
            {
                _lastConnectionStatus = normalizedStatus;
                Log.Information("Desktop bridge state changed. Status={Status}", normalizedStatus);
            }

            ConnectionStatusChanged?.Invoke(normalizedStatus);
        }

        private bool TrySendOverlayEnvelope(
            BridgeMessageType messageType,
            OverlayInputIntentMessage overlayIntent,
            OverlayWindowStateChangedMessage overlayWindowStateChanged,
            OverlayHostLifecycleMessage overlayLifecycle)
        {
            if (string.IsNullOrEmpty(_sessionId))
            {
                ReportOutboundFailure("No active bridge session for outbound message type " + messageType + ".");
                return false;
            }

            var stream = _stream;
            if (stream == null || !stream.IsConnected)
            {
                ReportOutboundFailure("Bridge stream is not connected for outbound message type " + messageType + ".");
                return false;
            }

            return TryWriteEnvelope(stream, new BridgeMessageEnvelope
            {
                ProtocolVersion = DesktopBridgeProtocol.Version,
                MessageId = Guid.NewGuid().ToString("N"),
                SessionId = _sessionId,
                MessageType = messageType,
                OverlayInputIntent = overlayIntent,
                OverlayWindowStateChanged = overlayWindowStateChanged,
                OverlayHostLifecycle = overlayLifecycle
            });
        }

        private bool TryWriteEnvelope(Stream stream, BridgeMessageEnvelope envelope)
        {
            lock (_writeSync)
            {
                try
                {
                    var payload = BridgeMessageSerializer.Serialize(envelope);
                    var lengthPrefix = BitConverter.GetBytes(payload.Length);
                    stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush();
                    _lastOutboundFailure = string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    ReportOutboundFailure(
                        "Failed to write outbound bridge envelope. MessageType=" +
                        (envelope != null ? envelope.MessageType.ToString() : string.Empty) +
                        ", Error=" + (ex.Message ?? ex.GetType().FullName) + ".");
                    return false;
                }
            }
        }

        private static BridgeCapabilitySet BuildCapabilities()
        {
            var capabilities = new BridgeCapabilitySet();
            capabilities.Features.Add(DesktopBridgeFeatureIds.OverlayPresentation);
            capabilities.Features.Add(DesktopBridgeFeatureIds.OverlayInputIntents);
            capabilities.Features.Add(DesktopBridgeFeatureIds.OverlayLifecycle);
            capabilities.Features.Add(DesktopBridgeFeatureIds.OverlayWindowStateSync);
            capabilities.Features.Add(DesktopBridgeFeatureIds.Heartbeat);
            return capabilities;
        }

        private bool HasFeature(string featureId)
        {
            if (_capabilities == null || _capabilities.Features == null || string.IsNullOrEmpty(featureId))
            {
                return false;
            }

            for (var i = 0; i < _capabilities.Features.Count; i++)
            {
                if (string.Equals(_capabilities.Features[i], featureId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<BridgeMessageEnvelope> ReadEnvelopeAsync(Stream stream, CancellationToken cancellationToken)
        {
            var lengthPrefix = new byte[4];
            if (!await TryReadExactAsync(stream, lengthPrefix, cancellationToken))
            {
                return null;
            }

            var payloadLength = BitConverter.ToInt32(lengthPrefix, 0);
            if (payloadLength <= 0)
            {
                return null;
            }

            var payload = new byte[payloadLength];
            if (!await TryReadExactAsync(stream, payload, cancellationToken))
            {
                return null;
            }

            return BridgeMessageSerializer.Deserialize(payload);
        }

        private static async Task<bool> TryReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken);
                if (read <= 0)
                {
                    return false;
                }

                offset += read;
            }

            return true;
        }

        private void ReportConnectionFailure(Exception exception)
        {
            var message = exception != null ? exception.Message ?? exception.GetType().FullName : "Unknown bridge connection failure.";
            if (string.Equals(_lastFailureMessage, message, StringComparison.Ordinal))
            {
                return;
            }

            _lastFailureMessage = message;
            Log.Warning(exception, "Desktop bridge connection attempt failed. PipeName={PipeName}", _options.PipeName);
        }

        private void ReportOutboundFailure(string message)
        {
            var normalizedMessage = message ?? string.Empty;
            if (string.Equals(_lastOutboundFailure, normalizedMessage, StringComparison.Ordinal))
            {
                return;
            }

            _lastOutboundFailure = normalizedMessage;
            Log.Warning("Desktop bridge outbound send issue. {Message}", normalizedMessage);
        }
    }
}
