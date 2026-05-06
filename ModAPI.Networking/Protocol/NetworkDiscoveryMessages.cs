using System;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Protocol
{
    public sealed class NetworkDiscoveryRequest
    {
        public byte ProtocolVersion = NetworkDefaults.ProtocolVersion;
        public string ApplicationId = NetworkDefaults.DefaultApplicationId;
        public string SessionId = string.Empty;

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteByte(ProtocolVersion);
            writer.WriteString(ApplicationId);
            writer.WriteString(SessionId);
        }

        public static NetworkDiscoveryRequest ReadFrom(ref BitReader reader)
        {
            NetworkDiscoveryRequest request = new NetworkDiscoveryRequest();
            request.ProtocolVersion = reader.ReadByte();
            request.ApplicationId = reader.ReadString();
            request.SessionId = reader.ReadString();
            ValidateStringLengths(request.ApplicationId, request.SessionId);
            return request;
        }

        private static void ValidateStringLengths(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (value != null && value.Length > NetworkDefaults.MaxDiscoveryStringLength)
                    throw new InvalidOperationException("Discovery request field exceeded maximum length.");
            }
        }
    }

    public sealed class NetworkDiscoveryResponse
    {
        public byte ProtocolVersion = NetworkDefaults.ProtocolVersion;
        public string ApplicationId = NetworkDefaults.DefaultApplicationId;
        public string SessionId = string.Empty;
        public int PeerCount;
        public int MaxPeers = NetworkDefaults.DefaultMaxPeers;
        public string DisplayName = string.Empty;

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteByte(ProtocolVersion);
            writer.WriteString(ApplicationId);
            writer.WriteString(SessionId);
            writer.WriteUInt16((ushort)PeerCount);
            writer.WriteUInt16((ushort)MaxPeers);
            writer.WriteString(DisplayName);
        }

        public static NetworkDiscoveryResponse ReadFrom(ref BitReader reader)
        {
            NetworkDiscoveryResponse response = new NetworkDiscoveryResponse();
            response.ProtocolVersion = reader.ReadByte();
            response.ApplicationId = reader.ReadString();
            response.SessionId = reader.ReadString();
            response.PeerCount = reader.ReadUInt16();
            response.MaxPeers = reader.ReadUInt16();
            response.DisplayName = reader.ReadString();
            ValidateStringLengths(response.ApplicationId, response.SessionId, response.DisplayName);
            return response;
        }

        private static void ValidateStringLengths(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (value != null && value.Length > NetworkDefaults.MaxDiscoveryStringLength)
                    throw new InvalidOperationException("Discovery response field exceeded maximum length.");
            }
        }
    }
}
