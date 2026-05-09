using System;
using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Networking.Diagnostics;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerTimelineTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Timeline_BoundedRingDropsOldest", BoundedRingDropsOldest));
            tests.Add(new TestCase("Timeline_SnapshotIsCloneSafe", SnapshotIsCloneSafe));
            tests.Add(new TestCase("Timeline_CompactFormattingIncludesKeyFields", CompactFormattingIncludesKeyFields));
            tests.Add(new TestCase("Timeline_AppendDoesNotThrowWhenContextMissing", AppendDoesNotThrowWhenContextMissing));
            tests.Add(new TestCase("Timeline_ClearRemovesEntries", ClearRemovesEntries));
        }

        private static void BoundedRingDropsOldest()
        {
            ShelteredMultiplayerTimeline timeline = CreateTimeline(3, CreateContext());

            timeline.Append(ShelteredMultiplayerTimelineCategory.Connection, ShelteredMultiplayerTimelineEventKind.HostStarted, "first");
            timeline.Append(ShelteredMultiplayerTimelineCategory.Connection, ShelteredMultiplayerTimelineEventKind.PeerConnected, 2, "second");
            timeline.Append(ShelteredMultiplayerTimelineCategory.Setup, ShelteredMultiplayerTimelineEventKind.SetupBeginSent, 2, "third");
            timeline.Append(ShelteredMultiplayerTimelineCategory.Setup, ShelteredMultiplayerTimelineEventKind.PeerLoaded, 2, "fourth");

            ShelteredMultiplayerTimelineEntry[] snapshot = timeline.GetSnapshot();
            TestAssert.Equal(3, snapshot.Length, "Timeline should retain only the configured capacity.");
            TestAssert.Equal("second", snapshot[0].Message, "Ring buffer should drop the oldest entry first.");
            TestAssert.Equal("fourth", snapshot[2].Message, "Newest entry should remain at the end of the snapshot.");
        }

        private static void SnapshotIsCloneSafe()
        {
            ShelteredMultiplayerTimeline timeline = CreateTimeline(4, CreateContext());
            timeline.Append(ShelteredMultiplayerTimelineCategory.Setup, ShelteredMultiplayerTimelineEventKind.SetupReceived, "setup");

            ShelteredMultiplayerTimelineEntry[] first = timeline.GetSnapshot();
            first[0] = null;

            ShelteredMultiplayerTimelineEntry[] second = timeline.GetSnapshot();
            TestAssert.False(object.ReferenceEquals(first, second), "Snapshots should not reuse the same array.");
            TestAssert.True(second[0] != null, "Mutating a returned snapshot array should not affect stored entries.");
        }

        private static void CompactFormattingIncludesKeyFields()
        {
            ShelteredMultiplayerSessionContext context = CreateContext();
            ShelteredMultiplayerTimeline timeline = CreateTimeline(4, context);
            timeline.Append(
                ShelteredMultiplayerTimelineCategory.Setup,
                ShelteredMultiplayerTimelineEventKind.SetupReceived,
                context,
                7,
                "received setup");

            string[] lines = timeline.FormatCompact(10);
            TestAssert.Equal(1, lines.Length, "Formatter should return one compact line for one entry.");
            AssertContains(lines[0], "SetupReceived", "Formatted timeline should include the event kind.");
            AssertContains(lines[0], "role=Host", "Formatted timeline should include the role/mode.");
            AssertContains(lines[0], "sid=abcd...5678", "Formatted timeline should include the short session id.");
            AssertContains(lines[0], "lp=2", "Formatted timeline should include the local player id.");
            AssertContains(lines[0], "peer=7", "Formatted timeline should include the relevant network peer id.");
            AssertContains(lines[0], "phase=Received", "Formatted timeline should include the setup phase.");
            AssertContains(lines[0], "tick=42", "Formatted timeline should include the world tick.");
            AssertContains(lines[0], "received setup", "Formatted timeline should include the message.");
        }

        private static void AppendDoesNotThrowWhenContextMissing()
        {
            ShelteredMultiplayerTimeline timeline = new ShelteredMultiplayerTimeline(
                4,
                delegate { throw new InvalidOperationException("missing context"); },
                FixedClock);

            timeline.Append(
                ShelteredMultiplayerTimelineCategory.Connection,
                ShelteredMultiplayerTimelineEventKind.ConnectionFailure,
                "connection failed without context");

            ShelteredMultiplayerTimelineEntry[] snapshot = timeline.GetSnapshot();
            TestAssert.Equal(1, snapshot.Length, "Append should still record an entry when context capture fails.");
            TestAssert.Equal(ShelteredMultiplayerSessionMode.SinglePlayer, snapshot[0].Mode,
                "Missing context should fall back to neutral session fields.");
            TestAssert.Equal(ShelteredMultiplayerSetupPhase.Inactive, snapshot[0].SetupPhase,
                "Missing context should not invent setup state.");
        }

        private static void ClearRemovesEntries()
        {
            ShelteredMultiplayerTimeline timeline = CreateTimeline(4, CreateContext());
            timeline.Append(ShelteredMultiplayerTimelineCategory.Load, ShelteredMultiplayerTimelineEventKind.LocalWorldLoaded, "loaded");

            timeline.Clear("unit-test-clear");

            TestAssert.Equal(0, timeline.GetSnapshot().Length, "Clear should remove all timeline entries.");
            TestAssert.Equal("unit-test-clear", timeline.LastClearReason, "Clear should remember its diagnostic reason.");
        }

        private static ShelteredMultiplayerTimeline CreateTimeline(
            int capacity,
            ShelteredMultiplayerSessionContext context)
        {
            return new ShelteredMultiplayerTimeline(capacity, delegate { return context; }, FixedClock);
        }

        private static ShelteredMultiplayerSessionContext CreateContext()
        {
            return new ShelteredMultiplayerSessionContext(
                ShelteredMultiplayerSessionMode.Host,
                "abcdefgh12345678",
                2,
                NetworkDefaults.HostPeerId,
                "host",
                20,
                42,
                0.05f,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                ShelteredMultiplayerSetupPhase.Received,
                new ShelteredMultiplayerPeerInfo[0],
                new ShelteredMultiplayerBunkerAssignmentRecord[0],
                ShelteredMultiplayerSetupSettings.Empty,
                "test");
        }

        private static DateTime FixedClock()
        {
            return new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc);
        }

        private static void AssertContains(string value, string expected, string message)
        {
            if ((value ?? string.Empty).IndexOf(expected, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(message + " Expected '" + value + "' to contain '" + expected + "'.");
        }
    }
}
