using System.Collections.Generic;

namespace ModAPI.Networking.Tests
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            List<TestCase> tests = new List<TestCase>();
            SaveResumeHookTests.Register(tests);
            CoreNetworkingExpansionTests.Register(tests);
            EventSyncTests.Register(tests);
            SnapshotTransferTests.Register(tests);
            ReliabilityTests.Register(tests);
            AddressingTests.Register(tests);
            DiscoveryTests.Register(tests);
            DiagnosticsTests.Register(tests);
            return TestRunner.Run(tests);
        }
    }
}
