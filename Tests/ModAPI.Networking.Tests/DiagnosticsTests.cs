using System;
using System.Collections.Generic;
using System.Net;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class DiagnosticsTests
    {
        private const ushort TestMessageType = SessionMessageTypes.FirstApplicationMessageType + 20;

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Diagnostics snapshot includes packet and byte counters", DiagnosticsSnapshotIncludesCounters));
            tests.Add(new TestCase("Diagnostics ring buffer respects configured bounds", DiagnosticsRingBufferRespectsBounds));
            tests.Add(new TestCase("Diagnostics snapshot tracks last peer error", DiagnosticsSnapshotTracksLastError));
            tests.Add(new TestCase("Diagnostics estimates heartbeat latency after ACK timing", DiagnosticsEstimatesHeartbeatLatency));
        }

        private static void DiagnosticsSnapshotIncludesCounters()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                byte[] receivedPayload = null;

                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    receivedPayload = NetworkTestUtilities.CopyPayload(e, TestMessageType);
                };

                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.DiagnosticsTests");
                byte[] payload = new byte[] { 1, 3, 5, 7, 9 };
                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Unreliable, payload), "Client send should succeed.");
                NetworkTestUtilities.PumpUntil(host, client, delegate { return receivedPayload != null; }, "Host did not receive diagnostics payload.");

                NetworkDiagnosticsSnapshot hostSnapshot = host.GetDiagnosticsSnapshot();
                NetworkDiagnosticsSnapshot clientSnapshot = client.GetDiagnosticsSnapshot();
                NetworkPeerDiagnosticsSnapshot hostPeer = FindPeer(hostSnapshot, client.LocalPeerId);
                NetworkPeerDiagnosticsSnapshot clientPeer = FindPeer(clientSnapshot, NetworkDefaults.HostPeerId);

                TestAssert.Equal(NetworkSessionMode.Host, hostSnapshot.Mode, "Host snapshot should report host mode.");
                TestAssert.Equal(NetworkSessionState.Listening, hostSnapshot.State, "Host snapshot should report listening state.");
                TestAssert.Equal(NetworkDefaults.HostPeerId, hostSnapshot.LocalPeerId, "Host snapshot should report host peer id.");
                TestAssert.True(hostPeer.PacketsReceived > 0, "Host peer diagnostics should count received packets.");
                TestAssert.True(hostPeer.BytesReceived > 0, "Host peer diagnostics should count received bytes.");
                TestAssert.True(clientPeer.PacketsSent > 0, "Client peer diagnostics should count sent packets.");
                TestAssert.True(clientPeer.BytesSent > 0, "Client peer diagnostics should count sent bytes.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void DiagnosticsRingBufferRespectsBounds()
        {
            NetworkConfig hostConfig = NetworkTestUtilities.CreateLoopbackConfig();
            NetworkConfig clientConfig = NetworkTestUtilities.CreateLoopbackConfig();
            hostConfig.DiagnosticsEventCapacity = 3;
            clientConfig.DiagnosticsEventCapacity = 3;

            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(hostConfig);
                client = new NetworkSession(clientConfig);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.DiagnosticsTests");

                for (int i = 0; i < 6; i++)
                {
                    client.SendToHost(TestMessageType, NetworkChannel.Unreliable, new byte[] { (byte)i });
                    NetworkTestUtilities.PumpOnce(host, client);
                }

                NetworkDiagnosticsSnapshot hostSnapshot = host.GetDiagnosticsSnapshot();
                NetworkDiagnosticsSnapshot clientSnapshot = client.GetDiagnosticsSnapshot();
                TestAssert.True(hostSnapshot.RecentEvents.Length > 0, "Host diagnostics event buffer should retain recent events.");
                TestAssert.True(hostSnapshot.RecentEvents.Length <= 3, "Host diagnostics event buffer should not exceed the configured bound.");
                TestAssert.True(clientSnapshot.RecentEvents.Length <= 3, "Client diagnostics event buffer should not exceed the configured bound.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void DiagnosticsSnapshotTracksLastError()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());

                host.StartHost(NetworkTestUtilities.CreateOptions("Expected.App"));
                client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), NetworkTestUtilities.CreateOptions("Wrong.App"));
                NetworkTestUtilities.PumpUntil(host, client, delegate { return client.State == NetworkSessionState.Failed; },
                    "Client did not fail after application mismatch.");

                NetworkDiagnosticsSnapshot snapshot = client.GetDiagnosticsSnapshot();
                NetworkPeerDiagnosticsSnapshot peer = FindPeer(snapshot, NetworkDefaults.HostPeerId);
                TestAssert.True(peer.LastError.IndexOf("Application id mismatch", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Client diagnostics should retain the last peer error.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void DiagnosticsEstimatesHeartbeatLatency()
        {
            NetworkConfig hostConfig = NetworkTestUtilities.CreateLoopbackConfig();
            NetworkConfig clientConfig = NetworkTestUtilities.CreateLoopbackConfig();
            hostConfig.HeartbeatIntervalMilliseconds = 20;
            clientConfig.HeartbeatIntervalMilliseconds = 20;

            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(hostConfig);
                client = new NetworkSession(clientConfig);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.DiagnosticsTests");

                NetworkTestUtilities.PumpUntil(host, client, delegate
                {
                    NetworkPeerDiagnosticsSnapshot clientPeer = FindPeer(client.GetDiagnosticsSnapshot(), NetworkDefaults.HostPeerId);
                    return clientPeer.HeartbeatLatencyMilliseconds.HasValue;
                }, "Client did not estimate heartbeat latency from ACK timing.");

                NetworkPeerDiagnosticsSnapshot peer = FindPeer(client.GetDiagnosticsSnapshot(), NetworkDefaults.HostPeerId);
                TestAssert.True(peer.HeartbeatLatencyMilliseconds.HasValue, "Heartbeat latency should be present after enough timing data.");
                TestAssert.True(peer.HeartbeatLatencyMilliseconds.Value >= 0, "Heartbeat latency should not be negative.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static NetworkPeerDiagnosticsSnapshot FindPeer(NetworkDiagnosticsSnapshot snapshot, byte peerId)
        {
            for (int i = 0; i < snapshot.Peers.Length; i++)
            {
                if (snapshot.Peers[i].PeerId == peerId)
                    return snapshot.Peers[i];
            }

            throw new InvalidOperationException("Snapshot did not include peer " + peerId + ".");
        }
    }
}
