using System.Collections.Generic;
using System.Reflection;
using ModAPI.Networking;
using ShelteredAPI.Networking.Persistence;
using ShelteredAPI.Networking.Recovery;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerCatchupTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("CatchupPolicy_ChoosesEventOnlyForRecentClient", ChoosesEventOnlyForRecentClient));
            tests.Add(new TestCase("CatchupPolicy_RequiresSnapshotWhenHistoryTrimmed", RequiresSnapshotWhenHistoryTrimmed));
            tests.Add(new TestCase("CatchupService_AppliesEventOnlyPackageAndResumesHostTick", AppliesEventOnlyPackageAndResumesHostTick));
        }

        private static void ChoosesEventOnlyForRecentClient()
        {
            ShelteredMultiplayerCatchupDecision decision = new ShelteredMultiplayerResyncPolicy().Choose(
                new ShelteredMultiplayerCatchupRequest { SessionId = "s", LastAppliedTick = 10 },
                CreateContext("s", 12),
                new ShelteredWorldEventJournal(),
                string.Empty);

            TestAssert.Equal(ShelteredMultiplayerCatchupDecisionKind.EventOnly, decision.Kind, "Recent reconnect should use event-only catchup.");
        }

        private static void RequiresSnapshotWhenHistoryTrimmed()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal(2);
            journal.Append(new ShelteredWorldEventRecord { EventId = "event-1", EventKind = "a", WorldTick = 1 });
            journal.Append(new ShelteredWorldEventRecord { EventId = "event-2", EventKind = "a", WorldTick = 2 });
            journal.Append(new ShelteredWorldEventRecord { EventId = "event-3", EventKind = "a", WorldTick = 3 });

            ShelteredMultiplayerCatchupDecision decision = new ShelteredMultiplayerResyncPolicy().Choose(
                new ShelteredMultiplayerCatchupRequest { SessionId = "s", LastAppliedTick = 1, LastAppliedEventId = "event-1" },
                CreateContext("s", 3),
                journal,
                string.Empty);

            TestAssert.Equal(ShelteredMultiplayerCatchupDecisionKind.SnapshotAndEvents, decision.Kind, "Trimmed history should require snapshot catchup.");
        }

        private static void AppliesEventOnlyPackageAndResumesHostTick()
        {
            FieldInfo contextField = typeof(ShelteredMultiplayerSessionCoordinator).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
            ShelteredMultiplayerSessionContext previous = (ShelteredMultiplayerSessionContext)contextField.GetValue(ShelteredMultiplayerSessionCoordinator.Instance);

            try
            {
                contextField.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, CreateContext("s", 5));
                ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();
                ShelteredMultiplayerWorldPersistence persistence = new ShelteredMultiplayerWorldPersistence(
                    ShelteredMultiplayerSessionCoordinator.Instance,
                    new ShelteredMapEntityRegistry(delegate { return 0; }),
                    journal,
                    new ShelteredTravelStateRegistry());
                ShelteredMultiplayerCatchupService service = new ShelteredMultiplayerCatchupService(
                    ShelteredMultiplayerSessionCoordinator.Instance,
                    persistence,
                    journal,
                    new ShelteredMultiplayerResyncPolicy(),
                    null);

                ShelteredMultiplayerCatchupPackage package = new ShelteredMultiplayerCatchupPackage();
                package.Decision = new ShelteredMultiplayerCatchupDecision { Kind = ShelteredMultiplayerCatchupDecisionKind.EventOnly };
                package.HostTick = 7;
                package.Events.Add(new ShelteredWorldEventRecord { EventId = "event-1", EventKind = "a", WorldTick = 6 });

                ShelteredMultiplayerCatchupApplyResult result = service.ApplyClientPackage(package);

                TestAssert.True(result.Success, "Event-only package should apply.");
                TestAssert.Equal(1, journal.Count, "Event replay should append missing events.");
                TestAssert.Equal((long)7, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldTick, "Client should resume at host tick.");
            }
            finally
            {
                contextField.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, previous);
            }
        }

        private static ShelteredMultiplayerSessionContext CreateContext(string sessionId, long tick)
        {
            return new ShelteredMultiplayerSessionContext(
                ShelteredMultiplayerSessionMode.Host,
                sessionId,
                1,
                NetworkDefaults.HostPeerId,
                "host",
                20,
                tick,
                0.05f,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                ShelteredMultiplayerSetupPhase.Released,
                new ShelteredMultiplayerPeerInfo[0],
                new ShelteredMultiplayerBunkerAssignmentRecord[0],
                ShelteredMultiplayerSetupSettings.Empty,
                "test");
        }
    }
}
