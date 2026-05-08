using System;
using System.Collections.Generic;
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
            tests.Add(new TestCase("WorldClock_FractionalFixedDeltaIsDeterministic", FractionalFixedDeltaIsDeterministic));
            tests.Add(new TestCase("WorldClock_ClientRemoteSampleAppliesIfNewer", ClientRemoteSampleAppliesIfNewer));
            tests.Add(new TestCase("WorldClock_OldRemoteSampleIsIgnored", OldRemoteSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_ForeignSessionRemoteSampleIsIgnored", ForeignSessionRemoteSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_NegativeRemoteTickDoesNotMoveCurrentTick", NegativeRemoteTickDoesNotMoveCurrentTick));
            tests.Add(new TestCase("WorldClock_ClientFixedProgressContinuesAfterHostSample", ClientFixedProgressContinuesAfterHostSample));
            tests.Add(new TestCase("WorldClock_EqualRemoteSampleAfterHostSampleIsIgnored", EqualRemoteSampleAfterHostSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_DriftReportUsesLastHostSample", DriftReportUsesLastHostSample));
            tests.Add(new TestCase("WorldClock_CorrectionServiceAdvancesWithoutHostSamples", CorrectionServiceAdvancesWithoutHostSamples));
            tests.Add(new TestCase("WorldClock_CorrectionServiceAppliesAuthoritativeEvent", CorrectionServiceAppliesAuthoritativeEvent));
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
            typeof(ShelteredMultiplayerWorldClock).GetField(
                "_fractionalTicks",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock, 0d);
            typeof(ShelteredMultiplayerWorldClock).GetField(
                "_lastHostSample",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock, null);
        }
    }
}
