namespace ModAPI.Networking.Sessions
{
    public enum HandshakeRejectReason : byte
    {
        None = 0,
        ProtocolMismatch = 1,
        ApplicationMismatch = 2,
        SessionMismatch = 3,
        ContentSchemaMismatch = 4,
        ModContentMismatch = 5,
        ServerFull = 6,
        MalformedRequest = 7,
        AlreadyConnected = 8,
        Unknown = 255
    }
}
