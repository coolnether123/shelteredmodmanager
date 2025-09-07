using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace ModAPI.Hooks
{
    internal static class HarmonyBootstrap
    {
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
            catch { return false; }
        }

        private static void TryPatch()
        {
            if (_installed) return;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var harmony = new Harmony("ModAPI.CustomSaves");
                harmony.PatchAll(asm);
                _installed = true;
                MMLog.WriteDebug("HarmonyBootstrap: ModAPI hooks patched");
                if (_runnerGo != null) UnityEngine.Object.Destroy(_runnerGo);
            }
            catch (Exception ex)
            {
                MMLog.Write("HarmonyBootstrap patch attempt failed: " + ex.Message);
            }
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
        private const int MaxAttempts = 60; // ~30s at 0.5s interval

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
