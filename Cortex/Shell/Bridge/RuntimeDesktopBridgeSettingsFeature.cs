using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cortex.Bridge;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Shell.Shared.Models;
using Cortex.Shell.Shared.Services;

namespace Cortex.Shell.Bridge
{
    internal sealed class RuntimeDesktopBridgeSettingsFeature
    {
        private readonly CortexShellState _shellState;
        private readonly Func<ICortexSettingsStore> _settingsStoreAccessor;
        private readonly SettingsDocumentBuilder _settingsDocumentBuilder = new SettingsDocumentBuilder();
        private readonly SettingsSearchService _settingsSearchService = new SettingsSearchService();
        private readonly SettingsDraftService _settingsDraftService = new SettingsDraftService();
        private readonly SettingsApplicationService _settingsApplicationService = new SettingsApplicationService();
        private readonly OnboardingService _onboardingService = new OnboardingService();

        private readonly WorkbenchCatalogSnapshot _catalog;
        private ShellSettings _shellSettings;
        private OnboardingState _onboardingState;
        private SettingsDocumentModel _settingsDocument;
        private SettingsSessionState _settingsSessionState;
        private SettingsDraftState _settingsDraftState;
        private readonly List<SettingsSectionModel> _visibleSettingsSections = new List<SettingsSectionModel>();
        private readonly List<SettingDescriptor> _activeSettings = new List<SettingDescriptor>();
        private string _selectedSectionId = string.Empty;
        private string _selectedSettingId = string.Empty;
        private string _cachedSettingsFingerprint = string.Empty;

        public RuntimeDesktopBridgeSettingsFeature(CortexShellState shellState, Func<ICortexSettingsStore> settingsStoreAccessor)
        {
            _shellState = shellState ?? new CortexShellState();
            _settingsStoreAccessor = settingsStoreAccessor;
            _catalog = WorkbenchCatalogFactory.CreateDefaultCatalog();
            _shellSettings = new ShellSettings();
            _onboardingState = new OnboardingState();
            _settingsDocument = new SettingsDocumentModel();
            _settingsSessionState = new SettingsSessionState();
            _settingsDraftState = new SettingsDraftState();
        }

        public WorkbenchCatalogSnapshot Catalog
        {
            get { return _catalog; }
        }

        public ShellSettings CurrentSettings
        {
            get { return _shellSettings; }
        }

        public void Initialize()
        {
            ApplyRuntimeSettings(true);
            RefreshSettingsProjection();
            CacheRuntimeMirror();
        }

        public bool SynchronizeFromRuntime()
        {
            var currentSettingsFingerprint = BuildSettingsFingerprint(_shellState.Settings);
            if (string.Equals(_cachedSettingsFingerprint, currentSettingsFingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            ApplyRuntimeSettings(true);
            RefreshSettingsProjection();
            CacheRuntimeMirror();
            return true;
        }

        public bool TryApplyIntent(BridgeIntentMessage intent, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (intent == null)
            {
                return false;
            }

            switch (intent.IntentType)
            {
                case BridgeIntentType.SelectOnboardingProfile:
                    _onboardingState.SelectedProfileId = intent.ProfileId ?? string.Empty;
                    statusMessage = "Updated onboarding profile selection.";
                    break;
                case BridgeIntentType.SelectOnboardingLayout:
                    _onboardingState.SelectedLayoutPresetId = intent.LayoutPresetId ?? string.Empty;
                    statusMessage = "Updated onboarding layout selection.";
                    break;
                case BridgeIntentType.SelectOnboardingTheme:
                    _onboardingState.SelectedThemeId = intent.ThemeId ?? string.Empty;
                    statusMessage = "Updated onboarding theme selection.";
                    break;
                case BridgeIntentType.SetOnboardingWorkspaceRoot:
                    _onboardingState.SelectedWorkspaceRootPath = intent.WorkspaceRootPath ?? string.Empty;
                    statusMessage = "Updated onboarding workspace root.";
                    break;
                case BridgeIntentType.ApplyOnboarding:
                    ApplyOnboarding();
                    statusMessage = "Applied onboarding defaults.";
                    break;
                case BridgeIntentType.SetWorkspaceRoot:
                    _shellSettings.WorkspaceRootPath = intent.WorkspaceRootPath ?? string.Empty;
                    _onboardingState.SelectedWorkspaceRootPath = _shellSettings.WorkspaceRootPath;
                    _settingsDraftState.Values[nameof(ShellSettings.WorkspaceRootPath)] = _shellSettings.WorkspaceRootPath;
                    statusMessage = "Updated workspace root.";
                    break;
                case BridgeIntentType.SaveSettings:
                    SaveSettings();
                    statusMessage = "Saved legacy runtime settings.";
                    break;
                case BridgeIntentType.SelectSettingsSection:
                    _settingsSessionState.ActiveSectionId = intent.SectionId ?? string.Empty;
                    RefreshSettingsProjection();
                    statusMessage = "Selected settings section.";
                    break;
                case BridgeIntentType.SelectSetting:
                    _selectedSettingId = intent.SettingId ?? string.Empty;
                    EnsureSelectedSetting();
                    statusMessage = "Selected setting.";
                    break;
                case BridgeIntentType.SetSettingValue:
                    SetSettingValue(intent.SettingId, intent.SettingValue);
                    statusMessage = "Updated setting draft value.";
                    break;
                case BridgeIntentType.SetSettingsSearchQuery:
                    _settingsSessionState.SearchQuery = intent.SearchQuery ?? string.Empty;
                    _settingsSessionState.AppliedSearchQuery = _settingsSessionState.SearchQuery;
                    RefreshSettingsProjection();
                    statusMessage = "Updated settings search.";
                    break;
                default:
                    return false;
            }

            _shellState.StatusMessage = statusMessage;
            CacheRuntimeMirror();
            return true;
        }

        public SettingsBridgeSnapshot BuildSnapshot()
        {
            var snapshot = new SettingsBridgeSnapshot
            {
                CurrentSettings = RuntimeDesktopBridgeModelCloner.CloneShellSettings(_shellSettings),
                Document = RuntimeDesktopBridgeModelCloner.CloneSettingsDocument(_settingsDocument),
                SelectedSectionId = _selectedSectionId,
                SelectedSettingId = _selectedSettingId,
                SearchQuery = _settingsSessionState.SearchQuery,
                ShowModifiedOnly = _settingsSessionState.ShowModifiedOnly
            };

            snapshot.VisibleSections.AddRange(_visibleSettingsSections.Select(RuntimeDesktopBridgeModelCloner.CloneSettingsSection));
            snapshot.ActiveSettings.AddRange(_activeSettings.Select(RuntimeDesktopBridgeModelCloner.CloneSettingDescriptor));
            foreach (var pair in _settingsDraftState.Values.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.DraftValues.Add(new BridgeSettingValueEntry
                {
                    SettingId = pair.Key ?? string.Empty,
                    Value = pair.Value ?? string.Empty
                });
            }

            return snapshot;
        }

        public OnboardingState BuildOnboardingState()
        {
            return RuntimeDesktopBridgeModelCloner.CloneOnboardingState(_onboardingState);
        }

        public OnboardingFlowModel BuildOnboardingFlow()
        {
            return RuntimeDesktopBridgeModelCloner.CloneOnboardingFlow(_onboardingService.BuildFlow(_onboardingState, _catalog));
        }

        public string BuildThemePreviewSummary()
        {
            var selection = _onboardingService.ResolveSelection(_onboardingState, _shellSettings, _catalog);
            return selection.Theme != null
                ? (selection.Theme.DisplayName ?? string.Empty) + " | " + (selection.Theme.Description ?? string.Empty)
                : "No theme selected.";
        }

        public string ResolveActiveLayoutPresetId()
        {
            if (!string.IsNullOrEmpty(_shellSettings.DefaultOnboardingLayoutPresetId))
            {
                return _shellSettings.DefaultOnboardingLayoutPresetId;
            }

            if (!string.IsNullOrEmpty(_onboardingState.SelectedLayoutPresetId))
            {
                return _onboardingState.SelectedLayoutPresetId;
            }

            return _catalog.OnboardingLayouts.Count > 0
                ? _catalog.OnboardingLayouts[0].LayoutPresetId
                : string.Empty;
        }

        private void ApplyRuntimeSettings(bool resetDraft)
        {
            _shellSettings = ConvertToShellSettings(_shellState.Settings);
            if (string.IsNullOrEmpty(_shellSettings.WorkspaceRootPath))
            {
                _shellSettings.WorkspaceRootPath = Directory.GetCurrentDirectory();
            }

            _settingsDocument = _settingsDocumentBuilder.BuildDocument(_catalog);
            _settingsSessionState.SearchQuery = _shellSettings.SettingsSearchQuery ?? string.Empty;
            _settingsSessionState.AppliedSearchQuery = _shellSettings.SettingsSearchQuery ?? string.Empty;
            _settingsSessionState.ShowModifiedOnly = _shellSettings.SettingsShowModifiedOnly;
            _settingsSessionState.ActiveSectionId = string.IsNullOrEmpty(_shellSettings.SettingsActiveSectionId)
                ? (_settingsDocument.Sections.Count > 0 ? _settingsDocument.Sections[0].SectionId : string.Empty)
                : _shellSettings.SettingsActiveSectionId;

            if (resetDraft)
            {
                _settingsDraftState = new SettingsDraftState();
                _settingsDraftService.Initialize(_settingsDraftState, _catalog, _shellSettings);
            }

            _onboardingState = new OnboardingState();
            _onboardingService.Seed(_onboardingState, _shellSettings, _catalog);
        }

        private void SaveSettings()
        {
            _shellSettings = _settingsApplicationService.Apply(
                _settingsDraftService,
                _settingsDraftState,
                _settingsSessionState,
                _catalog,
                _shellSettings);
            ApplyShellSettingsToRuntime();
            PersistSettings();
            _settingsDraftService.Initialize(_settingsDraftState, _catalog, _shellSettings);
            RefreshSettingsProjection();
        }

        private void ApplyOnboarding()
        {
            _shellSettings = _onboardingService.Apply(_onboardingState, _catalog, _shellSettings);
            ApplyShellSettingsToRuntime();
            PersistSettings();
            _settingsDraftService.Initialize(_settingsDraftState, _catalog, _shellSettings);
            RefreshSettingsProjection();
        }

        private void SetSettingValue(string settingId, string value)
        {
            var effectiveSettingId = !string.IsNullOrEmpty(settingId) ? settingId : _selectedSettingId;
            if (string.IsNullOrEmpty(effectiveSettingId))
            {
                return;
            }

            _settingsDraftState.Values[effectiveSettingId] = value ?? string.Empty;
            _selectedSettingId = effectiveSettingId;
        }

        private void RefreshSettingsProjection()
        {
            _visibleSettingsSections.Clear();
            _activeSettings.Clear();

            var normalizedQuery = _settingsSearchService.NormalizeQuery(_settingsSessionState.AppliedSearchQuery);
            _visibleSettingsSections.AddRange(
                _settingsSearchService
                    .GetVisibleSections(_settingsDocument, normalizedQuery)
                    .Select(RuntimeDesktopBridgeModelCloner.CloneSettingsSection));

            _selectedSectionId = _settingsSearchService.ResolveActiveSectionId(_settingsDocument, _settingsSessionState.ActiveSectionId, normalizedQuery);
            _settingsSessionState.ActiveSectionId = _selectedSectionId;

            _activeSettings.AddRange(
                _catalog.Settings
                    .Where(setting => string.Equals(setting.SectionId, _selectedSectionId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(setting => setting.SortOrder)
                    .ThenBy(setting => setting.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(RuntimeDesktopBridgeModelCloner.CloneSettingDescriptor));
            EnsureSelectedSetting();
        }

        private void EnsureSelectedSetting()
        {
            if (_activeSettings.Any(setting => string.Equals(setting.SettingId, _selectedSettingId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _selectedSettingId = _activeSettings.Count > 0 ? _activeSettings[0].SettingId ?? string.Empty : string.Empty;
        }

        private void ApplyShellSettingsToRuntime()
        {
            var runtimeSettings = _shellState.Settings ?? new CortexSettings();
            runtimeSettings.WorkspaceRootPath = _shellSettings.WorkspaceRootPath ?? string.Empty;
            runtimeSettings.RuntimeContentRootPath = _shellSettings.RuntimeContentRootPath ?? string.Empty;
            runtimeSettings.ReferenceAssemblyRootPath = _shellSettings.ReferenceAssemblyRootPath ?? string.Empty;
            runtimeSettings.AdditionalSourceRoots = _shellSettings.AdditionalSourceRoots ?? string.Empty;
            runtimeSettings.ThemeId = _shellSettings.ThemeId ?? runtimeSettings.ThemeId;
            runtimeSettings.DefaultOnboardingProfileId = _shellSettings.DefaultOnboardingProfileId ?? string.Empty;
            runtimeSettings.DefaultOnboardingLayoutPresetId = _shellSettings.DefaultOnboardingLayoutPresetId ?? string.Empty;
            runtimeSettings.DefaultOnboardingThemeId = _shellSettings.DefaultOnboardingThemeId ?? string.Empty;
            runtimeSettings.DefaultBuildConfiguration = _shellSettings.DefaultBuildConfiguration ?? "Debug";
            runtimeSettings.BuildTimeoutMs = _shellSettings.BuildTimeoutMs;
            runtimeSettings.EnableFileEditing = _shellSettings.EnableFileEditing;
            runtimeSettings.EnableFileSaving = _shellSettings.EnableFileSaving;
            runtimeSettings.EditorUndoHistoryLimit = _shellSettings.EditorUndoHistoryLimit;
            runtimeSettings.SettingsActiveSectionId = _shellSettings.SettingsActiveSectionId ?? string.Empty;
            runtimeSettings.SettingsSearchQuery = _shellSettings.SettingsSearchQuery ?? string.Empty;
            runtimeSettings.SettingsShowModifiedOnly = _shellSettings.SettingsShowModifiedOnly;
            runtimeSettings.HasCompletedOnboarding = _shellSettings.HasCompletedOnboarding;

            _shellState.Settings = runtimeSettings;
        }

        private void PersistSettings()
        {
            var settingsStore = _settingsStoreAccessor != null ? _settingsStoreAccessor() : null;
            settingsStore?.Save(_shellState.Settings);
        }

        private void CacheRuntimeMirror()
        {
            _cachedSettingsFingerprint = BuildSettingsFingerprint(_shellState.Settings);
        }

        private static string BuildSettingsFingerprint(CortexSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            return string.Join(
                "|",
                new[]
                {
                    settings.WorkspaceRootPath ?? string.Empty,
                    settings.RuntimeContentRootPath ?? string.Empty,
                    settings.ReferenceAssemblyRootPath ?? string.Empty,
                    settings.ThemeId ?? string.Empty,
                    settings.DefaultOnboardingProfileId ?? string.Empty,
                    settings.DefaultOnboardingLayoutPresetId ?? string.Empty,
                    settings.DefaultOnboardingThemeId ?? string.Empty,
                    settings.DefaultBuildConfiguration ?? string.Empty,
                    settings.BuildTimeoutMs.ToString(),
                    settings.EnableFileEditing.ToString(),
                    settings.EnableFileSaving.ToString(),
                    settings.EditorUndoHistoryLimit.ToString(),
                    settings.SettingsActiveSectionId ?? string.Empty,
                    settings.SettingsSearchQuery ?? string.Empty,
                    settings.SettingsShowModifiedOnly.ToString(),
                    settings.HasCompletedOnboarding.ToString()
                });
        }

        private static ShellSettings ConvertToShellSettings(CortexSettings settings)
        {
            if (settings == null)
            {
                return new ShellSettings();
            }

            return new ShellSettings
            {
                WorkspaceRootPath = settings.WorkspaceRootPath ?? string.Empty,
                RuntimeContentRootPath = settings.RuntimeContentRootPath ?? string.Empty,
                ReferenceAssemblyRootPath = settings.ReferenceAssemblyRootPath ?? string.Empty,
                AdditionalSourceRoots = settings.AdditionalSourceRoots ?? string.Empty,
                ThemeId = settings.ThemeId ?? "cortex.vs-dark",
                DefaultOnboardingProfileId = settings.DefaultOnboardingProfileId ?? string.Empty,
                DefaultOnboardingLayoutPresetId = settings.DefaultOnboardingLayoutPresetId ?? string.Empty,
                DefaultOnboardingThemeId = settings.DefaultOnboardingThemeId ?? string.Empty,
                DefaultBuildConfiguration = settings.DefaultBuildConfiguration ?? "Debug",
                BuildTimeoutMs = settings.BuildTimeoutMs,
                EnableFileEditing = settings.EnableFileEditing,
                EnableFileSaving = settings.EnableFileSaving,
                EditorUndoHistoryLimit = settings.EditorUndoHistoryLimit,
                SettingsActiveSectionId = settings.SettingsActiveSectionId ?? string.Empty,
                SettingsSearchQuery = settings.SettingsSearchQuery ?? string.Empty,
                SettingsShowModifiedOnly = settings.SettingsShowModifiedOnly,
                HasCompletedOnboarding = settings.HasCompletedOnboarding
            };
        }
    }
}
