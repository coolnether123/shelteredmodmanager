using System;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Protocol
{
    public sealed class NetworkHandshakeRequest
    {
        public byte ProtocolVersion = NetworkDefaults.ProtocolVersion;
        public string ApplicationId = NetworkDefaults.DefaultApplicationId;
        public string SessionId = string.Empty;
        public string SessionNonce = string.Empty;
        public string ContentSchemaHash = string.Empty;
        public string ModContentHash = string.Empty;
        public string DisplayName = string.Empty;
        public string StablePeerId = string.Empty;
        public string ReconnectToken = string.Empty;

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteByte(ProtocolVersion);
            writer.WriteString(ApplicationId);
            writer.WriteString(SessionId);
            writer.WriteString(SessionNonce);
            writer.WriteString(ContentSchemaHash);
            writer.WriteString(ModContentHash);
            writer.WriteString(DisplayName);
            writer.WriteString(StablePeerId);
            writer.WriteString(ReconnectToken);
        }

        public static NetworkHandshakeRequest ReadFrom(ref BitReader reader)
        {
            NetworkHandshakeRequest request = new NetworkHandshakeRequest();
            request.ProtocolVersion = reader.ReadByte();
            request.ApplicationId = reader.ReadString();
            request.SessionId = reader.ReadString();
            request.SessionNonce = reader.ReadString();
            request.ContentSchemaHash = reader.ReadString();
            request.ModContentHash = reader.ReadString();
            request.DisplayName = reader.ReadString();
            request.StablePeerId = reader.ReadString();
            request.ReconnectToken = reader.ReadString();
            ValidateStringLengths(request.ApplicationId, request.SessionId, request.SessionNonce,
                request.ContentSchemaHash, request.ModContentHash, request.DisplayName,
                request.StablePeerId, request.ReconnectToken);
            return request;
        }

        private static void ValidateStringLengths(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (value != null && value.Length > NetworkDefaults.MaxHandshakeStringLength)
                    throw new InvalidOperationException("Handshake request field exceeded maximum length.");
            }
        }
    }

    public sealed class NetworkHandshakeAccept
    {
        public byte ProtocolVersion = NetworkDefaults.ProtocolVersion;
        public byte AssignedPeerId = NetworkDefaults.UnassignedPeerId;
        public int MaxPeers = NetworkDefaults.DefaultMaxPeers;
        public int CurrentPeerCount;
        public string ApplicationId = NetworkDefaults.DefaultApplicationId;
        public string SessionId = string.Empty;
        public string SessionNonce = string.Empty;
        public string ContentSchemaHash = string.Empty;
        public string ModContentHash = string.Empty;
        public string HostDisplayName = string.Empty;
        public string HostStablePeerId = string.Empty;
        public string ReconnectToken = string.Empty;

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteByte(ProtocolVersion);
            writer.WriteByte(AssignedPeerId);
            writer.WriteUInt16((ushort)MaxPeers);
            writer.WriteUInt16((ushort)CurrentPeerCount);
            writer.WriteString(ApplicationId);
            writer.WriteString(SessionId);
            writer.WriteString(SessionNonce);
            writer.WriteString(ContentSchemaHash);
            writer.WriteString(ModContentHash);
            writer.WriteString(HostDisplayName);
            writer.WriteString(HostStablePeerId);
            writer.WriteString(ReconnectToken);
        }

        public static NetworkHandshakeAccept ReadFrom(ref BitReader reader)
        {
            NetworkHandshakeAccept accept = new NetworkHandshakeAccept();
            accept.ProtocolVersion = reader.ReadByte();
            accept.AssignedPeerId = reader.ReadByte();
            accept.MaxPeers = reader.ReadUInt16();
            accept.CurrentPeerCount = reader.ReadUInt16();
            accept.ApplicationId = reader.ReadString();
            accept.SessionId = reader.ReadString();
            accept.SessionNonce = reader.ReadString();
            accept.ContentSchemaHash = reader.ReadString();
            accept.ModContentHash = reader.ReadString();
            accept.HostDisplayName = reader.ReadString();
            accept.HostStablePeerId = reader.ReadString();
            accept.ReconnectToken = reader.ReadString();
            return accept;
        }
    }

    public sealed class NetworkHandshakeReject
    {
        public HandshakeRejectReason Reason = HandshakeRejectReason.Unknown;
        public string Message = string.Empty;

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteByte((byte)Reason);
            writer.WriteString(Message);
        }

        public static NetworkHandshakeReject ReadFrom(ref BitReader reader)
        {
            NetworkHandshakeReject reject = new NetworkHandshakeReject();
            reject.Reason = (HandshakeRejectReason)reader.ReadByte();
            reject.Message = reader.ReadString();
            return reject;
        }
    }

    public sealed class NetworkHeartbeatMessage
    {
        public byte PeerId = NetworkDefaults.UnassignedPeerId;

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteByte(PeerId);
        }

        public static NetworkHeartbeatMessage ReadFrom(ref BitReader reader)
        {
            NetworkHeartbeatMessage heartbeat = new NetworkHeartbeatMessage();
            heartbeat.PeerId = reader.ReadByte();
            return heartbeat;
        }
    }

    public sealed class NetworkDisconnectMessage
    {
        public NetworkDisconnectReason Reason = NetworkDisconnectReason.RemoteClosed;
        public string Message = string.Empty;

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteByte((byte)Reason);
            writer.WriteString(Message);
        }

        public static NetworkDisconnectMessage ReadFrom(ref BitReader reader)
        {
            NetworkDisconnectMessage disconnect = new NetworkDisconnectMessage();
            disconnect.Reason = (NetworkDisconnectReason)reader.ReadByte();
            disconnect.Message = reader.ReadString();
            return disconnect;
        }
    }
}
