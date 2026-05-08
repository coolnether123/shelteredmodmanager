using System;
using ShelteredAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerTimePolicyTests
    {
        private static float _testTimeScale;

        public static void Register(System.Collections.Generic.List<TestCase> tests)
        {
            tests.Add(new TestCase("TimePolicy_SharedWorldTravelCompensationNeutralizesShorterMultiplayerDay", SharedWorldTravelCompensationNeutralizesShorterMultiplayerDay));
            tests.Add(new TestCase("TimePolicy_LocalBunkerIntensityModesAreLocalOnly", LocalBunkerIntensityModesAreLocalOnly));
            tests.Add(new TestCase("TimePolicy_PauseRequestDoesNotPauseMultiplayer", PauseRequestDoesNotPauseMultiplayer));
            tests.Add(new TestCase("TimePolicy_ForceRealtimeTimescaleRestoresOne", ForceRealtimeTimescaleRestoresOne));
            tests.Add(new TestCase("TimePolicy_FastSlowInputsDoNotChangeWorldTickRate", FastSlowInputsDoNotChangeWorldTickRate));
        }

        private static void SharedWorldTravelCompensationNeutralizesShorterMultiplayerDay()
        {
            float expected = ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds / ShelteredMultiplayerTimeSettings.VanillaDaySeconds;
            TestAssert.Near(expected, ShelteredMultiplayerTimePolicy.SharedWorldTravelCompensationMultiplier, 0.0001f,
                "Shared world travel compensation should be multiplayer day seconds divided by vanilla day seconds.");
        }

        private static void LocalBunkerIntensityModesAreLocalOnly()
        {
            TestAssert.Near(ShelteredMultiplayerTimeSettings.CarefulBunkerIntensityMultiplier, ShelteredMultiplayerTimePolicy.GetLocalBunkerIntensityMultiplier(ShelteredMultiplayerLocalBunkerIntensityMode.Careful), 0.0001f,
                "Careful bunker mode should use the standardized local intensity factor.");
            TestAssert.Near(ShelteredMultiplayerTimeSettings.NormalBunkerIntensityMultiplier, ShelteredMultiplayerTimePolicy.GetLocalBunkerIntensityMultiplier(ShelteredMultiplayerLocalBunkerIntensityMode.Normal), 0.0001f,
                "Normal bunker mode should leave local intensity unchanged.");
            TestAssert.Near(ShelteredMultiplayerTimeSettings.RushBunkerIntensityMultiplier, ShelteredMultiplayerTimePolicy.GetLocalBunkerIntensityMultiplier(ShelteredMultiplayerLocalBunkerIntensityMode.Rush), 0.0001f,
                "Rush bunker mode should use the standardized local intensity factor.");

            float compensated = ShelteredMultiplayerTimePolicy.SharedWorldTravelCompensationMultiplier;
            UseFakeTimeScale(1f);
            ShelteredMultiplayerHookService.Instance.ActivateHost(1, "time-policy-local-only", 20);
            try
            {
                ShelteredMultiplayerTimePolicy.SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Careful, "test");
                TestAssert.Near(compensated, ShelteredMultiplayerTimePolicy.ApplyTravelDistance(1f), 0.0001f,
                    "Careful bunker mode must not slow shared world travel.");

                ShelteredMultiplayerTimePolicy.SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Rush, "test");
                TestAssert.Near(compensated, ShelteredMultiplayerTimePolicy.ApplyTravelDistance(1f), 0.0001f,
                    "Rush bunker mode must not speed shared world travel.");
            }
            finally
            {
                ShelteredMultiplayerTimePolicy.SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Normal, "test-cleanup");
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
            }
        }

        private static void PauseRequestDoesNotPauseMultiplayer()
        {
            UseFakeTimeScale(1f);
            ShelteredMultiplayerHookService.Instance.ActivateHost(1, "time-policy-pause", 20);
            try
            {
                _testTimeScale = 0f;

                bool allowVanillaPause = ShelteredMultiplayerHookService.Instance.HandlePauseRequest("test-pause", null);

                TestAssert.False(allowVanillaPause, "Active multiplayer should block vanilla pause requests.");
                TestAssert.Near(ShelteredMultiplayerTimeSettings.RealtimeTimescale, _testTimeScale, 0.0001f,
                    "Blocked multiplayer pause should restore realtime timescale.");
            }
            finally
            {
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
            }
        }

        private static void ForceRealtimeTimescaleRestoresOne()
        {
            UseFakeTimeScale(10f);
            ShelteredMultiplayerHookService.Instance.ActivateHost(1, "time-policy-timescale", 20);
            try
            {
                ShelteredMultiplayerTimePolicy.ForceRealtimeTimescale();

                TestAssert.Near(ShelteredMultiplayerTimeSettings.RealtimeTimescale, _testTimeScale, 0.0001f,
                    "Active multiplayer should force Time.timeScale back to realtime.");
            }
            finally
            {
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
            }
        }

        private static void FastSlowInputsDoNotChangeWorldTickRate()
        {
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            UseFakeTimeScale(1f);
            ShelteredMultiplayerHookService.Instance.ActivateHost(1, "time-policy-clock-rate", 20);
            try
            {
                clock.Reset("time-policy-clock-rate");

                ShelteredMultiplayerTimePolicy.TryHandleFastForward(true, null, "test-fast");
                TestAssert.Equal(20L, clock.AdvanceFixedDelta(1f),
                    "Fast-forward input should not increase shared world tick advancement.");

                ShelteredMultiplayerTimePolicy.TryHandleFastForward(false, null, "test-fast-off");
                ShelteredMultiplayerTimePolicy.TryHandleSlowDown(true, null, "test-slow");
                TestAssert.Equal(40L, clock.AdvanceFixedDelta(1f),
                    "Slow-down input should not reduce shared world tick advancement.");
            }
            finally
            {
                ShelteredMultiplayerTimePolicy.TryHandleSlowDown(false, null, "test-cleanup");
                ShelteredMultiplayerTimePolicy.SetLocalBunkerIntensityMode(ShelteredMultiplayerLocalBunkerIntensityMode.Normal, "test-cleanup");
                clock.Reset("time-policy-test-cleanup");
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
            }
        }

        private static void UseFakeTimeScale(float initialValue)
        {
            _testTimeScale = initialValue;
            ShelteredMultiplayerTimePolicy.OverrideTimeScaleAccessorsForTests(
                delegate { return _testTimeScale; },
                delegate(float value) { _testTimeScale = value; });
        }
    }
}
