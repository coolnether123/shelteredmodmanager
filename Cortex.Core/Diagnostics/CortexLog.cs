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

    public sealed class CortexLogEntry
    {
        public CortexLogLevel Level;
        public string Source;
        public string Message;
    }

    public interface ICortexLogSink
    {
        void Write(CortexLogEntry entry);
    }

    public sealed class CortexLogger
    {
        private readonly string _source;

        internal CortexLogger(string source)
        {
            _source = source ?? string.Empty;
        }

        public void WriteDebug(string message)
        {
            CortexLog.WriteDebug(_source, message);
        }

        public void WriteInfo(string message)
        {
            CortexLog.WriteInfo(_source, message);
        }

        public void WriteWarning(string message)
        {
            CortexLog.WriteWarning(_source, message);
        }

        public void WriteError(string message)
        {
            CortexLog.WriteError(_source, message);
        }
    }

    public static class CortexLog
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<string> OnceKeys = new HashSet<string>(StringComparer.Ordinal);
        private static ICortexLogSink _sink = new NullCortexLogSink();
        private static readonly Dictionary<string, CortexLogger> Loggers = new Dictionary<string, CortexLogger>(StringComparer.Ordinal);

        public static void Configure(ICortexLogSink sink)
        {
            lock (Sync)
            {
                _sink = sink ?? new NullCortexLogSink();
            }
        }

        public static CortexLogger ForSource(string source)
        {
            var normalizedSource = source ?? string.Empty;
            lock (Sync)
            {
                CortexLogger logger;
                if (!Loggers.TryGetValue(normalizedSource, out logger))
                {
                    logger = new CortexLogger(normalizedSource);
                    Loggers[normalizedSource] = logger;
                }

                return logger;
            }
        }

        public static void WriteDebug(string message)
        {
            Write(CortexLogLevel.Debug, null, message);
        }

        public static void WriteDebug(string source, string message)
        {
            Write(CortexLogLevel.Debug, source, message);
        }

        public static void WriteInfo(string message)
        {
            Write(CortexLogLevel.Info, null, message);
        }

        public static void WriteInfo(string source, string message)
        {
            Write(CortexLogLevel.Info, source, message);
        }

        public static void WriteWarning(string message)
        {
            Write(CortexLogLevel.Warning, null, message);
        }

        public static void WriteWarning(string source, string message)
        {
            Write(CortexLogLevel.Warning, source, message);
        }

        public static void WriteError(string message)
        {
            Write(CortexLogLevel.Error, null, message);
        }

        public static void WriteError(string source, string message)
        {
            Write(CortexLogLevel.Error, source, message);
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

        private static void Write(CortexLogLevel level, string source, string message)
        {
            string normalizedSource;
            string normalizedMessage;
            NormalizeEntry(source, message, out normalizedSource, out normalizedMessage);

            ICortexLogSink sink;
            lock (Sync)
            {
                sink = _sink;
            }

            sink.Write(new CortexLogEntry
            {
                Level = level,
                Source = normalizedSource,
                Message = normalizedMessage
            });
        }

        private static void NormalizeEntry(string source, string message, out string normalizedSource, out string normalizedMessage)
        {
            normalizedSource = source ?? string.Empty;
            normalizedMessage = message ?? string.Empty;

            if (!string.IsNullOrEmpty(normalizedSource))
            {
                return;
            }

            if (string.IsNullOrEmpty(normalizedMessage) || normalizedMessage[0] != '[')
            {
                return;
            }

            var endIndex = normalizedMessage.IndexOf(']');
            if (endIndex <= 1)
            {
                return;
            }

            normalizedSource = normalizedMessage.Substring(1, endIndex - 1).Trim();
            normalizedMessage = endIndex + 1 < normalizedMessage.Length
                ? normalizedMessage.Substring(endIndex + 1).TrimStart()
                : string.Empty;
        }

        private sealed class NullCortexLogSink : ICortexLogSink
        {
            public void Write(CortexLogEntry entry)
            {
            }
        }
    }
}
