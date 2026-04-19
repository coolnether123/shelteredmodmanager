using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cortex.Bridge;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Models;
using Cortex.Shell.Shared.Models;
using Cortex.Shell.Shared.Services;
using LiveSettingsContributionCollectionService = Cortex.Services.Settings.SettingsContributionCollectionService;
using LiveSettingsDocumentBuilder = Cortex.Services.Settings.SettingsDocumentBuilder;
using LiveSettingsDraftService = Cortex.Services.Settings.SettingsDraftService;
using LiveSettingsSearchService = Cortex.Services.Settings.SettingsSearchService;
using LiveSettingsSessionService = Cortex.Services.Settings.SettingsSessionService;
using LiveSettingsDocumentModel = Cortex.Services.Settings.SettingsDocumentModel;
using LiveSettingsDraftState = Cortex.Services.Settings.SettingsDraftState;
using LiveSettingsNavigationGroupModel = Cortex.Services.Settings.SettingsNavigationGroupModel;
using LiveSettingsSectionKind = Cortex.Services.Settings.SettingsSectionKind;
using LiveSettingsSectionModel = Cortex.Services.Settings.SettingsSectionModel;
using LiveSettingsSessionState = Cortex.Services.Settings.SettingsSessionState;

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
        private readonly LiveSettingsDocumentBuilder _liveSettingsDocumentBuilder = new LiveSettingsDocumentBuilder();
        private readonly LiveSettingsSearchService _liveSettingsSearchService = new LiveSettingsSearchService();
        private readonly LiveSettingsDraftService _liveSettingsDraftService = new LiveSettingsDraftService();
        private readonly LiveSettingsSessionService _liveSettingsSessionService = new LiveSettingsSessionService();
        private readonly LiveSettingsContributionCollectionService _liveSettingsContributionCollectionService = new LiveSettingsContributionCollectionService();

        private readonly WorkbenchCatalogSnapshot _catalog;
        private ShellSettings _shellSettings;
        private OnboardingState _onboardingState;
        private SettingsDocumentModel _settingsDocument;
        private SettingsSessionState _settingsSessionState;
        private SettingsDraftState _settingsDraftState;
        private WorkbenchPresentationSnapshot _liveSettingsSnapshot;
        private LiveSettingsDocumentModel _liveSettingsDocument;
        private LiveSettingsSessionState _liveSettingsSessionState;
        private LiveSettingsDraftState _liveSettingsDraftState;
        private readonly List<LiveSettingsSectionModel> _liveVisibleSettingsSections = new List<LiveSettingsSectionModel>();
        private readonly List<SettingContribution> _liveActiveSettings = new List<SettingContribution>();
        private readonly List<SettingsSectionModel> _visibleSettingsSections = new List<SettingsSectionModel>();
        private readonly List<SettingDescriptor> _activeSettings = new List<SettingDescriptor>();
        private string _selectedSectionId = string.Empty;
        private string _selectedSettingId = string.Empty;
        private string _cachedSettingsFingerprint = string.Empty;
        private string _cachedLiveSettingsFingerprint = string.Empty;
        private bool _liveSettingsLoaded;
        private static readonly FieldInfo[] CortexSettingsFingerprintFields = CreateCortexSettingsFingerprintFields();

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
            _liveSettingsDocument = new LiveSettingsDocumentModel();
            _liveSettingsSessionState = new LiveSettingsSessionState();
            _liveSettingsDraftState = new LiveSettingsDraftState();
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

        public bool SynchronizeFromPresentation(WorkbenchPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            var currentSettingsFingerprint = BuildLiveSettingsFingerprint(snapshot, _shellState.Settings);
            if (_liveSettingsLoaded &&
                string.Equals(_cachedLiveSettingsFingerprint, currentSettingsFingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            ApplyLiveRuntimeSettings(snapshot, true);
            RefreshLiveSettingsProjection();
            _cachedLiveSettingsFingerprint = currentSettingsFingerprint;
            _liveSettingsLoaded = true;
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
                    if (_liveSettingsLoaded)
                    {
                        SaveLiveSettings();
                        statusMessage = "Saved Cortex settings.";
                    }
                    else
                    {
                        SaveSettings();
                        statusMessage = "Saved legacy runtime settings.";
                    }
                    break;
                case BridgeIntentType.SelectSettingsSection:
                    if (_liveSettingsLoaded)
                    {
                        _liveSettingsSessionService.RequestSection(_liveSettingsSessionState, intent.SectionId ?? string.Empty);
                        RefreshLiveSettingsProjection();
                    }
                    else
                    {
                        _settingsSessionState.ActiveSectionId = intent.SectionId ?? string.Empty;
                        RefreshSettingsProjection();
                    }
                    statusMessage = "Selected settings section.";
                    break;
                case BridgeIntentType.SelectSetting:
                    if (_liveSettingsLoaded)
                    {
                        _selectedSettingId = intent.SettingId ?? string.Empty;
                        EnsureLiveSelectedSetting();
                    }
                    else
                    {
                        _selectedSettingId = intent.SettingId ?? string.Empty;
                        EnsureSelectedSetting();
                    }
                    statusMessage = "Selected setting.";
                    break;
                case BridgeIntentType.SetSettingValue:
                    if (_liveSettingsLoaded)
                    {
                        SetLiveSettingValue(intent.SettingId, intent.SettingValue);
                    }
                    else
                    {
                        SetSettingValue(intent.SettingId, intent.SettingValue);
                    }
                    statusMessage = "Updated setting draft value.";
                    break;
                case BridgeIntentType.SetSettingsSearchQuery:
                    if (_liveSettingsLoaded)
                    {
                        _liveSettingsSessionState.SearchQuery = intent.SearchQuery ?? string.Empty;
                        _liveSettingsSessionState.AppliedSearchQuery = _liveSettingsSessionState.SearchQuery;
                        RefreshLiveSettingsProjection();
                    }
                    else
                    {
                        _settingsSessionState.SearchQuery = intent.SearchQuery ?? string.Empty;
                        _settingsSessionState.AppliedSearchQuery = _settingsSessionState.SearchQuery;
                        RefreshSettingsProjection();
                    }
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
            if (_liveSettingsLoaded)
            {
                return BuildLiveSettingsSnapshot();
            }

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

        private void SaveLiveSettings()
        {
            var effectiveSettings = _shellState.Settings ?? new CortexSettings();
            _liveSettingsDraftService.ApplyDraft(_liveSettingsDraftState, _liveSettingsSnapshot, effectiveSettings);
            _liveSettingsSessionService.Persist(_liveSettingsSessionState, effectiveSettings, 0f, 0f);
            _shellState.Settings = effectiveSettings;
            _shellState.ReloadSettingsRequested = true;
            ApplyRuntimeSettings(false);
            PersistSettings();
            ApplyLiveRuntimeSettings(_liveSettingsSnapshot, true);
            RefreshLiveSettingsProjection();
            _cachedLiveSettingsFingerprint = BuildLiveSettingsFingerprint(_liveSettingsSnapshot, _shellState.Settings);
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

        private void ApplyLiveRuntimeSettings(WorkbenchPresentationSnapshot snapshot, bool resetDraft)
        {
            _liveSettingsSnapshot = snapshot ?? new WorkbenchPresentationSnapshot();
            _liveSettingsDocument = _liveSettingsDocumentBuilder.BuildDocument(_liveSettingsSnapshot);
            float navigationScrollY;
            float contentScrollY;
            _liveSettingsSessionService.Restore(
                _liveSettingsSessionState,
                _shellState.Settings,
                _liveSettingsDocument.Sections.Count > 0 ? _liveSettingsDocument.Sections[0].SectionId : string.Empty,
                out navigationScrollY,
                out contentScrollY);

            if (resetDraft)
            {
                _liveSettingsDraftState = new LiveSettingsDraftState();
                var themeState = new ThemeState();
                themeState.ThemeId = _shellState.Settings != null && !string.IsNullOrEmpty(_shellState.Settings.ThemeId)
                    ? _shellState.Settings.ThemeId
                    : (_liveSettingsSnapshot != null ? _liveSettingsSnapshot.ActiveThemeId : string.Empty);
                _liveSettingsDraftService.Initialize(_liveSettingsDraftState, _liveSettingsSnapshot, themeState, _shellState.Settings);
            }
        }

        private void RefreshLiveSettingsProjection()
        {
            _liveVisibleSettingsSections.Clear();
            _liveActiveSettings.Clear();

            var normalizedQuery = _liveSettingsSearchService.NormalizeQuery(_liveSettingsSessionState.AppliedSearchQuery);
            for (var groupIndex = 0; groupIndex < _liveSettingsDocument.Groups.Count; groupIndex++)
            {
                var visibleSections = _liveSettingsSearchService.GetVisibleSections(
                    _liveSettingsDocument.Groups[groupIndex],
                    normalizedQuery,
                    _liveSettingsSessionState.ShowModifiedOnly,
                    GetLiveSectionVisibleItemCount);
                _liveVisibleSettingsSections.AddRange(visibleSections);
            }

            _selectedSectionId = _liveSettingsSearchService.ResolveRenderActiveSectionId(
                _liveSettingsDocument,
                _liveSettingsSessionState.ActiveSectionId,
                normalizedQuery,
                _liveSettingsSessionState.ShowModifiedOnly,
                GetLiveSectionVisibleItemCount);
            _liveSettingsSessionState.ActiveSectionId = _selectedSectionId;

            var activeSection = FindLiveSection(_selectedSectionId);
            _liveActiveSettings.AddRange(CollectLiveContributionsForSection(activeSection));
            _liveActiveSettings.Sort(CompareSettingContributions);
            EnsureLiveSelectedSetting();
        }

        private SettingsBridgeSnapshot BuildLiveSettingsSnapshot()
        {
            var snapshot = new SettingsBridgeSnapshot
            {
                CurrentSettings = RuntimeDesktopBridgeModelCloner.CloneShellSettings(ConvertToShellSettings(_shellState.Settings)),
                Document = CloneLiveSettingsDocument(_liveSettingsDocument),
                SelectedSectionId = _selectedSectionId,
                SelectedSettingId = _selectedSettingId,
                SearchQuery = _liveSettingsSessionState.SearchQuery,
                ShowModifiedOnly = _liveSettingsSessionState.ShowModifiedOnly
            };

            for (var i = 0; i < _liveVisibleSettingsSections.Count; i++)
            {
                snapshot.VisibleSections.Add(CloneLiveSettingsSection(_liveVisibleSettingsSections[i]));
            }

            for (var i = 0; i < _liveActiveSettings.Count; i++)
            {
                snapshot.ActiveSettings.Add(ToSettingDescriptor(_liveActiveSettings[i], _selectedSectionId));
            }

            AddLiveDraftValues(snapshot);
            return snapshot;
        }

        private void AddLiveDraftValues(SettingsBridgeSnapshot snapshot)
        {
            if (snapshot == null || _liveSettingsDraftState == null)
            {
                return;
            }

            foreach (var pair in _liveSettingsDraftState.TextValues.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.DraftValues.Add(new BridgeSettingValueEntry
                {
                    SettingId = pair.Key ?? string.Empty,
                    Value = pair.Value ?? string.Empty
                });
            }

            foreach (var pair in _liveSettingsDraftState.ToggleValues.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.DraftValues.Add(new BridgeSettingValueEntry
                {
                    SettingId = pair.Key ?? string.Empty,
                    Value = pair.Value ? "true" : "false"
                });
            }

            if (!string.IsNullOrEmpty(_liveSettingsDraftState.SelectedThemeId))
            {
                snapshot.DraftValues.Add(new BridgeSettingValueEntry
                {
                    SettingId = nameof(CortexSettings.ThemeId),
                    Value = _liveSettingsDraftState.SelectedThemeId
                });
            }
        }

        private int GetLiveSectionVisibleItemCount(LiveSettingsSectionModel section)
        {
            return CollectLiveContributionsForSection(section).Count;
        }

        private List<SettingContribution> CollectLiveContributionsForSection(LiveSettingsSectionModel section)
        {
            var results = new List<SettingContribution>();
            if (section == null || _liveSettingsSnapshot == null)
            {
                return results;
            }

            if (section.SectionKind == LiveSettingsSectionKind.WorkspacePaths)
            {
                AddLiveContributionIfVisible(results, CortexHostPathSettings.WorkspaceRootSettingId);
                AddLiveContributionIfVisible(results, CortexHostPathSettings.RuntimeContentRootSettingId);
                AddLiveContributionIfVisible(results, CortexHostPathSettings.ReferenceAssemblyRootSettingId);
                AddLiveContributionIfVisible(results, CortexHostPathSettings.AdditionalSourceRootsSettingId);
                return results;
            }

            if (section.SectionKind == LiveSettingsSectionKind.WorkspaceSettingsContributions)
            {
                return _liveSettingsContributionCollectionService.CollectVisibleContributionsForScope(_liveSettingsSnapshot, "Workspace", IsLiveContributionVisible);
            }

            if (section.SectionKind == LiveSettingsSectionKind.ContributionScope)
            {
                return _liveSettingsContributionCollectionService.CollectVisibleContributionsForScope(_liveSettingsSnapshot, section.ContributionScope, IsLiveContributionVisible);
            }

            if (section.SectionKind == LiveSettingsSectionKind.Themes)
            {
                AddLiveContributionIfVisible(results, nameof(CortexSettings.ThemeId));
                return results;
            }

            AddLiveContributionIfVisibleForScope(results, section.Scope);
            return results;
        }

        private void AddLiveContributionIfVisible(List<SettingContribution> results, string settingId)
        {
            var contribution = FindLiveSettingContribution(settingId);
            if (contribution != null && IsLiveContributionVisible(contribution))
            {
                results.Add(contribution);
            }
        }

        private void AddLiveContributionIfVisibleForScope(List<SettingContribution> results, string scope)
        {
            if (_liveSettingsSnapshot == null || string.IsNullOrEmpty(scope))
            {
                return;
            }

            for (var i = 0; i < _liveSettingsSnapshot.Settings.Count; i++)
            {
                var contribution = _liveSettingsSnapshot.Settings[i];
                if (contribution != null &&
                    string.Equals(GetContributionScope(contribution), scope, StringComparison.OrdinalIgnoreCase) &&
                    IsLiveContributionVisible(contribution))
                {
                    results.Add(contribution);
                }
            }
        }

        private bool IsLiveContributionVisible(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            if (_liveSettingsSessionState.ShowModifiedOnly &&
                !_liveSettingsDraftService.IsSettingModified(_liveSettingsDraftState, contribution, _shellState.Settings))
            {
                return false;
            }

            return _liveSettingsSearchService.MatchesSearch(
                _liveSettingsSearchService.NormalizeQuery(_liveSettingsSessionState.AppliedSearchQuery),
                BuildContributionSearchText(contribution));
        }

        private void SetLiveSettingValue(string settingId, string value)
        {
            var effectiveSettingId = !string.IsNullOrEmpty(settingId) ? settingId : _selectedSettingId;
            if (string.IsNullOrEmpty(effectiveSettingId))
            {
                return;
            }

            var contribution = FindLiveSettingContribution(effectiveSettingId);
            if (contribution == null)
            {
                return;
            }

            _liveSettingsDraftService.SetDraftSerializedValue(_liveSettingsDraftState, contribution, value ?? string.Empty);
            if (string.Equals(effectiveSettingId, nameof(CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase))
            {
                _liveSettingsDraftState.SelectedThemeId = value ?? string.Empty;
            }

            _selectedSettingId = effectiveSettingId;
            RefreshLiveSettingsProjection();
        }

        private void EnsureLiveSelectedSetting()
        {
            for (var i = 0; i < _liveActiveSettings.Count; i++)
            {
                if (_liveActiveSettings[i] != null &&
                    string.Equals(_liveActiveSettings[i].SettingId, _selectedSettingId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _selectedSettingId = _liveActiveSettings.Count > 0 ? _liveActiveSettings[0].SettingId ?? string.Empty : string.Empty;
        }

        private SettingContribution FindLiveSettingContribution(string settingId)
        {
            if (_liveSettingsSnapshot == null || string.IsNullOrEmpty(settingId))
            {
                return null;
            }

            for (var i = 0; i < _liveSettingsSnapshot.Settings.Count; i++)
            {
                var contribution = _liveSettingsSnapshot.Settings[i];
                if (contribution != null && string.Equals(contribution.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    return contribution;
                }
            }

            return null;
        }

        private LiveSettingsSectionModel FindLiveSection(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId) || _liveSettingsDocument == null)
            {
                return null;
            }

            for (var i = 0; i < _liveSettingsDocument.Sections.Count; i++)
            {
                var section = _liveSettingsDocument.Sections[i];
                if (section != null && string.Equals(section.SectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return section;
                }
            }

            return null;
        }

        private void EnsureSelectedSetting()
        {
            if (_activeSettings.Any(setting => string.Equals(setting.SettingId, _selectedSettingId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _selectedSettingId = _activeSettings.Count > 0 ? _activeSettings[0].SettingId ?? string.Empty : string.Empty;
        }

        private SettingsDocumentModel CloneLiveSettingsDocument(LiveSettingsDocumentModel document)
        {
            var clone = new SettingsDocumentModel();
            if (document == null)
            {
                return clone;
            }

            for (var i = 0; i < document.Sections.Count; i++)
            {
                clone.Sections.Add(CloneLiveSettingsSection(document.Sections[i]));
            }

            for (var i = 0; i < document.Groups.Count; i++)
            {
                clone.Groups.Add(CloneLiveSettingsGroup(document.Groups[i]));
            }

            return clone;
        }

        private SettingsNavigationGroupModel CloneLiveSettingsGroup(LiveSettingsNavigationGroupModel group)
        {
            var clone = new SettingsNavigationGroupModel
            {
                GroupId = group != null ? group.GroupId ?? string.Empty : string.Empty,
                Title = group != null ? group.Title ?? string.Empty : string.Empty,
                SortOrder = group != null ? group.SortOrder : 0
            };

            if (group != null)
            {
                for (var i = 0; i < group.Sections.Count; i++)
                {
                    clone.Sections.Add(CloneLiveSettingsSection(group.Sections[i]));
                }
            }

            return clone;
        }

        private static SettingsSectionModel CloneLiveSettingsSection(LiveSettingsSectionModel section)
        {
            if (section == null)
            {
                return new SettingsSectionModel();
            }

            return new SettingsSectionModel
            {
                SectionId = section.SectionId ?? string.Empty,
                GroupId = section.GroupId ?? string.Empty,
                GroupTitle = section.GroupTitle ?? string.Empty,
                Scope = section.Scope ?? string.Empty,
                Title = section.Title ?? string.Empty,
                Description = section.Description ?? string.Empty,
                SearchText = section.SearchText ?? string.Empty,
                SortOrder = section.SortOrder
            };
        }

        private SettingDescriptor ToSettingDescriptor(SettingContribution contribution, string sectionId)
        {
            if (contribution == null)
            {
                return new SettingDescriptor();
            }

            return new SettingDescriptor
            {
                SettingId = contribution.SettingId ?? string.Empty,
                SectionId = sectionId ?? string.Empty,
                DisplayName = contribution.DisplayName ?? contribution.SettingId ?? string.Empty,
                Description = contribution.Description ?? string.Empty,
                Scope = GetContributionScope(contribution),
                DefaultValue = _liveSettingsDraftService.GetDefaultSerializedValue(contribution),
                ValueKind = ToShellValueKind(contribution.ValueKind),
                EditorKind = ToShellEditorKind(contribution),
                PlaceholderText = contribution.PlaceholderText ?? string.Empty,
                HelpText = contribution.HelpText ?? string.Empty,
                Keywords = contribution.Keywords ?? new string[0],
                Options = ToSettingChoices(contribution),
                IsRequired = contribution.IsRequired,
                IsSecret = contribution.IsSecret,
                SortOrder = contribution.SortOrder
            };
        }

        private SettingChoiceDescriptor[] ToSettingChoices(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return new SettingChoiceDescriptor[0];
            }

            if (string.Equals(contribution.SettingId, nameof(CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase) &&
                _liveSettingsSnapshot != null &&
                _liveSettingsSnapshot.Themes.Count > 0)
            {
                var themeOptions = new List<SettingChoiceDescriptor>();
                for (var i = 0; i < _liveSettingsSnapshot.Themes.Count; i++)
                {
                    var theme = _liveSettingsSnapshot.Themes[i];
                    if (theme == null)
                    {
                        continue;
                    }

                    themeOptions.Add(new SettingChoiceDescriptor
                    {
                        Value = theme.ThemeId ?? string.Empty,
                        DisplayName = theme.DisplayName ?? theme.ThemeId ?? string.Empty,
                        Description = theme.Description ?? string.Empty
                    });
                }

                return themeOptions.ToArray();
            }

            if (contribution.Options == null || contribution.Options.Length == 0)
            {
                return new SettingChoiceDescriptor[0];
            }

            var options = new SettingChoiceDescriptor[contribution.Options.Length];
            for (var i = 0; i < contribution.Options.Length; i++)
            {
                var option = contribution.Options[i];
                options[i] = new SettingChoiceDescriptor
                {
                    Value = option != null ? option.Value ?? string.Empty : string.Empty,
                    DisplayName = option != null ? option.DisplayName ?? string.Empty : string.Empty,
                    Description = option != null ? option.Description ?? string.Empty : string.Empty
                };
            }

            return options;
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

        private static ShellSettingValueKind ToShellValueKind(SettingValueKind valueKind)
        {
            switch (valueKind)
            {
                case SettingValueKind.Boolean:
                    return ShellSettingValueKind.Boolean;
                case SettingValueKind.Integer:
                    return ShellSettingValueKind.Integer;
                case SettingValueKind.Float:
                case SettingValueKind.String:
                default:
                    return ShellSettingValueKind.String;
            }
        }

        private static ShellSettingEditorKind ToShellEditorKind(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return ShellSettingEditorKind.Text;
            }

            if ((contribution.Options != null && contribution.Options.Length > 0) ||
                string.Equals(contribution.SettingId, nameof(CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase))
            {
                return ShellSettingEditorKind.Choice;
            }

            switch (contribution.EditorKind)
            {
                case SettingEditorKind.Path:
                    return ShellSettingEditorKind.Path;
                case SettingEditorKind.Choice:
                    return ShellSettingEditorKind.Choice;
                case SettingEditorKind.Secret:
                    return ShellSettingEditorKind.Secret;
                case SettingEditorKind.MultilineText:
                    return ShellSettingEditorKind.MultilineText;
                case SettingEditorKind.Auto:
                case SettingEditorKind.Text:
                default:
                    return contribution.ValueKind == SettingValueKind.Boolean
                        ? ShellSettingEditorKind.Choice
                        : ShellSettingEditorKind.Text;
            }
        }

        private static int CompareSettingContributions(SettingContribution left, SettingContribution right)
        {
            var order = (left != null ? left.SortOrder : int.MaxValue)
                .CompareTo(right != null ? right.SortOrder : int.MaxValue);
            if (order != 0)
            {
                return order;
            }

            return string.Compare(
                left != null ? left.DisplayName ?? left.SettingId : string.Empty,
                right != null ? right.DisplayName ?? right.SettingId : string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetContributionScope(SettingContribution contribution)
        {
            return string.IsNullOrEmpty(contribution != null ? contribution.Scope : string.Empty)
                ? "General"
                : contribution.Scope;
        }

        private static string BuildContributionSearchText(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            return
                (contribution.DisplayName ?? string.Empty) + " " +
                (contribution.Description ?? string.Empty) + " " +
                (contribution.HelpText ?? string.Empty) + " " +
                (contribution.PlaceholderText ?? string.Empty) + " " +
                (contribution.Scope ?? string.Empty) + " " +
                (contribution.SettingId ?? string.Empty) + " " +
                BuildKeywordsText(contribution.Keywords) + " " +
                BuildChoiceSearchText(contribution.Options);
        }

        private static string BuildKeywordsText(string[] keywords)
        {
            return keywords == null || keywords.Length == 0
                ? string.Empty
                : string.Join(" ", keywords);
        }

        private static string BuildChoiceSearchText(SettingChoiceOption[] options)
        {
            if (options == null || options.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                if (option == null)
                {
                    continue;
                }

                text +=
                    (option.Value ?? string.Empty) + " " +
                    (option.DisplayName ?? string.Empty) + " " +
                    (option.Description ?? string.Empty) + " ";
            }

            return text;
        }

        private static string BuildLiveSettingsFingerprint(WorkbenchPresentationSnapshot snapshot, CortexSettings settings)
        {
            var contributionFingerprint = string.Empty;
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    var contribution = snapshot.Settings[i];
                    if (contribution == null)
                    {
                        continue;
                    }

                    contributionFingerprint +=
                        (contribution.SettingId ?? string.Empty) + ":" +
                        (contribution.DisplayName ?? string.Empty) + ":" +
                        contribution.SortOrder.ToString() + ";";
                }

                contributionFingerprint += "|themes=" + snapshot.Themes.Count.ToString();
            }

            return BuildSettingsFingerprint(settings) + "|" + contributionFingerprint;
        }

        private static string BuildSettingsFingerprint(CortexSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < CortexSettingsFingerprintFields.Length; i++)
            {
                var field = CortexSettingsFingerprintFields[i];
                AppendFingerprintToken(builder, field.Name);
                builder.Append('=');
                AppendFingerprintValue(builder, field.GetValue(settings));
                builder.Append('|');
            }

            return builder.ToString();
        }

        private static FieldInfo[] CreateCortexSettingsFingerprintFields()
        {
            var fields = typeof(CortexSettings).GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, delegate(FieldInfo left, FieldInfo right)
            {
                return string.Compare(
                    left != null ? left.Name : string.Empty,
                    right != null ? right.Name : string.Empty,
                    StringComparison.Ordinal);
            });
            return fields;
        }

        private static void AppendFingerprintValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                return;
            }

            var moduleSettings = value as ModuleSettingValue[];
            if (moduleSettings != null)
            {
                AppendModuleSettingsFingerprint(builder, moduleSettings);
                return;
            }

            var editorKeybindings = value as EditorKeybinding[];
            if (editorKeybindings != null)
            {
                AppendEditorKeybindingsFingerprint(builder, editorKeybindings);
                return;
            }

            var languageProviderConfigurations = value as LanguageProviderConfiguration[];
            if (languageProviderConfigurations != null)
            {
                AppendLanguageProviderConfigurationsFingerprint(builder, languageProviderConfigurations);
                return;
            }

            var formattable = value as IFormattable;
            AppendFingerprintToken(builder, formattable != null ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString());
        }

        private static void AppendModuleSettingsFingerprint(StringBuilder builder, ModuleSettingValue[] values)
        {
            var sorted = CloneArray(values);
            Array.Sort(sorted, delegate(ModuleSettingValue left, ModuleSettingValue right)
            {
                return string.Compare(
                    left != null ? left.SettingId : string.Empty,
                    right != null ? right.SettingId : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < sorted.Length; i++)
            {
                var value = sorted[i];
                AppendFingerprintPair(builder, value != null ? value.SettingId : string.Empty, value != null ? value.Value : string.Empty);
            }
        }

        private static void AppendEditorKeybindingsFingerprint(StringBuilder builder, EditorKeybinding[] values)
        {
            var sorted = CloneArray(values);
            Array.Sort(sorted, delegate(EditorKeybinding left, EditorKeybinding right)
            {
                var order = string.Compare(
                    left != null ? left.BindingId : string.Empty,
                    right != null ? right.BindingId : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
                return order != 0
                    ? order
                    : string.Compare(
                        left != null ? left.CommandId : string.Empty,
                        right != null ? right.CommandId : string.Empty,
                        StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < sorted.Length; i++)
            {
                var value = sorted[i];
                AppendFingerprintPair(builder, "binding", value != null ? value.BindingId : string.Empty);
                AppendFingerprintPair(builder, "command", value != null ? value.CommandId : string.Empty);
                AppendFingerprintPair(builder, "key", value != null ? value.Key : string.Empty);
                AppendFingerprintPair(builder, "control", value != null && value.Control ? "true" : "false");
                AppendFingerprintPair(builder, "shift", value != null && value.Shift ? "true" : "false");
                AppendFingerprintPair(builder, "alt", value != null && value.Alt ? "true" : "false");
            }
        }

        private static void AppendLanguageProviderConfigurationsFingerprint(StringBuilder builder, LanguageProviderConfiguration[] values)
        {
            var sorted = CloneArray(values);
            Array.Sort(sorted, delegate(LanguageProviderConfiguration left, LanguageProviderConfiguration right)
            {
                return string.Compare(
                    left != null ? left.ProviderId : string.Empty,
                    right != null ? right.ProviderId : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < sorted.Length; i++)
            {
                var value = sorted[i];
                AppendFingerprintPair(builder, "provider", value != null ? value.ProviderId : string.Empty);
                AppendLanguageProviderSettingsFingerprint(builder, value != null ? value.Settings : null);
            }
        }

        private static void AppendLanguageProviderSettingsFingerprint(StringBuilder builder, LanguageProviderSettingValue[] values)
        {
            var sorted = CloneArray(values);
            Array.Sort(sorted, delegate(LanguageProviderSettingValue left, LanguageProviderSettingValue right)
            {
                return string.Compare(
                    left != null ? left.SettingId : string.Empty,
                    right != null ? right.SettingId : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < sorted.Length; i++)
            {
                var value = sorted[i];
                AppendFingerprintPair(builder, value != null ? value.SettingId : string.Empty, value != null ? value.Value : string.Empty);
            }
        }

        private static T[] CloneArray<T>(T[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new T[0];
            }

            var clone = new T[values.Length];
            Array.Copy(values, clone, values.Length);
            return clone;
        }

        private static void AppendFingerprintPair(StringBuilder builder, string name, string value)
        {
            AppendFingerprintToken(builder, name);
            builder.Append(':');
            AppendFingerprintToken(builder, value);
            builder.Append(';');
        }

        private static void AppendFingerprintToken(StringBuilder builder, string value)
        {
            var safeValue = value ?? string.Empty;
            builder.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append('#');
            builder.Append(safeValue);
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
