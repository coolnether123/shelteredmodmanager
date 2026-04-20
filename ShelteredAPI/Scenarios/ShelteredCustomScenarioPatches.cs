using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal static class ShelteredCustomScenarioRuntimeState
    {
        private static int _blockSlotClicksUntilFrame;

        public static bool IsSlotClickBlocked
        {
            get { return Time.frameCount <= _blockSlotClicksUntilFrame; }
        }

        public static void BlockSlotClicksBriefly()
        {
            _blockSlotClicksUntilFrame = Math.Max(_blockSlotClicksUntilFrame, Time.frameCount + 2);
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
                ShelteredCustomScenarioService.Instance.ClearState();
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSelection",
        TargetBehavior = "Custom scenario entries are surfaced in the vanilla Sheltered scenario selection panel.",
        FailureMode = "Registered custom scenarios are unavailable from the in-game scenario selection flow.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario selection patch host.")]
    [HarmonyPatch(typeof(ScenarioSelectionPanel))]
    internal static class ShelteredCustomScenarioSelectionPatches
    {
        private const string HubLabel = "Custom Scenarios";
        private static readonly Dictionary<int, bool> ButtonsCreated = new Dictionary<int, bool>();
        private static readonly Dictionary<int, int> BaseButtonCount = new Dictionary<int, int>();
        private static readonly Dictionary<int, List<UIButton>> OriginalButtons = new Dictionary<int, List<UIButton>>();
        private static readonly Dictionary<int, UIButton> HubButtons = new Dictionary<int, UIButton>();
        private static readonly Dictionary<int, List<UIButton>> CustomButtons = new Dictionary<int, List<UIButton>>();
        private static readonly HashSet<int> CustomModePanels = new HashSet<int>();

        [HarmonyPostfix]
        [HarmonyPatch("OnShow")]
        private static void OnShowPostfix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
        {
            try
            {
                if (__instance == null || ___m_scenarioButtons == null)
                    return;

                CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
                if (scenarios.Length == 0)
                    return;

                int instanceId = __instance.GetInstanceID();
                if (ButtonsCreated.ContainsKey(instanceId) && ButtonsCreated[instanceId])
                    return;

                if (___m_scenarioButtons.Count == 0)
                    return;

                UIButton sourceButton = ___m_scenarioButtons[___m_scenarioButtons.Count - 1];
                if (sourceButton == null || sourceButton.gameObject == null)
                    return;

                if (!OriginalButtons.ContainsKey(instanceId))
                    OriginalButtons[instanceId] = new List<UIButton>(___m_scenarioButtons);

                float spacingY = MeasureSpacing(___m_scenarioButtons);
                GameObject hubObject = GameObject.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                hubObject.name = "ShelteredAPI_CustomScenarios_HubButton";
                hubObject.SetActive(true);

                UIButton hubButton = hubObject.GetComponent<UIButton>();
                if (hubButton == null)
                    hubButton = hubObject.AddComponent<UIButton>();

                hubButton.transform.localPosition = sourceButton.transform.localPosition + new Vector3(0f, spacingY, 0f);
                ConfigureButton(hubObject, HubLabel);

                UIEventListener listener = UIEventListener.Get(hubObject);
                listener.onPress = delegate(GameObject go, bool pressed)
                {
                    if (pressed)
                        ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                };
                listener.onClick = delegate(GameObject go)
                {
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                    EnterCustomMode(__instance, ___m_scenarioButtons);
                };

                ___m_scenarioButtons.Add(hubButton);
                BaseButtonCount[instanceId] = ___m_scenarioButtons.Count - 1;
                HubButtons[instanceId] = hubButton;
                ButtonsCreated[instanceId] = true;

                MMLog.WriteDebug("[ShelteredCustomScenarioSelection] Added custom scenario hub with "
                    + scenarios.Length + " registered scenario(s).");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] OnShow failed: " + ex.Message);
            }
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
            int instanceId = __instance.GetInstanceID();
            if (!CustomModePanels.Contains(instanceId))
            {
                int baseCount = BaseButtonCount.ContainsKey(instanceId) ? BaseButtonCount[instanceId] : 2;
                if (___m_selectedScenario == baseCount)
                {
                    SetScenarioText(___m_scenarioNameLabel, ___m_scenarioDescLabel, ___m_scenarioHighScore, ___m_stasis_scoreLabelsRoot,
                        HubLabel,
                        "Browse custom scenarios registered by loaded mods.");
                    return false;
                }

                return true;
            }

            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            if (___m_selectedScenario >= 0 && ___m_selectedScenario < scenarios.Length)
            {
                CustomScenarioInfo scenario = scenarios[___m_selectedScenario];
                SlotManifest manifest = ShelteredCustomScenarioService.Instance.CreateDependencyManifest(scenario);
                SaveVerification.VerificationState dependencyState = SaveVerification.VerifyRequired(manifest);
                SetScenarioText(___m_scenarioNameLabel, ___m_scenarioDescLabel, ___m_scenarioHighScore, ___m_stasis_scoreLabelsRoot,
                    dependencyState == SaveVerification.VerificationState.Match ? scenario.DisplayName : scenario.DisplayName + " [LOCKED]",
                    BuildScenarioDescription(scenario.Description, manifest, dependencyState));
            }
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        private static void UpdatePostfix(
            ScenarioSelectionPanel __instance,
            int ___m_selectedScenario,
            UILabel ___m_scenarioNameLabel,
            UILabel ___m_scenarioDescLabel,
            UILabel ___m_scenarioHighScore,
            GameObject ___m_stasis_scoreLabelsRoot,
            SlotSelectionPanel ___selectionPanel)
        {
            try
            {
                int instanceId = __instance.GetInstanceID();
                if (!CustomModePanels.Contains(instanceId))
                    return;

                if (___selectionPanel != null)
                    ___selectionPanel.m_inputEnabled = false;

                if (___m_selectedScenario == -1)
                {
                    CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
                    SetScenarioText(___m_scenarioNameLabel, ___m_scenarioDescLabel, ___m_scenarioHighScore, ___m_stasis_scoreLabelsRoot,
                        HubLabel,
                        scenarios.Length + " custom scenario(s) available.");
                }
            }
            catch
            {
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnScenarioChosen")]
        private static bool OnScenarioChosenPrefix(
            ScenarioSelectionPanel __instance,
            int ___m_selectedScenario,
            List<UIButton> ___m_scenarioButtons)
        {
            int instanceId = __instance.GetInstanceID();
            if (!CustomModePanels.Contains(instanceId))
            {
                int baseCount = BaseButtonCount.ContainsKey(instanceId) ? BaseButtonCount[instanceId] : 2;
                if (___m_selectedScenario == baseCount)
                {
                    EnterCustomMode(__instance, ___m_scenarioButtons);
                    return false;
                }

                ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
                return true;
            }

            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            if (___m_selectedScenario < 0 || ___m_selectedScenario >= scenarios.Length)
                return false;

            CustomScenarioInfo scenario = scenarios[___m_selectedScenario];
            SlotManifest manifest = ShelteredCustomScenarioService.Instance.CreateDependencyManifest(scenario);
            SaveVerification.VerificationState dependencyState = SaveVerification.VerifyRequired(manifest);
            if (dependencyState != SaveVerification.VerificationState.Match)
            {
                SaveDetailsWindow.ShowScenario(scenario.DisplayName, manifest, dependencyState, delegate
                {
                    StartCustomScenario(__instance, ___m_scenarioButtons, scenario);
                });
                return false;
            }

            if (!StartCustomScenario(__instance, ___m_scenarioButtons, scenario))
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to select custom scenario: " + scenario.Id);
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnCancel")]
        private static bool OnCancelPrefix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
        {
            int instanceId = __instance.GetInstanceID();
            if (!CustomModePanels.Contains(instanceId))
                return true;

            ExitCustomMode(__instance, ___m_scenarioButtons);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDestroy")]
        private static void OnDestroyPostfix(ScenarioSelectionPanel __instance)
        {
            Cleanup(__instance.GetInstanceID());
        }

        private static void EnterCustomMode(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
        {
            if (panel == null || scenarioButtons == null)
                return;

            int instanceId = panel.GetInstanceID();
            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            if (scenarios.Length == 0)
                return;

            if (!CustomButtons.ContainsKey(instanceId))
                CustomButtons[instanceId] = new List<UIButton>();

            DestroyButtons(CustomButtons[instanceId]);
            CustomButtons[instanceId].Clear();

            List<UIButton> originals;
            if (OriginalButtons.TryGetValue(instanceId, out originals))
            {
                for (int i = 0; i < originals.Count; i++)
                {
                    if (originals[i] != null && originals[i].gameObject != null)
                        originals[i].gameObject.SetActive(false);
                }
            }

            UIButton hubButton;
            if (HubButtons.TryGetValue(instanceId, out hubButton) && hubButton != null && hubButton.gameObject != null)
                hubButton.gameObject.SetActive(false);

            UIButton source = hubButton;
            if (source == null && originals != null && originals.Count > 0)
                source = originals[0];
            if (source == null)
                return;

            Vector3 basePosition = source.transform.localPosition;
            if (originals != null && originals.Count > 0 && originals[0] != null)
                basePosition = originals[0].transform.localPosition;

            float spacingY = originals != null ? MeasureSpacing(originals) : -60f;
            scenarioButtons.Clear();

            for (int i = 0; i < scenarios.Length; i++)
            {
                CustomScenarioInfo scenario = scenarios[i];
                GameObject buttonObject = GameObject.Instantiate(source.gameObject, source.transform.parent);
                buttonObject.name = "ShelteredAPI_CustomScenario_" + SanitizeObjectName(scenario.Id);
                buttonObject.SetActive(true);

                UIButton button = buttonObject.GetComponent<UIButton>();
                if (button == null)
                    button = buttonObject.AddComponent<UIButton>();

                button.transform.localPosition = basePosition + new Vector3(0f, spacingY * i, 0f);
                bool locked = ShelteredCustomScenarioService.Instance.VerifyDependencies(scenario) != SaveVerification.VerificationState.Match;
                ConfigureButton(buttonObject, locked ? scenario.DisplayName + " [LOCKED]" : scenario.DisplayName, locked);

                int capturedIndex = i;
                UIEventListener listener = UIEventListener.Get(buttonObject);
                listener.onPress = delegate(GameObject go, bool pressed)
                {
                    if (pressed)
                        ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                };
                listener.onClick = delegate(GameObject go)
                {
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                    Traverse.Create(panel).Field("m_selectedScenario").SetValue(capturedIndex);
                    panel.OnScenarioChosen();
                };

                CustomButtons[instanceId].Add(button);
                scenarioButtons.Add(button);
            }

            CustomModePanels.Add(instanceId);
            Traverse.Create(panel).Field("m_selectedScenario").SetValue(-1);
            MMLog.WriteDebug("[ShelteredCustomScenarioSelection] Entered custom scenario list for panel " + instanceId + ".");
        }

        private static void ExitCustomMode(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
        {
            if (panel == null || scenarioButtons == null)
                return;

            int instanceId = panel.GetInstanceID();
            List<UIButton> buttons;
            if (CustomButtons.TryGetValue(instanceId, out buttons))
            {
                DestroyButtons(buttons);
                buttons.Clear();
            }

            scenarioButtons.Clear();
            List<UIButton> originals;
            if (OriginalButtons.TryGetValue(instanceId, out originals))
            {
                for (int i = 0; i < originals.Count; i++)
                {
                    UIButton button = originals[i];
                    if (button == null || button.gameObject == null)
                        continue;

                    button.gameObject.SetActive(true);
                    scenarioButtons.Add(button);
                }
            }

            UIButton hubButton;
            if (HubButtons.TryGetValue(instanceId, out hubButton) && hubButton != null && hubButton.gameObject != null)
            {
                hubButton.gameObject.SetActive(true);
                scenarioButtons.Add(hubButton);
            }

            CustomModePanels.Remove(instanceId);
            Traverse.Create(panel).Field("m_selectedScenario").SetValue(-1);
            MMLog.WriteDebug("[ShelteredCustomScenarioSelection] Exited custom scenario list for panel " + instanceId + ".");
        }

        private static void Cleanup(int instanceId)
        {
            ButtonsCreated.Remove(instanceId);
            BaseButtonCount.Remove(instanceId);
            CustomModePanels.Remove(instanceId);
            OriginalButtons.Remove(instanceId);
            HubButtons.Remove(instanceId);

            List<UIButton> buttons;
            if (CustomButtons.TryGetValue(instanceId, out buttons))
                DestroyButtons(buttons);
            CustomButtons.Remove(instanceId);
        }

        private static bool StartCustomScenario(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons, CustomScenarioInfo scenario)
        {
            if (scenario == null)
                return false;

            if (!ShelteredCustomScenarioService.Instance.MarkSelected(scenario.Id))
                return false;

            ExitCustomMode(panel, scenarioButtons);
            ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Selected custom scenario: " + scenario.Id);
            return true;
        }

        private static void ConfigureButton(GameObject buttonObject, string label)
        {
            ConfigureButton(buttonObject, label, false);
        }

        private static void ConfigureButton(GameObject buttonObject, string label, bool locked)
        {
            if (buttonObject == null)
                return;

            UIButton button = buttonObject.GetComponent<UIButton>();
            if (button != null && button.onClick != null)
                button.onClick.Clear();

            UIButtonMessage[] messages = buttonObject.GetComponentsInChildren<UIButtonMessage>(true);
            for (int i = 0; i < messages.Length; i++)
            {
                if (messages[i] != null)
                    UnityEngine.Object.Destroy(messages[i]);
            }

            UILocalize[] localizers = buttonObject.GetComponentsInChildren<UILocalize>(true);
            for (int i = 0; i < localizers.Length; i++)
            {
                if (localizers[i] != null)
                    UnityEngine.Object.Destroy(localizers[i]);
            }

            UILabel[] labels = buttonObject.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                {
                    labels[i].text = label ?? string.Empty;
                    if (locked)
                        labels[i].color = new Color(1f, 0.35f, 0.35f);
                }
            }
        }

        private static void SetScenarioText(
            UILabel nameLabel,
            UILabel descriptionLabel,
            UILabel highScoreLabel,
            GameObject stasisScoreLabelsRoot,
            string name,
            string description)
        {
            if (nameLabel != null)
                nameLabel.text = name ?? string.Empty;
            if (descriptionLabel != null)
                descriptionLabel.text = description ?? string.Empty;
            if (highScoreLabel != null)
                highScoreLabel.text = string.Empty;
            if (stasisScoreLabelsRoot != null)
                stasisScoreLabelsRoot.SetActive(false);
        }

        private static string BuildScenarioDescription(string description, SlotManifest manifest, SaveVerification.VerificationState state)
        {
            if (state == SaveVerification.VerificationState.Match)
                return description ?? string.Empty;

            string reason = BuildDependencySummary(manifest, state);
            if (string.IsNullOrEmpty(description))
                return "[LOCKED] " + reason;

            return "[LOCKED] " + reason + "\n" + description;
        }

        private static string BuildDependencySummary(SlotManifest manifest, SaveVerification.VerificationState state)
        {
            if (manifest == null)
                return "Scenario dependency metadata is unavailable.";

            List<SaveVerification.ModCompareEntry> comparison = SaveVerification.BuildModComparison(
                PluginManager.LoadedMods,
                manifest.lastLoadedMods,
                false);

            int missing = 0;
            int versionDiff = 0;
            for (int i = 0; i < comparison.Count; i++)
            {
                if (comparison[i].status == SaveVerification.ModCompareStatus.Missing)
                    missing++;
                else if (comparison[i].status == SaveVerification.ModCompareStatus.VersionDiff)
                    versionDiff++;
            }

            if (missing > 0 && versionDiff > 0)
                return "Cannot start: " + missing + " required mod(s) missing and " + versionDiff + " version mismatch(es).";
            if (missing > 0)
                return "Cannot start: " + missing + " required mod(s) missing.";
            if (versionDiff > 0)
                return "Cannot start: " + versionDiff + " required mod version mismatch(es).";
            if (state == SaveVerification.VerificationState.Unknown)
                return "Cannot start: dependency metadata could not be verified.";

            return "Cannot start until required mods are satisfied.";
        }

        private static float MeasureSpacing(IList<UIButton> buttons)
        {
            float spacingY = -60f;
            try
            {
                if (buttons != null && buttons.Count >= 2 && buttons[buttons.Count - 1] != null && buttons[buttons.Count - 2] != null)
                {
                    float last = buttons[buttons.Count - 1].transform.localPosition.y;
                    float previous = buttons[buttons.Count - 2].transform.localPosition.y;
                    float measured = last - previous;
                    if (Mathf.Abs(measured) > 1f)
                        spacingY = measured;
                }

                if (Mathf.Abs(spacingY) > 140f)
                    spacingY = Mathf.Sign(spacingY) * 120f;
            }
            catch
            {
            }

            return spacingY;
        }

        private static void DestroyButtons(List<UIButton> buttons)
        {
            if (buttons == null)
                return;

            for (int i = 0; i < buttons.Count; i++)
            {
                UIButton button = buttons[i];
                if (button != null && button.gameObject != null)
                    UnityEngine.Object.Destroy(button.gameObject);
            }
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSpawn",
        TargetBehavior = "Pending custom scenarios are spawned through Sheltered QuestManager once a new world is ready.",
        FailureMode = "A selected custom scenario reaches save-slot selection but never starts in the new game.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario spawn patch host.")]
    [HarmonyPatch(typeof(QuestManager), "UpdateManager")]
    internal static class ShelteredCustomScenarioQuestManagerPatches
    {
        [HarmonyPostfix]
        private static void UpdateManagerPostfix()
        {
            try
            {
                CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
                if (state == null || state.LifecycleState != CustomScenarioLifecycleState.Pending || string.IsNullOrEmpty(state.ScenarioId))
                    return;

                if (!ScenarioWorldReady.IsReady())
                    return;

                CustomScenarioInfo scenarioInfo;
                if (!ShelteredCustomScenarioService.Instance.TryGet(state.ScenarioId, out scenarioInfo)
                    || ShelteredCustomScenarioService.Instance.VerifyDependencies(scenarioInfo) != SaveVerification.VerificationState.Match)
                {
                    MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] Dependencies are not satisfied; custom scenario will not spawn: " + state.ScenarioId);
                    ShelteredCustomScenarioService.Instance.ClearState();
                    return;
                }

                ScenarioDef definition;
                string error;
                if (!ShelteredCustomScenarioService.Instance.TryCreateScenarioDef(state.ScenarioId, null, out definition, out error))
                {
                    MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] " + error);
                    ShelteredCustomScenarioService.Instance.ClearState();
                    return;
                }

                QuestInstance instance = QuestManager.instance.SpawnQuestOrScenario(definition);
                if (instance == null)
                {
                    MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] QuestManager failed to spawn custom scenario: " + state.ScenarioId);
                    ShelteredCustomScenarioService.Instance.ClearState();
                    return;
                }

                ShelteredCustomScenarioService.Instance.MarkSpawned(state.ScenarioId);
                MMLog.WriteInfo("[ShelteredCustomScenarioSpawn] Spawned custom scenario: " + state.ScenarioId);
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
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario state cleanup patch host.")]
    internal static class ShelteredCustomScenarioStateCleanupPatches
    {
        [HarmonyPatch(typeof(GameModeSelectionPanel), "OnSurvivalModeChosen")]
        [HarmonyPostfix]
        private static void SurvivalChosenPostfix()
        {
            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnCancel")]
        [HarmonyPostfix]
        private static void SlotSelectionCancelPostfix()
        {
            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredScenarioDefinitionApply",
        TargetBehavior = "Active scenario definitions are applied after save load once the Sheltered world is ready.",
        FailureMode = "A scenario-bound save loads as vanilla until the next successful scenario apply.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the definition apply patch host.")]
    [HarmonyPatch(typeof(QuestManager), "UpdateManager")]
    internal static class ShelteredScenarioDefinitionApplyPatches
    {
        private static string _lastAppliedKey;

        [HarmonyPostfix]
        private static void UpdateManagerPostfix()
        {
            try
            {
                ScenarioRuntimeBinding binding = ShelteredScenarioRuntimeBindingManager.Instance.GetActiveBindingForStartup();
                if (binding == null || string.IsNullOrEmpty(binding.ScenarioId) || !binding.IsActive)
                {
                    _lastAppliedKey = null;
                    return;
                }

                string applyKey = ShelteredScenarioRuntimeBindingManager.Instance.CurrentRevision
                    + "|" + binding.ScenarioId + "|" + (binding.VersionApplied ?? string.Empty);
                if (string.Equals(_lastAppliedKey, applyKey, StringComparison.OrdinalIgnoreCase))
                    return;

                if (!ScenarioWorldReady.IsReady())
                    return;

                ScenarioDefinition definition;
                string scenarioFilePath;
                ScenarioValidationResult validation;
                if (!ShelteredCustomScenarioService.Instance.TryLoadDefinition(binding.ScenarioId, out definition, out scenarioFilePath, out validation))
                {
                    LogValidationFailure(binding.ScenarioId, validation);
                    _lastAppliedKey = applyKey;
                    return;
                }

                ScenarioApplyResult apply = new ScenarioApplier().ApplyAll(definition, scenarioFilePath);
                _lastAppliedKey = applyKey;
                MMLog.WriteInfo("[ShelteredScenarioDefinitionApply] Applied active scenario binding: " + binding.ScenarioId
                    + " messages=" + apply.Messages.Length + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredScenarioDefinitionApply] UpdateManager hook failed: " + ex.Message);
            }
        }

        private static void LogValidationFailure(string scenarioId, ScenarioValidationResult validation)
        {
            if (validation == null)
            {
                MMLog.WriteWarning("[ShelteredScenarioDefinitionApply] Scenario failed to load: " + scenarioId);
                return;
            }

            ScenarioValidationIssue[] issues = validation.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null)
                    MMLog.WriteWarning("[ShelteredScenarioDefinitionApply] " + scenarioId + " " + issues[i].Severity + ": " + issues[i].Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSlotClickGuard",
        TargetBehavior = "Save-slot clicks are briefly blocked while custom scenario UI buttons are being pressed.",
        FailureMode = "Underlying save-slot controls can steal clicks from the custom scenario hub/list.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario slot click guard patch host.")]
    internal static class ShelteredCustomScenarioSlotClickGuardPatches
    {
        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotSelected")]
        [HarmonyPrefix]
        private static bool OnSlotSelectedPrefix()
        {
            return !ShelteredCustomScenarioRuntimeState.IsSlotClickBlocked;
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotChosen")]
        [HarmonyPrefix]
        private static bool OnSlotChosenPrefix()
        {
            return !ShelteredCustomScenarioRuntimeState.IsSlotClickBlocked;
        }

        [HarmonyPatch(typeof(SaveSlotButton), "OnClick")]
        [HarmonyPrefix]
        private static bool SaveSlotButtonClickPrefix()
        {
            return !ShelteredCustomScenarioRuntimeState.IsSlotClickBlocked;
        }
    }
}
