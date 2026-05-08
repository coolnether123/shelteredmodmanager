using System;

namespace ModAPI.Core
{
    /// <summary>
    /// Convenience extensions for <see cref="IModLogger"/>.
    /// These keep common logging patterns available without exposing the global logger to plugin code.
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Returns a logger that prefixes messages with a scope tag, for example <c>[Radio]</c>.
        /// </summary>
        public static IModLogger WithScope(this IModLogger log, string scope)
        {
            return new ScopedLogger(log, scope);
        }

        /// <summary>
        /// Logs a warning once for a unique key and also forwards the message through the plugin logger.
        /// Use this for recoverable repeated failures that would otherwise flood support logs.
        /// </summary>
        public static void WarnOnce(this IModLogger log, string key, string message)
        {
            try { MMLog.WarnOnce(key, message); }
            catch
            {
                // GuardrailAllow: SilentCatch - this extension is a best-effort warning mirror.
            }

            try { if (log != null) log.Warn(message); }
            catch
            {
                // GuardrailAllow: SilentCatch - plugin logger failures must not break caller control flow.
            }
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
