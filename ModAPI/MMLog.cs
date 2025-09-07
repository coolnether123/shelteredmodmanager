using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class MMLog
{
    private static readonly object _lock = new object();
    private static string _logPath;

    // spam aggregation state
    private static string _lastMsg;
    private static int _repeatCount;
    private static DateTime _lastWriteUtc;
    private static readonly TimeSpan _repeatFlushInterval = TimeSpan.FromSeconds(5);

    static MMLog()
    {
        var baseDir = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(baseDir);
        _logPath = Path.Combine(baseDir, "mod_manager.log");
    }

    // Debug toggle: set env var MODAPI_CUSTOMSAVES_DEBUG=1 (or true/on)
    private static bool DebugEnabled()
    {
        try
        {
            var s = Environment.GetEnvironmentVariable("MODAPI_CUSTOMSAVES_DEBUG");
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim().ToLowerInvariant();
            return s == "1" || s == "true" || s == "on" || s == "yes";
        }
        catch { return false; }
    }

    /// <summary>
    /// Writes a line to mod_manager.log with a timestamp.
    /// Coalesces repeated identical messages and emits a summary: "(repeated N times)".
    /// </summary>
    public static void Write(string msg)
    {
        if (msg == null) msg = string.Empty;
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_lastMsg) && string.Equals(msg, _lastMsg, StringComparison.Ordinal))
            {
                _repeatCount++;
                // Periodically flush repetition summaries
                if ((now - _lastWriteUtc) >= _repeatFlushInterval)
                {
                    FlushRepeat_NoLock(now);
                }
                return;
            }

            // If previous message repeated, flush its summary first
            if (_repeatCount > 0)
            {
                FlushRepeat_NoLock(now);
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            try { File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
            _lastMsg = msg;
            _repeatCount = 0;
            _lastWriteUtc = now;
        }
    }

    /// <summary>
    /// Debug log. Always enabled (no env var required). Aggregation behavior matches Write().
    /// </summary>
    public static void WriteDebug(string msg)
    {
        Write("[DEBUG] " + msg);
    }

    /// <summary>
    /// Force flush of pending repetition summary (if any).
    /// </summary>
    public static void Flush()
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if (_repeatCount > 0)
                FlushRepeat_NoLock(now);
        }
    }

    private static void FlushRepeat_NoLock(DateTime nowUtc)
    {
        if (_repeatCount <= 0) return;
        var summary = $"[{DateTime.Now:HH:mm:ss}] (previous message repeated {_repeatCount} times)";
        try { File.AppendAllText(_logPath, summary + Environment.NewLine, Encoding.UTF8); } catch { }
        _repeatCount = 0;
        _lastWriteUtc = nowUtc;
    }
}
