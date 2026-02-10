using System;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    public static class MemoryGovernor
    {
        private const long SOFT_LIMIT_32BIT = 150L * 1024L * 1024L;
        private const long SOFT_LIMIT_64BIT = 512L * 1024L * 1024L;

        public static long SoftLimit
        {
            get { return IntPtr.Size == 4 ? SOFT_LIMIT_32BIT : SOFT_LIMIT_64BIT; }
        }

        public static bool CheckMemoryPressure()
        {
            var current = GC.GetTotalMemory(false);
            if (current > SoftLimit)
            {
                MMLog.WriteWarning("Memory pressure: " + (current / 1024L / 1024L) + "MB / " + (SoftLimit / 1024L / 1024L) + "MB");
                GC.Collect();
                return true;
            }

            return false;
        }
    }
}
