using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ModAPI.Core
{
    /// <summary>
    /// Simplified static logger for Modders. Automatically detects mod ID from assembly.
    /// Uses caching to avoid expensive StackTrace calls on every log.
    /// </summary>
    public static class ModLog
    {
        private static readonly Dictionary<Assembly, string> _assemblyToModIdCache = new Dictionary<Assembly, string>();
        private static readonly object _cacheLock = new object();

        public static void Info(string message) => Write(MMLog.LogLevel.Info, message);
        public static void Debug(string message) => Write(MMLog.LogLevel.Debug, message);
        public static void Warn(string message) => Write(MMLog.LogLevel.Warning, message);
        public static void Error(string message) => Write(MMLog.LogLevel.Error, message);
        
        /// <summary>
        /// Returns a static logger instance bound to the calling mod.
        /// Useful for static classes or helper methods where Context is not available.
        /// </summary>
        public static IModLogger GetLogger() 
        {
            return new PrefixedLogger(GetCallingModId());
        }

        private static void Write(MMLog.LogLevel level, string message)
        {
            string modId = GetCallingModId();
            
            // Route to internal MMLog which handles file IO and formatting
            // We pass the modId as the 'source' parameter
            switch (level)
            {
                case MMLog.LogLevel.Debug:
                    MMLog.WriteWithSource(MMLog.LogLevel.Debug, MMLog.LogCategory.Plugin, modId, message);
                    break;
                case MMLog.LogLevel.Info:
                    MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Plugin, modId, message);
                    break;
                case MMLog.LogLevel.Warning:
                    MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Plugin, modId, message);
                    break;
                case MMLog.LogLevel.Error:
                    MMLog.WriteWithSource(MMLog.LogLevel.Error, MMLog.LogCategory.Plugin, modId, message);
                    break;
            }
        }

        private static string GetCallingModId()
        {
            try
            {
                StackTrace st = new StackTrace(false);
                Assembly modAPIAssembly = typeof(ModLog).Assembly;

                for (int i = 1; i < st.FrameCount; i++)
                {
                    var frame = st.GetFrame(i);
                    var method = frame.GetMethod();
                    if (method == null) continue;

                    var type = method.DeclaringType;
                    if (type == null) continue;
                    if (type == typeof(ModLog) || type == typeof(MMLog)) continue;

                    Assembly callingAssembly = type.Assembly;
                    if (callingAssembly == modAPIAssembly) continue;

                    // Fast path: check cache
                    lock (_cacheLock)
                    {
                        if (_assemblyToModIdCache.TryGetValue(callingAssembly, out var cachedId))
                            return cachedId;
                    }

                    string modId = "UnknownMod";
                    if (ModRegistry.TryGetModByAssembly(callingAssembly, out var mod))
                    {
                        modId = mod.Id;
                    }
                    else
                    {
                        modId = callingAssembly.GetName().Name;
                    }

                    lock (_cacheLock)
                    {
                        _assemblyToModIdCache[callingAssembly] = modId;
                    }
                    return modId;
                }
            }
            catch
            {
                return "Unknown";
            }
            return "Unknown";
        }
    }
}
