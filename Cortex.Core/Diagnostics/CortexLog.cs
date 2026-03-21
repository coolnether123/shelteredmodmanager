using System;
using System.Collections.Generic;

namespace Cortex.Core.Diagnostics
{
    public enum CortexLogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public interface ICortexLogSink
    {
        void Write(CortexLogLevel level, string message);
    }

    public static class CortexLog
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<string> OnceKeys = new HashSet<string>(StringComparer.Ordinal);
        private static ICortexLogSink _sink = new NullCortexLogSink();

        public static void Configure(ICortexLogSink sink)
        {
            lock (Sync)
            {
                _sink = sink ?? new NullCortexLogSink();
            }
        }

        public static void WriteDebug(string message)
        {
            Write(CortexLogLevel.Debug, message);
        }

        public static void WriteInfo(string message)
        {
            Write(CortexLogLevel.Info, message);
        }

        public static void WriteWarning(string message)
        {
            Write(CortexLogLevel.Warning, message);
        }

        public static void WriteError(string message)
        {
            Write(CortexLogLevel.Error, message);
        }

        public static void LogOnce(string key, Action writer)
        {
            if (writer == null)
            {
                return;
            }

            var shouldWrite = false;
            lock (Sync)
            {
                if (string.IsNullOrEmpty(key) || OnceKeys.Add(key))
                {
                    shouldWrite = true;
                }
            }

            if (shouldWrite)
            {
                writer();
            }
        }

        private static void Write(CortexLogLevel level, string message)
        {
            ICortexLogSink sink;
            lock (Sync)
            {
                sink = _sink;
            }

            sink.Write(level, message ?? string.Empty);
        }

        private sealed class NullCortexLogSink : ICortexLogSink
        {
            public void Write(CortexLogLevel level, string message)
            {
            }
        }
    }
}
