using System;
using System.Collections.Generic;
using System.Net;
using ModAPI.Networking.Addressing;
using ModAPI.Networking.Buffers;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Reliability;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Transport;

namespace ModAPI.Networking.Sessions
{
    public sealed class NetworkSession : IDisposable
    {
        internal delegate void WritePayloadDelegate(ref BitWriter writer);

        private readonly object _queueSync = new object();
        private readonly object _outboundSync = new object();
        private readonly Queue<ReceivedPacket> _pendingPackets = new Queue<ReceivedPacket>();
        private readonly Queue<Exception> _pendingErrors = new Queue<Exception>();
        private readonly List<PendingOutboundMessage> _pendingOutboundMessages = new List<PendingOutboundMessage>();
        private readonly BufferPool _sendPool;
        private readonly INetworkTransport _transport;
        private readonly NetworkDiagnosticsEventBuffer _diagnosticsEvents;
        private readonly bool _ownsTransport;
        private readonly NetworkHost _host;
        private readonly NetworkClient _client;
        private DateTime _nextApplicationFlushUtc = DateTime.MinValue;
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
            _diagnosticsEvents = new NetworkDiagnosticsEventBuffer(Config.DiagnosticsEventCapacity);
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
        public event EventHandler<NetworkPeerDisconnectedEventArgs> TransportDisconnected;
        public event EventHandler<NetworkTransportReconnectEventArgs> TransportReconnected;

        public NetworkSessionState State { get; private set; }
        public NetworkSessionMode Mode { get; private set; }
        public byte LocalPeerId { get; internal set; }
        public IPEndPoint LocalEndPoint { get { return _transport.LocalEndPoint; } }
        public string SessionId { get { return Options.SessionId; } }
        public string SessionNonce { get { return Options.SessionNonce; } }
        public string StablePeerId { get { return Options.StablePeerId; } }
        public string ReconnectToken { get { return Options.ReconnectToken; } }

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
            if (Options.SessionNonce.Length == 0)
                Options.SessionNonce = GenerateSessionNonce();
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

        public void Join(string manualEndpoint, NetworkSessionOptions options)
        {
            ManualEndpointParseResult parseResult = ManualEndpointParser.Parse(manualEndpoint, GetManualEndpointDefaultPort());
            if (!parseResult.Success)
                throw new FormatException(parseResult.Message);

            EndpointResolutionResult resolution = parseResult.Endpoint.Resolve();
            if (!resolution.Success)
                throw new InvalidOperationException(resolution.Message);

            Join(resolution.EndPoint, options);
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

            FlushApplicationMessages(utcNow, false);
            PumpReliableResends(utcNow);
        }

        public NetworkPeer[] GetPeers()
        {
            return Peers.GetAll();
        }

        public NetworkDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            NetworkPeer[] peers = Peers.GetAll();
            NetworkPeerDiagnosticsSnapshot[] peerSnapshots = new NetworkPeerDiagnosticsSnapshot[peers.Length];
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                peerSnapshots[i] = new NetworkPeerDiagnosticsSnapshot(
                    peer.PeerId,
                    peer.EndPoint,
                    peer.State,
                    peer.DisplayName,
                    peer.Diagnostics.PacketsSent,
                    peer.Diagnostics.PacketsReceived,
                    peer.Diagnostics.BytesSent,
                    peer.Diagnostics.BytesReceived,
                    peer.LastError,
                    peer.Diagnostics.HeartbeatLatencyMilliseconds,
                    peer.Connection.LastSendUtc,
                    peer.Connection.LastReceiveUtc);
            }

            return new NetworkDiagnosticsSnapshot(
                DateTime.UtcNow,
                Mode,
                State,
                LocalPeerId,
                peerSnapshots,
                _diagnosticsEvents.ToArray());
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
            FlushApplicationMessages(DateTime.UtcNow, true);
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

            FlushApplicationMessages(DateTime.UtcNow, true);
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

            peer.LastError = message ?? string.Empty;
            if (Mode == NetworkSessionMode.Host)
                _host.RememberDisconnectedPeer(peer);

            Peers.Remove(peer.PeerId);
            peer.SetState(NetworkConnectionState.Disconnected, DateTime.UtcNow);
            RaiseTransportDisconnected(peer, reason, message);
            RaisePeerDisconnected(peer, reason, message);
        }

        internal void ReportSessionError(string context, Exception exception, bool isFatal)
        {
            NetworkDiagnostics.Exception(exception, context);
            _diagnosticsEvents.Add(NetworkDiagnosticsEventKind.SessionError, NetworkDefaults.UnassignedPeerId,
                null, 0, 0, 0, context);
            RaiseSessionError(context, exception, isFatal);
            if (isFatal)
                ChangeState(NetworkSessionState.Failed);
        }

        internal void RaisePeerConnected(NetworkPeer peer)
        {
            if (peer != null)
            {
                _diagnosticsEvents.Add(NetworkDiagnosticsEventKind.PeerConnected, peer.PeerId, peer.EndPoint,
                    0, 0, 0, "Peer connected.");
            }

            EventHandler<NetworkPeerEventArgs> handler = PeerConnected;
            if (handler != null)
                handler(this, new NetworkPeerEventArgs(peer));
        }

        internal void RaisePeerDisconnected(NetworkPeer peer, NetworkDisconnectReason reason, string message)
        {
            if (peer != null)
            {
                _diagnosticsEvents.Add(NetworkDiagnosticsEventKind.PeerDisconnected, peer.PeerId, peer.EndPoint,
                    0, 0, 0, "Peer disconnected: " + reason + " " + (message ?? string.Empty));
            }

            EventHandler<NetworkPeerDisconnectedEventArgs> handler = PeerDisconnected;
            if (handler != null)
                handler(this, new NetworkPeerDisconnectedEventArgs(peer, reason, message));
        }

        internal void RaiseConnectionFailed(NetworkPeer peer, HandshakeRejectReason reason, string message, Exception exception)
        {
            if (peer != null)
            {
                peer.LastError = message ?? string.Empty;
                _diagnosticsEvents.Add(NetworkDiagnosticsEventKind.SessionWarning, peer.PeerId, peer.EndPoint,
                    0, 0, 0, "Connection failed: " + reason + " " + (message ?? string.Empty));
            }

            EventHandler<NetworkConnectionFailedEventArgs> handler = ConnectionFailed;
            if (handler != null)
                handler(this, new NetworkConnectionFailedEventArgs(peer, reason, message, exception));
        }

        internal void RaiseTransportReconnected(NetworkPeer peer, byte previousPeerId)
        {
            EventHandler<NetworkTransportReconnectEventArgs> handler = TransportReconnected;
            if (handler != null)
                handler(this, new NetworkTransportReconnectEventArgs(peer, previousPeerId));
        }

        internal void RaiseTransportDisconnected(NetworkPeer peer, NetworkDisconnectReason reason, string message)
        {
            EventHandler<NetworkPeerDisconnectedEventArgs> handler = TransportDisconnected;
            if (handler != null)
                handler(this, new NetworkPeerDisconnectedEventArgs(peer, reason, message));
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
            if (count > GetMaxSingleMessagePayloadBytes())
                throw new ArgumentOutOfRangeException("count", "Message payload does not fit in one network packet.");

            QueueApplicationMessage(peer, messageType, channel, payload, offset, count);
            return true;
        }

        private void QueueApplicationMessage(NetworkPeer peer, ushort messageType, NetworkChannel channel, byte[] payload, int offset, int count)
        {
            byte[] payloadCopy = new byte[count];
            if (count > 0)
                Buffer.BlockCopy(payload, offset, payloadCopy, 0, count);

            lock (_outboundSync)
            {
                bool wasEmpty = _pendingOutboundMessages.Count == 0;
                _pendingOutboundMessages.Add(new PendingOutboundMessage(peer, messageType, channel, payloadCopy));
                if (wasEmpty)
                    _nextApplicationFlushUtc = DateTime.UtcNow.AddMilliseconds(Config.FlushIntervalMilliseconds);
            }
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
            if (count > GetMaxSingleMessagePayloadBytes())
                throw new ArgumentOutOfRangeException("count", "Message payload does not fit in one network packet.");
            if (!_transport.IsRunning)
                return false;

            PooledBuffer buffer = _sendPool.Rent();
            try
            {
                MessageBatchBuilder builder = new MessageBatchBuilder(buffer.Bytes);
                builder.AddFlags(extraFlags);
                if (!builder.TryAdd(new NetworkMessage(messageType, channel, payload, offset, count)))
                    throw new InvalidOperationException("Message payload does not fit in one network packet.");

                return SendPreparedPacket(endPoint, peer, builder, buffer.Bytes);
            }
            catch (Exception ex)
            {
                if (peer != null)
                    peer.LastError = ex.Message;
                ReportSessionError("Failed to send packet to " + endPoint, ex, false);
                return false;
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private void FlushApplicationMessages(DateTime utcNow, bool force)
        {
            PendingOutboundMessage[] messages = TakePendingApplicationMessages(utcNow, force);
            if (messages.Length == 0)
                return;

            bool[] used = new bool[messages.Length];
            while (true)
            {
                int firstIndex = FindNextUnused(messages, used);
                if (firstIndex < 0)
                    return;

                PendingOutboundMessage first = messages[firstIndex];
                NetworkPeer peer = first.Peer;
                if (peer == null || peer.State != NetworkConnectionState.Connected)
                {
                    used[firstIndex] = true;
                    continue;
                }

                PooledBuffer buffer = _sendPool.Rent();
                try
                {
                    MessageBatchBuilder builder = new MessageBatchBuilder(buffer.Bytes);
                    for (int i = firstIndex; i < messages.Length; i++)
                    {
                        if (used[i])
                            continue;

                        PendingOutboundMessage candidate = messages[i];
                        if (!object.ReferenceEquals(candidate.Peer, peer) || candidate.Channel != first.Channel)
                            continue;
                        if (candidate.Peer.State != NetworkConnectionState.Connected)
                        {
                            used[i] = true;
                            continue;
                        }

                        if (!builder.TryAdd(new NetworkMessage(candidate.MessageType, candidate.Channel, candidate.Payload, 0, candidate.Payload.Length)))
                        {
                            if (!builder.HasMessages)
                            {
                                used[i] = true;
                                throw new InvalidOperationException("Message payload does not fit in one network packet.");
                            }

                            break;
                        }

                        used[i] = true;
                    }

                    if (builder.HasMessages)
                        SendPreparedPacket(peer.EndPoint, peer, builder, buffer.Bytes);
                }
                catch (Exception ex)
                {
                    peer.LastError = ex.Message;
                    ReportSessionError("Failed to flush batched messages to " + peer.EndPoint, ex, false);
                }
                finally
                {
                    buffer.Dispose();
                }
            }
        }

        private PendingOutboundMessage[] TakePendingApplicationMessages(DateTime utcNow, bool force)
        {
            lock (_outboundSync)
            {
                if (_pendingOutboundMessages.Count == 0)
                    return new PendingOutboundMessage[0];
                if (!force && utcNow < _nextApplicationFlushUtc)
                    return new PendingOutboundMessage[0];

                PendingOutboundMessage[] messages = _pendingOutboundMessages.ToArray();
                _pendingOutboundMessages.Clear();
                _nextApplicationFlushUtc = DateTime.MinValue;
                return messages;
            }
        }

        private static int FindNextUnused(PendingOutboundMessage[] messages, bool[] used)
        {
            for (int i = 0; i < messages.Length; i++)
            {
                if (!used[i])
                    return i;
            }

            return -1;
        }

        private bool SendPreparedPacket(IPEndPoint endPoint, NetworkPeer peer, MessageBatchBuilder builder, byte[] buffer)
        {
            if (!_transport.IsRunning)
                return false;

            ushort sequence = peer != null ? peer.NextSequence() : NextStatelessSequence();
            ushort ack = peer != null && peer.AckWindow.HasAny ? peer.AckWindow.Latest : (ushort)0;
            uint ackBits = peer != null && peer.AckWindow.HasAny ? peer.AckWindow.AckBits : 0;
            builder.WriteHeader(sequence, ack, ackBits);
            _transport.Send(endPoint, buffer, 0, builder.Length);
            if (peer != null)
            {
                DateTime utcNow = DateTime.UtcNow;
                peer.Diagnostics.RecordPacketSent(builder.Length);
                if ((builder.Flags & PacketFlags.IsHeartbeat) != 0)
                    peer.Diagnostics.RecordHeartbeatSent(sequence, utcNow);
                peer.TouchSend(utcNow);
                _diagnosticsEvents.Add(NetworkDiagnosticsEventKind.PacketSent, peer.PeerId, peer.EndPoint,
                    builder.Length, sequence, builder.MessageCount, DescribePacket("Sent", builder.Flags));
                if ((builder.Flags & PacketFlags.HasReliableMessages) != 0)
                {
                    peer.ReliableOutbound.TrackSent(
                        sequence,
                        buffer,
                        0,
                        builder.Length,
                        builder.Flags,
                        builder.MessageCount,
                        utcNow);
                }
            }

            return true;
        }

        private int GetMaxSingleMessagePayloadBytes()
        {
            return Math.Min(ushort.MaxValue, Config.MaxPacketSize - NetworkDefaults.HeaderSize - 5);
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

                if (Mode == NetworkSessionMode.Client && State == NetworkSessionState.Connecting)
                {
                    ReportSessionError("Transport error while connecting", exception, false);
                    continue;
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
            bool isNewSequence = true;
            if (peer != null)
            {
                peer.ReliableOutbound.ProcessAcks(header.Ack, header.AckBits);
                peer.Diagnostics.RecordInboundAck(header.Ack, header.AckBits, utcNow);
                peer.Diagnostics.RecordPacketReceived(packet.Length);
                isNewSequence = peer.TouchReceive(utcNow, header.Sequence);
                _diagnosticsEvents.Add(NetworkDiagnosticsEventKind.PacketReceived, peer.PeerId, peer.EndPoint,
                    packet.Length, header.Sequence, header.MessageCount, DescribePacket("Received", header.Flags));
            }

            NetworkMessage message;
            while (reader.TryReadNext(out message))
            {
                if (message.Channel == NetworkChannel.Reliable && !isNewSequence)
                    continue;

                if (SessionMessageTypes.IsReserved(message.MessageType))
                    HandleSessionMessage(packet.RemoteEndPoint, peer, message);
                else if (peer != null && peer.State == NetworkConnectionState.Connected)
                    RaiseMessageReceived(peer, message);
            }
        }

        private void PumpReliableResends(DateTime utcNow)
        {
            if (!_transport.IsRunning)
                return;

            NetworkPeer[] peers = Peers.GetAll();
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer.State != NetworkConnectionState.Connected)
                    continue;

                ReliableSentPacket expired = peer.ReliableOutbound.FindExpired(utcNow, Config.ConnectionTimeoutMilliseconds);
                if (expired != null)
                {
                    HandleReliableTimeout(peer, expired);
                    continue;
                }

                ReliableSentPacket[] duePackets = peer.ReliableOutbound.GetDuePackets(utcNow, Config.ReliableResendMilliseconds);
                for (int j = 0; j < duePackets.Length; j++)
                    ResendReliablePacket(peer, duePackets[j], utcNow);
            }
        }

        private void ResendReliablePacket(NetworkPeer peer, ReliableSentPacket packet, DateTime utcNow)
        {
            try
            {
                ushort ack = peer.AckWindow.HasAny ? peer.AckWindow.Latest : (ushort)0;
                uint ackBits = peer.AckWindow.HasAny ? peer.AckWindow.AckBits : 0;
                packet.RefreshHeader(ack, ackBits);
                _transport.Send(peer.EndPoint, packet.Buffer, 0, packet.Length);
                packet.MarkResent(utcNow);
                peer.Diagnostics.RecordPacketSent(packet.Length);
                peer.TouchSend(utcNow);
                _diagnosticsEvents.Add(NetworkDiagnosticsEventKind.PacketSent, peer.PeerId, peer.EndPoint,
                    packet.Length, packet.Sequence, 0, "Resent reliable packet.");
            }
            catch (Exception ex)
            {
                ReportSessionError("Failed to resend reliable packet " + packet.Sequence + " to " + peer.EndPoint, ex, false);
            }
        }

        private void HandleReliableTimeout(NetworkPeer peer, ReliableSentPacket packet)
        {
            string message = "Reliable packet " + packet.Sequence + " was not acknowledged before the connection timeout.";
            peer.LastError = message;

            if (Mode == NetworkSessionMode.Host)
            {
                RemovePeer(peer, NetworkDisconnectReason.Timeout, message);
                return;
            }

            peer.SetState(NetworkConnectionState.TimedOut, DateTime.UtcNow);
            RaiseTransportDisconnected(peer, NetworkDisconnectReason.Timeout, message);
            RaisePeerDisconnected(peer, NetworkDisconnectReason.Timeout, message);
            StopTransportAndClear(NetworkSessionState.Failed);
        }

        private static string DescribePacket(string direction, PacketFlags flags)
        {
            if ((flags & PacketFlags.IsHandshake) != 0)
                return direction + " handshake packet.";
            if ((flags & PacketFlags.IsHeartbeat) != 0)
                return direction + " heartbeat packet.";
            if ((flags & PacketFlags.HasReliableMessages) != 0)
                return direction + " reliable packet.";
            return direction + " packet.";
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

            if (message.MessageType == SessionMessageTypes.DiscoveryRequest)
            {
                if (Mode == NetworkSessionMode.Host && State == NetworkSessionState.Listening)
                    _host.HandleDiscoveryRequest(remoteEndPoint, message);
                return;
            }

            if (message.MessageType == SessionMessageTypes.DiscoveryResponse)
                return;

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

            peer.LastError = disconnect.Message ?? string.Empty;
            if (Mode == NetworkSessionMode.Host)
            {
                RemovePeer(peer, disconnect.Reason, disconnect.Message);
            }
            else
            {
                peer.SetState(NetworkConnectionState.Disconnected, DateTime.UtcNow);
                RaiseTransportDisconnected(peer, disconnect.Reason, disconnect.Message);
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

        private int GetManualEndpointDefaultPort()
        {
            return Config.Port > 0 ? Config.Port : NetworkDefaults.DefaultPort;
        }

        private static string GenerateSessionNonce()
        {
            return Guid.NewGuid().ToString("N");
        }

        private sealed class PendingOutboundMessage
        {
            public PendingOutboundMessage(NetworkPeer peer, ushort messageType, NetworkChannel channel, byte[] payload)
            {
                Peer = peer;
                MessageType = messageType;
                Channel = channel;
                Payload = payload ?? new byte[0];
            }

            public NetworkPeer Peer;
            public ushort MessageType;
            public NetworkChannel Channel;
            public byte[] Payload;
        }
    }
}
