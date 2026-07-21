using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.Internal;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Paging;
using ShelteredAPI.Saves.Runtime;
using UnityEngine;


using ShelteredAPI.UI.Internal.ModManager;
namespace ShelteredAPI.Harmony
{
    [PatchPolicy(PatchDomain.UI, "MainMenuModsEntry",
        TargetBehavior = "Main menu mods button injection and manager-driven auto-load/new-save flow",
        FailureMode = "Mods entry or manager-driven auto-load flow fails to start from the main menu.",
        RollbackStrategy = "Disable the UI patch domain or remove the main menu patch host.",
        StartupTiming = PatchStartupTiming.BootCritical)]
    [HarmonyPatch(typeof(MainMenu), "OnShow")]
    internal static class MainMenu_OnShow_Patch
    {
        private static bool _autoLoadChecked = false;

        public static void Postfix(MainMenu __instance)
        {
            try
            {
                bool managerDrivenLaunch = false;
                if (!_autoLoadChecked)
                {
                    _autoLoadChecked = true;
                    ShelteredDeferredPatchTriggers.ApplyMenuCritical("MainMenu.OnShow");
                    int autoLoadSlot = HarmonyBootstrap.ReadManagerInt("AutoLoadSaveSlot", 0);
                    managerDrivenLaunch = autoLoadSlot != 0;
                    if (managerDrivenLaunch)
                    {
                        ShelteredDeferredPatchTriggers.ApplySaveFlowCritical("MainMenu.OnShow auto-load");
                        ShelteredDeferredPatchTriggers.ApplyGameplayDeferred("MainMenu.OnShow auto-load");
                    }
                    HandleAutoLoad(__instance);
                }

                // If we returned to Main Menu, the app is still alive and future load/save flows
                // must not run with stale quit state.
                if (ModRuntime.IsQuitting)
                {
                    ResetModRuntimeQuitState();
                    MMLog.WriteDebug("[MainMenu_OnShow] Resetting IsQuitting flag to FALSE.");
                }

                MMLog.WriteDebug("Postfix triggered.");
                UIFontCache.SeedFromGameObject(__instance.gameObject, "main menu");
                ModManagerPanelScaffolding.WarmScenarioBookVisualCache();
                ShelteredAPI.Scenarios.Presentation.Selection.ScenarioBookPrewarmService.TryStart(__instance);

                TryShowStartupCondensePrompt(managerDrivenLaunch);

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

        private static void TryShowStartupCondensePrompt(bool suppressForManagerLaunch)
        {
            SaveCondenseManager.CheckOnStartup();
            if (!SaveCondenseManager.NeedsPrompt())
                return;

            if (suppressForManagerLaunch)
            {
                MMLog.WriteDebug("[MainMenu_OnShow] Suppressed save condense prompt during manager-driven launch.");
                return;
            }

            CondensePromptDialog.Show();
        }

        private static void ResetModRuntimeQuitState()
        {
            MethodInfo resetMethod = typeof(ModRuntime).GetMethod(
                "ResetQuitStateForHost",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (resetMethod == null)
            {
                MMLog.WriteError("[MainMenu_OnShow] Could not find ModRuntime quit-state reset method.");
                return;
            }

            resetMethod.Invoke(null, null);
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
        RollbackStrategy = "Disable the UI patch domain or remove the menu transition redirect patch.",
        StartupTiming = PatchStartupTiming.MenuCritical)]
    [HarmonyPatch(typeof(MainMenu), "OnTweenFinished")]
    internal static class MainMenu_OnTweenFinished_Patch
    {
        public static bool TransitioningToMods = false;

        public static bool Prefix(MainMenu __instance)
        {
            var tweenField = typeof(MainMenu).GetField("m_tween", BindingFlags.NonPublic | BindingFlags.Instance);
            var tween = (TweenAlpha)tweenField?.GetValue(__instance);

            if (!TransitioningToMods && tween != null && tween.direction == AnimationOrTween.Direction.Reverse)
            {
                ShelteredDeferredPatchTriggers.ApplySaveFlowCritical("MainMenu.OnTweenFinished play transition");
                ShelteredDeferredPatchTriggers.ApplyGameplayDeferred("MainMenu.OnTweenFinished play transition");
            }

            if (TransitioningToMods && tween != null && tween.direction == AnimationOrTween.Direction.Reverse)
            {
                TransitioningToMods = false;
                ModManagerPanel.ShowPanel();
                return false; // Skip original logic (which would push GameModeSelectionPanel)
            }
            return true;
        }

        public static void Postfix(MainMenu __instance)
        {
            if (!AutoLoadFlow.NeedsMainMenuAdvance)
                return;

            var tweenField = typeof(MainMenu).GetField("m_tween", BindingFlags.NonPublic | BindingFlags.Instance);
            var tween = (TweenAlpha)tweenField?.GetValue(__instance);
            if (tween == null || tween.direction == AnimationOrTween.Direction.Reverse)
                return;

            AutoLoadFlow.TryAdvanceMainMenu(__instance);
        }
    }

    [PatchPolicy(PatchDomain.UI, "MainMenuAutoNewSaveRetry",
        TargetBehavior = "Frame-based retry for manager-driven auto-new-save main menu advancement",
        FailureMode = "Auto-new-save can stall on the main menu if the tween callback fires before input is enabled.",
        RollbackStrategy = "Disable the UI patch domain or remove the main menu retry patch.",
        StartupTiming = PatchStartupTiming.BootCritical)]
    [HarmonyPatch(typeof(MainMenu), "Update")]
    internal static class MainMenu_Update_AutoNewSave_Patch
    {
        static void Postfix(MainMenu __instance)
        {
            AutoLoadFlow.TryAdvanceMainMenu(__instance);
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "AutoNewSaveModeSelection",
        TargetBehavior = "Automatic new-save mode selection during manager-driven flow",
        FailureMode = "Auto-new-save stalls before choosing a game mode.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the auto-new-save mode selector.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(GameModeSelectionPanel), "OnTweenFinished")]
    internal static class GameModeSelectionPanel_OnTweenFinished_AutoNewSave_Patch
    {
        static void Postfix(GameModeSelectionPanel __instance)
        {
            AutoLoadFlow.TryChooseMode(__instance);
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "AutoNewSaveModeSelectionRetry",
        TargetBehavior = "Frame-based retry for automatic new-save mode selection",
        FailureMode = "Auto-new-save can stall on game mode selection if the tween callback fires before input is enabled.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the auto-new-save mode retry patch.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(GameModeSelectionPanel), "Update")]
    internal static class GameModeSelectionPanel_Update_AutoNewSave_Patch
    {
        static void Postfix(GameModeSelectionPanel __instance)
        {
            AutoLoadFlow.TryChooseMode(__instance);
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "AutoNewSaveSlotSelection",
        TargetBehavior = "Automatic slot selection during manager-driven new-save flow",
        FailureMode = "Auto-new-save chooses the wrong slot or stalls before entering gameplay.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the auto-new-save slot selector.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(SlotSelectionPanel), "OnTweenFinished")]
    internal static class SlotSelectionPanel_OnTweenFinished_AutoNewSave_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            AutoLoadFlow.TryChooseSlot(__instance);
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "AutoNewSaveSlotSelectionRetry",
        TargetBehavior = "Frame-based retry for automatic slot selection during manager-driven new-save flow",
        FailureMode = "Auto-new-save can stall on slot selection if save metadata loading or tween timing delays input.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the auto-new-save slot retry patch.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(SlotSelectionPanel), "Update")]
    internal static class SlotSelectionPanel_Update_AutoNewSave_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            AutoLoadFlow.TryChooseSlot(__instance);
        }
    }
}
