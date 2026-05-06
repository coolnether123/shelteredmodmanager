using System;
using System.Net;
using System.Net.Sockets;

namespace ModAPI.Networking.Addressing
{
    public sealed class ManualNetworkEndpoint
    {
        internal ManualNetworkEndpoint(string host, int port, IPAddress parsedAddress)
        {
            Host = host;
            Port = port;
            ParsedAddress = parsedAddress;
        }

        public string Host { get; private set; }
        public int Port { get; private set; }
        public IPAddress ParsedAddress { get; private set; }
        public bool IsIPAddress { get { return ParsedAddress != null; } }

        public EndpointResolutionResult Resolve()
        {
            if (Host == null || Host.Length == 0)
                return EndpointResolutionResult.Failed(EndpointResolutionError.EmptyHost, "Endpoint host is empty.");

            if (ParsedAddress != null)
                return EndpointResolutionResult.Succeeded(new IPEndPoint(ParsedAddress, Port));

            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(Host);
                for (int i = 0; i < addresses.Length; i++)
                {
                    if (addresses[i] != null && addresses[i].AddressFamily == AddressFamily.InterNetwork)
                        return EndpointResolutionResult.Succeeded(new IPEndPoint(addresses[i], Port));
                }

                return EndpointResolutionResult.Failed(
                    EndpointResolutionError.NoIPv4Address,
                    "Host '" + Host + "' resolved, but no IPv4 address was available.");
            }
            catch (Exception ex)
            {
                return EndpointResolutionResult.Failed(
                    EndpointResolutionError.DnsLookupFailed,
                    "Could not resolve host '" + Host + "'. Check the manual endpoint spelling, VPN/LAN connection, and DNS. " + ex.Message);
            }
        }

        public override string ToString()
        {
            return Host + ":" + Port;
        }
    }
}
