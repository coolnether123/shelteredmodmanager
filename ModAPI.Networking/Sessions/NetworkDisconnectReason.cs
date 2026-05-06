namespace ModAPI.Networking.Sessions
{
    public enum NetworkDisconnectReason : byte
    {
        None = 0,
        LocalShutdown = 1,
        RemoteClosed = 2,
        Timeout = 3,
        ProtocolError = 4,
        Rejected = 5,
        TransportError = 6
    }
}
