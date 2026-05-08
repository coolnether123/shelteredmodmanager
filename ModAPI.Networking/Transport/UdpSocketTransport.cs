using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ModAPI.Networking.Buffers;
using ModAPI.Networking.Diagnostics;

namespace ModAPI.Networking.Transport
{
    /// <summary>
    /// Allocation-conscious UDP transport built on .NET 3.5 sockets.
    /// </summary>
    public sealed class UdpSocketTransport : INetworkTransport
    {
        private readonly NetworkConfig _config;
        private readonly BufferPool _receivePool;
        private readonly object _sendSync = new object();
        private readonly object _randomSync = new object();
        private readonly Random _simulationRandom = new Random();
        private Socket _socket;
        private Thread _receiveThread;
        private volatile bool _running;

        public UdpSocketTransport(NetworkConfig config)
        {
            _config = config ?? NetworkConfig.CreateDefault();
            _config.Validate();
            _receivePool = new BufferPool(_config.MaxPacketSize, _config.ReceiveBufferPoolSize, _config.ReceiveBufferPoolSize);
        }

        public event Action<ReceivedPacket> PacketReceived;
        public event Action<Exception> TransportError;

        public bool IsRunning { get { return _running; } }
        public IPEndPoint LocalEndPoint { get; private set; }

        public void Start()
        {
            Start(_config.Port);
        }

        public void Start(int port)
        {
            if (_running)
                return;
            ValidatePort(port);

            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _socket.Bind(new IPEndPoint(IPAddress.Any, port));
                if (_config.AllowBroadcast)
                    _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            }
            catch (SocketException ex)
            {
                CloseFailedStartSocket();
                NetworkBindException bindException = new NetworkBindException(port, ex);
                NetworkDiagnostics.Exception(bindException, "UDP bind failure");
                throw bindException;
            }
            catch
            {
                CloseFailedStartSocket();
                throw;
            }

            LocalEndPoint = (IPEndPoint)_socket.LocalEndPoint;
            _running = true;
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Name = "ModAPI.Networking.Receive";
            _receiveThread.Start();

            NetworkDiagnostics.Info("UDP transport started on " + LocalEndPoint);
        }

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            try
            {
                if (_socket != null)
                    _socket.Close();
            }
            catch { }

            try
            {
                if (_receiveThread != null && _receiveThread.IsAlive)
                    _receiveThread.Join(250);
            }
            catch { }

            _receiveThread = null;
            _socket = null;
            NetworkDiagnostics.Info("UDP transport stopped.");
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int count)
        {
            if (endPoint == null)
                throw new ArgumentNullException("endPoint");
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (!_running || _socket == null)
                throw new InvalidOperationException("Transport is not running.");

            if (ShouldDropSimulatedPacket())
                return;

            int delayMilliseconds = GetSimulatedDelayMilliseconds();
            if (delayMilliseconds > 0)
            {
                byte[] copy = new byte[count];
                Buffer.BlockCopy(buffer, offset, copy, 0, count);
                IPEndPoint target = new IPEndPoint(endPoint.Address, endPoint.Port);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        Thread.Sleep(delayMilliseconds);
                        SendNow(target, copy, 0, copy.Length);
                    }
                    catch (Exception ex)
                    {
                        if (_running)
                            RaiseError(ex);
                    }
                });
                return;
            }

            SendNow(endPoint, buffer, offset, count);
        }

        private void SendNow(IPEndPoint endPoint, byte[] buffer, int offset, int count)
        {
            if (!_running || _socket == null)
                return;

            lock (_sendSync)
            {
                if (_running && _socket != null)
                    _socket.SendTo(buffer, offset, count, SocketFlags.None, endPoint);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                PooledBuffer rented = null;
                try
                {
                    rented = _receivePool.Rent();
                    EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    int length = _socket.ReceiveFrom(rented.Bytes, 0, rented.Bytes.Length, SocketFlags.None, ref remote);
                    if (length <= 0)
                    {
                        rented.Dispose();
                        continue;
                    }

                    ReceivedPacket packet = new ReceivedPacket((IPEndPoint)remote, rented, length);
                    rented = null;
                    RaisePacketReceived(packet);
                }
                catch (SocketException ex)
                {
                    if (_running && ex.SocketErrorCode != SocketError.ConnectionReset)
                        RaiseError(ex);
                }
                catch (ObjectDisposedException)
                {
                    if (_running)
                        RaiseError(new InvalidOperationException("UDP socket was disposed while running."));
                }
                catch (Exception ex)
                {
                    if (_running)
                        RaiseError(ex);
                }
                finally
                {
                    if (rented != null)
                        rented.Dispose();
                }
            }
        }

        private void RaisePacketReceived(ReceivedPacket packet)
        {
            Action<ReceivedPacket> handler = PacketReceived;
            if (handler == null)
            {
                packet.Dispose();
                return;
            }

            try
            {
                handler(packet);
            }
            catch (Exception ex)
            {
                packet.Dispose();
                RaiseError(ex);
            }
        }

        private void RaiseError(Exception exception)
        {
            NetworkDiagnostics.Exception(exception, "UDP transport error");
            Action<Exception> handler = TransportError;
            if (handler != null)
            {
                try { handler(exception); } catch { }
            }
        }

        private void CloseFailedStartSocket()
        {
            try
            {
                if (_socket != null)
                    _socket.Close();
            }
            catch { }

            _socket = null;
            LocalEndPoint = null;
            _running = false;
        }

        private static void ValidatePort(int port)
        {
            if (port < 0 || port > 65535)
                throw new ArgumentOutOfRangeException("port", "Port must be between 0 and 65535.");
        }

        private bool ShouldDropSimulatedPacket()
        {
            if (_config.SimulatedPacketLossPercent <= 0)
                return false;
            if (_config.SimulatedPacketLossPercent >= 100)
                return true;

            lock (_randomSync)
            {
                return _simulationRandom.Next(100) < _config.SimulatedPacketLossPercent;
            }
        }

        private int GetSimulatedDelayMilliseconds()
        {
            int delay = _config.SimulatedLatencyMilliseconds;
            if (_config.SimulatedJitterMilliseconds > 0)
            {
                lock (_randomSync)
                {
                    delay += _simulationRandom.Next(_config.SimulatedJitterMilliseconds + 1);
                }
            }

            return delay;
        }
    }
}
