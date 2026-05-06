using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class NetworkTestUtilities
    {
        public const int DefaultPumpTimeoutMilliseconds = 3000;

        public static NetworkConfig CreateLoopbackConfig()
        {
            NetworkConfig config = NetworkConfig.CreateDefault();
            config.Port = 0;
            config.ConnectionTimeoutMilliseconds = 500;
            config.HandshakeRetryMilliseconds = 50;
            config.HandshakeTimeoutMilliseconds = 1000;
            config.HeartbeatIntervalMilliseconds = 100;
            return config;
        }

        public static NetworkConfig CreateFastTimeoutConfig()
        {
            NetworkConfig config = CreateLoopbackConfig();
            config.ConnectionTimeoutMilliseconds = 150;
            config.HeartbeatIntervalMilliseconds = 1000;
            config.HandshakeTimeoutMilliseconds = 300;
            return config;
        }

        public static NetworkSessionOptions CreateOptions(string applicationId)
        {
            NetworkSessionOptions options = NetworkSessionOptions.CreateDefault();
            options.ApplicationId = applicationId;
            options.SessionId = "loopback";
            options.DisplayName = "test";
            options.MaxPeers = 4;
            return options;
        }

        public static void Connect(NetworkSession host, NetworkSession client, string applicationId)
        {
            int hostConnected = 0;
            int clientConnected = 0;
            host.PeerConnected += delegate { hostConnected++; };
            client.PeerConnected += delegate { clientConnected++; };

            host.StartHost(CreateOptions(applicationId));
            client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), CreateOptions(applicationId));

            PumpUntil(new NetworkSession[] { host, client }, delegate
            {
                return hostConnected == 1
                    && clientConnected == 1
                    && client.State == NetworkSessionState.Connected;
            }, "Client and host did not complete the localhost handshake.");
        }

        public static void PumpOnce(params NetworkSession[] sessions)
        {
            for (int i = 0; i < sessions.Length; i++)
            {
                if (sessions[i] != null)
                    sessions[i].Update();
            }
        }

        public static void PumpUntil(NetworkSession session, Func<bool> condition, string failureMessage)
        {
            PumpUntil(new NetworkSession[] { session }, condition, failureMessage, DefaultPumpTimeoutMilliseconds);
        }

        public static void PumpUntil(NetworkSession first, NetworkSession second, Func<bool> condition, string failureMessage)
        {
            PumpUntil(new NetworkSession[] { first, second }, condition, failureMessage, DefaultPumpTimeoutMilliseconds);
        }

        public static void PumpUntil(NetworkSession[] sessions, Func<bool> condition, string failureMessage)
        {
            PumpUntil(sessions, condition, failureMessage, DefaultPumpTimeoutMilliseconds);
        }

        public static void PumpUntil(NetworkSession[] sessions, Func<bool> condition, string failureMessage, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                PumpOnce(sessions);
                if (condition())
                    return;
                Thread.Sleep(10);
            }

            PumpOnce(sessions);
            if (!condition())
                throw new InvalidOperationException(failureMessage);
        }

        public static byte[] CopyPayload(NetworkMessageReceivedEventArgs e, ushort expectedMessageType)
        {
            if (e.MessageType != expectedMessageType)
                return null;

            byte[] copy = new byte[e.Payload.Length];
            if (copy.Length > 0)
                Buffer.BlockCopy(e.Payload, 0, copy, 0, copy.Length);
            return copy;
        }

        public static byte[] ReceiveUdp(UdpClient udp, int timeoutMilliseconds, string failureMessage)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (udp.Client.Available > 0)
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    return udp.Receive(ref remote);
                }

                Thread.Sleep(10);
            }

            throw new InvalidOperationException(failureMessage);
        }
    }

    internal sealed class TestSessionSet : IDisposable
    {
        private readonly NetworkSession[] _sessions;

        public TestSessionSet(params NetworkSession[] sessions)
        {
            _sessions = sessions;
        }

        public NetworkSession[] Sessions
        {
            get { return _sessions; }
        }

        public void Dispose()
        {
            for (int i = _sessions.Length - 1; i >= 0; i--)
            {
                if (_sessions[i] != null)
                    _sessions[i].Dispose();
            }
        }
    }
}
