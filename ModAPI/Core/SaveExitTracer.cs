using System;
using System.Threading;

namespace ModAPI.Core
{
    internal static class SaveExitTracker
    {
        private static int _stepCounter;
        private static string _lastStep = string.Empty;
        private static DateTime _lastStepAtUtc;

        public static string LastStep
        {
            get
            {
                lock (typeof(SaveExitTracker))
                {
                    return _lastStep;
                }
            }
        }

        public static DateTime LastStepAtUtc
        {
            get
            {
                lock (typeof(SaveExitTracker))
                {
                    return _lastStepAtUtc;
                }
            }
        }

        public static void Mark(string step, string detail = null)
        {
            var index = Interlocked.Increment(ref _stepCounter);
            var now = DateTime.UtcNow;
            var text = "[SaveExitCheckpoint " + index + "] " + (step ?? "<null>");
            if (!string.IsNullOrEmpty(detail))
            {
                text += " | " + detail;
            }

            lock (typeof(SaveExitTracker))
            {
                _lastStep = text;
                _lastStepAtUtc = now;
            }

            MMLog.WriteWithSource(MMLog.LogLevel.Debug, MMLog.LogCategory.General, "SaveExitCheckpoint", text);
        }
    }
}
