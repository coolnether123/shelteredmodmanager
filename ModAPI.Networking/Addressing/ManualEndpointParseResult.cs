namespace ModAPI.Networking.Addressing
{
    public sealed class ManualEndpointParseResult
    {
        private ManualEndpointParseResult(bool success, ManualNetworkEndpoint endpoint, ManualEndpointParseError error, string message)
        {
            Success = success;
            Endpoint = endpoint;
            Error = error;
            Message = message ?? string.Empty;
        }

        public bool Success { get; private set; }
        public ManualNetworkEndpoint Endpoint { get; private set; }
        public ManualEndpointParseError Error { get; private set; }
        public string Message { get; private set; }

        public static ManualEndpointParseResult Succeeded(ManualNetworkEndpoint endpoint)
        {
            return new ManualEndpointParseResult(true, endpoint, ManualEndpointParseError.None, string.Empty);
        }

        public static ManualEndpointParseResult Failed(ManualEndpointParseError error, string message)
        {
            return new ManualEndpointParseResult(false, null, error, message);
        }
    }
}
