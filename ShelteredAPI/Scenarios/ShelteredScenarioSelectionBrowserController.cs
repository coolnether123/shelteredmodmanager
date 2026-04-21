using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Hooks;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
using ModAPI.Scenarios;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ShelteredScenarioSelectionBrowserController
    {
        private const string HubLabel = "Custom Scenarios";
        private const string AddNewLabel = "Add New Scenario";
        private const string PreviousPageLabel = "< Previous";
        private const string NextPageLabel = "Next >";
        private const int CompactButtonWidth = 320;
        private const int CompactButtonHeight = 74;
        private const int CompactButtonFontSize = 24;
        private const int CompactPagingButtonWidth = 132;
        private const int CompactPagingButtonHeight = 42;
        private const int CompactPagingButtonFontSize = 18;
        private static readonly Color AvailableButtonColor = new Color(0.88f, 0.76f, 0.63f, 1f);
        private static readonly Color AvailableHoverColor = new Color(0.97f, 0.85f, 0.70f, 1f);
        private static readonly Color AvailablePressedColor = new Color(0.74f, 0.61f, 0.49f, 1f);
        private static readonly Color HubButtonColor = new Color(0.87f, 0.75f, 0.62f, 1f);
        private static readonly Color HubHoverColor = new Color(0.96f, 0.84f, 0.69f, 1f);
        private static readonly Color HubPressedColor = new Color(0.73f, 0.60f, 0.48f, 1f);
        private static readonly Color ActionButtonColor = new Color(0.90f, 0.78f, 0.64f, 1f);
        private static readonly Color ActionHoverColor = new Color(0.99f, 0.86f, 0.71f, 1f);
        private static readonly Color ActionPressedColor = new Color(0.76f, 0.62f, 0.49f, 1f);
        private static readonly Color LockedButtonColor = new Color(0.77f, 0.57f, 0.54f, 1f);
        private static readonly Color LockedHoverColor = new Color(0.86f, 0.66f, 0.62f, 1f);
        private static readonly Color LockedPressedColor = new Color(0.66f, 0.47f, 0.45f, 1f);
        private static readonly Color ButtonDisabledColor = new Color(0.52f, 0.45f, 0.39f, 0.95f);
        private static readonly Color BrightLabelColor = new Color(0.18f, 0.13f, 0.09f, 1f);
        private static readonly Color LockedLabelColor = new Color(0.24f, 0.12f, 0.10f, 1f);
        private static readonly Color PagingLabelColor = new Color(0.18f, 0.13f, 0.09f, 1f);
        private static readonly Color PagingDisabledLabelColor = new Color(0.34f, 0.29f, 0.24f, 0.9f);
        private static readonly Color PageIndicatorColor = new Color(0.21f, 0.16f, 0.11f, 1f);

        private enum ScenarioButtonVisualStyle
        {
            Available,
            Hub,
            Action,
            Locked,
            PagingEnabled,
            PagingDisabled
        }

        private sealed class BrowserPanelState
        {
            public bool ButtonsCreated;
            public bool IsCustomMode;
            public int BaseButtonCount;
            public int Page;
            public int LastLoggedVanillaSelectedScenario = int.MinValue;
            public int LastLoggedCustomSelectedScenario = int.MinValue;
            public int LastLoggedPagingPage = -1;
            public int LastLoggedPagingTotalPages = -1;
            public int LastLoggedPagingScenarioCount = -1;
            public string LastLoggedScenarioTextKey;
            public readonly List<UIButton> OriginalButtons = new List<UIButton>();
            public readonly List<UIButton> CustomButtons = new List<UIButton>();
            public UIButton HubButton;
            public ScenarioPagingUi PagingUi;
            public ScenarioLayoutMetrics LayoutMetrics;
        }

        private sealed class ScenarioPagingUi
        {
            public UIButton AddNewButton;
            public UIButton PreviousButton;
            public UIButton NextButton;
            public UILabel PageLabel;
        }

        private sealed class ScenarioListEntry
        {
            public int ScenarioIndex;
            public string Label;
        }

        private sealed class ScenarioLayoutMetrics
        {
            public float SpacingY;
            public float ButtonWidth;
            public float ButtonHeight;
            public Vector3 TopSlotPosition;
            public readonly List<Vector3> ScenarioSlotPositions = new List<Vector3>();
            public Vector3 HubButtonPosition;
            public Vector3 AddNewButtonPosition;
            public Vector3 FooterCenterPosition;
            public Vector3 FooterPreviousPosition;
            public Vector3 FooterNextPosition;
        }

        public static ShelteredScenarioSelectionBrowserController Instance
        {
            get { return _instance; }
        }

        private static readonly ShelteredScenarioSelectionBrowserController _instance = new ShelteredScenarioSelectionBrowserController();
        private readonly Dictionary<int, BrowserPanelState> _states = new Dictionary<int, BrowserPanelState>();

        private ShelteredScenarioSelectionBrowserController()
        {
        }

        public void Initialize(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
        {
            try
            {
                if (panel == null || scenarioButtons == null || scenarioButtons.Count == 0)
                    return;

                BrowserPanelState state = GetState(panel);
                if (state.ButtonsCreated)
                {
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Initialize skipped; buttons already created for panel " + panel.GetInstanceID() + ".");
                    return;
                }

                RefreshDefinitionCatalogSafely();
                CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
                UIButton sourceButton = scenarioButtons[scenarioButtons.Count - 1];
                if (sourceButton == null || sourceButton.gameObject == null)
                    return;

                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Initialize start. panel=" + panel.GetInstanceID()
                    + " vanillaButtons=" + scenarioButtons.Count
                    + " discoveredScenarios=" + scenarios.Length
                    + " sourceButton=" + sourceButton.name + ".");

                if (state.OriginalButtons.Count == 0)
                    state.OriginalButtons.AddRange(scenarioButtons);

                state.LayoutMetrics = BuildLayoutMetrics(state, sourceButton);
                UIButton hubButton = CloneScenarioButton(sourceButton, sourceButton.transform.parent, "ShelteredAPI_CustomScenarios_HubButton");
                if (hubButton == null)
                    return;

                // Do not place the cloned hub on top of a vanilla scenario button.
                // Sheltered's cloned NGUI buttons can still inherit surprising click behavior
                // through the scene/prefab wiring, so the custom hub must live in its own space.
                hubButton.transform.localPosition = state.LayoutMetrics.HubButtonPosition;
                ConfigureButton(hubButton.gameObject, HubLabel, ScenarioButtonVisualStyle.Hub);
                LogUiElementLayout(panel, "HubButton", hubButton.gameObject, "hub");
                BindPressGuard(panel, hubButton.gameObject);
                UIEventListener.Get(hubButton.gameObject).onClick = delegate(GameObject go)
                {
                    ExecuteGuardedUiClick(panel, delegate
                    {
                        MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Hub clicked. panel=" + panel.GetInstanceID() + ".");
                        EnterCustomMode(panel, state, scenarioButtons);
                    });
                };

                scenarioButtons.Add(hubButton);
                state.BaseButtonCount = scenarioButtons.Count - 1;
                state.HubButton = hubButton;
                EnsurePagingUi(panel, state, sourceButton);
                state.ButtonsCreated = true;

                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Added custom scenario hub and paging UI. panel="
                    + panel.GetInstanceID() + " scenarios=" + scenarios.Length + " layout=" + DescribeLayoutMetrics(state.LayoutMetrics) + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] OnShow failed: " + ex.Message);
            }
        }

        public bool HandleScenarioSelected(
            ScenarioSelectionPanel panel,
            int selectedScenario,
            UILabel scenarioNameLabel,
            UILabel scenarioDescLabel,
            UILabel scenarioHighScore,
            GameObject stasisScoreLabelsRoot)
        {
            BrowserPanelState state = GetState(panel);
            if (!state.IsCustomMode)
            {
                int baseCount = state.BaseButtonCount > 0 ? state.BaseButtonCount : 2;
                bool selectionChanged = state.LastLoggedVanillaSelectedScenario != selectedScenario;
                if (selectionChanged)
                {
                    state.LastLoggedVanillaSelectedScenario = selectedScenario;
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Vanilla selection changed. panel="
                        + panel.GetInstanceID() + " selectedIndex=" + selectedScenario + " customHubIndex=" + baseCount + ".");
                }
                if (selectedScenario == baseCount)
                {
                    if (selectionChanged)
                        MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Hub highlighted on vanilla page. panel=" + panel.GetInstanceID() + ".");
                    SetScenarioText(
                        state,
                        panel,
                        scenarioNameLabel,
                        scenarioDescLabel,
                        scenarioHighScore,
                        stasisScoreLabelsRoot,
                        HubLabel,
                        "Browse custom scenarios registered by loaded mods and XML scenario packs.");
                    return false;
                }

                return true;
            }

            RefreshDefinitionCatalogSafely();
            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            ScenarioListEntry[] entries = BuildVisibleEntries(state, scenarios);
            bool customSelectionChanged = state.LastLoggedCustomSelectedScenario != selectedScenario;
            if (customSelectionChanged)
            {
                state.LastLoggedCustomSelectedScenario = selectedScenario;
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Custom selection changed. panel="
                    + panel.GetInstanceID() + " selectedIndex=" + selectedScenario
                    + " visibleEntries=" + entries.Length + " page=" + (state.Page + 1) + ".");
            }
            if (selectedScenario >= 0 && selectedScenario < entries.Length)
            {
                CustomScenarioInfo scenario = scenarios[entries[selectedScenario].ScenarioIndex];
                if (customSelectionChanged)
                {
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Custom scenario highlighted. panel=" + panel.GetInstanceID()
                        + " selectedIndex=" + selectedScenario + " scenarioId=" + scenario.Id + ".");
                }
                SlotManifest manifest = ShelteredCustomScenarioService.Instance.CreateDependencyManifest(scenario);
                SaveVerification.VerificationState dependencyState = SaveVerification.VerifyRequired(manifest);
                SetScenarioText(
                    state,
                    panel,
                    scenarioNameLabel,
                    scenarioDescLabel,
                    scenarioHighScore,
                    stasisScoreLabelsRoot,
                    dependencyState == SaveVerification.VerificationState.Match ? scenario.DisplayName : scenario.DisplayName + " [LOCKED]",
                    BuildScenarioDescription(scenario.Description, manifest, dependencyState));
                return false;
            }

            SetScenarioText(
                state,
                panel,
                scenarioNameLabel,
                scenarioDescLabel,
                scenarioHighScore,
                stasisScoreLabelsRoot,
                HubLabel,
                BuildBrowserDescription(state, scenarios.Length));
            return false;
        }

        public void HandleUpdate(
            ScenarioSelectionPanel panel,
            int selectedScenario,
            UILabel scenarioNameLabel,
            UILabel scenarioDescLabel,
            UILabel scenarioHighScore,
            GameObject stasisScoreLabelsRoot,
            SlotSelectionPanel selectionPanel)
        {
            try
            {
                BrowserPanelState state = GetExistingState(panel);
                if (state == null || !state.IsCustomMode)
                    return;

                if (selectionPanel != null)
                    selectionPanel.m_inputEnabled = false;

                RefreshDefinitionCatalogSafely();
                CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
                UpdatePagingUi(state, scenarios.Length);

                if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
                    TryChangePage(panel, state, 1);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
                    TryChangePage(panel, state, -1);

                if (selectedScenario == -1)
                {
                    SetScenarioText(
                        state,
                        panel,
                        scenarioNameLabel,
                        scenarioDescLabel,
                        scenarioHighScore,
                        stasisScoreLabelsRoot,
                        HubLabel,
                        BuildBrowserDescription(state, scenarios.Length));
                }
            }
            catch
            {
            }
        }

        public bool HandleScenarioChosen(
            ScenarioSelectionPanel panel,
            int selectedScenario,
            List<UIButton> scenarioButtons)
        {
            BrowserPanelState state = GetState(panel);
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] OnScenarioChosen intercepted. panel=" + panel.GetInstanceID()
                + " selectedIndex=" + selectedScenario + " customMode=" + state.IsCustomMode + ".");
            if (!state.IsCustomMode)
            {
                int baseCount = state.BaseButtonCount > 0 ? state.BaseButtonCount : 2;
                if (selectedScenario == baseCount)
                {
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] OnScenarioChosen routed into custom hub. panel=" + panel.GetInstanceID() + ".");
                    EnterCustomMode(panel, state, scenarioButtons);
                    return false;
                }

                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] OnScenarioChosen passed through to vanilla scenario flow. panel="
                    + panel.GetInstanceID() + " selectedIndex=" + selectedScenario + ".");
                ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
                return true;
            }

            StartCustomScenarioFromVisibleSelection(panel, state, scenarioButtons, selectedScenario);
            return false;
        }

        public bool HandleCancel(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
        {
            BrowserPanelState state = GetExistingState(panel);
            if (state == null || !state.IsCustomMode)
            {
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Cancel passed through to vanilla flow. panel="
                    + (panel != null ? panel.GetInstanceID().ToString() : "<null>") + ".");
                return true;
            }

            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Cancel pressed in custom mode. panel=" + panel.GetInstanceID() + ".");
            ExitCustomMode(panel, state, scenarioButtons);
            return false;
        }

        public void Cleanup(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return;

            int instanceId = panel.GetInstanceID();
            BrowserPanelState state;
            if (!_states.TryGetValue(instanceId, out state) || state == null)
                return;

            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Cleanup. panel=" + instanceId
                + " customButtons=" + state.CustomButtons.Count + ".");
            state.IsCustomMode = false;
            ShelteredCustomScenarioRuntimeState.SetCustomModeActive(false);
            UIFlowGuard.BlockSlotClicksToggle(false);
            UIUtil.ClearClickBlockers();
            DestroyButtons(state.CustomButtons);
            DestroyPagingUi(state);
            _states.Remove(instanceId);
        }

        private BrowserPanelState GetState(ScenarioSelectionPanel panel)
        {
            int instanceId = panel.GetInstanceID();
            BrowserPanelState state;
            if (!_states.TryGetValue(instanceId, out state) || state == null)
            {
                state = new BrowserPanelState();
                _states[instanceId] = state;
            }

            return state;
        }

        private BrowserPanelState GetExistingState(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return null;

            BrowserPanelState state;
            _states.TryGetValue(panel.GetInstanceID(), out state);
            return state;
        }

        private void EnterCustomMode(ScenarioSelectionPanel panel, BrowserPanelState state, List<UIButton> scenarioButtons)
        {
            if (panel == null || state == null || scenarioButtons == null)
                return;

            RefreshDefinitionCatalogSafely();
            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            ClampPage(state, scenarios.Length);
            ScenarioListEntry[] entries = BuildVisibleEntries(state, scenarios);
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Preparing custom mode contents. panel=" + panel.GetInstanceID()
                + " scenarios=" + scenarios.Length + " page=" + (state.Page + 1)
                + " entries=[" + DescribeEntries(entries, scenarios) + "].");

            DestroyButtons(state.CustomButtons);
            state.CustomButtons.Clear();

            for (int i = 0; i < state.OriginalButtons.Count; i++)
            {
                UIButton original = state.OriginalButtons[i];
                if (original != null && original.gameObject != null)
                    original.gameObject.SetActive(false);
            }

            if (state.HubButton != null && state.HubButton.gameObject != null)
                state.HubButton.gameObject.SetActive(false);

            UIButton sourceButton = state.HubButton;
            if (sourceButton == null && state.OriginalButtons.Count > 0)
                sourceButton = state.OriginalButtons[0];
            if (sourceButton == null)
                return;

            ScenarioLayoutMetrics metrics = GetOrCreateLayoutMetrics(state, sourceButton);
            scenarioButtons.Clear();

            for (int i = 0; i < entries.Length; i++)
            {
                ScenarioListEntry entry = entries[i];
                CustomScenarioInfo scenario = scenarios[entry.ScenarioIndex];
                UIButton button = CloneScenarioButton(sourceButton, sourceButton.transform.parent, "ShelteredAPI_CustomScenario_" + SanitizeObjectName(scenario.Id));
                if (button == null)
                {
                    MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to clone scenario button for " + scenario.Id + ".");
                    continue;
                }

                button.transform.localPosition = GetScenarioSlotPosition(metrics, i);
                bool locked = ShelteredCustomScenarioService.Instance.VerifyDependencies(scenario) != SaveVerification.VerificationState.Match;
                ConfigureButton(
                    button.gameObject,
                    locked ? entry.Label + " [LOCKED]" : entry.Label,
                    locked ? ScenarioButtonVisualStyle.Locked : ScenarioButtonVisualStyle.Available);
                BindPressGuard(panel, button.gameObject);
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Created scenario button. panel=" + panel.GetInstanceID()
                    + " slot=" + i + " scenarioId=" + scenario.Id + " locked=" + locked + ".");
                LogUiElementLayout(panel, "ScenarioButton[" + i + "]", button.gameObject, locked ? "locked" : "available");

                int capturedIndex = i;
                UIEventListener.Get(button.gameObject).onClick = delegate(GameObject go)
                {
                    ExecuteGuardedUiClick(panel, delegate
                    {
                        MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Scenario button clicked. panel=" + panel.GetInstanceID()
                            + " slot=" + capturedIndex + " scenarioId=" + scenario.Id + ".");
                        Traverse.Create(panel).Field("m_selectedScenario").SetValue(capturedIndex);
                        StartCustomScenarioFromVisibleSelection(panel, state, scenarioButtons, capturedIndex);
                    });
                };

                state.CustomButtons.Add(button);
                scenarioButtons.Add(button);
            }

            state.IsCustomMode = true;
            ShelteredCustomScenarioRuntimeState.SetCustomModeActive(true);
            UpdateCustomModeSupplementaryLayout(state, entries.Length);
            SetPagingUiVisible(state, true);
            UpdatePagingUi(state, scenarios.Length);
            Traverse.Create(panel).Field("m_selectedScenario").SetValue(-1);
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Entered custom mode. panel="
                + panel.GetInstanceID() + " page=" + (state.Page + 1) + "/" + GetTotalPages(state, scenarios.Length)
                + " visibleEntries=" + entries.Length + " pageSize=" + GetScenarioPageSize(state) + ".");
        }

        private void ExitCustomMode(ScenarioSelectionPanel panel, BrowserPanelState state, List<UIButton> scenarioButtons)
        {
            if (panel == null || state == null || scenarioButtons == null)
                return;

            DestroyButtons(state.CustomButtons);
            state.CustomButtons.Clear();

            scenarioButtons.Clear();
            for (int i = 0; i < state.OriginalButtons.Count; i++)
            {
                UIButton button = state.OriginalButtons[i];
                if (button == null || button.gameObject == null)
                    continue;

                button.gameObject.SetActive(true);
                scenarioButtons.Add(button);
            }

            if (state.HubButton != null && state.HubButton.gameObject != null)
            {
                state.HubButton.gameObject.SetActive(true);
                scenarioButtons.Add(state.HubButton);
            }

            state.IsCustomMode = false;
            ShelteredCustomScenarioRuntimeState.SetCustomModeActive(false);
            UIFlowGuard.BlockSlotClicksToggle(false);
            SetPagingUiVisible(state, false);
            Traverse.Create(panel).Field("m_selectedScenario").SetValue(-1);
            state.LastLoggedCustomSelectedScenario = int.MinValue;
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Exited custom mode. panel=" + panel.GetInstanceID() + ".");
        }

        private bool StartCustomScenario(ScenarioSelectionPanel panel, BrowserPanelState state, List<UIButton> scenarioButtons, CustomScenarioInfo scenario)
        {
            if (scenario == null)
                return false;

            if (!ShelteredCustomScenarioService.Instance.MarkSelected(scenario.Id))
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] MarkSelected failed for scenario " + scenario.Id + ".");
                return false;
            }

            SaveEntry startupSave = ScenarioSaves.CreateNext(scenario.Id, new SaveCreateOptions
            {
                name = scenario.DisplayName
            });
            if (startupSave == null)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to allocate startup save for scenario " + scenario.Id + ".");
                ShelteredCustomScenarioService.Instance.ClearState();
                return false;
            }

            ExitCustomMode(panel, state, scenarioButtons);
            ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
            QueueNewGameSaveTarget(startupSave.scenarioId, startupSave, GetLaunchVirtualSaveType());
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Selected custom scenario: " + scenario.Id
                + " startupSaveId=" + startupSave.id + " startupSlot=" + startupSave.absoluteSlot + ".");

            if (!BeginScenarioLaunchTransition(panel, "custom scenario '" + scenario.Id + "'", GetLaunchVirtualSaveType()))
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Launch transition failed for custom scenario '" + scenario.Id + "'.");
                PlatformSaveProxy.ClearNextSave(GetLaunchVirtualSaveType());
                ScenarioSaves.Delete(startupSave.scenarioId, startupSave.id);
                ShelteredCustomScenarioService.Instance.ClearState();
                return false;
            }

            return true;
        }

        private void StartCustomScenarioFromVisibleSelection(
            ScenarioSelectionPanel panel,
            BrowserPanelState state,
            List<UIButton> scenarioButtons,
            int selectedScenario)
        {
            if (panel == null || state == null)
                return;

            RefreshDefinitionCatalogSafely();
            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            ScenarioListEntry[] entries = BuildVisibleEntries(state, scenarios);
            if (selectedScenario < 0 || selectedScenario >= entries.Length)
            {
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Custom scenario chosen with out-of-range selection. panel="
                    + panel.GetInstanceID() + " selectedIndex=" + selectedScenario + " visibleEntries=" + entries.Length + ".");
                return;
            }

            CustomScenarioInfo scenario = scenarios[entries[selectedScenario].ScenarioIndex];
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Custom scenario chosen. panel=" + panel.GetInstanceID()
                + " selectedIndex=" + selectedScenario + " scenarioId=" + scenario.Id + ".");
            SlotManifest manifest = ShelteredCustomScenarioService.Instance.CreateDependencyManifest(scenario);
            SaveVerification.VerificationState dependencyState = SaveVerification.VerifyRequired(manifest);
            if (dependencyState != SaveVerification.VerificationState.Match)
            {
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Scenario dependency mismatch. panel=" + panel.GetInstanceID()
                    + " scenarioId=" + scenario.Id + " state=" + dependencyState + ".");
                SaveDetailsWindow.ShowScenario(scenario.DisplayName, manifest, dependencyState, delegate
                {
                    StartCustomScenario(panel, state, scenarioButtons, scenario);
                });
                return;
            }

            if (!StartCustomScenario(panel, state, scenarioButtons, scenario))
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to select custom scenario: " + scenario.Id);
        }

        private void StartNewScenarioEditor(ScenarioSelectionPanel panel)
        {
            UILabel nameLabel = null;
            UILabel descriptionLabel = null;
            UILabel highScoreLabel = null;
            GameObject stasisScoreLabelsRoot = null;
            try
            {
                nameLabel = Traverse.Create(panel).Field("m_scenarioNameLabel").GetValue<UILabel>();
                descriptionLabel = Traverse.Create(panel).Field("m_scenarioDescLabel").GetValue<UILabel>();
                highScoreLabel = Traverse.Create(panel).Field("m_scenarioHighScore").GetValue<UILabel>();
                stasisScoreLabelsRoot = Traverse.Create(panel).Field("m_stasis_scoreLabelsRoot").GetValue<GameObject>();
                SaveManager.SaveType launchSaveType = GetLaunchVirtualSaveType();
                ScenarioAuthoringSession draft = ScenarioAuthoringBootstrapService.Instance.QueueNewDraft(ScenarioBaseGameMode.Survival, launchSaveType);
                string id = draft != null && !string.IsNullOrEmpty(draft.DraftId) ? draft.DraftId : "new scenario";
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Add New clicked. panel=" + panel.GetInstanceID()
                    + " draftId=" + id + ".");
                SetScenarioText(
                    null,
                    null,
                    nameLabel,
                    descriptionLabel,
                    highScoreLabel,
                    stasisScoreLabelsRoot,
                    AddNewLabel,
                    "Draft '" + id + "' created. Launching into a dedicated authoring save; the editor shell will open once the world is ready.");

                BrowserPanelState state = GetState(panel);
                List<UIButton> scenarioButtons = Traverse.Create(panel).Field("m_scenarioButtons").GetValue<List<UIButton>>();
                ExitCustomMode(panel, state, scenarioButtons);
                if (draft == null || string.IsNullOrEmpty(draft.StartupSaveId))
                    throw new InvalidOperationException("The authoring draft session did not provide a startup save.");

                SaveEntry draftStartupSave;
                if (!ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(id, out draftStartupSave) || draftStartupSave == null)
                    throw new InvalidOperationException("Could not resolve the draft save entry for '" + id + "'.");

                QueueNewGameSaveTarget(ScenarioAuthoringDraftRepository.DraftStorageScenarioId, draftStartupSave, launchSaveType);
                if (!BeginScenarioLaunchTransition(panel, "authoring draft '" + id + "'", launchSaveType))
                    throw new InvalidOperationException("Scenario selection transition could not be started for draft '" + id + "'.");
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Queued authoring bootstrap for draft: " + id
                    + " startupSaveId=" + draftStartupSave.id + " startupSlot=" + draftStartupSave.absoluteSlot + ".");
            }
            catch (Exception ex)
            {
                SetScenarioText(
                    null,
                    null,
                    nameLabel,
                    descriptionLabel,
                    highScoreLabel,
                    stasisScoreLabelsRoot,
                    AddNewLabel,
                    "Could not create the scenario authoring draft: " + ex.Message);
                PlatformSaveProxy.ClearNextSave(GetLaunchVirtualSaveType());
                ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Authoring launch failed.");
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to queue scenario authoring draft: " + ex.Message);
            }
        }

        private static bool BeginScenarioLaunchTransition(ScenarioSelectionPanel panel, string launchTarget, SaveManager.SaveType virtualSaveType)
        {
            if (panel == null)
                return false;

            try
            {
                Traverse panelTraverse = Traverse.Create(panel);
                bool inputEnabled = panelTraverse.Field("m_inputEnabled").GetValue<bool>();
                SlotSelectionPanel slotSelectionPanel = null;
                try { slotSelectionPanel = panel.selectionPanel; }
                catch { }
                BasePanel customizationPanel = null;
                if (slotSelectionPanel != null)
                {
                    try { customizationPanel = Traverse.Create(slotSelectionPanel).Field("m_customizationPanel").GetValue<BasePanel>(); }
                    catch { }
                }
                UIPanelManager panelManager = UIPanelManager.instance;
                SaveManager saveManager = SaveManager.instance;
                int launchSlotNumber = GetSlotNumber(virtualSaveType);

                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Starting scenario launch transition. panel="
                    + panel.GetInstanceID() + " target=" + (launchTarget ?? "<unknown>")
                    + " inputEnabled=" + inputEnabled
                    + " panelOnStack=" + (panelManager != null && panelManager.IsPanelOnStack(panel))
                    + " hasSlotPanel=" + (slotSelectionPanel != null)
                    + " hasCustomizationPanel=" + (customizationPanel != null)
                    + " virtualSaveType=" + virtualSaveType + ".");

                if (!inputEnabled)
                {
                    panelTraverse.Field("m_inputEnabled").SetValue(true);
                    MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Scenario launch transition forced input enabled before starting target="
                        + (launchTarget ?? "<unknown>") + ".");
                }

                if (saveManager == null)
                {
                    MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Scenario launch transition could not find SaveManager for "
                        + (launchTarget ?? "<unknown>") + ".");
                    return false;
                }

                // Do not route custom scenario starts through SlotSelectionPanel.
                // Sheltered only has vanilla save semantics behind that panel, so even a
                // visually harmless detour would keep coupling startup to Standard slots.
                // We still reuse the stock customization panel so the rest of the new-game
                // flow remains familiar, but the save target has already been queued into
                // the scenario-specific registry before we get here.
                saveManager.SetCurrentSlot(launchSlotNumber);
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Bound virtual save slot " + launchSlotNumber
                    + " for " + (launchTarget ?? "<unknown>") + ".");

                if (customizationPanel != null && panelManager != null)
                {
                    panelManager.PushPanel(customizationPanel);
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Standard customization panel opened for "
                        + (launchTarget ?? "<unknown>") + ".");
                    return true;
                }

                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Scenario launch transition has no customization panel for "
                    + (launchTarget ?? "<unknown>") + ".");
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Scenario launch transition failed for "
                    + (launchTarget ?? "<unknown>") + ": " + ex);
                return false;
            }
        }

        private static SaveManager.SaveType GetLaunchVirtualSaveType()
        {
            return SaveManager.SaveType.Slot1;
        }

        private static int GetSlotNumber(SaveManager.SaveType saveType)
        {
            switch (saveType)
            {
                case SaveManager.SaveType.Slot1:
                    return 1;
                case SaveManager.SaveType.Slot2:
                    return 2;
                case SaveManager.SaveType.Slot3:
                    return 3;
                case SaveManager.SaveType.SlotSurrounded:
                    return 4;
                case SaveManager.SaveType.SlotStasis:
                    return 5;
                default:
                    return 1;
            }
        }

        private static void QueueNewGameSaveTarget(string scenarioId, SaveEntry startupSave, SaveManager.SaveType saveType)
        {
            if (startupSave == null)
                throw new ArgumentNullException("startupSave");

            PlatformSaveProxy.ClearNextSave(saveType);
            PlatformSaveProxy.SetNextSave(saveType, scenarioId, startupSave.id);
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Queued dedicated startup save. scenarioId="
                + scenarioId + " saveId=" + startupSave.id + " slot=" + startupSave.absoluteSlot
                + " virtualSaveType=" + saveType + ".");
        }

        private ScenarioListEntry[] BuildVisibleEntries(BrowserPanelState state, CustomScenarioInfo[] scenarios)
        {
            if (scenarios == null)
                scenarios = new CustomScenarioInfo[0];

            ClampPage(state, scenarios.Length);
            int pageSize = GetScenarioPageSize(state);
            int start = state.Page * pageSize;
            int count = Math.Min(pageSize, Math.Max(0, scenarios.Length - start));
            List<ScenarioListEntry> entries = new List<ScenarioListEntry>(count);

            for (int i = 0; i < count; i++)
            {
                CustomScenarioInfo scenario = scenarios[start + i];
                entries.Add(new ScenarioListEntry
                {
                    ScenarioIndex = start + i,
                    Label = scenario != null ? scenario.DisplayName : "Unknown Scenario"
                });
            }

            return entries.ToArray();
        }

        private static string BuildBrowserDescription(BrowserPanelState state, int scenarioCount)
        {
            int totalPages = GetTotalPages(state, scenarioCount);
            return scenarioCount + " custom scenario(s) available. Page " + (state.Page + 1) + " of " + totalPages
                + ". Use " + PreviousPageLabel + " and " + NextPageLabel + " to browse, or choose " + AddNewLabel + " to start a new draft.";
        }

        private static void SetPage(BrowserPanelState state, int page, int scenarioCount)
        {
            int totalPages = GetTotalPages(state, scenarioCount);
            if (page < 0)
                page = 0;
            if (page >= totalPages)
                page = totalPages - 1;
            state.Page = page;
        }

        private static void ClampPage(BrowserPanelState state, int scenarioCount)
        {
            SetPage(state, state.Page, scenarioCount);
        }

        private static int GetScenarioPageSize(BrowserPanelState state)
        {
            int pageSize = state.BaseButtonCount;
            if (pageSize <= 0)
                pageSize = state.OriginalButtons.Count;
            return Math.Max(1, pageSize);
        }

        private static int GetTotalPages(BrowserPanelState state, int scenarioCount)
        {
            if (scenarioCount <= 0)
                return 1;

            int pageSize = GetScenarioPageSize(state);
            return Math.Max(1, (scenarioCount + pageSize - 1) / pageSize);
        }

        private void EnsurePagingUi(ScenarioSelectionPanel panel, BrowserPanelState state, UIButton sourceButton)
        {
            if (panel == null || state == null || sourceButton == null || state.PagingUi != null)
                return;

            UILabel templateLabel = sourceButton.GetComponentInChildren<UILabel>(true);
            if (templateLabel == null)
                templateLabel = panel.GetComponentInChildren<UILabel>(true);
            if (templateLabel == null)
                return;

            Transform root = sourceButton.transform.parent != null ? sourceButton.transform.parent : panel.transform;
            ScenarioLayoutMetrics metrics = GetOrCreateLayoutMetrics(state, sourceButton);
            ScenarioPagingUi pagingUi = new ScenarioPagingUi();

            UIButton addNewButton = CloneScenarioButton(sourceButton, root, "ShelteredAPI_CustomScenario_AddButton");
            if (addNewButton != null)
            {
                addNewButton.transform.localPosition = metrics.AddNewButtonPosition;
                ConfigureButton(addNewButton.gameObject, AddNewLabel, ScenarioButtonVisualStyle.Action);
                BindPressGuard(panel, addNewButton.gameObject);
                UIEventListener.Get(addNewButton.gameObject).onClick = delegate(GameObject go)
                {
                    ExecuteGuardedUiClick(panel, delegate
                    {
                        StartNewScenarioEditor(panel);
                    });
                };
                addNewButton.gameObject.SetActive(false);
                pagingUi.AddNewButton = addNewButton;
                LogUiElementLayout(panel, "AddNewButton", addNewButton.gameObject, "action");
            }

            pagingUi.PreviousButton = CreatePagingButton(
                panel,
                state,
                sourceButton,
                root,
                "ShelteredAPI_CustomScenario_PrevButton",
                PreviousPageLabel,
                metrics.FooterPreviousPosition,
                -1);
            pagingUi.NextButton = CreatePagingButton(
                panel,
                state,
                sourceButton,
                root,
                "ShelteredAPI_CustomScenario_NextButton",
                NextPageLabel,
                metrics.FooterNextPosition,
                1);

            GameObject pageObject = NGUITools.AddChild(root.gameObject, templateLabel.gameObject);
            pageObject.name = "ShelteredAPI_CustomScenario_PageLabel";
            pageObject.transform.localPosition = metrics.FooterCenterPosition;
            UILabel pageLabel = pageObject.GetComponent<UILabel>();
            if (pageLabel != null)
            {
                pageLabel.text = "Page 1 / 1";
                pageLabel.fontSize = 20;
                pageLabel.alignment = NGUIText.Alignment.Center;
                pageLabel.color = PageIndicatorColor;
                pageLabel.effectStyle = UILabel.Effect.Outline;
                pageLabel.effectColor = new Color(0f, 0f, 0f, 0.85f);
                pageLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            }
            pageObject.SetActive(false);
            pagingUi.PageLabel = pageLabel;

            state.PagingUi = pagingUi;
            LogUiElementLayout(panel, "PageLabel", pageObject, "page-indicator");
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Paging UI created. panel=" + panel.GetInstanceID()
                + " layout=" + DescribeLayoutMetrics(metrics) + ".");
        }

        private UIButton CreatePagingButton(
            ScenarioSelectionPanel panel,
            BrowserPanelState state,
            UIButton sourceButton,
            Transform root,
            string objectName,
            string label,
            Vector3 position,
            int delta)
        {
            UIButton button = CloneScenarioButton(sourceButton, root, objectName);
            if (button == null || button.gameObject == null)
                return null;

            GameObject buttonObject = button.gameObject;
            buttonObject.transform.localPosition = SnapLocalPosition(position);
            ConfigureButton(buttonObject, label, ScenarioButtonVisualStyle.PagingEnabled);
            BindPressGuard(panel, buttonObject);
            UIEventListener.Get(buttonObject).onClick = delegate(GameObject go)
            {
                ExecuteGuardedUiClick(panel, delegate
                {
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Paging button clicked. panel=" + panel.GetInstanceID()
                        + " label=" + label + " delta=" + delta + ".");
                    TryChangePage(panel, state, delta);
                });
            };
            buttonObject.SetActive(false);
            LogUiElementLayout(panel, objectName, buttonObject, "paging");
            return button;
        }

        private static void SetPagingUiVisible(BrowserPanelState state, bool visible)
        {
            if (state == null || state.PagingUi == null)
                return;

            SetButtonVisible(state.PagingUi.AddNewButton, visible);
            SetButtonVisible(state.PagingUi.PreviousButton, visible);
            SetButtonVisible(state.PagingUi.NextButton, visible);
            if (state.PagingUi.PageLabel != null && state.PagingUi.PageLabel.gameObject != null)
                state.PagingUi.PageLabel.gameObject.SetActive(visible);
        }

        private static void SetButtonVisible(UIButton button, bool visible)
        {
            if (button != null && button.gameObject != null)
                button.gameObject.SetActive(visible);
        }

        private static void DestroyPagingUi(BrowserPanelState state)
        {
            if (state == null || state.PagingUi == null)
                return;

            DestroyPagingButton(state.PagingUi.AddNewButton);
            DestroyPagingButton(state.PagingUi.PreviousButton);
            DestroyPagingButton(state.PagingUi.NextButton);
            if (state.PagingUi.PageLabel != null && state.PagingUi.PageLabel.gameObject != null)
                UnityEngine.Object.Destroy(state.PagingUi.PageLabel.gameObject);

            state.PagingUi = null;
        }

        private static void DestroyPagingButton(UIButton button)
        {
            if (button != null && button.gameObject != null)
                UnityEngine.Object.Destroy(button.gameObject);
        }

        private static void UpdatePagingUi(BrowserPanelState state, int scenarioCount)
        {
            if (state == null || state.PagingUi == null)
                return;

            int totalPages = GetTotalPages(state, scenarioCount);
            if (state.PagingUi.PageLabel != null)
                state.PagingUi.PageLabel.text = "Page " + (state.Page + 1) + " of " + totalPages;

            UpdatePagingButtonState(state.PagingUi.PreviousButton, state.Page > 0);
            UpdatePagingButtonState(state.PagingUi.NextButton, state.Page + 1 < totalPages);
            if (state.LastLoggedPagingPage != state.Page
                || state.LastLoggedPagingTotalPages != totalPages
                || state.LastLoggedPagingScenarioCount != scenarioCount)
            {
                state.LastLoggedPagingPage = state.Page;
                state.LastLoggedPagingTotalPages = totalPages;
                state.LastLoggedPagingScenarioCount = scenarioCount;
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Paging UI updated. page=" + (state.Page + 1)
                    + "/" + totalPages + " scenarioCount=" + scenarioCount + ".");
            }
        }

        private static void UpdatePagingButtonState(UIButton button, bool enabled)
        {
            if (button == null)
                return;

            button.isEnabled = enabled;
            ConfigureButton(
                button.gameObject,
                GetButtonLabelText(button.gameObject),
                enabled ? ScenarioButtonVisualStyle.PagingEnabled : ScenarioButtonVisualStyle.PagingDisabled);
            button.SetState(enabled ? UIButtonColor.State.Normal : UIButtonColor.State.Disabled, true);
        }

        private bool TryChangePage(ScenarioSelectionPanel panel, BrowserPanelState state, int delta)
        {
            if (panel == null || state == null || delta == 0 || !state.IsCustomMode)
                return false;

            RefreshDefinitionCatalogSafely();
            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            int currentPage = state.Page;
            SetPage(state, currentPage + delta, scenarios.Length);
            if (state.Page == currentPage)
            {
                UpdatePagingUi(state, scenarios.Length);
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Page change ignored. currentPage=" + (currentPage + 1)
                    + " delta=" + delta + " totalPages=" + GetTotalPages(state, scenarios.Length) + ".");
                return false;
            }

            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Page changed. from=" + (currentPage + 1)
                + " to=" + (state.Page + 1) + " totalPages=" + GetTotalPages(state, scenarios.Length) + ".");
            List<UIButton> scenarioButtons = Traverse.Create(panel).Field("m_scenarioButtons").GetValue<List<UIButton>>();
            EnterCustomMode(panel, state, scenarioButtons);
            return true;
        }

        private static UIButton CloneScenarioButton(UIButton sourceButton, Transform parent, string objectName)
        {
            if (sourceButton == null || sourceButton.gameObject == null)
                return null;

            UIButton button = UIUtil.CloneButton(sourceButton, parent, string.Empty);
            if (button == null || button.gameObject == null)
                return null;

            GameObject buttonObject = button.gameObject;
            buttonObject.name = objectName;
            buttonObject.SetActive(true);
            // UIUtil.CloneButton strips the most common inherited NGUI behaviors, but we also
            // reset the root listener here so every custom scenario button starts from a known
            // blank input state before we attach our own handlers.
            UIEventListener rootListener = buttonObject.GetComponent<UIEventListener>();
            if (rootListener != null)
            {
                rootListener.onSubmit = null;
                rootListener.onClick = null;
                rootListener.onDoubleClick = null;
                rootListener.onHover = null;
                rootListener.onPress = null;
                rootListener.onSelect = null;
                rootListener.onScroll = null;
                rootListener.onDrag = null;
                rootListener.onDrop = null;
                rootListener.onKey = null;
            }
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Cloned button via UIUtil. source=" + sourceButton.name
                + " clone=" + objectName + ".");
            return button;
        }

        private static void BindPressGuard(ScenarioSelectionPanel panel, GameObject buttonObject)
        {
            UIEventListener.Get(buttonObject).onPress = delegate(GameObject go, bool pressed)
            {
                if (pressed)
                {
                    UIFlowGuard.BlockSlotClicksToggle(true);
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                    if (panel != null)
                        UIUtil.PushClickBlocker(panel.transform, 99999);
                }
                else if (panel != null)
                {
                    panel.StartCoroutine(ReleaseFlowGuardNextFrame());
                }
            };
        }

        private static void ExecuteGuardedUiClick(ScenarioSelectionPanel panel, Action action)
        {
            ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
            UIFlowGuard.BlockSlotClicksOnce(panel);
            if (panel != null)
            {
                UIUtil.PushClickBlocker(panel.transform, 99999);
                panel.StartCoroutine(ReleaseFlowGuardNextFrame());
            }

            if (action != null)
                action();
        }

        private static IEnumerator ReleaseFlowGuardNextFrame()
        {
            yield return null;
            UIFlowGuard.BlockSlotClicksToggle(false);
            UIUtil.PopClickBlocker();
        }

        private static void ConfigureButton(GameObject buttonObject, string label, ScenarioButtonVisualStyle style)
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
            Color labelColor;
            Color defaultColor;
            Color hoverColor;
            Color pressedColor;
            Color disabledColor;
            ResolveButtonVisualStyle(style, out defaultColor, out hoverColor, out pressedColor, out disabledColor, out labelColor);
            UILabel primaryLabel = GetPrimaryLabel(labels);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null)
                    continue;

                bool isPrimary = labels[i] == primaryLabel;
                labels[i].enabled = isPrimary;
                labels[i].text = isPrimary ? label ?? string.Empty : string.Empty;
                labels[i].color = labelColor;
                labels[i].effectStyle = UILabel.Effect.Outline;
                labels[i].effectColor = new Color(0f, 0f, 0f, 0.75f);
                labels[i].overflowMethod = UILabel.Overflow.ShrinkContent;
                labels[i].alignment = NGUIText.Alignment.Center;
            }

            int targetWidth = style == ScenarioButtonVisualStyle.PagingEnabled || style == ScenarioButtonVisualStyle.PagingDisabled
                ? CompactPagingButtonWidth
                : CompactButtonWidth;
            int targetHeight = style == ScenarioButtonVisualStyle.PagingEnabled || style == ScenarioButtonVisualStyle.PagingDisabled
                ? CompactPagingButtonHeight
                : CompactButtonHeight;
            int targetFontSize = style == ScenarioButtonVisualStyle.PagingEnabled || style == ScenarioButtonVisualStyle.PagingDisabled
                ? CompactPagingButtonFontSize
                : CompactButtonFontSize;
            ApplyButtonSizing(buttonObject, primaryLabel, targetWidth, targetHeight, targetFontSize);

            if (primaryLabel != null)
            {
                primaryLabel.ProcessText();
                primaryLabel.MarkAsChanged();
            }

            if (button != null)
            {
                button.defaultColor = defaultColor;
                button.hover = hoverColor;
                button.pressed = pressedColor;
                button.disabledColor = disabledColor;
                button.duration = 0.08f;
                button.SetState(button.isEnabled ? UIButtonColor.State.Normal : UIButtonColor.State.Disabled, true);
            }

            NGUITools.UpdateWidgetCollider(buttonObject, true);
        }

        private static void SetScenarioText(
            BrowserPanelState state,
            ScenarioSelectionPanel panel,
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

            if (state != null)
            {
                string normalizedDescription = description ?? string.Empty;
                if (normalizedDescription.Length > 160)
                    normalizedDescription = normalizedDescription.Substring(0, 160) + "...";

                string key = (name ?? string.Empty) + "|" + normalizedDescription;
                if (!string.Equals(state.LastLoggedScenarioTextKey, key, StringComparison.Ordinal))
                {
                    state.LastLoggedScenarioTextKey = key;
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Scenario text updated. panel="
                        + (panel != null ? panel.GetInstanceID().ToString() : "<unknown>")
                        + " title='" + (name ?? string.Empty) + "' desc='" + normalizedDescription.Replace("\n", "\\n") + "'.");
                }
            }
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
            }
            catch
            {
            }

            return spacingY;
        }

        private static ScenarioLayoutMetrics GetOrCreateLayoutMetrics(BrowserPanelState state, UIButton sourceButton)
        {
            if (state.LayoutMetrics == null)
                state.LayoutMetrics = BuildLayoutMetrics(state, sourceButton);

            return state.LayoutMetrics;
        }

        private static ScenarioLayoutMetrics BuildLayoutMetrics(BrowserPanelState state, UIButton sourceButton)
        {
            ScenarioLayoutMetrics metrics = new ScenarioLayoutMetrics();
            metrics.SpacingY = MeasureSpacing(state != null ? state.OriginalButtons : null);

            Vector3 anchorPosition = sourceButton != null ? sourceButton.transform.localPosition : Vector3.zero;
            if (state != null && state.OriginalButtons.Count > 0 && state.OriginalButtons[0] != null)
                anchorPosition = state.OriginalButtons[0].transform.localPosition;
            metrics.TopSlotPosition = SnapLocalPosition(anchorPosition);

            Bounds bounds = sourceButton != null ? NGUIMath.CalculateRelativeWidgetBounds(sourceButton.transform, true) : new Bounds(Vector3.zero, Vector3.zero);
            metrics.ButtonWidth = Mathf.Clamp(bounds.size.x > 1f ? bounds.size.x : CompactButtonWidth, 280f, CompactButtonWidth);
            metrics.ButtonHeight = Mathf.Clamp(bounds.size.y > 1f ? bounds.size.y : CompactButtonHeight, 60f, CompactButtonHeight);

            int pageSize = state != null ? GetScenarioPageSize(state) : 1;
            if (state != null)
            {
                for (int i = 0; i < state.OriginalButtons.Count && metrics.ScenarioSlotPositions.Count < pageSize; i++)
                {
                    UIButton original = state.OriginalButtons[i];
                    if (original != null && original.gameObject != null)
                        metrics.ScenarioSlotPositions.Add(SnapLocalPosition(original.transform.localPosition));
                }
            }

            while (metrics.ScenarioSlotPositions.Count < pageSize)
            {
                int slotIndex = metrics.ScenarioSlotPositions.Count;
                Vector3 position = metrics.TopSlotPosition + new Vector3(0f, metrics.SpacingY * slotIndex, 0f);
                metrics.ScenarioSlotPositions.Add(SnapLocalPosition(position));
            }

            Vector3 lastVisibleSlotPosition = metrics.ScenarioSlotPositions.Count > 0
                ? metrics.ScenarioSlotPositions[metrics.ScenarioSlotPositions.Count - 1]
                : metrics.TopSlotPosition;

            float footerButtonY = Mathf.Min(lastVisibleSlotPosition.y - 84f, -100f);
            metrics.AddNewButtonPosition = SnapLocalPosition(new Vector3(metrics.TopSlotPosition.x, footerButtonY, metrics.TopSlotPosition.z));

            float footerOffsetX = Mathf.Clamp(metrics.ButtonWidth * 0.52f, 150f, 180f);
            Vector3 footerCenter = new Vector3(metrics.TopSlotPosition.x, Mathf.Min(metrics.AddNewButtonPosition.y - 82f, -190f), metrics.TopSlotPosition.z);
            metrics.FooterCenterPosition = SnapLocalPosition(footerCenter);
            metrics.FooterPreviousPosition = SnapLocalPosition(footerCenter + new Vector3(-footerOffsetX, 0f, 0f));
            metrics.FooterNextPosition = SnapLocalPosition(footerCenter + new Vector3(footerOffsetX, 0f, 0f));

            // Keep the browse hub away from the cloned vanilla scenario button positions.
            // The Add New button still uses the original lower action slot once we are in custom mode.
            metrics.HubButtonPosition = metrics.FooterCenterPosition;
            return metrics;
        }

        private static void ResolveButtonVisualStyle(
            ScenarioButtonVisualStyle style,
            out Color defaultColor,
            out Color hoverColor,
            out Color pressedColor,
            out Color disabledColor,
            out Color labelColor)
        {
            switch (style)
            {
                case ScenarioButtonVisualStyle.Hub:
                    defaultColor = HubButtonColor;
                    hoverColor = HubHoverColor;
                    pressedColor = HubPressedColor;
                    disabledColor = ButtonDisabledColor;
                    labelColor = BrightLabelColor;
                    break;
                case ScenarioButtonVisualStyle.Action:
                    defaultColor = ActionButtonColor;
                    hoverColor = ActionHoverColor;
                    pressedColor = ActionPressedColor;
                    disabledColor = ButtonDisabledColor;
                    labelColor = BrightLabelColor;
                    break;
                case ScenarioButtonVisualStyle.Locked:
                    defaultColor = LockedButtonColor;
                    hoverColor = LockedHoverColor;
                    pressedColor = LockedPressedColor;
                    disabledColor = ButtonDisabledColor;
                    labelColor = LockedLabelColor;
                    break;
                case ScenarioButtonVisualStyle.PagingEnabled:
                    defaultColor = new Color(0.85f, 0.73f, 0.60f, 1f);
                    hoverColor = new Color(0.95f, 0.83f, 0.68f, 1f);
                    pressedColor = new Color(0.71f, 0.58f, 0.46f, 1f);
                    disabledColor = PagingDisabledLabelColor;
                    labelColor = PagingLabelColor;
                    break;
                case ScenarioButtonVisualStyle.PagingDisabled:
                    defaultColor = new Color(0.56f, 0.48f, 0.40f, 0.95f);
                    hoverColor = defaultColor;
                    pressedColor = defaultColor;
                    disabledColor = defaultColor;
                    labelColor = PagingDisabledLabelColor;
                    break;
                default:
                    defaultColor = AvailableButtonColor;
                    hoverColor = AvailableHoverColor;
                    pressedColor = AvailablePressedColor;
                    disabledColor = ButtonDisabledColor;
                    labelColor = BrightLabelColor;
                    break;
            }
        }

        private static string GetButtonLabelText(GameObject buttonObject)
        {
            if (buttonObject == null)
                return string.Empty;

            UILabel[] labels = buttonObject.GetComponentsInChildren<UILabel>(true);
            UILabel primaryLabel = GetPrimaryLabel(labels);
            return primaryLabel != null ? primaryLabel.text ?? string.Empty : string.Empty;
        }

        private static Vector3 SnapLocalPosition(Vector3 value)
        {
            return new Vector3(Mathf.Round(value.x), Mathf.Round(value.y), Mathf.Round(value.z));
        }

        private static void LogUiElementLayout(ScenarioSelectionPanel panel, string role, GameObject gameObject, string theme)
        {
            if (gameObject == null)
                return;

            Bounds bounds = NGUIMath.CalculateRelativeWidgetBounds(gameObject.transform, true);
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Layout. panel="
                + (panel != null ? panel.GetInstanceID().ToString() : "<unknown>")
                + " role=" + role
                + " theme=" + theme
                + " pos=" + FormatVector(gameObject.transform.localPosition)
                + " size=" + FormatVector(bounds.size) + ".");
        }

        private static string DescribeLayoutMetrics(ScenarioLayoutMetrics metrics)
        {
            if (metrics == null)
                return "<none>";

            return "spacingY=" + metrics.SpacingY
                + " top=" + FormatVector(metrics.TopSlotPosition)
                + " slots=" + metrics.ScenarioSlotPositions.Count
                + " hub=" + FormatVector(metrics.HubButtonPosition)
                + " addNew=" + FormatVector(metrics.AddNewButtonPosition)
                + " footerPrev=" + FormatVector(metrics.FooterPreviousPosition)
                + " footerCenter=" + FormatVector(metrics.FooterCenterPosition)
                + " footerNext=" + FormatVector(metrics.FooterNextPosition)
                + " buttonSize=" + FormatVector(new Vector3(metrics.ButtonWidth, metrics.ButtonHeight, 0f));
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " + value.z.ToString("0.##") + ")";
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

        private static void RefreshDefinitionCatalogSafely()
        {
            try
            {
                ShelteredCustomScenarioService.Instance.RefreshDefinitionCatalog();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to refresh XML scenario catalog: " + ex.Message);
            }
        }

        private static string DescribeEntries(ScenarioListEntry[] entries, CustomScenarioInfo[] scenarios)
        {
            if (entries == null || entries.Length == 0)
                return "<none>";

            List<string> parts = new List<string>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                ScenarioListEntry entry = entries[i];
                if (entry == null)
                    continue;

                string scenarioId = "<missing>";
                if (scenarios != null
                    && entry.ScenarioIndex >= 0
                    && entry.ScenarioIndex < scenarios.Length
                    && scenarios[entry.ScenarioIndex] != null
                    && !string.IsNullOrEmpty(scenarios[entry.ScenarioIndex].Id))
                {
                    scenarioId = scenarios[entry.ScenarioIndex].Id;
                }

                parts.Add(i + ":" + scenarioId);
            }

            return string.Join(", ", parts.ToArray());
        }

        private static Vector3 GetScenarioSlotPosition(ScenarioLayoutMetrics metrics, int slotIndex)
        {
            if (metrics != null
                && metrics.ScenarioSlotPositions.Count > 0
                && slotIndex >= 0
                && slotIndex < metrics.ScenarioSlotPositions.Count)
            {
                return metrics.ScenarioSlotPositions[slotIndex];
            }

            if (metrics == null)
                return Vector3.zero;

            return SnapLocalPosition(metrics.TopSlotPosition + new Vector3(0f, metrics.SpacingY * slotIndex, 0f));
        }

        private static void UpdateCustomModeSupplementaryLayout(BrowserPanelState state, int visibleScenarioCount)
        {
            if (state == null || state.PagingUi == null)
                return;

            ScenarioLayoutMetrics metrics = state.LayoutMetrics;
            if (metrics == null)
                return;

            if (state.PagingUi.AddNewButton != null && state.PagingUi.AddNewButton.gameObject != null)
                state.PagingUi.AddNewButton.transform.localPosition = GetAddNewButtonPosition(metrics, visibleScenarioCount);
            if (state.PagingUi.PreviousButton != null && state.PagingUi.PreviousButton.gameObject != null)
                state.PagingUi.PreviousButton.transform.localPosition = metrics.FooterPreviousPosition;
            if (state.PagingUi.NextButton != null && state.PagingUi.NextButton.gameObject != null)
                state.PagingUi.NextButton.transform.localPosition = metrics.FooterNextPosition;
            if (state.PagingUi.PageLabel != null && state.PagingUi.PageLabel.gameObject != null)
                state.PagingUi.PageLabel.transform.localPosition = metrics.FooterCenterPosition;
        }

        private static Vector3 GetAddNewButtonPosition(ScenarioLayoutMetrics metrics, int visibleScenarioCount)
        {
            if (metrics == null)
                return Vector3.zero;

            if (visibleScenarioCount >= 0 && visibleScenarioCount < metrics.ScenarioSlotPositions.Count)
                return metrics.ScenarioSlotPositions[visibleScenarioCount];

            return metrics.AddNewButtonPosition;
        }

        private static UILabel GetPrimaryLabel(IList<UILabel> labels)
        {
            if (labels == null)
                return null;

            UILabel best = null;
            int bestWidth = int.MinValue;
            for (int i = 0; i < labels.Count; i++)
            {
                UILabel label = labels[i];
                if (label == null)
                    continue;

                int score = Math.Max(label.width, label.fontSize);
                if (best == null || score > bestWidth)
                {
                    best = label;
                    bestWidth = score;
                }
            }

            return best;
        }

        private static void ApplyButtonSizing(GameObject buttonObject, UILabel primaryLabel, int width, int height, int fontSize)
        {
            if (buttonObject == null)
                return;

            UIWidget[] widgets = buttonObject.GetComponentsInChildren<UIWidget>(true);
            UIWidget backgroundWidget = null;
            int bestArea = int.MinValue;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null || widget is UILabel)
                    continue;

                int area = widget.width * widget.height;
                if (backgroundWidget == null || area > bestArea)
                {
                    backgroundWidget = widget;
                    bestArea = area;
                }
            }

            if (backgroundWidget == null)
                backgroundWidget = buttonObject.GetComponent<UIWidget>();

            if (backgroundWidget != null)
            {
                backgroundWidget.width = width;
                backgroundWidget.height = height;
            }

            if (primaryLabel != null)
            {
                primaryLabel.width = Mathf.Max(80, width - 22);
                primaryLabel.fontSize = fontSize;
                primaryLabel.pivot = UIWidget.Pivot.Center;
                primaryLabel.transform.localPosition = new Vector3(0f, 0f, primaryLabel.transform.localPosition.z);
            }
        }
    }
}
