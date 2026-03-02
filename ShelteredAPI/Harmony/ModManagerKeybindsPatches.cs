using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.UI;
using ShelteredAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Adds a KEYBINDS button into the existing Mod Manager window.
    /// </summary>
    [HarmonyPatch(typeof(ModManagerPanel), "Initialise")]
    internal static class ModManagerKeybindsPatches
    {
        private const string ButtonName = "KeybindsButton";

        [HarmonyPostfix]
        private static void Postfix(ModManagerPanel __instance)
        {
            try
            {
                if (__instance == null || __instance.transform == null) return;
                if (__instance.transform.Find(ButtonName) != null) return;

                var settingsButtonTr = __instance.transform.Find("SettingsButton");
                if (settingsButtonTr == null)
                {
                    MMLog.WriteWarning("[ModManagerKeybindsPatches] SettingsButton not found; keybinds button not injected.");
                    return;
                }

                var settingsButton = settingsButtonTr.gameObject;
                var clone = UnityEngine.Object.Instantiate(settingsButton) as GameObject;
                if (clone == null) return;

                clone.name = ButtonName;
                clone.transform.SetParent(__instance.transform, false);
                clone.layer = __instance.gameObject.layer;
                clone.transform.localPosition = settingsButton.transform.localPosition + new Vector3(0f, -65f, 0f);
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;

                // Keep text/interaction deterministic after cloning.
                var labels = clone.GetComponentsInChildren<UILabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] == null) continue;
                    labels[i].text = "KEYBINDS";
                    labels[i].fontSize = 22;
                    labels[i].overflowMethod = UILabel.Overflow.ShrinkContent;
                }

                var btn = clone.GetComponent<UIButton>();
                if (btn != null && btn.onClick != null) btn.onClick.Clear();
                if (btn != null) btn.isEnabled = true;

                UIEventListener.Get(clone).onClick = _ => ShelteredKeybindsUI.Show();
                clone.SetActive(true);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModManagerKeybindsPatches] Failed to inject keybinds button: " + ex.Message);
            }
        }
    }
}
