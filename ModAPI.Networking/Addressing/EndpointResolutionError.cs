namespace ModAPI.Networking.Addressing
{
    public enum EndpointResolutionError
    {
        None = 0,
        EmptyHost = 1,
        DnsLookupFailed = 2,
        NoIPv4Address = 3
    }
}
