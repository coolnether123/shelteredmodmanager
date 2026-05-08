using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Networking;
using ShelteredAPI.Networking.Persistence;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerWorldPersistenceTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("WorldPersistence_RoundTripsSnapshotXml", RoundTripsSnapshotXml));
            tests.Add(new TestCase("WorldPersistence_RejectsMalformedSnapshot", RejectsMalformedSnapshot));
            tests.Add(new TestCase("WorldPersistence_CapturesAndAppliesMapEntitiesAndEvents", CapturesAndAppliesMapEntitiesAndEvents));
        }

        private static void RoundTripsSnapshotXml()
        {
            ShelteredMultiplayerWorldSnapshot snapshot = new ShelteredMultiplayerWorldSnapshot();
            snapshot.SessionId = "session-a";
            snapshot.MasterSeed = 123;
            snapshot.WorldTick = 45;
            snapshot.CompatibilityHash = "hash-a";
            snapshot.MapKnowledge.Add(new ShelteredMultiplayerSnapshotKeyValue("p1", "known"));
            snapshot.RetainedEvents.Add(new ShelteredMultiplayerSnapshotWorldEvent
            {
                EventId = "event-1",
                EventKind = "travel.started",
                WorldTick = 44,
                PayloadJson = "{}",
                Authoritative = true
            });

            ShelteredMultiplayerWorldSnapshot parsed;
            string error;

            TestAssert.True(ShelteredMultiplayerWorldSnapshot.TryFromXml(snapshot.ToXml(), out parsed, out error), "Snapshot XML should parse.");
            TestAssert.Equal("session-a", parsed.SessionId, "Session id should round-trip.");
            TestAssert.Equal((long)45, parsed.WorldTick, "World tick should round-trip.");
            TestAssert.Equal(1, parsed.MapKnowledge.Count, "Optional map knowledge section should round-trip.");
            TestAssert.Equal(1, parsed.RetainedEvents.Count, "Retained events should round-trip.");
        }

        private static void RejectsMalformedSnapshot()
        {
            ShelteredMultiplayerWorldSnapshot parsed;
            string error;

            TestAssert.False(ShelteredMultiplayerWorldSnapshot.TryFromXml("<bad", out parsed, out error), "Malformed XML should be rejected.");
            TestAssert.True(!string.IsNullOrEmpty(error), "Malformed snapshot should explain the failure.");
        }

        private static void CapturesAndAppliesMapEntitiesAndEvents()
        {
            FieldInfo contextField = typeof(ShelteredMultiplayerSessionCoordinator).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
            ShelteredMultiplayerSessionContext previous = (ShelteredMultiplayerSessionContext)contextField.GetValue(ShelteredMultiplayerSessionCoordinator.Instance);

            try
            {
                contextField.SetValue(
                    ShelteredMultiplayerSessionCoordinator.Instance,
                    new ShelteredMultiplayerSessionContext(
                        ShelteredMultiplayerSessionMode.Host,
                        "persist-session",
                        1,
                        NetworkDefaults.HostPeerId,
                        "host",
                        20,
                        99,
                        0.05f,
                        ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                        ShelteredMultiplayerSetupPhase.Released,
                        new ShelteredMultiplayerPeerInfo[0],
                        new ShelteredMultiplayerBunkerAssignmentRecord[0],
                        ShelteredMultiplayerSetupSettings.Empty,
                        "test"));

                ModRandom.Initialize(1234);
                ShelteredMapEntityRegistry map = new ShelteredMapEntityRegistry(delegate { return 99; });
                ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();
                ShelteredTravelStateRegistry travel = new ShelteredTravelStateRegistry(map);
                ShelteredMultiplayerWorldPersistence persistence =
                    new ShelteredMultiplayerWorldPersistence(ShelteredMultiplayerSessionCoordinator.Instance, map, journal, travel);

                map.Upsert(new ShelteredMapEntity { EntityId = "entity-1", Kind = ShelteredMapEntityKind.ResourceNode, IsVisible = true });
                journal.Append(new ShelteredWorldEventRecord { EventId = "event-1", EventKind = "resource.claimed", WorldTick = 98, Authoritative = true });

                ShelteredMultiplayerWorldSnapshot snapshot = persistence.Capture("test");
                map.Clear("test");
                journal.Clear("test");

                string error;
                TestAssert.True(persistence.Apply(snapshot, "test", out error), "Snapshot apply should succeed.");
                TestAssert.Equal(1, map.GetAll().Count, "Map entity should be restored.");
                TestAssert.Equal(1, journal.Count, "Retained event should be restored.");
            }
            finally
            {
                contextField.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, previous);
            }
        }
    }
}
