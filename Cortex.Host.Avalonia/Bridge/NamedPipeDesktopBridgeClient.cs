using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Cortex.Bridge;
using Cortex.Host.Avalonia.Composition;

namespace Cortex.Host.Avalonia.Bridge
{
    internal sealed class NamedPipeDesktopBridgeClient : IDisposable
    {
        private readonly DesktopBridgeClientOptions _options;
        private readonly object _writeSync = new object();
        private CancellationTokenSource _cancellation;
        private Task _connectionTask;
        private NamedPipeClientStream _stream;
        private string _sessionId = string.Empty;

        public NamedPipeDesktopBridgeClient(DesktopBridgeClientOptions options)
        {
            _options = options ?? new DesktopBridgeClientOptions();
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

        public void Start()
        {
            if (_connectionTask != null)
            {
                return;
            }

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
                    RaiseConnectionStatus("Connecting to legacy runtime bridge...");
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
                                RequestedProtocolVersion = DesktopBridgeProtocol.Version
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
                catch
                {
                    RaiseConnectionStatus("Waiting for legacy runtime bridge...");
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
                        RaiseConnectionStatus(envelope.SessionOpened != null ? envelope.SessionOpened.StatusMessage : "Bridge session opened.");
                        break;
                    case BridgeMessageType.WorkbenchSnapshot:
                        if (envelope.WorkbenchSnapshot != null)
                        {
                            SnapshotReceived?.Invoke(envelope.WorkbenchSnapshot.Snapshot);
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
            ConnectionStatusChanged?.Invoke(status ?? string.Empty);
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
                    return true;
                }
                catch
                {
                    return false;
                }
            }
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
    }
}
