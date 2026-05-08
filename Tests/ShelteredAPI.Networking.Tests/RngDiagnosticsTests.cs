using System.Collections.Generic;
using System.IO;
using ModAPI.Core;

namespace ShelteredAPI.Networking.Tests
{
    internal static class RngDiagnosticsTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("RngDiagnostics_DigestChangesAfterDraw", DigestChangesAfterDraw));
            tests.Add(new TestCase("RngDiagnostics_DumpDoesNotCrash", DumpDoesNotCrash));
        }

        private static void DigestChangesAfterDraw()
        {
            RngDebugOptions.ResetDefaults();
            ModRandom.Initialize(123);
            string before = ModRandom.GetDeterminismDigest().ToString();
            ModRandom.Range(0, 10);
            string after = ModRandom.GetDeterminismDigest().ToString();

            TestAssert.False(before == after, "Digest should change after RNG state advances.");
        }

        private static void DumpDoesNotCrash()
        {
            RngDebugOptions.ResetDefaults();
            RngDebugOptions.Enabled = true;
            RngDebugOptions.TraceMode = RngTraceMode.Full;
            RngDebugOptions.WorldTickProvider = delegate { return 123; };
            ModRandom.Initialize(456);
            ModRandom.GetStream("MultiplayerSync.World").Range(0, 100);

            string path = Path.Combine(Path.GetTempPath(), "smm-rng-diagnostics-test.json");
            if (File.Exists(path))
                File.Delete(path);

            ModRandom.DumpRngDiagnostics("test", path);

            TestAssert.True(File.Exists(path), "RNG diagnostic dump should write a file.");
            TestAssert.True(File.ReadAllText(path).IndexOf("MultiplayerSync.World") >= 0, "Dump should include traced stream name.");
            RngDebugOptions.ResetDefaults();
        }
    }
}
