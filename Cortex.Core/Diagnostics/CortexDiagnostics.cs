using System;
using System.Collections.Generic;

namespace Cortex.Core.Diagnostics
{
    public interface ICortexDiagnosticConfiguration
    {
        bool IsEnabled(string channel, CortexLogLevel level);
    }

    public sealed class CortexDiagnosticLogger
    {
        private readonly string _channel;
        private readonly string _source;

        internal CortexDiagnosticLogger(string channel, string source)
        {
            _channel = channel ?? string.Empty;
            _source = source ?? string.Empty;
        }

        public string Channel
        {
            get { return _channel; }
        }

        public string Source
        {
            get { return _source; }
        }

        public bool IsEnabled(CortexLogLevel level)
        {
            return CortexDiagnostics.IsEnabled(_channel, level);
        }

        public void WriteDebug(string message)
        {
            CortexDiagnostics.Write(_channel, CortexLogLevel.Debug, _source, message);
        }

        public void WriteInfo(string message)
        {
            CortexDiagnostics.Write(_channel, CortexLogLevel.Info, _source, message);
        }

        public void WriteWarning(string message)
        {
            CortexDiagnostics.Write(_channel, CortexLogLevel.Warning, _source, message);
        }

        public void WriteError(string message)
        {
            CortexDiagnostics.Write(_channel, CortexLogLevel.Error, _source, message);
        }
    }

    public static class CortexDiagnostics
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, CortexDiagnosticLogger> Loggers = new Dictionary<string, CortexDiagnosticLogger>(StringComparer.Ordinal);
        private static ICortexDiagnosticConfiguration _configuration = DisabledCortexDiagnosticConfiguration.Instance;

        public static void Configure(ICortexDiagnosticConfiguration configuration)
        {
            lock (Sync)
            {
                _configuration = configuration ?? DisabledCortexDiagnosticConfiguration.Instance;
            }
        }

        public static CortexDiagnosticLogger ForChannel(string channel, string source)
        {
            var normalizedChannel = channel ?? string.Empty;
            var normalizedSource = source ?? string.Empty;
            var key = normalizedChannel + "|" + normalizedSource;
            lock (Sync)
            {
                CortexDiagnosticLogger logger;
                if (!Loggers.TryGetValue(key, out logger))
                {
                    logger = new CortexDiagnosticLogger(normalizedChannel, normalizedSource);
                    Loggers[key] = logger;
                }

                return logger;
            }
        }

        public static bool IsEnabled(string channel, CortexLogLevel level)
        {
            ICortexDiagnosticConfiguration configuration;
            lock (Sync)
            {
                configuration = _configuration;
            }

            return configuration != null && configuration.IsEnabled(channel ?? string.Empty, level);
        }

        public static void Write(string channel, CortexLogLevel level, string source, string message)
        {
            if (!IsEnabled(channel, level))
            {
                return;
            }

            switch (level)
            {
                case CortexLogLevel.Debug:
                    CortexLog.WriteDebug(source, message);
                    break;
                case CortexLogLevel.Warning:
                    CortexLog.WriteWarning(source, message);
                    break;
                case CortexLogLevel.Error:
                    CortexLog.WriteError(source, message);
                    break;
                default:
                    CortexLog.WriteInfo(source, message);
                    break;
            }
        }

        private sealed class DisabledCortexDiagnosticConfiguration : ICortexDiagnosticConfiguration
        {
            public static readonly DisabledCortexDiagnosticConfiguration Instance = new DisabledCortexDiagnosticConfiguration();

            public bool IsEnabled(string channel, CortexLogLevel level)
            {
                return false;
            }
        }
    }
}
