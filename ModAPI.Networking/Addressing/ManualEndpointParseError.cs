namespace ModAPI.Networking.Addressing
{
    public enum ManualEndpointParseError
    {
        None = 0,
        Empty = 1,
        MissingHost = 2,
        InvalidFormat = 3,
        InvalidPort = 4,
        PortOutOfRange = 5,
        UnsupportedAddressFamily = 6
    }
}
