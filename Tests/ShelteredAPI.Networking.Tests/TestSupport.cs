using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using ModAPI.Networking;
using ModAPI.Networking.Sessions;

namespace ShelteredAPI.Networking.Tests
{
    internal sealed class TestCase
    {
        public string Name;
        public Action Body;

        public TestCase(string name, Action body)
        {
            Name = name;
            Body = body;
        }
    }

    internal static class TestRunner
    {
        public static int Run(string label, List<TestCase> tests)
        {
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
            Console.WriteLine(label + ": " + (tests.Count - failed) + " passed, " + failed + " failed.");
            return failed == 0 ? 0 : 1;
        }
    }

    internal static class TestAssert
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
        }
    }

    internal static class NetworkTestHarness
    {
        private const int DefaultPumpTimeoutMilliseconds = 3000;

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

        public static NetworkSessionOptions CreateOptions(string applicationId)
        {
            NetworkSessionOptions options = NetworkSessionOptions.CreateDefault();
            options.ApplicationId = applicationId;
            options.SessionId = "sheltered-loopback";
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

        public static void PumpUntil(NetworkSession first, NetworkSession second, Func<bool> condition, string failureMessage)
        {
            PumpUntil(new NetworkSession[] { first, second }, condition, failureMessage);
        }

        private static void PumpUntil(NetworkSession[] sessions, Func<bool> condition, string failureMessage)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(DefaultPumpTimeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                for (int i = 0; i < sessions.Length; i++)
                {
                    if (sessions[i] != null)
                        sessions[i].Update();
                }

                if (condition())
                    return;

                Thread.Sleep(10);
            }

            for (int i = 0; i < sessions.Length; i++)
            {
                if (sessions[i] != null)
                    sessions[i].Update();
            }

            if (!condition())
                throw new InvalidOperationException(failureMessage);
        }
    }
}
