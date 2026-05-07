using System;
using ShelteredAPI.Networking;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerTimePolicyTests
    {
        public static void Register(System.Collections.Generic.List<TestCase> tests)
        {
            tests.Add(new TestCase("TimePolicy_MapCompensationNeutralizesShorterMultiplayerDay", MapCompensationNeutralizesShorterMultiplayerDay));
            tests.Add(new TestCase("TimePolicy_MapSpeedModesScaleAroundCompensation", MapSpeedModesScaleAroundCompensation));
        }

        private static void MapCompensationNeutralizesShorterMultiplayerDay()
        {
            float expected = ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds / ShelteredMultiplayerTimeSettings.VanillaDaySeconds;
            TestAssert.Near(expected, ShelteredMultiplayerTimePolicy.MultiplayerMapCompensationMultiplier, 0.0001f,
                "Normal map compensation should be multiplayer day seconds divided by vanilla day seconds.");
        }

        private static void MapSpeedModesScaleAroundCompensation()
        {
            TestAssert.Near(ShelteredMultiplayerTimeSettings.SlowMapSpeedFactor, ShelteredMultiplayerTimePolicy.GetMapSpeedFactor(ShelteredMultiplayerMapSpeedMode.Slow), 0.0001f,
                "Slow map mode should use the standardized slow factor.");
            TestAssert.Near(ShelteredMultiplayerTimeSettings.NormalMapSpeedFactor, ShelteredMultiplayerTimePolicy.GetMapSpeedFactor(ShelteredMultiplayerMapSpeedMode.Normal), 0.0001f,
                "Normal map mode should leave compensated map travel unchanged.");
            TestAssert.Near(ShelteredMultiplayerTimeSettings.FastMapSpeedFactor, ShelteredMultiplayerTimePolicy.GetMapSpeedFactor(ShelteredMultiplayerMapSpeedMode.Fast), 0.0001f,
                "Fast map mode should use the standardized fast factor.");

            float compensated = ShelteredMultiplayerTimePolicy.MultiplayerMapCompensationMultiplier;
            TestAssert.Near(compensated * ShelteredMultiplayerTimeSettings.SlowMapSpeedFactor, compensated * ShelteredMultiplayerTimePolicy.GetMapSpeedFactor(ShelteredMultiplayerMapSpeedMode.Slow), 0.0001f,
                "Slow map mode should scale after compensation.");
            TestAssert.Near(compensated * ShelteredMultiplayerTimeSettings.FastMapSpeedFactor, compensated * ShelteredMultiplayerTimePolicy.GetMapSpeedFactor(ShelteredMultiplayerMapSpeedMode.Fast), 0.0001f,
                "Fast map mode should scale after compensation.");
        }
    }
}
