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
            tests.Add(new TestCase("WorldClock_HostUpdateAdvancesTick", HostUpdateAdvancesTick));
            tests.Add(new TestCase("WorldClock_ClientRemoteSampleAppliesIfNewer", ClientRemoteSampleAppliesIfNewer));
            tests.Add(new TestCase("WorldClock_OldRemoteSampleIsIgnored", OldRemoteSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_NegativeRemoteTickClampsToZero", NegativeRemoteTickClampsToZero));
            tests.Add(new TestCase("WorldClock_ClientPredictionStopsAfterHostSample", ClientPredictionStopsAfterHostSample));
            tests.Add(new TestCase("WorldClock_EqualRemoteSampleAfterHostSampleIsIgnored", EqualRemoteSampleAfterHostSampleIsIgnored));
            tests.Add(new TestCase("WorldClock_DriftReportUsesLastHostSample", DriftReportUsesLastHostSample));
        }

        private static void HostUpdateAdvancesTick()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateHost(clock, 20);

            try
            {
                long tick = clock.UpdateFromHostFrame(0.1f);

                TestAssert.Equal(2L, tick, "Host frame should advance by delta seconds multiplied by tick rate.");
                TestAssert.Equal(2L, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldTick,
                    "Host frame should update coordinator world tick.");
                TestAssert.Near(0.1f, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldDeltaSeconds, 0.0001f,
                    "Host frame should update coordinator world delta.");
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

        private static void NegativeRemoteTickClampsToZero()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                bool applied = clock.ApplyRemoteSample(CreateHostSample(-20, -1f, 20));

                TestAssert.True(applied, "Client should accept the first host sample after clamping negative values.");
                TestAssert.Equal(0L, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldTick,
                    "Negative host sample tick should clamp to zero.");
                TestAssert.Near(0f, ShelteredMultiplayerSessionCoordinator.Instance.Context.WorldDeltaSeconds, 0.0001f,
                    "Negative host sample delta should clamp to zero.");
            }
            finally
            {
                RestoreContext(previous);
                ResetClockFields(clock);
            }
        }

        private static void ClientPredictionStopsAfterHostSample()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerSessionContext previous = ActivateClient(clock, 20);

            try
            {
                TestAssert.Equal(2L, clock.UpdateFromHostFrame(0.1f),
                    "Client may predict the clock before a host sample exists.");
                TestAssert.True(clock.ApplyRemoteSample(CreateHostSample(3, 0.05f, 20)),
                    "Client should accept the first newer host sample.");

                long tick = clock.UpdateFromHostFrame(1f);

                TestAssert.Equal(3L, tick, "Client should not freely advance authoritative tick after a host sample exists.");
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
                TestAssert.True(clock.ApplyRemoteSample(CreateHostSample(7, 0.05f, 20)),
                    "Client should apply the first host sample.");

                bool applied = clock.ApplyRemoteSample(CreateHostSample(7, 0.05f, 20));

                TestAssert.False(applied, "Client should ignore equal host samples once host authority is established.");
                TestAssert.Equal(7L, clock.GetCurrentTick(), "Equal host sample should not change current tick.");
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
                "_hasHostSample",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock, false);
            typeof(ShelteredMultiplayerWorldClock).GetField(
                "_lastHostSample",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock, null);
        }
    }
}
