using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace ModAPI.Networking.Addressing
{
    public static class ManualEndpointParser
    {
        public static ManualEndpointParseResult Parse(string value)
        {
            return Parse(value, NetworkDefaults.DefaultPort);
        }

        public static ManualEndpointParseResult Parse(string value, int defaultPort)
        {
            if (defaultPort <= 0 || defaultPort > 65535)
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.PortOutOfRange,
                    "Default port must be between 1 and 65535 for a manual remote endpoint.");
            }

            if (value == null || value.Trim().Length == 0)
                return ManualEndpointParseResult.Failed(ManualEndpointParseError.Empty, "Endpoint is empty. Use host:port or host.");

            string text = value.Trim();
            if (text.StartsWith("[", StringComparison.Ordinal))
                return ParseBracketedAddress(text, defaultPort);

            int firstColon = text.IndexOf(':');
            int lastColon = text.LastIndexOf(':');
            if (firstColon != lastColon)
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.UnsupportedAddressFamily,
                    "IPv6 manual endpoints are not supported by this IPv4 UDP transport. Use an IPv4 address or hostname.");
            }

            string host;
            int port;
            if (firstColon >= 0)
            {
                host = text.Substring(0, firstColon).Trim();
                string portText = text.Substring(firstColon + 1).Trim();
                ManualEndpointParseResult portResult = TryParsePort(portText, out port);
                if (!portResult.Success)
                    return portResult;
            }
            else
            {
                host = text;
                port = defaultPort;
            }

            return CreateEndpoint(host, port);
        }

        public static bool TryParse(string value, int defaultPort, out ManualNetworkEndpoint endpoint, out string errorMessage)
        {
            ManualEndpointParseResult result = Parse(value, defaultPort);
            endpoint = result.Endpoint;
            errorMessage = result.Message;
            return result.Success;
        }

        private static ManualEndpointParseResult ParseBracketedAddress(string text, int defaultPort)
        {
            int close = text.IndexOf(']');
            if (close < 0)
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.InvalidFormat,
                    "Endpoint has an opening '[' but no closing ']'.");
            }

            string host = text.Substring(1, close - 1).Trim();
            int port = defaultPort;
            if (close + 1 < text.Length)
            {
                if (text[close + 1] != ':')
                {
                    return ManualEndpointParseResult.Failed(
                        ManualEndpointParseError.InvalidFormat,
                        "Bracketed endpoint must use [host]:port.");
                }

                ManualEndpointParseResult portResult = TryParsePort(text.Substring(close + 2).Trim(), out port);
                if (!portResult.Success)
                    return portResult;
            }

            IPAddress parsed;
            if (IPAddress.TryParse(host, out parsed) && parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.UnsupportedAddressFamily,
                    "IPv6 manual endpoints are not supported by this IPv4 UDP transport. Use an IPv4 address or hostname.");
            }

            return CreateEndpoint(host, port);
        }

        private static ManualEndpointParseResult TryParsePort(string portText, out int port)
        {
            port = 0;
            if (portText == null || portText.Length == 0)
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.InvalidPort,
                    "Endpoint port is empty. Omit ':port' to use the default port, or provide a number between 1 and 65535.");
            }

            for (int i = 0; i < portText.Length; i++)
            {
                if (!char.IsDigit(portText[i]))
                {
                    return ManualEndpointParseResult.Failed(
                        ManualEndpointParseError.InvalidPort,
                        "Endpoint port '" + portText + "' is not numeric.");
                }
            }

            if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port <= 0 || port > 65535)
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.PortOutOfRange,
                    "Endpoint port must be between 1 and 65535.");
            }

            return ManualEndpointParseResult.Succeeded(null);
        }

        private static ManualEndpointParseResult CreateEndpoint(string host, int port)
        {
            if (host == null || host.Trim().Length == 0)
                return ManualEndpointParseResult.Failed(ManualEndpointParseError.MissingHost, "Endpoint host is empty.");

            host = host.Trim();
            if (ContainsWhitespace(host))
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.InvalidFormat,
                    "Endpoint host contains whitespace.");
            }

            IPAddress address;
            if (IPAddress.TryParse(host, out address) && address.AddressFamily != AddressFamily.InterNetwork)
            {
                return ManualEndpointParseResult.Failed(
                    ManualEndpointParseError.UnsupportedAddressFamily,
                    "Only IPv4 addresses are supported by this transport.");
            }

            return ManualEndpointParseResult.Succeeded(new ManualNetworkEndpoint(host, port, address));
        }

        private static bool ContainsWhitespace(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }
    }
}
