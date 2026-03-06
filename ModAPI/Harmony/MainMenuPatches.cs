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
    internal static class AutoLoadFlow
    {
        // Sentinel value from Manager: AutoLoadSaveSlot=-1 means "start a new game in lowest free slot".
        public const int NewSaveSentinel = -1;

        public static bool PendingNewSave = false;
        public static bool ModeChosen = false;
        public static bool SlotChosen = false;

        public static void BeginNewSave()
        {
            PendingNewSave = true;
            ModeChosen = false;
            SlotChosen = false;
        }

        public static void Reset()
        {
            PendingNewSave = false;
            ModeChosen = false;
            SlotChosen = false;
        }
    }

    [PatchPolicy(PatchDomain.UI, "MainMenuModsEntry",
        TargetBehavior = "Main menu mods button injection and manager-driven auto-load/new-save flow",
        FailureMode = "Mods entry or manager-driven auto-load flow fails to start from the main menu.",
        RollbackStrategy = "Disable the UI patch domain or remove the main menu patch host.")]
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

                // If we returned to Main Menu, the app is still alive and future load/save flows
                // must not run with stale quit state.
                if (PluginRunner.IsQuitting)
                {
                    PluginRunner.IsQuitting = false;
                    MMLog.WriteDebug("[MainMenu_OnShow] Resetting IsQuitting flag to FALSE.");
                }

                MMLog.WriteDebug("Postfix triggered.");
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

                    MMLog.WriteDebug("Injected Mods button with transition handling.");
                }
            }
            catch (Exception ex) { MMLog.Write("Exception: " + ex.Message); }
        }

        private static void HandleModsClick(MainMenu menu)
        {
            if (ModManagerPanel.IsShowingInstance) return;
            MMLog.WriteDebug("Mods button clicked - initiating transition.");
            MainMenu_OnTweenFinished_Patch.TransitioningToMods = true;
            menu.OnPlayButtonPressed(); // This triggers the fade-out
        }

        private static void HandleAutoLoad(MainMenu __instance)
        {
            try
            {
                int slot = HarmonyBootstrap.ReadManagerInt("AutoLoadSaveSlot", 0);
                if (slot == AutoLoadFlow.NewSaveSentinel)
                {
                    MMLog.Write("Auto-new-save requested. Navigating to new game flow.");
                    AutoLoadFlow.BeginNewSave();
                    __instance.OnPlayButtonPressed();
                    return;
                }

                if (slot <= 0)
                {
                    AutoLoadFlow.Reset();
                    return;
                }

                AutoLoadFlow.Reset();

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
                AutoLoadFlow.Reset();
                MMLog.WriteError("Failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.UI, "MainMenuTransitionRedirect",
        TargetBehavior = "Main menu transition redirect into the Mod Manager panel",
        FailureMode = "Main menu transitions return to vanilla flow instead of opening the Mod Manager panel.",
        RollbackStrategy = "Disable the UI patch domain or remove the menu transition redirect patch.")]
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

    [PatchPolicy(PatchDomain.SaveFlow, "AutoNewSaveModeSelection",
        TargetBehavior = "Automatic new-save mode selection during manager-driven flow",
        FailureMode = "Auto-new-save stalls before choosing a game mode.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the auto-new-save mode selector.")]
    [HarmonyPatch(typeof(GameModeSelectionPanel), "OnTweenFinished")]
    internal static class GameModeSelectionPanel_OnTweenFinished_AutoNewSave_Patch
    {
        static void Postfix(GameModeSelectionPanel __instance)
        {
            if (!AutoLoadFlow.PendingNewSave || AutoLoadFlow.ModeChosen) return;

            try
            {
                var tweenField = typeof(GameModeSelectionPanel).GetField("m_tween", BindingFlags.NonPublic | BindingFlags.Instance);
                var tween = (TweenAlpha)tweenField?.GetValue(__instance);
                if (tween != null && tween.direction == AnimationOrTween.Direction.Reverse) return;

                AutoLoadFlow.ModeChosen = true;
                MMLog.WriteDebug("[AutoLoad] Auto-selecting Survival mode for New Save.");
                __instance.OnSurvivalModeChosen();
            }
            catch (Exception ex)
            {
                AutoLoadFlow.Reset();
                MMLog.WriteError("[AutoLoad] Failed choosing mode: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "AutoNewSaveSlotSelection",
        TargetBehavior = "Automatic slot selection during manager-driven new-save flow",
        FailureMode = "Auto-new-save chooses the wrong slot or stalls before entering gameplay.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the auto-new-save slot selector.")]
    [HarmonyPatch(typeof(SlotSelectionPanel), "OnTweenFinished")]
    internal static class SlotSelectionPanel_OnTweenFinished_AutoNewSave_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            if (!AutoLoadFlow.PendingNewSave || AutoLoadFlow.SlotChosen) return;
            if (!__instance.m_inputEnabled) return;

            try
            {
                var tweenField = typeof(SlotSelectionPanel).GetField("m_tween", BindingFlags.NonPublic | BindingFlags.Instance);
                var tween = (TweenAlpha)tweenField?.GetValue(__instance);
                if (tween != null && tween.direction == AnimationOrTween.Direction.Reverse) return;

                AutoLoadFlow.SlotChosen = true;

                int lowestSlot = FindLowestAvailableSurvivalSlot();
                int targetPage;
                int targetIndex;

                if (lowestSlot <= 3)
                {
                    targetPage = 0;
                    targetIndex = lowestSlot - 1;
                }
                else
                {
                    int customOffset = lowestSlot - 4;
                    targetPage = (customOffset / 3) + 1;
                    targetIndex = customOffset % 3;
                }

                int currentPage = PagingManager.GetPage(__instance);
                while (currentPage < targetPage)
                {
                    int before = currentPage;
                    PagingManager.ChangePage(__instance, +1);
                    currentPage = PagingManager.GetPage(__instance);
                    if (currentPage == before) break;
                }

                while (currentPage > targetPage)
                {
                    int before = currentPage;
                    PagingManager.ChangePage(__instance, -1);
                    currentPage = PagingManager.GetPage(__instance);
                    if (currentPage == before) break;
                }

                Traverse.Create(__instance).Field("m_selectedSlot").SetValue(targetIndex);
                MMLog.Write($"[AutoLoad] Starting New Save in slot {lowestSlot} (page {targetPage}, index {targetIndex}).");

                __instance.OnSlotChosen();
                AutoLoadFlow.Reset();
            }
            catch (Exception ex)
            {
                AutoLoadFlow.Reset();
                MMLog.WriteError("[AutoLoad] Failed choosing New Save slot: " + ex.Message);
            }
        }

        private static int FindLowestAvailableSurvivalSlot()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                var info = SaveRegistryCore.ReadVanillaSaveInfo(slot);
                if (info == null) return slot;
            }

            int customSlot = 4;
            while (ExpandedVanillaSaves.GetBySlot(customSlot) != null)
            {
                customSlot++;
            }

            return customSlot;
        }
    }
}
