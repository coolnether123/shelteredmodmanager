using System;

namespace ModAPI.Networking.Protocol
{
    [Flags]
    public enum PacketFlags : byte
    {
        None = 0,
        HasReliableMessages = 1,
        IsHeartbeat = 2,
        IsHandshake = 4,
        IsAckOnly = 8
    }
}
