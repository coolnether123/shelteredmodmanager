using System;
using ModAPI.Core;

namespace ShelteredAPI.UI.Internal
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
