using ModAPI.Networking;
using ModAPI.Networking.Addressing;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerPortValidationResult
    {
        private MultiplayerPortValidationResult(bool isValid, int port, string normalizedText, string errorText)
        {
            IsValid = isValid;
            Port = port;
            NormalizedText = normalizedText ?? string.Empty;
            ErrorText = errorText ?? string.Empty;
        }

        public bool IsValid { get; private set; }
        public int Port { get; private set; }
        public string NormalizedText { get; private set; }
        public string ErrorText { get; private set; }

        public static MultiplayerPortValidationResult Valid(int port)
        {
            return new MultiplayerPortValidationResult(true, port, port.ToString(), string.Empty);
        }

        public static MultiplayerPortValidationResult Invalid(string errorText)
        {
            return new MultiplayerPortValidationResult(false, 0, string.Empty, errorText);
        }
    }

    internal sealed class MultiplayerEndpointValidationResult
    {
        private MultiplayerEndpointValidationResult(
            bool isValid,
            string endpointText,
            string host,
            int port,
            string errorText)
        {
            IsValid = isValid;
            EndpointText = endpointText ?? string.Empty;
            Host = host ?? string.Empty;
            Port = port;
            ErrorText = errorText ?? string.Empty;
        }

        public bool IsValid { get; private set; }
        public string EndpointText { get; private set; }
        public string Host { get; private set; }
        public int Port { get; private set; }
        public string ErrorText { get; private set; }

        public static MultiplayerEndpointValidationResult Valid(ManualNetworkEndpoint endpoint)
        {
            if (endpoint == null)
                return Invalid("Endpoint could not be parsed.");

            return new MultiplayerEndpointValidationResult(
                true,
                endpoint.ToString(),
                endpoint.Host,
                endpoint.Port,
                string.Empty);
        }

        public static MultiplayerEndpointValidationResult Invalid(string errorText)
        {
            return new MultiplayerEndpointValidationResult(false, string.Empty, string.Empty, 0, errorText);
        }
    }

    internal static class MultiplayerConnectionInputValidator
    {
        public const string EndpointExample = "192.168.1.10:7777";

        public static MultiplayerPortValidationResult ValidatePortText(string portText)
        {
            if (portText == null || portText.Trim().Length == 0)
                return MultiplayerPortValidationResult.Valid(NetworkDefaults.DefaultPort);

            string text = portText.Trim();
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                    return MultiplayerPortValidationResult.Invalid("Port must be a number between 1 and 65535.");
            }

            int port;
            if (!int.TryParse(text, out port))
                return MultiplayerPortValidationResult.Invalid("Port must be a number between 1 and 65535.");

            return ValidatePort(port);
        }

        public static MultiplayerPortValidationResult ValidatePort(int port)
        {
            if (port <= 0 || port > 65535)
                return MultiplayerPortValidationResult.Invalid("Port must be between 1 and 65535.");

            return MultiplayerPortValidationResult.Valid(port);
        }

        public static MultiplayerEndpointValidationResult ValidateEndpointText(string endpointText, int defaultPort)
        {
            MultiplayerPortValidationResult port = ValidatePort(defaultPort);
            if (!port.IsValid)
                return MultiplayerEndpointValidationResult.Invalid("Default endpoint port is invalid.");

            ManualEndpointParseResult result = ManualEndpointParser.Parse(endpointText, defaultPort);
            if (!result.Success)
                return MultiplayerEndpointValidationResult.Invalid(ToFriendlyEndpointError(result.Message));

            return MultiplayerEndpointValidationResult.Valid(result.Endpoint);
        }

        private static string ToFriendlyEndpointError(string message)
        {
            if (message == null || message.Length == 0)
                return "Enter an endpoint like " + EndpointExample + ".";

            return message + " Example: " + EndpointExample + ".";
        }
    }
}
