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
    internal sealed class RuntimeDesktopBridgeSession
    {
        private readonly CortexShellState _shellState;
        private readonly Func<ICortexSettingsStore> _settingsStoreAccessor;
        private readonly Func<IProjectCatalog> _projectCatalogAccessor;
        private readonly SettingsDocumentBuilder _settingsDocumentBuilder = new SettingsDocumentBuilder();
        private readonly SettingsSearchService _settingsSearchService = new SettingsSearchService();
        private readonly SettingsDraftService _settingsDraftService = new SettingsDraftService();
        private readonly SettingsApplicationService _settingsApplicationService = new SettingsApplicationService();
        private readonly OnboardingService _onboardingService = new OnboardingService();
        private readonly ProjectWorkspaceService _projectWorkspaceService = new ProjectWorkspaceService();

        private readonly WorkbenchCatalogSnapshot _catalog;
        private ShellSettings _shellSettings;
        private OnboardingState _onboardingState;
        private SettingsDocumentModel _settingsDocument;
        private SettingsSessionState _settingsSessionState;
        private SettingsDraftState _settingsDraftState;
        private readonly List<SettingsSectionModel> _visibleSettingsSections = new List<SettingsSectionModel>();
        private readonly List<SettingDescriptor> _activeSettings = new List<SettingDescriptor>();
        private readonly List<WorkspaceProjectDefinition> _projects = new List<WorkspaceProjectDefinition>();
        private WorkspaceProjectDefinition _selectedProject;
        private WorkspaceFileNode _workspaceTreeRoot;
        private string _selectedSectionId = string.Empty;
        private string _selectedSettingId = string.Empty;
        private string _previewFilePath = string.Empty;
        private string _previewText = string.Empty;
        private string _statusMessage = string.Empty;
        private string _cachedSettingsFingerprint = string.Empty;
        private string _cachedSelectedProjectId = string.Empty;
        private string _cachedStatusMessage = string.Empty;
        private long _revision;

        public RuntimeDesktopBridgeSession(
            CortexShellState shellState,
            Func<ICortexSettingsStore> settingsStoreAccessor,
            Func<IProjectCatalog> projectCatalogAccessor)
        {
            _shellState = shellState ?? new CortexShellState();
            _settingsStoreAccessor = settingsStoreAccessor;
            _projectCatalogAccessor = projectCatalogAccessor;
            _catalog = WorkbenchCatalogFactory.CreateDefaultCatalog();
            _shellSettings = new ShellSettings();
            _onboardingState = new OnboardingState();
            _settingsDocument = new SettingsDocumentModel();
            _settingsSessionState = new SettingsSessionState();
            _settingsDraftState = new SettingsDraftState();
        }

        public long Revision
        {
            get { return _revision; }
        }

        public void Initialize()
        {
            ApplyRuntimeSettings(resetDraft: true);
            LoadProjectsFromCatalog();
            RefreshSettingsProjection();
            RefreshWorkspaceTree();
            _statusMessage = ResolveRuntimeStatusMessage("Legacy runtime bridge ready.");
            CacheRuntimeMirror();
            Touch();
        }

        public bool SynchronizeFromRuntime()
        {
            var changed = false;
            var currentSettingsFingerprint = BuildSettingsFingerprint(_shellState.Settings);
            if (!string.Equals(_cachedSettingsFingerprint, currentSettingsFingerprint, StringComparison.Ordinal))
            {
                ApplyRuntimeSettings(resetDraft: true);
                RefreshSettingsProjection();
                RefreshWorkspaceTree();
                changed = true;
            }

            LoadProjectsFromCatalog();

            var currentSelectedProjectId = ResolveSelectedProjectId(_shellState.SelectedProject);
            if (!string.Equals(_cachedSelectedProjectId, currentSelectedProjectId, StringComparison.Ordinal))
            {
                SelectProjectById(currentSelectedProjectId, false);
                changed = true;
            }

            var currentStatus = ResolveRuntimeStatusMessage(_statusMessage);
            if (!string.Equals(_cachedStatusMessage, currentStatus, StringComparison.Ordinal))
            {
                _statusMessage = currentStatus;
                changed = true;
            }

            if (changed)
            {
                CacheRuntimeMirror();
                Touch();
            }

            return changed;
        }

        public BridgeOperationResultMessage ApplyIntent(BridgeIntentMessage intent)
        {
            var result = new BridgeOperationResultMessage
            {
                RequestId = intent != null ? intent.RequestId ?? string.Empty : string.Empty,
                IntentType = intent != null ? intent.IntentType : BridgeIntentType.SaveSettings,
                Status = BridgeOperationStatus.Completed,
                StatusMessage = string.Empty
            };
            if (intent == null)
            {
                result.Status = BridgeOperationStatus.Rejected;
                result.StatusMessage = "Intent payload was missing.";
                return result;
            }

            switch (intent.IntentType)
            {
                case BridgeIntentType.SelectOnboardingProfile:
                    SelectOnboardingProfile(intent.ProfileId);
                    result.StatusMessage = "Selected onboarding profile.";
                    break;
                case BridgeIntentType.SelectOnboardingLayout:
                    SelectOnboardingLayout(intent.LayoutPresetId);
                    result.StatusMessage = "Selected onboarding layout.";
                    break;
                case BridgeIntentType.SelectOnboardingTheme:
                    SelectOnboardingTheme(intent.ThemeId);
                    result.StatusMessage = "Selected onboarding theme.";
                    break;
                case BridgeIntentType.SetOnboardingWorkspaceRoot:
                    SetOnboardingWorkspaceRoot(intent.WorkspaceRootPath);
                    result.StatusMessage = "Updated onboarding workspace root.";
                    break;
                case BridgeIntentType.ApplyOnboarding:
                    ApplyOnboarding();
                    result.StatusMessage = _statusMessage;
                    break;
                case BridgeIntentType.SetWorkspaceRoot:
                    SetWorkspaceRoot(intent.WorkspaceRootPath);
                    result.StatusMessage = _statusMessage;
                    break;
                case BridgeIntentType.SaveSettings:
                    SaveSettings();
                    result.StatusMessage = _statusMessage;
                    break;
                case BridgeIntentType.SelectSettingsSection:
                    SelectSettingsSection(intent.SectionId);
                    result.StatusMessage = "Selected settings section.";
                    break;
                case BridgeIntentType.SelectSetting:
                    SelectSetting(intent.SettingId);
                    result.StatusMessage = "Selected setting.";
                    break;
                case BridgeIntentType.SetSettingValue:
                    SetSettingValue(intent.SettingId, intent.SettingValue);
                    result.StatusMessage = "Updated setting draft value.";
                    break;
                case BridgeIntentType.SetSettingsSearchQuery:
                    SetSettingsSearchQuery(intent.SearchQuery);
                    result.StatusMessage = "Updated settings search.";
                    break;
                case BridgeIntentType.AnalyzeWorkspace:
                    AnalyzeWorkspace();
                    result.StatusMessage = _statusMessage;
                    break;
                case BridgeIntentType.ImportWorkspace:
                    ImportWorkspace();
                    result.StatusMessage = _statusMessage;
                    break;
                case BridgeIntentType.SelectProject:
                    SelectProjectById(intent.ProjectId, true);
                    result.StatusMessage = _statusMessage;
                    break;
                case BridgeIntentType.OpenFilePreview:
                    OpenFilePreview(intent.FilePath);
                    result.StatusMessage = _statusMessage;
                    break;
                default:
                    result.Status = BridgeOperationStatus.Rejected;
                    result.StatusMessage = "Unsupported bridge intent.";
                    return result;
            }

            CacheRuntimeMirror();
            Touch();
            return result;
        }

        public WorkbenchBridgeSnapshot BuildSnapshot()
        {
            return new WorkbenchBridgeSnapshot
            {
                WorkbenchId = "default",
                ActiveLayoutPresetId = ResolveActiveLayoutPresetId(),
                StatusMessage = _statusMessage,
                RuntimeConnectionState = "connected",
                Catalog = _catalog,
                Onboarding = CloneOnboardingState(_onboardingState),
                OnboardingFlow = CloneOnboardingFlow(_onboardingService.BuildFlow(_onboardingState, _catalog)),
                ThemePreviewSummary = BuildThemePreviewSummary(),
                Settings = BuildSettingsSnapshot(),
                Workspace = BuildWorkspaceSnapshot()
            };
        }

        private SettingsBridgeSnapshot BuildSettingsSnapshot()
        {
            var snapshot = new SettingsBridgeSnapshot
            {
                CurrentSettings = CloneShellSettings(_shellSettings),
                Document = CloneSettingsDocument(_settingsDocument),
                SelectedSectionId = _selectedSectionId,
                SelectedSettingId = _selectedSettingId,
                SearchQuery = _settingsSessionState.SearchQuery,
                ShowModifiedOnly = _settingsSessionState.ShowModifiedOnly
            };

            snapshot.VisibleSections.AddRange(_visibleSettingsSections.Select(CloneSettingsSection));
            snapshot.ActiveSettings.AddRange(_activeSettings.Select(CloneSettingDescriptor));
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

        private WorkspaceBridgeSnapshot BuildWorkspaceSnapshot()
        {
            var snapshot = new WorkspaceBridgeSnapshot
            {
                WorkspaceRootPath = _shellSettings.WorkspaceRootPath ?? string.Empty,
                SelectedProjectId = _selectedProject != null ? _selectedProject.ProjectId ?? string.Empty : string.Empty,
                WorkspaceTreeRoot = CloneWorkspaceFileNode(_workspaceTreeRoot),
                PreviewFilePath = _previewFilePath ?? string.Empty,
                PreviewText = _previewText ?? string.Empty
            };

            snapshot.Projects.AddRange(_projects.Select(CloneProjectDefinition));
            return snapshot;
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
            RefreshWorkspaceTree();
            _statusMessage = "Saved legacy runtime settings.";
        }

        private void ApplyOnboarding()
        {
            _shellSettings = _onboardingService.Apply(_onboardingState, _catalog, _shellSettings);
            ApplyShellSettingsToRuntime();
            PersistSettings();
            _settingsDraftService.Initialize(_settingsDraftState, _catalog, _shellSettings);
            RefreshSettingsProjection();
            RefreshWorkspaceTree();
            _statusMessage = "Applied onboarding defaults.";
        }

        private void SetWorkspaceRoot(string workspaceRootPath)
        {
            _shellSettings.WorkspaceRootPath = workspaceRootPath ?? string.Empty;
            _onboardingState.SelectedWorkspaceRootPath = _shellSettings.WorkspaceRootPath;
            _settingsDraftState.Values[nameof(ShellSettings.WorkspaceRootPath)] = _shellSettings.WorkspaceRootPath;
            RefreshWorkspaceTree();
            _statusMessage = "Updated workspace root.";
        }

        private void SetOnboardingWorkspaceRoot(string workspaceRootPath)
        {
            _onboardingState.SelectedWorkspaceRootPath = workspaceRootPath ?? string.Empty;
            _statusMessage = "Updated onboarding workspace root.";
        }

        private void SelectOnboardingProfile(string profileId)
        {
            _onboardingState.SelectedProfileId = profileId ?? string.Empty;
            _statusMessage = "Updated onboarding profile selection.";
        }

        private void SelectOnboardingLayout(string layoutPresetId)
        {
            _onboardingState.SelectedLayoutPresetId = layoutPresetId ?? string.Empty;
            _statusMessage = "Updated onboarding layout selection.";
        }

        private void SelectOnboardingTheme(string themeId)
        {
            _onboardingState.SelectedThemeId = themeId ?? string.Empty;
            _statusMessage = "Updated onboarding theme selection.";
        }

        private void SetSettingsSearchQuery(string searchQuery)
        {
            _settingsSessionState.SearchQuery = searchQuery ?? string.Empty;
            _settingsSessionState.AppliedSearchQuery = _settingsSessionState.SearchQuery;
            RefreshSettingsProjection();
        }

        private void SelectSettingsSection(string sectionId)
        {
            _settingsSessionState.ActiveSectionId = sectionId ?? string.Empty;
            RefreshSettingsProjection();
        }

        private void SelectSetting(string settingId)
        {
            _selectedSettingId = settingId ?? string.Empty;
            EnsureSelectedSetting();
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
            _statusMessage = "Updated setting draft value.";
        }

        private void AnalyzeWorkspace()
        {
            var analysis = _projectWorkspaceService.AnalyzeSourceRoot(_shellSettings.WorkspaceRootPath, _selectedProject != null ? _selectedProject.ProjectId : string.Empty);
            _projects.Clear();
            if (analysis.Success && analysis.Definition != null)
            {
                _projects.Add(CloneProjectDefinition(analysis.Definition));
                _selectedProject = _projects[0];
                UpsertProjectCatalog(_selectedProject);
                ApplySelectedProjectToRuntime(_selectedProject);
            }

            RefreshWorkspaceTree();
            _statusMessage = analysis.StatusMessage ?? "Analyzed workspace root.";
        }

        private void ImportWorkspace()
        {
            var result = _projectWorkspaceService.DiscoverWorkspaceProjects(_shellSettings.WorkspaceRootPath);
            _projects.Clear();
            foreach (var definition in result.Definitions.OrderBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var cloned = CloneProjectDefinition(definition);
                _projects.Add(cloned);
                UpsertProjectCatalog(cloned);
            }

            _selectedProject = _projects.Count > 0 ? _projects[0] : null;
            ApplySelectedProjectToRuntime(_selectedProject);
            RefreshWorkspaceTree();
            _statusMessage = result.StatusMessage ?? "Imported workspace projects.";
        }

        private void SelectProjectById(string projectId, bool updateStatusMessage)
        {
            _selectedProject = _projects.FirstOrDefault(project => string.Equals(project.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                ?? (_projects.Count > 0 ? _projects[0] : null);
            ApplySelectedProjectToRuntime(_selectedProject);
            RefreshWorkspaceTree();
            if (updateStatusMessage)
            {
                _statusMessage = _selectedProject != null
                    ? "Selected project " + (_selectedProject.DisplayName ?? _selectedProject.ProjectId ?? string.Empty) + "."
                    : "No project selected.";
            }
        }

        private void OpenFilePreview(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || Directory.Exists(filePath))
            {
                _previewFilePath = string.Empty;
                _previewText = string.Empty;
                return;
            }

            _previewFilePath = filePath;
            _previewText = _projectWorkspaceService.ReadFilePreview(filePath);
            _statusMessage = "Opened " + Path.GetFileName(filePath) + ".";
        }

        private void RefreshSettingsProjection()
        {
            _visibleSettingsSections.Clear();
            _activeSettings.Clear();

            var normalizedQuery = _settingsSearchService.NormalizeQuery(_settingsSessionState.SearchQuery);
            _visibleSettingsSections.AddRange(_settingsSearchService.GetVisibleSections(_settingsDocument, normalizedQuery));
            _selectedSectionId = _settingsSearchService.ResolveActiveSectionId(_settingsDocument, _settingsSessionState.ActiveSectionId, normalizedQuery);
            _settingsSessionState.ActiveSectionId = _selectedSectionId;

            _activeSettings.AddRange(
                _catalog.Settings
                    .Where(setting => string.Equals(setting.SectionId, _selectedSectionId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(setting => setting.SortOrder)
                    .ThenBy(setting => setting.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(CloneSettingDescriptor));
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

        private void LoadProjectsFromCatalog()
        {
            var selectedProjectId = _selectedProject != null ? _selectedProject.ProjectId ?? string.Empty : ResolveSelectedProjectId(_shellState.SelectedProject);
            _projects.Clear();

            var catalog = _projectCatalogAccessor != null ? _projectCatalogAccessor() : null;
            var projects = catalog != null ? catalog.GetProjects() : new List<CortexProjectDefinition>();
            foreach (var project in projects.OrderBy(definition => definition != null ? definition.GetDisplayName() : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (project == null)
                {
                    continue;
                }

                _projects.Add(new WorkspaceProjectDefinition
                {
                    ProjectId = project.ModId ?? string.Empty,
                    DisplayName = project.GetDisplayName(),
                    SourceRootPath = project.SourceRootPath ?? string.Empty,
                    ProjectFilePath = project.ProjectFilePath ?? string.Empty
                });
            }

            _selectedProject = _projects.FirstOrDefault(project => string.Equals(project.ProjectId, selectedProjectId, StringComparison.OrdinalIgnoreCase))
                ?? (_projects.Count > 0 ? _projects[0] : null);
        }

        private void RefreshWorkspaceTree()
        {
            var rootPath = _selectedProject != null && !string.IsNullOrEmpty(_selectedProject.SourceRootPath)
                ? _selectedProject.SourceRootPath
                : _shellSettings.WorkspaceRootPath;
            _workspaceTreeRoot = _projectWorkspaceService.BuildWorkspaceTree(rootPath);
            if (!string.IsNullOrEmpty(_previewFilePath) && !File.Exists(_previewFilePath))
            {
                _previewFilePath = string.Empty;
                _previewText = string.Empty;
            }
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
            _shellState.StatusMessage = _statusMessage;
        }

        private void PersistSettings()
        {
            var settingsStore = _settingsStoreAccessor != null ? _settingsStoreAccessor() : null;
            settingsStore?.Save(_shellState.Settings);
        }

        private void ApplySelectedProjectToRuntime(WorkspaceProjectDefinition project)
        {
            _shellState.SelectedProject = project == null
                ? null
                : new CortexProjectDefinition
                {
                    ModId = project.ProjectId ?? string.Empty,
                    SourceRootPath = project.SourceRootPath ?? string.Empty,
                    ProjectFilePath = project.ProjectFilePath ?? string.Empty
                };
        }

        private void UpsertProjectCatalog(WorkspaceProjectDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            var projectCatalog = _projectCatalogAccessor != null ? _projectCatalogAccessor() : null;
            projectCatalog?.Upsert(new CortexProjectDefinition
            {
                ModId = definition.ProjectId ?? string.Empty,
                SourceRootPath = definition.SourceRootPath ?? string.Empty,
                ProjectFilePath = definition.ProjectFilePath ?? string.Empty
            });
        }

        private void CacheRuntimeMirror()
        {
            _cachedSettingsFingerprint = BuildSettingsFingerprint(_shellState.Settings);
            _cachedSelectedProjectId = ResolveSelectedProjectId(_shellState.SelectedProject);
            _cachedStatusMessage = ResolveRuntimeStatusMessage(_statusMessage);
        }

        private string ResolveRuntimeStatusMessage(string fallback)
        {
            return !string.IsNullOrEmpty(_shellState.StatusMessage)
                ? _shellState.StatusMessage
                : fallback ?? string.Empty;
        }

        private string ResolveActiveLayoutPresetId()
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

        private string BuildThemePreviewSummary()
        {
            var selection = _onboardingService.ResolveSelection(_onboardingState, _shellSettings, _catalog);
            return selection.Theme != null
                ? (selection.Theme.DisplayName ?? string.Empty) + " | " + (selection.Theme.Description ?? string.Empty)
                : "No theme selected.";
        }

        private static string ResolveSelectedProjectId(CortexProjectDefinition definition)
        {
            return definition != null ? definition.ModId ?? string.Empty : string.Empty;
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

        private static ShellSettings CloneShellSettings(ShellSettings settings)
        {
            return new ShellSettings
            {
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty,
                RuntimeContentRootPath = settings != null ? settings.RuntimeContentRootPath ?? string.Empty : string.Empty,
                ReferenceAssemblyRootPath = settings != null ? settings.ReferenceAssemblyRootPath ?? string.Empty : string.Empty,
                AdditionalSourceRoots = settings != null ? settings.AdditionalSourceRoots ?? string.Empty : string.Empty,
                ThemeId = settings != null ? settings.ThemeId ?? string.Empty : string.Empty,
                DefaultOnboardingProfileId = settings != null ? settings.DefaultOnboardingProfileId ?? string.Empty : string.Empty,
                DefaultOnboardingLayoutPresetId = settings != null ? settings.DefaultOnboardingLayoutPresetId ?? string.Empty : string.Empty,
                DefaultOnboardingThemeId = settings != null ? settings.DefaultOnboardingThemeId ?? string.Empty : string.Empty,
                DefaultBuildConfiguration = settings != null ? settings.DefaultBuildConfiguration ?? string.Empty : string.Empty,
                BuildTimeoutMs = settings != null ? settings.BuildTimeoutMs : 0,
                EnableFileEditing = settings != null && settings.EnableFileEditing,
                EnableFileSaving = settings != null && settings.EnableFileSaving,
                EditorUndoHistoryLimit = settings != null ? settings.EditorUndoHistoryLimit : 0,
                SettingsActiveSectionId = settings != null ? settings.SettingsActiveSectionId ?? string.Empty : string.Empty,
                SettingsSearchQuery = settings != null ? settings.SettingsSearchQuery ?? string.Empty : string.Empty,
                SettingsShowModifiedOnly = settings != null && settings.SettingsShowModifiedOnly,
                HasCompletedOnboarding = settings != null && settings.HasCompletedOnboarding
            };
        }

        private static OnboardingState CloneOnboardingState(OnboardingState onboarding)
        {
            return new OnboardingState
            {
                SelectedProfileId = onboarding != null ? onboarding.SelectedProfileId ?? string.Empty : string.Empty,
                SelectedLayoutPresetId = onboarding != null ? onboarding.SelectedLayoutPresetId ?? string.Empty : string.Empty,
                SelectedThemeId = onboarding != null ? onboarding.SelectedThemeId ?? string.Empty : string.Empty,
                SelectedWorkspaceRootPath = onboarding != null ? onboarding.SelectedWorkspaceRootPath ?? string.Empty : string.Empty,
                ActiveStepIndex = onboarding != null ? onboarding.ActiveStepIndex : 0
            };
        }

        private static OnboardingFlowModel CloneOnboardingFlow(OnboardingFlowModel flow)
        {
            var clone = new OnboardingFlowModel
            {
                ActiveStepIndex = flow != null ? flow.ActiveStepIndex : 0
            };
            if (flow != null)
            {
                foreach (var step in flow.Steps)
                {
                    clone.Steps.Add(new OnboardingStepModel
                    {
                        StepId = step != null ? step.StepId ?? string.Empty : string.Empty,
                        Title = step != null ? step.Title ?? string.Empty : string.Empty,
                        Description = step != null ? step.Description ?? string.Empty : string.Empty
                    });
                }
            }

            return clone;
        }

        private static SettingsDocumentModel CloneSettingsDocument(SettingsDocumentModel document)
        {
            var clone = new SettingsDocumentModel();
            if (document == null)
            {
                return clone;
            }

            clone.Sections.AddRange(document.Sections.Select(CloneSettingsSection));
            clone.Groups.AddRange(document.Groups.Select(CloneSettingsGroup));
            return clone;
        }

        private static SettingsNavigationGroupModel CloneSettingsGroup(SettingsNavigationGroupModel group)
        {
            var clone = new SettingsNavigationGroupModel
            {
                GroupId = group != null ? group.GroupId ?? string.Empty : string.Empty,
                Title = group != null ? group.Title ?? string.Empty : string.Empty,
                SortOrder = group != null ? group.SortOrder : 0
            };
            if (group != null)
            {
                clone.Sections.AddRange(group.Sections.Select(CloneSettingsSection));
            }

            return clone;
        }

        private static SettingsSectionModel CloneSettingsSection(SettingsSectionModel section)
        {
            return new SettingsSectionModel
            {
                SectionId = section != null ? section.SectionId ?? string.Empty : string.Empty,
                GroupId = section != null ? section.GroupId ?? string.Empty : string.Empty,
                GroupTitle = section != null ? section.GroupTitle ?? string.Empty : string.Empty,
                Scope = section != null ? section.Scope ?? string.Empty : string.Empty,
                Title = section != null ? section.Title ?? string.Empty : string.Empty,
                Description = section != null ? section.Description ?? string.Empty : string.Empty,
                SearchText = section != null ? section.SearchText ?? string.Empty : string.Empty,
                SortOrder = section != null ? section.SortOrder : 0
            };
        }

        private static SettingDescriptor CloneSettingDescriptor(SettingDescriptor setting)
        {
            if (setting == null)
            {
                return new SettingDescriptor();
            }

            return new SettingDescriptor
            {
                SettingId = setting.SettingId ?? string.Empty,
                SectionId = setting.SectionId ?? string.Empty,
                DisplayName = setting.DisplayName ?? string.Empty,
                Description = setting.Description ?? string.Empty,
                Scope = setting.Scope ?? string.Empty,
                DefaultValue = setting.DefaultValue ?? string.Empty,
                ValueKind = setting.ValueKind,
                EditorKind = setting.EditorKind,
                PlaceholderText = setting.PlaceholderText ?? string.Empty,
                HelpText = setting.HelpText ?? string.Empty,
                Keywords = setting.Keywords != null ? setting.Keywords.ToArray() : new string[0],
                Options = setting.Options != null
                    ? setting.Options.Select(option => new SettingChoiceDescriptor
                    {
                        Value = option != null ? option.Value ?? string.Empty : string.Empty,
                        DisplayName = option != null ? option.DisplayName ?? string.Empty : string.Empty,
                        Description = option != null ? option.Description ?? string.Empty : string.Empty
                    }).ToArray()
                    : new SettingChoiceDescriptor[0],
                IsRequired = setting.IsRequired,
                IsSecret = setting.IsSecret,
                SortOrder = setting.SortOrder
            };
        }

        private static WorkspaceProjectDefinition CloneProjectDefinition(WorkspaceProjectDefinition definition)
        {
            return new WorkspaceProjectDefinition
            {
                ProjectId = definition != null ? definition.ProjectId ?? string.Empty : string.Empty,
                DisplayName = definition != null ? definition.DisplayName ?? string.Empty : string.Empty,
                SourceRootPath = definition != null ? definition.SourceRootPath ?? string.Empty : string.Empty,
                ProjectFilePath = definition != null ? definition.ProjectFilePath ?? string.Empty : string.Empty
            };
        }

        private static WorkspaceFileNode CloneWorkspaceFileNode(WorkspaceFileNode node)
        {
            if (node == null)
            {
                return null;
            }

            var clone = new WorkspaceFileNode
            {
                Name = node.Name ?? string.Empty,
                FullPath = node.FullPath ?? string.Empty,
                IsDirectory = node.IsDirectory
            };
            foreach (var child in node.Children)
            {
                clone.Children.Add(CloneWorkspaceFileNode(child));
            }

            return clone;
        }

        private void Touch()
        {
            _revision++;
        }
    }
}
