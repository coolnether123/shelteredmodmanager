using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Enhanced logging system for the Sheltered Mod Loader.
/// Provides comprehensive debugging capabilities including:
/// - Multi-level logging (Debug, Info, Warning, Error, Fatal)
/// - Detailed exception logging with stack traces
/// - Plugin lifecycle tracking with performance timing
/// - Dependency resolution debugging
/// - Memory usage monitoring
/// - Assembly loading diagnostics
/// - Automatic log rotation and filtering
/// </summary>
public static class MMLog
{
    #region Log Levels
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Fatal = 4
    }

    public enum LogCategory
    {
        General,
        Loader,
        Plugin,
        Assembly,
        Dependency,
        Configuration,
        Performance,
        Memory,
        Scene,
        UI,
        Network,
        IO
    }
    #endregion

    #region Private Fields
    private static readonly object _lock = new object();
    private static string _logPath;
    // Archived log files will use timestamped filenames
    private static LogLevel _minLevel = LogLevel.Info;
    private static readonly HashSet<LogCategory> _enabledCategories = new HashSet<LogCategory>();
    private static readonly Dictionary<string, int> _modLogCounts = new Dictionary<string, int>();
    private static long _logFileSize = 0;
    private static readonly long _maxLogFileSize = 10 * 1024 * 1024; // 10MB

    // Spam aggregation state
    private static string _lastMsg;
    private static int _repeatCount;
    private static DateTime _lastWriteUtc;
    private static DateTime _repeatStartTime;
    private static readonly TimeSpan _repeatFlushInterval = TimeSpan.FromSeconds(5);

    // Performance tracking
    private static readonly Dictionary<string, Stopwatch> _activeTimers = new Dictionary<string, Stopwatch>();
    private static readonly Dictionary<string, List<long>> _performanceHistory = new Dictionary<string, List<long>>();

    // Memory tracking
    private static long _lastMemoryUsage = 0;
    private static readonly List<long> _memoryHistory = new List<long>();

    // WarnOnce keys
    private static readonly HashSet<string> _warnOnceKeys = new HashSet<string>(StringComparer.Ordinal);
    #endregion

    #region Initialization
    static MMLog()
    {
        var baseDir = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(baseDir);
        _logPath = Path.Combine(baseDir, "mod_manager.log");
        try { if (File.Exists(_logPath)) _logFileSize = new FileInfo(_logPath).Length; } catch { _logFileSize = 0; }

        // Initialize with default categories
        _enabledCategories.Add(LogCategory.General);
        _enabledCategories.Add(LogCategory.Loader);
        _enabledCategories.Add(LogCategory.Plugin);
        _enabledCategories.Add(LogCategory.Assembly);

        InitializeFromManagerIni();
        WriteStartupBanner();
    }

    // Read Manager-authored settings from mod_manager.ini in the game directory.
    private static void InitializeFromManagerIni()
    {
        try
        {
            var ini = System.IO.Path.Combine(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SMM"), "mod_manager.ini");
            string devMode = null, logLevel = null, logCategories = null;
            if (File.Exists(ini))
            {
                foreach (var raw in File.ReadAllLines(ini))
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    var line = raw.Trim();
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;
                    var idx = line.IndexOf('='); if (idx <= 0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx + 1).Trim();
                    if (key.Equals("DevMode", StringComparison.OrdinalIgnoreCase)) devMode = val;
                    else if (key.Equals("LogLevel", StringComparison.OrdinalIgnoreCase)) logLevel = val;
                    else if (key.Equals("LogCategories", StringComparison.OrdinalIgnoreCase)) logCategories = val;
                }
            }

            if (!string.IsNullOrEmpty(devMode) && devMode.ToLower() == "true")
            {
                _minLevel = LogLevel.Debug;
                _enabledCategories.Clear();
                foreach (LogCategory cat in Enum.GetValues(typeof(LogCategory)))
                    _enabledCategories.Add(cat);
            }

            var level = TryParseLogLevel(logLevel);
            if (level.HasValue) _minLevel = level.Value;

            if (!string.IsNullOrEmpty(logCategories))
            {
                _enabledCategories.Clear();
                var parts = (logCategories ?? string.Empty).Split(',');
                foreach (var p in parts)
                {
                    var c = TryParseLogCategory(p.Trim());
                    if (c.HasValue) _enabledCategories.Add(c.Value);
                }
            }
        }
        catch (Exception ex)
        {
            WriteInternal(LogLevel.Error, LogCategory.General, "System",
                $"Failed to initialize logging from environment: {ex.Message}");
        }
    }

    private static void WriteStartupBanner()
    {
        var banner = new StringBuilder();
        banner.AppendLine("================================================================================");
        banner.AppendLine("                    SHELTERED MOD LOADER - DEBUG LOG");
        banner.AppendLine("================================================================================");
        banner.AppendLine($"Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        banner.AppendLine($"Game Version: {Application.version}");
        banner.AppendLine($"Unity Version: {Application.unityVersion}");
        banner.AppendLine($"Platform: {Application.platform}");
        banner.AppendLine($"Data Path: {Application.dataPath}");
        banner.AppendLine($"Persistent Data: {Application.persistentDataPath}");
        banner.AppendLine($"Log Level: {_minLevel}");
        banner.AppendLine($"Active Categories: {string.Join(", ", _enabledCategories.Select(x => x.ToString()).ToArray())}");
        banner.AppendLine($"Process ID: {Process.GetCurrentProcess().Id}");
        banner.AppendLine($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");
        banner.AppendLine("================================================================================");

        try { File.AppendAllText(_logPath, banner.ToString(), Encoding.UTF8); }
        catch { /* Swallow startup logging errors */ }
    }

    // .NET 3.5 compatible enum parsing
    private static LogLevel? TryParseLogLevel(string value)
    {
        try
        {
            return (LogLevel)Enum.Parse(typeof(LogLevel), value, true);
        }
        catch
        {
            return null;
        }
    }

    private static LogCategory? TryParseLogCategory(string value)
    {
        try
        {
            return (LogCategory)Enum.Parse(typeof(LogCategory), value, true);
        }
        catch
        {
            return null;
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Basic logging method for backwards compatibility
    /// </summary>
    public static void Write(string message)
    {
        WriteInternal(LogLevel.Info, LogCategory.General, GetCallerInfo(), message);
    }

    /// <summary>
    /// Debug logging with category support
    /// </summary>
    public static void WriteDebug(string message, LogCategory category = LogCategory.General)
    {
        WriteInternal(LogLevel.Debug, category, GetCallerInfo(), message);
    }

    /// <summary>
    /// Info level logging
    /// </summary>
    public static void WriteInfo(string message, LogCategory category = LogCategory.General)
    {
        WriteInternal(LogLevel.Info, category, GetCallerInfo(), message);
    }

    /// <summary>
    /// Warning level logging
    /// </summary>
    public static void WriteWarning(string message, LogCategory category = LogCategory.General)
    {
        WriteInternal(LogLevel.Warning, category, GetCallerInfo(), message);
    }

    /// <summary>
    /// Error level logging
    /// </summary>
    public static void WriteError(string message, LogCategory category = LogCategory.General)
    {
        WriteInternal(LogLevel.Error, category, GetCallerInfo(), message);
    }

    /// <summary>
    /// Fatal error logging
    /// </summary>
    public static void WriteFatal(string message, LogCategory category = LogCategory.General)
    {
        WriteInternal(LogLevel.Fatal, category, GetCallerInfo(), message);
    }

    /// <summary>
    /// Log a warning once per unique key. Useful to avoid spamming logs.
    /// </summary>
    public static void WarnOnce(string key, string message)
    {
        if (string.IsNullOrEmpty(key)) key = "<empty>";
        lock (_lock)
        {
            if (_warnOnceKeys.Contains(key)) return;
            _warnOnceKeys.Add(key);
        }
        WriteInternal(LogLevel.Warning, LogCategory.General, "WarnOnce", message);
    }

    /// <summary>
    /// Enhanced exception logging with full details
    /// </summary>
    public static void WriteException(Exception ex, string context = "", LogCategory category = LogCategory.General)
    {
        if (ex == null) return;

        var message = new StringBuilder();
        message.AppendLine($"EXCEPTION in {context}:");
        message.AppendLine($"Type: {ex.GetType().FullName}");
        message.AppendLine($"Message: {ex.Message}");
        message.AppendLine($"Source: {ex.Source}");

        if (ex.TargetSite != null)
        {
            message.AppendLine($"Target Method: {ex.TargetSite.DeclaringType?.FullName}.{ex.TargetSite.Name}");
        }

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            message.AppendLine("Stack Trace:");
            message.AppendLine(ex.StackTrace);
        }

        // Log inner exceptions recursively
        var innerEx = ex.InnerException;
        int depth = 1;
        while (innerEx != null && depth < 10) // Prevent infinite recursion
        {
            message.AppendLine($"--- Inner Exception #{depth} ---");
            message.AppendLine($"Type: {innerEx.GetType().FullName}");
            message.AppendLine($"Message: {innerEx.Message}");
            if (!string.IsNullOrEmpty(innerEx.StackTrace))
            {
                message.AppendLine("Stack Trace:");
                message.AppendLine(innerEx.StackTrace);
            }
            innerEx = innerEx.InnerException;
            depth++;
        }

        WriteInternal(LogLevel.Error, category, GetCallerInfo(), message.ToString());
    }

    /// <summary>
    /// Plugin lifecycle logging with detailed context
    /// </summary>
    public static void WritePluginLifecycle(string pluginName, string phase, string details = "", bool isError = false)
    {
        var level = isError ? LogLevel.Error : LogLevel.Info;
        var message = $"[PLUGIN-{phase.ToUpper()}] {pluginName}";
        if (!string.IsNullOrEmpty(details))
            message += $": {details}";

        WriteInternal(level, LogCategory.Plugin, "PluginManager", message);
    }

    /// <summary>
    /// Assembly loading diagnostics
    /// </summary>
    public static void WriteAssemblyInfo(Assembly assembly, string operation, string details = "")
    {
        var message = new StringBuilder();
        message.AppendLine($"[ASSEMBLY-{operation.ToUpper()}]");
        message.AppendLine($"Name: {assembly.GetName().Name}");
        message.AppendLine($"Version: {assembly.GetName().Version}");
        message.AppendLine($"Location: {SafeGetLocation(assembly)}");
        message.AppendLine($"GAC: {assembly.GlobalAssemblyCache}");
        message.AppendLine($"Is Dynamic: {CheckIsDynamic(assembly)}");

        if (!string.IsNullOrEmpty(details))
            message.AppendLine($"Details: {details}");

        WriteInternal(LogLevel.Debug, LogCategory.Assembly, "ModDiscovery", message.ToString());
    }

    /// <summary>
    /// Dependency resolution logging
    /// </summary>
    public static void WriteDependencyInfo(string modId, string operation, string details)
    {
        var message = $"[DEPENDENCY-{operation.ToUpper()}] {modId}: {details}";
        WriteInternal(LogLevel.Debug, LogCategory.Dependency, "LoadOrderResolver", message);
    }

    /// <summary>
    /// Performance timing helpers
    /// </summary>
    public static void StartTimer(string operationName)
    {
        lock (_lock)
        {
            if (!_activeTimers.ContainsKey(operationName))
                _activeTimers[operationName] = new Stopwatch();

            // .NET 3.5 compatible restart
            var timer = _activeTimers[operationName];
            timer.Stop();
            timer.Reset();
            timer.Start();
        }
    }

    public static void StopTimer(string operationName, string details = "")
    {
        lock (_lock)
        {
            if (!_activeTimers.ContainsKey(operationName)) return;

            var timer = _activeTimers[operationName];
            timer.Stop();

            var elapsed = timer.ElapsedMilliseconds;
            RecordPerformance(operationName, elapsed);

            var message = $"[PERFORMANCE] {operationName}: {elapsed}ms";
            if (!string.IsNullOrEmpty(details))
                message += $" ({details})";

            WriteInternal(LogLevel.Debug, LogCategory.Performance, "Timer", message);
        }
    }

    // Removed WriteMemoryInfo

    /// <summary>
    /// Scene transition logging
    /// </summary>
    public static void WriteSceneInfo(string sceneName, string operation)
    {
        var message = $"[SCENE-{operation.ToUpper()}] {sceneName}";
        WriteInternal(LogLevel.Info, LogCategory.Scene, "SceneManager", message);
    }

    /// <summary>
    /// Configuration change logging
    /// </summary>
    public static void WriteConfigChange(string modId, string key, string oldValue, string newValue)
    {
        var message = $"[CONFIG-CHANGE] {modId}: {key} = '{oldValue}' -> '{newValue}'";
        WriteInternal(LogLevel.Debug, LogCategory.Configuration, "ModSettings", message);
    }

    /// <summary>
    /// Force flush pending logs and perform maintenance
    /// </summary>
    public static void Flush()
    {
        lock (_lock)
        {
            FlushRepeat_NoLock(DateTime.UtcNow);
            CheckLogRotation();
        }
    }

    /// <summary>
    /// Set minimum log level dynamically
    /// </summary>
    public static void SetLogLevel(LogLevel level)
    {
        _minLevel = level;
        WriteInternal(LogLevel.Info, LogCategory.General, "Logger", $"Log level changed to: {level}");
    }

    // Removed SetCategoryEnabled: categories controlled by Manager via INI

    // Removed WriteSystemReport
    #endregion

    #region Internal Methods
    private static void WriteInternal(LogLevel level, LogCategory category, string source, string message)
    {
        if (level < _minLevel) return;
        if (message == null) message = string.Empty;

        var now = DateTime.Now;
        var comparableMessage = FormatLogMessage(level, category, source, message, now, false);

        lock (_lock)
        {
            if (!_enabledCategories.Contains(category) && level < LogLevel.Error)
                return;
            // Handle spam aggregation
            if (!string.IsNullOrEmpty(_lastMsg) && string.Equals(comparableMessage, _lastMsg, StringComparison.Ordinal))
            {
                if (_repeatCount == 0)
                {
                    _repeatStartTime = _lastWriteUtc;
                }
                _repeatCount++;
                if ((now - _repeatStartTime) >= _repeatFlushInterval)
                {
                    FlushRepeat_NoLock(now);
                }
                return;
            }

            // Flush any pending repeats
            if (_repeatCount > 0)
            {
                FlushRepeat_NoLock(now);
            }

            // Write the new message
            var formattedMessage = FormatLogMessage(level, category, source, message, now);
            WriteToFile(formattedMessage);

            // Track statistics
            TrackModLogCount(source);

            _lastMsg = comparableMessage;
            _repeatCount = 0;
            _lastWriteUtc = now;
        }
    }

    private static string FormatLogMessage(LogLevel level, LogCategory category, string source, string message, DateTime timestamp, bool includeTimestamp = true)
    {
        var sb = new StringBuilder();
        if (includeTimestamp)
            sb.Append($"[{timestamp:HH:mm:ss.fff}] ");
        sb.Append($"[{level.ToString().ToUpper().PadRight(5)}] ");
        sb.Append($"[{category.ToString().ToUpper().PadRight(8)}] ");
        sb.Append($"[{(source ?? "UNKNOWN").PadRight(12)}] ");
        sb.Append(message);
        return sb.ToString();
    }

    private static void WriteToFile(string message)
    {
        try
        {
            CheckLogRotation();
            File.AppendAllText(_logPath, message + Environment.NewLine, Encoding.UTF8);
            _logFileSize += Encoding.UTF8.GetByteCount(message + Environment.NewLine);
        }
        catch
        {
            // Swallow file I/O errors to prevent cascading failures
        }
    }

    private static void FlushRepeat_NoLock(DateTime nowUtc)
    {
        if (_repeatCount <= 0) return;

        var startTime = _repeatStartTime.ToLocalTime().ToString("HH:mm:ss.fff");
        var endTime = nowUtc.ToLocalTime().ToString("HH:mm:ss.fff");

        var summary = FormatLogMessage(LogLevel.Info, LogCategory.General, "Logger",
            ($"(Previous message repeated {_repeatCount} times from {startTime} to {endTime})"), nowUtc, true);

        WriteToFile(summary);
        _repeatCount = 0;
        _lastWriteUtc = nowUtc;
    }

    private static void CheckLogRotation()
    {
        if (_logFileSize > _maxLogFileSize)
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    var dir = Path.GetDirectoryName(_logPath);
                    var archive = Path.Combine(dir ?? string.Empty, $"mod_manager_{ts}.log");
                    File.Move(_logPath, archive);
                }

                _logFileSize = 0;
                WriteInternal(LogLevel.Info, LogCategory.General, "Logger", "Log file rotated");
            }
            catch (Exception ex)
            {
                // If rotation fails, just truncate the current log
                try
                {
                    File.WriteAllText(_logPath, $"[LOG ROTATION FAILED: {ex.Message}]\n");
                    _logFileSize = 0;
                }
                catch { /* Final fallback - give up on logging */ }
            }
        }
    }

    private static void TrackModLogCount(string source)
    {
        if (string.IsNullOrEmpty(source)) return;

        if (!_modLogCounts.ContainsKey(source))
            _modLogCounts[source] = 0;

        _modLogCounts[source]++;
    }

    private static void RecordPerformance(string operation, long milliseconds)
    {
        if (!_performanceHistory.ContainsKey(operation))
            _performanceHistory[operation] = new List<long>();

        var history = _performanceHistory[operation];
        history.Add(milliseconds);

        // Keep only recent samples
        if (history.Count > 50)
            history.RemoveAt(0);
    }

    private static string GetCallerInfo()
    {
        try
        {
            var frame = new StackFrame(2, false);
            var method = frame.GetMethod();
            if (method != null && method.DeclaringType != null)
            {
                return method.DeclaringType.Name;
            }
        }
        catch { }
        return "Unknown";
    }

    private static string SafeGetLocation(Assembly assembly)
    {
        try { return assembly.Location; }
        catch { return "Dynamic/Unknown"; }
    }

    // .NET 3.5 compatible IsDynamic check
    private static bool CheckIsDynamic(Assembly assembly)
    {
        try
        {
            // In .NET 3.5, we can't directly check IsDynamic
            // Instead, check if Location throws or returns empty
            var location = assembly.Location;
            return string.IsNullOrEmpty(location);
        }
        catch
        {
            return true; // Assume dynamic if we can't get location
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return string.Format("{0:n1} {1}", number, suffixes[counter]);
    }
    #endregion

    #region Debug Utilities
    /// <summary>
    /// Enhanced plugin error logging specifically requested by user
    /// </summary>
    public static void WritePluginError(string pluginName, string phase, Exception ex)
    {
        var message = new StringBuilder();
        message.AppendLine($"[PLUGIN-ERROR] {pluginName} failed during {phase}");

        if (ex != null)
        {
            message.AppendLine($"Exception Type: {ex.GetType().FullName}");
            message.AppendLine($"Exception Message: {ex.Message}");

            if (ex.TargetSite != null)
            {
                message.AppendLine($"Failed Method: {ex.TargetSite.DeclaringType?.FullName}.{ex.TargetSite.Name}");
            }

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                message.AppendLine("Stack Trace:");
                message.AppendLine(ex.StackTrace);
            }

            // Handle specific common exceptions
            if (ex is ReflectionTypeLoadException)
            {
                var rtle = ex as ReflectionTypeLoadException;
                message.AppendLine("Loader Exceptions:");
                foreach (var loaderEx in rtle.LoaderExceptions)
                {
                    if (loaderEx != null)
                    {
                        message.AppendLine($"  - {loaderEx.GetType().Name}: {loaderEx.Message}");
                    }
                }
            }

            if (ex is FileNotFoundException)
            {
                var fnfe = ex as FileNotFoundException;
                message.AppendLine($"Missing File: {fnfe.FileName}");
                if (!string.IsNullOrEmpty(fnfe.FusionLog))
                {
                    message.AppendLine("Fusion Log:");
                    message.AppendLine(fnfe.FusionLog);
                }
            }
        }

        WriteInternal(LogLevel.Error, LogCategory.Plugin, "PluginManager", message.ToString());
    }

    // Removed DumpLoggerState
    #endregion

    // Disposable scope for measuring operations safely
    public sealed class MeasureScope : IDisposable
    {
        private readonly string _name; private readonly string _details; private bool _stopped;
        public MeasureScope(string name, string details = "") { _name = name; _details = details; StartTimer(name); }
        public void Dispose() { if (!_stopped) { StopTimer(_name, _details); _stopped = true; } }
    }

    public static MeasureScope Measure(string operationName, string details = "")
    {
        return new MeasureScope(operationName, details);
    }
}
