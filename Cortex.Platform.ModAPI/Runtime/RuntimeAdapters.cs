using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Diagnostics;
using Cortex.Core.Models;
using ModAPI.Core;
using ModAPI.Inspector;
using GameModding.Shared.Restart;
using UnityEngine;
using ModApiMMLog = ModAPI.Core.MMLog;

namespace Cortex.Platform.ModAPI.Runtime
{
    public sealed class MmLogRuntimeLogFeed : IRuntimeLogFeed, IMMLogRuntimeSink
    {
        private readonly object _sync = new object();
        private readonly List<RuntimeLogEntry> _recentEntries = new List<RuntimeLogEntry>();
        private readonly Dictionary<string, BacklogSnapshot> _backlogSnapshots = new Dictionary<string, BacklogSnapshot>(StringComparer.OrdinalIgnoreCase);
        private bool _attached;
        private int _bufferSize = 600;

        public void Attach()
        {
            lock (_sync)
            {
                if (_attached)
                {
                    return;
                }

                ModApiMMLog.RegisterRuntimeSink(this);
                _attached = true;
                RebuildSnapshot_NoLock(_bufferSize);
            }
        }

        public void Detach()
        {
            lock (_sync)
            {
                if (!_attached)
                {
                    return;
                }

                ModApiMMLog.UnregisterRuntimeSink(this);
                _attached = false;
            }
        }

        public IList<RuntimeLogEntry> ReadRecent(string minimumLevel, int maxCount)
        {
            EnsureAttached();

            ModApiMMLog.LogLevel level;
            try
            {
                level = (ModApiMMLog.LogLevel)Enum.Parse(typeof(ModApiMMLog.LogLevel), minimumLevel ?? "Info", true);
            }
            catch
            {
                level = ModApiMMLog.LogLevel.Info;
            }

            lock (_sync)
            {
                if (maxCount > _bufferSize)
                {
                    _bufferSize = maxCount;
                    RebuildSnapshot_NoLock(_bufferSize);
                }

                var entries = new List<RuntimeLogEntry>();
                var required = Math.Max(1, maxCount);
                for (var i = _recentEntries.Count - 1; i >= 0 && entries.Count < required; i--)
                {
                    var item = _recentEntries[i];
                    if (item == null || !MatchesLevel(item.Level, level))
                    {
                        continue;
                    }

                    entries.Insert(0, CloneEntry(item));
                }

                return entries;
            }
        }

        public IList<string> ReadBacklog(string logPath, int maxCount)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
            {
                return lines;
            }

            lock (_sync)
            {
                var snapshot = GetOrCreateBacklogSnapshot_NoLock(logPath, Math.Max(1, maxCount));
                if (snapshot == null || snapshot.Lines.Count == 0)
                {
                    return lines;
                }

                var start = Math.Max(0, snapshot.Lines.Count - Math.Max(1, maxCount));
                for (var i = start; i < snapshot.Lines.Count; i++)
                {
                    lines.Add(snapshot.Lines[i]);
                }
            }

            return lines;
        }

        public void OnLogEntry(ModApiMMLog.LogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            lock (_sync)
            {
                _recentEntries.Add(MapEntry(entry));
                TrimBuffer_NoLock();
            }
        }

        private void EnsureAttached()
        {
            if (_attached)
            {
                return;
            }

            Attach();
        }

        private void RebuildSnapshot_NoLock(int maxCount)
        {
            _recentEntries.Clear();

            var raw = ModApiMMLog.GetRecentEntries(ModApiMMLog.LogLevel.Debug, Math.Max(1, maxCount));
            for (var i = 0; i < raw.Count; i++)
            {
                _recentEntries.Add(MapEntry(raw[i]));
            }

            TrimBuffer_NoLock();
        }

        private void TrimBuffer_NoLock()
        {
            while (_recentEntries.Count > _bufferSize)
            {
                _recentEntries.RemoveAt(0);
            }
        }

        private static bool MatchesLevel(string levelName, ModApiMMLog.LogLevel minimumLevel)
        {
            ModApiMMLog.LogLevel level;
            try
            {
                level = (ModApiMMLog.LogLevel)Enum.Parse(typeof(ModApiMMLog.LogLevel), levelName ?? "Info", true);
            }
            catch
            {
                level = ModApiMMLog.LogLevel.Info;
            }

            return level >= minimumLevel;
        }

        private static RuntimeLogEntry MapEntry(ModApiMMLog.LogEntry item)
        {
            return new RuntimeLogEntry
            {
                Sequence = item != null ? item.Sequence : 0,
                EntryId = item != null ? item.EntryId : string.Empty,
                Timestamp = item != null ? item.Timestamp : DateTime.MinValue,
                Level = item != null ? item.Level.ToString() : string.Empty,
                Category = item != null ? item.Category.ToString() : string.Empty,
                Source = item != null ? item.Source : string.Empty,
                Message = item != null ? item.Message : string.Empty,
                ThreadId = item != null ? item.ThreadId : 0,
                UnityFrame = item != null ? item.UnityFrame : -1,
                RepeatCount = item != null ? item.RepeatCount : 1,
                StackFrames = item != null ? MapStackFrames(item.StackFrames) : new List<RuntimeStackFrame>()
            };
        }

        private static RuntimeLogEntry CloneEntry(RuntimeLogEntry item)
        {
            if (item == null)
            {
                return new RuntimeLogEntry();
            }

            return new RuntimeLogEntry
            {
                Sequence = item.Sequence,
                EntryId = item.EntryId,
                Timestamp = item.Timestamp,
                Level = item.Level,
                Category = item.Category,
                Source = item.Source,
                Message = item.Message,
                ThreadId = item.ThreadId,
                UnityFrame = item.UnityFrame,
                RepeatCount = item.RepeatCount,
                StackFrames = CloneStackFrames(item.StackFrames)
            };
        }

        private static List<RuntimeStackFrame> CloneStackFrames(List<RuntimeStackFrame> stackFrames)
        {
            var frames = new List<RuntimeStackFrame>();
            if (stackFrames == null || stackFrames.Count == 0)
            {
                return frames;
            }

            for (var i = 0; i < stackFrames.Count; i++)
            {
                var frame = stackFrames[i];
                if (frame == null)
                {
                    continue;
                }

                frames.Add(new RuntimeStackFrame
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

            return frames;
        }

        private static List<RuntimeStackFrame> MapStackFrames(List<ModApiMMLog.RuntimeStackFrameInfo> stackFrames)
        {
            var frames = new List<RuntimeStackFrame>();
            if (stackFrames == null || stackFrames.Count == 0)
            {
                return frames;
            }

            for (var i = 0; i < stackFrames.Count; i++)
            {
                var frame = stackFrames[i];
                if (frame == null)
                {
                    continue;
                }

                frames.Add(new RuntimeStackFrame
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

            return frames;
        }

        private BacklogSnapshot GetOrCreateBacklogSnapshot_NoLock(string logPath, int maxCount)
        {
            BacklogSnapshot snapshot;
            if (!_backlogSnapshots.TryGetValue(logPath, out snapshot))
            {
                snapshot = new BacklogSnapshot();
                _backlogSnapshots[logPath] = snapshot;
            }

            if (!snapshot.Initialized || snapshot.MaxCount < maxCount)
            {
                snapshot.MaxCount = Math.Max(snapshot.MaxCount, maxCount);
                InitializeBacklogSnapshot_NoLock(snapshot, logPath);
                return snapshot;
            }

            UpdateBacklogSnapshot_NoLock(snapshot, logPath);
            TrimBacklog_NoLock(snapshot);
            return snapshot;
        }

        private static void InitializeBacklogSnapshot_NoLock(BacklogSnapshot snapshot, string logPath)
        {
            snapshot.Initialized = true;
            snapshot.PendingPartialLine = string.Empty;
            snapshot.Lines.Clear();

            try
            {
                using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var startPosition = FindTailStartPosition(stream, Math.Max(1, snapshot.MaxCount));
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                    {
                        AppendText_NoLock(snapshot, reader.ReadToEnd());
                        snapshot.Position = stream.Length;
                        snapshot.FileLength = stream.Length;
                    }
                }
            }
            catch
            {
                snapshot.Position = 0L;
                snapshot.FileLength = 0L;
                snapshot.PendingPartialLine = string.Empty;
                snapshot.Lines.Clear();
            }

            TrimBacklog_NoLock(snapshot);
        }

        private static void UpdateBacklogSnapshot_NoLock(BacklogSnapshot snapshot, string logPath)
        {
            try
            {
                using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < snapshot.Position)
                    {
                        InitializeBacklogSnapshot_NoLock(snapshot, logPath);
                        return;
                    }

                    if (stream.Length == snapshot.Position)
                    {
                        snapshot.FileLength = stream.Length;
                        return;
                    }

                    stream.Seek(snapshot.Position, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                    {
                        AppendText_NoLock(snapshot, reader.ReadToEnd());
                        snapshot.Position = stream.Length;
                        snapshot.FileLength = stream.Length;
                    }
                }
            }
            catch
            {
                InitializeBacklogSnapshot_NoLock(snapshot, logPath);
            }
        }

        private static void AppendText_NoLock(BacklogSnapshot snapshot, string text)
        {
            if (snapshot == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var combined = (snapshot.PendingPartialLine ?? string.Empty) + text.Replace("\r\n", "\n");
            var endsWithNewLine = combined.EndsWith("\n", StringComparison.Ordinal);
            var parts = combined.Split('\n');
            var limit = endsWithNewLine ? parts.Length : parts.Length - 1;
            for (var i = 0; i < limit; i++)
            {
                snapshot.Lines.Add(parts[i]);
            }

            snapshot.PendingPartialLine = endsWithNewLine || parts.Length == 0
                ? string.Empty
                : parts[parts.Length - 1];
        }

        private static void TrimBacklog_NoLock(BacklogSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            while (snapshot.Lines.Count > snapshot.MaxCount)
            {
                snapshot.Lines.RemoveAt(0);
            }
        }

        private static long FindTailStartPosition(FileStream stream, int lineCount)
        {
            if (stream == null || !stream.CanSeek)
            {
                return 0L;
            }

            var buffer = new byte[4096];
            var newlineCount = 0;
            var position = stream.Length;

            while (position > 0)
            {
                var readSize = (int)Math.Min(buffer.Length, position);
                position -= readSize;
                stream.Seek(position, SeekOrigin.Begin);
                stream.Read(buffer, 0, readSize);

                for (var i = readSize - 1; i >= 0; i--)
                {
                    if (buffer[i] != '\n')
                    {
                        continue;
                    }

                    newlineCount++;
                    if (newlineCount > lineCount)
                    {
                        return position + i + 1;
                    }
                }
            }

            return 0L;
        }

        private sealed class BacklogSnapshot
        {
            public readonly List<string> Lines = new List<string>();
            public bool Initialized;
            public long Position;
            public long FileLength;
            public int MaxCount;
            public string PendingPartialLine = string.Empty;
        }
    }

    public sealed class ModApiRuntimeSymbolResolver : IRuntimeSymbolResolver
    {
        private readonly ISourcePathResolver _sourcePathResolver;

        public ModApiRuntimeSymbolResolver(ISourcePathResolver sourcePathResolver)
        {
            _sourcePathResolver = sourcePathResolver;
        }

        public SourceNavigationTarget Resolve(RuntimeStackFrame frame, CortexProjectDefinition project, CortexSettings settings)
        {
            if (frame == null)
            {
                return Failure("The runtime stack frame is missing.");
            }

            var directPath = _sourcePathResolver != null
                ? _sourcePathResolver.ResolveCandidatePath(project, settings, frame.FilePath)
                : string.Empty;
            if (!string.IsNullOrEmpty(directPath))
            {
                return Success(directPath, frame.LineNumber, frame.ColumnNumber, false, "Resolved runtime frame via PDB source path.");
            }

            MethodBase method;
            if (!TryResolveMethod(frame, project, out method))
            {
                return Failure("Cortex could not resolve the runtime method for this stack frame.");
            }

            SourceCacheManager.GetSource(method);
            var cachePath = SourceCacheManager.GetCachePath(method);
            if (!File.Exists(cachePath))
            {
                return Failure(string.IsNullOrEmpty(SourceCacheManager.LastError) ? "No cached source is available for this frame." : SourceCacheManager.LastError);
            }

            var sourceLine = frame.LineNumber > 0 ? frame.LineNumber : SourceCacheManager.MapILToSourceLine(method, frame.IlOffset);
            if (sourceLine <= 0)
            {
                sourceLine = 1;
            }

            return Success(cachePath, sourceLine, frame.ColumnNumber, true, "Resolved runtime frame via generated source cache.");
        }

        private static bool TryResolveMethod(RuntimeStackFrame frame, CortexProjectDefinition project, out MethodBase method)
        {
            method = null;

            var assembly = ResolveAssembly(frame, project);
            if (assembly != null && frame.MetadataToken > 0)
            {
                method = ResolveMethodByToken(assembly, frame.MetadataToken);
                if (method != null)
                {
                    return true;
                }
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                method = ResolveMethodByName(assemblies[i], frame.TypeName, frame.MethodName);
                if (method != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static Assembly ResolveAssembly(RuntimeStackFrame frame, CortexProjectDefinition project)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                if (AssemblyMatches(assemblies[i], frame.AssemblyPath))
                {
                    return assemblies[i];
                }
            }

            var fallbackAssemblyPath = !string.IsNullOrEmpty(frame.AssemblyPath)
                ? frame.AssemblyPath
                : (project != null ? project.OutputAssemblyPath : string.Empty);
            if (!string.IsNullOrEmpty(fallbackAssemblyPath) && File.Exists(fallbackAssemblyPath))
            {
                try
                {
                    return Assembly.LoadFrom(fallbackAssemblyPath);
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool AssemblyMatches(Assembly assembly, string assemblyPath)
        {
            if (assembly == null || string.IsNullOrEmpty(assemblyPath))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static MethodBase ResolveMethodByToken(Assembly assembly, int metadataToken)
        {
            if (assembly == null || metadataToken <= 0)
            {
                return null;
            }

            try
            {
                return assembly.ManifestModule.ResolveMethod(metadataToken);
            }
            catch
            {
                var modules = assembly.GetModules();
                for (var i = 0; i < modules.Length; i++)
                {
                    try
                    {
                        var resolved = modules[i].ResolveMethod(metadataToken);
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static MethodBase ResolveMethodByName(Assembly assembly, string typeName, string methodName)
        {
            if (assembly == null || string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            Type type = null;
            try
            {
                var types = assembly.GetTypes();
                for (var i = 0; i < types.Length; i++)
                {
                    if (string.Equals(types[i].FullName, typeName, StringComparison.Ordinal) ||
                        string.Equals(types[i].Name, typeName, StringComparison.Ordinal))
                    {
                        type = types[i];
                        break;
                    }
                }
            }
            catch
            {
                return null;
            }

            if (type == null)
            {
                return null;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var methods = type.GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                if (string.Equals(methods[i].Name, methodName, StringComparison.Ordinal))
                {
                    return methods[i];
                }
            }

            var constructors = type.GetConstructors(flags);
            for (var i = 0; i < constructors.Length; i++)
            {
                if (string.Equals(constructors[i].Name, methodName, StringComparison.Ordinal) ||
                    string.Equals(".ctor", methodName, StringComparison.Ordinal))
                {
                    return constructors[i];
                }
            }

            if (string.Equals(".cctor", methodName, StringComparison.Ordinal))
            {
                return type.TypeInitializer;
            }

            return null;
        }

        private static SourceNavigationTarget Success(string filePath, int lineNumber, int columnNumber, bool isDecompiledSource, string statusMessage)
        {
            return new SourceNavigationTarget
            {
                Success = true,
                FilePath = filePath,
                LineNumber = lineNumber > 0 ? lineNumber : 1,
                ColumnNumber = columnNumber,
                IsDecompiledSource = isDecompiledSource,
                StatusMessage = statusMessage
            };
        }

        private static SourceNavigationTarget Failure(string statusMessage)
        {
            return new SourceNavigationTarget
            {
                Success = false,
                FilePath = string.Empty,
                LineNumber = 0,
                ColumnNumber = 0,
                IsDecompiledSource = false,
                StatusMessage = statusMessage ?? string.Empty
            };
        }
    }

    public sealed class ModApiLoadedModCatalog : ILoadedModCatalog
    {
        public IList<LoadedModInfo> GetLoadedMods()
        {
            var results = new List<LoadedModInfo>();
            var loadedMods = ModRegistry.GetLoadedMods();
            if (loadedMods == null)
            {
                return results;
            }

            for (var i = 0; i < loadedMods.Count; i++)
            {
                var mod = loadedMods[i];
                if (mod == null || string.IsNullOrEmpty(mod.Id))
                {
                    continue;
                }

                results.Add(new LoadedModInfo
                {
                    ModId = mod.Id,
                    DisplayName = mod.Id,
                    RootPath = mod.RootPath ?? string.Empty
                });
            }

            return results;
        }

        public LoadedModInfo GetMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return null;
            }

            var mod = ModRegistry.GetMod(modId);
            if (mod == null)
            {
                return null;
            }

            return new LoadedModInfo
            {
                ModId = mod.Id,
                DisplayName = mod.Id,
                RootPath = mod.RootPath ?? string.Empty
            };
        }
    }

    public sealed class ModApiRestartCoordinator : IRestartCoordinator
    {
        private readonly IRestartRequestWriter _writer;

        public ModApiRestartCoordinator(IRestartRequestWriter writer)
        {
            _writer = writer;
        }

        public bool RequestCurrentSessionRestart(out string errorMessage)
        {
            string restartPath;
            var success = _writer.WriteCurrentSessionRequest(out restartPath, out errorMessage);
            if (success)
            {
                Application.Quit();
            }

            return success;
        }

        public bool RequestManifestRestart(RestartRequest request, out string errorMessage)
        {
            string restartPath;
            var success = _writer.WriteRequest(request != null ? request.ResolveManifestPath() : string.Empty, out restartPath, out errorMessage);
            if (success)
            {
                Application.Quit();
            }

            return success;
        }
    }

    public sealed class ModApiRuntimeToolBridge : IRuntimeToolBridge
    {
        private readonly List<RuntimeToolStatus> _cachedTools = new List<RuntimeToolStatus>();
        private float _nextRefreshTime;

        public IList<RuntimeToolStatus> GetTools()
        {
            if (_cachedTools.Count == 0 || Time.realtimeSinceStartup >= _nextRefreshTime)
            {
                RefreshTools();
            }

            return CloneTools();
        }

        public bool Execute(string toolId, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (string.IsNullOrEmpty(toolId))
            {
                statusMessage = "No runtime tool was selected.";
                return false;
            }

            RuntimeToolStatus tool = null;
            var tools = GetTools();
            for (var i = 0; i < tools.Count; i++)
            {
                if (string.Equals(tools[i].ToolId, toolId, StringComparison.OrdinalIgnoreCase))
                {
                    tool = tools[i];
                    break;
                }
            }

            if (tool == null)
            {
                statusMessage = "The selected runtime tool is not registered.";
                return false;
            }

            if (!tool.IsAvailable)
            {
                statusMessage = string.IsNullOrEmpty(tool.UnavailableReason) ? "This runtime tool is not available in the current host." : tool.UnavailableReason;
                return false;
            }

            if (string.Equals(toolId, "runtime.inspector", StringComparison.OrdinalIgnoreCase))
            {
                ToggleRuntimeInspector();
            }
            else if (string.Equals(toolId, "runtime.il", StringComparison.OrdinalIgnoreCase))
            {
                ToggleIlInspector();
            }
            else if (string.Equals(toolId, "runtime.ui", StringComparison.OrdinalIgnoreCase))
            {
                ToggleUiDebugger();
            }
            else if (string.Equals(toolId, "runtime.debugger", StringComparison.OrdinalIgnoreCase))
            {
                ToggleRuntimeDebugger();
            }
            else
            {
                statusMessage = "The selected runtime tool does not have an execution handler.";
                return false;
            }

            _nextRefreshTime = 0f;
            statusMessage = tool.DisplayName + " toggled.";
            return true;
        }

        public void ToggleRuntimeInspector()
        {
            ToggleComponentState("ModAPI.Inspector.RuntimeInspector", "_visible");
        }

        public void ToggleIlInspector()
        {
            ToggleComponentState("ModAPI.Inspector.RuntimeILInspector", "_visible");
        }

        public void ToggleUiDebugger()
        {
            ToggleComponentState("ModAPI.UI.UIDebugInspector", "_active");
        }

        public void ToggleRuntimeDebugger()
        {
            ToggleComponentState("ModAPI.Inspector.RuntimeDebuggerUI", "_active");
        }

        private void RefreshTools()
        {
            _cachedTools.Clear();
            _cachedTools.Add(BuildTool("runtime.inspector", "Legacy Runtime Inspector", "Inspect live objects and scene hierarchies inside the current host.", "F9", "ModAPI.Inspector.RuntimeInspector", "_visible"));
            _cachedTools.Add(BuildTool("runtime.il", "IL Inspector", "Inspect IL and generated runtime source mappings when the ModAPI IL inspector is present.", "F10", "ModAPI.Inspector.RuntimeILInspector", "_visible"));
            _cachedTools.Add(BuildTool("runtime.ui", "UI Debugger", "Open the UI debugger when the host exposes the ModAPI UI inspection surface.", "F11", "ModAPI.UI.UIDebugInspector", "_active"));
            _cachedTools.Add(BuildTool("runtime.debugger", "Runtime Debugger", "Toggle the legacy runtime debugger overlay if the current title ships it.", "F7", "ModAPI.Inspector.RuntimeDebuggerUI", "_active"));
            _nextRefreshTime = Time.realtimeSinceStartup + 0.5f;
        }

        private IList<RuntimeToolStatus> CloneTools()
        {
            var results = new List<RuntimeToolStatus>();
            for (var i = 0; i < _cachedTools.Count; i++)
            {
                var tool = _cachedTools[i];
                results.Add(new RuntimeToolStatus
                {
                    ToolId = tool.ToolId,
                    DisplayName = tool.DisplayName,
                    Description = tool.Description,
                    ShortcutHint = tool.ShortcutHint,
                    IsAvailable = tool.IsAvailable,
                    IsActive = tool.IsActive,
                    UnavailableReason = tool.UnavailableReason
                });
            }

            return results;
        }

        private static RuntimeToolStatus BuildTool(string toolId, string displayName, string description, string shortcutHint, string typeName, string fieldName)
        {
            MonoBehaviour component;
            var available = TryFindComponent(typeName, out component);
            var active = available && ReadToolState(component, fieldName);

            return new RuntimeToolStatus
            {
                ToolId = toolId,
                DisplayName = displayName,
                Description = description,
                ShortcutHint = shortcutHint,
                IsAvailable = available,
                IsActive = active,
                UnavailableReason = available ? string.Empty : "The current host did not expose " + displayName + "."
            };
        }

        private static void ToggleComponentState(string typeName, string fieldName)
        {
            MonoBehaviour component;
            if (!TryFindComponent(typeName, out component))
            {
                CortexLog.WriteWarning("[Cortex.RuntimeTools] Runtime tool was not found. Type=" + typeName + ".");
                return;
            }

            var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                var current = (bool)field.GetValue(component);
                field.SetValue(component, !current);
            }
        }

        private static bool TryFindComponent(string typeName, out MonoBehaviour component)
        {
            component = null;
            var components = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().FullName == typeName)
                {
                    component = components[i];
                    return true;
                }
            }

            return false;
        }

        private static bool ReadToolState(MonoBehaviour component, string fieldName)
        {
            if (component == null)
            {
                return false;
            }

            var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(component);
        }
    }
}
