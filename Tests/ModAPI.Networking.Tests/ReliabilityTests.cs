using System;
using System.Collections.Generic;
using System.Net;
using ModAPI.Networking.Buffers;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Reliability;
using ModAPI.Networking.Sessions;
using ModAPI.Networking.Transport;

namespace ModAPI.Networking.Tests
{
    internal static class ReliabilityTests
    {
        private const ushort TestMessageType = SessionMessageTypes.FirstApplicationMessageType + 10;

        internal static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Reliable outbound queue removes ACKed packets", ReliableOutboundQueueRemovesAckedPackets));
            tests.Add(new TestCase("Reliable message delivers exactly once after ACK", ReliableMessageDeliversExactlyOnceAfterAck));
            tests.Add(new TestCase("Reliable ACK-only packets clear queue before heartbeat", ReliableAckOnlyPacketsClearQueueBeforeHeartbeat));
            tests.Add(new TestCase("Lost ACK causes reliable resend without duplicate delivery", LostAckCausesReliableResendWithoutDuplicateDelivery));
            tests.Add(new TestCase("Reliable packets resend after packet loss and clear after ACK", ReliablePacketsResendAndClearAfterAck));
            tests.Add(new TestCase("Reliable resend timeout marks sender failed predictably", ReliableResendTimeoutMarksSenderFailedPredictably));
            tests.Add(new TestCase("Multiple reliable messages in one packet deliver once", MultipleReliableMessagesInOnePacketDeliverOnce));
            tests.Add(new TestCase("Out-of-order reliable packets deliver unordered", OutOfOrderReliablePacketsDeliverUnordered));
            tests.Add(new TestCase("Reliable receive suppresses duplicate packets", ReliableReceiveSuppressesDuplicatePackets));
            tests.Add(new TestCase("Unreliable packets stay fire-and-forget after loss", UnreliablePacketsDoNotResend));
        }

        private static void ReliableOutboundQueueRemovesAckedPackets()
        {
            ReliableOutboundQueue queue = new ReliableOutboundQueue();
            byte[] packet = new byte[NetworkDefaults.HeaderSize];
            DateTime now = DateTime.UtcNow;

            queue.TrackSent(10, packet, 0, packet.Length, PacketFlags.HasReliableMessages, 1, now);
            queue.TrackSent(11, packet, 0, packet.Length, PacketFlags.HasReliableMessages, 1, now);
            queue.TrackSent(12, packet, 0, packet.Length, PacketFlags.HasReliableMessages, 1, now);

            TestAssert.Equal(3, queue.Count, "Queue should track all sent reliable packets.");
            TestAssert.Equal(2, queue.ProcessAcks(12, 1), "ACK 12 plus bit 0 should remove packets 12 and 11.");
            TestAssert.Equal(1, queue.Count, "Only the unacked packet should remain.");
            TestAssert.Equal(1, queue.ProcessAcks(10, 0), "Cumulative ACK should remove the final packet.");
            TestAssert.Equal(0, queue.Count, "Queue should be empty after all ACKs.");
        }

        private static void ReliableMessageDeliversExactlyOnceAfterAck()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                config.HeartbeatIntervalMilliseconds = 1000;
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                int received = 0;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        received++;
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 8 }),
                    "Reliable send should be accepted.");

                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return received == 1 && ClientReliableQueueCount(client) == 0;
                }, "Reliable payload was not delivered and ACKed.", 1000);

                DateTime settleUntil = DateTime.UtcNow.AddMilliseconds(150);
                while (DateTime.UtcNow < settleUntil)
                {
                    NetworkTestUtilities.PumpOnce(host, client);
                    System.Threading.Thread.Sleep(10);
                }

                TestAssert.Equal(1, received, "Reliable payload should be surfaced exactly once after a clean ACK.");
                TestAssert.Equal(1, transports.ClientToHostReliableApplicationSends,
                    "Clean reliable delivery should not retransmit.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void ReliableAckOnlyPacketsClearQueueBeforeHeartbeat()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                config.AckFlushMilliseconds = 10;
                config.ReliableResendMilliseconds = 200;
                config.HeartbeatIntervalMilliseconds = 1000;
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                int received = 0;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        received++;
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 9 }),
                    "Reliable send should be accepted.");

                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return received == 1 && ClientReliableQueueCount(client) == 0;
                }, "ACK-only packet did not clear the reliable queue before heartbeat/resend.", 1000);

                TestAssert.Equal(1, transports.ClientToHostReliableApplicationSends,
                    "Reliable sender should not retransmit while waiting for the next heartbeat ACK.");
                TestAssert.True(transports.HostToClientAckOnlySends > 0,
                    "Receiver should send a compact ACK-only packet for reliable data.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void LostAckCausesReliableResendWithoutDuplicateDelivery()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                config.AckFlushMilliseconds = 10;
                config.ReliableResendMilliseconds = 40;
                config.HeartbeatIntervalMilliseconds = 1000;
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                transports.DropNextHostToClientAckOnly = true;

                int received = 0;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        received++;
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 4 }),
                    "Reliable send should be accepted.");

                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return ClientReliableQueueCount(client) == 0
                        && transports.HostToClientAckOnlyDrops == 1
                        && transports.ClientToHostReliableApplicationSends >= 2;
                }, "Lost ACK did not force a reliable resend that later cleared.", 2000);

                TestAssert.Equal(1, transports.HostToClientAckOnlyDrops,
                    "The first receiver ACK-only packet should have been dropped by the test transport.");
                TestAssert.True(transports.ClientToHostReliableApplicationSends >= 2,
                    "Sender should resend when the ACK is lost.");
                TestAssert.Equal(1, received,
                    "Receiver should suppress the duplicate reliable packet caused by the lost ACK.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void ReliablePacketsResendAndClearAfterAck()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                transports.DropNextClientToHostReliableApplication = true;

                int received = 0;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        received++;
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 1 }),
                    "Reliable send should be accepted.");

                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return received == 1 && ClientReliableQueueCount(client) == 0;
                }, "Reliable packet was not resent, delivered once, and removed after ACK.", 2000);

                TestAssert.Equal(1, transports.ClientToHostReliableApplicationDrops,
                    "The first reliable application datagram should have been dropped by the test transport.");
                TestAssert.True(transports.ClientToHostReliableApplicationSends >= 2,
                    "Reliable delivery should retransmit after the configured resend interval.");
                TestAssert.Equal(1, received, "Receiver should surface the reliable payload once.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void ReliableResendTimeoutMarksSenderFailedPredictably()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                config.ConnectionTimeoutMilliseconds = 180;
                config.ReliableResendMilliseconds = 30;
                config.HeartbeatIntervalMilliseconds = 40;
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                transports.DropAllClientToHostReliableApplication = true;

                int hostReceived = 0;
                int clientDisconnected = 0;
                NetworkDisconnectReason reason = NetworkDisconnectReason.None;
                string disconnectMessage = string.Empty;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        hostReceived++;
                };
                client.PeerDisconnected += delegate(object sender, NetworkPeerDisconnectedEventArgs e)
                {
                    clientDisconnected++;
                    reason = e.Reason;
                    disconnectMessage = e.Message;
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 6 }),
                    "Reliable send should be accepted.");

                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return client.State == NetworkSessionState.Failed && clientDisconnected == 1;
                }, "Sender did not fail predictably after the reliable resend timeout.", 2000);

                TestAssert.Equal(0, hostReceived, "Dropped reliable packet should never reach the receiver.");
                TestAssert.True(transports.ClientToHostReliableApplicationSends >= 2,
                    "Sender should attempt reliable retries before timing out.");
                TestAssert.Equal(NetworkDisconnectReason.Timeout, reason,
                    "Reliable resend expiry should report a timeout disconnect.");
                TestAssert.True(disconnectMessage.IndexOf("Reliable-unordered packet", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Reliable timeout diagnostics should identify the unacked reliable packet.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void MultipleReliableMessagesInOnePacketDeliverOnce()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                config.HeartbeatIntervalMilliseconds = 1000;
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                List<byte> received = new List<byte>();
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType && e.Payload.Length == 1)
                        received.Add(e.Payload[0]);
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 11 }),
                    "First reliable send should be accepted.");
                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 12 }),
                    "Second reliable send should be accepted.");

                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return received.Count == 2 && ClientReliableQueueCount(client) == 0;
                }, "Batched reliable messages were not delivered and ACKed.", 1000);

                TestAssert.Equal(1, transports.ClientToHostReliableApplicationSends,
                    "Two queued reliable messages to the same peer should share one packet.");
                TestAssert.Equal(2, received.Count, "Both reliable messages should be surfaced exactly once.");
                TestAssert.Equal((byte)11, received[0], "First batched reliable payload should be delivered.");
                TestAssert.Equal((byte)12, received[1], "Second batched reliable payload should be delivered.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void OutOfOrderReliablePacketsDeliverUnordered()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                config.ReliableResendMilliseconds = 500;
                config.HeartbeatIntervalMilliseconds = 1000;
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                List<byte> received = new List<byte>();
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType && e.Payload.Length == 1)
                        received.Add(e.Payload[0]);
                };

                transports.HoldNextClientToHostReliableApplication = true;
                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 1 }),
                    "First reliable send should be accepted.");
                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return transports.HeldClientToHostReliableApplicationPackets == 1;
                }, "First reliable packet was not held by the test transport.", 1000);

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 2 }),
                    "Second reliable send should be accepted.");
                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return received.Count == 1 && received[0] == 2;
                }, "Second reliable packet did not arrive ahead of the held first packet.", 1000);

                transports.ReleaseHeldClientToHostReliablePackets();
                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return received.Count == 2 && ClientReliableQueueCount(client) == 0;
                }, "Held out-of-order reliable packet was not delivered and ACKed.", 1000);

                TestAssert.Equal((byte)2, received[0],
                    "Reliable-unordered delivery should surface the newer packet when it arrives first.");
                TestAssert.Equal((byte)1, received[1],
                    "Reliable-unordered delivery should surface the older missing packet when it arrives later.");
                TestAssert.Equal(2, transports.ClientToHostReliableApplicationSends,
                    "Out-of-order delivery test should send two application packets without relying on resend.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void ReliableReceiveSuppressesDuplicatePackets()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                transports.DuplicateNextClientToHostReliableApplication = true;

                int received = 0;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        received++;
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Reliable, new byte[] { 2 }),
                    "Reliable send should be accepted.");

                NetworkTestUtilities.PumpUntil(new NetworkSession[] { host, client }, delegate
                {
                    return transports.ClientToHostReliableApplicationDuplicates == 1 && received == 1;
                }, "Duplicate reliable packet was not delivered through the test transport.");

                TestAssert.Equal(1, received, "Duplicate reliable datagrams must not raise duplicate messages.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void UnreliablePacketsDoNotResend()
        {
            PairedMemoryTransportPair transports = PairedMemoryTransportPair.Create();
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateReliableTestConfig();
                host = new NetworkSession(config, transports.Host);
                client = new NetworkSession(config, transports.Client);
                NetworkTestUtilities.Connect(host, client, "ModAPI.Networking.ReliabilityTests");

                transports.DropNextClientToHostUnreliableApplication = true;

                int received = 0;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == TestMessageType)
                        received++;
                };

                TestAssert.True(client.SendToHost(TestMessageType, NetworkChannel.Unreliable, new byte[] { 3 }),
                    "Unreliable send should be accepted.");

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(300);
                while (DateTime.UtcNow < deadline)
                {
                    NetworkTestUtilities.PumpOnce(host, client);
                    System.Threading.Thread.Sleep(10);
                }

                TestAssert.Equal(1, transports.ClientToHostUnreliableApplicationDrops,
                    "The unreliable application datagram should have been dropped by the test transport.");
                TestAssert.Equal(1, transports.ClientToHostUnreliableApplicationSends,
                    "Unreliable application messages should not be resent.");
                TestAssert.Equal(0, received, "Dropped unreliable payload should not be delivered later.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static NetworkConfig CreateReliableTestConfig()
        {
            NetworkConfig config = NetworkTestUtilities.CreateLoopbackConfig();
            config.FlushIntervalMilliseconds = 10;
            config.AckFlushMilliseconds = 10;
            config.ReliableResendMilliseconds = 40;
            config.ConnectionTimeoutMilliseconds = 1000;
            config.HeartbeatIntervalMilliseconds = 40;
            return config;
        }

        private static int ClientReliableQueueCount(NetworkSession client)
        {
            return client.GetPeers()[0].ReliableOutbound.Count;
        }

        private sealed class PairedMemoryTransportPair
        {
            public PairedMemoryTransport Host;
            public PairedMemoryTransport Client;

            public bool DropNextClientToHostReliableApplication;
            public bool DropNextClientToHostUnreliableApplication;
            public bool DropNextHostToClientAckOnly;
            public bool DropAllClientToHostReliableApplication;
            public bool DuplicateNextClientToHostReliableApplication;
            public bool HoldNextClientToHostReliableApplication;
            public int ClientToHostReliableApplicationSends;
            public int ClientToHostUnreliableApplicationSends;
            public int ClientToHostReliableApplicationDrops;
            public int ClientToHostUnreliableApplicationDrops;
            public int ClientToHostReliableApplicationDuplicates;
            public int HostToClientAckOnlySends;
            public int HostToClientAckOnlyDrops;
            private readonly List<HeldPacket> _heldClientToHostReliablePackets = new List<HeldPacket>();

            public static PairedMemoryTransportPair Create()
            {
                PairedMemoryTransportPair pair = new PairedMemoryTransportPair();
                pair.Host = new PairedMemoryTransport(pair, true);
                pair.Client = new PairedMemoryTransport(pair, false);
                pair.Host.Partner = pair.Client;
                pair.Client.Partner = pair.Host;
                return pair;
            }

            public int HeldClientToHostReliableApplicationPackets
            {
                get { return _heldClientToHostReliablePackets.Count; }
            }

            public void ReleaseHeldClientToHostReliablePackets()
            {
                while (_heldClientToHostReliablePackets.Count > 0)
                {
                    HeldPacket held = _heldClientToHostReliablePackets[0];
                    _heldClientToHostReliablePackets.RemoveAt(0);
                    Host.Receive(held.RemoteEndPoint, held.Bytes);
                }
            }

            public bool ShouldDrop(PairedMemoryTransport sender, PacketDescription packet)
            {
                if (sender.IsHost)
                {
                    if (sender.IsHost && packet.IsAckOnly)
                    {
                        HostToClientAckOnlySends++;
                        if (DropNextHostToClientAckOnly)
                        {
                            DropNextHostToClientAckOnly = false;
                            HostToClientAckOnlyDrops++;
                            return true;
                        }
                    }

                    return false;
                }

                if (!packet.IsApplication)
                    return false;

                if (packet.Channel == NetworkChannel.Reliable)
                {
                    ClientToHostReliableApplicationSends++;
                    if (DropAllClientToHostReliableApplication)
                    {
                        ClientToHostReliableApplicationDrops++;
                        return true;
                    }

                    if (DropNextClientToHostReliableApplication)
                    {
                        DropNextClientToHostReliableApplication = false;
                        ClientToHostReliableApplicationDrops++;
                        return true;
                    }
                }
                else
                {
                    ClientToHostUnreliableApplicationSends++;
                    if (DropNextClientToHostUnreliableApplication)
                    {
                        DropNextClientToHostUnreliableApplication = false;
                        ClientToHostUnreliableApplicationDrops++;
                        return true;
                    }
                }

                return false;
            }

            public bool ShouldHold(PairedMemoryTransport sender, PacketDescription packet, byte[] bytes)
            {
                if (sender.IsHost || !packet.IsApplication || packet.Channel != NetworkChannel.Reliable)
                    return false;
                if (!HoldNextClientToHostReliableApplication)
                    return false;

                HoldNextClientToHostReliableApplication = false;
                byte[] copy = new byte[bytes.Length];
                Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
                _heldClientToHostReliablePackets.Add(new HeldPacket(sender.LocalEndPoint, copy));
                return true;
            }

            public bool ShouldDuplicate(PairedMemoryTransport sender, PacketDescription packet)
            {
                if (sender.IsHost || !packet.IsApplication || packet.Channel != NetworkChannel.Reliable)
                    return false;
                if (!DuplicateNextClientToHostReliableApplication)
                    return false;

                DuplicateNextClientToHostReliableApplication = false;
                ClientToHostReliableApplicationDuplicates++;
                return true;
            }

            private sealed class HeldPacket
            {
                public HeldPacket(IPEndPoint remoteEndPoint, byte[] bytes)
                {
                    RemoteEndPoint = remoteEndPoint;
                    Bytes = bytes;
                }

                public IPEndPoint RemoteEndPoint;
                public byte[] Bytes;
            }
        }

        private sealed class PairedMemoryTransport : INetworkTransport
        {
            private static int _nextPort = 40000;
            private readonly PairedMemoryTransportPair _pair;
            private readonly bool _isHost;
            private bool _running;

            public PairedMemoryTransport(PairedMemoryTransportPair pair, bool isHost)
            {
                _pair = pair;
                _isHost = isHost;
            }

            public event Action<ReceivedPacket> PacketReceived;
            public event Action<Exception> TransportError;

            public PairedMemoryTransport Partner;

            public bool IsHost { get { return _isHost; } }
            public bool IsRunning { get { return _running; } }
            public IPEndPoint LocalEndPoint { get; private set; }

            public void Start()
            {
                Start(0);
            }

            public void Start(int port)
            {
                if (port == 0)
                    port = _nextPort++;
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, port);
                _running = true;
            }

            public void Stop()
            {
                _running = false;
            }

            public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int count)
            {
                if (!_running || Partner == null || !Partner.IsRunning)
                    return;

                byte[] copy = new byte[count];
                Buffer.BlockCopy(buffer, offset, copy, 0, count);
                PacketDescription packet = PacketDescription.Read(copy, count);
                if (_pair.ShouldDrop(this, packet))
                    return;
                if (_pair.ShouldHold(this, packet, copy))
                    return;

                Partner.Receive(LocalEndPoint, copy);
                if (_pair.ShouldDuplicate(this, packet))
                {
                    byte[] duplicate = new byte[count];
                    Buffer.BlockCopy(copy, 0, duplicate, 0, count);
                    Partner.Receive(LocalEndPoint, duplicate);
                }
            }

            public void Dispose()
            {
                Stop();
            }

            public void Receive(IPEndPoint remoteEndPoint, byte[] bytes)
            {
                Action<ReceivedPacket> handler = PacketReceived;
                if (handler == null)
                    return;

                handler(new ReceivedPacket(remoteEndPoint, new PooledBuffer(null, bytes), bytes.Length));
            }

            private void RaiseTransportError(Exception exception)
            {
                Action<Exception> handler = TransportError;
                if (handler != null)
                    handler(exception);
            }
        }

        private struct PacketDescription
        {
            public bool IsApplication;
            public bool IsAckOnly;
            public NetworkChannel Channel;

            public static PacketDescription Read(byte[] bytes, int count)
            {
                PacketDescription description = new PacketDescription();
                if (bytes == null || count < NetworkDefaults.HeaderSize)
                    return description;

                try
                {
                    MessageBatchReader reader = new MessageBatchReader(bytes, 0, count);
                    description.IsAckOnly = (reader.Header.Flags & PacketFlags.IsAckOnly) != 0;
                    NetworkMessage message;
                    if (reader.TryReadNext(out message))
                    {
                        description.IsApplication = !SessionMessageTypes.IsReserved(message.MessageType);
                        description.Channel = message.Channel;
                    }
                }
                catch
                {
                }

                return description;
            }
        }
    }
}
