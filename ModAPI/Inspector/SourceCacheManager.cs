using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    public static class SourceCacheManager
    {
        private static readonly string APPDATA_ROOT = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModAPI");
        private static readonly string CACHE_ROOT = Path.Combine(APPDATA_ROOT, "Cache");

        private static readonly ExternalProcessManager ProcessManager = new ExternalProcessManager();
        private static string _lastError = string.Empty;

        static SourceCacheManager()
        {
            EnsureDirectories();
        }

        public static string CacheRootPath
        {
            get { return CACHE_ROOT; }
        }

        public static string LastError
        {
            get { return _lastError ?? string.Empty; }
        }

        public static string ResolveDecompilerPath()
        {
            return ProcessManager.ResolveDecompilerPath();
        }

        public static bool IsCached(MethodBase method)
        {
            var cacheFile = GetCachePath(method);
            var mapFile = GetMapPath(method);
            if (!File.Exists(cacheFile) || !File.Exists(mapFile)) return false;

            var asmTime = File.GetLastWriteTimeUtc(method.Module.Assembly.Location);
            var cacheTime = File.GetLastWriteTimeUtc(cacheFile);
            return cacheTime >= asmTime;
        }

        public static string GetCachePath(MethodBase method)
        {
            return Path.Combine(CACHE_ROOT, BuildCacheFileName(method, ".cs"));
        }

        public static string GetMapPath(MethodBase method)
        {
            return Path.Combine(CACHE_ROOT, BuildCacheFileName(method, ".map"));
        }

        public static int MapILToSourceLine(MethodBase method, int ilOffset)
        {
            Dictionary<int, int> map;
            if (!TryReadMap(method, out map) || map.Count == 0)
            {
                return -1;
            }

            var bestLine = -1;
            var bestDistance = int.MaxValue;

            foreach (var kv in map)
            {
                var distance = Math.Abs(kv.Value - ilOffset);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLine = kv.Key;
                }
                else if (distance == bestDistance && kv.Value <= ilOffset)
                {
                    bestLine = kv.Key;
                }
            }

            return bestLine;
        }

        /// <summary>
        /// Resolves the best IL offset for a source line from the cached .map file.
        /// When an exact line is unavailable, returns the nearest mapped line offset.
        /// </summary>
        public static int MapSourceLineToILOffset(MethodBase method, int sourceLineNumber)
        {
            if (sourceLineNumber <= 0)
            {
                return -1;
            }

            Dictionary<int, int> map;
            if (!TryReadMap(method, out map) || map.Count == 0)
            {
                return -1;
            }

            int ilOffset;
            if (map.TryGetValue(sourceLineNumber, out ilOffset))
            {
                return ilOffset;
            }

            var bestLine = -1;
            var bestDistance = int.MaxValue;
            foreach (var kv in map)
            {
                var distance = Math.Abs(kv.Key - sourceLineNumber);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLine = kv.Key;
                }
                else if (distance == bestDistance && kv.Key < sourceLineNumber)
                {
                    bestLine = kv.Key;
                }
            }

            if (bestLine > 0 && map.TryGetValue(bestLine, out ilOffset))
            {
                return ilOffset;
            }

            return -1;
        }

        public static string GetSource(MethodBase method, int timeoutMs = 5000)
        {
            try
            {
                EnsureDirectories();

                var outputPath = GetCachePath(method);
                var mapPath = GetMapPath(method);

                if (IsCached(method))
                {
                    _lastError = string.Empty;
                    return File.ReadAllText(outputPath);
                }

                var decompilerPath = ProcessManager.ResolveDecompilerPath();
                if (!File.Exists(decompilerPath))
                {
                    _lastError = "Decompiler.exe not found at: " + decompilerPath;
                    return "// Error: Decompiler.exe not found.";
                }

                // Log the exact command for debugging
                var privacyArgs = ExternalProcessManager.BuildArgs(method.Module.Assembly.Location, method.MetadataToken) + " --privacy-check";
                MMLog.WriteDebug($"[SourceCacheManager] Probing privacy: {decompilerPath} {privacyArgs}");

                ModAPI.Inspector.ExternalProcessManager.PrivacyProbeResult privacy;
                try 
                {
                    privacy = ProcessManager.ProbePrivacy(decompilerPath, method.Module.Assembly.Location, method.MetadataToken, Math.Min(timeoutMs, 3000));
                }
                catch (Exception probeEx)
                {
                     // Capture the specific error details
                     MMLog.WriteError($"[SourceCacheManager] Privacy probe failed. Cmd: {decompilerPath}. Args: {privacyArgs}");
                     MMLog.WriteError($"[SourceCacheManager] Exception: {probeEx.Message}");
                     if (probeEx.InnerException != null) MMLog.WriteError($"[SourceCacheManager] Inner: {probeEx.InnerException.Message}");
                     throw;
                }

                if (privacy.Level == PrivacyLevel.Private)
                {
                    var source = "// [Private] Access denied by mod author.";
                    WriteCache(outputPath, mapPath, source, "LineNumber=ILOffset\n");
                    _lastError = string.Empty;
                    return source;
                }

                if (privacy.Level == PrivacyLevel.Obfuscated)
                {
                    var source = BuildObfuscatedStub(privacy.Signature, privacy.Reason);
                    WriteCache(outputPath, mapPath, source, "LineNumber=ILOffset\n");
                    _lastError = string.Empty;
                    return source;
                }

                var result = ProcessManager.RequestDecompileSource(
                    decompilerPath,
                    method.Module.Assembly.Location,
                    method.MetadataToken,
                    outputPath,
                    timeoutMs);

                if (result.TimedOut)
                {
                    throw new TimeoutException("Decompilation timed out");
                }

                if (result.ExitCode != 0)
                {
                    throw new Exception("Decompiler error: " + result.StandardError);
                }

                if (File.Exists(outputPath))
                {
                    if (!File.Exists(mapPath))
                    {
                        File.WriteAllText(mapPath, "LineNumber=ILOffset\n");
                    }

                    _lastError = string.Empty;
                    return File.ReadAllText(outputPath);
                }

                _lastError = "Decompiler completed but output file is missing: " + outputPath;
                return "// Error: Decompilation produced no output.";
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                MMLog.WriteError("SourceCacheManager Error: " + ex.Message);
                return "// Error retrieving source: " + ex.Message;
            }
        }

        private static bool TryReadMap(MethodBase method, out Dictionary<int, int> map)
        {
            map = new Dictionary<int, int>();
            var path = GetMapPath(method);
            if (!File.Exists(path))
            {
                return false;
            }

            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("LineNumber", StringComparison.OrdinalIgnoreCase)) continue;

                var eq = line.IndexOf('=');
                if (eq <= 0 || eq >= line.Length - 1) continue;

                int sourceLine;
                int ilOffset;
                if (!int.TryParse(line.Substring(0, eq).Trim(), out sourceLine)) continue;
                if (!int.TryParse(line.Substring(eq + 1).Trim(), out ilOffset)) continue;

                if (!map.ContainsKey(sourceLine))
                {
                    map[sourceLine] = ilOffset;
                }
            }

            return map.Count > 0;
        }

        private static string BuildCacheFileName(MethodBase method, string extension)
        {
            var assemblyName = method.Module.Assembly.GetName().Name ?? "Assembly";
            return assemblyName + "_0x" + method.MetadataToken.ToString("X8") + extension;
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(APPDATA_ROOT)) Directory.CreateDirectory(APPDATA_ROOT);
            if (!Directory.Exists(CACHE_ROOT)) Directory.CreateDirectory(CACHE_ROOT);
        }

        private static void WriteCache(string sourcePath, string mapPath, string source, string map)
        {
            File.WriteAllText(sourcePath, source ?? string.Empty);
            File.WriteAllText(mapPath, map ?? "LineNumber=ILOffset\n");
        }

        private static string BuildObfuscatedStub(string signature, string reason)
        {
            var sig = string.IsNullOrEmpty(signature) ? "void UnknownMethod()" : signature;
            var why = string.IsNullOrEmpty(reason) ? string.Empty : (": " + reason);
            return "[ModPrivacy(PrivacyLevel.Obfuscated)]\n" +
                   sig + "\n" +
                   "{\n" +
                   "    // [Obfuscated" + why + "] { /* Implementation hidden */ }\n" +
                   "}";
        }
    }
}
