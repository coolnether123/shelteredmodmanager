using Cortex.Core.Diagnostics;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class MmLogCortexLogSink : ICortexLogSink
    {
        public void Write(CortexLogLevel level, string message)
        {
            switch (level)
            {
                case CortexLogLevel.Debug:
                    ModAPI.Core.MMLog.WriteDebug(message);
                    break;
                case CortexLogLevel.Warning:
                    ModAPI.Core.MMLog.WriteWarning(message);
                    break;
                case CortexLogLevel.Error:
                    ModAPI.Core.MMLog.WriteError(message);
                    break;
                default:
                    ModAPI.Core.MMLog.WriteInfo(message);
                    break;
            }
        }
    }
}
