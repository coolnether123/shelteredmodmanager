using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Saves.Paging;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Runtime;
using ModAPI.Scenarios;
using ModAPI.UI;
using UnityEngine;

using ShelteredAPI.Infrastructure;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Registration;
using ShelteredAPI.Scenarios.Shared;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Infrastructure.Harmony{
    internal static class ShelteredCustomScenarioRuntimeState
    {
        private static int _lastLoggedBlockedUntilFrame = -1;

        public static bool IsSlotClickBlocked
        {
            get { return UIFlowGuard.IsSlotClickBlocked; }
        }

        public static bool ShouldBlockSlotInteraction(Component component)
        {
            return UIFlowGuard.IsSlotClickBlocked;
        }

        public static void BlockSlotClicksBriefly()
        {
            UIFlowGuard.BlockSlotClicksForFrames(2);
            int blockedUntilFrame = UIFlowGuard.BlockSlotClicksUntilFrame;
            if (_lastLoggedBlockedUntilFrame != blockedUntilFrame)
            {
                _lastLoggedBlockedUntilFrame = blockedUntilFrame;
                MMLog.WriteDebug("[ShelteredCustomScenarioSelection] Slot clicks blocked until frame " + blockedUntilFrame
                    + " (current=" + Time.frameCount + ").");
            }
        }

        public static bool HasPendingCustomScenario()
        {
            CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
            return state != null && state.LifecycleState == CustomScenarioLifecycleState.Pending && !string.IsNullOrEmpty(state.ScenarioId);
        }

        public static void ClearPendingCustomScenario()
        {
            CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
            if (state != null && state.LifecycleState == CustomScenarioLifecycleState.Pending)
            {
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Clearing pending custom scenario state. scenarioId=" + state.ScenarioId + ".");
                ShelteredCustomScenarioService.Instance.ClearState();
            }
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSelection",
        TargetBehavior = "Custom scenario entries are surfaced in the vanilla Sheltered scenario selection panel.",
        FailureMode = "Registered custom scenarios are unavailable from the in-game scenario selection flow.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario selection patch host.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(ScenarioSelectionPanel))]
    internal static class ShelteredCustomScenarioSelectionPatches
    {
        private static ShelteredScenarioSelectionBrowserController Controller
        {
            get { return ShelteredScenarioSelectionBrowserController.Instance; }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnShow")]
        private static void OnShowPostfix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
        {
            Controller.Initialize(__instance, ___m_scenarioButtons);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnScenarioSelected")]
        private static bool OnScenarioSelectedPrefix(
            ScenarioSelectionPanel __instance,
            int ___m_selectedScenario,
            UILabel ___m_scenarioNameLabel,
            UILabel ___m_scenarioDescLabel,
            UILabel ___m_scenarioHighScore,
            GameObject ___m_stasis_scoreLabelsRoot)
        {
            return Controller.HandleScenarioSelected(
                __instance,
                ___m_selectedScenario,
                ___m_scenarioNameLabel,
                ___m_scenarioDescLabel,
                ___m_scenarioHighScore,
                ___m_stasis_scoreLabelsRoot);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnScenarioChosen")]
        private static bool OnScenarioChosenPrefix(
            ScenarioSelectionPanel __instance,
            int ___m_selectedScenario,
            List<UIButton> ___m_scenarioButtons,
            UILabel ___m_scenarioNameLabel,
            UILabel ___m_scenarioDescLabel,
            UILabel ___m_scenarioHighScore,
            GameObject ___m_stasis_scoreLabelsRoot)
        {
            return Controller.HandleScenarioChosen(__instance, ___m_selectedScenario, ___m_scenarioButtons);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnCancel")]
        private static bool OnCancelPrefix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
        {
            return Controller.HandleCancel(__instance, ___m_scenarioButtons);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDestroy")]
        private static void OnDestroyPostfix(ScenarioSelectionPanel __instance)
        {
            Controller.Cleanup(__instance);
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSpawn",
        TargetBehavior = "Pending custom scenarios are spawned through Sheltered QuestManager once a new world is ready.",
        FailureMode = "A selected custom scenario reaches save-slot selection but never starts in the new game.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario spawn patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(QuestManager), "UpdateManager")]
    internal static class ShelteredCustomScenarioQuestManagerPatches
    {
        [HarmonyPostfix]
        private static void UpdateManagerPostfix()
        {
            try
            {
                ScenarioRuntimeCompositionRoot.Resolve<IScenarioRuntimeOrchestrator>().UpdatePendingScenarioSpawn();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] UpdateManager hook failed: " + ex.Message);
            }
        }

    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioStateCleanup",
        TargetBehavior = "Pending custom scenario state is cleared when the player leaves custom scenario startup flow.",
        FailureMode = "A stale pending custom scenario may spawn after the player cancels or starts a vanilla mode.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario state cleanup patch host.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    internal static class ShelteredCustomScenarioStateCleanupPatches
    {
        [HarmonyPatch(typeof(GameModeSelectionPanel), "OnSurvivalModeChosen")]
        [HarmonyPostfix]
        private static void SurvivalChosenPostfix()
        {
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Survival mode chosen; checking for stale pending custom scenario state.");
            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
            SlotPagingScopeResolver.ClearRememberedScenarioSelections();
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnCancel")]
        [HarmonyPostfix]
        private static void SlotSelectionCancelPostfix(SlotSelectionPanel __instance)
        {
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Slot selection cancelled; checking for stale pending custom scenario state.");
            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
            SlotPagingScopeResolver.ForgetScenarioSelection(__instance);
        }

        [HarmonyPatch(typeof(CustomisationPanel), "OnCancel")]
        [HarmonyPrefix]
        private static void CustomisationCancelPrefix(CustomisationPanel __instance)
        {
            if (__instance == null)
                return;

            int currentPage = 0;
            try { currentPage = Traverse.Create(__instance).Field("m_currentPageIndex").GetValue<int>(); }
            catch { }

            if (currentPage != 0)
                return;

            SaveManager.SaveType[] startupSaveTypes = GetScenarioStartupSaveTypes();
            for (int i = 0; i < startupSaveTypes.Length; i++)
                ClearQueuedStartupSave(startupSaveTypes[i]);

            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
        }

        private static SaveManager.SaveType[] GetScenarioStartupSaveTypes()
        {
            return new SaveManager.SaveType[]
            {
                SaveManager.SaveType.Slot1,
                SaveManager.SaveType.Slot2,
                SaveManager.SaveType.Slot3,
                SaveManager.SaveType.SlotSurrounded,
                SaveManager.SaveType.SlotStasis
            };
        }

        private static void ClearQueuedStartupSave(SaveManager.SaveType saveType)
        {
            SaveRuntimeState.Target pendingTarget;
            if (!SaveRuntimeState.TryGetPendingSave(saveType, out pendingTarget) || pendingTarget == null)
                return;

            IScenarioSaveLibrary saveLibrary = ScenarioRuntimeCompositionRoot.Resolve<IScenarioSaveLibrary>();
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Customisation cancelled before game start. Clearing queued startup save. scenarioId="
                + pendingTarget.ScenarioId + " saveId=" + pendingTarget.SaveId + " saveType=" + saveType + ".");

            if (!string.IsNullOrEmpty(pendingTarget.ScenarioId)
                && !string.Equals(pendingTarget.ScenarioId, "Standard", StringComparison.OrdinalIgnoreCase))
                saveLibrary.Delete(pendingTarget.ScenarioId, pendingTarget.SaveId);

            saveLibrary.ClearQueuedNewGameSave(saveType);
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioCharacterRuntimeNames",
        TargetBehavior = "Authored scenario-character display names are applied before vanilla creates scenario visitors.",
        FailureMode = "Scenario dialogue uses a randomized vanilla visitor name instead of the authored character name.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario-character name patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(NpcVisitManager), "AddNewScenario")]
    internal static class ScenarioCharacterRuntimeNamePatches
    {
        [HarmonyPrefix]
        private static void AddNewScenarioPrefix(
            List<QuestManager.QuestCharacterInfo> charInfo,
            int scenarioId)
        {
            try
            {
                int applied = ScenarioCharacterRuntimeNameRegistry.ApplyToPendingStage(scenarioId, charInfo);
                if (applied >= 0)
                {
                    MMLog.WriteInfo(
                        "[ScenarioCharacterRuntimeNames] Applied " + applied
                        + " authored visitor preset name(s) for scenario instance " + scenarioId + ".");
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioCharacterRuntimeNames] Could not apply authored visitor names: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioGuidedSetup",
        TargetBehavior = "Guided custom scenarios pre-set and lock author-fixed vanilla difficulty controls.",
        FailureMode = "Guided launches fall back to interactive vanilla difficulty controls.",
        RollbackStrategy = "Disable the Scenarios patch domain or choose Full Setup for the scenario.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    internal static class ShelteredCustomScenarioGuidedSetupPatches
    {
        [HarmonyPatch(typeof(DifficultyCustomisation), "OnShowPage")]
        [HarmonyPostfix]
        private static void OnShowPagePostfix(DifficultyCustomisation __instance)
        {
            ScenarioDefinition definition;
            if (__instance == null || !ScenarioLaunchSetupPolicy.TryGetPendingGuidedDefinition(out definition))
                return;

            ApplyFixed(__instance, definition, ScenarioDifficultyCategoryIds.Rain, "m_currentRain", "UpdateRainLabel", "RainText");
            ApplyFixed(__instance, definition, ScenarioDifficultyCategoryIds.Resources, "m_currentResources", "UpdateResourcesLabel", "ResourcesText");
            ApplyFixed(__instance, definition, ScenarioDifficultyCategoryIds.Breach, "m_currentBreach", "UpdateBreachLabel", "BreachText");
            ApplyFixed(__instance, definition, ScenarioDifficultyCategoryIds.Faction, "m_currentFaction", "UpdateFactionLabel", "FactionText");
            ApplyFixed(__instance, definition, ScenarioDifficultyCategoryIds.Mood, "m_currentMood", "UpdateMoodLabel", "MoodText");
            ApplyFixed(__instance, definition, ScenarioDifficultyCategoryIds.MapSize, "m_currentMapSize", "UpdateMapSizeLabel", "MapSizeText");
            ApplyFixedFog(__instance, definition);
        }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextDifficulty")]
        [HarmonyPrefix]
        private static bool NextPresetPrefix() { return !HasAnyFixedCategory(); }
        [HarmonyPatch(typeof(DifficultyCustomisation), "PrevDifficulty")]
        [HarmonyPrefix]
        private static bool PreviousPresetPrefix() { return !HasAnyFixedCategory(); }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextRainDifficulty")]
        [HarmonyPrefix]
        private static bool NextRainPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Rain); }
        [HarmonyPatch(typeof(DifficultyCustomisation), "PrevRainDifficulty")]
        [HarmonyPrefix]
        private static bool PreviousRainPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Rain); }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextResourceDifficulty")]
        [HarmonyPrefix]
        private static bool NextResourcesPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Resources); }
        [HarmonyPatch(typeof(DifficultyCustomisation), "PrevResourceDifficulty")]
        [HarmonyPrefix]
        private static bool PreviousResourcesPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Resources); }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextBreachDifficulty")]
        [HarmonyPrefix]
        private static bool NextBreachPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Breach); }
        [HarmonyPatch(typeof(DifficultyCustomisation), "PrevBreachDifficulty")]
        [HarmonyPrefix]
        private static bool PreviousBreachPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Breach); }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextFactionDifficulty")]
        [HarmonyPrefix]
        private static bool NextFactionPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Faction); }
        [HarmonyPatch(typeof(DifficultyCustomisation), "PrevFactionDifficulty")]
        [HarmonyPrefix]
        private static bool PreviousFactionPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Faction); }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextMoodDifficulty")]
        [HarmonyPrefix]
        private static bool NextMoodPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Mood); }
        [HarmonyPatch(typeof(DifficultyCustomisation), "PrevMoodDifficulty")]
        [HarmonyPrefix]
        private static bool PreviousMoodPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Mood); }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextMapSize")]
        [HarmonyPrefix]
        private static bool NextMapSizePrefix() { return CanChange(ScenarioDifficultyCategoryIds.MapSize); }
        [HarmonyPatch(typeof(DifficultyCustomisation), "PrevMapSize")]
        [HarmonyPrefix]
        private static bool PreviousMapSizePrefix() { return CanChange(ScenarioDifficultyCategoryIds.MapSize); }

        [HarmonyPatch(typeof(DifficultyCustomisation), "NextFogDifficulty")]
        [HarmonyPrefix]
        private static bool FogPrefix() { return CanChange(ScenarioDifficultyCategoryIds.Fog); }

        private static bool CanChange(string categoryId)
        {
            ScenarioDefinition definition;
            return !ScenarioLaunchSetupPolicy.TryGetPendingGuidedDefinition(out definition)
                || ScenarioLaunchSetupPolicy.IsPlayerSelectable(definition, categoryId);
        }

        private static bool HasAnyFixedCategory()
        {
            ScenarioDefinition definition;
            if (!ScenarioLaunchSetupPolicy.TryGetPendingGuidedDefinition(out definition))
                return false;
            string[] ids = { ScenarioDifficultyCategoryIds.Rain, ScenarioDifficultyCategoryIds.Resources,
                ScenarioDifficultyCategoryIds.Breach, ScenarioDifficultyCategoryIds.Faction,
                ScenarioDifficultyCategoryIds.Mood, ScenarioDifficultyCategoryIds.MapSize, ScenarioDifficultyCategoryIds.Fog };
            for (int i = 0; i < ids.Length; i++)
                if (!ScenarioLaunchSetupPolicy.IsPlayerSelectable(definition, ids[i])) return true;
            return false;
        }

        private static void ApplyFixed(DifficultyCustomisation page, ScenarioDefinition definition, string id, string field, string updateMethod, string labelField)
        {
            if (ScenarioLaunchSetupPolicy.IsPlayerSelectable(definition, id))
                return;
            Traverse traverse = Traverse.Create(page);
            traverse.Field(field).SetValue(ScenarioLaunchSetupPolicy.GetValue(definition, id, 1));
            traverse.Method(updateMethod).GetValue();
            AppendAuthoredNote(traverse.Field(labelField).GetValue<UILabel>());
        }

        private static void ApplyFixedFog(DifficultyCustomisation page, ScenarioDefinition definition)
        {
            if (ScenarioLaunchSetupPolicy.IsPlayerSelectable(definition, ScenarioDifficultyCategoryIds.Fog))
                return;
            Traverse traverse = Traverse.Create(page);
            traverse.Field("m_currentFog").SetValue(ScenarioLaunchSetupPolicy.GetValue(definition, ScenarioDifficultyCategoryIds.Fog, 0) != 0);
            traverse.Method("UpdateFogLabel").GetValue();
            AppendAuthoredNote(traverse.Field("FogText").GetValue<UILabel>());
        }

        private static void AppendAuthoredNote(UILabel label)
        {
            const string note = " [808080](authored)[-]";
            if (label != null && !string.IsNullOrEmpty(label.text) && label.text.IndexOf("(authored)", StringComparison.Ordinal) < 0)
                label.text += note;
        }
    }


    [PatchPolicy(PatchDomain.Scenarios, "ShelteredScenarioDefinitionApply",
        TargetBehavior = "Active scenario definitions are applied after save load once the Sheltered world is ready.",
        FailureMode = "A scenario-bound save loads as vanilla until the next successful scenario apply.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the definition apply patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(QuestManager), "UpdateManager")]
    internal static class ShelteredScenarioDefinitionApplyPatches
    {
        [HarmonyPostfix]
        private static void UpdateManagerPostfix()
        {
            try
            {
                ScenarioRuntimeCompositionRoot.Resolve<IScenarioRuntimeOrchestrator>().UpdateActiveScenarioApply();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredScenarioDefinitionApply] UpdateManager hook failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioFutureSurvivorRecruitBinding",
        TargetBehavior = "Scenario-scheduled ask-to-join future survivors bind to their authored actor after vanilla recruitment.",
        FailureMode = "Accepted ask-to-join future survivors join the family without scenario actor identity or conditions resolving.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the future-survivor recruit binding patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ScenarioFutureSurvivorRecruitBindingPatches
    {
        private static ScenarioFutureSurvivorRecruitBindingService BindingService
        {
            get { return ScenarioRuntimeCompositionRoot.Resolve<ScenarioFutureSurvivorRecruitBindingService>(); }
        }

        [HarmonyPatch(typeof(NpcVisitManager), "CreateNpcVisitor")]
        [HarmonyPostfix]
        private static void CreateNpcVisitorPostfix(
            NpcVisitor.NpcType type,
            FamilySpawner.CharacterAttributes attribsOverride,
            NpcVisitor __result)
        {
            if (type != NpcVisitor.NpcType.Joiner || __result == null || attribsOverride == null)
                return;

            string message;
            SeamGuard.Run(
                "scenario.future-survivor.ask-to-join.visitor-created",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { BindingService.OnVisitorCreated(__result, attribsOverride); },
                "Ask-to-join survivor binding unavailable - scenario still playable.",
                null,
                out message);
        }

        [HarmonyPatch(typeof(FamilyManager), "AdoptNpc")]
        [HarmonyPostfix]
        private static void AdoptNpcPostfix(NpcVisitor npc, bool __result)
        {
            if (!__result || npc == null)
                return;

            string message;
            SeamGuard.Run(
                "scenario.future-survivor.ask-to-join.adopt",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate
                {
                    FamilyMember member = npc.GetComponent<FamilyMember>();
                    if (member != null)
                        BindingService.OnNpcAdopted(npc, member);
                },
                "Ask-to-join survivor binding unavailable - scenario still playable.",
                null,
                out message);
        }

        [HarmonyPatch(typeof(NpcVisitManager), "OnNpcFinished")]
        [HarmonyPostfix]
        private static void OnNpcFinishedPostfix(NpcVisitor npc)
        {
            if (npc == null)
                return;

            string message;
            SeamGuard.Run(
                "scenario.future-survivor.ask-to-join.visitor-finished",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { BindingService.OnVisitorFinished(npc); },
                "Ask-to-join survivor binding unavailable - scenario still playable.",
                null,
                out message);
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSlotClickGuard",
        TargetBehavior = "Save-slot clicks are briefly blocked while custom scenario UI buttons are being pressed.",
        FailureMode = "Underlying save-slot controls can steal clicks from the custom scenario hub/list.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario slot click guard patch host.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    internal static class ShelteredCustomScenarioSlotClickGuardPatches
    {
        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotSelected")]
        [HarmonyPrefix]
        private static bool OnSlotSelectedPrefix(SlotSelectionPanel __instance)
        {
            bool allowed = !ShelteredCustomScenarioRuntimeState.ShouldBlockSlotInteraction(__instance);
            if (!allowed)
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Blocked SlotSelectionPanel.OnSlotSelected during guarded UI click.");
            return allowed;
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotChosen")]
        [HarmonyPrefix]
        private static bool OnSlotChosenPrefix(SlotSelectionPanel __instance)
        {
            bool allowed = !ShelteredCustomScenarioRuntimeState.ShouldBlockSlotInteraction(__instance);
            if (!allowed)
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Blocked SlotSelectionPanel.OnSlotChosen during guarded UI click.");
            return allowed;
        }

        [HarmonyPatch(typeof(SaveSlotButton), "OnClick")]
        [HarmonyPrefix]
        private static bool SaveSlotButtonClickPrefix(SaveSlotButton __instance)
        {
            bool allowed = !ShelteredCustomScenarioRuntimeState.ShouldBlockSlotInteraction(__instance);
            if (!allowed)
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Blocked SaveSlotButton.OnClick during guarded UI click.");
            return allowed;
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioDynamicFamilyUiReadiness",
        TargetBehavior = "Dynamically spawned scenario survivors wait for their vanilla UI_Character binding before warning UI updates run.",
        FailureMode = "A newly spawned survivor can throw UI_CharacterInfo.Update NullReferenceException during its first frame.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the dynamic family UI readiness patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ScenarioDynamicFamilyUiReadinessPatches
    {
        [HarmonyPatch(typeof(UI_CharacterInfo), "Update")]
        [HarmonyPrefix]
        private static bool UpdatePrefix(UI_CharacterInfo __instance)
        {
            if (__instance == null || __instance.transform == null)
                return true;

            Transform parent = __instance.transform.parent;
            UI_Character character = parent != null ? parent.GetComponent<UI_Character>() : null;
            if (character == null && parent != null && parent.parent != null)
                character = parent.parent.GetComponent<UI_Character>();

            return character != null && character.familyMember != null;
        }
    }
}
