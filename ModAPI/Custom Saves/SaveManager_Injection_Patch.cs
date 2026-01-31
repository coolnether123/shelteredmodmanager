using HarmonyLib;
using ModAPI.Core;
using ModAPI.Hooks;
using ModAPI.Saves;
using System;

namespace ModAPI.Hooks
{
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
                    MMLog.Write("--------------------------------------------------");
                    MMLog.Write("Swapping vanilla PlatformSave_PC with PlatformSaveProxy.");
                    MMLog.Write("--------------------------------------------------");

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
