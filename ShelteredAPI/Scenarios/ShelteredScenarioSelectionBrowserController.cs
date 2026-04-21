using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
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

                float spacingY = MeasureSpacing(state.OriginalButtons);
                UIButton hubButton = CloneScenarioButton(sourceButton, sourceButton.transform.parent, "ShelteredAPI_CustomScenarios_HubButton");
                if (hubButton == null)
                    return;

                hubButton.transform.localPosition = sourceButton.transform.localPosition + new Vector3(0f, spacingY, 0f);
                ConfigureButton(hubButton.gameObject, HubLabel, false);
                BindPressGuard(hubButton.gameObject);
                UIEventListener.Get(hubButton.gameObject).onClick = delegate(GameObject go)
                {
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Hub clicked. panel=" + panel.GetInstanceID() + ".");
                    EnterCustomMode(panel, state, scenarioButtons);
                };

                scenarioButtons.Add(hubButton);
                state.BaseButtonCount = scenarioButtons.Count - 1;
                state.HubButton = hubButton;
                EnsurePagingUi(panel, state, sourceButton, spacingY);
                state.ButtonsCreated = true;

                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Added custom scenario hub and paging UI. panel="
                    + panel.GetInstanceID() + " scenarios=" + scenarios.Length + " spacingY=" + spacingY + ".");
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
                if (state.LastLoggedVanillaSelectedScenario != selectedScenario)
                {
                    state.LastLoggedVanillaSelectedScenario = selectedScenario;
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Vanilla selection changed. panel="
                        + panel.GetInstanceID() + " selectedIndex=" + selectedScenario + " customHubIndex=" + baseCount + ".");
                }
                if (selectedScenario == baseCount)
                {
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
            if (state.LastLoggedCustomSelectedScenario != selectedScenario)
            {
                state.LastLoggedCustomSelectedScenario = selectedScenario;
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Custom selection changed. panel="
                    + panel.GetInstanceID() + " selectedIndex=" + selectedScenario
                    + " visibleEntries=" + entries.Length + " page=" + (state.Page + 1) + ".");
            }
            if (selectedScenario >= 0 && selectedScenario < entries.Length)
            {
                CustomScenarioInfo scenario = scenarios[entries[selectedScenario].ScenarioIndex];
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Custom scenario highlighted. panel=" + panel.GetInstanceID()
                    + " selectedIndex=" + selectedScenario + " scenarioId=" + scenario.Id + ".");
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

            RefreshDefinitionCatalogSafely();
            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
            ScenarioListEntry[] entries = BuildVisibleEntries(state, scenarios);
            if (selectedScenario < 0 || selectedScenario >= entries.Length)
            {
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Custom scenario chosen with out-of-range selection. panel="
                    + panel.GetInstanceID() + " selectedIndex=" + selectedScenario + " visibleEntries=" + entries.Length + ".");
                return false;
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
                return false;
            }

            if (!StartCustomScenario(panel, state, scenarioButtons, scenario))
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to select custom scenario: " + scenario.Id);
                return false;
            }

            return true;
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

            Vector3 basePosition = sourceButton.transform.localPosition;
            if (state.OriginalButtons.Count > 0 && state.OriginalButtons[0] != null)
                basePosition = state.OriginalButtons[0].transform.localPosition;

            float spacingY = MeasureSpacing(state.OriginalButtons);
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

                button.transform.localPosition = basePosition + new Vector3(0f, spacingY * i, 0f);
                bool locked = ShelteredCustomScenarioService.Instance.VerifyDependencies(scenario) != SaveVerification.VerificationState.Match;
                ConfigureButton(button.gameObject, locked ? entry.Label + " [LOCKED]" : entry.Label, locked);
                BindPressGuard(button.gameObject);
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Created scenario button. panel=" + panel.GetInstanceID()
                    + " slot=" + i + " scenarioId=" + scenario.Id + " locked=" + locked + ".");

                int capturedIndex = i;
                UIEventListener.Get(button.gameObject).onClick = delegate(GameObject go)
                {
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                    MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Scenario button clicked. panel=" + panel.GetInstanceID()
                        + " slot=" + capturedIndex + " scenarioId=" + scenario.Id + ".");
                    Traverse.Create(panel).Field("m_selectedScenario").SetValue(capturedIndex);
                    panel.OnScenarioChosen();
                };

                state.CustomButtons.Add(button);
                scenarioButtons.Add(button);
            }

            state.IsCustomMode = true;
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

            ExitCustomMode(panel, state, scenarioButtons);
            ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Selected custom scenario: " + scenario.Id);
            return true;
        }

        private static void StartNewScenarioEditor(ScenarioSelectionPanel panel)
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
                ScenarioEditorSession session = ScenarioEditorController.Instance.EnterEditMode(ScenarioBaseGameMode.Survival);
                string id = session != null && session.WorkingDefinition != null ? session.WorkingDefinition.Id : "new scenario";
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
                    "Created an in-memory custom scenario draft (" + id + "). The scenario editor backend is active; a full visual authoring form is still required to edit and save fields from this menu.");
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Started new custom scenario editor session: " + id + ".");
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
                    "Could not start the scenario editor: " + ex.Message);
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to start scenario editor: " + ex.Message);
            }
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

        private void EnsurePagingUi(ScenarioSelectionPanel panel, BrowserPanelState state, UIButton sourceButton, float spacingY)
        {
            if (panel == null || state == null || sourceButton == null || state.PagingUi != null)
                return;

            UILabel templateLabel = sourceButton.GetComponentInChildren<UILabel>(true);
            if (templateLabel == null)
                templateLabel = panel.GetComponentInChildren<UILabel>(true);
            if (templateLabel == null)
                return;

            Transform root = sourceButton.transform.parent != null ? sourceButton.transform.parent : panel.transform;
            ScenarioPagingUi pagingUi = new ScenarioPagingUi();

            UIButton addNewButton = CloneScenarioButton(sourceButton, root, "ShelteredAPI_CustomScenario_AddButton");
            if (addNewButton != null)
            {
                addNewButton.transform.localPosition = sourceButton.transform.localPosition + new Vector3(0f, spacingY, 0f);
                ConfigureButton(addNewButton.gameObject, AddNewLabel, false);
                BindPressGuard(addNewButton.gameObject);
                UIEventListener.Get(addNewButton.gameObject).onClick = delegate(GameObject go)
                {
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                    StartNewScenarioEditor(panel);
                };
                addNewButton.gameObject.SetActive(false);
                pagingUi.AddNewButton = addNewButton;
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Created Add New button. panel=" + panel.GetInstanceID() + ".");
            }

            pagingUi.PreviousButton = CreatePagingButton(panel, state, root, templateLabel, "ShelteredAPI_CustomScenario_PrevButton", PreviousPageLabel, new Vector3(-280f, -200f, 0f), -1);
            pagingUi.NextButton = CreatePagingButton(panel, state, root, templateLabel, "ShelteredAPI_CustomScenario_NextButton", NextPageLabel, new Vector3(280f, -200f, 0f), 1);

            GameObject pageObject = NGUITools.AddChild(root.gameObject, templateLabel.gameObject);
            pageObject.name = "ShelteredAPI_CustomScenario_PageLabel";
            pageObject.transform.localPosition = new Vector3(0f, -200f, 0f);
            UILabel pageLabel = pageObject.GetComponent<UILabel>();
            if (pageLabel != null)
                pageLabel.text = "Page 1 / 1";
            pageObject.SetActive(false);
            pagingUi.PageLabel = pageLabel;

            state.PagingUi = pagingUi;
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Paging UI created. panel=" + panel.GetInstanceID() + ".");
        }

        private UIButton CreatePagingButton(
            ScenarioSelectionPanel panel,
            BrowserPanelState state,
            Transform root,
            UILabel templateLabel,
            string objectName,
            string label,
            Vector3 position,
            int delta)
        {
            GameObject buttonObject = NGUITools.AddChild(root.gameObject, templateLabel.gameObject);
            buttonObject.name = objectName;
            buttonObject.transform.localPosition = position;
            UILabel buttonLabel = buttonObject.GetComponent<UILabel>();
            if (buttonLabel != null)
            {
                buttonLabel.text = label;
                buttonLabel.fontSize = 18;
            }

            NGUITools.AddWidgetCollider(buttonObject);
            UIButton button = buttonObject.GetComponent<UIButton>();
            if (button == null)
                button = buttonObject.AddComponent<UIButton>();
            BindPressGuard(buttonObject);
            UIEventListener.Get(buttonObject).onClick = delegate(GameObject go)
            {
                ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Paging button clicked. panel=" + panel.GetInstanceID()
                    + " label=" + label + " delta=" + delta + ".");
                TryChangePage(panel, state, delta);
            };
            buttonObject.SetActive(false);
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
                state.PagingUi.PageLabel.text = "Page " + (state.Page + 1) + " / " + totalPages;

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
            UILabel label = button.GetComponent<UILabel>();
            if (label != null)
                label.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
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
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Cloned button via UIUtil. source=" + sourceButton.name
                + " clone=" + objectName + ".");
            return button;
        }

        private static void BindPressGuard(GameObject buttonObject)
        {
            UIEventListener.Get(buttonObject).onPress = delegate(GameObject go, bool pressed)
            {
                if (pressed)
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
            };
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
                if (labels[i] == null)
                    continue;

                labels[i].text = label ?? string.Empty;
                labels[i].color = locked ? new Color(1f, 0.35f, 0.35f) : Color.white;
            }
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
    }
}
