using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using Cortex.Bridge;
using Cortex.Host.Avalonia.Bridge;
using Cortex.Host.Avalonia.Composition;
using Cortex.Host.Avalonia.Models;
using Cortex.Shell.Shared.Models;

namespace Cortex.Host.Avalonia.ViewModels
{
    internal sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly NamedPipeDesktopBridgeClient _bridgeClient;
        private readonly DesktopHostOptions _hostOptions;
        private WorkbenchBridgeSnapshot _snapshot = new WorkbenchBridgeSnapshot();
        private SettingsBridgeSnapshot _settingsSnapshot = new SettingsBridgeSnapshot();
        private WorkspaceBridgeSnapshot _workspaceSnapshot = new WorkspaceBridgeSnapshot();
        private EditorWorkbenchModel _editorSnapshot = new EditorWorkbenchModel();
        private SearchWorkbenchModel _searchSnapshot = new SearchWorkbenchModel();
        private ReferenceWorkbenchModel _referenceSnapshot = new ReferenceWorkbenchModel();
        private string _connectionStatusMessage = "Waiting for legacy runtime bridge...";
        private string _runtimeStatusMessage = "No runtime snapshot received yet.";
        private string _selectedSettingValue = string.Empty;
        private string _workbenchSearchQuery = string.Empty;

        public MainWindowViewModel(NamedPipeDesktopBridgeClient bridgeClient, DesktopHostOptions hostOptions)
        {
            _bridgeClient = bridgeClient;
            _hostOptions = hostOptions ?? new DesktopHostOptions();
            Projects = new ObservableCollection<WorkspaceProjectDefinition>();
            OpenEditorDocuments = new ObservableCollection<EditorDocumentSummaryModel>();
            WorkspaceTree = new ObservableCollection<WorkspaceFileNodeViewModel>();
            VisibleSettingsSections = new ObservableCollection<SettingsSectionModel>();
            ActiveSettings = new ObservableCollection<SettingDescriptor>();
            ThemeOptions = new ObservableCollection<ThemeDescriptor>();
            OnboardingProfiles = new ObservableCollection<OnboardingProfileDescriptor>();
            OnboardingLayouts = new ObservableCollection<OnboardingLayoutDescriptor>();
            SearchMatches = new ObservableCollection<SearchMatchItemViewModel>();

            _bridgeClient.ConnectionStatusChanged += status => Dispatcher.UIThread.Post(() => ConnectionStatusMessage = status);
            _bridgeClient.OperationStatusReceived += status => Dispatcher.UIThread.Post(() =>
            {
                if (!string.IsNullOrEmpty(status))
                {
                    RuntimeStatusMessage = status;
                }
            });
            _bridgeClient.SnapshotReceived += snapshot => Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));
            _bridgeClient.Start();
        }

        public ObservableCollection<WorkspaceProjectDefinition> Projects { get; }
        public ObservableCollection<EditorDocumentSummaryModel> OpenEditorDocuments { get; }
        public ObservableCollection<WorkspaceFileNodeViewModel> WorkspaceTree { get; }
        public ObservableCollection<SettingsSectionModel> VisibleSettingsSections { get; }
        public ObservableCollection<SettingDescriptor> ActiveSettings { get; }
        public ObservableCollection<ThemeDescriptor> ThemeOptions { get; }
        public ObservableCollection<OnboardingProfileDescriptor> OnboardingProfiles { get; }
        public ObservableCollection<OnboardingLayoutDescriptor> OnboardingLayouts { get; }
        public ObservableCollection<SearchMatchItemViewModel> SearchMatches { get; }

        public ShellSettings Settings
        {
            get { return _settingsSnapshot.CurrentSettings ?? new ShellSettings(); }
        }

        public OnboardingState Onboarding
        {
            get { return _snapshot.Onboarding ?? new OnboardingState(); }
        }

        public OnboardingFlowModel OnboardingFlow
        {
            get { return _snapshot.OnboardingFlow ?? new OnboardingFlowModel(); }
        }

        public OnboardingProfileDescriptor SelectedOnboardingProfile
        {
            get
            {
                return OnboardingProfiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, Onboarding.SelectedProfileId, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (value != null)
                {
                    SelectOnboardingProfile(value);
                }
            }
        }

        public OnboardingLayoutDescriptor SelectedOnboardingLayout
        {
            get
            {
                return OnboardingLayouts.FirstOrDefault(layout => string.Equals(layout.LayoutPresetId, Onboarding.SelectedLayoutPresetId, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (value != null)
                {
                    SelectOnboardingLayout(value);
                }
            }
        }

        public ThemeDescriptor SelectedOnboardingTheme
        {
            get
            {
                return ThemeOptions.FirstOrDefault(theme => string.Equals(theme.ThemeId, Onboarding.SelectedThemeId, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (value != null)
                {
                    SelectOnboardingTheme(value);
                }
            }
        }

        public WorkspaceProjectDefinition SelectedProject
        {
            get
            {
                return Projects.FirstOrDefault(project => string.Equals(project.ProjectId, _workspaceSnapshot.SelectedProjectId, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (value == null)
                {
                    return;
                }

                TrySendIntent(new BridgeIntentMessage
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    IntentType = BridgeIntentType.SelectProject,
                    ProjectId = value.ProjectId ?? string.Empty
                });
            }
        }

        public SettingsSectionModel SelectedSettingsSection
        {
            get
            {
                return VisibleSettingsSections.FirstOrDefault(section => string.Equals(section.SectionId, _settingsSnapshot.SelectedSectionId, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (value == null)
                {
                    return;
                }

                TrySendIntent(new BridgeIntentMessage
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    IntentType = BridgeIntentType.SelectSettingsSection,
                    SectionId = value.SectionId ?? string.Empty
                });
            }
        }

        public SettingDescriptor SelectedSetting
        {
            get
            {
                return ActiveSettings.FirstOrDefault(setting => string.Equals(setting.SettingId, _settingsSnapshot.SelectedSettingId, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (value == null)
                {
                    return;
                }

                TrySendIntent(new BridgeIntentMessage
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    IntentType = BridgeIntentType.SelectSetting,
                    SettingId = value.SettingId ?? string.Empty
                });
            }
        }

        public string SelectedSettingValue
        {
            get { return _selectedSettingValue; }
            set
            {
                if (string.Equals(_selectedSettingValue, value ?? string.Empty, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedSettingValue = value ?? string.Empty;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsSelectedSettingModified));
            }
        }

        public string SettingsSearchQuery
        {
            get { return _settingsSnapshot.SearchQuery ?? string.Empty; }
            set
            {
                if (string.Equals(_settingsSnapshot.SearchQuery ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal))
                {
                    return;
                }

                SetSettingsSearchQuery(value);
            }
        }

        public string FilePreviewText
        {
            get { return _workspaceSnapshot.PreviewText ?? string.Empty; }
        }

        public string WorkbenchSearchQuery
        {
            get { return _workbenchSearchQuery; }
            set
            {
                var normalized = value ?? string.Empty;
                if (string.Equals(_workbenchSearchQuery, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _workbenchSearchQuery = normalized;
                RaisePropertyChanged();
            }
        }

        public string PreviewFilePath
        {
            get { return _workspaceSnapshot.PreviewFilePath ?? string.Empty; }
        }

        public string ConnectionStatusMessage
        {
            get { return _connectionStatusMessage; }
            private set
            {
                _connectionStatusMessage = value ?? string.Empty;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusMessage));
            }
        }

        public string RuntimeStatusMessage
        {
            get { return _runtimeStatusMessage; }
            private set
            {
                _runtimeStatusMessage = value ?? string.Empty;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusMessage));
            }
        }

        public string StatusMessage
        {
            get { return ConnectionStatusMessage + " | " + RuntimeStatusMessage; }
        }

        public string DesktopStartupSummary
        {
            get { return _hostOptions.StartupModeSummary ?? string.Empty; }
        }

        public string DesktopBundleProfileName
        {
            get { return _hostOptions.BundleProfileName ?? string.Empty; }
        }

        public string DesktopBundleRootPath
        {
            get
            {
                return _hostOptions.EnvironmentPaths != null
                    ? _hostOptions.EnvironmentPaths.BundleRootPath ?? string.Empty
                    : string.Empty;
            }
        }

        public string DesktopBundledPluginSummary
        {
            get
            {
                return _hostOptions.EnvironmentPaths != null
                    ? _hostOptions.EnvironmentPaths.BundledPluginSummary ?? string.Empty
                    : string.Empty;
            }
        }

        public string DesktopBundledToolSummary
        {
            get
            {
                return _hostOptions.EnvironmentPaths != null
                    ? _hostOptions.EnvironmentPaths.BundledToolSummary ?? string.Empty
                    : string.Empty;
            }
        }

        public string SelectedSettingDisplayName
        {
            get { return SelectedSetting != null ? SelectedSetting.DisplayName : "Select a setting"; }
        }

        public bool HasSelectedSettingChoices
        {
            get { return SelectedSetting != null && SelectedSetting.Options != null && SelectedSetting.Options.Length > 0; }
        }

        public IEnumerable<SettingChoiceDescriptor> SelectedSettingChoices
        {
            get { return SelectedSetting != null && SelectedSetting.Options != null ? SelectedSetting.Options : Array.Empty<SettingChoiceDescriptor>(); }
        }

        public SettingChoiceDescriptor SelectedSettingChoice
        {
            get
            {
                return SelectedSettingChoices.FirstOrDefault(choice => string.Equals(choice.Value, SelectedSettingValue, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (value != null)
                {
                    SelectedSettingValue = value.Value ?? string.Empty;
                    CommitSelectedSettingValue();
                }
            }
        }

        public string SelectedSettingPlaceholder
        {
            get { return SelectedSetting != null ? SelectedSetting.PlaceholderText ?? string.Empty : string.Empty; }
        }

        public string SelectedSettingDescription
        {
            get { return SelectedSetting != null ? SelectedSetting.Description ?? string.Empty : "Choose a setting from the section list to inspect or edit it."; }
        }

        public string SelectedSettingHelpText
        {
            get { return SelectedSetting != null ? SelectedSetting.HelpText ?? string.Empty : string.Empty; }
        }

        public bool IsSelectedSettingModified
        {
            get
            {
                var selected = SelectedSetting;
                return selected != null &&
                    !string.Equals(GetDraftValue(selected.SettingId), ReadLoadedValue(selected.SettingId), StringComparison.Ordinal);
            }
        }

        public string ThemePreviewSummary
        {
            get { return _snapshot.ThemePreviewSummary ?? string.Empty; }
        }

        public string ActiveEditorDisplayName
        {
            get { return _editorSnapshot.ActiveDocumentDisplayName ?? string.Empty; }
        }

        public string ActiveEditorCompactPath
        {
            get { return _editorSnapshot.CompactPath ?? string.Empty; }
        }

        public string ActiveEditorStatusSummary
        {
            get
            {
                var language = _editorSnapshot.LanguageStatusLabel ?? string.Empty;
                var completion = _editorSnapshot.CompletionStatusLabel ?? string.Empty;
                if (string.IsNullOrEmpty(language))
                {
                    return completion;
                }

                if (string.IsNullOrEmpty(completion))
                {
                    return language;
                }

                return language + " | " + completion;
            }
        }

        public string ActiveEditorMetricsSummary
        {
            get
            {
                return "Line " + _editorSnapshot.CaretLine +
                    ", Column " + _editorSnapshot.CaretColumn +
                    " | " + _editorSnapshot.LineCount + " lines" +
                    (_editorSnapshot.IsDirty ? " | Modified" : string.Empty) +
                    (_editorSnapshot.AllowSaving ? " | Save enabled" : " | Save disabled");
            }
        }

        public string SearchTitle
        {
            get { return _searchSnapshot.Title ?? string.Empty; }
        }

        public string SearchStatusSummary
        {
            get { return _searchSnapshot.StatusMessage ?? string.Empty; }
        }

        public string SearchScopeCaption
        {
            get { return _searchSnapshot.ScopeCaption ?? string.Empty; }
        }

        public string SearchMatchCountSummary
        {
            get
            {
                var total = _searchSnapshot.TotalMatchCount;
                if (total <= 0)
                {
                    return "No matches.";
                }

                return total + " match(es)";
            }
        }

        public string ReferenceStatusSummary
        {
            get { return _referenceSnapshot.StatusMessage ?? string.Empty; }
        }

        public string ReferenceTargetDisplayName
        {
            get { return _referenceSnapshot.ResolvedTargetDisplayName ?? string.Empty; }
        }

        public string ReferenceCachePath
        {
            get { return _referenceSnapshot.CachePath ?? string.Empty; }
        }

        public string ReferenceDocumentationPath
        {
            get { return _referenceSnapshot.XmlDocumentationPath ?? string.Empty; }
        }

        public string ReferenceDocumentationText
        {
            get { return _referenceSnapshot.XmlDocumentationText ?? string.Empty; }
        }

        public string ReferenceSourceText
        {
            get { return _referenceSnapshot.SourceText ?? string.Empty; }
        }

        public string BuildDefaultsSummary
        {
            get
            {
                return "Build " + Settings.DefaultBuildConfiguration +
                    " | Timeout " + Settings.BuildTimeoutMs + " ms" +
                    (Settings.EnableFileEditing ? " | Edit enabled" : " | Edit disabled") +
                    (Settings.EnableFileSaving ? " | Save enabled" : " | Save disabled");
            }
        }

        public string ActiveWorkbenchLayoutPresetId
        {
            get { return _snapshot.ActiveLayoutPresetId ?? string.Empty; }
        }

        public void SaveSettings()
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SaveSettings
            });
        }

        public void ApplyOnboarding()
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.ApplyOnboarding
            });
        }

        public void SetWorkspaceRoot(string path)
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SetWorkspaceRoot,
                WorkspaceRootPath = path ?? string.Empty
            });
        }

        public void AnalyzeWorkspaceRoot()
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.AnalyzeWorkspace
            });
        }

        public void ImportWorkspaceProjects()
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.ImportWorkspace
            });
        }

        public void LoadFilePreview(string filePath)
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.OpenFilePreview,
                FilePath = filePath ?? string.Empty
            });
        }

        public void UpdateWorkbenchSearch()
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.UpdateSearch,
                SearchQuery = WorkbenchSearchQuery ?? string.Empty,
                SearchScope = _searchSnapshot.Query != null ? _searchSnapshot.Query.Scope : WorkbenchSearchScope.CurrentDocument,
                MatchCase = _searchSnapshot.Query != null && _searchSnapshot.Query.MatchCase,
                WholeWord = _searchSnapshot.Query != null && _searchSnapshot.Query.WholeWord
            });
        }

        public void OpenSearchResult(SearchMatchItemViewModel result)
        {
            if (result == null)
            {
                return;
            }

            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.OpenSearchResult,
                ResultIndex = result.ResultIndex
            });
        }

        public void SelectOnboardingProfile(OnboardingProfileDescriptor selected)
        {
            if (selected == null)
            {
                return;
            }

            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SelectOnboardingProfile,
                ProfileId = selected.ProfileId ?? string.Empty
            });
        }

        public void SelectOnboardingLayout(OnboardingLayoutDescriptor selected)
        {
            if (selected == null)
            {
                return;
            }

            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SelectOnboardingLayout,
                LayoutPresetId = selected.LayoutPresetId ?? string.Empty
            });
        }

        public void SelectOnboardingTheme(ThemeDescriptor selected)
        {
            if (selected == null)
            {
                return;
            }

            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SelectOnboardingTheme,
                ThemeId = selected.ThemeId ?? string.Empty
            });
        }

        public void SetOnboardingWorkspaceRoot(string path)
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SetOnboardingWorkspaceRoot,
                WorkspaceRootPath = path ?? string.Empty
            });
        }

        public void SetSettingsSearchQuery(string query)
        {
            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SetSettingsSearchQuery,
                SearchQuery = query ?? string.Empty
            });
        }

        public void CommitSelectedSettingValue()
        {
            var selected = SelectedSetting;
            if (selected == null)
            {
                return;
            }

            TrySendIntent(new BridgeIntentMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                IntentType = BridgeIntentType.SetSettingValue,
                SettingId = selected.SettingId ?? string.Empty,
                SettingValue = SelectedSettingValue ?? string.Empty
            });
        }

        private void ApplySnapshot(WorkbenchBridgeSnapshot snapshot)
        {
            _snapshot = snapshot ?? new WorkbenchBridgeSnapshot();
            _settingsSnapshot = _snapshot.Settings ?? new SettingsBridgeSnapshot();
            _workspaceSnapshot = _snapshot.Workspace ?? new WorkspaceBridgeSnapshot();
            _editorSnapshot = _snapshot.Editor ?? new EditorWorkbenchModel();
            _searchSnapshot = _snapshot.Search ?? new SearchWorkbenchModel();
            _referenceSnapshot = _snapshot.Reference ?? new ReferenceWorkbenchModel();

            ReplaceCollection(ThemeOptions, _snapshot.Catalog != null ? _snapshot.Catalog.Themes : new List<ThemeDescriptor>());
            ReplaceCollection(OnboardingProfiles, _snapshot.Catalog != null ? _snapshot.Catalog.OnboardingProfiles : new List<OnboardingProfileDescriptor>());
            ReplaceCollection(OnboardingLayouts, _snapshot.Catalog != null ? _snapshot.Catalog.OnboardingLayouts : new List<OnboardingLayoutDescriptor>());
            ReplaceCollection(VisibleSettingsSections, _settingsSnapshot.VisibleSections ?? new List<SettingsSectionModel>());
            ReplaceCollection(ActiveSettings, _settingsSnapshot.ActiveSettings ?? new List<SettingDescriptor>());
            ReplaceCollection(Projects, _workspaceSnapshot.Projects ?? new List<WorkspaceProjectDefinition>());
            ReplaceCollection(OpenEditorDocuments, _editorSnapshot.OpenDocuments ?? new List<EditorDocumentSummaryModel>());
            ReplaceSearchMatches();

            WorkspaceTree.Clear();
            if (_workspaceSnapshot.WorkspaceTreeRoot != null)
            {
                WorkspaceTree.Add(new WorkspaceFileNodeViewModel(_workspaceSnapshot.WorkspaceTreeRoot));
            }

            SelectedSettingValue = GetDraftValue(_settingsSnapshot.SelectedSettingId);
            WorkbenchSearchQuery = _searchSnapshot.Query != null ? _searchSnapshot.Query.SearchText ?? string.Empty : string.Empty;
            RuntimeStatusMessage = _snapshot.StatusMessage ?? RuntimeStatusMessage;

            RaisePropertyChanged(nameof(Settings));
            RaisePropertyChanged(nameof(Onboarding));
            RaisePropertyChanged(nameof(OnboardingFlow));
            RaisePropertyChanged(nameof(SelectedOnboardingProfile));
            RaisePropertyChanged(nameof(SelectedOnboardingLayout));
            RaisePropertyChanged(nameof(SelectedOnboardingTheme));
            RaisePropertyChanged(nameof(SelectedProject));
            RaisePropertyChanged(nameof(SelectedSettingsSection));
            RaisePropertyChanged(nameof(SelectedSetting));
            RaisePropertyChanged(nameof(SettingsSearchQuery));
            RaisePropertyChanged(nameof(FilePreviewText));
            RaisePropertyChanged(nameof(PreviewFilePath));
            RaisePropertyChanged(nameof(SelectedSettingDisplayName));
            RaisePropertyChanged(nameof(HasSelectedSettingChoices));
            RaisePropertyChanged(nameof(SelectedSettingChoices));
            RaisePropertyChanged(nameof(SelectedSettingChoice));
            RaisePropertyChanged(nameof(SelectedSettingPlaceholder));
            RaisePropertyChanged(nameof(SelectedSettingDescription));
            RaisePropertyChanged(nameof(SelectedSettingHelpText));
            RaisePropertyChanged(nameof(IsSelectedSettingModified));
            RaisePropertyChanged(nameof(ThemePreviewSummary));
            RaisePropertyChanged(nameof(ActiveEditorDisplayName));
            RaisePropertyChanged(nameof(ActiveEditorCompactPath));
            RaisePropertyChanged(nameof(ActiveEditorStatusSummary));
            RaisePropertyChanged(nameof(ActiveEditorMetricsSummary));
            RaisePropertyChanged(nameof(SearchTitle));
            RaisePropertyChanged(nameof(SearchStatusSummary));
            RaisePropertyChanged(nameof(SearchScopeCaption));
            RaisePropertyChanged(nameof(SearchMatchCountSummary));
            RaisePropertyChanged(nameof(ReferenceStatusSummary));
            RaisePropertyChanged(nameof(ReferenceTargetDisplayName));
            RaisePropertyChanged(nameof(ReferenceCachePath));
            RaisePropertyChanged(nameof(ReferenceDocumentationPath));
            RaisePropertyChanged(nameof(ReferenceDocumentationText));
            RaisePropertyChanged(nameof(ReferenceSourceText));
            RaisePropertyChanged(nameof(BuildDefaultsSummary));
            RaisePropertyChanged(nameof(ActiveWorkbenchLayoutPresetId));
        }

        private bool TrySendIntent(BridgeIntentMessage intent)
        {
            if (!_bridgeClient.TrySendIntent(intent))
            {
                RuntimeStatusMessage = "Cortex runtime bridge is not connected.";
                return false;
            }

            return true;
        }

        private string GetDraftValue(string settingId)
        {
            if (_settingsSnapshot.DraftValues == null || string.IsNullOrEmpty(settingId))
            {
                return string.Empty;
            }

            var entry = _settingsSnapshot.DraftValues.FirstOrDefault(value => string.Equals(value.SettingId, settingId, StringComparison.OrdinalIgnoreCase));
            return entry != null ? entry.Value ?? string.Empty : string.Empty;
        }

        private string ReadLoadedValue(string settingId)
        {
            var currentSettings = _settingsSnapshot.CurrentSettings;
            var selected = SelectedSetting;
            if (currentSettings == null || selected == null || string.IsNullOrEmpty(settingId))
            {
                return string.Empty;
            }

            var property = typeof(ShellSettings).GetProperty(settingId);
            if (property == null)
            {
                return string.Empty;
            }

            var rawValue = property.GetValue(currentSettings);
            return rawValue != null ? rawValue.ToString() ?? string.Empty : string.Empty;
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                target.Add(item);
            }
        }

        private void ReplaceSearchMatches()
        {
            SearchMatches.Clear();
            if (_searchSnapshot.Documents == null)
            {
                return;
            }

            foreach (var document in _searchSnapshot.Documents)
            {
                if (document == null || document.Matches == null)
                {
                    continue;
                }

                foreach (var match in document.Matches)
                {
                    if (match == null)
                    {
                        continue;
                    }

                    SearchMatches.Add(new SearchMatchItemViewModel
                    {
                        ResultIndex = match.ResultIndex,
                        DisplayText = "Ln " + match.LineNumber + ", Col " + match.ColumnNumber + "  " + (match.PreviewText ?? match.LineText ?? string.Empty),
                        DocumentPath = match.DocumentPath ?? string.Empty,
                        IsActive = match.IsActive
                    });
                }
            }
        }
    }
}
