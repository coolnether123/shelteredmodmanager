using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using ModAPI.Core;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Responsible for initializing Harmony and applying patches.
    /// Redirects 0Harmony log to MMLog.
    /// </summary>
    public static class HarmonyBootstrap
    {
        private static bool _installed = false;
        private static GameObject _runnerGo;

        static HarmonyBootstrap()
        {
            // Redirect Harmony's internal trace logs if needed
        }

        /// <summary>
        /// Entry point for mod loader to start patching.
        /// If Harmony DLL is not yet loaded by BepInEx or Doorstop,
        /// starts a runner to periodically retry.
        /// </summary>
        public static void EnsurePatched()
        {
            if (_installed) return;

            try
            {
                // Verify Harmony is available in current domain
                var type = typeof(HarmonyLib.Harmony);
                if (type == null)
                {
                    MMLog.Write("HarmonyLib not found. Starting retry runner...");
                    StartRetryRunner();
                    return;
                }

                TryPatch();
            }
            catch (Exception ex)
            {
                MMLog.Write("Initial patch check failed: " + ex.Message);
                StartRetryRunner();
            }
        }

        private static void TryPatch()
        {
            if (_installed) return;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var harmony = new HarmonyLib.Harmony("ModAPI.Core");

                var opts = new ModAPI.Harmony.HarmonyUtil.PatchOptions
                {
                    AllowDebugPatches = ReadManagerBool("EnableDebugPatches", false),
                    AllowDangerousPatches = ReadManagerBool("AllowDangerousPatches", false),
                    AllowStructReturns = ReadManagerBool("AllowStructReturns", false),
                    OnResult = (obj, reason) =>
                    {
                        if (reason != null)
                        {
                            var mb = obj as MemberInfo;
                            var name = mb != null ? (mb.DeclaringType != null ? mb.DeclaringType.Name + "." + mb.Name : mb.Name) : (obj?.ToString() ?? "<null>");
                            MMLog.WriteDebug($"{name} -> {reason}");
                        }
                    }
                };

                var registryOptions = PatchRegistry.CreateManagerOptions(
                    opts,
                    asm.GetName().Name,
                    key => ReadManagerString(key, null));
                var corePatchTimer = Stopwatch.StartNew();
                PatchRegistry.ApplyAssembly(harmony, asm, registryOptions);
                LogStartupTiming("Harmony patch " + asm.GetName().Name, corePatchTimer);

                var runtimePatchTimer = Stopwatch.StartNew();
                PatchGameRuntimeAssemblies(harmony, asm, opts);
                LogStartupTiming("Harmony patch game runtime assemblies", runtimePatchTimer);

                _installed = true;

                MMLog.WriteDebug("ModAPI hooks patched");
                if (_runnerGo != null) UnityEngine.Object.Destroy(_runnerGo);
            }
            catch (Exception ex)
            {
                MMLog.Write("patch attempt failed: " + ex.Message);
            }
        }

        public static bool ReadManagerBool(string key, bool fallback)
        {
            string s = ReadManagerString(key, null);
            if (s == null) return fallback;
            
            bool b;
            if (bool.TryParse(s, out b)) return b;
            
            var lower = s.ToLowerInvariant();
            if (lower == "1" || lower == "yes" || lower == "y" || lower == "on" || lower == "true") return true;
            if (lower == "0" || lower == "no" || lower == "n" || lower == "off" || lower == "false") return false;
            
            return fallback;
        }

        public static string ReadManagerString(string key, string fallback)
        {
            try
            {
                string gameRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
                string smmDir = System.IO.Path.Combine(gameRoot, "SMM");
                string binDir = System.IO.Path.Combine(smmDir, "bin");
                var ini = System.IO.Path.Combine(binDir, "mod_manager.ini");
                if (!System.IO.File.Exists(ini)) return fallback;
                foreach (var raw in System.IO.File.ReadAllLines(ini))
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    var line = raw.Trim();
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;
                    var idx = line.IndexOf('='); if (idx <= 0) continue;
                    var k = line.Substring(0, idx).Trim();
                    var v = line.Substring(idx + 1).Trim();
                    if (k.Equals(key, StringComparison.OrdinalIgnoreCase)) return v;
                }
            }
            catch { }
            return fallback;
        }

        public static int ReadManagerInt(string key, int fallback)
        {
            string s = ReadManagerString(key, null);
            if (s != null && int.TryParse(s, out int val)) return val;
            return fallback;
        }

        private static void StartRetryRunner()
        {
            if (_runnerGo != null) return;
            _runnerGo = new GameObject("HarmonyRetryRunner");
            UnityEngine.Object.DontDestroyOnLoad(_runnerGo);
            _runnerGo.AddComponent<HarmonyRetryRunner>();
        }

        private static void PatchGameRuntimeAssemblies(HarmonyLib.Harmony harmony, Assembly coreAssembly, HarmonyUtil.PatchOptions opts)
        {
            Assembly[] assemblies = SharedAssemblyResolver.LoadAvailableSharedRuntimeAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly runtimeAssembly = assemblies[i];
                if (runtimeAssembly == null || IsSameAssembly(runtimeAssembly, coreAssembly))
                    continue;

                if (!ContainsGameRuntimeBootstrap(runtimeAssembly))
                    continue;

                try
                {
                    string location = "<dynamic>";
                    try { location = runtimeAssembly.Location; } catch { }

                    MMLog.WriteInfo("HarmonyBootstrap: applying game runtime patches from "
                        + runtimeAssembly.GetName().Name + " v" + runtimeAssembly.GetName().Version
                        + " @" + location);

                    var runtimeOptions = PatchRegistry.CreateManagerOptions(
                        opts,
                        runtimeAssembly.GetName().Name,
                        key => ReadManagerString(key, null));

                    var patchTimer = Stopwatch.StartNew();
                    PatchRegistry.ApplyAssembly(harmony, runtimeAssembly, runtimeOptions);
                    LogStartupTiming("Harmony patch " + runtimeAssembly.GetName().Name, patchTimer);
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("HarmonyBootstrap: game runtime patch scan failed for "
                        + SafeAssemblyName(runtimeAssembly) + ": " + ex.Message);
                }
            }
        }

        private static bool ContainsGameRuntimeBootstrap(Assembly assembly)
        {
            try
            {
                foreach (Type type in HarmonyUtil.SafeTypes(assembly))
                {
                    if (type != null
                        && type.IsClass
                        && !type.IsAbstract
                        && typeof(IGameRuntimeBootstrap).IsAssignableFrom(type))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsSameAssembly(Assembly left, Assembly right)
        {
            if (ReferenceEquals(left, right))
                return true;

            try { return left != null && right != null && left.FullName == right.FullName; }
            catch { return false; }
        }

        private static string SafeAssemblyName(Assembly assembly)
        {
            try { return assembly != null ? assembly.GetName().Name : "<null>"; }
            catch { return "<unknown>"; }
        }

        private static void LogStartupTiming(string phaseName, Stopwatch timer)
        {
            if (timer == null)
                return;

            timer.Stop();
            MMLog.WriteWithSource(
                MMLog.LogLevel.Info,
                MMLog.LogCategory.General,
                "StartupTiming",
                phaseName + " took " + timer.ElapsedMilliseconds + "ms.");
        }
    }

    internal class HarmonyRetryRunner : MonoBehaviour
    {
        private float _timer;
        private int _attempts;
        private const int MaxAttempts = 60;

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < 0.5f) return;
            _timer = 0f;
            _attempts++;
            MMLog.WriteDebug($"attempt {_attempts}");
            HarmonyBootstrap.EnsurePatched();
            if (_attempts >= MaxAttempts)
            {
                MMLog.Write("giving up waiting for 0Harmony");
                Destroy(this.gameObject);
            }
        }
    }
}
