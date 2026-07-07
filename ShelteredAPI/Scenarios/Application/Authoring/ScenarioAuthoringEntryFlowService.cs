using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;
using UnityEngine.UI;

using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringEntryFlowService
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioAuthoring.EntryFlow";
        private const string ActionBlank = "entry.blank";
        private const string ActionCancel = "entry.cancel";
        private const string ActionModePrefix = "entry.mode.";
        private const string ActionScenarioPrefix = "entry.scenario.";

        private readonly object _sync = new object();
        private readonly IScenarioEditorService _editorService;
        private readonly IScenarioSelectionCatalogService _catalog;
        private readonly IScenarioDefinitionCatalogService _definitionCatalog;
        private readonly ScenarioAuthoringBaseModeReloadService _baseModeReloadService;
        private readonly ScenarioAuthoringSettingsService _settingsService;

        private ScenarioAuthoringEntryFlowRuntime _runtime;
        private ScenarioAuthoringEntryFlowSnapshot _snapshot = new ScenarioAuthoringEntryFlowSnapshot();
        private bool _showBaselinePickerAfterReady;
        private string _activeDraftId;

        public ScenarioAuthoringEntryFlowService(
            IScenarioEditorService editorService,
            IScenarioSelectionCatalogService catalog,
            IScenarioDefinitionCatalogService definitionCatalog,
            ScenarioAuthoringBaseModeReloadService baseModeReloadService,
            ScenarioAuthoringSettingsService settingsService)
        {
            _editorService = editorService;
            _catalog = catalog;
            _definitionCatalog = definitionCatalog;
            _baseModeReloadService = baseModeReloadService;
            _settingsService = settingsService;
        }

        public void BeginNewDraftLaunch(ScenarioAuthoringSession session, bool showBaselinePicker)
        {
            _showBaselinePickerAfterReady = showBaselinePicker;
            _activeDraftId = session != null ? session.DraftId : null;
            SetSnapshot(CreateLoadingSnapshot(
                session,
                "Status: game loading - creating the authoring world.",
                showBaselinePicker));
        }

        public void BeginExistingDraftLaunch(ScenarioAuthoringSession session)
        {
            _showBaselinePickerAfterReady = false;
            _activeDraftId = session != null ? session.DraftId : null;
            SetSnapshot(CreateLoadingSnapshot(
                session,
                "Status: game loading - reopening the existing draft.",
                false));
        }

        public void BeginReload(ScenarioAuthoringSession session, string reason)
        {
            _showBaselinePickerAfterReady = false;
            _activeDraftId = session != null ? session.DraftId : null;
            SetSnapshot(CreateLoadingSnapshot(
                session,
                string.IsNullOrEmpty(reason) ? "Status: game loading - reloading the authoring world." : "Status: " + reason,
                false));
        }

        public void SetLoadingStatus(string status)
        {
            lock (_sync)
            {
                if (_snapshot == null || !_snapshot.Visible || _snapshot.Kind != ScenarioAuthoringEntryFlowKind.Loading)
                    return;

                _snapshot.Status = string.IsNullOrEmpty(status) ? _snapshot.Status : status;
            }

            EnsureRuntime();
        }

        public void MarkEditorReady(ScenarioAuthoringSession session)
        {
            if (!_showBaselinePickerAfterReady)
            {
                Hide("Editor ready.");
                return;
            }

            _activeDraftId = session != null ? session.DraftId : _activeDraftId;
            SetSnapshot(new ScenarioAuthoringEntryFlowSnapshot
            {
                Visible = true,
                Kind = ScenarioAuthoringEntryFlowKind.BaselinePicker,
                DraftId = _activeDraftId,
                Title = "Welcome to the Custom Scenario Editor!",
                Flavor = "Begin with an empty shelter, a vanilla scenario base, or a copied custom scenario.",
                Status = "Status: editor ready - choose a starting baseline.",
                Cards = BuildBaselineCards()
            });
        }

        public void Hide(string reason)
        {
            _showBaselinePickerAfterReady = false;
            SetSnapshot(new ScenarioAuthoringEntryFlowSnapshot
            {
                Visible = false,
                Status = reason ?? string.Empty
            });
        }

        public ScenarioAuthoringEntryFlowSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return _snapshot != null ? _snapshot.Copy() : new ScenarioAuthoringEntryFlowSnapshot();
            }
        }

        public void ExecuteEntryAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return;

            if (string.Equals(actionId, ActionBlank, StringComparison.Ordinal))
            {
                Hide("Blank shelter selected.");
                return;
            }

            if (string.Equals(actionId, ActionCancel, StringComparison.Ordinal))
            {
                CancelToMainMenu();
                return;
            }

            if (actionId.StartsWith(ActionModePrefix, StringComparison.Ordinal))
            {
                int value;
                if (int.TryParse(actionId.Substring(ActionModePrefix.Length), out value)
                    && Enum.IsDefined(typeof(ScenarioBaseGameMode), value))
                {
                    ApplyBaseMode((ScenarioBaseGameMode)value);
                }
                return;
            }

            if (actionId.StartsWith(ActionScenarioPrefix, StringComparison.Ordinal))
            {
                ApplyCustomScenario(actionId.Substring(ActionScenarioPrefix.Length));
            }
        }

        private ScenarioAuthoringEntryFlowSnapshot CreateLoadingSnapshot(
            ScenarioAuthoringSession session,
            string status,
            bool pickerPending)
        {
            return new ScenarioAuthoringEntryFlowSnapshot
            {
                Visible = true,
                Kind = ScenarioAuthoringEntryFlowKind.Loading,
                DraftId = session != null ? session.DraftId : null,
                Title = "Welcome to the Custom Scenario Editor!",
                Flavor = "The shelter is loading behind this screen. The editor will attach when Sheltered finishes building the world.",
                Status = status,
                PickerPending = pickerPending
            };
        }

        private ScenarioAuthoringEntryBaselineCard[] BuildBaselineCards()
        {
            List<ScenarioAuthoringEntryBaselineCard> cards = new List<ScenarioAuthoringEntryBaselineCard>();
            cards.Add(new ScenarioAuthoringEntryBaselineCard
            {
                Title = "Blank shelter",
                Badge = "BLANK",
                Detail = "Start with an empty shelter and build everything yourself.",
                Meta = string.Empty,
                ActionId = ActionBlank,
                Enabled = true
            });
            AddBaseModeCard(cards, ScenarioBaseGameMode.Survival, "Standard", "The classic survival start.");
            AddBaseModeCard(cards, ScenarioBaseGameMode.Stasis, "Stasis", "The Stasis scenario world.");
            AddBaseModeCard(cards, ScenarioBaseGameMode.Surrounded, "Surrounded", "The Surrounded scenario world.");
            AddCustomScenarioCards(cards);
            return cards.ToArray();
        }

        private static void AddBaseModeCard(List<ScenarioAuthoringEntryBaselineCard> cards, ScenarioBaseGameMode mode, string title, string detail)
        {
            cards.Add(new ScenarioAuthoringEntryBaselineCard
            {
                Title = title,
                Badge = "BASE",
                Detail = detail,
                Meta = string.Empty,
                ActionId = ActionModePrefix + ((int)mode).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Enabled = true
            });
        }

        private void AddCustomScenarioCards(List<ScenarioAuthoringEntryBaselineCard> cards)
        {
            ScenarioCatalogEntry[] entries = new ScenarioCatalogEntry[0];
            try
            {
                if (_catalog != null)
                    entries = _catalog.ListBySource(ScenarioCatalogSource.Modded);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringEntryFlow] Could not list custom scenarios for baseline picker: " + ex.Message);
            }

            int added = 0;
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                ScenarioCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.ScenarioId))
                    continue;

                ScenarioDefinition editableDefinition;
                bool hasEditableDefinition = TryLoadEditableDefinition(entry.ScenarioId, out editableDefinition);
                bool enabled = entry.CanStart && hasEditableDefinition;
                string disabledReason = null;
                if (!entry.CanStart)
                    disabledReason = "Dependencies are missing or mismatched.";
                else if (!hasEditableDefinition)
                    disabledReason = "No editable scenario definition is available to copy.";

                string summary = editableDefinition != null && !string.IsNullOrEmpty(editableDefinition.Description)
                    ? editableDefinition.Description
                    : entry.Description;
                string author = editableDefinition != null && !string.IsNullOrEmpty(editableDefinition.Author)
                    ? editableDefinition.Author
                    : entry.OwnerModId;
                cards.Add(new ScenarioAuthoringEntryBaselineCard
                {
                    Title = Safe(entry.DisplayName, entry.ScenarioId),
                    Badge = "COPY",
                    Detail = Safe(summary, "No scenario summary provided."),
                    Meta = "Author: " + Safe(author, "unknown"),
                    ActionId = ActionScenarioPrefix + entry.ScenarioId,
                    Enabled = enabled,
                    DisabledReason = disabledReason
                });
                added++;
            }

            if (added == 0)
            {
                cards.Add(new ScenarioAuthoringEntryBaselineCard
                {
                    Title = "No installed custom scenarios",
                    Badge = "COPY",
                    Detail = "Published or exported scenarios will appear here when the scenario book can see them.",
                    Meta = "Create one from the editor or install a scenario pack.",
                    Enabled = false,
                    DisabledReason = "No custom scenario baselines are installed."
                });
            }
        }

        private bool TryLoadEditableDefinition(string scenarioId, out ScenarioDefinition definition)
        {
            definition = null;
            try
            {
                string path;
                ScenarioValidationResult validation;
                return _definitionCatalog != null
                    && _definitionCatalog.TryLoadDefinition(scenarioId, out definition, out path, out validation)
                    && definition != null;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyBaseMode(ScenarioBaseGameMode mode)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                SetPickerStatus("Status: baseline blocked - no active draft is available.");
                return;
            }

            if (definition.BaseGameMode == mode)
            {
                Hide("Baseline selected.");
                return;
            }

            string message = null;
            if (_baseModeReloadService == null
                || !_baseModeReloadService.SaveAndReload(session, mode, ScenarioBaseFamilyChoices.KeepCurrentCast, true, out message))
            {
                SetPickerStatus("Status: baseline blocked - " + Safe(message, "scenario world could not be loaded."));
                return;
            }

            if (!IsReloadQueuedMessage(message))
            {
                SetPickerStatus("Status: baseline blocked - " + Safe(message, "scenario world could not be loaded."));
                return;
            }

            SetSnapshot(CreateLoadingSnapshot(
                ScenarioAuthoringBootstrapService.Instance.CurrentOrPendingSessionForEntryFlow(),
                Safe(message, "Scenario draft saved. Reloading selected baseline."),
                false));
        }

        private void ApplyCustomScenario(string scenarioId)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            if (session == null || session.WorkingDefinition == null)
            {
                SetPickerStatus("Status: baseline blocked - no active draft is available.");
                return;
            }

            ScenarioDefinition source;
            string path;
            ScenarioValidationResult validation;
            if (_definitionCatalog == null
                || !_definitionCatalog.TryLoadDefinition(scenarioId, out source, out path, out validation)
                || source == null)
            {
                SetPickerStatus("Status: baseline blocked - custom scenario could not be loaded.");
                return;
            }

            ScenarioDefinition copy = ScenarioDefinitionCloner.Clone(source);
            if (copy == null)
            {
                SetPickerStatus("Status: baseline blocked - custom scenario copy failed.");
                return;
            }

            string draftId = !string.IsNullOrEmpty(_activeDraftId) ? _activeDraftId : session.WorkingDefinition.Id;
            copy.Id = draftId;
            copy.DisplayName = Safe(source.DisplayName, scenarioId) + " Copy";
            copy.Description = "Draft copied from " + Safe(source.DisplayName, scenarioId) + ". " + Safe(source.Description, string.Empty);
            if (copy.SelectionRules == null)
                copy.SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(copy.BaseGameMode);

            session.WorkingDefinition = copy;
            session.MarkDraftChanged(ScenarioDirtySection.Meta, ScenarioEditCategory.Family);
            session.MarkDraftChanged(ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            session.MarkDraftChanged(ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
            session.MarkDraftChanged(ScenarioDirtySection.Bunker, ScenarioEditCategory.Bunker);
            session.MarkDraftChanged(ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            session.MarkDraftChanged(ScenarioDirtySection.WinLoss, ScenarioEditCategory.WinLoss);
            session.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);

            string message = null;
            if (_baseModeReloadService == null
                || !_baseModeReloadService.SaveAndReloadBaseline(session, copy.BaseGameMode, "from a copy of " + Safe(source.DisplayName, scenarioId), out message))
            {
                SetPickerStatus("Status: baseline blocked - " + Safe(message, "custom scenario reload failed."));
                return;
            }

            if (!IsReloadQueuedMessage(message))
            {
                SetPickerStatus("Status: baseline blocked - " + Safe(message, "custom scenario reload failed."));
                return;
            }

            SetSnapshot(CreateLoadingSnapshot(
                ScenarioAuthoringBootstrapService.Instance.CurrentOrPendingSessionForEntryFlow(),
                Safe(message, "Scenario copied. Reloading the copied baseline."),
                false));
        }

        private void CancelToMainMenu()
        {
            string message;
            if (ScenarioAuthoringBootstrapService.Instance.RequestCloseActiveSessionToMainMenu("Canceled baseline picker.", out message))
            {
                SetSnapshot(CreateLoadingSnapshot(
                    ScenarioAuthoringBootstrapService.Instance.CurrentOrPendingSessionForEntryFlow(),
                    Safe(message, "Status: returning to the main menu."),
                    false));
            }
        }

        private void SetPickerStatus(string status)
        {
            lock (_sync)
            {
                if (_snapshot != null)
                    _snapshot.Status = status;
            }
        }

        private void SetSnapshot(ScenarioAuthoringEntryFlowSnapshot snapshot)
        {
            lock (_sync)
            {
                _snapshot = snapshot ?? new ScenarioAuthoringEntryFlowSnapshot();
            }

            EnsureRuntime();
        }

        private void EnsureRuntime()
        {
            if (_runtime != null)
                return;

            GameObject runtimeObject = GameObject.Find(RuntimeObjectName);
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject(RuntimeObjectName);
                UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
            }

            _runtime = runtimeObject.GetComponent<ScenarioAuthoringEntryFlowRuntime>();
            if (_runtime == null)
                _runtime = runtimeObject.AddComponent<ScenarioAuthoringEntryFlowRuntime>();
            _runtime.Initialize(this, _settingsService);
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }

        private static bool IsReloadQueuedMessage(string message)
        {
            return !string.IsNullOrEmpty(message)
                && message.IndexOf("Reloading", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class ScenarioAuthoringEntryFlowRuntime : MonoBehaviour
        {
            private const int OverlayCanvasOrder = 32000;
            private ScenarioAuthoringEntryFlowService _owner;
            private ScenarioAuthoringSettingsService _settingsService;
            private ScenarioUiContext _uiContext;
            private GUIStyle _titleStyle;
            private GUIStyle _flavorStyle;
            private GUIStyle _statusStyle;
            private GUIStyle _cardButtonStyle;
            private GUIStyle _cardTitleStyle;
            private GUIStyle _cardTextStyle;
            private GUIStyle _cardMetaStyle;
            private GUIStyle _disabledTextStyle;
            private float _styleOpacity = -1f;
            private Canvas _loadingCanvas;
            private Text _loadingTitle;
            private Text _loadingFlavor;
            private Text _loadingStatus;
            private Text _loadingFooter;

            public void Initialize(ScenarioAuthoringEntryFlowService owner, ScenarioAuthoringSettingsService settingsService)
            {
                _owner = owner;
                _settingsService = settingsService;
                enabled = true;
            }

            private void Update()
            {
                ScenarioAuthoringEntryFlowSnapshot snapshot = _owner != null ? _owner.GetSnapshot() : null;
                if (snapshot != null && snapshot.Visible && snapshot.Kind == ScenarioAuthoringEntryFlowKind.Loading)
                {
                    EnsureLoadingCanvas();
                    UpdateLoadingCanvas(snapshot);
                }
                else
                {
                    HideLoadingCanvas();
                }
            }

            private void OnGUI()
            {
                ScenarioAuthoringEntryFlowSnapshot snapshot = _owner != null ? _owner.GetSnapshot() : null;
                if (snapshot == null || !snapshot.Visible)
                    return;

                EnsureStyles();
                int oldDepth = GUI.depth;
                GUI.depth = int.MinValue;
                try
                {
                    Draw(snapshot);
                    BlockInput(snapshot);
                }
                finally
                {
                    GUI.depth = oldDepth;
                }
            }

            private void EnsureLoadingCanvas()
            {
                if (_loadingCanvas != null)
                    return;

                GameObject root = new GameObject("ShelteredAPI.ScenarioAuthoring.EntryFlow.LoadingCanvas");
                root.transform.SetParent(transform, false);
                _loadingCanvas = root.AddComponent<Canvas>();
                _loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _loadingCanvas.sortingOrder = OverlayCanvasOrder;
                root.AddComponent<GraphicRaycaster>();

                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                Image backdrop = root.AddComponent<Image>();
                backdrop.color = new Color(0.10f, 0.08f, 0.06f, 1f);

                GameObject panel = new GameObject("Panel");
                panel.transform.SetParent(root.transform, false);
                RectTransform panelRect = panel.AddComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(1040f, 420f);
                panelRect.anchoredPosition = Vector2.zero;
                Image panelImage = panel.AddComponent<Image>();
                panelImage.color = new Color(0.27f, 0.21f, 0.14f, 0.98f);

                _loadingTitle = CreateCanvasText(panel.transform, "Title", 30, TextAnchor.MiddleCenter, new Rect(32f, -56f, 976f, 52f));
                _loadingFlavor = CreateCanvasText(panel.transform, "Flavor", 16, TextAnchor.UpperCenter, new Rect(72f, -124f, 896f, 58f));
                CreateCanvasImage(panel.transform, "StatusBand", new Rect(84f, -214f, 872f, 42f), new Color(0.15f, 0.12f, 0.08f, 0.86f));
                _loadingStatus = CreateCanvasText(panel.transform, "Status", 15, TextAnchor.MiddleLeft, new Rect(104f, -214f, 832f, 42f));
                _loadingFooter = CreateCanvasText(panel.transform, "Footer", 14, TextAnchor.MiddleCenter, new Rect(72f, -292f, 896f, 36f));
            }

            private static Image CreateCanvasImage(Transform parent, string name, Rect rect, Color color)
            {
                GameObject obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                RectTransform rectTransform = obj.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(rect.width, rect.height);
                rectTransform.anchoredPosition = new Vector2(rect.x + (rect.width * 0.5f) - 520f, rect.y + (rect.height * 0.5f) + 210f);
                Image statusBand = obj.AddComponent<Image>();
                statusBand.color = color;
                return statusBand;
            }

            private static Text CreateCanvasText(Transform parent, string name, int fontSize, TextAnchor alignment, Rect rect)
            {
                GameObject obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                RectTransform rectTransform = obj.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(rect.width, rect.height);
                rectTransform.anchoredPosition = new Vector2(rect.x + (rect.width * 0.5f) - 520f, rect.y + (rect.height * 0.5f) + 210f);

                Text text = obj.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = fontSize;
                text.alignment = alignment;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.color = new Color(0.94f, 0.88f, 0.76f, 1f);
                return text;
            }

            private void UpdateLoadingCanvas(ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                if (_loadingCanvas == null)
                    return;

                _loadingCanvas.enabled = true;
                if (_loadingTitle != null)
                    _loadingTitle.text = snapshot.Title ?? string.Empty;
                if (_loadingFlavor != null)
                    _loadingFlavor.text = snapshot.Flavor ?? string.Empty;
                if (_loadingStatus != null)
                    _loadingStatus.text = snapshot.Status ?? string.Empty;
                if (_loadingFooter != null)
                    _loadingFooter.text = "The world continues to load normally in the background.";
            }

            private void HideLoadingCanvas()
            {
                if (_loadingCanvas != null)
                    _loadingCanvas.enabled = false;
            }

            private void Draw(ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
                Color oldColor = GUI.color;
                GUI.color = new Color(0.10f, 0.08f, 0.06f, 1f);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
                GUI.color = oldColor;

                float width = Mathf.Clamp(Screen.width - 180f, 720f, 1120f);
                Rect panel = new Rect((Screen.width - width) * 0.5f, 64f, width, Screen.height - 128f);
                ScenarioUiWindowRegions regions = _uiContext.Frame.Build(panel, string.Empty, string.Empty, false, 0f, 0f);

                Rect inner = new Rect(regions.Body.x + 12f, regions.Body.y + 12f, regions.Body.width - 24f, regions.Body.height - 24f);
                GUI.Label(new Rect(inner.x, inner.y, inner.width, 48f), snapshot.Title ?? string.Empty, _titleStyle);
                GUI.Label(new Rect(inner.x, inner.y + 52f, inner.width, 42f), snapshot.Flavor ?? string.Empty, _flavorStyle);
                GUI.Box(new Rect(inner.x, inner.y + 100f, inner.width, 36f), snapshot.Status ?? string.Empty, _statusStyle);

                if (snapshot.Kind == ScenarioAuthoringEntryFlowKind.Loading)
                {
                    GUI.Label(new Rect(inner.x, inner.y + 158f, inner.width, 42f), "The world continues to load normally in the background.", _flavorStyle);
                    return;
                }

                DrawCards(inner, snapshot);
            }

            private void DrawCards(Rect inner, ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                ScenarioAuthoringEntryBaselineCard[] cards = snapshot.Cards ?? new ScenarioAuthoringEntryBaselineCard[0];
                float top = inner.y + 154f;
                float cardWidth = Mathf.Max(220f, (inner.width - 24f) / 3f);
                float cardHeight = 126f;
                for (int i = 0; i < cards.Length; i++)
                {
                    ScenarioAuthoringEntryBaselineCard card = cards[i];
                    int col = i % 3;
                    int row = i / 3;
                    Rect rect = new Rect(inner.x + (col * (cardWidth + 12f)), top + (row * (cardHeight + 12f)), cardWidth, cardHeight);
                    DrawCard(rect, card);
                }

                Rect cancelRect = new Rect(inner.xMax - 144f, inner.yMax - 42f, 144f, 34f);
                if (GUI.Button(cancelRect, "Cancel", _uiContext.Styles.Button))
                    _owner.ExecuteEntryAction(ActionCancel);
            }

            private void DrawCard(Rect rect, ScenarioAuthoringEntryBaselineCard card)
            {
                bool enabled = card != null && card.Enabled;
                GUIContent content = new GUIContent(string.Empty, card != null ? card.Detail ?? string.Empty : string.Empty);
                if (enabled && GUI.Button(rect, content, _cardButtonStyle))
                    _owner.ExecuteEntryAction(card.ActionId);
                else if (!enabled)
                    GUI.Box(rect, content, _uiContext.Styles.PanelInset);

                DrawCardSelectionOverlay(rect, enabled);

                Rect badgeRect = new Rect(rect.x + 10f, rect.y + 10f, 64f, 22f);
                GUI.Box(badgeRect, card != null ? card.Badge ?? string.Empty : string.Empty, enabled ? _uiContext.Styles.PillEmphasized : _uiContext.Styles.Pill);
                GUI.Label(new Rect(rect.x + 84f, rect.y + 8f, rect.width - 96f, 26f), card != null ? card.Title ?? string.Empty : string.Empty, enabled ? _cardTitleStyle : _disabledTextStyle);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 42f, rect.width - 24f, 50f), card != null ? card.Detail ?? string.Empty : string.Empty, enabled ? _cardTextStyle : _disabledTextStyle);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 96f, rect.width - 24f, 22f), card != null && !enabled ? card.DisabledReason ?? string.Empty : (card != null ? card.Meta ?? string.Empty : string.Empty), enabled ? _cardMetaStyle : _disabledTextStyle);
            }

            private void DrawCardSelectionOverlay(Rect rect, bool enabled)
            {
                if (!enabled || Event.current == null)
                    return;

                bool hovered = rect.Contains(Event.current.mousePosition);
                bool pressed = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
                if (!hovered && !pressed)
                    return;

                Color oldColor = GUI.color;
                Rect overlayRect = InsetCardOverlayRect(rect);
                if (hovered)
                {
                    GUI.color = new Color(0.882f, 0.784f, 0.588f, 0.24f);
                    ScenarioUiAtlasSkin.DrawCornerCutTexture(overlayRect, _uiContext.Styles.AccentHoverTexture != null ? _uiContext.Styles.AccentHoverTexture : Texture2D.whiteTexture);
                }

                if (pressed)
                {
                    GUI.color = new Color(0.718f, 0.639f, 0.482f, 0.34f);
                    ScenarioUiAtlasSkin.DrawCornerCutTexture(overlayRect, Texture2D.whiteTexture);
                }

                GUI.color = oldColor;
            }

            private static Rect InsetCardOverlayRect(Rect rect)
            {
                const float inset = 3f;
                if (rect.width <= inset * 2f || rect.height <= inset * 2f)
                    return rect;

                return new Rect(rect.x + inset, rect.y + inset, rect.width - (inset * 2f), rect.height - (inset * 2f));
            }

            private void BlockInput(ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                Event evt = Event.current;
                if (evt == null)
                    return;

                if (snapshot.Kind == ScenarioAuthoringEntryFlowKind.BaselinePicker
                    && evt.type == EventType.KeyDown
                    && evt.keyCode == KeyCode.Escape)
                {
                    _owner.ExecuteEntryAction(ActionCancel);
                    evt.Use();
                    return;
                }

                if (evt.type == EventType.MouseDown
                    || evt.type == EventType.MouseUp
                    || evt.type == EventType.MouseDrag
                    || evt.type == EventType.ScrollWheel
                    || evt.type == EventType.KeyDown
                    || evt.type == EventType.KeyUp)
                {
                    evt.Use();
                }
            }

            private void EnsureStyles()
            {
                ScenarioAuthoringSettingsSnapshot settings = null;
                try { settings = _settingsService != null ? _settingsService.Load() : null; }
                catch { }

                float opacity = ScenarioUiTheme.ResolvePanelOpacity(settings);
                if (_uiContext != null && Mathf.Abs(_styleOpacity - opacity) <= 0.001f)
                    return;

                if (_uiContext != null)
                    _uiContext.Dispose();

                _uiContext = ScenarioUiKit.Build(settings);
                _styleOpacity = opacity;
                _titleStyle = new GUIStyle(_uiContext.Styles.BrandTitleText);
                _titleStyle.fontSize = 30;
                _flavorStyle = new GUIStyle(_uiContext.Styles.BodyText);
                _flavorStyle.fontSize = 14;
                _statusStyle = new GUIStyle(_uiContext.Styles.Status);
                _statusStyle.alignment = TextAnchor.MiddleLeft;
                _cardButtonStyle = new GUIStyle(_uiContext.Styles.Card);
                _cardButtonStyle.hover.background = _uiContext.Styles.Card.normal.background;
                _cardButtonStyle.active.background = _uiContext.Styles.Card.normal.background;
                _cardButtonStyle.focused.background = _uiContext.Styles.Card.normal.background;
                _cardTitleStyle = new GUIStyle(_uiContext.Styles.PaperTitleText);
                _cardTitleStyle.fontSize = 15;
                _cardTextStyle = new GUIStyle(_uiContext.Styles.PaperBodyText);
                _cardTextStyle.fontSize = 12;
                _cardMetaStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
                _cardMetaStyle.fontSize = 11;
                _disabledTextStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
                _disabledTextStyle.normal.textColor = new Color(0.28f, 0.23f, 0.18f, 1f);
            }

            private void OnDestroy()
            {
                if (_uiContext != null)
                    _uiContext.Dispose();
                if (_loadingCanvas != null)
                    Destroy(_loadingCanvas.gameObject);
            }
        }
    }

    internal enum ScenarioAuthoringEntryFlowKind
    {
        Loading = 0,
        BaselinePicker = 1
    }

    internal sealed class ScenarioAuthoringEntryFlowSnapshot
    {
        public bool Visible;
        public ScenarioAuthoringEntryFlowKind Kind;
        public string DraftId;
        public string Title;
        public string Flavor;
        public string Status;
        public bool PickerPending;
        public ScenarioAuthoringEntryBaselineCard[] Cards;

        public ScenarioAuthoringEntryFlowSnapshot Copy()
        {
            ScenarioAuthoringEntryFlowSnapshot copy = new ScenarioAuthoringEntryFlowSnapshot
            {
                Visible = Visible,
                Kind = Kind,
                DraftId = DraftId,
                Title = Title,
                Flavor = Flavor,
                Status = Status,
                PickerPending = PickerPending
            };

            if (Cards != null)
            {
                copy.Cards = new ScenarioAuthoringEntryBaselineCard[Cards.Length];
                for (int i = 0; i < Cards.Length; i++)
                    copy.Cards[i] = Cards[i] != null ? Cards[i].Copy() : null;
            }

            return copy;
        }
    }

    internal sealed class ScenarioAuthoringEntryBaselineCard
    {
        public string Title;
        public string Badge;
        public string Detail;
        public string Meta;
        public string ActionId;
        public bool Enabled;
        public string DisabledReason;

        public ScenarioAuthoringEntryBaselineCard Copy()
        {
            return new ScenarioAuthoringEntryBaselineCard
            {
                Title = Title,
                Badge = Badge,
                Detail = Detail,
                Meta = Meta,
                ActionId = ActionId,
                Enabled = Enabled,
                DisabledReason = DisabledReason
            };
        }
    }
}
