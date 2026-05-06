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

        public static void False(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException(message);
        }

        public static void NotNull(object value, string message)
        {
            if (value == null)
                throw new InvalidOperationException(message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
        }

        public static void BytesEqual(byte[] expected, byte[] actual, string message)
        {
            if (expected == null && actual == null)
                return;
            if (expected == null || actual == null)
                throw new InvalidOperationException(message + " One payload was null.");
            if (expected.Length != actual.Length)
                throw new InvalidOperationException(message + " Expected length: " + expected.Length + " Actual length: " + actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                    throw new InvalidOperationException(message + " Byte mismatch at " + i + ". Expected: " + expected[i] + " Actual: " + actual[i]);
            }
        }
    }
}
