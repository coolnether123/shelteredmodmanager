namespace ModAPI.Networking.Addressing
{
    public sealed class LocalNetworkAddressSelection
    {
        private LocalNetworkAddressSelection(bool success, LocalNetworkAddressInfo address, string message)
        {
            Success = success;
            Address = address;
            Message = message ?? string.Empty;
        }

        public bool Success { get; private set; }
        public LocalNetworkAddressInfo Address { get; private set; }
        public string Message { get; private set; }

        public static LocalNetworkAddressSelection Succeeded(LocalNetworkAddressInfo address)
        {
            return new LocalNetworkAddressSelection(true, address, string.Empty);
        }

        public static LocalNetworkAddressSelection Failed(string message)
        {
            return new LocalNetworkAddressSelection(false, null, message);
        }
    }
}
