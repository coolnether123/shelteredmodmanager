using HarmonyLib;
using UnityEngine;

namespace PluginHarmony
{
    [HarmonyPatch(typeof(CutsceneManager), "UpdateManager")]
    public static class SkipIntroAndTitlePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CutsceneManager __instance)
        {
            MMLog.Write("SkipIntroAndTitlePatch Prefix called"); // Using MMLog.Write

            if (PlatformInput.GetButtonDown(PlatformInput.InputButton.Action))
            {
                MMLog.Write("Left click pressed"); // Using MMLog.Write

                var cutsceneActiveField = AccessTools.Field(typeof(CutsceneManager), "cutsceneActive");
                var activeCutsceneField = AccessTools.Field(typeof(CutsceneManager), "activeCutscene");

                if (cutsceneActiveField != null && activeCutsceneField != null)
                {
                    var cutsceneActive = (bool)cutsceneActiveField.GetValue(__instance);
                    var activeCutscene = (Cutscene)activeCutsceneField.GetValue(__instance);

                    if (cutsceneActive && activeCutscene != null && activeCutscene.IsIntro)
                    {
                        MMLog.Write("Intro cutscene is active, skipping..."); // Using MMLog.Write

                        activeCutscene.SkipCutscene();
                        __instance.DeactivateCutscene();

                        var titleField = AccessTools.Field(typeof(CutsceneManager), "Title");
                        if (titleField != null)
                        {
                            var title = (GameObject)titleField.GetValue(__instance);
                            if (title != null)
                            {
                                title.SetActive(false);
                            }
                        }
                        return false; // Skip original UpdateManager
                    }
                }
            }
            return true; // Run original UpdateManager
        }
    }
}


