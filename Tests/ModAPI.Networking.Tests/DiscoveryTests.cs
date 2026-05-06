using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using ModAPI.Networking.Discovery;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class DiscoveryTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Discovery request serializes", DiscoveryRequestRoundTrips));
            tests.Add(new TestCase("Discovery response serializes", DiscoveryResponseRoundTrips));
            tests.Add(new TestCase("Broadcast discovery can be disabled", BroadcastDiscoveryCanBeDisabled));
            tests.Add(new TestCase("Loopback discovery finds listening host", LoopbackDiscoveryFindsListeningHost));
        }

        private static void DiscoveryRequestRoundTrips()
        {
            NetworkDiscoveryRequest request = new NetworkDiscoveryRequest();
            request.ApplicationId = "ModAPI.Networking.Tests";
            request.SessionId = "session-a";

            BitReader reader = CreateReader(request.WriteTo);
            NetworkDiscoveryRequest actual = NetworkDiscoveryRequest.ReadFrom(ref reader);

            TestAssert.Equal(NetworkDefaults.ProtocolVersion, actual.ProtocolVersion, "Protocol version should round-trip.");
            TestAssert.Equal(request.ApplicationId, actual.ApplicationId, "Application id should round-trip.");
            TestAssert.Equal(request.SessionId, actual.SessionId, "Session id should round-trip.");
        }

        private static void DiscoveryResponseRoundTrips()
        {
            NetworkDiscoveryResponse response = new NetworkDiscoveryResponse();
            response.ApplicationId = "ModAPI.Networking.Tests";
            response.SessionId = "session-b";
            response.PeerCount = 2;
            response.MaxPeers = 4;
            response.DisplayName = "Local host";

            BitReader reader = CreateReader(response.WriteTo);
            NetworkDiscoveryResponse actual = NetworkDiscoveryResponse.ReadFrom(ref reader);

            TestAssert.Equal(NetworkDefaults.ProtocolVersion, actual.ProtocolVersion, "Protocol version should round-trip.");
            TestAssert.Equal(response.ApplicationId, actual.ApplicationId, "Application id should round-trip.");
            TestAssert.Equal(response.SessionId, actual.SessionId, "Session id should round-trip.");
            TestAssert.Equal(response.PeerCount, actual.PeerCount, "Peer count should round-trip.");
            TestAssert.Equal(response.MaxPeers, actual.MaxPeers, "Max peers should round-trip.");
            TestAssert.Equal(response.DisplayName, actual.DisplayName, "Display name should round-trip.");
        }

        private static void BroadcastDiscoveryCanBeDisabled()
        {
            NetworkConfig config = NetworkTestUtilities.CreateLoopbackConfig();
            config.EnableBroadcastDiscovery = false;
            NetworkDiscoveryClient client = new NetworkDiscoveryClient(config);
            NetworkDiscoveryOptions options = NetworkDiscoveryOptions.CreateDefault();
            options.Port = 9;
            options.TimeoutMilliseconds = 50;

            NetworkDiscoveryResult[] results = client.DiscoverBroadcast(options);
            TestAssert.Equal(0, results.Length, "Disabled broadcast discovery should return no results.");
        }

        private static void LoopbackDiscoveryFindsListeningHost()
        {
            NetworkConfig config = NetworkTestUtilities.CreateLoopbackConfig();
            config.EnableBroadcastDiscovery = true;
            config.DiscoveryTimeoutMilliseconds = 250;

            NetworkSession host = null;
            ManualResetEvent stopPump = new ManualResetEvent(false);
            Thread pumpThread = null;
            try
            {
                host = new NetworkSession(config);
                NetworkSessionOptions hostOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                hostOptions.SessionId = "discovery-loopback";
                hostOptions.DisplayName = "Discovery Host";
                host.StartHost(hostOptions);

                NetworkSession capturedHost = host;
                pumpThread = new Thread(delegate()
                {
                    while (!stopPump.WaitOne(10, false))
                        capturedHost.Update();
                });
                pumpThread.IsBackground = true;
                pumpThread.Name = "ModAPI.Networking.Tests.DiscoveryPump";
                pumpThread.Start();

                NetworkDiscoveryOptions discoveryOptions = NetworkDiscoveryOptions.CreateDefault();
                discoveryOptions.ApplicationId = hostOptions.ApplicationId;
                discoveryOptions.SessionId = hostOptions.SessionId;
                discoveryOptions.Port = host.LocalEndPoint.Port;
                discoveryOptions.TimeoutMilliseconds = 500;

                NetworkDiscoveryClient client = new NetworkDiscoveryClient(config);
                NetworkDiscoveryResult[] results = client.DiscoverEndpoint(
                    new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port),
                    discoveryOptions);

                TestAssert.Equal(1, results.Length, "Loopback discovery should find the listening host.");
                TestAssert.Equal(hostOptions.ApplicationId, results[0].ApplicationId, "Result application id should match.");
                TestAssert.Equal(hostOptions.SessionId, results[0].SessionId, "Result session id should match.");
                TestAssert.Equal(hostOptions.DisplayName, results[0].DisplayName, "Result display name should match.");
                TestAssert.Equal(1, results[0].PeerCount, "Peer count should include the host.");
                TestAssert.Equal(hostOptions.MaxPeers, results[0].MaxPeers, "Max peers should match host options.");
                TestAssert.Equal(host.LocalEndPoint.Port, results[0].EndPoint.Port, "Endpoint port should be the host port.");
                TestAssert.Equal(NetworkSessionState.Listening, host.State, "Discovery must not change host session state.");
                TestAssert.Equal(0, host.GetPeers().Length, "Discovery must not create session peers.");
            }
            finally
            {
                stopPump.Set();
                if (pumpThread != null)
                    pumpThread.Join(500);
                stopPump.Close();
                if (host != null)
                    host.Dispose();
            }
        }

        private delegate void WritePayloadDelegate(ref BitWriter writer);

        private static BitReader CreateReader(WritePayloadDelegate writerCallback)
        {
            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            BitWriter writer = new BitWriter(buffer);
            writerCallback(ref writer);
            return new BitReader(buffer, 0, writer.Position);
        }
    }
}
