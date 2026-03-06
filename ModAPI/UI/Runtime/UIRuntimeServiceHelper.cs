using System;
using ModAPI.Core;

namespace ModAPI.Internal.UI
{
    internal static class UIRuntimeServiceHelper
    {
        internal static void Run(string operation, Action action)
        {
            try
            {
                if (action != null)
                    action();
            }
            catch (Exception ex)
            {
                MMLog.Write("ERROR in " + operation + ": " + ex);
            }
        }
    }
}
