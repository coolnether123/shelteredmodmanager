using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using ModAPI.Networking.Addressing;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class AddressingTests
    {
        internal static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Manual endpoint parses host and explicit port", ManualEndpointParsesHostPort));
            tests.Add(new TestCase("Manual endpoint uses default port when omitted", ManualEndpointUsesDefaultPort));
            tests.Add(new TestCase("Manual endpoint rejects invalid input", ManualEndpointRejectsInvalidInput));
            tests.Add(new TestCase("Manual endpoint resolves IPv4 without DNS", ManualEndpointResolvesIPv4WithoutDns));
            tests.Add(new TestCase("Local address helper classifies private IPv4 ranges", LocalAddressHelperClassifiesPrivateRanges));
            tests.Add(new TestCase("Local address helper enumerates IPv4-only address records", LocalAddressHelperEnumeratesIPv4OnlyRecords));
            tests.Add(new TestCase("Local LAN selection is best-effort and non-throwing", LocalLanSelectionDoesNotThrow));
            tests.Add(new TestCase("Session can join with manual endpoint string", SessionJoinsManualEndpointString));
        }

        private static void ManualEndpointParsesHostPort()
        {
            ManualEndpointParseResult result = ManualEndpointParser.Parse("localhost:12345", NetworkDefaults.DefaultPort);
            TestAssert.True(result.Success, "localhost:port should parse.");
            TestAssert.Equal("localhost", result.Endpoint.Host, "Host should round-trip.");
            TestAssert.Equal(12345, result.Endpoint.Port, "Explicit port should win.");

            result = ManualEndpointParser.Parse("127.0.0.1:23456", NetworkDefaults.DefaultPort);
            TestAssert.True(result.Success, "IPv4:port should parse.");
            TestAssert.True(result.Endpoint.IsIPAddress, "IPv4 endpoint should expose parsed address.");
            TestAssert.Equal(IPAddress.Parse("127.0.0.1"), result.Endpoint.ParsedAddress, "Parsed IPv4 address should match.");
            TestAssert.Equal(23456, result.Endpoint.Port, "IPv4 port should parse.");
        }

        private static void ManualEndpointUsesDefaultPort()
        {
            ManualEndpointParseResult result = ManualEndpointParser.Parse("192.168.1.20", 8888);
            TestAssert.True(result.Success, "IPv4 without port should parse.");
            TestAssert.Equal("192.168.1.20", result.Endpoint.Host, "Host should be preserved.");
            TestAssert.Equal(8888, result.Endpoint.Port, "Default port should be applied.");

            result = ManualEndpointParser.Parse("example-host", 9999);
            TestAssert.True(result.Success, "Hostname without port should parse.");
            TestAssert.Equal(9999, result.Endpoint.Port, "Default port should be applied to hostname.");
        }

        private static void ManualEndpointRejectsInvalidInput()
        {
            AssertEndpointFailure(null, ManualEndpointParseError.Empty);
            AssertEndpointFailure("   ", ManualEndpointParseError.Empty);
            AssertEndpointFailure(":7777", ManualEndpointParseError.MissingHost);
            AssertEndpointFailure("127.0.0.1:", ManualEndpointParseError.InvalidPort);
            AssertEndpointFailure("127.0.0.1:notaport", ManualEndpointParseError.InvalidPort);
            AssertEndpointFailure("127.0.0.1:0", ManualEndpointParseError.PortOutOfRange);
            AssertEndpointFailure("127.0.0.1:65536", ManualEndpointParseError.PortOutOfRange);
            AssertEndpointFailure("127.0.0.1:7777:8888", ManualEndpointParseError.UnsupportedAddressFamily);
        }

        private static void AssertEndpointFailure(string value, ManualEndpointParseError expectedError)
        {
            ManualEndpointParseResult result = ManualEndpointParser.Parse(value, NetworkDefaults.DefaultPort);
            TestAssert.False(result.Success, "Endpoint should fail: " + value);
            TestAssert.Equal(expectedError, result.Error, "Endpoint should fail with the expected error.");
            TestAssert.True(result.Message.Length > 0, "Endpoint failure should include a diagnostic message.");
        }

        private static void ManualEndpointResolvesIPv4WithoutDns()
        {
            ManualEndpointParseResult parse = ManualEndpointParser.Parse("127.0.0.1:7777", NetworkDefaults.DefaultPort);
            TestAssert.True(parse.Success, "IPv4 endpoint should parse.");

            EndpointResolutionResult resolution = parse.Endpoint.Resolve();
            TestAssert.True(resolution.Success, "Parsed IPv4 endpoint should resolve without DNS.");
            TestAssert.Equal(IPAddress.Parse("127.0.0.1"), resolution.EndPoint.Address, "Resolved address should match.");
            TestAssert.Equal(7777, resolution.EndPoint.Port, "Resolved port should match.");
        }

        private static void LocalAddressHelperClassifiesPrivateRanges()
        {
            TestAssert.True(LocalNetworkAddressHelper.IsPrivateIPv4(IPAddress.Parse("10.0.0.1")), "10/8 should be private.");
            TestAssert.True(LocalNetworkAddressHelper.IsPrivateIPv4(IPAddress.Parse("172.16.0.1")), "172.16/12 lower bound should be private.");
            TestAssert.True(LocalNetworkAddressHelper.IsPrivateIPv4(IPAddress.Parse("172.31.255.254")), "172.16/12 upper bound should be private.");
            TestAssert.True(LocalNetworkAddressHelper.IsPrivateIPv4(IPAddress.Parse("192.168.1.1")), "192.168/16 should be private.");
            TestAssert.False(LocalNetworkAddressHelper.IsPrivateIPv4(IPAddress.Parse("172.32.0.1")), "172.32/16 should not be private.");
            TestAssert.False(LocalNetworkAddressHelper.IsPrivateIPv4(IPAddress.Parse("8.8.8.8")), "Public IPv4 should not be private.");
            TestAssert.True(LocalNetworkAddressHelper.IsLinkLocalIPv4(IPAddress.Parse("169.254.1.20")), "169.254/16 should be link-local.");
        }

        private static void LocalAddressHelperEnumeratesIPv4OnlyRecords()
        {
            LocalNetworkAddressInfo[] addresses = LocalNetworkAddressHelper.GetLocalIPv4Addresses();
            TestAssert.NotNull(addresses, "Local address enumeration should return an array.");
            for (int i = 0; i < addresses.Length; i++)
            {
                TestAssert.NotNull(addresses[i].Address, "Address record should include an IP address.");
                TestAssert.Equal(AddressFamily.InterNetwork, addresses[i].Address.AddressFamily, "Only IPv4 records should be returned.");
            }
        }

        private static void LocalLanSelectionDoesNotThrow()
        {
            LocalNetworkAddressSelection selection = LocalNetworkAddressHelper.SelectBestLanAddress();
            TestAssert.NotNull(selection, "LAN selection should return a result object.");
            if (selection.Success)
            {
                TestAssert.NotNull(selection.Address, "Successful LAN selection should include an address.");
                TestAssert.Equal(AddressFamily.InterNetwork, selection.Address.Address.AddressFamily, "Selected LAN address should be IPv4.");
                TestAssert.False(selection.Address.IsLoopback, "Selected LAN address should not be loopback.");
            }
            else
            {
                TestAssert.True(selection.Message.Length > 0, "Failed LAN selection should explain why no LAN address was selected.");
            }
        }

        private static void SessionJoinsManualEndpointString()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                int clientConnected = 0;
                client.PeerConnected += delegate { clientConnected++; };

                host.StartHost(NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests"));
                client.Join("127.0.0.1:" + host.LocalEndPoint.Port, NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests"));

                NetworkTestUtilities.PumpUntil(host, client, delegate
                {
                    return clientConnected == 1 && client.State == NetworkSessionState.Connected;
                }, "Client did not connect through manual endpoint string.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }
    }
}
