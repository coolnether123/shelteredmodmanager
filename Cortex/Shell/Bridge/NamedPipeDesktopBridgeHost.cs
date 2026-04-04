using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Cortex.Bridge;

namespace Cortex.Shell.Bridge
{
    internal sealed class NamedPipeDesktopBridgeHost : IDesktopBridgeHost
    {
        private readonly string _pipeName;
        private readonly object _sync = new object();
        private readonly Queue<BridgeMessageEnvelope> _inboundMessages = new Queue<BridgeMessageEnvelope>();
        private readonly Queue<BridgeMessageEnvelope> _outboundMessages = new Queue<BridgeMessageEnvelope>();
        private readonly AutoResetEvent _sendSignal = new AutoResetEvent(false);
        private Thread _listenerThread;
        private Thread _readerThread;
        private Thread _writerThread;
        private NamedPipeServerStream _serverStream;
        private bool _disposed;
        private bool _running;
        private bool _clientConnected;

        public NamedPipeDesktopBridgeHost(string pipeName)
        {
            _pipeName = string.IsNullOrEmpty(pipeName) ? DesktopBridgeProtocol.DefaultPipeName : pipeName;
        }

        public bool IsClientConnected
        {
            get
            {
                lock (_sync)
                {
                    return _clientConnected;
                }
            }
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Cortex.DesktopBridge.Listener"
            };
            _listenerThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _disposed = true;
            _sendSignal.Set();
            CloseCurrentConnection();
        }

        public bool TryDequeueInbound(out BridgeMessageEnvelope envelope)
        {
            lock (_sync)
            {
                if (_inboundMessages.Count > 0)
                {
                    envelope = _inboundMessages.Dequeue();
                    return true;
                }
            }

            envelope = null;
            return false;
        }

        public void EnqueueOutbound(BridgeMessageEnvelope envelope)
        {
            if (envelope == null)
            {
                return;
            }

            lock (_sync)
            {
                _outboundMessages.Enqueue(envelope);
            }

            _sendSignal.Set();
        }

        public void Dispose()
        {
            Stop();
        }

        private void ListenLoop()
        {
            while (_running && !_disposed)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        lock (_sync)
                        {
                            _serverStream = server;
                        }

                        server.WaitForConnection();
                        SetClientConnected(true);
                        _readerThread = new Thread(ReadLoop) { IsBackground = true, Name = "Cortex.DesktopBridge.Reader" };
                        _writerThread = new Thread(WriteLoop) { IsBackground = true, Name = "Cortex.DesktopBridge.Writer" };
                        _readerThread.Start();
                        _writerThread.Start();
                        _readerThread.Join();
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch
                {
                }
                finally
                {
                    CloseCurrentConnection();
                }
            }
        }

        private void ReadLoop()
        {
            while (_running && !_disposed)
            {
                var stream = GetConnectedStream();
                if (stream == null)
                {
                    return;
                }

                BridgeMessageEnvelope envelope;
                if (!TryReadEnvelope(stream, out envelope))
                {
                    return;
                }

                lock (_sync)
                {
                    _inboundMessages.Enqueue(envelope);
                }
            }
        }

        private void WriteLoop()
        {
            while (_running && !_disposed)
            {
                _sendSignal.WaitOne();
                while (_running && !_disposed)
                {
                    BridgeMessageEnvelope envelope = null;
                    lock (_sync)
                    {
                        if (_outboundMessages.Count > 0)
                        {
                            envelope = _outboundMessages.Dequeue();
                        }
                    }

                    if (envelope == null)
                    {
                        break;
                    }

                    var stream = GetConnectedStream();
                    if (stream == null)
                    {
                        return;
                    }

                    if (!TryWriteEnvelope(stream, envelope))
                    {
                        return;
                    }
                }
            }
        }

        private NamedPipeServerStream GetConnectedStream()
        {
            lock (_sync)
            {
                return _serverStream != null && _serverStream.IsConnected ? _serverStream : null;
            }
        }

        private void SetClientConnected(bool connected)
        {
            lock (_sync)
            {
                _clientConnected = connected;
            }
        }

        private void CloseCurrentConnection()
        {
            lock (_sync)
            {
                _clientConnected = false;
                try
                {
                    _serverStream?.Dispose();
                }
                catch
                {
                }

                _serverStream = null;
                _readerThread = null;
                _writerThread = null;
                _outboundMessages.Clear();
            }
        }

        private static bool TryReadEnvelope(Stream stream, out BridgeMessageEnvelope envelope)
        {
            envelope = null;
            var lengthBuffer = new byte[4];
            if (!TryReadExact(stream, lengthBuffer, 0, lengthBuffer.Length))
            {
                return false;
            }

            var payloadLength = BridgeMessageSerializer.DecodeLength(lengthBuffer);
            if (payloadLength <= 0)
            {
                return false;
            }

            var payload = new byte[payloadLength];
            if (!TryReadExact(stream, payload, 0, payloadLength))
            {
                return false;
            }

            envelope = BridgeMessageSerializer.Deserialize(payload);
            return envelope != null;
        }

        private static bool TryWriteEnvelope(Stream stream, BridgeMessageEnvelope envelope)
        {
            try
            {
                var payload = BridgeMessageSerializer.Serialize(envelope);
                var lengthPrefix = BridgeMessageSerializer.EncodeLength(payload.Length);
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

        private static bool TryReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);
                if (read <= 0)
                {
                    return false;
                }

                offset += read;
                count -= read;
            }

            return true;
        }
    }
}
