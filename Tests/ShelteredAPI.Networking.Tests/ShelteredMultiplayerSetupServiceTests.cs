using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Networking;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Events;
using ShelteredAPI.Harmony;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerSetupServiceTests
    {
        private const string ApplicationId = "ShelteredAPI.Networking.SetupTests";
        private const string SessionId = "setup-tests";

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Setup_HostCannotReleaseBeforeLocalWorldLoaded", HostCannotReleaseBeforeLocalWorldLoaded));
            tests.Add(new TestCase("Setup_HostCannotReleaseBeforeExpectedClientsLoaded", HostCannotReleaseBeforeExpectedClientsLoaded));
            tests.Add(new TestCase("Setup_ClientIgnoresStaleBeginSetupSession", ClientIgnoresStaleBeginSetupSession));
            tests.Add(new TestCase("Setup_DisconnectedExpectedPeerKeepsStartBlocked", DisconnectedExpectedPeerKeepsStartBlocked));
            tests.Add(new TestCase("Setup_ReconnectRecoversExpectedPeerGate", ReconnectRecoversExpectedPeerGate));
            tests.Add(new TestCase("Setup_CoordinatorRaisesPhasesInOrder", CoordinatorRaisesPhasesInOrder));
        }

        private static void HostCannotReleaseBeforeLocalWorldLoaded()
        {
            ResetState();

            NetworkSession host = null;
            NetworkSession client = null;
            ShelteredMultiplayerSetupService setup = null;
            try
            {
                CreateConnectedPair("host-release-local", out host, out client);
                setup = CreateHostSetup(host);

                setup.BeginHostSetup(1);
                setup.ReleaseStartFromHost();

                TestAssert.False(setup.CanHostReleaseStart, "Host release should stay blocked before local world load.");
                AssertContains(setup.Status, "host is not loaded", "Status should explain that the host has not loaded.");
                TestAssert.True(ShelteredMultiplayerHookService.Instance.IsWorldStartBlocked,
                    "Setup preparation should block GameTime until release.");
            }
            finally
            {
                DisposeSetup(setup);
                DisposeSession(client);
                DisposeSession(host);
                ResetState();
            }
        }

        private static void HostCannotReleaseBeforeExpectedClientsLoaded()
        {
            ResetState();

            NetworkSession host = null;
            NetworkSession client = null;
            ShelteredMultiplayerSetupService setup = null;
            try
            {
                CreateConnectedPair("host-release-client", out host, out client);
                setup = CreateHostSetup(host);

                setup.BeginHostSetup(1);
                GameEvents.TryRaiseSessionStarted();
                setup.ReleaseStartFromHost();

                TestAssert.False(setup.CanHostReleaseStart, "Host release should stay blocked until expected clients load.");
                AssertContains(setup.Status, "waiting for 1 peer", "Status should report the expected unloaded peer count.");
                TestAssert.True(ShelteredMultiplayerHookService.Instance.IsWorldStartBlocked,
                    "World start should remain blocked while clients are still loading.");
            }
            finally
            {
                DisposeSetup(setup);
                DisposeSession(client);
                DisposeSession(host);
                ResetState();
            }
        }

        private static void ClientIgnoresStaleBeginSetupSession()
        {
            ResetState();

            NetworkSession host = null;
            NetworkSession client = null;
            ShelteredMultiplayerSetupService setup = null;
            try
            {
                CreateConnectedPair("client-stale", out host, out client);
                setup = new ShelteredMultiplayerSetupService(client, NullLog);

                setup.TryHandleMessage(
                    FirstPeer(client),
                    ShelteredMultiplayerSetupService.BeginSetupMessageType,
                    CreateBeginSetupPayload("stale-session", 1, 0, 2));

                TestAssert.Equal("stale setup ignored", setup.Status, "Client should expose stale setup ignores through status.");
                AssertContains(setup.LastError, "stale-session", "Client should expose the ignored stale session id.");
                TestAssert.Equal(ShelteredMultiplayerSetupPhase.Inactive,
                    ShelteredMultiplayerSessionCoordinator.Instance.Context.SetupPhase,
                    "A stale setup message must not activate the Sheltered coordinator.");
            }
            finally
            {
                DisposeSetup(setup);
                DisposeSession(client);
                DisposeSession(host);
                ResetState();
            }
        }

        private static void DisconnectedExpectedPeerKeepsStartBlocked()
        {
            ResetState();

            NetworkSession host = null;
            NetworkSession client = null;
            ShelteredMultiplayerSetupService setup = null;
            try
            {
                CreateConnectedPair("disconnect-block", out host, out client);
                setup = CreateHostSetup(host);

                setup.BeginHostSetup(1);
                GameEvents.TryRaiseSessionStarted();

                byte peerId = FirstPeer(host).PeerId;
                host.DisconnectPeer(peerId, NetworkDisconnectReason.LocalShutdown, "setup test disconnect");

                TestAssert.False(setup.CanHostReleaseStart, "Disconnecting an expected peer should keep release blocked.");
                AssertContains(setup.Status, "waiting for 1 peer", "Status should keep reporting the missing expected peer.");
                TestAssert.True(ShelteredMultiplayerHookService.Instance.IsWorldStartBlocked,
                    "World start should remain blocked when an expected peer disconnects mid-setup.");
                TestAssert.False(GetAssignmentOnline(peerId), "Coordinator bunker assignment should retain the peer as offline.");
            }
            finally
            {
                DisposeSetup(setup);
                DisposeSession(client);
                DisposeSession(host);
                ResetState();
            }
        }

        private static void ReconnectRecoversExpectedPeerGate()
        {
            ResetState();

            NetworkSession host = null;
            NetworkSession client = null;
            NetworkSession reconnected = null;
            ShelteredMultiplayerSetupService setup = null;
            try
            {
                CreateConnectedPair("reconnect", out host, out client);
                setup = CreateHostSetup(host);

                setup.BeginHostSetup(1);
                GameEvents.TryRaiseSessionStarted();

                byte originalPeerId = FirstPeer(host).PeerId;
                host.DisconnectPeer(originalPeerId, NetworkDisconnectReason.LocalShutdown, "setup test reconnect");
                DisposeSession(client);
                client = null;

                reconnected = CreateClient("reconnect");
                reconnected.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), CreateOptions("Client", "client-reconnect"));
                NetworkTestHarness.PumpUntil(host, reconnected, delegate
                {
                    return reconnected.State == NetworkSessionState.Connected && host.GetPeers().Length == 1;
                }, "Client did not reconnect to the active setup host.");

                NetworkPeer peer = FirstPeer(host);
                TestAssert.Equal(originalPeerId, peer.PeerId, "Reconnect should recover the previous peer id when stable identity matches.");
                TestAssert.False(setup.CanHostReleaseStart, "Reconnect alone should not release the gate before setup-loaded arrives.");

                setup.TryHandleMessage(
                    peer,
                    ShelteredMultiplayerSetupService.SetupLoadedMessageType,
                    CreateSetupLoadedPayload(SessionId, 1));

                TestAssert.True(setup.CanHostReleaseStart, "Expected peer should satisfy the release gate after reconnect and setup-loaded.");
                setup.ReleaseStartFromHost();
                TestAssert.Equal("released", setup.Status, "Release should become visible once all players are loaded.");
                TestAssert.False(ShelteredMultiplayerHookService.Instance.IsWorldStartBlocked,
                    "World start should unblock after host release.");
            }
            finally
            {
                DisposeSetup(setup);
                DisposeSession(reconnected);
                DisposeSession(client);
                DisposeSession(host);
                ResetState();
            }
        }

        private static void CoordinatorRaisesPhasesInOrder()
        {
            ResetState();

            RecordingLifecycleHandler recorder = new RecordingLifecycleHandler();
            ShelteredMultiplayerSessionCoordinator.Instance.Register(recorder, false);

            NetworkSession host = null;
            NetworkSession client = null;
            ShelteredMultiplayerSetupService setup = null;
            try
            {
                CreateConnectedPair("phase-order", out host, out client);
                setup = CreateHostSetup(host);

                recorder.Events.Clear();
                setup.BeginHostSetup(1);
                GameEvents.TryRaiseSessionStarted();
                setup.TryHandleMessage(
                    FirstPeer(host),
                    ShelteredMultiplayerSetupService.SetupLoadedMessageType,
                    CreateSetupLoadedPayload(SessionId, 1));
                setup.ReleaseStartFromHost();

                AssertPhaseOrder(
                    recorder.Events,
                    new ShelteredMultiplayerLifecycleEventKind[]
                    {
                        ShelteredMultiplayerLifecycleEventKind.SessionActivated,
                        ShelteredMultiplayerLifecycleEventKind.RosterChanged,
                        ShelteredMultiplayerLifecycleEventKind.SetupPreparing,
                        ShelteredMultiplayerLifecycleEventKind.LocalWorldLoaded,
                        ShelteredMultiplayerLifecycleEventKind.WorldStartReleased
                    });
            }
            finally
            {
                recorder.Active = false;
                DisposeSetup(setup);
                DisposeSession(client);
                DisposeSession(host);
                ResetState();
            }
        }

        private static ShelteredMultiplayerSetupService CreateHostSetup(NetworkSession host)
        {
            ShelteredMultiplayerSetupService setup = new ShelteredMultiplayerSetupService(host, NullLog);
            host.PeerConnected += delegate(object sender, NetworkPeerEventArgs e)
            {
                setup.HandlePeerConnected(e != null ? e.Peer : null);
            };
            host.PeerDisconnected += delegate(object sender, NetworkPeerDisconnectedEventArgs e)
            {
                setup.HandlePeerDisconnected(e != null ? e.Peer : null);
            };
            return setup;
        }

        private static void CreateConnectedPair(string suffix, out NetworkSession host, out NetworkSession client)
        {
            NetworkSession createdHost = CreateHost();
            NetworkSession createdClient = CreateClient(suffix);
            createdHost.StartHost(CreateOptions("Host", "host-" + suffix));
            createdClient.Join(new IPEndPoint(IPAddress.Loopback, createdHost.LocalEndPoint.Port), CreateOptions("Client", "client-" + suffix));
            NetworkTestHarness.PumpUntil(createdHost, createdClient, delegate
            {
                return createdHost.GetPeers().Length == 1 && createdClient.State == NetworkSessionState.Connected;
            }, "Client and host did not complete setup-test handshake.");

            host = createdHost;
            client = createdClient;
        }

        private static NetworkSession CreateHost()
        {
            return new NetworkSession(NetworkTestHarness.CreateLoopbackConfig());
        }

        private static NetworkSession CreateClient(string suffix)
        {
            return new NetworkSession(NetworkTestHarness.CreateLoopbackConfig());
        }

        private static NetworkSessionOptions CreateOptions(string role, string stablePeerId)
        {
            NetworkSessionOptions options = NetworkTestHarness.CreateOptions(ApplicationId);
            options.SessionId = SessionId;
            options.DisplayName = role;
            options.StablePeerId = stablePeerId;
            options.ReconnectToken = stablePeerId;
            options.MaxPeers = 4;
            return options;
        }

        private static NetworkPeer FirstPeer(NetworkSession session)
        {
            NetworkPeer[] peers = session != null ? session.GetPeers() : new NetworkPeer[0];
            if (peers.Length != 1 || peers[0] == null)
                throw new InvalidOperationException("Expected exactly one connected peer.");

            return peers[0];
        }

        private static byte[] CreateBeginSetupPayload(string sessionId, int hostSlot, int clientSlot, int playerId)
        {
            byte[] buffer = new byte[256];
            BitWriter writer = new BitWriter(buffer);
            writer.WriteString(sessionId ?? string.Empty);
            writer.WriteInt32(hostSlot);
            writer.WriteInt32(clientSlot);
            writer.WriteInt32(playerId);
            writer.WriteInt32(1);
            writer.WriteInt32(1);
            writer.WriteInt32(1);
            writer.WriteInt32(1);
            writer.WriteInt32(1);
            writer.WriteInt32(0);
            writer.WriteBool(false);
            writer.WriteInt32(0);
            return TrimPayload(buffer, writer.Position);
        }

        private static byte[] CreateSetupLoadedPayload(string sessionId, int absoluteSlot)
        {
            byte[] buffer = new byte[256];
            BitWriter writer = new BitWriter(buffer);
            writer.WriteString(sessionId ?? string.Empty);
            writer.WriteInt32(absoluteSlot);
            return TrimPayload(buffer, writer.Position);
        }

        private static byte[] TrimPayload(byte[] buffer, int length)
        {
            byte[] payload = new byte[length];
            if (length > 0)
                Buffer.BlockCopy(buffer, 0, payload, 0, length);
            return payload;
        }

        private static bool GetAssignmentOnline(byte peerId)
        {
            ShelteredMultiplayerBunkerAssignmentRecord[] assignments =
                ShelteredMultiplayerSessionCoordinator.Instance.Context.BunkerAssignments;
            for (int i = 0; i < assignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord assignment = assignments[i];
                if (assignment != null && assignment.NetworkPeerId == peerId)
                    return assignment.IsOnline;
            }

            throw new InvalidOperationException("Expected a bunker assignment for peer " + peerId + ".");
        }

        private static void AssertPhaseOrder(
            List<ShelteredMultiplayerLifecycleEventKind> actual,
            ShelteredMultiplayerLifecycleEventKind[] expected)
        {
            int cursor = 0;
            for (int i = 0; i < actual.Count && cursor < expected.Length; i++)
            {
                if (actual[i] == expected[cursor])
                    cursor++;
            }

            if (cursor != expected.Length)
                throw new InvalidOperationException("Coordinator phases were not raised in the expected order. Actual: "
                    + FormatPhases(actual));
        }

        private static string FormatPhases(List<ShelteredMultiplayerLifecycleEventKind> phases)
        {
            string[] values = new string[phases.Count];
            for (int i = 0; i < phases.Count; i++)
                values[i] = phases[i].ToString();
            return string.Join(",", values);
        }

        private static void AssertContains(string value, string expected, string message)
        {
            if ((value ?? string.Empty).IndexOf(expected, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(message + " Expected to find '" + expected + "' in '" + value + "'.");
        }

        private static void ResetState()
        {
            AutoLoadFlow.Reset();
            ShelteredMultiplayerSessionCoordinator.Instance.Deactivate("setup-test-reset");
        }

        private static void DisposeSetup(ShelteredMultiplayerSetupService setup)
        {
            if (setup != null)
                setup.Dispose();
        }

        private static void DisposeSession(NetworkSession session)
        {
            if (session != null)
                session.Dispose();
        }

        private static void NullLog(MMLog.LogLevel level, string component, string message)
        {
        }

        private sealed class RecordingLifecycleHandler : IShelteredMultiplayerSessionLifecycleHandler
        {
            public readonly List<ShelteredMultiplayerLifecycleEventKind> Events =
                new List<ShelteredMultiplayerLifecycleEventKind>();

            public bool Active = true;

            public void Handle(ShelteredMultiplayerLifecycleEvent lifecycleEvent)
            {
                if (!Active || lifecycleEvent == null)
                    return;

                Events.Add(lifecycleEvent.Kind);
            }
        }
    }
}
