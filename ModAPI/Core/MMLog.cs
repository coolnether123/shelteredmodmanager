using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ModAPI.Core
{
    public static class MMLog
    {
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3,
            Fatal = 4
        }

        // TODO: LogCategory system needs to be reworked more directly into ModAPI
        // For v1.0 release, categories are HARDCODED (no INI customization)
        // Default categories: General, Loader, Plugin, Assembly
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

        public sealed class LogEntry
        {
            public long Sequence;
            public string EntryId;
            public DateTime Timestamp;
            public LogLevel Level;
            public LogCategory Category;
            public string Source;
            public string Message;
            public int ThreadId;
            public int UnityFrame;
            public int RepeatCount;
            public List<RuntimeStackFrameInfo> StackFrames;
        }

        public sealed class RuntimeStackFrameInfo
        {
            public string AssemblyPath;
            public string TypeName;
            public string MethodName;
            public int MetadataToken;
            public int IlOffset;
            public string FilePath;
            public int LineNumber;
            public int ColumnNumber;
            public string DisplayText;
        }

        private static readonly object _lock = new object();
        private static string _logPath;
        private static LogLevel _minLevel = LogLevel.Info;
        private static readonly HashSet<LogCategory> _enabledCategories = new HashSet<LogCategory>();
        private static readonly Dictionary<string, int> _modLogCounts = new Dictionary<string, int>();
        private static long _logFileSize = 0;
        private static readonly long _maxLogFileSize = 10 * 1024 * 1024; // 10MB

        private static string _lastMsg;
        private static int _repeatCount;
        private static DateTime _lastWriteUtc;
        private static DateTime _repeatStartTime;
        private static readonly TimeSpan _repeatFlushInterval = TimeSpan.FromSeconds(5);

        private static readonly Dictionary<string, Stopwatch> _activeTimers = new Dictionary<string, Stopwatch>();
        private static readonly Dictionary<string, List<long>> _performanceHistory = new Dictionary<string, List<long>>();

        private static readonly HashSet<string> _warnOnceKeys = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _iniSettings = new Dictionary<string, string>();
        
        private static readonly Dictionary<Assembly, string> _sourceCache = new Dictionary<Assembly, string>();
        private static readonly object _cacheLock = new object();
        private static readonly Queue<LogEntry> _recentEntries = new Queue<LogEntry>();
        private static readonly int _maxRecentEntries = 500;
        private static readonly List<IMMLogRuntimeSink> _runtimeSinks = new List<IMMLogRuntimeSink>();
        private static MMLogRuntimeOptions _runtimeOptions = MMLogRuntimeOptions.Disabled();
        private static long _sequenceCounter = 0;

        static MMLog()
        {
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "SMM");
            Directory.CreateDirectory(baseDir);
            _logPath = Path.Combine(baseDir, "mod_manager.log");
            try { if (File.Exists(_logPath)) _logFileSize = new FileInfo(_logPath).Length; } catch { _logFileSize = 0; }

            // Hardcoded defaults for v1.0 - same as original behavior
            _enabledCategories.Add(LogCategory.General);
            _enabledCategories.Add(LogCategory.Loader);
            _enabledCategories.Add(LogCategory.Plugin);
            _enabledCategories.Add(LogCategory.Assembly);

            InitializeFromManagerIni();
            WriteStartupBanner();
        }

        private static void InitializeFromManagerIni()
        {
            try
            {
                var smmPath = Path.Combine(Directory.GetCurrentDirectory(), "SMM");
                var binPath = Path.Combine(smmPath, "bin");
                var ini = Path.Combine(binPath, "mod_manager.ini");
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
                        _iniSettings[key] = val;
                        if (key.Equals("DevMode", StringComparison.OrdinalIgnoreCase)) devMode = val;
                        else if (key.Equals("LogLevel", StringComparison.OrdinalIgnoreCase)) logLevel = val;
                        else if (key.Equals("LogCategories", StringComparison.OrdinalIgnoreCase)) logCategories = val;
                    }
                }
                // DevMode or Debug LogLevel = enable ALL categories for verbose logging
                if (!string.IsNullOrEmpty(devMode) && devMode.ToLower() == "true")
                {
                    _minLevel = LogLevel.Debug;
                    // Enable all categories for verbose logging
                    _enabledCategories.Clear();
                    foreach (LogCategory cat in Enum.GetValues(typeof(LogCategory)))
                        _enabledCategories.Add(cat);
                }

                var level = TryParseLogLevel(logLevel);
                if (level.HasValue)
                {
                    _minLevel = level.Value;
                    // If Debug level is set, also enable all categories
                    if (level.Value == LogLevel.Debug)
                    {
                        _enabledCategories.Clear();
                        foreach (LogCategory cat in Enum.GetValues(typeof(LogCategory)))
                            _enabledCategories.Add(cat);
                    }
                }

                // LogCategories INI setting ignored in v1.0 - categories controlled by LogLevel
            }
            catch (Exception ex)
            {
                WriteInternal(LogLevel.Error, LogCategory.General, "System",
                    $"Failed to initialize logging from environment: {ex.Message}");
            }
        }

        private static void WriteStartupBanner()
        {
            var unityVersion = RuntimeCompat.UnityVersion;
            var gameVersion = RuntimeCompat.GameVersion;
            var modApiVersion = RuntimeCompat.ModApiVersion;
            var arch = RuntimeCompat.Architecture;
            var buildTarget = unityVersion.StartsWith("5.6") ? "Epic Games (x64)" : "Steam/GOG (x86)";

            var banner = new StringBuilder();
            banner.AppendLine("=================================================================================");
            banner.AppendLine("                    SHELTERED MOD LOADER - DEBUG LOG");
            banner.AppendLine("=================================================================================");
            banner.AppendLine(string.Format("Session Started: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
            banner.AppendLine(string.Format("ModAPI Version:  {0}", modApiVersion));
            banner.AppendLine(string.Format("Architecture:    {0}", arch));
            banner.AppendLine(string.Format("Build Target:    {0}", buildTarget));
            banner.AppendLine(string.Format("Game Version:    {0}", gameVersion));
            banner.AppendLine(string.Format("Unity Version:   {0}", unityVersion));
            banner.AppendLine(string.Format("Data Path:       {0}", Application.dataPath));
            banner.AppendLine(string.Format("Log Level:       {0}", _minLevel));
            banner.AppendLine("=================================================================================");

            try { File.AppendAllText(_logPath, banner.ToString(), Encoding.UTF8); } 
            catch { }
        }

        // Version probes are centralized in RuntimeCompat to keep 5.3/5.6 differences in one place.

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

        public static void Write(string message)
        {
            WriteInternal(LogLevel.Info, LogCategory.General, GetCallerInfo(), message);
        }

        public static void Write(string source, string message)
        {
            WriteInternal(LogLevel.Info, LogCategory.General, source, message);
        }

        public static void WriteDebug(string message, LogCategory category = LogCategory.General)
        {
            WriteInternal(LogLevel.Debug, category, GetCallerInfo(), message);
        }

        public static void WriteInfo(string message, LogCategory category = LogCategory.General)
        {
            WriteInternal(LogLevel.Info, category, GetCallerInfo(), message);
        }

        public static void WriteWarning(string message, LogCategory category = LogCategory.General)
        {
            WriteInternal(LogLevel.Warning, category, GetCallerInfo(), message);
        }

        public static void WriteError(string message, LogCategory category = LogCategory.General)
        {
            WriteInternal(LogLevel.Error, category, GetCallerInfo(), message);
        }

        public static void WriteFatal(string message, LogCategory category = LogCategory.General)
        {
            WriteInternal(LogLevel.Fatal, category, GetCallerInfo(), message);
        }

        public static void WriteWithSource(LogLevel level, LogCategory category, string source, string message)
        {
            WriteInternal(level, category, source, message);
        }

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

        public static void LogOnce(string key, Action logAction)
        {
            if (string.IsNullOrEmpty(key) || logAction == null) return;
            lock (_lock)
            {
                if (_warnOnceKeys.Contains(key)) return;
                _warnOnceKeys.Add(key);
            }
            try { logAction(); } catch { }
        }

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

            var innerEx = ex.InnerException;
            int depth = 1;
            while (innerEx != null && depth < 10)
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

            WriteInternal(LogLevel.Error, category, GetCallerInfo(), message.ToString(), ExtractRuntimeFrames(ex));
        }

        public static void WritePluginLifecycle(string pluginName, string phase, string details = "", bool isError = false)
        {
            var level = isError ? LogLevel.Error : LogLevel.Info;
            var message = $"[PLUGIN-{phase.ToUpper()}] {pluginName}";
            if (!string.IsNullOrEmpty(details))
                message += $": {details}";

            WriteInternal(level, LogCategory.Plugin, "PluginManager", message);
        }

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

        public static void WriteDependencyInfo(string modId, string operation, string details)
        {
            var message = $"[DEPENDENCY-{operation.ToUpper()}] {modId}: {details}";
            WriteInternal(LogLevel.Debug, LogCategory.Dependency, "LoadOrderResolver", message);
        }

        public static void StartTimer(string operationName)
        {
            lock (_lock)
            {
                if (!_activeTimers.ContainsKey(operationName))
                    _activeTimers[operationName] = new Stopwatch();

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

        public static void WriteSceneInfo(string sceneName, string operation)
        {
            var message = $"[SCENE-{operation.ToUpper()}] {sceneName}";
            WriteInternal(LogLevel.Info, LogCategory.Scene, "SceneManager", message);
        }

        public static void WriteConfigChange(string modId, string key, string oldValue, string newValue)
        {
            var message = $"[CONFIG-CHANGE] {modId}: {key} = '{oldValue}' -> '{newValue}'";
            WriteInternal(LogLevel.Debug, LogCategory.Configuration, "ModSettings", message);
        }

        public static void Flush()
        {
            LogEntry emittedEntry = null;
            lock (_lock)
            {
                emittedEntry = FlushRepeat_NoLock(DateTime.UtcNow);
                CheckLogRotation();
            }

            PublishRuntimeSinkEntry(emittedEntry);
        }

        public static void SetLogLevel(LogLevel level)
        {
            _minLevel = level;
            WriteInternal(LogLevel.Info, LogCategory.General, "Logger", $"Log level changed to: {level}");
        }

        public static void ConfigureRuntimeIntegration(MMLogRuntimeOptions options)
        {
            lock (_lock)
            {
                _runtimeOptions = options ?? MMLogRuntimeOptions.Disabled();
            }
        }

        public static void RegisterRuntimeSink(IMMLogRuntimeSink sink)
        {
            if (sink == null)
            {
                return;
            }

            lock (_lock)
            {
                if (_runtimeSinks.Contains(sink))
                {
                    return;
                }

                _runtimeSinks.Add(sink);
            }
        }

        public static void UnregisterRuntimeSink(IMMLogRuntimeSink sink)
        {
            if (sink == null)
            {
                return;
            }

            lock (_lock)
            {
                _runtimeSinks.Remove(sink);
            }
        }

        public static List<LogEntry> GetRecentEntries(LogLevel minLevel, int maxCount)
        {
            if (maxCount <= 0) return new List<LogEntry>();
            lock (_lock)
            {
                return _recentEntries
                    .Where(e => e != null && e.Level >= minLevel)
                    .Reverse()
                    .Take(maxCount)
                    .Reverse()
                    .Select(CloneLogEntry)
                    .ToList();
            }
        }

        private static void WriteInternal(LogLevel level, LogCategory category, string source, string message, List<RuntimeStackFrameInfo> stackFrames = null)
        {
            if (level < _minLevel) return;
            if (message == null) message = string.Empty;

            if (stackFrames == null && ShouldCaptureRuntimeFrames(level))
            {
                stackFrames = CaptureCurrentRuntimeFrames(3);
            }

            var now = DateTime.Now;
            var comparableMessage = FormatLogMessage(level, category, source, message, now, false);
            LogEntry repeatedEntry = null;
            LogEntry emittedEntry = null;

            lock (_lock)
            {
                // Category filtering enabled - only configured categories log (unless Error level)
                if (!_enabledCategories.Contains(category) && level < LogLevel.Error)
                    return;

                if (!string.IsNullOrEmpty(_lastMsg) && string.Equals(comparableMessage, _lastMsg, StringComparison.Ordinal))
                {
                    if (_repeatCount == 0)
                    {
                        _repeatStartTime = _lastWriteUtc;
                    }
                    _repeatCount++;
                    if ((now - _repeatStartTime) >= _repeatFlushInterval)
                    {
                        repeatedEntry = FlushRepeat_NoLock(now);
                    }
                    goto PublishAndExit;
                }

                if (_repeatCount > 0)
                {
                    repeatedEntry = FlushRepeat_NoLock(now);
                }

                var formattedMessage = FormatLogMessage(level, category, source, message, now);
                WriteToFile(formattedMessage);
                emittedEntry = PushRecentEntry_NoLock(now, level, category, source, message, stackFrames, 1);

                TrackModLogCount(source);

                _lastMsg = comparableMessage;
                _repeatCount = 0;
                _lastWriteUtc = now;
            }

        PublishAndExit:
            PublishRuntimeSinkEntry(repeatedEntry);
            PublishRuntimeSinkEntry(emittedEntry);
        }

        private static string FormatLogMessage(LogLevel level, LogCategory category, string source, string message, DateTime timestamp, bool includeTimestamp = true)
        {
            var sb = new StringBuilder();
            if (includeTimestamp)
                sb.Append($"[{timestamp:HH:mm:ss.fff}] ");
            sb.Append($"[{level.ToString().ToUpper().PadRight(5)}] ");
            sb.Append($"[{(source ?? "Unknown")}] ");
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
            }
        }

        private static LogEntry FlushRepeat_NoLock(DateTime nowUtc)
        {
            if (_repeatCount <= 0) return null;

            var startTime = _repeatStartTime.ToLocalTime().ToString("HH:mm:ss.fff");
            var endTime = nowUtc.ToLocalTime().ToString("HH:mm:ss.fff");

            var summary = FormatLogMessage(LogLevel.Info, LogCategory.General, "Logger",
                ($"(Previous message repeated {_repeatCount} times from {startTime} to {endTime})"), nowUtc, true);

            WriteToFile(summary);
            var entry = PushRecentEntry_NoLock(nowUtc, LogLevel.Info, LogCategory.General, "Logger",
                $"(Previous message repeated {_repeatCount} times from {startTime} to {endTime})", null, _repeatCount);
            _repeatCount = 0;
            _lastWriteUtc = nowUtc;
            return entry;
        }

        private static LogEntry PushRecentEntry_NoLock(DateTime timestamp, LogLevel level, LogCategory category, string source, string message, List<RuntimeStackFrameInfo> stackFrames, int repeatCount)
        {
            var sequence = ++_sequenceCounter;
            var entry = new LogEntry
            {
                Sequence = sequence,
                EntryId = "log-" + sequence.ToString(),
                Timestamp = timestamp,
                Level = level,
                Category = category,
                Source = source ?? "Unknown",
                Message = message ?? string.Empty,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                UnityFrame = SafeGetUnityFrameCount(),
                RepeatCount = repeatCount <= 0 ? 1 : repeatCount,
                StackFrames = CloneRuntimeFrames(stackFrames)
            };
            _recentEntries.Enqueue(entry);
            while (_recentEntries.Count > _maxRecentEntries)
            {
                _recentEntries.Dequeue();
            }

            return entry;
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
                    try
                    {
                        File.WriteAllText(_logPath, $"[LOG ROTATION FAILED: {ex.Message}]\n");
                        _logFileSize = 0;
                    }
                    catch { }
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

            if (history.Count > 50)
                history.RemoveAt(0);
        }

        private static string GetCallerInfo()
        {
            if (PluginRunner.IsQuitting) return "Quitting";
            try
            {
                StackTrace st = new StackTrace(false);
                Assembly modAPIAssembly = typeof(MMLog).Assembly;

                for (int i = 2; i < st.FrameCount; i++)
                {
                    var frame = st.GetFrame(i);
                    var method = frame.GetMethod();
                    if (method == null) continue;

                    var declaringType = method.DeclaringType;
                    if (declaringType == null) continue;

                    // Skip internal logging wrappers to find the real caller
                    if (declaringType == typeof(MMLog) || 
                        declaringType.Name == "ModLog" || 
                        declaringType.Name == "PrefixedLogger")
                    {
                        continue;
                    }

                    Assembly callingAssembly = declaringType.Assembly;
                    
                    // If we found an assembly that isn't ModAPI, it's a mod!
                    if (callingAssembly != modAPIAssembly)
                    {
                        lock (_cacheLock)
                        {
                            if (_sourceCache.TryGetValue(callingAssembly, out var cached))
                                return cached;
                        }

                        string name = "Unknown";
                        ModEntry entry;
                        if (ModRegistry.TryGetModByAssembly(callingAssembly, out entry) && entry != null)
                        {
                            name = entry.Id;
                        }
                        else
                        {
                            name = callingAssembly.GetName().Name;
                        }

                        lock (_cacheLock)
                        {
                            _sourceCache[callingAssembly] = name;
                        }
                        return name;
                    }
                    
                    // If we are still in ModAPI, use the first non-wrapper class name we hit
                    return declaringType.Name;
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

        private static bool CheckIsDynamic(Assembly assembly)
        {
            try
            {
                var location = assembly.Location;
                return string.IsNullOrEmpty(location);
            }
            catch
            {
                return true;
            }
        }

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

            WriteInternal(LogLevel.Error, LogCategory.Plugin, "PluginManager", message.ToString(), ExtractRuntimeFrames(ex));
        }

        private static List<RuntimeStackFrameInfo> ExtractRuntimeFrames(Exception ex)
        {
            if (ex == null)
            {
                return null;
            }

            try
            {
                var trace = new StackTrace(ex, true);
                var frames = trace.GetFrames();
                if (frames == null || frames.Length == 0)
                {
                    return null;
                }

                var results = new List<RuntimeStackFrameInfo>(frames.Length);
                for (var i = 0; i < frames.Length; i++)
                {
                    var runtimeFrame = BuildRuntimeFrame(frames[i]);
                    if (runtimeFrame != null)
                    {
                        results.Add(runtimeFrame);
                    }
                }

                return results.Count > 0 ? results : null;
            }
            catch
            {
                return null;
            }
        }

        private static RuntimeStackFrameInfo BuildRuntimeFrame(StackFrame frame)
        {
            if (frame == null)
            {
                return null;
            }

            MethodBase method = null;
            try
            {
                method = frame.GetMethod();
            }
            catch
            {
            }

            var assemblyPath = string.Empty;
            var typeName = string.Empty;
            var methodName = string.Empty;
            if (method != null)
            {
                methodName = method.Name ?? string.Empty;
                typeName = method.DeclaringType != null ? (method.DeclaringType.FullName ?? string.Empty) : string.Empty;
                try
                {
                    assemblyPath = method.Module != null && method.Module.Assembly != null
                        ? (method.Module.Assembly.Location ?? string.Empty)
                        : string.Empty;
                }
                catch
                {
                    assemblyPath = string.Empty;
                }
            }

            string filePath;
            int lineNumber;
            int columnNumber;
            try
            {
                filePath = frame.GetFileName() ?? string.Empty;
                lineNumber = frame.GetFileLineNumber();
                columnNumber = frame.GetFileColumnNumber();
            }
            catch
            {
                filePath = string.Empty;
                lineNumber = 0;
                columnNumber = 0;
            }

            int ilOffset;
            try
            {
                ilOffset = frame.GetILOffset();
            }
            catch
            {
                ilOffset = -1;
            }

            var displayText = BuildFrameDisplayText(typeName, methodName, filePath, lineNumber, ilOffset);
            return new RuntimeStackFrameInfo
            {
                AssemblyPath = assemblyPath,
                TypeName = typeName,
                MethodName = methodName,
                MetadataToken = SafeGetMetadataToken(method),
                IlOffset = ilOffset,
                FilePath = filePath,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                DisplayText = displayText
            };
        }

        private static List<RuntimeStackFrameInfo> CloneRuntimeFrames(List<RuntimeStackFrameInfo> stackFrames)
        {
            if (stackFrames == null || stackFrames.Count == 0)
            {
                return null;
            }

            var clones = new List<RuntimeStackFrameInfo>(stackFrames.Count);
            for (var i = 0; i < stackFrames.Count; i++)
            {
                var frame = stackFrames[i];
                if (frame == null)
                {
                    continue;
                }

                clones.Add(new RuntimeStackFrameInfo
                {
                    AssemblyPath = frame.AssemblyPath,
                    TypeName = frame.TypeName,
                    MethodName = frame.MethodName,
                    MetadataToken = frame.MetadataToken,
                    IlOffset = frame.IlOffset,
                    FilePath = frame.FilePath,
                    LineNumber = frame.LineNumber,
                    ColumnNumber = frame.ColumnNumber,
                    DisplayText = frame.DisplayText
                });
            }

            return clones;
        }

        private static LogEntry CloneLogEntry(LogEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return new LogEntry
            {
                Sequence = entry.Sequence,
                EntryId = entry.EntryId,
                Timestamp = entry.Timestamp,
                Level = entry.Level,
                Category = entry.Category,
                Source = entry.Source,
                Message = entry.Message,
                ThreadId = entry.ThreadId,
                UnityFrame = entry.UnityFrame,
                RepeatCount = entry.RepeatCount,
                StackFrames = CloneRuntimeFrames(entry.StackFrames)
            };
        }

        private static bool ShouldCaptureRuntimeFrames(LogLevel level)
        {
            var options = _runtimeOptions;
            if (options == null)
            {
                return false;
            }

            switch (level)
            {
                case LogLevel.Warning:
                    return options.CaptureWarningStackFrames;
                case LogLevel.Error:
                    return options.CaptureErrorStackFrames;
                case LogLevel.Fatal:
                    return options.CaptureFatalStackFrames;
                default:
                    return false;
            }
        }

        private static List<RuntimeStackFrameInfo> CaptureCurrentRuntimeFrames(int skipFrameCount)
        {
            try
            {
                var maxCapturedFrames = _runtimeOptions != null && _runtimeOptions.MaxCapturedFrames > 0
                    ? _runtimeOptions.MaxCapturedFrames
                    : 0;
                if (maxCapturedFrames <= 0)
                {
                    return null;
                }

                var trace = new StackTrace(skipFrameCount, true);
                var frames = trace.GetFrames();
                if (frames == null || frames.Length == 0)
                {
                    return null;
                }

                var results = new List<RuntimeStackFrameInfo>(Math.Min(frames.Length, maxCapturedFrames));
                for (var i = 0; i < frames.Length && results.Count < maxCapturedFrames; i++)
                {
                    var runtimeFrame = BuildRuntimeFrame(frames[i]);
                    if (runtimeFrame == null || IsInternalRuntimeFrame(runtimeFrame))
                    {
                        continue;
                    }

                    results.Add(runtimeFrame);
                }

                return results.Count > 0 ? results : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsInternalRuntimeFrame(RuntimeStackFrameInfo frame)
        {
            if (frame == null)
            {
                return true;
            }

            var typeName = frame.TypeName ?? string.Empty;
            if (typeName == typeof(MMLog).FullName || typeName == typeof(ModLog).FullName || typeName == typeof(PrefixedLogger).FullName)
            {
                return true;
            }

            return false;
        }

        private static int SafeGetUnityFrameCount()
        {
            try
            {
                return Time.frameCount;
            }
            catch
            {
                return -1;
            }
        }

        private static void PublishRuntimeSinkEntry(LogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            List<IMMLogRuntimeSink> sinks = null;
            lock (_lock)
            {
                if (_runtimeSinks.Count == 0)
                {
                    return;
                }

                sinks = new List<IMMLogRuntimeSink>(_runtimeSinks);
            }

            for (var i = 0; i < sinks.Count; i++)
            {
                try
                {
                    sinks[i].OnLogEntry(CloneLogEntry(entry));
                }
                catch
                {
                }
            }
        }

        private static int SafeGetMetadataToken(MethodBase method)
        {
            if (method == null)
            {
                return 0;
            }

            try
            {
                return method.MetadataToken;
            }
            catch
            {
                return 0;
            }
        }

        private static string BuildFrameDisplayText(string typeName, string methodName, string filePath, int lineNumber, int ilOffset)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(typeName))
            {
                builder.Append(typeName);
            }
            else
            {
                builder.Append("UnknownType");
            }

            builder.Append(".");
            builder.Append(string.IsNullOrEmpty(methodName) ? "UnknownMethod" : methodName);

            if (!string.IsNullOrEmpty(filePath))
            {
                builder.Append(" @ ");
                builder.Append(Path.GetFileName(filePath));
                if (lineNumber > 0)
                {
                    builder.Append(":");
                    builder.Append(lineNumber);
                }
            }
            else if (ilOffset >= 0)
            {
                builder.Append(" @ IL ");
                builder.Append(ilOffset);
            }

            return builder.ToString();
        }

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
}
