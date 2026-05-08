using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredWorldTimeSideEffectTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("WorldTimeSideEffects_InactiveMultiplayerDoesNotApplyProjection", InactiveMultiplayerDoesNotApplyProjection));
            tests.Add(new TestCase("WorldTimeSideEffects_FirstProjectionRefreshesCalendarWithoutBoundaryEvents", FirstProjectionRefreshesCalendarWithoutBoundaryEvents));
            tests.Add(new TestCase("WorldTimeSideEffects_NoBoundaryEffectsBeforeBoundary", NoBoundaryEffectsBeforeBoundary));
            tests.Add(new TestCase("WorldTimeSideEffects_DayBoundaryFiresVanillaEquivalentDayEffects", DayBoundaryFiresVanillaEquivalentDayEffects));
            tests.Add(new TestCase("WorldTimeSideEffects_WeekBoundaryFiresDayAndWeekEffects", WeekBoundaryFiresDayAndWeekEffects));
            tests.Add(new TestCase("WorldTimeSideEffects_LargeDayJumpCoalescesPredictably", LargeDayJumpCoalescesPredictably));
            tests.Add(new TestCase("WorldTimeSideEffects_SinglePlayerSessionDoesNotFire", SinglePlayerSessionDoesNotFire));
            tests.Add(new TestCase("WorldTimeSideEffects_ReapplyingSameTickDoesNotDuplicate", ReapplyingSameTickDoesNotDuplicate));
            tests.Add(new TestCase("WorldTimeSideEffects_CalendarRefreshFiresWhenVisibleTimeChanges", CalendarRefreshFiresWhenVisibleTimeChanges));
        }

        private static void InactiveMultiplayerDoesNotApplyProjection()
        {
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);
            ShelteredWorldTimeProjectionSnapshot current = Project(1);

            ShelteredWorldTimeSideEffectReport report = service.Apply(null, current, false);

            TestAssert.False(report.MultiplayerActive, "Inactive multiplayer should report no side effects.");
            TestAssert.False(report.ProjectionApplied, "Inactive multiplayer should not apply projected time.");
            TestAssert.Equal(0, sink.ProjectedTimeCount, "Inactive multiplayer should not write projected time.");
        }

        private static void FirstProjectionRefreshesCalendarWithoutBoundaryEvents()
        {
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);
            ShelteredWorldTimeProjectionSnapshot current = Project(10);

            ShelteredWorldTimeSideEffectReport report = service.Apply(null, current, true);

            TestAssert.True(report.ProjectionApplied, "First active projection should write vanilla-visible time.");
            TestAssert.True(report.CalendarRefreshRequested, "First active projection should refresh visible calendar state.");
            TestAssert.False(report.NewDayFired, "First projection alone should not fire a new-day side effect.");
            TestAssert.False(report.NewWeekFired, "First projection alone should not fire a new-week side effect.");
            TestAssert.Equal(1, sink.ProjectedTimeCount, "Projected time should be written once.");
            TestAssert.Equal(1, sink.CalendarRefreshCount, "Calendar should be refreshed once.");
            TestAssert.Equal(0, sink.NewDayCount, "No day boundary should be fired.");
            TestAssert.Equal(0, sink.AutosaveCount, "No autosave should be requested without a day boundary.");
        }

        private static void DayBoundaryFiresVanillaEquivalentDayEffects()
        {
            long dayTicks = MultiplayerDayTicks();
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);

            ShelteredWorldTimeSideEffectReport report = service.Apply(Project(dayTicks - 1), Project(dayTicks), true);

            TestAssert.True(report.NewDayFired, "Crossing a projected multiplayer day boundary should fire new-day side effects.");
            TestAssert.False(report.NewWeekFired, "Crossing the first day boundary should not fire a new-week side effect.");
            TestAssert.True(report.AutosaveRequested, "Projected day boundaries should request the vanilla-equivalent autosave hook.");
            TestAssert.True(report.AchievementNewDayNotified, "Projected day boundaries should notify achievement day hooks.");
            TestAssert.True(report.CurrentDayStatNotified, "Projected day boundaries should notify current-day stats.");
            TestAssert.Equal(1, report.DaysCrossed, "Exactly one day should be reported as crossed.");
            TestAssert.Equal(1, sink.NewDayCount, "New-day sink should be invoked once.");
            TestAssert.Equal(1, sink.AutosaveCount, "Autosave sink should be invoked once.");
            TestAssert.Equal(1, sink.AchievementNewDayCount, "Achievement sink should be invoked once.");
            TestAssert.Equal(1, sink.CurrentDayStatCount, "Current-day stat sink should be invoked once.");
        }

        private static void NoBoundaryEffectsBeforeBoundary()
        {
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);

            service.Apply(Project(100), Project(200), true);

            TestAssert.Equal(0, sink.NewDayCount, "New-day side effects should not fire away from a projected day boundary.");
            TestAssert.Equal(0, sink.NewWeekCount, "New-week side effects should not fire away from a projected week boundary.");
            TestAssert.Equal(0, sink.AutosaveCount, "Autosave-equivalent requests should not fire away from a projected day boundary.");
            TestAssert.Equal(0, sink.AchievementNewDayCount, "Achievement new-day hooks should not fire away from a projected day boundary.");
            TestAssert.Equal(0, sink.CurrentDayStatCount, "Current-day stat hooks should not fire away from a projected day boundary.");
        }

        private static void WeekBoundaryFiresDayAndWeekEffects()
        {
            long weekTicks = MultiplayerDayTicks() * 7;
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);

            ShelteredWorldTimeSideEffectReport report = service.Apply(Project(weekTicks - 1), Project(weekTicks), true);

            TestAssert.True(report.NewDayFired, "Projected week boundary should also fire day side effects.");
            TestAssert.True(report.NewWeekFired, "Projected week boundary should fire new-week side effects.");
            TestAssert.Equal(1, report.DaysCrossed, "Week boundary should report a single day crossed from the previous tick.");
            TestAssert.Equal(1, report.WeeksCrossed, "Week boundary should report a single week crossed from the previous tick.");
            TestAssert.Equal(1, sink.NewDayCount, "New-day sink should be invoked once at week boundary.");
            TestAssert.Equal(1, sink.NewWeekCount, "New-week sink should be invoked once at week boundary.");
            TestAssert.Equal(1, sink.AutosaveCount, "Week boundary day rollover should request autosave once.");
        }

        private static void LargeDayJumpCoalescesPredictably()
        {
            long dayTicks = MultiplayerDayTicks();
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);

            ShelteredWorldTimeSideEffectReport report = service.Apply(Project(0), Project(dayTicks * 3), true);

            TestAssert.True(report.NewDayFired, "Large projected day jumps should still fire the coalesced new-day effect.");
            TestAssert.True(report.CoalescedBoundaryEvents, "Large projected day jumps should explicitly report coalesced boundary behavior.");
            TestAssert.Equal(3, report.DaysCrossed, "Large projected day jumps should preserve the crossed-day count.");
            TestAssert.Equal(1, sink.NewDayCount, "Large projected day jumps should coalesce new-day side effects into one sink call.");
            TestAssert.Equal(1, sink.AutosaveCount, "Large projected day jumps should not spam autosave-equivalent requests.");
        }

        private static void SinglePlayerSessionDoesNotFire()
        {
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);

            ShelteredWorldTimeSideEffectReport report = service.UpdateFromSession(CreateSinglePlayerContext(MultiplayerDayTicks()));

            TestAssert.False(report.MultiplayerActive, "Single-player session path should report inactive projected side effects.");
            TestAssert.Equal(0, sink.ProjectedTimeCount, "Single-player session path should not write projected multiplayer time.");
            TestAssert.Equal(0, sink.NewDayCount, "Single-player session path should not fire projected day side effects.");
            TestAssert.Equal(0, sink.NewWeekCount, "Single-player session path should not fire projected week side effects.");
            TestAssert.Equal(0, sink.AutosaveCount, "Single-player session path should not request projected autosaves.");
        }

        private static void ReapplyingSameTickDoesNotDuplicate()
        {
            long dayTicks = MultiplayerDayTicks();
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);

            service.UpdateFromSession(CreateHostContext(dayTicks - 1));
            service.UpdateFromSession(CreateHostContext(dayTicks));
            ShelteredWorldTimeSideEffectReport duplicate = service.UpdateFromSession(CreateHostContext(dayTicks));

            TestAssert.Equal(1, sink.NewDayCount, "Reapplying the same projected tick should not duplicate new-day side effects.");
            TestAssert.Equal(1, sink.AutosaveCount, "Reapplying the same projected tick should not duplicate autosave-equivalent requests.");
            TestAssert.False(duplicate.NewDayFired, "Duplicate same-tick application should not report a new-day side effect.");
        }

        private static void CalendarRefreshFiresWhenVisibleTimeChanges()
        {
            RecordingSideEffectSink sink = new RecordingSideEffectSink();
            ShelteredWorldTimeSideEffectService service = new ShelteredWorldTimeSideEffectService(sink);

            ShelteredWorldTimeSideEffectReport report = service.Apply(Project(0), Project(6), true);

            TestAssert.True(report.CalendarRefreshRequested, "Projected visible time changes should request calendar refresh.");
            TestAssert.Equal(1, sink.CalendarRefreshCount, "Projected visible time changes should invoke the calendar refresh sink once.");
        }

        private static ShelteredWorldTimeProjectionSnapshot Project(long tick)
        {
            return ShelteredWorldTimeProjection.Instance.Project(
                tick,
                ShelteredMultiplayerWorldClock.DefaultTickRate,
                ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);
        }

        private static long MultiplayerDayTicks()
        {
            return (long)(ShelteredMultiplayerWorldClock.DefaultTickRate * ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);
        }

        private static ShelteredMultiplayerSessionContext CreateHostContext(long worldTick)
        {
            return CreateContext(
                ShelteredMultiplayerSessionMode.Host,
                1,
                NetworkDefaults.HostPeerId,
                worldTick,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                ShelteredMultiplayerSetupPhase.Activated);
        }

        private static ShelteredMultiplayerSessionContext CreateSinglePlayerContext(long worldTick)
        {
            return CreateContext(
                ShelteredMultiplayerSessionMode.SinglePlayer,
                0,
                NetworkDefaults.UnassignedPeerId,
                worldTick,
                ShelteredMultiplayerGameTimeMode.Vanilla,
                ShelteredMultiplayerSetupPhase.Inactive);
        }

        private static ShelteredMultiplayerSessionContext CreateContext(
            ShelteredMultiplayerSessionMode mode,
            int localPlayerId,
            byte networkPeerId,
            long worldTick,
            ShelteredMultiplayerGameTimeMode timeMode,
            ShelteredMultiplayerSetupPhase setupPhase)
        {
            return new ShelteredMultiplayerSessionContext(
                mode,
                mode == ShelteredMultiplayerSessionMode.SinglePlayer ? string.Empty : "world-time-side-effects",
                localPlayerId,
                networkPeerId,
                "world-time-side-effects-peer",
                ShelteredMultiplayerWorldClock.DefaultTickRate,
                worldTick,
                1f / ShelteredMultiplayerWorldClock.DefaultTickRate,
                timeMode,
                setupPhase,
                new ShelteredMultiplayerPeerInfo[0],
                new ShelteredMultiplayerBunkerAssignmentRecord[0],
                ShelteredMultiplayerSetupSettings.Empty,
                "world-time-side-effects-test");
        }

        private sealed class RecordingSideEffectSink : IShelteredWorldTimeSideEffectSink
        {
            public int ProjectedTimeCount;
            public int CalendarRefreshCount;
            public int NewWeekCount;
            public int NewDayCount;
            public int AutosaveCount;
            public int AchievementNewDayCount;
            public int CurrentDayStatCount;

            public void ApplyProjectedTime(ShelteredWorldTimeProjectionSnapshot projection)
            {
                ProjectedTimeCount++;
            }

            public void RefreshCalendar(
                ShelteredWorldTimeProjectionSnapshot previous,
                ShelteredWorldTimeProjectionSnapshot current)
            {
                CalendarRefreshCount++;
            }

            public void FireNewWeek(ShelteredWorldTimeProjectionSnapshot projection, int weeksCrossed)
            {
                NewWeekCount++;
            }

            public void FireNewDay(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed)
            {
                NewDayCount++;
            }

            public void RequestAutosave(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed)
            {
                AutosaveCount++;
            }

            public void NotifyAchievementNewDay(ShelteredWorldTimeProjectionSnapshot projection)
            {
                AchievementNewDayCount++;
            }

            public void NotifyCurrentDayStat(ShelteredWorldTimeProjectionSnapshot projection)
            {
                CurrentDayStatCount++;
            }
        }
    }
}
