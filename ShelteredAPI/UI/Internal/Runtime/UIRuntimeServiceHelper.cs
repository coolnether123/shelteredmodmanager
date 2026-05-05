using System;
using ModAPI.Core;
using ShelteredAPI.Content;
namespace ShelteredAPI.UI.Internal.Runtime{
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
