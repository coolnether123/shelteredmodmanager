using System;
using Cortex.Core.Diagnostics;

namespace Cortex
{
    public static class MMLog
    {
        public static void WriteDebug(string message)
        {
            CortexLog.WriteDebug(message);
        }

        public static void WriteInfo(string message)
        {
            CortexLog.WriteInfo(message);
        }

        public static void WriteWarning(string message)
        {
            CortexLog.WriteWarning(message);
        }

        public static void WriteError(string message)
        {
            CortexLog.WriteError(message);
        }

        public static void LogOnce(string key, Action writer)
        {
            CortexLog.LogOnce(key, writer);
        }
    }
}
