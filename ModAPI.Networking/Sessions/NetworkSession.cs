using System;
using System.Collections.Generic;
using System.Net;
using ModAPI.Networking.Buffers;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Transport;

namespace ModAPI.Networking.Sessions
{
    public sealed class NetworkSession : IDisposable
    {
        internal delegate void WritePayloadDelegate(ref BitWriter writer);

        private readonly object _queueSync = new object();
        private readonly Queue<ReceivedPacket> _pendingPackets = new Queue<ReceivedPacket>();
        private readonly Queue<Exception> _pendingErrors = new Queue<Exception>();
        private readonly BufferPool _sendPool;
        private readonly INetworkTransport _transport;
        private readonly bool _ownsTransport;
        private readonly NetworkHost _host;
        private readonly NetworkClient _client;
        private ushort _statelessSequence;
        private bool _disposed;

        public NetworkSession()
            : this(NetworkConfig.CreateDefault(), null)
        {
        }

        public NetworkSession(NetworkConfig config)
            : this(config, null)
        {
        }

        public NetworkSession(NetworkConfig config, INetworkTransport transport)
        {
            Config = config ?? NetworkConfig.CreateDefault();
            Config.Validate();
            _transport = transport ?? new UdpSocketTransport(Config);
            _ownsTransport = transport == null;
            _transport.PacketReceived += OnPacketReceived;
            _transport.TransportError += OnTransportError;
            _sendPool = new BufferPool(Config.MaxPacketSize, Config.SendBufferPoolSize, Config.SendBufferPoolSize);
            Peers = new NetworkPeerRegistry();
            Options = NetworkSessionOptions.CreateDefault();
            State = NetworkSessionState.Stopped;
            Mode = NetworkSessionMode.None;
            LocalPeerId = NetworkDefaults.UnassignedPeerId;
            _host = new NetworkHost(this);
            _client = new NetworkClient(this);
        }

        public event EventHandler<NetworkPeerEventArgs> PeerConnected;
        public event EventHandler<NetworkPeerDisconnectedEventArgs> PeerDisconnected;
        public event EventHandler<NetworkConnectionFailedEventArgs> ConnectionFailed;
        public event EventHandler<NetworkMessageReceivedEventArgs> MessageReceived;
        public event EventHandler<NetworkSessionErrorEventArgs> SessionError;

        public NetworkSessionState State { get; private set; }
        public NetworkSessionMode Mode { get; private set; }
        public byte LocalPeerId { get; internal set; }
        public IPEndPoint LocalEndPoint { get { return _transport.LocalEndPoint; } }

        internal NetworkConfig Config { get; private set; }
        internal NetworkSessionOptions Options { get; private set; }
        internal NetworkPeerRegistry Peers { get; private set; }
        internal int RemotePeerCount { get { return Peers.Count; } }

        public void StartHost(NetworkSessionOptions options)
        {
            EnsureNotDisposed();
            EnsureStopped();
            Options = options ?? NetworkSessionOptions.CreateDefault();
            Options.Validate();
            ChangeState(NetworkSessionState.Starting);
            Mode = NetworkSessionMode.Host;
            LocalPeerId = NetworkDefaults.HostPeerId;
            _transport.Start(Config.Port);
            _host.Start(Options);
            ChangeState(NetworkSessionState.Listening);
        }

        public void Join(IPEndPoint hostEndPoint, NetworkSessionOptions options)
        {
            EnsureNotDisposed();
            EnsureStopped();
            if (hostEndPoint == null)
                throw new ArgumentNullException("hostEndPoint");

            Options = options ?? NetworkSessionOptions.CreateDefault();
            Options.Validate();
            ChangeState(NetworkSessionState.Starting);
            Mode = NetworkSessionMode.Client;
            LocalPeerId = NetworkDefaults.UnassignedPeerId;
            _transport.Start(0);
            _client.Start(hostEndPoint, Options);
            ChangeState(NetworkSessionState.Connecting);
        }

        public void Pump()
        {
            Update();
        }

        public void Update()
        {
            EnsureNotDisposed();
            DrainTransportErrors();
            DrainPackets();

            DateTime utcNow = DateTime.UtcNow;
            if (Mode == NetworkSessionMode.Host && State == NetworkSessionState.Listening)
                _host.Pump(utcNow);
            else if (Mode == NetworkSessionMode.Client
                && (State == NetworkSessionState.Connecting || State == NetworkSessionState.Connected))
                _client.Pump(utcNow);
        }

        public NetworkPeer[] GetPeers()
        {
            return Peers.GetAll();
        }

        public bool SendToHost(ushort messageType, NetworkChannel channel, byte[] payload)
        {
            if (Mode != NetworkSessionMode.Client)
                throw new InvalidOperationException("SendToHost is only valid for client sessions.");

            NetworkPeer hostPeer = Peers.FindByPeerId(NetworkDefaults.HostPeerId);
            return SendApplicationMessage(hostPeer, messageType, channel, payload, 0, payload != null ? payload.Length : 0);
        }

        public bool SendToPeer(byte peerId, ushort messageType, NetworkChannel channel, byte[] payload)
        {
            if (Mode != NetworkSessionMode.Host)
                throw new InvalidOperationException("SendToPeer is only valid for host sessions.");

            NetworkPeer peer = Peers.FindByPeerId(peerId);
            return SendApplicationMessage(peer, messageType, channel, payload, 0, payload != null ? payload.Length : 0);
        }

        public int Broadcast(ushort messageType, NetworkChannel channel, byte[] payload)
        {
            if (Mode != NetworkSessionMode.Host)
                throw new InvalidOperationException("Broadcast is only valid for host sessions.");

            int sent = 0;
            NetworkPeer[] peers = Peers.GetAll();
            for (int i = 0; i < peers.Length; i++)
            {
                if (SendApplicationMessage(peers[i], messageType, channel, payload, 0, payload != null ? payload.Length : 0))
                    sent++;
            }

            return sent;
        }

        public void Disconnect()
        {
            Disconnect(NetworkDisconnectReason.LocalShutdown, "Local session disconnected.");
        }

        public void Disconnect(NetworkDisconnectReason reason, string message)
        {
            EnsureNotDisposed();
            if (State == NetworkSessionState.Stopped)
                return;

            ChangeState(NetworkSessionState.Disconnecting);
            NetworkPeer[] peers = Peers.GetAll();
            for (int i = 0; i < peers.Length; i++)
                SendDisconnect(peers[i], reason, message);

            StopTransportAndClear(NetworkSessionState.Stopped);
        }

        public void DisconnectPeer(byte peerId, NetworkDisconnectReason reason, string message)
        {
            EnsureNotDisposed();
            if (Mode != NetworkSessionMode.Host)
                throw new InvalidOperationException("Only host sessions can disconnect individual peers.");

            NetworkPeer peer = Peers.FindByPeerId(peerId);
            if (peer == null)
                return;

            SendDisconnect(peer, reason, message);
            RemovePeer(peer, reason, message);
        }

        public void Shutdown()
        {
            Disconnect(NetworkDisconnectReason.LocalShutdown, "Session shutdown.");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (State != NetworkSessionState.Stopped)
                    Disconnect(NetworkDisconnectReason.LocalShutdown, "Session disposed.");
            }
            catch
            {
            }

            _transport.PacketReceived -= OnPacketReceived;
            _transport.TransportError -= OnTransportError;
            if (_ownsTransport)
                _transport.Dispose();

            DisposePendingPackets();
            _disposed = true;
        }

        internal void ChangeState(NetworkSessionState state)
        {
            State = state;
        }

        internal byte[] CreatePayload(WritePayloadDelegate writerCallback)
        {
            if (writerCallback == null)
                throw new ArgumentNullException("writerCallback");

            byte[] scratch = new byte[Config.MaxPacketSize];
            BitWriter writer = new BitWriter(scratch);
            writerCallback(ref writer);
            byte[] payload = new byte[writer.Position];
            Buffer.BlockCopy(scratch, 0, payload, 0, payload.Length);
            return payload;
        }

        internal void SendHeartbeat(NetworkPeer peer)
        {
            if (peer == null)
                return;

            NetworkHeartbeatMessage heartbeat = new NetworkHeartbeatMessage();
            heartbeat.PeerId = LocalPeerId;
            SendBuiltIn(peer, SessionMessageTypes.Heartbeat, CreatePayload(heartbeat.WriteTo), PacketFlags.IsHeartbeat);
        }

        internal void SendDisconnect(NetworkPeer peer, NetworkDisconnectReason reason, string message)
        {
            if (peer == null)
                return;

            NetworkDisconnectMessage disconnect = new NetworkDisconnectMessage();
            disconnect.Reason = reason;
            disconnect.Message = message ?? string.Empty;
            SendBuiltIn(peer, SessionMessageTypes.Disconnect, CreatePayload(disconnect.WriteTo), PacketFlags.None);
        }

        internal void SendBuiltIn(NetworkPeer peer, ushort messageType, byte[] payload, PacketFlags flags)
        {
            if (peer == null)
                return;
            SendMessage(peer.EndPoint, peer, messageType, NetworkChannel.Unreliable, payload, 0, payload != null ? payload.Length : 0, flags);
        }

        internal void SendBuiltIn(IPEndPoint endPoint, ushort messageType, byte[] payload, PacketFlags flags)
        {
            SendMessage(endPoint, null, messageType, NetworkChannel.Unreliable, payload, 0, payload != null ? payload.Length : 0, flags);
        }

        internal void RemovePeer(NetworkPeer peer, NetworkDisconnectReason reason, string message)
        {
            if (peer == null)
                return;

            Peers.Remove(peer.PeerId);
            peer.SetState(NetworkConnectionState.Disconnected, DateTime.UtcNow);
            RaisePeerDisconnected(peer, reason, message);
        }

        internal void ReportSessionError(string context, Exception exception, bool isFatal)
        {
            NetworkDiagnostics.Exception(exception, context);
            RaiseSessionError(context, exception, isFatal);
            if (isFatal)
                ChangeState(NetworkSessionState.Failed);
        }

        internal void RaisePeerConnected(NetworkPeer peer)
        {
            EventHandler<NetworkPeerEventArgs> handler = PeerConnected;
            if (handler != null)
                handler(this, new NetworkPeerEventArgs(peer));
        }

        internal void RaisePeerDisconnected(NetworkPeer peer, NetworkDisconnectReason reason, string message)
        {
            EventHandler<NetworkPeerDisconnectedEventArgs> handler = PeerDisconnected;
            if (handler != null)
                handler(this, new NetworkPeerDisconnectedEventArgs(peer, reason, message));
        }

        internal void RaiseConnectionFailed(NetworkPeer peer, HandshakeRejectReason reason, string message, Exception exception)
        {
            EventHandler<NetworkConnectionFailedEventArgs> handler = ConnectionFailed;
            if (handler != null)
                handler(this, new NetworkConnectionFailedEventArgs(peer, reason, message, exception));
        }

        private bool SendApplicationMessage(NetworkPeer peer, ushort messageType, NetworkChannel channel, byte[] payload, int offset, int count)
        {
            if (peer == null || peer.State != NetworkConnectionState.Connected)
                return false;
            if (SessionMessageTypes.IsReserved(messageType))
                throw new ArgumentOutOfRangeException("messageType", "Message type is reserved for ModAPI.Networking session control.");
            if (payload == null)
                payload = new byte[0];
            if (offset < 0 || count < 0 || offset + count > payload.Length)
                throw new ArgumentOutOfRangeException("offset");

            return SendMessage(peer.EndPoint, peer, messageType, channel, payload, offset, count, PacketFlags.None);
        }

        private bool SendMessage(
            IPEndPoint endPoint,
            NetworkPeer peer,
            ushort messageType,
            NetworkChannel channel,
            byte[] payload,
            int offset,
            int count,
            PacketFlags extraFlags)
        {
            if (endPoint == null)
                return false;
            if (payload == null)
                payload = new byte[0];
            if (count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("count", "Payload is too large.");
            if (!_transport.IsRunning)
                return false;

            PooledBuffer buffer = _sendPool.Rent();
            try
            {
                MessageBatchBuilder builder = new MessageBatchBuilder(buffer.Bytes);
                builder.AddFlags(extraFlags);
                if (!builder.TryAdd(new NetworkMessage(messageType, channel, payload, offset, count)))
                    throw new InvalidOperationException("Message payload does not fit in one network packet.");

                ushort sequence = peer != null ? peer.NextSequence() : NextStatelessSequence();
                ushort ack = peer != null && peer.AckWindow.HasAny ? peer.AckWindow.Latest : (ushort)0;
                uint ackBits = peer != null && peer.AckWindow.HasAny ? peer.AckWindow.AckBits : 0;
                builder.WriteHeader(sequence, ack, ackBits);
                _transport.Send(endPoint, buffer.Bytes, 0, builder.Length);
                if (peer != null)
                    peer.TouchSend(DateTime.UtcNow);
                return true;
            }
            catch (Exception ex)
            {
                ReportSessionError("Failed to send packet to " + endPoint, ex, false);
                return false;
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private ushort NextStatelessSequence()
        {
            ushort sequence = _statelessSequence;
            _statelessSequence = (ushort)(_statelessSequence + 1);
            return sequence;
        }

        private void OnPacketReceived(ReceivedPacket packet)
        {
            lock (_queueSync)
            {
                _pendingPackets.Enqueue(packet);
            }
        }

        private void OnTransportError(Exception exception)
        {
            lock (_queueSync)
            {
                _pendingErrors.Enqueue(exception);
            }
        }

        private void DrainTransportErrors()
        {
            while (true)
            {
                Exception exception;
                lock (_queueSync)
                {
                    if (_pendingErrors.Count == 0)
                        return;
                    exception = _pendingErrors.Dequeue();
                }

                ReportSessionError("Transport error", exception, true);
            }
        }

        private void DrainPackets()
        {
            while (true)
            {
                ReceivedPacket packet;
                lock (_queueSync)
                {
                    if (_pendingPackets.Count == 0)
                        return;
                    packet = _pendingPackets.Dequeue();
                }

                try
                {
                    HandlePacket(packet);
                }
                catch (Exception ex)
                {
                    ReportSessionError("Failed to process packet from " + packet.RemoteEndPoint, ex, false);
                }
                finally
                {
                    packet.Dispose();
                }
            }
        }

        private void HandlePacket(ReceivedPacket packet)
        {
            if (packet == null || packet.Bytes == null || packet.Length < NetworkDefaults.HeaderSize)
                return;

            MessageBatchReader reader = new MessageBatchReader(packet.Bytes, 0, packet.Length);
            NetworkPacketHeader header = reader.Header;
            if (!header.IsValid)
                return;

            NetworkPeer peer = Peers.FindByEndPoint(packet.RemoteEndPoint);
            DateTime utcNow = DateTime.UtcNow;
            if (peer != null)
                peer.TouchReceive(utcNow, header.Sequence);

            NetworkMessage message;
            while (reader.TryReadNext(out message))
            {
                if (SessionMessageTypes.IsReserved(message.MessageType))
                    HandleSessionMessage(packet.RemoteEndPoint, peer, message);
                else if (peer != null && peer.State == NetworkConnectionState.Connected)
                    RaiseMessageReceived(peer, message);
            }
        }

        private void HandleSessionMessage(IPEndPoint remoteEndPoint, NetworkPeer peer, NetworkMessage message)
        {
            if (message.MessageType == SessionMessageTypes.HandshakeRequest)
            {
                if (Mode == NetworkSessionMode.Host && State == NetworkSessionState.Listening)
                    _host.HandleHandshakeRequest(remoteEndPoint, message);
                return;
            }

            if (message.MessageType == SessionMessageTypes.HandshakeAccept)
            {
                if (Mode == NetworkSessionMode.Client && State == NetworkSessionState.Connecting)
                    _client.HandleAccept(message);
                return;
            }

            if (message.MessageType == SessionMessageTypes.HandshakeReject)
            {
                if (Mode == NetworkSessionMode.Client && State == NetworkSessionState.Connecting)
                    _client.HandleReject(message);
                return;
            }

            if (message.MessageType == SessionMessageTypes.Heartbeat)
                return;

            if (message.MessageType == SessionMessageTypes.Disconnect)
                HandleDisconnect(peer, message);
        }

        private void HandleDisconnect(NetworkPeer peer, NetworkMessage message)
        {
            NetworkDisconnectMessage disconnect;
            try
            {
                BitReader reader = new BitReader(message.Payload, message.Offset, message.Length);
                disconnect = NetworkDisconnectMessage.ReadFrom(ref reader);
            }
            catch (Exception ex)
            {
                ReportSessionError("Malformed disconnect packet", ex, false);
                return;
            }

            if (peer == null)
                return;

            if (Mode == NetworkSessionMode.Host)
            {
                RemovePeer(peer, disconnect.Reason, disconnect.Message);
            }
            else
            {
                peer.SetState(NetworkConnectionState.Disconnected, DateTime.UtcNow);
                RaisePeerDisconnected(peer, disconnect.Reason, disconnect.Message);
                StopTransportAndClear(NetworkSessionState.Stopped);
            }
        }

        private void RaiseMessageReceived(NetworkPeer peer, NetworkMessage message)
        {
            byte[] payload = new byte[message.Length];
            if (message.Length > 0)
                Buffer.BlockCopy(message.Payload, message.Offset, payload, 0, message.Length);

            EventHandler<NetworkMessageReceivedEventArgs> handler = MessageReceived;
            if (handler != null)
                handler(this, new NetworkMessageReceivedEventArgs(peer, message.MessageType, message.Channel, payload));
        }

        private void RaiseSessionError(string context, Exception exception, bool isFatal)
        {
            EventHandler<NetworkSessionErrorEventArgs> handler = SessionError;
            if (handler != null)
                handler(this, new NetworkSessionErrorEventArgs(context, exception, isFatal));
        }

        private void StopTransportAndClear(NetworkSessionState finalState)
        {
            _transport.Stop();
            Peers.Clear();
            DisposePendingPackets();
            Mode = NetworkSessionMode.None;
            LocalPeerId = NetworkDefaults.UnassignedPeerId;
            ChangeState(finalState);
        }

        private void DisposePendingPackets()
        {
            lock (_queueSync)
            {
                while (_pendingPackets.Count > 0)
                    _pendingPackets.Dequeue().Dispose();
                _pendingErrors.Clear();
            }
        }

        private void EnsureStopped()
        {
            if (State != NetworkSessionState.Stopped && State != NetworkSessionState.Failed)
                throw new InvalidOperationException("Network session is already running.");
            if (State == NetworkSessionState.Failed)
                StopTransportAndClear(NetworkSessionState.Stopped);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("NetworkSession");
        }
    }
}
