using System;
using System.Collections.Generic;
using System.Net;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class SaveResumeHookTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Host-authored session id is adopted by clients", HostAuthoredSessionIdIsAdoptedByClients));
            tests.Add(new TestCase("Reconnect tokens and stable peer ids propagate", ReconnectTokensPropagate));
            tests.Add(new TestCase("Session nonce is host-authored and validated", SessionNonceIsHostAuthoredAndValidated));
            tests.Add(new TestCase("Transport disconnect and reconnect hooks fire", TransportLifecycleHooksFire));
        }

        private static void HostAuthoredSessionIdIsAdoptedByClients()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());

                NetworkSessionOptions hostOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                hostOptions.SessionId = string.Empty;
                NetworkSessionOptions clientOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                clientOptions.SessionId = string.Empty;

                host.StartHost(hostOptions);
                TestAssert.True(host.SessionId.Length > 0, "Host should create a non-empty session id.");

                client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), clientOptions);
                NetworkTestUtilities.PumpUntil(host, client, delegate
                {
                    return client.State == NetworkSessionState.Connected && host.GetPeers().Length == 1;
                }, "Client did not connect to host-authored session id.");

                TestAssert.Equal(host.SessionId, client.SessionId, "Client should adopt host session id when it joined without one.");
                TestAssert.Equal(host.SessionId, client.GetPeers()[0].SessionId, "Client should store the host session id on the host peer.");
                TestAssert.Equal(host.SessionId, host.GetPeers()[0].SessionId, "Host should store its session id on connected peers.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }

            NetworkSessionOptions mismatchHostOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
            mismatchHostOptions.SessionId = "expected-session";
            NetworkSessionOptions mismatchClientOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
            mismatchClientOptions.SessionId = "wrong-session";
            RunRejectedJoin(mismatchHostOptions, mismatchClientOptions, HandshakeRejectReason.SessionMismatch);
        }

        private static void ReconnectTokensPropagate()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());

                NetworkSessionOptions hostOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                hostOptions.StablePeerId = "host-stable";
                hostOptions.ReconnectToken = "host-token";
                NetworkSessionOptions clientOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                clientOptions.StablePeerId = "client-stable";
                clientOptions.ReconnectToken = "client-token";

                host.StartHost(hostOptions);
                client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), clientOptions);

                NetworkTestUtilities.PumpUntil(host, client, delegate
                {
                    return client.State == NetworkSessionState.Connected && host.GetPeers().Length == 1;
                }, "Client and host did not connect.");

                NetworkPeer hostSidePeer = host.GetPeers()[0];
                NetworkPeer clientHostPeer = client.GetPeers()[0];
                TestAssert.Equal("client-stable", hostSidePeer.StablePeerId, "Host should retain client stable peer id.");
                TestAssert.Equal("client-token", hostSidePeer.ReconnectToken, "Host should retain client reconnect token.");
                TestAssert.Equal("host-stable", clientHostPeer.StablePeerId, "Client should retain host stable peer id.");
                TestAssert.Equal("host-token", clientHostPeer.ReconnectToken, "Client should retain host reconnect token.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }
        }

        private static void SessionNonceIsHostAuthoredAndValidated()
        {
            NetworkSession host = null;
            NetworkSession client = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());

                NetworkSessionOptions hostOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                hostOptions.SessionId = "public-session";
                host.StartHost(hostOptions);
                TestAssert.True(host.SessionNonce.Length > 0, "Host should create a runtime session nonce.");
                TestAssert.True(!string.Equals(host.SessionId, host.SessionNonce, StringComparison.Ordinal),
                    "Runtime nonce should be distinct from the user-facing session id.");

                NetworkSessionOptions clientOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                clientOptions.SessionId = "public-session";
                client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), clientOptions);

                NetworkTestUtilities.PumpUntil(host, client, delegate
                {
                    return client.State == NetworkSessionState.Connected;
                }, "Client did not connect to host-authored nonce session.");
                TestAssert.Equal(host.SessionNonce, client.SessionNonce, "Client should adopt the host-authored session nonce.");
            }
            finally
            {
                if (client != null)
                    client.Dispose();
                if (host != null)
                    host.Dispose();
            }

            NetworkSessionOptions mismatchHostOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
            mismatchHostOptions.SessionNonce = "expected-nonce";
            NetworkSessionOptions mismatchClientOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
            mismatchClientOptions.SessionNonce = "wrong-nonce";
            RunRejectedJoin(mismatchHostOptions, mismatchClientOptions, HandshakeRejectReason.SessionMismatch);
        }

        private static void TransportLifecycleHooksFire()
        {
            NetworkSession host = null;
            NetworkSession firstClient = null;
            NetworkSession secondClient = null;
            try
            {
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                firstClient = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());

                NetworkSessionOptions hostOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                hostOptions.SessionNonce = "resume-session";
                NetworkSessionOptions clientOptions = NetworkTestUtilities.CreateOptions("ModAPI.Networking.Tests");
                clientOptions.SessionNonce = "resume-session";
                clientOptions.StablePeerId = "stable-client";
                clientOptions.ReconnectToken = "resume-token";

                int hostTransportDisconnected = 0;
                int hostTransportReconnected = 0;
                byte firstPeerId = NetworkDefaults.UnassignedPeerId;
                byte reconnectedPreviousPeerId = NetworkDefaults.UnassignedPeerId;

                host.TransportDisconnected += delegate { hostTransportDisconnected++; };
                host.TransportReconnected += delegate(object sender, NetworkTransportReconnectEventArgs e)
                {
                    hostTransportReconnected++;
                    reconnectedPreviousPeerId = e.PreviousPeerId;
                };

                host.StartHost(hostOptions);
                firstClient.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), clientOptions);
                NetworkTestUtilities.PumpUntil(host, firstClient, delegate
                {
                    return firstClient.State == NetworkSessionState.Connected && host.GetPeers().Length == 1;
                }, "Initial client did not connect.");

                firstPeerId = host.GetPeers()[0].PeerId;
                host.DisconnectPeer(firstPeerId, NetworkDisconnectReason.RemoteClosed, "resume test");
                NetworkTestUtilities.PumpUntil(host, firstClient, delegate
                {
                    return hostTransportDisconnected == 1;
                }, "Host transport disconnect hook did not fire.");

                firstClient.Dispose();
                firstClient = null;

                secondClient = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                secondClient.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), clientOptions);
                NetworkTestUtilities.PumpUntil(host, secondClient, delegate
                {
                    return hostTransportReconnected == 1
                        && secondClient.State == NetworkSessionState.Connected
                        && host.GetPeers().Length == 1;
                }, "Host transport reconnect hook did not fire.");

                TestAssert.Equal(firstPeerId, reconnectedPreviousPeerId, "Reconnect hook should expose the previous peer id.");
                TestAssert.Equal(firstPeerId, host.GetPeers()[0].PeerId, "Host should reuse the resumed peer id when it is available.");
            }
            finally
            {
                if (secondClient != null)
                    secondClient.Dispose();
                if (firstClient != null)
                    firstClient.Dispose();
                if (host != null)
                    host.Dispose();
            }
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
                host = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());
                client = new NetworkSession(NetworkTestUtilities.CreateLoopbackConfig());

                HandshakeRejectReason failureReason = HandshakeRejectReason.None;
                client.ConnectionFailed += delegate(object sender, NetworkConnectionFailedEventArgs e)
                {
                    failureReason = e.Reason;
                };

                host.StartHost(hostOptions);
                client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), clientOptions);

                NetworkTestUtilities.PumpUntil(host, client, delegate { return failureReason != HandshakeRejectReason.None; },
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

    }
}
