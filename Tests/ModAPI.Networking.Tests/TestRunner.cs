using System;
using System.Collections.Generic;

namespace ModAPI.Networking.Tests
{
    internal static class TestRunner
    {
        public static int Run(List<TestCase> tests)
        {
            int failed = 0;
            for (int i = 0; i < tests.Count; i++)
            {
                TestCase test = tests[i];
                try
                {
                    test.Body();
                    Console.WriteLine("[PASS] " + test.Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("[FAIL] " + test.Name);
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine();
            Console.WriteLine("Networking tests: " + (tests.Count - failed) + " passed, " + failed + " failed.");
            return failed == 0 ? 0 : 1;
        }
    }
}
