using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Reliability;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class CoreNetworkingExpansionTests
    {
        private const string TestApplicationId = "ModAPI.Networking.Tests";
        private const ushort TestMessageType = SessionMessageTypes.FirstApplicationMessageType;
        private const ushort AlternateMessageType = SessionMessageTypes.FirstApplicationMessageType + 1;

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("AckWindow handles ushort wraparound edges", AckWindowHandlesWraparoundEdges));
            tests.Add(new TestCase("MessageBatchReader rejects malformed headers and frames", MessageBatchReaderRejectsMalformedHeadersAndFrames));
            tests.Add(new TestCase("Session rejects malformed handshake packets", SessionRejectsMalformedHandshakePackets));
            tests.Add(new TestCase("Client handshake times out when no host responds", ClientHandshakeTimesOutWhenHostAbsent));
            tests.Add(new TestCase("Host times out a silent connected client", HostTimesOutSilentClient));
            tests.Add(new TestCase("Client times out a silent connected host", ClientTimesOutSilentHost));
            tests.Add(new TestCase("Client disconnect reports reason to host", ClientDisconnectReportsReasonToHost));
            tests.Add(new TestCase("Host disconnects a selected client", HostDisconnectsSelectedClient));
            tests.Add(new TestCase("Two-client localhost smoke covers broadcast and direct send", TwoClientLocalhostSmokeCoversBroadcastAndDirectSend));
            tests.Add(new TestCase("Two-process localhost harness completes reliable UDP round trip", TwoProcessLocalhostHarnessCompletesReliableUdpRoundTrip));
            tests.Add(new TestCase("Application sends flush in configured batches", ApplicationSendsFlushInConfiguredBatches));
            tests.Add(new TestCase("Repeated localhost sessions remain independent", RepeatedLocalhostSessionsRemainIndependent));
        }

        private static void AckWindowHandlesWraparoundEdges()
        {
            AckWindow window = new AckWindow();
            TestAssert.True(window.MarkReceived(65534), "Initial high sequence should be accepted.");
            TestAssert.True(window.MarkReceived(65535), "Sequence 65535 should advance from 65534.");
            TestAssert.True(window.MarkReceived(0), "Sequence 0 should advance after ushort wrap.");
            TestAssert.True(window.MarkReceived(65533), "Out-of-order wrapped sequence 65533 should fill ack bit 2.");
            TestAssert.True(window.IsAcked(65535, window.Latest, window.AckBits), "Sequence immediately before wrapped latest should be acked.");
            TestAssert.True(window.IsAcked(65534, window.Latest, window.AckBits), "Second sequence before wrapped latest should be acked.");
            TestAssert.True(window.IsAcked(65533, window.Latest, window.AckBits), "Late previous wrapped sequence should be acked.");
            TestAssert.False(window.MarkReceived(65533), "Duplicate old sequence should not be treated as newly received.");

            AckWindow boundary = new AckWindow();
            TestAssert.True(boundary.MarkReceived(20), "Boundary window should accept initial sequence.");
            TestAssert.True(boundary.MarkReceived(65524), "Sequence 65524 should be exactly 32 packets behind sequence 20 across wrap.");
            TestAssert.True(boundary.IsAcked(65524, boundary.Latest, boundary.AckBits), "Exactly 32 previous packets should be retained across wrap.");
            TestAssert.False(boundary.MarkReceived(65523), "Sequence 65523 is 33 packets behind and should be rejected.");
        }

        private static void MessageBatchReaderRejectsMalformedHeadersAndFrames()
        {
            byte[] invalidMagic = new byte[NetworkDefaults.HeaderSize];
            BitWriter invalidHeaderWriter = new BitWriter(invalidMagic);
            NetworkPacketHeader invalidHeader = NetworkPacketHeader.Create(1, 0, 0, PacketFlags.None, 1);
            invalidHeader.Magic = 0;
            invalidHeader.WriteTo(ref invalidHeaderWriter);

            MessageBatchReader invalidReader = new MessageBatchReader(invalidMagic, 0, invalidMagic.Length);
            NetworkMessage ignored;
            TestAssert.False(invalidReader.Header.IsValid, "Reader should mark packets with invalid magic as invalid.");
            TestAssert.False(invalidReader.TryReadNext(out ignored), "Reader should not return messages from invalid packets.");

            byte[] truncated = new byte[NetworkDefaults.HeaderSize + 5];
            BitWriter truncatedWriter = new BitWriter(truncated);
            NetworkPacketHeader validHeader = NetworkPacketHeader.Create(2, 0, 0, PacketFlags.None, 1);
            validHeader.WriteTo(ref truncatedWriter);
            truncatedWriter.WriteByte((byte)NetworkChannel.Unreliable);
            truncatedWriter.WriteUInt16(TestMessageType);
            truncatedWriter.WriteUInt16(4);

            MessageBatchReader truncatedReader = new MessageBatchReader(truncated, 0, truncated.Length);
            TestAssert.True(truncatedReader.Header.IsValid, "Truncated-frame packet should still have a valid header.");
            TestAssert.False(truncatedReader.TryReadNext(out ignored), "Reader should reject a frame whose declared payload overruns the packet.");
        }

        private static void SessionRejectsMalformedHandshakePackets()
        {
            NetworkSession host = null;
            UdpClient rawClient = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                int nonFatalErrors = 0;
                host.SessionError += delegate(object sender, NetworkSessionErrorEventArgs e)
                {
                    if (!e.IsFatal)
                        nonFatalErrors++;
                };

                host.StartHost(NetworkTestUtilities.CreateOptions(TestApplicationId));
                rawClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                byte[] malformed = BuildPacket(SessionMessageTypes.HandshakeRequest, NetworkChannel.Unreliable, new byte[0], 9);
                rawClient.Send(malformed, malformed.Length, new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port));

                NetworkTestUtilities.PumpUntil(host, delegate { return nonFatalErrors == 1; },
                    "Host did not report the malformed handshake packet as a non-fatal session error.");

                byte[] rejectPacket = NetworkTestUtilities.ReceiveUdp(rawClient, 1000,
                    "Raw malformed handshake sender did not receive a handshake reject packet.");
                MessageBatchReader rejectReader = new MessageBatchReader(rejectPacket, 0, rejectPacket.Length);
                NetworkMessage rejectMessage;
                TestAssert.True(rejectReader.TryReadNext(out rejectMessage), "Reject packet should contain one framed message.");
                TestAssert.Equal(SessionMessageTypes.HandshakeReject, rejectMessage.MessageType, "Malformed handshake should be answered with a reject message.");

                BitReader rejectPayload = new BitReader(rejectMessage.Payload, rejectMessage.Offset, rejectMessage.Length);
                NetworkHandshakeReject reject = NetworkHandshakeReject.ReadFrom(ref rejectPayload);
                TestAssert.Equal(HandshakeRejectReason.MalformedRequest, reject.Reason, "Malformed handshake should use malformed-request reject reason.");
                TestAssert.Equal(0, host.GetPeers().Length, "Malformed handshakes must not create peers.");
                TestAssert.Equal(NetworkSessionState.Listening, host.State, "Host should keep listening after rejecting a malformed packet.");
            }
            finally
            {
                if (rawClient != null)
                    rawClient.Close();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void ClientHandshakeTimesOutWhenHostAbsent()
        {
            NetworkSession client = null;
            UdpClient unusedListener = null;
            try
            {
                unusedListener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                int unusedPort = ((IPEndPoint)unusedListener.Client.LocalEndPoint).Port;

                client = new NetworkSession(NetworkTestUtilities.CreateFastTimeoutConfig());
                HandshakeRejectReason failureReason = HandshakeRejectReason.None;
                string failureMessage = string.Empty;
                client.ConnectionFailed += delegate(object sender, NetworkConnectionFailedEventArgs e)
                {
                    failureReason = e.Reason;
                    failureMessage = e.Message;
                };

                client.Join(new IPEndPoint(IPAddress.Loopback, unusedPort), NetworkTestUtilities.CreateOptions(TestApplicationId));
                NetworkTestUtilities.PumpUntil(new NetworkSession[] { client }, delegate { return client.State == NetworkSessionState.Failed; },
                    "Client did not fail after the handshake timeout window.", 1000);

                TestAssert.Equal(HandshakeRejectReason.Unknown, failureReason, "Handshake timeout should report unknown handshake failure.");
                TestAssert.True(failureMessage.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Handshake timeout failure message should mention the timeout.");
            }
            finally
            {
                if (unusedListener != null)
                    unusedListener.Close();
                if (client != null)
                    client.Dispose();
            }
        }

        private static void HostTimesOutSilentClient()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateFastTimeoutConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateFastTimeoutConfig());
                int hostDisconnected = 0;
                NetworkDisconnectReason observedReason = NetworkDisconnectReason.None;
                host.PeerDisconnected += delegate(object sender, NetworkPeerDisconnectedEventArgs e)
                {
                    hostDisconnected++;
                    observedReason = e.Reason;
                };

                NetworkTestUtilities.Connect(host, client, TestApplicationId);
                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host }, delegate { return hostDisconnected == 1; },
                    "Host did not time out a connected client that stopped pumping.", 1000);

                TestAssert.Equal(NetworkDisconnectReason.Timeout, observedReason, "Host should report timeout when a connected client goes silent.");
                TestAssert.Equal(0, host.GetPeers().Length, "Timed-out client should be removed from the host registry.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void ClientTimesOutSilentHost()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateFastTimeoutConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateFastTimeoutConfig());
                int clientDisconnected = 0;
                NetworkDisconnectReason observedReason = NetworkDisconnectReason.None;
                client.PeerDisconnected += delegate(object sender, NetworkPeerDisconnectedEventArgs e)
                {
                    clientDisconnected++;
                    observedReason = e.Reason;
                };

                NetworkTestUtilities.Connect(host, client, TestApplicationId);
                NetworkTestUtilities.PumpUntil(new NetworkSession[] { client }, delegate { return client.State == NetworkSessionState.Failed; },
                    "Client did not time out a connected host that stopped pumping.", 1000);

                TestAssert.Equal(1, clientDisconnected, "Client should raise one host-disconnected event on timeout.");
                TestAssert.Equal(NetworkDisconnectReason.Timeout, observedReason, "Client should report host timeout as the disconnect reason.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void ClientDisconnectReportsReasonToHost()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                int hostDisconnected = 0;
                NetworkDisconnectReason reason = NetworkDisconnectReason.None;
                string message = string.Empty;
                host.PeerDisconnected += delegate(object sender, NetworkPeerDisconnectedEventArgs e)
                {
                    hostDisconnected++;
                    reason = e.Reason;
                    message = e.Message;
                };

                NetworkTestUtilities.Connect(host, client, TestApplicationId);
                client.Disconnect(NetworkDisconnectReason.RemoteClosed, "client leaving");
                NetworkTestUtilities.PumpUntil(host, client, delegate { return hostDisconnected == 1; },
                    "Host did not observe the client's graceful disconnect.");

                TestAssert.Equal(NetworkDisconnectReason.RemoteClosed, reason, "Host should surface the client's disconnect reason.");
                TestAssert.Equal("client leaving", message, "Host should surface the client's disconnect message.");
                TestAssert.Equal(0, host.GetPeers().Length, "Client disconnect should remove the peer from the host registry.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void HostDisconnectsSelectedClient()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                int clientDisconnected = 0;
                NetworkDisconnectReason reason = NetworkDisconnectReason.None;
                client.PeerDisconnected += delegate(object sender, NetworkPeerDisconnectedEventArgs e)
                {
                    clientDisconnected++;
                    reason = e.Reason;
                };

                NetworkTestUtilities.Connect(host, client, TestApplicationId);
                host.DisconnectPeer(client.LocalPeerId, NetworkDisconnectReason.ProtocolError, "host removed client");
                NetworkTestUtilities.PumpUntil(host, client, delegate { return clientDisconnected == 1; },
                    "Client did not observe the host-selected disconnect.");

                TestAssert.Equal(NetworkDisconnectReason.ProtocolError, reason, "Client should surface the host-supplied disconnect reason.");
                TestAssert.Equal(NetworkSessionState.Stopped, client.State, "Client should stop after a host disconnect packet.");
                TestAssert.Equal(0, host.GetPeers().Length, "Host should remove the selected peer immediately.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void TwoClientLocalhostSmokeCoversBroadcastAndDirectSend()
        {
            NetworkSession host = null;
            NetworkSession clientA = null;
            NetworkSession clientB = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                clientA = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                clientB = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                int hostConnected = 0;
                int clientAConnected = 0;
                int clientBConnected = 0;
                byte[] clientABroadcast = null;
                byte[] clientBBroadcast = null;
                byte[] clientADirect = null;
                byte[] clientBDirect = null;

                host.PeerConnected += delegate { hostConnected++; };
                clientA.PeerConnected += delegate { clientAConnected++; };
                clientB.PeerConnected += delegate { clientBConnected++; };
                clientA.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    byte[] copied = NetworkTestUtilities.CopyPayload(e, AlternateMessageType);
                    if (copied != null)
                        clientABroadcast = copied;
                    copied = NetworkTestUtilities.CopyPayload(e, TestMessageType);
                    if (copied != null)
                        clientADirect = copied;
                };
                clientB.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    byte[] copied = NetworkTestUtilities.CopyPayload(e, AlternateMessageType);
                    if (copied != null)
                        clientBBroadcast = copied;
                    copied = NetworkTestUtilities.CopyPayload(e, TestMessageType);
                    if (copied != null)
                        clientBDirect = copied;
                };

                host.StartHost(NetworkTestUtilities.CreateOptions(TestApplicationId));
                clientA.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), NetworkTestUtilities.CreateOptions(TestApplicationId));
                clientB.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), NetworkTestUtilities.CreateOptions(TestApplicationId));
                NetworkSession[] sessions = new NetworkSession[] { host, clientA, clientB };
                NetworkTestUtilities.PumpUntil(sessions, delegate
                {
                    return hostConnected == 2 && clientAConnected == 1 && clientBConnected == 1
                        && clientA.State == NetworkSessionState.Connected && clientB.State == NetworkSessionState.Connected;
                }, "Host and two localhost clients did not all connect.");

                byte[] broadcastPayload = new byte[] { 7, 7, 7, 7 };
                TestAssert.Equal(2, host.Broadcast(AlternateMessageType, NetworkChannel.Unreliable, broadcastPayload),
                    "Host broadcast should send to both connected clients.");
                NetworkTestUtilities.PumpUntil(sessions, delegate { return clientABroadcast != null && clientBBroadcast != null; },
                    "Both clients did not receive the host broadcast.");
                TestAssert.BytesEqual(broadcastPayload, clientABroadcast, "Client A should receive exact broadcast bytes.");
                TestAssert.BytesEqual(broadcastPayload, clientBBroadcast, "Client B should receive exact broadcast bytes.");

                byte[] directPayload = new byte[] { 5, 4, 3, 2, 1 };
                TestAssert.True(host.SendToPeer(clientA.LocalPeerId, TestMessageType, NetworkChannel.Unreliable, directPayload),
                    "Host direct send to client A should succeed.");
                NetworkTestUtilities.PumpUntil(sessions, delegate { return clientADirect != null; },
                    "Client A did not receive host direct payload.");
                TestAssert.BytesEqual(directPayload, clientADirect, "Client A should receive exact direct payload bytes.");
                TestAssert.True(clientBDirect == null, "Client B should not receive payload sent only to client A.");
            }
            finally
            {
                if (clientB != null)
                    clientB.Dispose();
                if (clientA != null)
                    clientA.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void TwoProcessLocalhostHarnessCompletesReliableUdpRoundTrip()
        {
            NetworkTestUtilities.RunTwoProcessLocalhostHarness();
        }

        private static void RepeatedLocalhostSessionsRemainIndependent()
        {
            RunSingleRoundTripSession(new byte[] { 3, 1, 4 }, "first repeated session");
            RunSingleRoundTripSession(new byte[] { 1, 5, 9, 2 }, "second repeated session");
        }

        private static void ApplicationSendsFlushInConfiguredBatches()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig hostConfig = NetworkTestUtilities.CreateLoopbackConfig();
                NetworkConfig clientConfig = NetworkTestUtilities.CreateLoopbackConfig();
                hostConfig.FlushIntervalMilliseconds = 200;
                clientConfig.FlushIntervalMilliseconds = 200;
                hostConfig.HeartbeatIntervalMilliseconds = 1000;
                clientConfig.HeartbeatIntervalMilliseconds = 1000;
                host = new NetworkSession(hostConfig);
                client = new NetworkSession(clientConfig);

                int received = 0;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        received++;
                };

                NetworkTestUtilities.Connect(host, client, TestApplicationId);
                long sentBefore = client.GetDiagnosticsSnapshot().Peers[0].PacketsSent;

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Unreliable, new byte[] { 1 }),
                    "First queued send should be accepted.");
                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Unreliable, new byte[] { 2 }),
                    "Second queued send should be accepted.");

                NetworkTestUtilities.PumpOnce(host, client);
                TestAssert.Equal(0, received, "Application messages should wait for the configured flush interval.");

                NetworkTestUtilities.PumpUntil(host, client, delegate { return received == 2; },
                    "Host did not receive both batched application messages.");

                long sentAfter = client.GetDiagnosticsSnapshot().Peers[0].PacketsSent;
                TestAssert.Equal(sentBefore + 1, sentAfter, "Two queued application messages should flush as one packet.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void RunSingleRoundTripSession(byte[] payload, string label)
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                byte[] received = null;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    byte[] copied = NetworkTestUtilities.CopyPayload(e, TestMessageType);
                    if (copied != null)
                        received = copied;
                };

                NetworkTestUtilities.Connect(host, client, TestApplicationId);
                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Unreliable, payload), label + " send-to-host should succeed.");
                NetworkTestUtilities.PumpUntil(host, client, delegate { return received != null; },
                    "Host did not receive payload in " + label + ".");
                TestAssert.BytesEqual(payload, received, "Host should receive exact bytes in " + label + ".");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static byte[] BuildPacket(ushort messageType, NetworkChannel channel, byte[] payload, ushort sequence)
        {
            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            MessageBatchBuilder builder = new MessageBatchBuilder(buffer);
            TestAssert.True(builder.TryAdd(new NetworkMessage(messageType, channel, payload, 0, payload.Length)),
                "Test packet payload should fit in one datagram.");
            builder.WriteHeader(sequence, 0, 0);
            byte[] packet = new byte[builder.Length];
            Buffer.BlockCopy(buffer, 0, packet, 0, packet.Length);
            return packet;
        }
    }
}
