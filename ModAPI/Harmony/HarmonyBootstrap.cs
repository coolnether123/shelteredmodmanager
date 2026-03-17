using System;
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
                var harmony = new HarmonyLib.Harmony("ShelteredModManager.ModAPI");

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
                PatchRegistry.ApplyAssembly(harmony, asm, registryOptions);

                // Backward compatibility: patch ShelteredAPI too so Sheltered-specific
                // implementations and adapters are activated alongside core ModAPI hooks.
                var shelteredAssembly = ResolveShelteredApiAssembly();
                if (shelteredAssembly != null && shelteredAssembly != asm)
                {
                    string location = "<dynamic>";
                    try { location = shelteredAssembly.Location; } catch { }
                    MMLog.WriteInfo("HarmonyBootstrap: applying ShelteredAPI patches from "
                        + shelteredAssembly.GetName().Name + " v" + shelteredAssembly.GetName().Version
                        + " @" + location);
                    var shelteredRegistryOptions = PatchRegistry.CreateManagerOptions(
                        opts,
                        shelteredAssembly.GetName().Name,
                        key => ReadManagerString(key, null));
                    PatchRegistry.ApplyAssembly(harmony, shelteredAssembly, shelteredRegistryOptions);
                    TryInitializeShelteredApiCore(shelteredAssembly);
                }
                else
                {
                    MMLog.WriteInfo("HarmonyBootstrap: ShelteredAPI assembly not found for patching (or same as ModAPI).");
                }

                LogPatchStatus("SettingsPCPanel.OnControlsButtonPressed", "SettingsPCPanel", "OnControlsButtonPressed");
                LogPatchStatus("SettingsPCPanel.OnControlsButtonPressed_PAD", "SettingsPCPanel", "OnControlsButtonPressed_PAD");
                LogPatchStatus("UIPanelManager.PushPanel(BasePanel)", "UIPanelManager", "PushPanel", "BasePanel");
                LogPatchStatus("PlatformInput_PC.GetButtonDown(InputButton)", "PlatformInput_PC", "GetButtonDown", "PlatformInput+InputButton");
                LogPatchStatus("PlatformInput_PC.GetButtonDown(MenuInputButton)", "PlatformInput_PC", "GetButtonDown", "PlatformInput+MenuInputButton");

                // Explicitly verify UIPatches was discovered and patched
                var uiPatches = asm.GetType("ModAPI.UI.UIPatches");
                if (uiPatches != null)
                {
                    MMLog.WriteDebug("Discovered UIPatches for verification.");
                }

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

        private static Assembly ResolveShelteredApiAssembly()
        {
            return SharedAssemblyResolver.ResolveSharedAssembly("ShelteredAPI");
        }

        private static void StartRetryRunner()
        {
            if (_runnerGo != null) return;
            _runnerGo = new GameObject("HarmonyRetryRunner");
            UnityEngine.Object.DontDestroyOnLoad(_runnerGo);
            _runnerGo.AddComponent<HarmonyRetryRunner>();
        }

        private static void TryInitializeShelteredApiCore(Assembly shelteredAssembly)
        {
            if (shelteredAssembly == null) return;

            try
            {
                var bootstrapType = shelteredAssembly.GetType("ShelteredAPI.Core.ShelteredApiRuntimeBootstrap", false);
                if (bootstrapType == null)
                {
                    MMLog.WriteInfo("HarmonyBootstrap: ShelteredAPI runtime bootstrap type not found.");
                    return;
                }

                var init = bootstrapType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (init == null)
                {
                    MMLog.WriteInfo("HarmonyBootstrap: ShelteredAPI runtime bootstrap Initialize method not found.");
                    return;
                }

                init.Invoke(null, null);
                MMLog.WriteInfo("HarmonyBootstrap: ShelteredAPI runtime bootstrap initialized.");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("HarmonyBootstrap: ShelteredAPI runtime bootstrap failed: " + ex.Message);
            }
        }

        private static void LogPatchStatus(string label, string typeName, string methodName, string parameterTypeName = null)
        {
            try
            {
                Type targetType = AccessTools.TypeByName(typeName);
                if (targetType == null)
                {
                    MMLog.WriteDebug("HarmonyBootstrap: patch verify target type missing: " + label);
                    return;
                }

                MethodBase target = null;
                if (string.IsNullOrEmpty(parameterTypeName))
                {
                    target = AccessTools.Method(targetType, methodName);
                }
                else
                {
                    Type parameterType = AccessTools.TypeByName(parameterTypeName);
                    if (parameterType == null)
                    {
                        MMLog.WriteDebug("HarmonyBootstrap: patch verify parameter type missing for " + label + ": " + parameterTypeName);
                        return;
                    }
                    target = AccessTools.Method(targetType, methodName, new[] { parameterType });
                }

                if (target == null)
                {
                    MMLog.WriteDebug("HarmonyBootstrap: patch verify target method missing: " + label);
                    return;
                }

                var info = HarmonyLib.Harmony.GetPatchInfo(target);
                bool patched = info != null && (
                    (info.Prefixes != null && info.Prefixes.Count > 0) ||
                    (info.Postfixes != null && info.Postfixes.Count > 0) ||
                    (info.Transpilers != null && info.Transpilers.Count > 0));

                MMLog.WriteDebug("HarmonyBootstrap: patch verify " + label + " => " + (patched ? "patched" : "not patched"));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("HarmonyBootstrap: patch verify failed for " + label + ": " + ex.Message);
            }
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
