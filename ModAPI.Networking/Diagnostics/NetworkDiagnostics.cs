using System;
using System.Diagnostics;
using System.Reflection;

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
            Write("Debug", message);
        }

        public static void Info(string message)
        {
            Write("Info", message);
        }

        public static void Warn(string message)
        {
            Write("Warning", message);
        }

        public static void Error(string message)
        {
            Write("Error", message);
        }

        public static void Exception(Exception exception, string context)
        {
            if (exception == null)
            {
                Error(context);
                return;
            }

            try
            {
                Type logType = GetLogType();
                Type categoryType = logType.GetNestedType("LogCategory");
                object category = Enum.Parse(categoryType, "Network");
                MethodInfo method = logType.GetMethod("WriteException", new Type[] { typeof(Exception), typeof(string), categoryType });
                method.Invoke(null, new object[] { exception, context, category });
            }
            catch
            {
                Trace.WriteLine("[ModAPI.Networking] ERROR " + context + ": " + exception);
            }
        }

        private static void Write(string levelName, string message)
        {
            try
            {
                Type logType = GetLogType();
                Type levelType = logType.GetNestedType("LogLevel");
                Type categoryType = logType.GetNestedType("LogCategory");
                object level = Enum.Parse(levelType, levelName);
                object category = Enum.Parse(categoryType, "Network");
                MethodInfo method = logType.GetMethod("WriteWithSource", new Type[] { levelType, categoryType, typeof(string), typeof(string) });
                method.Invoke(null, new object[] { level, category, Source, message });
            }
            catch
            {
                Trace.WriteLine("[ModAPI.Networking] " + levelName + " " + (message ?? string.Empty));
            }
        }

        private static Type GetLogType()
        {
            Type logType = Type.GetType("ModAPI.Core.MMLog, ModAPI", false);
            if (logType == null)
                throw new InvalidOperationException("ModAPI MMLog is unavailable.");
            return logType;
        }
    }
}
