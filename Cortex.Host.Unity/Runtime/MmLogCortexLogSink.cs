using Cortex.Core.Diagnostics;

namespace Cortex.Host.Unity.Runtime
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
                    ModAPI.Core.MMLog.WriteWithSource(ModAPI.Core.MMLog.LogLevel.Debug, ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
                case CortexLogLevel.Warning:
                    ModAPI.Core.MMLog.WriteWithSource(ModAPI.Core.MMLog.LogLevel.Warning, ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
                case CortexLogLevel.Error:
                    ModAPI.Core.MMLog.WriteWithSource(ModAPI.Core.MMLog.LogLevel.Error, ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
                default:
                    ModAPI.Core.MMLog.WriteWithSource(ModAPI.Core.MMLog.LogLevel.Info, ModAPI.Core.MMLog.LogCategory.General, source, message);
                    break;
            }
        }
    }
}
