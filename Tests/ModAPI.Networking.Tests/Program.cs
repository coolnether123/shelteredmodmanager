using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using ModAPI.Networking;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Reliability;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class Program
    {
        private const ushort TestMessageType = SessionMessageTypes.FirstApplicationMessageType;

        private sealed class TestCase
        {
            public string Name;
            public Action Body;

            public TestCase(string name, Action body)
            {
                Name = name;
                Body = body;
            }
        }

        public static int Main(string[] args)
        {
            List<TestCase> tests = new List<TestCase>();
            tests.Add(new TestCase("AckWindow tracks wrap-safe 32 packet history", AckWindowTracksHistory));
            tests.Add(new TestCase("MessageBatch round-trips one message", MessageBatchRoundTrips));
            tests.Add(new TestCase("Session handshake connects and moves payload", SessionHandshakeMovesPayload));
            tests.Add(new TestCase("Session rejects incompatible application ids", SessionRejectsApplicationMismatch));
            tests.Add(new TestCase("Session rejects incompatible session ids", SessionRejectsSessionMismatch));

            int failed = 0;
            for (int i = 0; i < tests.Count; i++)
            {
                TestCase test = tests[i];
                try
                {
                    test.Body();
                    Console.WriteLine("[PASS] " + test.Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("[FAIL] " + test.Name);
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine();
            Console.WriteLine("Networking tests: " + (tests.Count - failed) + " passed, " + failed + " failed.");
            return failed == 0 ? 0 : 1;
        }

        private static void AckWindowTracksHistory()
        {
            AckWindow window = new AckWindow();
            TestAssert.True(window.MarkReceived(10), "Initial sequence should be accepted.");
            TestAssert.True(window.MarkReceived(9), "Previous sequence should fill ack bit 0.");
            TestAssert.True(window.IsAcked(9, window.Latest, window.AckBits), "Previous sequence should be acked.");
            TestAssert.True(window.MarkReceived(42), "Sequence 42 should be newer than sequence 10.");
            TestAssert.True(window.IsAcked(10, window.Latest, window.AckBits), "The 32nd previous sequence must remain acked.");
        }

        private static void MessageBatchRoundTrips()
        {
            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            byte[] payload = new byte[] { 1, 2, 3, 4 };
            MessageBatchBuilder builder = new MessageBatchBuilder(buffer);
            TestAssert.True(builder.TryAdd(new NetworkMessage(TestMessageType, NetworkChannel.Reliable, payload, 0, payload.Length)),
                "Message should fit in an empty packet.");
            builder.WriteHeader(7, 6, 1);

            MessageBatchReader reader = new MessageBatchReader(buffer, 0, builder.Length);
            TestAssert.True(reader.Header.IsValid, "Header should be valid.");
            TestAssert.Equal((byte)1, reader.Header.MessageCount, "Header should record one message.");

            NetworkMessage message;
            TestAssert.True(reader.TryReadNext(out message), "Reader should return the batched message.");
            TestAssert.Equal(TestMessageType, message.MessageType, "Message type should round-trip.");
            TestAssert.Equal(NetworkChannel.Reliable, message.Channel, "Channel should round-trip.");

            byte[] actual = new byte[message.Length];
            Buffer.BlockCopy(message.Payload, message.Offset, actual, 0, message.Length);
            TestAssert.BytesEqual(payload, actual, "Payload should round-trip.");
        }

        private static void SessionHandshakeMovesPayload()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(CreateLoopbackConfig());
                client = new NetworkSession(CreateLoopbackConfig());

                int hostConnected = 0;
                int clientConnected = 0;
                int hostDisconnected = 0;
                byte hostSeenPeerId = NetworkDefaults.UnassignedPeerId;
                byte[] receivedPayload = null;

                host.PeerConnected += delegate(object sender, NetworkPeerEventArgs e)
                {
                    hostConnected++;
                    hostSeenPeerId = e.Peer.PeerId;
                };
                client.PeerConnected += delegate { clientConnected++; };
                host.PeerDisconnected += delegate { hostDisconnected++; };
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        receivedPayload = e.Payload;
                };

                NetworkSessionOptions options = CreateOptions("ModAPI.Networking.Tests");
                host.StartHost(options);
                client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), CreateOptions("ModAPI.Networking.Tests"));

                PumpUntil(host, client, delegate
                {
                    return hostConnected == 1 && clientConnected == 1 && client.State == NetworkSessionState.Connected;
                }, "Client and host did not complete handshake.");

                TestAssert.Equal(NetworkSessionState.Listening, host.State, "Host should keep listening after accepting one peer.");
                TestAssert.Equal(NetworkSessionState.Connected, client.State, "Client should be connected.");
                TestAssert.True(client.LocalPeerId != NetworkDefaults.UnassignedPeerId, "Client should receive a peer id.");

                byte[] payload = new byte[] { 9, 8, 7 };
                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Unreliable, payload), "Client send should succeed.");
                PumpUntil(host, client, delegate { return receivedPayload != null; }, "Host did not receive application payload.");
                TestAssert.BytesEqual(payload, receivedPayload, "Host should receive the exact payload bytes.");

                client.Disconnect(NetworkDisconnectReason.LocalShutdown, "test complete");
                PumpUntil(host, client, delegate { return hostDisconnected == 1; }, "Host did not observe client disconnect.");
                TestAssert.True(hostSeenPeerId != NetworkDefaults.UnassignedPeerId, "Host should assign a concrete peer id.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void SessionRejectsApplicationMismatch()
        {
            RunRejectedJoin(CreateOptions("Expected.App"), CreateOptions("Wrong.App"),
                HandshakeRejectReason.ApplicationMismatch);
        }

        private static void SessionRejectsSessionMismatch()
        {
            NetworkSessionOptions hostOptions = CreateOptions("ModAPI.Networking.Tests");
            NetworkSessionOptions clientOptions = CreateOptions("ModAPI.Networking.Tests");
            hostOptions.SessionId = "host-session";
            clientOptions.SessionId = "client-session";
            RunRejectedJoin(hostOptions, clientOptions, HandshakeRejectReason.SessionMismatch);
        }

        private static void RunRejectedJoin(
            NetworkSessionOptions hostOptions,
            NetworkSessionOptions clientOptions,
            HandshakeRejectReason expectedReason)
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(CreateLoopbackConfig());
                client = new NetworkSession(CreateLoopbackConfig());

                HandshakeRejectReason failureReason = HandshakeRejectReason.None;
                client.ConnectionFailed += delegate(object sender, NetworkConnectionFailedEventArgs e)
                {
                    failureReason = e.Reason;
                };

                host.StartHost(hostOptions);
                client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), clientOptions);

                PumpUntil(host, client, delegate { return failureReason != HandshakeRejectReason.None; },
                    "Client did not report the expected rejection.");

                TestAssert.Equal(expectedReason, failureReason, "Client should fail with the expected rejection reason.");
                TestAssert.Equal(NetworkSessionState.Failed, client.State, "Client should enter failed state after rejection.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static NetworkConfig CreateLoopbackConfig()
        {
            NetworkConfig config = NetworkConfig.CreateDefault();
            config.Port = 0;
            config.ConnectionTimeoutMilliseconds = 500;
            config.HandshakeRetryMilliseconds = 50;
            config.HandshakeTimeoutMilliseconds = 1000;
            config.HeartbeatIntervalMilliseconds = 100;
            return config;
        }

        private static NetworkSessionOptions CreateOptions(string applicationId)
        {
            NetworkSessionOptions options = NetworkSessionOptions.CreateDefault();
            options.ApplicationId = applicationId;
            options.SessionId = "loopback";
            options.DisplayName = "test";
            options.MaxPeers = 4;
            return options;
        }

        private static void PumpUntil(NetworkSession host, NetworkSession client, Func<bool> condition, string failureMessage)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                host.Update();
                client.Update();
                if (condition())
                    return;
                Thread.Sleep(10);
            }

            host.Update();
            client.Update();
            if (!condition())
                throw new InvalidOperationException(failureMessage);
        }
    }
}
