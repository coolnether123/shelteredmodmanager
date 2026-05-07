using System.Collections.Generic;

namespace ShelteredAPI.Networking.Tests
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            List<TestCase> tests = new List<TestCase>();
            ShelteredTradeEventTests.Register(tests);
            return TestRunner.Run("ShelteredAPI networking tests", tests);
        }
    }
}
