using System.Collections.Generic;
using System.Reflection;
using ModAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredWorldEventJournalTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("WorldEventJournal_GeneratesEventIdWhenMissing", GeneratesEventIdWhenMissing));
            tests.Add(new TestCase("WorldEventJournal_DeduplicatesByEventId", DeduplicatesByEventId));
            tests.Add(new TestCase("WorldEventJournal_PreservesAppendOrder", PreservesAppendOrder));
            tests.Add(new TestCase("WorldEventJournal_TickQueriesAreInclusive", TickQueriesAreInclusive));
            tests.Add(new TestCase("WorldEvents_FacadeUsesCoordinatorWorldTick", FacadeUsesCoordinatorWorldTick));
            tests.Add(new TestCase("WorldEvents_FacadeDeduplicatesByCorrelation", FacadeDeduplicatesByCorrelation));
            tests.Add(new TestCase("WorldEventJournal_ClearRemovesRecords", ClearRemovesRecords));
            tests.Add(new TestCase("WorldEventJournal_TrimsOldestRecords", TrimsOldestRecords));
            tests.Add(new TestCase("WorldEventReplayCursor_AdvancesAfterApply", ReplayCursorAdvancesAfterApply));
        }

        private static void GeneratesEventIdWhenMissing()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();

            ShelteredWorldEventAppendResult result = journal.Append(CreateRecord(string.Empty, "trade.offer", 3));

            TestAssert.True(result.Success, "Append should succeed for a valid event without an id.");
            TestAssert.True(result.EventId.StartsWith("worldevent."), "Missing event ids should use the worldevent prefix.");
            TestAssert.True(journal.Contains(result.EventId), "Generated event id should be indexed.");
        }

        private static void DeduplicatesByEventId()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();

            ShelteredWorldEventAppendResult first = journal.Append(CreateRecord("event-1", "trade.offer", 1));
            ShelteredWorldEventAppendResult second = journal.Append(CreateRecord("event-1", "trade.offer", 2));

            TestAssert.True(first.Success, "First append should succeed.");
            TestAssert.True(!second.Success, "Duplicate event id should be rejected.");
            TestAssert.Equal(1, journal.Count, "Duplicate append should not change the journal count.");
            TestAssert.Equal((long)1, journal.GetById("event-1").WorldTick, "Duplicate append should preserve the first record.");
        }

        private static void PreservesAppendOrder()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();

            journal.Append(CreateRecord("event-late", "raid.started", 20));
            journal.Append(CreateRecord("event-early", "trade.offer", 5));
            journal.Append(CreateRecord("event-mid", "bunker.changed", 10));

            IList<ShelteredWorldEventRecord> records = journal.GetSince(0);

            TestAssert.Equal("event-late", records[0].EventId, "First appended record should remain first.");
            TestAssert.Equal("event-early", records[1].EventId, "Second appended record should remain second.");
            TestAssert.Equal("event-mid", records[2].EventId, "Third appended record should remain third.");
        }

        private static void TickQueriesAreInclusive()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();

            journal.Append(CreateRecord("event-1", "trade.offer", 1));
            journal.Append(CreateRecord("event-5", "trade.accepted", 5));
            journal.Append(CreateRecord("event-9", "raid.started", 9));

            IList<ShelteredWorldEventRecord> since = journal.GetSince(5);
            IList<ShelteredWorldEventRecord> range = journal.GetRange(1, 5);

            TestAssert.Equal(2, since.Count, "GetSince should include records at the requested tick and after.");
            TestAssert.Equal("event-5", since[0].EventId, "GetSince should include the boundary tick.");
            TestAssert.Equal(2, range.Count, "GetRange should include both start and end ticks.");
            TestAssert.Equal("event-1", range[0].EventId, "GetRange should include the start tick.");
            TestAssert.Equal("event-5", range[1].EventId, "GetRange should include the end tick.");
        }

        private static void FacadeUsesCoordinatorWorldTick()
        {
            FieldInfo contextField = typeof(ShelteredMultiplayerSessionCoordinator).GetField(
                "_context",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ShelteredMultiplayerSessionContext previous =
                (ShelteredMultiplayerSessionContext)contextField.GetValue(ShelteredMultiplayerSessionCoordinator.Instance);

            try
            {
                ShelteredWorldEvents.Clear("test-start");
                contextField.SetValue(
                    ShelteredMultiplayerSessionCoordinator.Instance,
                    new ShelteredMultiplayerSessionContext(
                        ShelteredMultiplayerSessionMode.Host,
                        "world-event-test-session",
                        2,
                        7,
                        "local-stable-peer",
                        20,
                        42,
                        0.25f,
                        ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                        ShelteredMultiplayerSetupPhase.Activated,
                        new ShelteredMultiplayerPeerInfo[0],
                        new ShelteredMultiplayerBunkerAssignmentRecord[0],
                        ShelteredMultiplayerSetupSettings.Empty,
                        "test"));

                ShelteredWorldEventAppendResult result = ShelteredWorldEvents.AppendAuthoritative(
                    "trade.accepted",
                    "corr-1",
                    "{\"ok\":true}",
                    0,
                    NetworkDefaults.UnassignedPeerId);

                ShelteredWorldEventRecord record = ShelteredWorldEvents.Journal.GetById(result.EventId);

                TestAssert.True(result.Success, "Facade append should succeed.");
                TestAssert.Equal((long)42, record.WorldTick, "Facade should stamp world tick from the coordinator context.");
                TestAssert.Near(0.25f, record.WorldDeltaSeconds, 0.0001f, "Facade should stamp delta seconds from the coordinator context.");
                TestAssert.Equal(2, record.SourcePlayerId, "Facade should default source player from the coordinator context.");
                TestAssert.Equal((byte)7, record.SourceNetworkPeerId, "Facade should default source peer from the coordinator context.");
                TestAssert.True(record.Authoritative, "Authoritative helper should mark authoritative records.");
            }
            finally
            {
                ShelteredWorldEvents.Clear("test-cleanup");
                contextField.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, previous);
            }
        }

        private static void FacadeDeduplicatesByCorrelation()
        {
            FieldInfo contextField = typeof(ShelteredMultiplayerSessionCoordinator).GetField(
                "_context",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ShelteredMultiplayerSessionContext previous =
                (ShelteredMultiplayerSessionContext)contextField.GetValue(ShelteredMultiplayerSessionCoordinator.Instance);

            try
            {
                ShelteredWorldEvents.Clear("test-start");
                contextField.SetValue(
                    ShelteredMultiplayerSessionCoordinator.Instance,
                    new ShelteredMultiplayerSessionContext(
                        ShelteredMultiplayerSessionMode.Host,
                        "world-event-test-session",
                        2,
                        7,
                        "local-stable-peer",
                        20,
                        42,
                        0.25f,
                        ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                        ShelteredMultiplayerSetupPhase.Activated,
                        new ShelteredMultiplayerPeerInfo[0],
                        new ShelteredMultiplayerBunkerAssignmentRecord[0],
                        ShelteredMultiplayerSetupSettings.Empty,
                        "test"));

                ShelteredWorldEventAppendResult first = ShelteredWorldEvents.AppendAuthoritative(
                    "trade.accepted",
                    "corr-duplicate",
                    "{\"ok\":true}",
                    0,
                    NetworkDefaults.UnassignedPeerId);
                ShelteredWorldEventAppendResult second = ShelteredWorldEvents.AppendAuthoritative(
                    "trade.accepted",
                    "corr-duplicate",
                    "{\"ok\":true}",
                    0,
                    NetworkDefaults.UnassignedPeerId);

                TestAssert.True(first.Success, "First correlated event should append.");
                TestAssert.False(second.Success, "Duplicate correlated event should be rejected.");
                TestAssert.Equal(1, ShelteredWorldEvents.Journal.Count, "Duplicate correlated events should not grow the journal.");
            }
            finally
            {
                ShelteredWorldEvents.Clear("test-cleanup");
                contextField.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, previous);
            }
        }

        private static void ClearRemovesRecords()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();

            journal.Append(CreateRecord("event-1", "trade.offer", 5));
            journal.Clear("test");

            TestAssert.Equal(0, journal.Count, "Clear should remove all records.");
            TestAssert.Equal((long)0, journal.LatestTick, "Clear should reset latest tick.");
            TestAssert.True(!journal.Contains("event-1"), "Clear should remove event id indexes.");
        }

        private static void TrimsOldestRecords()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal(2);

            journal.Append(CreateRecord("event-1", "trade.offer", 1));
            journal.Append(CreateRecord("event-2", "trade.accepted", 2));
            journal.Append(CreateRecord("event-3", "raid.started", 3));

            TestAssert.Equal(2, journal.Count, "Journal should retain only the configured maximum event count.");
            TestAssert.False(journal.Contains("event-1"), "Trim should remove the oldest id from the index.");
            TestAssert.True(journal.Contains("event-2"), "Trim should keep newer records.");
            TestAssert.True(journal.Contains("event-3"), "Trim should keep newest records.");
            TestAssert.Equal((long)3, journal.LatestTick, "LatestTick should survive trim.");
        }

        private static void ReplayCursorAdvancesAfterApply()
        {
            ShelteredWorldEventJournal journal = new ShelteredWorldEventJournal();
            journal.Append(CreateRecord("event-1", "trade.offer", 1));
            journal.Append(CreateRecord("event-2", "trade.accepted", 1));
            journal.Append(CreateRecord("event-3", "raid.started", 2));
            ShelteredWorldEventReplayCursor cursor = new ShelteredWorldEventReplayCursor();

            IList<ShelteredWorldEventRecord> firstRead = cursor.EnumerateUnapplied(journal);
            cursor.AdvanceAfterApply(firstRead[0]);
            IList<ShelteredWorldEventRecord> secondRead = cursor.EnumerateUnapplied(journal);
            cursor.AdvanceAfterApply(secondRead[0]);
            cursor.AdvanceAfterApply(secondRead[1]);

            TestAssert.Equal(3, firstRead.Count, "Cursor should initially expose all unapplied records.");
            TestAssert.Equal(2, secondRead.Count, "Cursor should hide records only after successful apply.");
            TestAssert.Equal((long)2, cursor.LastAppliedTick, "Cursor should track the last applied tick.");
            TestAssert.Equal("event-3", cursor.LastAppliedEventId, "Cursor should track the last applied event id.");
        }

        private static ShelteredWorldEventRecord CreateRecord(string eventId, string eventKind, long worldTick)
        {
            return new ShelteredWorldEventRecord
            {
                EventId = eventId,
                EventKind = eventKind,
                CorrelationId = "corr",
                SourcePlayerId = 1,
                SourceNetworkPeerId = 1,
                WorldTick = worldTick,
                WorldDeltaSeconds = 0.1f,
                PayloadJson = "{}",
                Authoritative = true
            };
        }
    }
}
