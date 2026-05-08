namespace ModAPI.Networking.Protocol
{
    /// <summary>
    /// Message delivery policy requested by the caller.
    /// </summary>
    public enum NetworkChannel : byte
    {
        /// <summary>
        /// Fire-and-forget delivery. Dropped packets are not resent.
        /// </summary>
        Unreliable = 0,

        /// <summary>
        /// Reliable-unordered delivery. Packets are resent until ACKed, duplicate reliable packets are suppressed,
        /// and out-of-order packets are delivered as they arrive instead of being held for strict ordering.
        /// </summary>
        Reliable = 1
    }
}
