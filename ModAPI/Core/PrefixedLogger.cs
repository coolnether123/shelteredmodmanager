using System;

namespace ModAPI.Core
{
    /// <summary>
    /// Lightweight logger adapter that injects a mod-scoped source prefix into MMLog.
    /// </summary>
    internal class PrefixedLogger : IModLogger
    {
        private readonly string _prefix;
        public bool IsDebugEnabled { get; set; }

        public PrefixedLogger(string modId)
        {
            _prefix = string.IsNullOrEmpty(modId) ? "mod" : modId;
            IsDebugEnabled = true;
        }

        public void Debug(string message) { if (IsDebugEnabled) MMLog.WriteWithSource(MMLog.LogLevel.Debug, MMLog.LogCategory.Plugin, _prefix, message); }
        public void Info(string message) { MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Plugin, _prefix, message); }
        public void Warn(string message) { MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Plugin, _prefix, message); }
        public void Error(string message) { MMLog.WriteWithSource(MMLog.LogLevel.Error, MMLog.LogCategory.Plugin, _prefix, message); }
    }
}
