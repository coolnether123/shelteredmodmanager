namespace ModAPI.Networking.Diagnostics
{
    public enum NetworkDiagnosticsEventKind
    {
        PacketSent = 0,
        PacketReceived = 1,
        PeerConnected = 2,
        PeerDisconnected = 3,
        SessionWarning = 4,
        SessionError = 5
    }
}
