namespace ModAPI.Networking.Addressing
{
    public sealed class LocalNetworkInterfaceInfo
    {
        internal LocalNetworkInterfaceInfo(
            string id,
            string name,
            string description,
            string interfaceType,
            bool isOperational,
            bool supportsMulticast,
            long speed,
            string physicalAddress,
            LocalNetworkAddressInfo[] addresses)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            InterfaceType = interfaceType ?? string.Empty;
            IsOperational = isOperational;
            SupportsMulticast = supportsMulticast;
            Speed = speed;
            PhysicalAddress = physicalAddress ?? string.Empty;
            Addresses = addresses ?? new LocalNetworkAddressInfo[0];
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string InterfaceType { get; private set; }
        public bool IsOperational { get; private set; }
        public bool SupportsMulticast { get; private set; }
        public long Speed { get; private set; }
        public string PhysicalAddress { get; private set; }
        public LocalNetworkAddressInfo[] Addresses { get; private set; }
    }
}
