using System;
using System.Threading;

namespace ModAPI.Core
{
    internal static class CrashCorridorTracer
    {
        private static int _stepCounter;
        private static string _lastStep = string.Empty;
        private static DateTime _lastStepAtUtc;

        public static string LastStep
        {
            get
            {
                lock (typeof(CrashCorridorTracer))
                {
                    return _lastStep;
                }
            }
        }

        public static DateTime LastStepAtUtc
        {
            get
            {
                lock (typeof(CrashCorridorTracer))
                {
                    return _lastStepAtUtc;
                }
            }
        }

        public static void Mark(string step, string detail = null)
        {
            var index = Interlocked.Increment(ref _stepCounter);
            var now = DateTime.UtcNow;
            var text = "[CorridorStep " + index + "] " + (step ?? "<null>");
            if (!string.IsNullOrEmpty(detail))
            {
                text += " | " + detail;
            }

            lock (typeof(CrashCorridorTracer))
            {
                _lastStep = text;
                _lastStepAtUtc = now;
            }

            MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.General, "CrashCorridor", text);
            MMLog.Flush();
        }
    }
}
