using System;

namespace ModAPI.Core
{
    public static class LoggerExtensions
    {
        /// <summary>
        /// Returns a logger that prefixes messages with a scope tag, e.g., "[Radio]".
        /// </summary>
        public static IModLogger WithScope(this IModLogger log, string scope)
        {
            return new ScopedLogger(log, scope);
        }

        /// <summary>
        /// Logs a warning only once per unique key. Uses global MMLog.WarnOnce to suppress spam.
        /// </summary>
        public static void WarnOnce(this IModLogger log, string key, string message)
        {
            try { MMLog.WarnOnce(key, message); } catch { }
            try { if (log != null) log.Warn(message); } catch { }
        }

        private class ScopedLogger : IModLogger
        {
            private readonly IModLogger _inner; private readonly string _scope;
            public ScopedLogger(IModLogger inner, string scope) { _inner = inner; _scope = string.IsNullOrEmpty(scope) ? null : scope; }
            public bool IsDebugEnabled { get { return _inner != null && _inner.IsDebugEnabled; } }
            public void Debug(string message) { if (IsDebugEnabled && _inner != null) _inner.Debug(Format(message)); }
            public void Info(string message) { if (_inner != null) _inner.Info(Format(message)); }
            public void Warn(string message) { if (_inner != null) _inner.Warn(Format(message)); }
            public void Error(string message) { if (_inner != null) _inner.Error(Format(message)); }
            private string Format(string msg)
            {
                if (string.IsNullOrEmpty(_scope)) return msg;
                return "[" + _scope + "] " + (msg ?? string.Empty);
            }
        }
    }
}