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
            tests.Add(new TestCase("Reliable ACK-only packets clear queue before heartbeat", ReliableAckOnlyPacketsClearQueueBeforeHeartbeat));
            tests.Add(new TestCase("Reliable packets resend after packet loss and clear after ACK", ReliablePacketsResendAndClearAfterAck));
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
            public bool DuplicateNextClientToHostReliableApplication;
            public int ClientToHostReliableApplicationSends;
            public int ClientToHostUnreliableApplicationSends;
            public int ClientToHostReliableApplicationDrops;
            public int ClientToHostUnreliableApplicationDrops;
            public int ClientToHostReliableApplicationDuplicates;
            public int HostToClientAckOnlySends;

            public static PairedMemoryTransportPair Create()
            {
                PairedMemoryTransportPair pair = new PairedMemoryTransportPair();
                pair.Host = new PairedMemoryTransport(pair, true);
                pair.Client = new PairedMemoryTransport(pair, false);
                pair.Host.Partner = pair.Client;
                pair.Client.Partner = pair.Host;
                return pair;
            }

            public bool ShouldDrop(PairedMemoryTransport sender, PacketDescription packet)
            {
                if (sender.IsHost || !packet.IsApplication)
                {
                    if (sender.IsHost && packet.IsAckOnly)
                        HostToClientAckOnlySends++;
                    return false;
                }

                if (packet.Channel == NetworkChannel.Reliable)
                {
                    ClientToHostReliableApplicationSends++;
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

            private void Receive(IPEndPoint remoteEndPoint, byte[] bytes)
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
