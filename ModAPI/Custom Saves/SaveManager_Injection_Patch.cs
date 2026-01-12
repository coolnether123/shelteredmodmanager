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
            try
            {
                // 1. Get the current vanilla saver
                var traverse = Traverse.Create(__instance);
                var vanillaScript = traverse.Field("m_saveScript").GetValue<PlatformSave_Base>();

                // 2. Check if we already injected it (to prevent double injection)
                if (vanillaScript != null && !(vanillaScript is PlatformSaveProxy))
                {
                    MMLog.Write("[Injection] Swapping vanilla PlatformSave_PC with PlatformSaveProxy.");
                    
                    // 3. Wrap the vanilla saver in our Proxy
                    var proxy = new PlatformSaveProxy(vanillaScript);
                    
                    // 4. Inject it back into the private field
                    traverse.Field("m_saveScript").SetValue(proxy);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[Injection] Critical error during SaveManager proxy injection: " + ex);
            }
        }
    }
}
