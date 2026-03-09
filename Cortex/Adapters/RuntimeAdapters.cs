using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using ModAPI.Core;
using ModAPI.Inspector;
using GameModding.Shared.Restart;
using UnityEngine;

namespace Cortex.Adapters
{
    public sealed class MmLogRuntimeLogFeed : IRuntimeLogFeed, IMMLogRuntimeSink
    {
        private readonly object _sync = new object();
        private readonly List<RuntimeLogEntry> _recentEntries = new List<RuntimeLogEntry>();
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

                MMLog.RegisterRuntimeSink(this);
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

                MMLog.UnregisterRuntimeSink(this);
                _attached = false;
            }
        }

        public IList<RuntimeLogEntry> ReadRecent(string minimumLevel, int maxCount)
        {
            EnsureAttached();

            MMLog.LogLevel level;
            try
            {
                level = (MMLog.LogLevel)Enum.Parse(typeof(MMLog.LogLevel), minimumLevel ?? "Info", true);
            }
            catch
            {
                level = MMLog.LogLevel.Info;
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

            var allLines = File.ReadAllLines(logPath);
            var start = Math.Max(0, allLines.Length - maxCount);
            for (var i = start; i < allLines.Length; i++)
            {
                lines.Add(allLines[i]);
            }

            return lines;
        }

        public void OnLogEntry(MMLog.LogEntry entry)
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

            var raw = MMLog.GetRecentEntries(MMLog.LogLevel.Debug, Math.Max(1, maxCount));
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

        private static bool MatchesLevel(string levelName, MMLog.LogLevel minimumLevel)
        {
            MMLog.LogLevel level;
            try
            {
                level = (MMLog.LogLevel)Enum.Parse(typeof(MMLog.LogLevel), levelName ?? "Info", true);
            }
            catch
            {
                level = MMLog.LogLevel.Info;
            }

            return level >= minimumLevel;
        }

        private static RuntimeLogEntry MapEntry(MMLog.LogEntry item)
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

        private static List<RuntimeStackFrame> MapStackFrames(List<MMLog.RuntimeStackFrameInfo> stackFrames)
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
    }

    public sealed class ModApiRuntimeSymbolResolver : IRuntimeSymbolResolver
    {
        public SourceNavigationTarget Resolve(RuntimeStackFrame frame, CortexProjectDefinition project, CortexSettings settings)
        {
            if (frame == null)
            {
                return Failure("The runtime stack frame is missing.");
            }

            var directPath = ResolveCandidatePath(project, settings, frame.FilePath);
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

        private static string ResolveCandidatePath(CortexProjectDefinition project, CortexSettings settings, string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath))
            {
                return string.Empty;
            }

            rawPath = rawPath.Trim().Trim('"');
            if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            if (File.Exists(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            var searchRoots = BuildSearchRoots(project, settings);
            for (var i = 0; i < searchRoots.Count; i++)
            {
                var sourceRoot = searchRoots[i];
                var combined = Path.Combine(sourceRoot, rawPath);
                if (File.Exists(combined))
                {
                    return Path.GetFullPath(combined);
                }

                try
                {
                    var fileName = Path.GetFileName(rawPath);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        var matches = Directory.GetFiles(sourceRoot, fileName, SearchOption.AllDirectories);
                        if (matches.Length > 0)
                        {
                            return matches[0];
                        }
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static List<string> BuildSearchRoots(CortexProjectDefinition project, CortexSettings settings)
        {
            var roots = new List<string>();
            AddRoot(roots, project != null ? project.SourceRootPath : string.Empty);
            AddRoot(roots, project != null ? Path.GetDirectoryName(project.ProjectFilePath) : string.Empty);
            AddRoot(roots, settings != null ? settings.WorkspaceRootPath : string.Empty);
            AddRoot(roots, settings != null ? settings.ModsRootPath : string.Empty);

            var rawRoots = settings != null ? settings.AdditionalSourceRoots : string.Empty;
            if (!string.IsNullOrEmpty(rawRoots))
            {
                var segments = rawRoots.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length; i++)
                {
                    AddRoot(roots, segments[i]);
                }
            }

            return roots;
        }

        private static void AddRoot(List<string> roots, string root)
        {
            if (roots == null || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(root.Trim());
                if (Directory.Exists(fullPath) && !roots.Contains(fullPath))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static string BuildFrameName(RuntimeStackFrame frame)
        {
            if (frame == null)
            {
                return "unknown-frame";
            }

            if (!string.IsNullOrEmpty(frame.DisplayText))
            {
                return frame.DisplayText;
            }

            var typeName = string.IsNullOrEmpty(frame.TypeName) ? "UnknownType" : frame.TypeName;
            var methodName = string.IsNullOrEmpty(frame.MethodName) ? "UnknownMethod" : frame.MethodName;
            return typeName + "." + methodName;
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

        private static void ToggleComponentState(string typeName, string fieldName)
        {
            var components = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || component.GetType().FullName != typeName)
                {
                    continue;
                }

                var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    var current = (bool)field.GetValue(component);
                    field.SetValue(component, !current);
                }

                return;
            }

            MMLog.WriteWarning("Cortex could not find runtime tool: " + typeName);
        }
    }
}
