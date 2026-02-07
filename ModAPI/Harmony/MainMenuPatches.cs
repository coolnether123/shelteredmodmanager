using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.UI;
using ModAPI.Saves;
using ModAPI.Hooks;
using ModAPI.Hooks.Paging;
using UnityEngine;

namespace ModAPI.Harmony
{
    [HarmonyPatch(typeof(MainMenu), "OnShow")]
    public static class MainMenu_OnShow_Patch
    {
        private static bool _autoLoadChecked = false;

        public static void Postfix(MainMenu __instance)
        {
            try
            {
                if (!_autoLoadChecked)
                {
                    _autoLoadChecked = true;
                    HandleAutoLoad(__instance);
                }

                // If we returned to the Main Menu (e.g. from "Save & Exit" that didn't actually quit app),
                // we must reset the quitting flag so that SaveRegistryCore can parse metadata again.
                // USER DIRECTIVE: STOP OVER-ENGINEERING. DO NOT RESET IsQuitting.
                /* 
                if (PluginRunner.IsQuitting)
                {
                    PluginRunner.IsQuitting = false;
                    MMLog.WriteDebug("[MainMenu_OnShow] Resetting IsQuitting flag to FALSE.");
                }
                */

                MMLog.Write("Postfix triggered.");
                // One-time startup check for save slot gaps
                SaveCondenseManager.CheckOnStartup();
                if (SaveCondenseManager.NeedsPrompt())
                {
                    CondensePromptDialog.Show();
                }

                var tableField = typeof(MainMenu).GetField("m_table", BindingFlags.NonPublic | BindingFlags.Instance);
                var table = (UITablePivot)tableField?.GetValue(__instance);
                if (table == null) return;

                // Check if we already have a Mods button in this table instance
                foreach (Transform child in table.transform)
                {
                    if (child.name == "Button_Mods")
                    {
                        MMLog.WriteDebug("Mods button already exists in table.");
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
                            l.fontSize = 32; 
                            l.overflowMethod = UILabel.Overflow.ShrinkContent;
                        }
                    }

                    modsBtn.onClick.Clear();
                    EventDelegate.Add(modsBtn.onClick, () => HandleModsClick(__instance));

                    modsBtn.gameObject.SetActive(true);
                    table.Reposition();

                    var updateMethod = typeof(MainMenu).GetMethod("UpdateButtonTable", BindingFlags.NonPublic | BindingFlags.Instance);
                    updateMethod?.Invoke(__instance, null);

                    MMLog.Write("Injected Mods button with transition handling.");
                }
            }
            catch (Exception ex) { MMLog.Write("Exception: " + ex.Message); }
        }

        private static void HandleModsClick(MainMenu menu)
        {
            if (ModManagerPanel.IsShowingInstance) return;
            MMLog.Write("Mods button clicked - initiating transition.");
            MainMenu_OnTweenFinished_Patch.TransitioningToMods = true;
            menu.OnPlayButtonPressed(); // This triggers the fade-out
        }

        private static void HandleAutoLoad(MainMenu __instance)
        {
            try
            {
                int slot = HarmonyBootstrap.ReadManagerInt("AutoLoadSaveSlot", 0);
                if (slot <= 0) return;

                MMLog.Write($"Auto-loading save slot {slot} requested via config.");

                if (slot <= 3)
                {
                    // Vanilla Load
                    var info = SaveRegistryCore.ReadVanillaSaveInfo(slot);
                    if (info == null)
                    {
                        MMLog.Write("Vanilla slot empty or unreadable. Ignoring.");
                        return;
                    }

                    DifficultyManager.StoreMenuDifficultySettings(
                        info.rainDiff, info.resourceDiff, info.breachDiff, info.factionDiff, 
                        info.moodDiff, info.mapSize, info.fog);

                    SaveManager.instance.SetSlotToLoad(slot);
                    MMLog.Write($"Initiated vanilla load for slot {slot}");
                }
                else
                {
                    // Custom Load
                    var entry = ExpandedVanillaSaves.GetBySlot(slot);
                    if (entry == null || !System.IO.File.Exists(DirectoryProvider.EntryPath("Standard", slot)))
                    {
                        MMLog.Write($"Custom slot {slot} empty or missing. Ignoring.");
                        return;
                    }

                    // For auto-load, we use Slot 1 as the proxy carrier
                    var virtualSaveType = SaveManager.SaveType.Slot1;
                    PlatformSaveProxy.SetNextLoad(virtualSaveType, "Standard", entry.id);

                    DifficultyManager.StoreMenuDifficultySettings(
                        entry.saveInfo.rainDiff, entry.saveInfo.resourceDiff, entry.saveInfo.breachDiff, 
                        entry.saveInfo.factionDiff, entry.saveInfo.moodDiff, entry.saveInfo.mapSize, 
                        entry.saveInfo.fog);

                    SaveManager.instance.SetSlotToLoad(1);
                    MMLog.Write($"Initiated custom load for slot {slot} via virtual slot 1");
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed: " + ex.Message);
            }
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
