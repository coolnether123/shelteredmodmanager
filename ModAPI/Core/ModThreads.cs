using System;
using System.Threading;

namespace ModAPI.Core
{
    public static class ModThreads
    {
        public static void RunAsync(Action action)
        {
            if (action == null) return;
            ThreadPool.QueueUserWorkItem(state => {
                try {
                    action();
                } catch (Exception ex) {
                    MMLog.WriteError("[ModThreads] Async Exception: " + ex);
                }
            });
        }
    }
}
