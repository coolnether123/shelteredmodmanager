using System.Net;

namespace ModAPI.Networking.Addressing
{
    public sealed class LocalNetworkAddressInfo
    {
        internal LocalNetworkAddressInfo(
            IPAddress address,
            IPAddress subnetMask,
            string interfaceName,
            string interfaceId,
            string interfaceDescription,
            string interfaceType,
            bool isOperational,
            bool supportsMulticast,
            long speed,
            string physicalAddress)
        {
            Address = address;
            SubnetMask = subnetMask;
            InterfaceName = interfaceName ?? string.Empty;
            InterfaceId = interfaceId ?? string.Empty;
            InterfaceDescription = interfaceDescription ?? string.Empty;
            InterfaceType = interfaceType ?? string.Empty;
            IsOperational = isOperational;
            SupportsMulticast = supportsMulticast;
            Speed = speed;
            PhysicalAddress = physicalAddress ?? string.Empty;
            IsLoopback = IPAddress.IsLoopback(address);
            IsPrivate = LocalNetworkAddressHelper.IsPrivateIPv4(address);
            IsLinkLocal = LocalNetworkAddressHelper.IsLinkLocalIPv4(address);
        }

        public IPAddress Address { get; private set; }
        public IPAddress SubnetMask { get; private set; }
        public string InterfaceName { get; private set; }
        public string InterfaceId { get; private set; }
        public string InterfaceDescription { get; private set; }
        public string InterfaceType { get; private set; }
        public bool IsOperational { get; private set; }
        public bool SupportsMulticast { get; private set; }
        public long Speed { get; private set; }
        public string PhysicalAddress { get; private set; }
        public bool IsLoopback { get; private set; }
        public bool IsPrivate { get; private set; }
        public bool IsLinkLocal { get; private set; }

        public override string ToString()
        {
            return Address + " (" + InterfaceName + ")";
        }
    }
}
