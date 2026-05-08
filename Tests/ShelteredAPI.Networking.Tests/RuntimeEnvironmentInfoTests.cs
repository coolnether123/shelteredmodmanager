using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ModAPI.Core;

namespace ShelteredAPI.Networking.Tests
{
    internal static class RuntimeEnvironmentInfoTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Runtime environment falls back when Unity application values are unavailable", SafeUnityProbeFallback));
            tests.Add(new TestCase("MMLog startup banner initialization does not throw outside Unity runtime", MMLogStartupDoesNotThrowOutsideUnityRuntime));
        }

        private static void SafeUnityProbeFallback()
        {
            RuntimeEnvironmentInfo.SetUnityApplicationProbeForTests(new ThrowingUnityApplicationProbe());
            try
            {
                UnityRuntimeInfo info = RuntimeEnvironmentInfo.GetUnityRuntimeInfo();

                TestAssert.Equal(RuntimeEnvironmentInfo.UnavailableValue, info.DataPath, "Unity dataPath should fall back when the probe fails.");
                TestAssert.Equal(RuntimeEnvironmentInfo.UnknownValue, info.UnityVersion, "Unity version should fall back when the probe fails.");
                TestAssert.Equal(RuntimeEnvironmentInfo.UnknownValue, info.GameVersion, "Game version should fall back when the probe fails.");
                TestAssert.Equal(RuntimeEnvironmentInfo.UnavailableValue, info.Platform, "Unity platform should fall back when the probe fails.");
            }
            finally
            {
                RuntimeEnvironmentInfo.SetUnityApplicationProbeForTests(null);
            }
        }

        private static void MMLogStartupDoesNotThrowOutsideUnityRuntime()
        {
            RuntimeEnvironmentInfo.SetUnityApplicationProbeForTests(new ThrowingUnityApplicationProbe());
            try
            {
                RuntimeHelpers.RunClassConstructor(typeof(MMLog).TypeHandle);
                MMLog.WriteInfo("MMLog startup fallback smoke test.");
            }
            finally
            {
                RuntimeEnvironmentInfo.SetUnityApplicationProbeForTests(null);
            }
        }

        private sealed class ThrowingUnityApplicationProbe : IUnityApplicationProbe
        {
            public bool TryGetStringProperty(string propertyName, out string value, out Exception error)
            {
                value = null;
                error = new InvalidOperationException("Unity application string probe is unavailable in this test.");
                return false;
            }

            public bool TryGetPlatform(out string platform, out Exception error)
            {
                platform = null;
                error = new InvalidOperationException("Unity application platform probe is unavailable in this test.");
                return false;
            }
        }
    }
}
