using System;
using ModAPI.Core;

namespace ModAPI.Networking.Diagnostics
{
    /// <summary>
    /// Small logging bridge so networking code has one source and one category.
    /// </summary>
    public static class NetworkDiagnostics
    {
        private const string Source = "ModAPI.Networking";

        public static void Debug(string message)
        {
            MMLog.WriteWithSource(MMLog.LogLevel.Debug, MMLog.LogCategory.Network, Source, message);
        }

        public static void Info(string message)
        {
            MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, Source, message);
        }

        public static void Warn(string message)
        {
            MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Network, Source, message);
        }

        public static void Error(string message)
        {
            MMLog.WriteWithSource(MMLog.LogLevel.Error, MMLog.LogCategory.Network, Source, message);
        }

        public static void Exception(Exception exception, string context)
        {
            if (exception == null)
            {
                Error(context);
                return;
            }

            MMLog.WriteException(exception, context, MMLog.LogCategory.Network);
        }
    }
}
