using System;

namespace ModAPI.Core
{
    public interface IMMLogRuntimeSink
    {
        void OnLogEntry(MMLog.LogEntry entry);
    }

    public sealed class MMLogRuntimeOptions
    {
        public bool CaptureWarningStackFrames;
        public bool CaptureErrorStackFrames;
        public bool CaptureFatalStackFrames;
        public int MaxCapturedFrames;

        public static MMLogRuntimeOptions Disabled()
        {
            return new MMLogRuntimeOptions
            {
                CaptureWarningStackFrames = false,
                CaptureErrorStackFrames = false,
                CaptureFatalStackFrames = false,
                MaxCapturedFrames = 0
            };
        }

        public static MMLogRuntimeOptions CortexDefaults()
        {
            return new MMLogRuntimeOptions
            {
                CaptureWarningStackFrames = false,
                CaptureErrorStackFrames = true,
                CaptureFatalStackFrames = true,
                MaxCapturedFrames = 14
            };
        }
    }
}
