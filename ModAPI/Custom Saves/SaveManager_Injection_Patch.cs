using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.Hooks;
using ModAPI.Saves;
using System;

namespace ModAPI.Hooks
{
    [PatchPolicy(PatchDomain.SaveFlow, "PlatformSaveProxyInjection",
        TargetBehavior = "SaveManager save-script replacement with PlatformSaveProxy",
        FailureMode = "Custom save virtualization never activates and save flow falls back to vanilla only.",
        RollbackStrategy = "Disable the SaveFlow patch domain or bypass proxy injection.")]
    [HarmonyPatch(typeof(SaveManager), "Awake")]
    internal static class SaveManager_Injection_Patch
    {
        static void Postfix(SaveManager __instance)
        {
            Inject(__instance);
        }

        // Public helper method to force injection immediately
        public static void Inject(SaveManager instance)
        {
            if (instance == null) return;

            try
            {
                var traverse = Traverse.Create(instance);
                var currentScript = traverse.Field("m_saveScript").GetValue<PlatformSave_Base>();

                // Only inject if it's NOT ALREADY a proxy
                if (currentScript != null && !(currentScript is PlatformSaveProxy))
                {
                    MMLog.WriteDebug("[SaveManager_Injection_Patch] Swapping vanilla PlatformSave_PC with PlatformSaveProxy.");

                    var proxy = new PlatformSaveProxy(currentScript);
                    
                    // Inject into private field
                    traverse.Field("m_saveScript").SetValue(proxy);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("FATAL ERROR during proxy injection: " + ex);
            }
        }
    }
}
