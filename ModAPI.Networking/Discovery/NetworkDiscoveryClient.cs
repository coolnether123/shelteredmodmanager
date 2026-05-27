using System;
using System.Net;
using System.Net.Sockets;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Discovery
{
    public sealed class NetworkDiscoveryClient
    {
        private readonly NetworkConfig _config;

        public NetworkDiscoveryClient()
            : this(NetworkConfig.CreateDefault())
        {
        }

        public NetworkDiscoveryClient(NetworkConfig config)
        {
            _config = config ?? NetworkConfig.CreateDefault();
            _config.Validate();
        }

        public NetworkDiscoveryResult[] DiscoverBroadcast(NetworkDiscoveryOptions options)
        {
            options = PrepareOptions(options);
            if (!_config.EnableBroadcastDiscovery)
                return new NetworkDiscoveryResult[0];

            return Discover(new IPEndPoint(options.BroadcastAddress, options.Port), options, true);
        }

        public NetworkDiscoveryResult[] DiscoverEndpoint(IPEndPoint endPoint, NetworkDiscoveryOptions options)
        {
            if (endPoint == null)
                throw new ArgumentNullException("endPoint");

            options = PrepareOptions(options);
            return Discover(endPoint, options, false);
        }

        private NetworkDiscoveryResult[] Discover(IPEndPoint endPoint, NetworkDiscoveryOptions options, bool broadcast)
        {
            NetworkDiscoveryResultCollection results = new NetworkDiscoveryResultCollection();
            byte[] request = CreateDiscoveryPacket(SessionMessageTypes.DiscoveryRequest, CreateRequestPayload(options));
            byte[] receiveBuffer = new byte[_config.MaxPacketSize];

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                if (broadcast)
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                socket.SendTo(request, 0, request.Length, SocketFlags.None, endPoint);
                DateTime deadlineUtc = DateTime.UtcNow.AddMilliseconds(options.TimeoutMilliseconds);
                while (DateTime.UtcNow < deadlineUtc)
                {
                    int remainingMilliseconds = (int)(deadlineUtc - DateTime.UtcNow).TotalMilliseconds;
                    if (remainingMilliseconds <= 0)
                        break;

                    socket.ReceiveTimeout = remainingMilliseconds;
                    try
                    {
                        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        int length = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref remote);
                        NetworkDiscoveryResult result = TryReadResult(receiveBuffer, length, (IPEndPoint)remote, options);
                        if (result != null)
                            results.AddOrUpdate(result);
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode == SocketError.TimedOut || ex.SocketErrorCode == SocketError.WouldBlock)
                            break;
                        throw;
                    }
                }
            }

            return results.ToArray();
        }

        internal static byte[] CreateDiscoveryPacket(ushort messageType, byte[] payload)
        {
            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            MessageBatchBuilder builder = new MessageBatchBuilder(buffer);
            if (!builder.TryAdd(new NetworkMessage(messageType, NetworkChannel.Unreliable, payload, 0, payload.Length)))
                throw new InvalidOperationException("Discovery message payload does not fit in one packet.");

            builder.WriteHeader(0, 0, 0);
            byte[] packet = new byte[builder.Length];
            Buffer.BlockCopy(buffer, 0, packet, 0, packet.Length);
            return packet;
        }

        private static NetworkDiscoveryOptions PrepareOptions(NetworkDiscoveryOptions options)
        {
            options = options ?? NetworkDiscoveryOptions.CreateDefault();
            options.Validate();
            return options;
        }

        private static byte[] CreateRequestPayload(NetworkDiscoveryOptions options)
        {
            NetworkDiscoveryRequest request = new NetworkDiscoveryRequest();
            request.ApplicationId = options.ApplicationId;
            request.SessionId = options.SessionId;
            return SerializePayload(request.WriteTo);
        }

        private static NetworkDiscoveryResult TryReadResult(
            byte[] buffer,
            int length,
            IPEndPoint remoteEndPoint,
            NetworkDiscoveryOptions options)
        {
            try
            {
                if (length < NetworkDefaults.HeaderSize)
                    return null;

                MessageBatchReader reader = new MessageBatchReader(buffer, 0, length);
                if (!reader.Header.IsValid)
                    return null;

                NetworkMessage message;
                while (reader.TryReadNext(out message))
                {
                    if (message.MessageType != SessionMessageTypes.DiscoveryResponse)
                        continue;

                    BitReader payloadReader = new BitReader(message.Payload, message.Offset, message.Length);
                    NetworkDiscoveryResponse response = NetworkDiscoveryResponse.ReadFrom(ref payloadReader);
                    if (response.ProtocolVersion != NetworkDefaults.ProtocolVersion)
                        return null;
                    if (!string.Equals(response.ApplicationId ?? string.Empty, options.ApplicationId, StringComparison.Ordinal))
                        return null;
                    if (!MatchesOptional(options.SessionId, response.SessionId))
                        return null;

                    return new NetworkDiscoveryResult(remoteEndPoint, response.ApplicationId, response.SessionId,
                        response.PeerCount, response.MaxPeers, response.DisplayName);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private delegate void WritePayloadDelegate(ref BitWriter writer);

        private static byte[] SerializePayload(WritePayloadDelegate writerCallback)
        {
            byte[] scratch = new byte[NetworkDefaults.MaxPacketSize];
            BitWriter writer = new BitWriter(scratch);
            writerCallback(ref writer);
            byte[] payload = new byte[writer.Position];
            Buffer.BlockCopy(scratch, 0, payload, 0, payload.Length);
            return payload;
        }

        private static bool MatchesOptional(string expected, string actual)
        {
            expected = expected ?? string.Empty;
            actual = actual ?? string.Empty;
            if (expected.Length == 0)
                return true;
            return string.Equals(expected, actual, StringComparison.Ordinal);
        }
    }
}
