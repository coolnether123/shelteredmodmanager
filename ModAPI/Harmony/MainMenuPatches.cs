using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Harmony
{
    [HarmonyPatch(typeof(MainMenu), "OnShow")]
    public static class MainMenu_OnShow_Patch
    {
        public static void Postfix(MainMenu __instance)
        {
            try
            {
                var tableField = typeof(MainMenu).GetField("m_table", BindingFlags.NonPublic | BindingFlags.Instance);
                var table = (UITablePivot)tableField?.GetValue(__instance);
                if (table == null) return;

                // Check if we already have a Mods button in this table instance
                foreach (Transform child in table.transform)
                {
                    if (child.name == "Button_Mods")
                    {
                        MMLog.WriteDebug("[MainMenuPatch] Mods button already exists in table.");
                        return;
                    }
                }

                UIButton templateBtn = null;
                if (table.children != null)
                {
                    foreach (var child in table.children)
                    {
                        if (child != null && (child.name.Contains("Options") || child.name.Contains("Exit") || child.name.Contains("Play")))
                        {
                            templateBtn = child.GetComponent<UIButton>();
                            if (templateBtn != null) break;
                        }
                    }
                }

                if (templateBtn == null) templateBtn = UIUtil.FindAnyButtonTemplate();
                if (templateBtn == null) return;

                var modsBtn = UIUtil.CloneButton(templateBtn, table.transform, "Mods");
                if (modsBtn != null)
                {
                    modsBtn.gameObject.name = "Button_Mods";
                    modsBtn.gameObject.layer = table.gameObject.layer;
                    
                    var labels = modsBtn.GetComponentsInChildren<UILabel>(true);
                    foreach (var l in labels)
                    {
                        if (l != null)
                        {
                            l.fontSize = 26; 
                            l.overflowMethod = UILabel.Overflow.ShrinkContent;
                        }
                    }

                    modsBtn.onClick.Clear();
                    EventDelegate.Add(modsBtn.onClick, () => HandleModsClick(__instance));
                    UIEventListener.Get(modsBtn.gameObject).onClick = (go) => HandleModsClick(__instance);

                    modsBtn.gameObject.SetActive(true);
                    table.Reposition();

                    var updateMethod = typeof(MainMenu).GetMethod("UpdateButtonTable", BindingFlags.NonPublic | BindingFlags.Instance);
                    updateMethod?.Invoke(__instance, null);

                    MMLog.Write("[MainMenuPatch] Injected Mods button with transition handling.");
                }
            }
            catch (Exception ex) { MMLog.Write("[MainMenuPatch] Exception: " + ex.Message); }
        }

        private static void HandleModsClick(MainMenu menu)
        {
            if (ModManagerPanel.IsShowingInstance) return;
            MMLog.Write("[MainMenuPatch] Mods button clicked - initiating transition.");
            MainMenu_OnTweenFinished_Patch.TransitioningToMods = true;
            menu.OnPlayButtonPressed(); // This triggers the fade-out
        }
    }

    [HarmonyPatch(typeof(MainMenu), "OnTweenFinished")]
    public static class MainMenu_OnTweenFinished_Patch
    {
        public static bool TransitioningToMods = false;

        public static bool Prefix(MainMenu __instance)
        {
            var tweenField = typeof(MainMenu).GetField("m_tween", BindingFlags.NonPublic | BindingFlags.Instance);
            var tween = (TweenAlpha)tweenField?.GetValue(__instance);

            if (TransitioningToMods && tween != null && tween.direction == AnimationOrTween.Direction.Reverse)
            {
                TransitioningToMods = false;
                ModManagerPanel.ShowPanel();
                return false; // Skip original logic (which would push GameModeSelectionPanel)
            }
            return true;
        }
    }
}
