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
    public sealed class MmLogRuntimeLogFeed : IRuntimeLogFeed
    {
        public IList<RuntimeLogEntry> ReadRecent(string minimumLevel, int maxCount)
        {
            MMLog.LogLevel level;
            try
            {
                level = (MMLog.LogLevel)Enum.Parse(typeof(MMLog.LogLevel), minimumLevel ?? "Info", true);
            }
            catch
            {
                level = MMLog.LogLevel.Info;
            }

            var raw = MMLog.GetRecentEntries(level, maxCount);
            var entries = new List<RuntimeLogEntry>(raw.Count);
            for (var i = 0; i < raw.Count; i++)
            {
                var item = raw[i];
                entries.Add(new RuntimeLogEntry
                {
                    Timestamp = item.Timestamp,
                    Level = item.Level.ToString(),
                    Category = item.Category.ToString(),
                    Source = item.Source,
                    Message = item.Message,
                    StackFrames = MapStackFrames(item.StackFrames)
                });
            }

            return entries;
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
        public SourceNavigationTarget Resolve(RuntimeStackFrame frame, CortexProjectDefinition project)
        {
            if (frame == null)
            {
                return Failure("The runtime stack frame is missing.");
            }

            var directPath = ResolveCandidatePath(project, frame.FilePath);
            if (!string.IsNullOrEmpty(directPath))
            {
                MMLog.WriteDebug("Cortex resolved runtime frame via PDB path: " + directPath, MMLog.LogCategory.UI);
                return Success(directPath, frame.LineNumber, frame.ColumnNumber, false, "Resolved runtime frame via PDB source path.");
            }

            MethodBase method;
            if (!TryResolveMethod(frame, project, out method))
            {
                MMLog.WriteWarning("Cortex could not resolve runtime frame method for " + BuildFrameName(frame), MMLog.LogCategory.UI);
                return Failure("Cortex could not resolve the runtime method for this stack frame.");
            }

            SourceCacheManager.GetSource(method);
            var cachePath = SourceCacheManager.GetCachePath(method);
            if (!File.Exists(cachePath))
            {
                var methodOwner = method.DeclaringType != null ? method.DeclaringType.FullName : (method.Name ?? "UnknownMethod");
                MMLog.WriteWarning("Cortex could not locate cached source for " + methodOwner + "." + method.Name, MMLog.LogCategory.UI);
                return Failure(string.IsNullOrEmpty(SourceCacheManager.LastError) ? "No cached source is available for this frame." : SourceCacheManager.LastError);
            }

            var sourceLine = frame.LineNumber > 0 ? frame.LineNumber : SourceCacheManager.MapILToSourceLine(method, frame.IlOffset);
            if (sourceLine <= 0)
            {
                sourceLine = 1;
            }

            MMLog.WriteDebug("Cortex resolved runtime frame via cached source: " + cachePath, MMLog.LogCategory.UI);
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

        private static string ResolveCandidatePath(CortexProjectDefinition project, string rawPath)
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

            var sourceRoot = project != null ? project.SourceRootPath : string.Empty;
            if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                return string.Empty;
            }

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

            return string.Empty;
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
