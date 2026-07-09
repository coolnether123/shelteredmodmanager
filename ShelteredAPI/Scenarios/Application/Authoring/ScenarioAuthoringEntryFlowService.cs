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
    /// <summary>
    /// Full-screen setup surface shown over the loading world when a scenario
    /// authoring draft launches.
    ///
    /// For a new interactive draft it is a live wizard: the world (Standard by
    /// default) loads behind it while the author names the scenario, picks a
    /// base (Blank/Standard/Stasis/Surrounded/installed copy), and flips a few
    /// quick settings. Picking a non-current base while loading retargets the
    /// world through the existing base-mode reload machinery. When the world +
    /// editor session for the selected base are actually ready, a prominent
    /// OPEN EDITOR button appears at the bottom and pulses until clicked;
    /// clicking commits name/settings into the draft and drops into the shell.
    ///
    /// For reloads, reopening an existing draft, and the non-interactive harness
    /// create path it is a plain loading overlay that auto-hides when ready.
    /// Every launch/base-switch/reload still flows through the single bootstrap
    /// seam; this service never forks the runtime path.
    /// </summary>
    internal sealed class ScenarioAuthoringEntryFlowService
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioAuthoring.EntryFlow";
        private const string DefaultScenarioName = "Untitled Scenario";

        internal const string BaseKeyBlank = "blank";
        internal const string BaseKeyStandard = "standard";
        internal const string BaseKeyStasis = "stasis";
        internal const string BaseKeySurrounded = "surrounded";
        internal const string BaseKeyCustomPrefix = "custom:";

        internal const string SettingSupplies = "supplies";
        internal const string SettingSuppressRaids = "raids";
        internal const string SettingSuppressVisitors = "visitors";

        private readonly object _sync = new object();
        private readonly IScenarioEditorService _editorService;
        private readonly IScenarioSelectionCatalogService _catalog;
        private readonly IScenarioDefinitionCatalogService _definitionCatalog;
        private readonly ScenarioAuthoringBaseModeReloadService _baseModeReloadService;
        private readonly ScenarioAuthoringSettingsService _settingsService;

        private ScenarioAuthoringEntryFlowRuntime _runtime;
        private ScenarioAuthoringEntryFlowSnapshot _snapshot = new ScenarioAuthoringEntryFlowSnapshot();

        // Wizard intent (persists across base-retarget reloads so the author's
        // name / settings / selection survive the world rebuilding).
        private bool _wizardActive;
        private bool _worldReady;
        private string _activeDraftId;
        private string _wizardName = DefaultScenarioName;
        private bool _wizardNameEdited;
        private string _selectedBaseKey = BaseKeyStandard;
        private int _selectionToken;
        private int _dispatchedToken;
        private readonly Dictionary<string, bool> _settings = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly HashSet<string> _settingsTouched = new HashSet<string>(StringComparer.Ordinal);

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
            _settings[SettingSupplies] = false;
            _settings[SettingSuppressRaids] = false;
            _settings[SettingSuppressVisitors] = false;
        }

        public void BeginNewDraftLaunch(ScenarioAuthoringSession session, bool interactiveWizard)
        {
            _activeDraftId = session != null ? session.DraftId : null;
            if (interactiveWizard)
            {
                ResetWizardState();
                _wizardActive = true;
                _worldReady = false;
                PublishSnapshot();
                EnsureRuntime();
                return;
            }

            _wizardActive = false;
            SetSnapshot(CreateLoadingSnapshot(
                session,
                "Status: game loading - creating the authoring world."));
        }

        public void BeginExistingDraftLaunch(ScenarioAuthoringSession session)
        {
            string draftId = session != null ? session.DraftId : null;

            // Selecting another base in the setup wizard saves the current
            // draft, then deliberately re-enters the normal existing-draft
            // reload path.  That path comes through here before BeginReload.
            // Do not accidentally demote the wizard to the plain loading
            // overlay: the author still needs its name, base, and quick
            // settings controls while that replacement world loads.
            if (_wizardActive
                && !string.IsNullOrEmpty(draftId)
                && string.Equals(_activeDraftId, draftId, StringComparison.OrdinalIgnoreCase))
            {
                _activeDraftId = draftId;
                _worldReady = false;
                SetWizardStatus("Status: loading the selected scenario base.");
                MMLog.WriteInfo("[ScenarioAuthoringEntryFlow] Preserved the setup wizard while reloading draft '"
                    + draftId + "' from its selected base.");
                return;
            }

            _wizardActive = false;
            _activeDraftId = draftId;
            SetSnapshot(CreateLoadingSnapshot(
                session,
                "Status: game loading - reopening the existing draft."));
        }

        public void BeginReload(ScenarioAuthoringSession session, string reason)
        {
            _activeDraftId = session != null ? session.DraftId : _activeDraftId;
            string status = string.IsNullOrEmpty(reason)
                ? "Status: game loading - reloading the authoring world."
                : "Status: " + reason;

            if (_wizardActive)
            {
                // A base retarget is rebuilding the world. Keep the wizard up
                // (name / settings / selection preserved) but drop readiness so
                // the OPEN EDITOR button cannot flash until THAT base is ready.
                _worldReady = false;
                SetWizardStatus(status);
                return;
            }

            SetSnapshot(CreateLoadingSnapshot(session, status));
        }

        public void SetLoadingStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
                return;

            if (_wizardActive)
            {
                SetWizardStatus(status);
                return;
            }

            lock (_sync)
            {
                if (_snapshot == null || !_snapshot.Visible || _snapshot.Kind != ScenarioAuthoringEntryFlowKind.Loading)
                    return;

                _snapshot.Status = status;
            }

            EnsureRuntime();
        }

        /// <summary>
        /// Bootstrap readiness callback. <paramref name="worldFullyReady"/> is
        /// false for the early world-loading shell (world still loading behind
        /// the overlay) and true once the editor session is loaded and the
        /// shelter has settled for the currently targeted base.
        /// </summary>
        public void MarkEditorReady(ScenarioAuthoringSession session, bool worldFullyReady)
        {
            _activeDraftId = session != null ? session.DraftId : _activeDraftId;

            if (!_wizardActive)
            {
                // Existing draft / reload / harness: reveal the shell as soon as
                // it is navigable, exactly as before.
                Hide("Editor ready.");
                return;
            }

            if (!worldFullyReady)
            {
                // World-loading shell opened behind the wizard; keep waiting.
                SetWizardStatus("Status: building the shelter world - hold tight.");
                return;
            }

            _worldReady = true;
            SyncWizardTogglesFromDefinition(_editorService != null ? _editorService.CurrentSession : null);
            PublishSnapshot();
        }

        public void Hide(string reason)
        {
            _wizardActive = false;
            _worldReady = false;
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

        /// <summary>
        /// Frame poll (driven by the runtime). Applies a base selection that
        /// could not be dispatched at click time because the editor session was
        /// not yet available, so "pick early while loading" reliably retargets.
        /// </summary>
        public void TickWizard()
        {
            if (!_wizardActive || _selectionToken == _dispatchedToken)
                return;

            ScenarioEditorSession editorSession = _editorService != null ? _editorService.CurrentSession : null;
            if (editorSession == null || editorSession.WorkingDefinition == null)
                return;

            DispatchSelectedBase(editorSession);
        }

        public void SetWizardName(string name)
        {
            if (!_wizardActive)
                return;

            _wizardName = name ?? string.Empty;
            _wizardNameEdited = true;
        }

        public void SelectWizardBase(string baseKey)
        {
            if (!_wizardActive || string.IsNullOrEmpty(baseKey))
                return;

            if (string.Equals(_selectedBaseKey, baseKey, StringComparison.Ordinal)
                && _selectionToken == _dispatchedToken)
                return;

            _selectedBaseKey = baseKey;
            _selectionToken++;
            // Do not clear _worldReady here: a no-op pick (Blank, or a base that
            // is already loaded) triggers no reload, so nothing would restore it.
            // A real base change clears readiness via BeginReload when the reload
            // is queued; a no-op pick keeps the world ready and simply re-selects.

            ScenarioEditorSession editorSession = _editorService != null ? _editorService.CurrentSession : null;
            if (editorSession != null && editorSession.WorkingDefinition != null)
                DispatchSelectedBase(editorSession);
            else
                SetWizardStatus("Status: '" + DescribeBase(baseKey) + "' will load once the world is ready.");
        }

        public void ToggleWizardSetting(string settingKey)
        {
            if (!_wizardActive || string.IsNullOrEmpty(settingKey))
                return;

            bool current;
            _settings.TryGetValue(settingKey, out current);
            _settings[settingKey] = !current;
            _settingsTouched.Add(settingKey);
            PublishSnapshot();
        }

        public void OpenWizardEditor()
        {
            if (!_wizardActive || !IsReadyToOpen())
                return;

            CommitWizardIntent();
            _wizardActive = false;
            _worldReady = false;
            Hide("Opening the scenario editor.");
        }

        public void CancelWizard()
        {
            ScenarioAuthoringBootstrapService bootstrap = ScenarioAuthoringBootstrapService.Instance;
            string draftId = _activeDraftId;
            _wizardActive = false;
            _worldReady = false;

            string message;
            if (bootstrap.CancelUncommittedWizardDraft(draftId, "Canceled the scenario setup wizard.", out message))
                return;

            if (bootstrap.HasPendingDraftLaunch() && !bootstrap.IsEditingDraftActive())
            {
                bootstrap.CancelPendingDraft("Canceled the scenario setup wizard.");
                return;
            }

            if (bootstrap.RequestCloseActiveSessionToMainMenu("Canceled the scenario setup wizard.", out message))
            {
                SetSnapshot(CreateLoadingSnapshot(
                    bootstrap.CurrentOrPendingSessionForEntryFlow(),
                    Safe(message, "Status: returning to the main menu.")));
            }
        }

        // ---- internals -----------------------------------------------------

        private void ResetWizardState()
        {
            _wizardName = DefaultScenarioName;
            _wizardNameEdited = false;
            _selectedBaseKey = BaseKeyStandard;
            _selectionToken = 0;
            _dispatchedToken = 0;
            _settingsTouched.Clear();
            _settings[SettingSupplies] = false;
            _settings[SettingSuppressRaids] = false;
            _settings[SettingSuppressVisitors] = false;
        }

        private bool IsReadyToOpen()
        {
            return _worldReady && _selectionToken == _dispatchedToken;
        }

        private void DispatchSelectedBase(ScenarioEditorSession editorSession)
        {
            ScenarioDefinition definition = editorSession.WorkingDefinition;
            string key = _selectedBaseKey;

            // Blank keeps the world exactly as first loaded; a base whose mode
            // already matches the loaded draft needs no reload either.
            if (string.Equals(key, BaseKeyBlank, StringComparison.Ordinal))
            {
                _dispatchedToken = _selectionToken;
                SetWizardStatus("Status: blank shelter selected.");
                return;
            }

            if (key.StartsWith(BaseKeyCustomPrefix, StringComparison.Ordinal))
            {
                _dispatchedToken = _selectionToken;
                ApplyCustomScenario(editorSession, key.Substring(BaseKeyCustomPrefix.Length));
                return;
            }

            ScenarioBaseGameMode mode = ResolveBaseMode(key);
            if (definition.BaseGameMode == mode)
            {
                _dispatchedToken = _selectionToken;
                SetWizardStatus("Status: " + DescribeBase(key) + " base selected.");
                return;
            }

            _dispatchedToken = _selectionToken;
            ApplyBaseMode(editorSession, mode);
        }

        private void ApplyBaseMode(ScenarioEditorSession editorSession, ScenarioBaseGameMode mode)
        {
            string message = null;
            if (_baseModeReloadService == null
                || !_baseModeReloadService.SaveAndReload(editorSession, mode, ScenarioBaseFamilyChoices.KeepCurrentCast, true, out message)
                || !IsReloadQueuedMessage(message))
            {
                // Mark the selection handled so TickWizard does not re-dispatch a
                // failing reload every frame; the author can re-click to retry.
                _dispatchedToken = _selectionToken;
                SetWizardStatus("Status: base blocked - " + Safe(message, "scenario world could not be loaded."));
                return;
            }

            SetWizardStatus(Safe(message, "Loading the selected base..."));
        }

        private void ApplyCustomScenario(ScenarioEditorSession editorSession, string scenarioId)
        {
            ScenarioDefinition source;
            string path;
            ScenarioValidationResult validation;
            if (_definitionCatalog == null
                || !_definitionCatalog.TryLoadDefinition(scenarioId, out source, out path, out validation)
                || source == null)
            {
                _dispatchedToken = _selectionToken;
                SetWizardStatus("Status: base blocked - custom scenario could not be loaded.");
                return;
            }

            ScenarioDefinition copy = ScenarioDefinitionCloner.Clone(source);
            if (copy == null)
            {
                _dispatchedToken = _selectionToken;
                SetWizardStatus("Status: base blocked - custom scenario copy failed.");
                return;
            }

            string draftId = !string.IsNullOrEmpty(_activeDraftId) ? _activeDraftId : editorSession.WorkingDefinition.Id;
            copy.Id = draftId;
            copy.DisplayName = Safe(source.DisplayName, scenarioId) + " Copy";
            copy.Description = "Draft copied from " + Safe(source.DisplayName, scenarioId) + ". " + Safe(source.Description, string.Empty);
            if (copy.SelectionRules == null)
                copy.SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(copy.BaseGameMode);

            editorSession.WorkingDefinition = copy;
            editorSession.MarkDraftChanged(ScenarioDirtySection.Meta, ScenarioEditCategory.Family);
            editorSession.MarkDraftChanged(ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            editorSession.MarkDraftChanged(ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
            editorSession.MarkDraftChanged(ScenarioDirtySection.Bunker, ScenarioEditCategory.Bunker);
            editorSession.MarkDraftChanged(ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            editorSession.MarkDraftChanged(ScenarioDirtySection.WinLoss, ScenarioEditCategory.WinLoss);
            editorSession.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);

            string message = null;
            if (_baseModeReloadService == null
                || !_baseModeReloadService.SaveAndReloadBaseline(editorSession, copy.BaseGameMode, "from a copy of " + Safe(source.DisplayName, scenarioId), out message)
                || !IsReloadQueuedMessage(message))
            {
                _dispatchedToken = _selectionToken;
                SetWizardStatus("Status: base blocked - " + Safe(message, "custom scenario reload failed."));
                return;
            }

            SetWizardStatus(Safe(message, "Copying the custom scenario base..."));
        }

        private void CommitWizardIntent()
        {
            ScenarioEditorSession editorSession = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            if (editorSession == null || definition == null)
                return;

            bool changed = false;

            if (_wizardNameEdited && !string.IsNullOrEmpty(_wizardName) && _wizardName.Trim().Length > 0)
            {
                definition.DisplayName = _wizardName.Trim();
                editorSession.MarkDraftChanged(ScenarioDirtySection.Meta, ScenarioEditCategory.Family);
                changed = true;
            }

            if (_settingsTouched.Contains(SettingSupplies))
            {
                if (definition.StartingInventory == null)
                    definition.StartingInventory = new StartingInventoryDefinition();
                definition.StartingInventory.OverrideRandomStart = GetSetting(SettingSupplies);
                editorSession.MarkDraftChanged(ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
                changed = true;
            }

            if (_settingsTouched.Contains(SettingSuppressRaids) || _settingsTouched.Contains(SettingSuppressVisitors))
            {
                if (definition.VanillaSuppression == null)
                    definition.VanillaSuppression = new ScenarioVanillaSuppressionDefinition();
                if (_settingsTouched.Contains(SettingSuppressRaids))
                    definition.VanillaSuppression.Raids = GetSetting(SettingSuppressRaids);
                if (_settingsTouched.Contains(SettingSuppressVisitors))
                    definition.VanillaSuppression.RandomVisitors = GetSetting(SettingSuppressVisitors);
                editorSession.MarkDraftChanged(ScenarioDirtySection.Meta, ScenarioEditCategory.Family);
                changed = true;
            }

            if (!changed)
                return;

            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(null);
                if (validation != null && !validation.IsValid)
                    MMLog.WriteWarning("[ScenarioAuthoringEntryFlow] Committed wizard setup with validation warnings for draft '"
                        + Safe(_activeDraftId, definition.Id) + "'.");
                else
                    MMLog.WriteInfo("[ScenarioAuthoringEntryFlow] Committed wizard setup for draft '"
                        + Safe(_activeDraftId, definition.Id) + "'.");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringEntryFlow] Failed to commit wizard setup: " + ex.Message);
            }
        }

        private void SyncWizardTogglesFromDefinition(ScenarioEditorSession editorSession)
        {
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            if (definition == null)
                return;

            if (!_settingsTouched.Contains(SettingSupplies))
                _settings[SettingSupplies] = definition.StartingInventory != null && definition.StartingInventory.OverrideRandomStart;
            if (!_settingsTouched.Contains(SettingSuppressRaids))
                _settings[SettingSuppressRaids] = definition.VanillaSuppression != null && definition.VanillaSuppression.Raids;
            if (!_settingsTouched.Contains(SettingSuppressVisitors))
                _settings[SettingSuppressVisitors] = definition.VanillaSuppression != null && definition.VanillaSuppression.RandomVisitors;
        }

        private bool GetSetting(string key)
        {
            bool value;
            _settings.TryGetValue(key, out value);
            return value;
        }

        private static ScenarioBaseGameMode ResolveBaseMode(string baseKey)
        {
            switch (baseKey)
            {
                case BaseKeyStasis:
                    return ScenarioBaseGameMode.Stasis;
                case BaseKeySurrounded:
                    return ScenarioBaseGameMode.Surrounded;
                default:
                    return ScenarioBaseGameMode.Survival;
            }
        }

        private static string DescribeBase(string baseKey)
        {
            if (string.IsNullOrEmpty(baseKey))
                return "Standard";
            switch (baseKey)
            {
                case BaseKeyBlank:
                    return "Blank shelter";
                case BaseKeyStandard:
                    return "Standard";
                case BaseKeyStasis:
                    return "Stasis";
                case BaseKeySurrounded:
                    return "Surrounded";
                default:
                    return "Custom copy";
            }
        }

        private ScenarioAuthoringEntryFlowSnapshot CreateLoadingSnapshot(ScenarioAuthoringSession session, string status)
        {
            return new ScenarioAuthoringEntryFlowSnapshot
            {
                Visible = true,
                Kind = ScenarioAuthoringEntryFlowKind.Loading,
                DraftId = session != null ? session.DraftId : _activeDraftId,
                Title = "Custom Scenario Editor",
                Flavor = "The shelter is loading behind this screen. The editor will attach when Sheltered finishes building the world.",
                Status = status
            };
        }

        private void SetWizardStatus(string status)
        {
            if (!string.IsNullOrEmpty(status))
                PublishSnapshot(status);
            else
                PublishSnapshot();
        }

        private void PublishSnapshot()
        {
            PublishSnapshot(null);
        }

        private void PublishSnapshot(string statusOverride)
        {
            if (!_wizardActive)
                return;

            string status;
            if (!string.IsNullOrEmpty(statusOverride))
            {
                status = statusOverride;
            }
            else
            {
                lock (_sync)
                {
                    status = _snapshot != null && !string.IsNullOrEmpty(_snapshot.Status)
                        ? _snapshot.Status
                        : "Status: preparing the authoring world.";
                }
            }

            ScenarioAuthoringEntryFlowSnapshot snapshot = new ScenarioAuthoringEntryFlowSnapshot
            {
                Visible = true,
                Kind = ScenarioAuthoringEntryFlowKind.Wizard,
                DraftId = _activeDraftId,
                Title = "Set Up Your Scenario",
                Flavor = "Name it, choose a base, and pick a few starting rules. The world loads behind this panel; the base you choose loads it in.",
                Status = status,
                Ready = IsReadyToOpen(),
                Name = _wizardName,
                SelectedBaseKey = _selectedBaseKey,
                Cards = BuildBaselineCards(_selectedBaseKey),
                Settings = BuildSettingToggles()
            };

            SetSnapshot(snapshot);
        }

        private ScenarioAuthoringEntrySettingToggle[] BuildSettingToggles()
        {
            return new[]
            {
                new ScenarioAuthoringEntrySettingToggle
                {
                    Key = SettingSupplies,
                    Label = "Override random starting supplies",
                    Detail = "Start from your authored inventory instead of vanilla random supplies.",
                    On = GetSetting(SettingSupplies)
                },
                new ScenarioAuthoringEntrySettingToggle
                {
                    Key = SettingSuppressRaids,
                    Label = "Suppress raids",
                    Detail = "Stop vanilla raid events from spawning in this scenario.",
                    On = GetSetting(SettingSuppressRaids)
                },
                new ScenarioAuthoringEntrySettingToggle
                {
                    Key = SettingSuppressVisitors,
                    Label = "Suppress random visitors",
                    Detail = "Stop vanilla wandering visitors from arriving.",
                    On = GetSetting(SettingSuppressVisitors)
                }
            };
        }

        private ScenarioAuthoringEntryBaselineCard[] BuildBaselineCards(string selectedKey)
        {
            List<ScenarioAuthoringEntryBaselineCard> cards = new List<ScenarioAuthoringEntryBaselineCard>();
            AddBaseCard(cards, BaseKeyBlank, "Blank shelter", "BLANK", "Start empty and build everything yourself.", selectedKey);
            AddBaseCard(cards, BaseKeyStandard, "Standard", "BASE", "The classic survival start.", selectedKey);
            AddBaseCard(cards, BaseKeyStasis, "Stasis", "BASE", "The Stasis scenario world.", selectedKey);
            AddBaseCard(cards, BaseKeySurrounded, "Surrounded", "BASE", "The Surrounded scenario world.", selectedKey);
            AddCustomScenarioCards(cards, selectedKey);
            return cards.ToArray();
        }

        private static void AddBaseCard(List<ScenarioAuthoringEntryBaselineCard> cards, string key, string title, string badge, string detail, string selectedKey)
        {
            cards.Add(new ScenarioAuthoringEntryBaselineCard
            {
                Key = key,
                Title = title,
                Badge = badge,
                Detail = detail,
                Meta = string.Empty,
                Enabled = true,
                Selected = string.Equals(key, selectedKey, StringComparison.Ordinal)
            });
        }

        private void AddCustomScenarioCards(List<ScenarioAuthoringEntryBaselineCard> cards, string selectedKey)
        {
            ScenarioCatalogEntry[] entries = new ScenarioCatalogEntry[0];
            try
            {
                if (_catalog != null)
                    entries = _catalog.ListBySource(ScenarioCatalogSource.Modded);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringEntryFlow] Could not list custom scenarios for the wizard: " + ex.Message);
            }

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
                string key = BaseKeyCustomPrefix + entry.ScenarioId;
                cards.Add(new ScenarioAuthoringEntryBaselineCard
                {
                    Key = key,
                    Title = Safe(entry.DisplayName, entry.ScenarioId),
                    Badge = "COPY",
                    Detail = Safe(summary, "No scenario summary provided."),
                    Meta = "Author: " + Safe(author, "unknown"),
                    Enabled = enabled,
                    Selected = string.Equals(key, selectedKey, StringComparison.Ordinal),
                    DisabledReason = disabledReason
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
            private GUIStyle _sectionStyle;
            private GUIStyle _nameFieldStyle;
            private GUIStyle _openButtonStyle;
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
            private string _nameBuffer;
            private bool _nameInitialized;
            private const string NameControlName = "ScenarioAuthoringWizard.NameField";

            public void Initialize(ScenarioAuthoringEntryFlowService owner, ScenarioAuthoringSettingsService settingsService)
            {
                _owner = owner;
                _settingsService = settingsService;
                enabled = true;
            }

            private void Update()
            {
                ScenarioAuthoringEntryFlowSnapshot snapshot = _owner != null ? _owner.GetSnapshot() : null;
                if (_owner != null)
                    _owner.TickWizard();

                if (snapshot != null && snapshot.Visible && snapshot.Kind == ScenarioAuthoringEntryFlowKind.Loading)
                {
                    EnsureLoadingCanvas();
                    UpdateLoadingCanvas(snapshot);
                }
                else
                {
                    HideLoadingCanvas();
                }

                if (snapshot == null || !snapshot.Visible || snapshot.Kind != ScenarioAuthoringEntryFlowKind.Wizard)
                    _nameInitialized = false;
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
                    if (snapshot.Kind == ScenarioAuthoringEntryFlowKind.Wizard)
                        DrawWizard(snapshot);
                    else
                        DrawLoading(snapshot);
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

            private void DrawLoading(ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                DrawBackdrop();
                Rect inner;
                BuildPanel(out inner);
                GUI.Label(new Rect(inner.x, inner.y, inner.width, 48f), snapshot.Title ?? string.Empty, _titleStyle);
                GUI.Label(new Rect(inner.x, inner.y + 52f, inner.width, 42f), snapshot.Flavor ?? string.Empty, _flavorStyle);
                GUI.Box(new Rect(inner.x, inner.y + 100f, inner.width, 36f), snapshot.Status ?? string.Empty, _statusStyle);
                GUI.Label(new Rect(inner.x, inner.y + 158f, inner.width, 42f), "The world continues to load normally in the background.", _flavorStyle);
            }

            private void DrawWizard(ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                DrawBackdrop();
                Rect inner;
                BuildPanel(out inner);

                float y = inner.y;
                GUI.Label(new Rect(inner.x, y, inner.width, 40f), snapshot.Title ?? string.Empty, _titleStyle);
                y += 44f;
                GUI.Label(new Rect(inner.x, y, inner.width, 40f), snapshot.Flavor ?? string.Empty, _flavorStyle);
                y += 46f;

                // Name field (deferred commit - the buffer is only pushed to the
                // owner; nothing persists until OPEN EDITOR is pressed).
                GUI.Label(new Rect(inner.x, y, 160f, 26f), "Scenario name", _sectionStyle);
                y += 28f;
                if (!_nameInitialized)
                {
                    _nameBuffer = snapshot.Name ?? string.Empty;
                    _nameInitialized = true;
                }
                GUI.SetNextControlName(NameControlName);
                Rect nameRect = new Rect(inner.x, y, Mathf.Min(560f, inner.width), 30f);
                string edited = GUI.TextField(nameRect, _nameBuffer ?? string.Empty, 80, _nameFieldStyle);
                if (!string.Equals(edited, _nameBuffer, StringComparison.Ordinal))
                {
                    _nameBuffer = edited;
                    _owner.SetWizardName(edited);
                }
                y += 44f;

                // Base selection
                GUI.Label(new Rect(inner.x, y, inner.width, 26f), "Scenario base", _sectionStyle);
                y += 30f;
                float cardsBottom = DrawCards(new Rect(inner.x, y, inner.width, 0f), snapshot);
                y = cardsBottom + 10f;

                // Quick settings
                GUI.Label(new Rect(inner.x, y, inner.width, 26f), "Quick settings", _sectionStyle);
                y += 30f;
                y = DrawSettings(new Rect(inner.x, y, inner.width, 0f), snapshot) + 10f;

                // Status band
                float statusY = inner.yMax - 96f;
                GUI.Box(new Rect(inner.x, statusY, inner.width, 32f), snapshot.Status ?? string.Empty, _statusStyle);

                // Open / cancel row
                DrawFooterButtons(inner, snapshot);
            }

            private float DrawCards(Rect area, ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                ScenarioAuthoringEntryBaselineCard[] cards = snapshot.Cards ?? new ScenarioAuthoringEntryBaselineCard[0];
                int columns = 4;
                float gap = 12f;
                float cardWidth = Mathf.Max(200f, (area.width - (gap * (columns - 1))) / columns);
                float cardHeight = 108f;
                float bottom = area.y;
                for (int i = 0; i < cards.Length; i++)
                {
                    int col = i % columns;
                    int row = i / columns;
                    Rect rect = new Rect(area.x + (col * (cardWidth + gap)), area.y + (row * (cardHeight + gap)), cardWidth, cardHeight);
                    DrawCard(rect, cards[i]);
                    bottom = Mathf.Max(bottom, rect.yMax);
                }

                return bottom;
            }

            private void DrawCard(Rect rect, ScenarioAuthoringEntryBaselineCard card)
            {
                bool enabled = card != null && card.Enabled;
                bool selected = card != null && card.Selected;
                GUIContent content = new GUIContent(string.Empty, card != null ? card.Detail ?? string.Empty : string.Empty);
                if (enabled && GUI.Button(rect, content, _cardButtonStyle))
                    _owner.SelectWizardBase(card.Key);
                else if (!enabled)
                    GUI.Box(rect, content, _uiContext.Styles.PanelInset);

                DrawCardStateOverlay(rect, enabled, selected);

                Rect badgeRect = new Rect(rect.x + 10f, rect.y + 10f, 66f, 22f);
                GUI.Box(badgeRect, card != null ? card.Badge ?? string.Empty : string.Empty, (enabled || selected) ? _uiContext.Styles.PillEmphasized : _uiContext.Styles.Pill);
                GUI.Label(new Rect(rect.x + 86f, rect.y + 8f, rect.width - 96f, 26f), card != null ? card.Title ?? string.Empty : string.Empty, enabled ? _cardTitleStyle : _disabledTextStyle);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 40f, rect.width - 24f, 44f), card != null ? card.Detail ?? string.Empty : string.Empty, enabled ? _cardTextStyle : _disabledTextStyle);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 84f, rect.width - 24f, 20f), card != null && !enabled ? card.DisabledReason ?? string.Empty : (card != null ? card.Meta ?? string.Empty : string.Empty), enabled ? _cardMetaStyle : _disabledTextStyle);
            }

            private void DrawCardStateOverlay(Rect rect, bool enabled, bool selected)
            {
                Color oldColor = GUI.color;
                Rect overlayRect = InsetRect(rect, 3f);

                if (selected)
                {
                    GUI.color = new Color(0.792f, 0.678f, 0.404f, 0.34f);
                    ScenarioUiAtlasSkin.DrawCornerCutTexture(overlayRect, _uiContext.Styles.AccentActiveTexture != null ? _uiContext.Styles.AccentActiveTexture : Texture2D.whiteTexture);
                    ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderStrongTexture);
                }

                if (enabled && Event.current != null)
                {
                    bool hovered = rect.Contains(Event.current.mousePosition);
                    if (hovered && !selected)
                    {
                        GUI.color = new Color(0.882f, 0.784f, 0.588f, 0.20f);
                        ScenarioUiAtlasSkin.DrawCornerCutTexture(overlayRect, _uiContext.Styles.AccentHoverTexture != null ? _uiContext.Styles.AccentHoverTexture : Texture2D.whiteTexture);
                    }
                }

                GUI.color = oldColor;
            }

            private float DrawSettings(Rect area, ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                ScenarioAuthoringEntrySettingToggle[] toggles = snapshot.Settings ?? new ScenarioAuthoringEntrySettingToggle[0];
                int columns = 3;
                float gap = 12f;
                float toggleWidth = Mathf.Max(240f, (area.width - (gap * (columns - 1))) / columns);
                float toggleHeight = 64f;
                float bottom = area.y;
                for (int i = 0; i < toggles.Length; i++)
                {
                    ScenarioAuthoringEntrySettingToggle toggle = toggles[i];
                    int col = i % columns;
                    int row = i / columns;
                    Rect rect = new Rect(area.x + (col * (toggleWidth + gap)), area.y + (row * (toggleHeight + gap)), toggleWidth, toggleHeight);
                    GUIContent content = new GUIContent(string.Empty, toggle != null ? toggle.Detail ?? string.Empty : string.Empty);
                    if (GUI.Button(rect, content, toggle != null && toggle.On ? _uiContext.Styles.ButtonActive : _cardButtonStyle))
                        _owner.ToggleWizardSetting(toggle.Key);

                    string mark = toggle != null && toggle.On ? "[ON]" : "[OFF]";
                    GUI.Box(new Rect(rect.x + 10f, rect.y + 10f, 56f, 22f), mark, toggle != null && toggle.On ? _uiContext.Styles.PillSuccess : _uiContext.Styles.Pill);
                    GUI.Label(new Rect(rect.x + 74f, rect.y + 8f, rect.width - 84f, 24f), toggle != null ? toggle.Label ?? string.Empty : string.Empty, _cardTitleStyle);
                    GUI.Label(new Rect(rect.x + 12f, rect.y + 36f, rect.width - 24f, 24f), toggle != null ? toggle.Detail ?? string.Empty : string.Empty, _cardMetaStyle);
                    bottom = Mathf.Max(bottom, rect.yMax);
                }

                return bottom;
            }

            private void DrawFooterButtons(Rect inner, ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                Rect cancelRect = new Rect(inner.x, inner.yMax - 44f, 150f, 36f);
                if (GUI.Button(cancelRect, "Cancel", _uiContext.Styles.Button))
                    _owner.CancelWizard();

                float openWidth = 320f;
                Rect openRect = new Rect(inner.x + (inner.width - openWidth) * 0.5f, inner.yMax - 48f, openWidth, 44f);
                if (snapshot.Ready)
                {
                    DrawFlashingOpenButton(openRect);
                }
                else
                {
                    Color old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.55f);
                    GUI.Box(openRect, "Preparing editor...", _openButtonStyle);
                    GUI.color = old;
                }
            }

            private void DrawFlashingOpenButton(Rect rect)
            {
                // Native value/glow pulse (not a blink): a warm accent glow
                // breathes behind the button until the author clicks it.
                float pulse = 0.5f + (0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4.2f));
                Color old = GUI.color;
                Rect glow = new Rect(rect.x - 6f, rect.y - 6f, rect.width + 12f, rect.height + 12f);
                GUI.color = new Color(0.98f, 0.82f, 0.42f, 0.20f + (0.34f * pulse));
                ScenarioUiAtlasSkin.DrawCornerCutTexture(glow, _uiContext.Styles.AccentActiveTexture != null ? _uiContext.Styles.AccentActiveTexture : Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, 0.85f + (0.15f * pulse));
                bool clicked = GUI.Button(rect, "OPEN SCENARIO EDITOR", _openButtonStyle);
                GUI.color = old;
                if (clicked)
                    _owner.OpenWizardEditor();
            }

            private void DrawBackdrop()
            {
                Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
                Color oldColor = GUI.color;
                GUI.color = new Color(0.10f, 0.08f, 0.06f, 1f);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
                GUI.color = oldColor;
            }

            private void BuildPanel(out Rect inner)
            {
                float width = Mathf.Clamp(Screen.width - 180f, 860f, 1180f);
                Rect panel = new Rect((Screen.width - width) * 0.5f, 56f, width, Screen.height - 112f);
                ScenarioUiWindowRegions regions = _uiContext.Frame.Build(panel, string.Empty, string.Empty, false, 0f, 0f);
                inner = new Rect(regions.Body.x + 16f, regions.Body.y + 14f, regions.Body.width - 32f, regions.Body.height - 28f);
            }

            private static Rect InsetRect(Rect rect, float inset)
            {
                if (rect.width <= inset * 2f || rect.height <= inset * 2f)
                    return rect;
                return new Rect(rect.x + inset, rect.y + inset, rect.width - (inset * 2f), rect.height - (inset * 2f));
            }

            private void BlockInput(ScenarioAuthoringEntryFlowSnapshot snapshot)
            {
                Event evt = Event.current;
                if (evt == null)
                    return;

                if (snapshot.Kind == ScenarioAuthoringEntryFlowKind.Wizard
                    && evt.type == EventType.KeyDown
                    && evt.keyCode == KeyCode.Escape)
                {
                    _owner.CancelWizard();
                    evt.Use();
                    return;
                }

                // Keep keyboard events flowing to the name field; the field and
                // buttons are drawn before this runs, so they have already
                // consumed what they need. We only fully swallow pointer events
                // and stray input so the world/shell behind stays inert.
                if (evt.type == EventType.MouseDown
                    || evt.type == EventType.MouseUp
                    || evt.type == EventType.MouseDrag
                    || evt.type == EventType.ScrollWheel)
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
                _sectionStyle = new GUIStyle(_uiContext.Styles.SectionTitleText);
                _nameFieldStyle = new GUIStyle(_uiContext.Styles.Field);
                _nameFieldStyle.fontSize = 16;
                _openButtonStyle = new GUIStyle(_uiContext.Styles.ButtonActive);
                _openButtonStyle.fontSize = 18;
                _openButtonStyle.fontStyle = FontStyle.Bold;
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
        Wizard = 1
    }

    internal sealed class ScenarioAuthoringEntryFlowSnapshot
    {
        public bool Visible;
        public ScenarioAuthoringEntryFlowKind Kind;
        public string DraftId;
        public string Title;
        public string Flavor;
        public string Status;
        public bool Ready;
        public string Name;
        public string SelectedBaseKey;
        public ScenarioAuthoringEntryBaselineCard[] Cards;
        public ScenarioAuthoringEntrySettingToggle[] Settings;

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
                Ready = Ready,
                Name = Name,
                SelectedBaseKey = SelectedBaseKey
            };

            if (Cards != null)
            {
                copy.Cards = new ScenarioAuthoringEntryBaselineCard[Cards.Length];
                for (int i = 0; i < Cards.Length; i++)
                    copy.Cards[i] = Cards[i] != null ? Cards[i].Copy() : null;
            }

            if (Settings != null)
            {
                copy.Settings = new ScenarioAuthoringEntrySettingToggle[Settings.Length];
                for (int i = 0; i < Settings.Length; i++)
                    copy.Settings[i] = Settings[i] != null ? Settings[i].Copy() : null;
            }

            return copy;
        }
    }

    internal sealed class ScenarioAuthoringEntryBaselineCard
    {
        public string Key;
        public string Title;
        public string Badge;
        public string Detail;
        public string Meta;
        public bool Enabled;
        public bool Selected;
        public string DisabledReason;

        public ScenarioAuthoringEntryBaselineCard Copy()
        {
            return new ScenarioAuthoringEntryBaselineCard
            {
                Key = Key,
                Title = Title,
                Badge = Badge,
                Detail = Detail,
                Meta = Meta,
                Enabled = Enabled,
                Selected = Selected,
                DisabledReason = DisabledReason
            };
        }
    }

    internal sealed class ScenarioAuthoringEntrySettingToggle
    {
        public string Key;
        public string Label;
        public string Detail;
        public bool On;

        public ScenarioAuthoringEntrySettingToggle Copy()
        {
            return new ScenarioAuthoringEntrySettingToggle
            {
                Key = Key,
                Label = Label,
                Detail = Detail,
                On = On
            };
        }
    }
}
