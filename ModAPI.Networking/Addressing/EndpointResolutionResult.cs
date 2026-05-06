using System.Net;

namespace ModAPI.Networking.Addressing
{
    public sealed class EndpointResolutionResult
    {
        private EndpointResolutionResult(bool success, IPEndPoint endPoint, EndpointResolutionError error, string message)
        {
            Success = success;
            EndPoint = endPoint;
            Error = error;
            Message = message ?? string.Empty;
        }

        public bool Success { get; private set; }
        public IPEndPoint EndPoint { get; private set; }
        public EndpointResolutionError Error { get; private set; }
        public string Message { get; private set; }

        public static EndpointResolutionResult Succeeded(IPEndPoint endPoint)
        {
            return new EndpointResolutionResult(true, endPoint, EndpointResolutionError.None, string.Empty);
        }

        public static EndpointResolutionResult Failed(EndpointResolutionError error, string message)
        {
            return new EndpointResolutionResult(false, null, error, message);
        }
    }
}
