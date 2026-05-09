using System;
using System.Reflection;
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
            tests.Add(new TestCase("TimePolicy_MultiplayerGameTimeSuppressesVanillaDeltaAuthority", MultiplayerGameTimeSuppressesVanillaDeltaAuthority));
            tests.Add(new TestCase("TimePolicy_SinglePlayerGameTimePathAllowsVanillaUpdate", SinglePlayerGameTimePathAllowsVanillaUpdate));
            tests.Add(new TestCase("TimePolicy_PauseDoesNotStopProjectedMultiplayerTime", PauseDoesNotStopProjectedMultiplayerTime));
            tests.Add(new TestCase("TimePolicy_SetupGateProjectionDoesNotCrossDayBoundary", SetupGateProjectionDoesNotCrossDayBoundary));
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
                TestAssert.Near(ShelteredMultiplayerTimeSettings.RealtimeTimescale, _testTimeScale, 0.0001f,
                    "Fast-forward input should not change Time.timeScale during multiplayer.");

                ShelteredMultiplayerTimePolicy.TryHandleFastForward(false, null, "test-fast-off");
                ShelteredMultiplayerTimePolicy.TryHandleSlowDown(true, null, "test-slow");
                TestAssert.Equal(40L, clock.AdvanceFixedDelta(1f),
                    "Slow-down input should not reduce shared world tick advancement.");
                TestAssert.Near(ShelteredMultiplayerTimeSettings.RealtimeTimescale, _testTimeScale, 0.0001f,
                    "Slow-down input should not change Time.timeScale during multiplayer.");
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

        private static void MultiplayerGameTimeSuppressesVanillaDeltaAuthority()
        {
            GameTimeStaticState previous = CaptureGameTimeStaticState();
            UseFakeTimeScale(1f);
            ShelteredMultiplayerHookService.Instance.ActivateHost(1, "time-policy-projection", 20);
            try
            {
                ShelteredMultiplayerHookService.Instance.SetWorldTick(960, 0.05f);
                WriteGameTimeField("game_time", 12345f);
                WriteGameTimeField("current_hour", 23);
                WriteGameTimeField("current_minute", 58);
                WriteGameTimeField("current_day", 42);
                WriteGameTimeField("current_week", 6);

                bool allowVanillaUpdate = ShelteredMultiplayerHookService.Instance.BeginGameTimeUpdate(null);

                TestAssert.False(allowVanillaUpdate,
                    "Active multiplayer should suppress vanilla GameTime.Update delta authority.");
                TestAssert.Equal(9, ReadIntGameTimeField("current_hour"),
                    "Projected multiplayer GameTime hour should come from WorldTick.");
                TestAssert.Equal(0, ReadIntGameTimeField("current_minute"),
                    "Projected multiplayer GameTime minute should come from WorldTick.");
                TestAssert.Equal(1, ReadIntGameTimeField("current_day"),
                    "Projected multiplayer GameTime day should come from WorldTick.");
                TestAssert.Equal(1, ReadIntGameTimeField("current_week"),
                    "Projected multiplayer GameTime week should come from WorldTick.");
                TestAssert.Near(32400f, ReadFloatGameTimeField("game_time"), 0.001f,
                    "Projected multiplayer GameTime seconds should include vanilla 21600-second day start.");
            }
            finally
            {
                ShelteredMultiplayerHookService.Instance.EndGameTimeUpdate(null);
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ApplyGameTimePolicy(null);
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
                previous.Restore();
            }
        }

        private static void SinglePlayerGameTimePathAllowsVanillaUpdate()
        {
            GameTimeStaticState previous = CaptureGameTimeStaticState();
            UseFakeTimeScale(1f);
            ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-single-player");
            try
            {
                WriteGameTimeField("game_time", 12345f);
                WriteGameTimeField("current_hour", 3);
                WriteGameTimeField("current_minute", 25);
                WriteGameTimeField("current_day", 12);
                WriteGameTimeField("current_week", 2);

                bool allowVanillaUpdate = ShelteredMultiplayerHookService.Instance.BeginGameTimeUpdate(null);

                TestAssert.True(allowVanillaUpdate,
                    "Inactive multiplayer should leave vanilla GameTime.Update enabled.");
                TestAssert.Near(12345f, ReadFloatGameTimeField("game_time"), 0.001f,
                    "Single-player GameTime seconds should remain untouched by multiplayer projection.");
                TestAssert.Equal(3, ReadIntGameTimeField("current_hour"),
                    "Single-player GameTime hour should remain untouched by multiplayer projection.");
                TestAssert.Equal(25, ReadIntGameTimeField("current_minute"),
                    "Single-player GameTime minute should remain untouched by multiplayer projection.");
                TestAssert.Equal(12, ReadIntGameTimeField("current_day"),
                    "Single-player GameTime day should remain untouched by multiplayer projection.");
                TestAssert.Equal(2, ReadIntGameTimeField("current_week"),
                    "Single-player GameTime week should remain untouched by multiplayer projection.");
            }
            finally
            {
                ShelteredMultiplayerHookService.Instance.EndGameTimeUpdate(null);
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ApplyGameTimePolicy(null);
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
                previous.Restore();
            }
        }

        private static void PauseDoesNotStopProjectedMultiplayerTime()
        {
            GameTimeStaticState previous = CaptureGameTimeStaticState();
            UseFakeTimeScale(0f);
            ShelteredMultiplayerHookService.Instance.ActivateHost(1, "time-policy-pause-projection", 20);
            try
            {
                ShelteredMultiplayerHookService.Instance.SetWorldTick(MultiplayerDayTicks(20), 0.05f);

                bool allowVanillaUpdate = ShelteredMultiplayerHookService.Instance.BeginGameTimeUpdate(null);

                TestAssert.False(allowVanillaUpdate,
                    "Active multiplayer should suppress vanilla GameTime.Update even while the local UI is paused.");
                TestAssert.Near(ShelteredMultiplayerTimeSettings.RealtimeTimescale, _testTimeScale, 0.0001f,
                    "Multiplayer GameTime projection should restore realtime timescale when pause set it to zero.");
                TestAssert.Equal(2, ReadIntGameTimeField("current_day"),
                    "Projected multiplayer day should continue from WorldTick while local pause is blocked.");
                TestAssert.Equal(6, ReadIntGameTimeField("current_hour"),
                    "Projected multiplayer hour should continue from WorldTick while local pause is blocked.");
                TestAssert.Equal(0, ReadIntGameTimeField("current_minute"),
                    "Projected multiplayer minute should continue from WorldTick while local pause is blocked.");
            }
            finally
            {
                ShelteredMultiplayerHookService.Instance.EndGameTimeUpdate(null);
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ApplyGameTimePolicy(null);
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
                previous.Restore();
            }
        }

        private static void SetupGateProjectionDoesNotCrossDayBoundary()
        {
            GameTimeStaticState previous = CaptureGameTimeStaticState();
            ShelteredMultiplayerWorldClock clock = ShelteredMultiplayerWorldClock.Instance;
            long dayBoundaryTick = MultiplayerDayTicks(20);
            UseFakeTimeScale(1f);
            ShelteredMultiplayerHookService.Instance.ActivateHost(1, "time-policy-setup-gate", 20);
            try
            {
                clock.Reset("time-policy-setup-gate");
                ShelteredMultiplayerHookService.Instance.SetWorldTick(dayBoundaryTick - 1, 0.05f);
                ShelteredMultiplayerSessionCoordinator.Instance.BeginSetupPreparation(
                    ShelteredMultiplayerSetupSettings.Empty,
                    "time-policy-setup-gate");

                bool allowVanillaUpdateBefore = ShelteredMultiplayerHookService.Instance.BeginGameTimeUpdate(null);
                ShelteredMultiplayerHookService.Instance.EndGameTimeUpdate(null);
                long blockedTick = clock.AdvanceFixedDelta(1f);
                bool allowVanillaUpdateAfter = ShelteredMultiplayerHookService.Instance.BeginGameTimeUpdate(null);

                TestAssert.False(allowVanillaUpdateBefore,
                    "Active setup gate should suppress vanilla GameTime.Update before a blocked tick attempt.");
                TestAssert.False(allowVanillaUpdateAfter,
                    "Active setup gate should suppress vanilla GameTime.Update after a blocked tick attempt.");
                TestAssert.Equal(dayBoundaryTick - 1, blockedTick,
                    "Setup gate should prevent the world clock from crossing the day boundary.");
                TestAssert.Equal(1, ReadIntGameTimeField("current_day"),
                    "Projected GameTime should stay on day one while setup is gated.");
                TestAssert.Equal(5, ReadIntGameTimeField("current_hour"),
                    "Projected GameTime should stay before the 06:00 day boundary while setup is gated.");
                TestAssert.Equal(59, ReadIntGameTimeField("current_minute"),
                    "Projected GameTime should stay before the 06:00 day boundary while setup is gated.");
            }
            finally
            {
                ShelteredMultiplayerHookService.Instance.EndGameTimeUpdate(null);
                clock.Reset("time-policy-test-cleanup");
                ShelteredMultiplayerHookService.Instance.Deactivate("time-policy-test-cleanup");
                ShelteredMultiplayerTimePolicy.ApplyGameTimePolicy(null);
                ShelteredMultiplayerTimePolicy.ResetTimeScaleAccessorsForTests();
                previous.Restore();
            }
        }

        private static void UseFakeTimeScale(float initialValue)
        {
            _testTimeScale = initialValue;
            ShelteredMultiplayerTimePolicy.OverrideTimeScaleAccessorsForTests(
                delegate { return _testTimeScale; },
                delegate(float value) { _testTimeScale = value; });
        }

        private static long MultiplayerDayTicks(int tickRate)
        {
            return (long)(tickRate * ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);
        }

        private static GameTimeStaticState CaptureGameTimeStaticState()
        {
            return new GameTimeStaticState(
                ReadGameTimeField("day_seconds"),
                ReadGameTimeField("real_to_game_seconds_multiplier"),
                ReadGameTimeField("game_to_real_seconds_multiplier"),
                ReadGameTimeField("game_time"),
                ReadGameTimeField("current_minute"),
                ReadGameTimeField("current_hour"),
                ReadGameTimeField("current_day"),
                ReadGameTimeField("current_week"));
        }

        private static object ReadGameTimeField(string name)
        {
            return GetGameTimeField(name).GetValue(null);
        }

        private static int ReadIntGameTimeField(string name)
        {
            return Convert.ToInt32(ReadGameTimeField(name));
        }

        private static float ReadFloatGameTimeField(string name)
        {
            return Convert.ToSingle(ReadGameTimeField(name));
        }

        private static void WriteGameTimeField(string name, object value)
        {
            GetGameTimeField(name).SetValue(null, value);
        }

        private static FieldInfo GetGameTimeField(string name)
        {
            return typeof(GameTime).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        private sealed class GameTimeStaticState
        {
            private readonly object _daySeconds;
            private readonly object _realToGameSecondsMultiplier;
            private readonly object _gameToRealSecondsMultiplier;
            private readonly object _gameTime;
            private readonly object _currentMinute;
            private readonly object _currentHour;
            private readonly object _currentDay;
            private readonly object _currentWeek;

            public GameTimeStaticState(
                object daySeconds,
                object realToGameSecondsMultiplier,
                object gameToRealSecondsMultiplier,
                object gameTime,
                object currentMinute,
                object currentHour,
                object currentDay,
                object currentWeek)
            {
                _daySeconds = daySeconds;
                _realToGameSecondsMultiplier = realToGameSecondsMultiplier;
                _gameToRealSecondsMultiplier = gameToRealSecondsMultiplier;
                _gameTime = gameTime;
                _currentMinute = currentMinute;
                _currentHour = currentHour;
                _currentDay = currentDay;
                _currentWeek = currentWeek;
            }

            public void Restore()
            {
                WriteGameTimeField("day_seconds", _daySeconds);
                WriteGameTimeField("real_to_game_seconds_multiplier", _realToGameSecondsMultiplier);
                WriteGameTimeField("game_to_real_seconds_multiplier", _gameToRealSecondsMultiplier);
                WriteGameTimeField("game_time", _gameTime);
                WriteGameTimeField("current_minute", _currentMinute);
                WriteGameTimeField("current_hour", _currentHour);
                WriteGameTimeField("current_day", _currentDay);
                WriteGameTimeField("current_week", _currentWeek);
            }
        }
    }
}
