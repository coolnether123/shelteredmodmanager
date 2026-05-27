namespace ModAPI.Networking.Events
{
    /// <summary>
    /// Identifies where an event is in an authoritative synchronization flow.
    /// </summary>
    public enum NetworkEventPhase
    {
        /// <summary>
        /// A peer is asking the authority to validate and apply the event.
        /// </summary>
        Intent = 0,

        /// <summary>
        /// The authority has accepted and applied the event.
        /// </summary>
        Authoritative = 1,

        /// <summary>
        /// The event is informational and does not imply authority transfer.
        /// </summary>
        Notification = 2
    }
}
