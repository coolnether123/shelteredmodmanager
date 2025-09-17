using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace ModAPI.Harmony
{
    internal static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            MMLog.Write("[HarmonyBootstrap] Static constructor called. ModAPI.dll is loading.");
        }

        private static bool _installed;
        private static GameObject _runnerGo;

        public static void EnsurePatched()
        {
            if (_installed) return;
            if (!IsHarmonyAvailable())
            {
                MMLog.WriteDebug("HarmonyBootstrap: 0Harmony not available yet; scheduling retry");
                StartRetryRunner();
                return;
            }
            TryPatch();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnUnityLoad()
        {
            MMLog.WriteDebug("HarmonyBootstrap: RuntimeInitializeOnLoadMethod fired");
            EnsurePatched();
        }

        private static bool IsHarmonyAvailable()
        {
            try
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies();
                if (loaded.Any(a => string.Equals(a.GetName().Name, "0Harmony", StringComparison.OrdinalIgnoreCase)))
                    return true;
                var t = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
                return t != null;
            }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyBootstrap.IsHarmonyAvailable", "Error checking for Harmony: " + ex.Message); return false; }
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
                    OnResult = (mb, reason) =>
                    {
                        try
                        {
                            var who = mb is MethodBase ? ((MethodBase)mb).DeclaringType.FullName + "." + ((MethodBase)mb).Name : (mb != null ? mb.ToString() : "<null>");
                            MMLog.WriteDebug("[HarmonyBootstrap] " + who + " -> " + reason);
                        }
                        catch (Exception ex) { MMLog.WarnOnce("HarmonyBootstrap.OnResult", "Error in OnResult callback: " + ex.Message); }
                    }
                };

                ModAPI.Harmony.HarmonyUtil.PatchAll(harmony, asm, opts);
                _installed = true;

                MMLog.WriteDebug("HarmonyBootstrap: ModAPI hooks patched");
                if (_runnerGo != null) UnityEngine.Object.Destroy(_runnerGo);
            }
            catch (Exception ex)
            {
                MMLog.Write("HarmonyBootstrap patch attempt failed: " + ex.Message);
            }
        }

        private static bool ReadManagerBool(string key, bool fallback)
        {
            try
            {
                var ini = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "SMM"), "mod_manager.ini");
                if (!System.IO.File.Exists(ini)) return fallback;
                foreach (var raw in System.IO.File.ReadAllLines(ini))
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    var line = raw.Trim();
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;
                    var idx = line.IndexOf('='); if (idx <= 0) continue;
                    var k = line.Substring(0, idx).Trim();
                    var v = line.Substring(idx + 1).Trim();
                    if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                    bool b;
                    if (bool.TryParse(v, out b)) return b;
                    var s = v.ToLowerInvariant();
                    if (s == "1" || s == "yes" || s == "y" || s == "on") return true;
                    if (s == "0" || s == "no" || s == "n" || s == "off") return false;
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyBootstrap.ReadManagerBool", "Error reading mod_manager.ini: " + ex.Message); }
            return fallback;
        }

        private static void StartRetryRunner()
        {
            if (_runnerGo != null) return;
            _runnerGo = new GameObject("HarmonyRetryRunner");
            UnityEngine.Object.DontDestroyOnLoad(_runnerGo);
            _runnerGo.AddComponent<HarmonyRetryRunner>();
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
            MMLog.WriteDebug($"HarmonyRetryRunner: attempt {_attempts}");
            HarmonyBootstrap.EnsurePatched();
            if (_attempts >= MaxAttempts)
            {
                MMLog.Write("HarmonyRetryRunner: giving up waiting for 0Harmony");
                Destroy(this.gameObject);
            }
        }
    }
}
