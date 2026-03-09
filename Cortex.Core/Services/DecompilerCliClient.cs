using System;
using System.Diagnostics;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class DecompilerCliClient : IDecompilerClient
    {
        private readonly string _decompilerPath;
        private readonly string _cacheRoot;
        private readonly int _timeoutMs;

        public DecompilerCliClient(string decompilerPath, string cacheRoot, int timeoutMs)
        {
            _decompilerPath = decompilerPath;
            _cacheRoot = cacheRoot;
            _timeoutMs = timeoutMs <= 0 ? 15000 : timeoutMs;
        }

        public DecompilerResponse Decompile(DecompilerRequest request)
        {
            var response = new DecompilerResponse();
            if (request == null || string.IsNullOrEmpty(request.AssemblyPath) || string.IsNullOrEmpty(_decompilerPath))
            {
                response.StatusMessage = "Decompiler request is missing required values.";
                return response;
            }

            if (!Directory.Exists(_cacheRoot))
            {
                Directory.CreateDirectory(_cacheRoot);
            }

            var entityPrefix = request.EntityKind == DecompilerEntityKind.Type ? "type" : "method";
            var legacyFileKey = Path.GetFileNameWithoutExtension(request.AssemblyPath) + "_" + entityPrefix + "_0x" + request.MetadataToken.ToString("X8");
            var cachePathStem = !string.IsNullOrEmpty(request.CacheRelativePathStem)
                ? request.CacheRelativePathStem
                : legacyFileKey;
            response.CachePath = Path.Combine(_cacheRoot, cachePathStem + ".cs");
            response.MapPath = Path.Combine(_cacheRoot, cachePathStem + ".map");

            var cacheDirectory = Path.GetDirectoryName(response.CachePath);
            if (!string.IsNullOrEmpty(cacheDirectory) && !Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            if (!request.IgnoreCache && File.Exists(response.CachePath) && File.Exists(response.MapPath))
            {
                var assemblyWrite = File.GetLastWriteTimeUtc(request.AssemblyPath);
                var cacheWrite = File.GetLastWriteTimeUtc(response.CachePath);
                if (cacheWrite >= assemblyWrite)
                {
                    response.SourceText = File.ReadAllText(response.CachePath);
                    response.MapText = File.ReadAllText(response.MapPath);
                    response.FromCache = true;
                    response.StatusMessage = "Loaded from cache.";
                    return response;
                }
            }

            var legacyCachePath = Path.Combine(_cacheRoot, legacyFileKey + ".cs");
            var legacyMapPath = Path.Combine(_cacheRoot, legacyFileKey + ".map");
            if (!request.IgnoreCache &&
                !string.Equals(response.CachePath, legacyCachePath, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(legacyCachePath) &&
                File.Exists(legacyMapPath))
            {
                var assemblyWrite = File.GetLastWriteTimeUtc(request.AssemblyPath);
                var legacyCacheWrite = File.GetLastWriteTimeUtc(legacyCachePath);
                if (legacyCacheWrite >= assemblyWrite)
                {
                    File.Copy(legacyCachePath, response.CachePath, true);
                    File.Copy(legacyMapPath, response.MapPath, true);
                    response.SourceText = File.ReadAllText(response.CachePath);
                    response.MapText = File.ReadAllText(response.MapPath);
                    response.FromCache = true;
                    response.StatusMessage = "Loaded from legacy cache and migrated.";
                    return response;
                }
            }

            var process = new Process();
            process.StartInfo = new ProcessStartInfo(_decompilerPath,
                "--assembly " + Quote(request.AssemblyPath) + " --token " + request.MetadataToken + " --entity " + (request.EntityKind == DecompilerEntityKind.Type ? "type" : "method") + " --format source --output " + Quote(response.CachePath))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            if (!process.WaitForExit(_timeoutMs))
            {
                try { process.Kill(); } catch { }
                response.StatusMessage = "Decompiler timed out.";
                return response;
            }

            if (process.ExitCode != 0 || !File.Exists(response.CachePath))
            {
                response.StatusMessage = process.StandardError.ReadToEnd();
                return response;
            }

            response.SourceText = File.ReadAllText(response.CachePath);
            response.MapText = File.Exists(response.MapPath) ? File.ReadAllText(response.MapPath) : string.Empty;
            response.FromCache = false;
            response.StatusMessage = process.StandardOutput.ReadToEnd();
            return response;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
