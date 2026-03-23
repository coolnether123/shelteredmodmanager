using Cortex.Core.Diagnostics;

namespace Cortex.Platform.ModAPI.Runtime
{
    public sealed class MmLogCortexLogSink : ICortexLogSink
    {
        public void Write(CortexLogEntry entry)
        {
            var message = entry != null ? entry.Message ?? string.Empty : string.Empty;
            var source = entry != null && !string.IsNullOrEmpty(entry.Source)
                ? entry.Source
                : "Cortex";
            var level = entry != null ? entry.Level : CortexLogLevel.Info;

            switch (level)
            {
                case CortexLogLevel.Debug:
                    global::ModAPI.Core.MMLog.WriteWithSource(global::ModAPI.Core.MMLog.LogLevel.Debug, global::ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
                case CortexLogLevel.Warning:
                    global::ModAPI.Core.MMLog.WriteWithSource(global::ModAPI.Core.MMLog.LogLevel.Warning, global::ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
                case CortexLogLevel.Error:
                    global::ModAPI.Core.MMLog.WriteWithSource(global::ModAPI.Core.MMLog.LogLevel.Error, global::ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
                default:
                    global::ModAPI.Core.MMLog.WriteWithSource(global::ModAPI.Core.MMLog.LogLevel.Info, global::ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
            }
        }
    }
}
