using System;

namespace ModAPI.Networking.Tests
{
    internal static class TestAssert
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
        }

        public static void BytesEqual(byte[] expected, byte[] actual, string message)
        {
            if (expected == null || actual == null || expected.Length != actual.Length)
                throw new InvalidOperationException(message);

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                    throw new InvalidOperationException(message + " Byte mismatch at " + i + ".");
            }
        }
    }
}
