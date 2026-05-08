using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredWorldClockTests
    {
        private const string SessionId = "world-clock-tests";

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("WorldClock_FixedDeltaAdvancesTick", FixedDeltaAdvancesTick));
            tests.Add(new TestCase("WorldClock_FixedStepSequenceIsDeterministic", FixedStepSequenceIsDeterministic));
            tests.Add(new TestCase("WorldClock_FrameChunkingUsesSameFixedTicks", FrameChunkingUsesSameFixedTicks));
            tests.Add(new TestCase("WorldClock_FractionalFixedDeltaIsDeterministic", FractionalFixedDeltaIsDeterministic));
            tests.Add(new TestCase("WorldClock_ClientRemoteSampleAppliesIfNewer", ClientRemoteSampleAppliesIfNewer));
            tests.Add(new TestCase("WorldClock_OldRemoteSampleIsIgnored", OldRemoteSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_ForeignSessionRemoteSampleIsIgnored", ForeignSessionRemoteSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_NonAuthoritativeRemoteSampleIsIgnored", NonAuthoritativeRemoteSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_NegativeRemoteTickDoesNotMoveCurrentTick", NegativeRemoteTickDoesNotMoveCurrentTick));
            tests.Add(new TestCase("WorldClock_SmallCorrectionIsAcceptedByPolicy", SmallCorrectionIsAcceptedByPolicy));
            tests.Add(new TestCase("WorldClock_LargeCorrectionReportsDesyncPolicy", LargeCorrectionReportsDesyncPolicy));
            tests.Add(new TestCase("WorldClock_ClientFixedProgressContinuesAfterHostSample", ClientFixedProgressContinuesAfterHostSample));
            tests.Add(new TestCase("WorldClock_EqualRemoteSampleAfterHostSampleIsIgnored", EqualRemoteSampleAfterHostSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_DriftReportUsesLastHostSample", DriftReportUsesLastHostSample));
            tests.Add(new TestCase("WorldClock_CorrectionServiceAdvancesWithoutHostSamples", CorrectionServiceAdvancesWithoutHostSamples));
            tests.Add(new TestCase("WorldClock_CorrectionServiceAppliesAuthoritativeEvent", CorrectionServiceAppliesAuthoritativeEvent));
            tests.Add(new TestCase("WorldClock_TickSchedulerDoesNotUseDateTimeForAdvancement", TickSchedulerDoesNotUseDateTimeForAdvancement));
            tests.Add(new TestCase("WorldTimeProjection_ProjectsStableCalendarFromTick", ProjectsStableCalendarFromTick));
            tests.Add(new TestCase("WorldTimeProjection_CrossesDayBoundary", CrossesDayBoundary));
            tests.Add(new TestCase("WorldTimeProjection_CrossesWeekBoundary", CrossesWeekBoundary));
        }

        private static void ProjectsStableCalendarFromTick()
        {
            ShelteredWorldTimeProjectionSnapshot projection = ShelteredWorldTimeProjection.Instance.Project(
                960,
                20,
                ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);

            TestAssert.Equal(960L, projection.WorldTick, "Projection should preserve the source world tick.");
            TestAssert.Equal(20, projection.TickRate, "Projection should preserve the normalized tick rate.");
            TestAssert.Near(10800f, (float)projection.ElapsedGameSeconds, 0.001f,
                "Projection should derive elapsed game seconds from WorldTick, TickRate, and multiplayer day seconds.");
            TestAssert.Near(32400f, (float)projection.GameSeconds, 0.001f,
                "Projection should include vanilla 21600-second day start.");
            TestAssert.Equal(9, projection.Hour, "Projection should derive the visible hour.");
            TestAssert.Equal(0, projection.Minute, "Projection should derive the visible minute.");
            TestAssert.Equal(1, projection.Day, "Projection should derive the visible day.");
            TestAssert.Equal(1, projection.Week, "Projection should derive the visible week.");
            TestAssert.False(projection.DayRollover, "Projection should not flag rollover away from a boundary.");
            TestAssert.False(projection.WeekRollover, "Projection should not flag week rollover away from a boundary.");
        }

        private static void CrossesDayBoundary()
        {
            long dayTicks = MultiplayerDayTicks(20);

            ShelteredWorldTimeProjectionSnapshot before = ShelteredWorldTimeProjection.Instance.Project(
                dayTicks - 1,
                20,
                ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);
            ShelteredWorldTimeProjectionSnapshot after = ShelteredWorldTimeProjection.Instance.Project(
                dayTicks,
                20,
                ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);

            TestAssert.Equal(1, before.Day, "Projection should remain on day one before the day boundary.");
            TestAssert.Equal(5, before.Hour, "Projection should be just before the vanilla 06:00 day boundary.");
            TestAssert.Equal(59, before.Minute, "Projection should be just before the vanilla 06:00 day boundary.");
            TestAssert.False(before.DayRollover, "Projection should not flag day rollover before the boundary.");
            TestAssert.Equal(2, after.Day, "Projection should advance the visible day at the boundary.");
            TestAssert.Equal(6, after.Hour, "Projection should keep the vanilla 06:00 day boundary.");
            TestAssert.Equal(0, after.Minute, "Projection should keep the vanilla 06:00 day boundary.");
            TestAssert.True(after.DayRollover, "Projection should flag day rollover on the first tick at the new day.");
            TestAssert.False(after.WeekRollover, "Projection should not flag week rollover on day two.");
        }

        private static void CrossesWeekBoundary()
        {
            long weekTicks = MultiplayerDayTicks(20) * 7;

            ShelteredWorldTimeProjectionSnapshot before = ShelteredWorldTimeProjection.Instance.Project(
                weekTicks - 1,
                20,
                ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);
            ShelteredWorldTimeProjectionSnapshot after = ShelteredWorldTimeProjection.Instance.Project(
                weekTicks,
                20,
                ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);

            TestAssert.Equal(7, before.Day, "Projection should remain on day seven before the week boundary.");
            TestAssert.Equal(1, before.Week, "Projection should remain on week one before the week boundary.");
            TestAssert.False(before.WeekRollover, "Projection should not flag week rollover before the boundary.");
            TestAssert.Equal(8, after.Day, "Projection should advance to day eight at the week boundary.");
            TestAssert.Equal(2, after.Week, "Projection should advance to week two at the week boundary.");
            TestAssert.True(after.DayRollover, "Week boundary projection should also expose the day rollover.");
            TestAssert.True(after.WeekRollover, "Projection should flag week rollover on the first tick of week two.");
            TestAssert.Equal(7, after.PreviousDay, "Projection should expose the previous day for side-effect integration.");
            TestAssert.Equal(1, after.PreviousWeek, "Projection should expose the previous week for side-effect integration.");
        }

        private static void FixedDeltaAdvancesTick()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateHost(clock, 20);

            try
            {
                long tick = clock.AdvanceFixedDelta(0.1f);

                TestAssert.Equal(2L, tick, "Fixed delta should advance by delta seconds multiplied by tick rate.");
                TestAssert.Equal(2L, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldTick,
                    "Fixed delta should update coordinator world tick.");
                TestAssert.Near(0.1f, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldDeltaSeconds, 0.0001f,
                    "Fixed delta should update coordinator world delta.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void FixedStepSequenceIsDeterministic()
        {
            ShelteredWorldTickScheduler first = new ShelteredWorldTickScheduler();
            ShelteredWorldTickScheduler second = new ShelteredWorldTickScheduler();

            long firstTicks = first.AdvanceFixedSteps(1, 20).TicksToAdvance
                + first.AdvanceFixedSteps(3, 20).TicksToAdvance
                + first.AdvanceFixedSteps(6, 20).TicksToAdvance;
            long secondTicks = second.AdvanceFixedSteps(1, 20).TicksToAdvance
                + second.AdvanceFixedSteps(3, 20).TicksToAdvance
                + second.AdvanceFixedSteps(6, 20).TicksToAdvance;

            TestAssert.Equal(10L, firstTicks, "Explicit fixed-step sequence should advance the expected tick count.");
            TestAssert.Equal(firstTicks, secondTicks, "The same explicit fixed-step sequence should produce the same tick count.");
        }

        private static void FrameChunkingUsesSameFixedTicks()
        {
            ShelteredWorldTickScheduler singleChunk = new ShelteredWorldTickScheduler();
            ShelteredWorldTickScheduler frameChunks = new ShelteredWorldTickScheduler();

            long singleTicks = singleChunk.AccumulateFixedInterval(0.25f, 20).TicksToAdvance;
            long chunkedTicks = frameChunks.AccumulateFixedInterval(0.016f, 20).TicksToAdvance
                + frameChunks.AccumulateFixedInterval(0.017f, 20).TicksToAdvance
                + frameChunks.AccumulateFixedInterval(0.017f, 20).TicksToAdvance
                + frameChunks.AccumulateFixedInterval(0.100f, 20).TicksToAdvance
                + frameChunks.AccumulateFixedInterval(0.100f, 20).TicksToAdvance;

            TestAssert.Equal(5L, singleTicks, "Single fixed interval should produce five ticks at 20 Hz.");
            TestAssert.Equal(singleTicks, chunkedTicks,
                "Frame-sized chunks totaling the same elapsed fixed interval should produce the same fixed ticks.");
        }

        private static void FractionalFixedDeltaIsDeterministic()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateHost(clock, 20);

            try
            {
                TestAssert.Equal(0L, clock.AdvanceFixedDelta(0.024f),
                    "Fractional fixed delta should not advance until a whole tick is accumulated.");
                TestAssert.Equal(1L, clock.AdvanceFixedDelta(0.026f),
                    "Accumulated fixed delta should advance exactly one tick at 20 Hz.");
                TestAssert.Equal(3L, clock.AdvanceFixedDelta(0.1f),
                    "Subsequent fixed delta should preserve deterministic accumulated state.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void ClientRemoteSampleAppliesIfNewer()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                ShelteredMultiplayerSessionCoordinator.Instance.SetWorldTick(4, 0.05f, "world-clock-test-current");

                bool applied = clock.ApplyRemoteSample(CreateHostSample(7, 0.05f, 20));

                TestAssert.True(applied, "Client should apply a newer host-authoritative world clock sample.");
                TestAssert.Equal(7L, clock.GetCurrentTick(), "Newer host sample should become current coordinator tick.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void OldRemoteSampleIsIgnored()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                ShelteredMultiplayerSessionCoordinator.Instance.SetWorldTick(10, 0.05f, "world-clock-test-current");

                bool applied = clock.ApplyRemoteSample(CreateHostSample(9, 0.05f, 20));

                TestAssert.True(!applied, "Client should ignore an older host-authoritative world clock sample.");
                TestAssert.Equal(10L, clock.GetCurrentTick(), "Old host sample should not move coordinator tick backward.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void ForeignSessionRemoteSampleIsIgnored()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                ShelteredWorldClockSample sample = CreateHostSample(7, 0.05f, 20);
                sample.SessionId = "foreign-session";

                bool applied = clock.ApplyRemoteSample(sample);

                TestAssert.False(applied, "Client should ignore a host sample from a different session.");
                TestAssert.Equal(0L, clock.GetCurrentTick(), "Foreign-session host sample should not change current tick.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void NonAuthoritativeRemoteSampleIsIgnored()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                ShelteredWorldClockSample sample = CreateHostSample(7, 0.05f, 20);
                sample.HostAuthoritative = false;

                bool applied = clock.ApplyRemoteSample(sample);

                TestAssert.False(applied, "Client should ignore non-authoritative world clock samples.");
                TestAssert.Equal(0L, clock.GetCurrentTick(), "Non-authoritative samples should not change current tick.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void NegativeRemoteTickDoesNotMoveCurrentTick()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                bool applied = clock.ApplyRemoteSample(CreateHostSample(-20, -1f, 20));

                TestAssert.False(applied, "Client should not treat a clamped negative host sample as a correction.");
                TestAssert.Equal(0L, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldTick,
                    "Negative host sample tick should not move current tick.");
                TestAssert.Near(0f, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldDeltaSeconds, 0.0001f,
                    "Negative host sample delta should not move current delta.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void SmallCorrectionIsAcceptedByPolicy()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                ShelteredMultiplayerSessionCoordinator.Instance.SetWorldTick(5, 0.05f, "world-clock-test-current");

                ShelteredWorldClockCorrectionResult result =
                    clock.ApplyRemoteSampleDetailed(CreateHostSample(15, 0.05f, 20));

                TestAssert.True(result.Applied, "Small host drift should be accepted as a correction.");
                TestAssert.Equal(ShelteredWorldClockCorrectionOutcome.Applied, result.Outcome,
                    "Small correction should report the applied outcome.");
                TestAssert.Equal(ShelteredWorldClockDriftSeverity.SmallCorrection, result.DriftDecision.Severity,
                    "Small correction should be classified by the centralized drift policy.");
                TestAssert.Equal(15L, clock.GetCurrentTick(), "Small correction should move client to host tick.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void LargeCorrectionReportsDesyncPolicy()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                ShelteredMultiplayerSessionCoordinator.Instance.SetWorldTick(5, 0.05f, "world-clock-test-current");

                ShelteredWorldClockCorrectionResult result =
                    clock.ApplyRemoteSampleDetailed(CreateHostSample(26, 0.05f, 20));
                ShelteredWorldClockDriftReport report = clock.GetDriftReport();

                TestAssert.False(result.Applied, "Large host drift should not be silently corrected.");
                TestAssert.True(result.RequiresDesyncDiagnostics,
                    "Large host drift should require desync/resync diagnostics.");
                TestAssert.Equal(ShelteredWorldClockCorrectionOutcome.DesyncDiagnosticsRequired, result.Outcome,
                    "Large drift should report the desync diagnostics outcome.");
                TestAssert.Equal(ShelteredWorldClockDriftSeverity.DesyncDiagnosticsRequired, result.DriftDecision.Severity,
                    "Large drift should be classified by the centralized drift policy.");
                TestAssert.Equal(5L, clock.GetCurrentTick(), "Large correction should not silently move current tick.");
                TestAssert.Equal(26L, report.HostTick, "Drift report should retain the latest monotonic host sample tick.");
                TestAssert.True(report.RequiresDesyncDiagnostics,
                    "Drift report should expose the desync diagnostics policy result.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void ClientFixedProgressContinuesAfterHostSample()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                TestAssert.Equal(2L, clock.AdvanceFixedDelta(0.1f),
                    "Client should advance from fixed local deltas before a host sample exists.");
                TestAssert.True(clock.ApplyRemoteSample(CreateHostSample(3, 0.05f, 20)),
                    "Client should accept a newer host sample as a correction.");

                long tick = clock.AdvanceFixedDelta(1f);

                TestAssert.Equal(23L, tick, "Client should keep fixed-rate progression after a correction sample.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void EqualRemoteSampleAfterHostSampleIsIgnored()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                ShelteredMultiplayerSessionCoordinator.Instance.SetWorldTick(7, 0.05f, "world-clock-test-current");

                bool applied = clock.ApplyRemoteSample(CreateHostSample(7, 0.05f, 20));

                TestAssert.False(applied, "Client should ignore equal host samples as non-corrections.");
                TestAssert.Equal(7L, clock.GetCurrentTick(), "Equal host sample should not change current tick.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void CorrectionServiceAdvancesWithoutHostSamples()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                using (ShelteredMultiplayerWorldClockCorrectionService service = new ShelteredMultiplayerWorldClockCorrectionService(clock))
                {
                    service.Update(0.1f);
                }

                TestAssert.Equal(2L, clock.GetCurrentTick(),
                    "Correction service should advance clients from fixed local deltas without requiring periodic host samples.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void CorrectionServiceAppliesAuthoritativeEvent()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                using (ShelteredMultiplayerWorldClockCorrectionService service = new ShelteredMultiplayerWorldClockCorrectionService(clock))
                {
                    ShelteredNetworkGameplayEvent gameplayEvent =
                        ShelteredWorldClockSampleCodec.ToGameplayEvent(CreateHostSample(10, 0.05f, 20));

                    ShelteredMultiplayerNetworkEvents.RaiseAuthoritative(
                        new ShelteredNetworkEventContext(null, null, gameplayEvent));
                }

                TestAssert.Equal(10L, clock.GetCurrentTick(),
                    "Correction service should apply newer authoritative correction events.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void DriftReportUsesLastHostSample()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                TestAssert.True(clock.ApplyRemoteSample(CreateHostSample(8, 0.05f, 20)),
                    "Client should apply host sample before reporting drift.");
                ShelteredMultiplayerSessionCoordinator.Instance.SetWorldTick(10, 0.05f, "world-clock-test-local-drift");

                ShelteredWorldClockDriftReport report = clock.GetDriftReport();

                TestAssert.Equal(10L, report.LocalTick, "Drift report should include local tick.");
                TestAssert.Equal(8L, report.HostTick, "Drift report should include last host tick.");
                TestAssert.Equal(2L, report.DriftTicks, "Drift report should compute local minus host tick.");
                TestAssert.False(report.IsHostAuthoritative, "Client drift report should not mark local host authority.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void TickSchedulerDoesNotUseDateTimeForAdvancement()
        {
            string repoRoot = FindRepoRoot();
            string schedulerPath = Path.Combine(
                Path.Combine(
                    Path.Combine(
                        Path.Combine(repoRoot, "ShelteredAPI"),
                        "Networking"),
                    "World"),
                "ShelteredWorldTickScheduler.cs");
            string text = File.ReadAllText(schedulerPath);

            TestAssert.True(text.IndexOf("DateTime.UtcNow", StringComparison.Ordinal) < 0
                && text.IndexOf("DateTime.Now", StringComparison.Ordinal) < 0,
                "World tick advancement must not use wall-clock DateTime APIs.");
        }

        private static ShelteredMultiplayerSessionContext ActivateHost(ShelteredMultiplayerWorldClock clock, int tickRate)
        {
            ShelteredMultiplayerSessionContext previous = ReplaceContext(CreateContext(
                ShelteredMultiplayerSessionMode.Host,
                1,
                NetworkDefaults.HostPeerId,
                tickRate,
                0,
                0f,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative));
            clock.Reset("test-host");
            return previous;
        }

        private static ShelteredMultiplayerSessionContext ActivateClient(ShelteredMultiplayerWorldClock clock, int tickRate)
        {
            ShelteredMultiplayerSessionContext previous = ReplaceContext(CreateContext(
                ShelteredMultiplayerSessionMode.Client,
                2,
                2,
                tickRate,
                0,
                0f,
                ShelteredMultiplayerGameTimeMode.RemoteAuthoritative));
            clock.Reset("test-client");
            return previous;
        }

        private static ShelteredWorldClockSample CreateHostSample(long worldTick, float deltaSeconds, int tickRate)
        {
            return new ShelteredWorldClockSample
            {
                SessionId = SessionId,
                WorldTick = worldTick,
                DeltaSeconds = deltaSeconds,
                TickRate = tickRate,
                SampleUtc = DateTime.UtcNow,
                HostAuthoritative = true
            };
        }

        private static long MultiplayerDayTicks(int tickRate)
        {
            return (long)(tickRate * ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);
        }

        private static ShelteredMultiplayerSessionContext CreateContext(
            ShelteredMultiplayerSessionMode mode,
            int localPlayerId,
            byte networkPeerId,
            int tickRate,
            long worldTick,
            float worldDeltaSeconds,
            ShelteredMultiplayerGameTimeMode timeMode)
        {
            return new ShelteredMultiplayerSessionContext(
                mode,
                SessionId,
                localPlayerId,
                networkPeerId,
                "world-clock-test-peer",
                tickRate,
                worldTick,
                worldDeltaSeconds,
                timeMode,
                ShelteredMultiplayerSetupPhase.Activated,
                new ShelteredMultiplayerPeerInfo[0],
                new ShelteredMultiplayerBunkerAssignmentRecord[0],
                ShelteredMultiplayerSetupSettings.Empty,
                "world-clock-test");
        }

        private static ShelteredMultiplayerSessionContext ReplaceContext(ShelteredMultiplayerSessionContext context)
        {
            FieldInfo contextField = GetCoordinatorContextField();
            ShelteredMultiplayerSessionContext previous =
                (ShelteredMultiplayerSessionContext)contextField.GetValue(ShelteredMultiplayerSessionCoordinator.Instance);
            contextField.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, context);
            return previous;
        }

        private static void RestoreContext(ShelteredMultiplayerSessionContext previous)
        {
            GetCoordinatorContextField().SetValue(ShelteredMultiplayerSessionCoordinator.Instance, previous);
        }

        private static FieldInfo GetCoordinatorContextField()
        {
            return typeof(ShelteredMultiplayerSessionCoordinator).GetField(
                "_context",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static void ResetClockFields(ShelteredMultiplayerWorldClock clock)
        {
            IShelteredWorldTickScheduler scheduler =
                (IShelteredWorldTickScheduler)typeof(ShelteredMultiplayerWorldClock).GetField(
                    "_scheduler",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clock);
            if (scheduler != null)
                scheduler.Reset();
            typeof(ShelteredMultiplayerWorldClock).GetField(
                "_lastHostSample",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock, null);
        }

        private static string FindRepoRoot()
        {
            string fromCurrentDirectory = FindRepoRootFrom(Directory.GetCurrentDirectory());
            if (fromCurrentDirectory.Length > 0)
                return fromCurrentDirectory;

            string fromBaseDirectory = FindRepoRootFrom(AppDomain.CurrentDomain.BaseDirectory);
            if (fromBaseDirectory.Length > 0)
                return fromBaseDirectory;

            throw new InvalidOperationException("Could not locate repo root containing ShelteredModManager.sln.");
        }

        private static string FindRepoRootFrom(string start)
        {
            if (string.IsNullOrEmpty(start))
                return string.Empty;

            DirectoryInfo dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ShelteredModManager.sln"))
                    && Directory.Exists(Path.Combine(dir.FullName, "ShelteredAPI")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return string.Empty;
        }
    }
}
