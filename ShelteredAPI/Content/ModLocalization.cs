using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Content
{
    /// <summary>
    /// ShelteredAPI-managed localization table to preserve literal casing and avoid
    /// fallback key mangling in vanilla Localization.Get.
    /// </summary>
    public static class ModLocalization
    {
        private static readonly Dictionary<string, string> _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Sync = new object();

        public static void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (Sync)
            {
                _entries[key] = value ?? string.Empty;
            }
        }

        public static bool TryGet(string key, out string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = null;
                return false;
            }

            lock (Sync)
            {
                return _entries.TryGetValue(key, out value);
            }
        }
    }

    [PatchPolicy(PatchDomain.Content, "ModLocalization",
        TargetBehavior = "Localization key interception for ShelteredAPI-managed entries",
        FailureMode = "Literal mod localization falls back to vanilla key lookup and casing can be mangled.",
        RollbackStrategy = "Disable the Content patch domain or remove the ModLocalization interception patch.")]
    [HarmonyPatch(typeof(Localization), "Get", new Type[] { typeof(string), typeof(bool) })]
    internal static class Localization_Get_ModApiPatch
    {
        private static bool Prefix(string key, ref string __result)
        {
            if (ModLocalization.TryGet(key, out var value))
            {
                __result = value;
                return false;
            }
            return true;
        }
    }
}
