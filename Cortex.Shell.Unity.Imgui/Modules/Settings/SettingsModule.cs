using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Shell;
using UnityEngine;
using Cortex.Services.Editor.Input;
using Cortex.Services.Onboarding;
using Cortex.Services.Settings;
using Cortex.Shell.Unity.Imgui;

namespace Cortex.Modules.Settings
{
    public sealed class SettingsModule
    {
        private const string SourceSetupSectionId = "settings.sourceSetup";
        private const string WorkspaceOverviewSectionId = "settings.sourceSetup.overview";
        private const string ThemesSectionId = "settings.themes";
        private const string OnboardingSectionId = "settings.onboarding";
        private const string KeybindingsSectionId = "settings.keybindings";
        private const string EditorsSectionId = "settings.editors";
        private const string ActionsSectionId = "settings.actions";
        private const string WorkspaceRootSettingId = CortexHostPathSettings.WorkspaceRootSettingId;
        private const string RuntimeContentRootSettingId = CortexHostPathSettings.RuntimeContentRootSettingId;
        private const string ReferenceAssemblyRootSettingId = CortexHostPathSettings.ReferenceAssemblyRootSettingId;
        private const string AdditionalSourceRootsSettingId = CortexHostPathSettings.AdditionalSourceRootsSettingId;
        private const float NavigationWidth = 268f;
        private const float CompactEditorWidth = 380f;
        private const string SettingGearGlyph = "\u2699";

        private bool _loaded;
        private Vector2 _navigationScroll = Vector2.zero;
        private Vector2 _contentScroll = Vector2.zero;
        private readonly SettingsApplicationService _applicationService = new SettingsApplicationService();
        private readonly SettingsDocumentBuilder _documentBuilder = new SettingsDocumentBuilder();
        private readonly SettingsSearchService _searchService = new SettingsSearchService();
        private readonly SettingsDraftService _draftService = new SettingsDraftService();
        private readonly SettingsSessionService _sessionService = new SettingsSessionService();
        private readonly SettingsContributionCollectionService _contributionCollectionService = new SettingsContributionCollectionService();
        private readonly SettingsLoadedModLinkService _loadedModLinkService = new SettingsLoadedModLinkService();
        private readonly SettingsDraftState _draftState = new SettingsDraftState();
        private readonly SettingsSessionState _sessionState = new SettingsSessionState();
        private readonly Dictionary<string, Vector2> _multilineEditorScrolls = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        private bool _preserveContentScrollForNestedEditor;
        private readonly Dictionary<string, float> _sectionAnchors = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _navigationAnchors = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _navigationGroupExpanded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _collapsedNavigationGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _openSettingActionMenuId = string.Empty;
        private IProjectCatalog _projectCatalog;
        private IProjectWorkspaceService _workspaceService;
        private ILoadedModCatalog _loadedModCatalog;
        private IPathInteractionService _pathInteractionService;
        private WorkbenchPresentationSnapshot _snapshot;
        private CortexShellState _shellState;
        private CortexShellViewState _viewState;
        private IWorkbenchUiSurface _uiSurface;
        private readonly IEditorKeybindingService _editorKeybindingService = new EditorKeybindingService();
        private IDictionary<string, string> _textValues { get { return _draftState.TextValues; } }
        private IDictionary<string, bool> _toggleValues { get { return _draftState.ToggleValues; } }
        private IDictionary<string, string> _loadedSerializedValues { get { return _draftState.LoadedSerializedValues; } }
        private IDictionary<string, SettingValidationResult> _validationResults { get { return _draftState.ValidationResults; } }
        private IDictionary<string, string> _loadedModPathDrafts { get { return _draftState.LoadedModPathDrafts; } }
        private string _selectedThemeId { get { return _draftState.SelectedThemeId; } set { _draftState.SelectedThemeId = value ?? string.Empty; } }

        private IWorkbenchUiSurface UiSurface
        {
            get { return _uiSurface ?? NullWorkbenchUiSurface.Instance; }
        }

        internal void Draw(
            ICortexSettingsStore settingsStore,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog,
            IPathInteractionService pathInteractionService,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state,
            CortexShellViewState viewState,
            IWorkbenchUiSurface uiSurface)
        {
            _projectCatalog = projectCatalog;
            _workspaceService = workspaceService;
            _loadedModCatalog = loadedModCatalog;
            _pathInteractionService = pathInteractionService;
            _snapshot = snapshot;
            _shellState = state;
            _viewState = viewState;
            _uiSurface = uiSurface ?? NullWorkbenchUiSurface.Instance;
            EnsureLoaded(snapshot, themeState, state);

            var document = _documentBuilder.BuildDocument(snapshot);
            var renderActiveSectionId = ResolveRenderActiveSectionId(document);
            var navigationInteraction = new NavigationInteraction();

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawToolbar(settingsStore, snapshot, themeState, state);
            GUILayout.Space(6f);
            DrawSearchBar(document);
            GUILayout.Space(6f);
            HandleSearchQueryChanged(document);
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawNavigation(document, renderActiveSectionId, navigationInteraction);
            GUILayout.Space(8f);
            DrawContentSurface(settingsStore, snapshot, themeState, state, document, renderActiveSectionId);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            CommitNavigationInteraction(navigationInteraction);
            FinalizeNavigationState(document);
        }

        private void DrawToolbar(ICortexSettingsStore settingsStore, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            ImguiWorkbenchLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Cortex Settings", GUILayout.Height(24f), GUILayout.Width(180f));
                GUILayout.Label("One continuous settings document with section checkpoints, contribution-driven groups, and no page swapping.", GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();

                if (state != null &&
                    state.Workbench != null &&
                    string.Equals(state.Workbench.EditorContainerId, CortexWorkbenchIds.SettingsContainer, StringComparison.OrdinalIgnoreCase) &&
                    GUILayout.Button("Return To Editor", GUILayout.Width(140f), GUILayout.Height(24f)))
                {
                    state.Workbench.EditorContainerId = CortexWorkbenchIds.EditorContainer;
                    state.Workbench.FocusedContainerId = CortexWorkbenchIds.EditorContainer;
                    state.StatusMessage = "Returned to the editor surface.";
                }

                if (GUILayout.Button("Save", GUILayout.Width(100f), GUILayout.Height(24f)))
                {
                    state.Settings = _applicationService.Apply(
                        _draftService,
                        _draftState,
                        _sessionService,
                        _sessionState,
                        snapshot,
                        themeState,
                        state.Settings,
                        _navigationScroll.y,
                        _contentScroll.y);
                    ApplyLoadedModMappings(state);
                    if (settingsStore != null)
                    {
                        settingsStore.Save(state.Settings);
                    }

                    state.ReloadSettingsRequested = true;
                    _loaded = false;
                    state.StatusMessage = "Saved Cortex settings.";
                }

                if (GUILayout.Button("Reset", GUILayout.Width(100f), GUILayout.Height(24f)))
                {
                    ResetToDefaults(snapshot, themeState, state);
                }

                GUILayout.EndHorizontal();
            }, GUILayout.Height(56f), GUILayout.ExpandWidth(true));
        }

        internal void ResetToDefaults(WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            if (state == null)
            {
                return;
            }

            state.Settings = new CortexSettings();
            if (themeState != null)
            {
                themeState.ThemeId = state.Settings.ThemeId;
            }

            _loaded = false;
            _contentScroll = Vector2.zero;
            EnsureLoaded(snapshot, themeState, state);
            _sessionService.RequestSection(_sessionState, WorkspaceOverviewSectionId);
            _sessionService.Persist(_sessionState, state.Settings, _navigationScroll.y, _contentScroll.y);
            state.StatusMessage = "Reset settings fields to defaults.";
        }

        private void DrawSearchBar(SettingsDocumentModel document)
        {
            _sessionState.SearchQuery = UiSurface
                .DrawSearchToolbar("Search properties", _sessionState.SearchQuery, 42f, true);

            ImguiWorkbenchLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                _sessionState.ShowModifiedOnly = GUILayout.Toggle(_sessionState.ShowModifiedOnly, "Show modified only", GUILayout.Width(140f));
                GUILayout.Space(8f);
                GUILayout.Label(BuildSearchSummary(document), GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
        }

        private void DrawNavigation(SettingsDocumentModel document, string renderActiveSectionId, NavigationInteraction interaction)
        {
            ImguiWorkbenchLayout.DrawGroup(null, delegate
            {
                GUILayout.Label("Property Pages", GUILayout.Height(22f));
                GUILayout.Space(4f);
                _navigationAnchors.Clear();
                _navigationScroll = GUILayout.BeginScrollView(_navigationScroll, GUI.skin.box, GUILayout.Width(NavigationWidth), GUILayout.ExpandHeight(true));
                var visibleGroupCount = 0;
                for (var i = 0; i < document.Groups.Count; i++)
                {
                    var visibleSections = GetVisibleSections(document.Groups[i]);
                    if (visibleSections.Count == 0)
                    {
                        continue;
                    }

                    visibleGroupCount++;
                    DrawNavigationGroup(document.Groups[i], renderActiveSectionId, interaction);
                }

                if (visibleGroupCount == 0)
                {
                    GUILayout.Label("No property pages match the current search.");
                }
                GUILayout.EndScrollView();
            }, GUILayout.Width(NavigationWidth + 16f), GUILayout.ExpandHeight(true));
        }

        private void DrawNavigationGroup(SettingsNavigationGroupModel group, string renderActiveSectionId, NavigationInteraction interaction)
        {
            if (group == null || group.Sections.Count == 0)
            {
                return;
            }

            var visibleSections = GetVisibleSections(group);
            if (visibleSections.Count == 0)
            {
                return;
            }

            bool expanded;
            if (!_navigationGroupExpanded.TryGetValue(group.GroupId, out expanded))
            {
                expanded = ShouldDefaultGroupExpanded(group.GroupId);
                _navigationGroupExpanded[group.GroupId] = expanded;
            }

            var searchActive = !string.IsNullOrEmpty(GetNormalizedSearchQuery());
            var activeSection = GetActiveSection(group, renderActiveSectionId);
            var groupHasActiveSection = activeSection != null;
            var effectiveExpanded = IsNavigationGroupExpandedForDisplay(group, expanded, renderActiveSectionId);
            var groupTitle = group.Title;
            if (searchActive || _sessionState.ShowModifiedOnly)
            {
                groupTitle += " (" + CountVisibleSections(group).ToString(CultureInfo.InvariantCulture) + ")";
            }

            if (UiSurface.DrawNavigationGroupHeader(groupTitle, groupHasActiveSection, effectiveExpanded))
            {
                if (searchActive)
                {
                    interaction.RequestSectionReveal(visibleSections[0].SectionId);
                }
                else
                {
                    interaction.ToggleGroup(group.GroupId);
                }
            }
            _navigationAnchors[group.GroupId] = GUILayoutUtility.GetLastRect().y;

            if (!effectiveExpanded && groupHasActiveSection)
            {
                DrawCollapsedActiveSection(activeSection);
            }

            if (effectiveExpanded)
            {
                GUILayout.Space(2f);
                for (var i = 0; i < visibleSections.Count; i++)
                {
                    DrawNavigationButton(visibleSections[i], 14f, renderActiveSectionId, interaction);
                }
            }

            GUILayout.Space(8f);
        }

        private void DrawNavigationButton(SettingsSectionModel section, float indent, string renderActiveSectionId, NavigationInteraction interaction)
        {
            if (section == null)
            {
                return;
            }

            var isSelected = string.Equals(renderActiveSectionId, section.SectionId, StringComparison.OrdinalIgnoreCase);
            if (UiSurface.DrawNavigationItem(BuildSectionNavigationLabel(section), isSelected, indent))
            {
                interaction.RequestSectionReveal(section.SectionId);
            }
            _navigationAnchors[section.SectionId] = GUILayoutUtility.GetLastRect().y;
            GUILayout.Space(2f);
        }

        private void DrawCollapsedActiveSection(SettingsSectionModel activeSection)
        {
            if (activeSection == null)
            {
                return;
            }

            UiSurface.DrawCollapsedNavigationItem(BuildSectionNavigationLabel(activeSection), 18f);
        }

        private void DrawContentSurface(
            ICortexSettingsStore settingsStore,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state,
            SettingsDocumentModel document,
            string renderActiveSectionId)
        {
            ImguiWorkbenchLayout.DrawGroup(null, delegate
            {
                GUILayout.Label("Properties", GUILayout.Height(24f));
                GUILayout.Space(4f);
                DrawActiveSectionBanner(document, renderActiveSectionId);
                GUILayout.Space(4f);

                _sectionAnchors.Clear();
                var contentScrollBeforeDraw = _contentScroll;
                _preserveContentScrollForNestedEditor = false;
                _contentScroll = GUILayout.BeginScrollView(_contentScroll, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                var visibleCount = 0;
                var orderedSections = GetOrderedSections(document);
                for (var i = 0; i < orderedSections.Count; i++)
                {
                    if (IsSectionVisible(orderedSections[i]))
                    {
                        DrawDocumentSection(orderedSections[i], settingsStore, snapshot, themeState, state);
                        visibleCount++;
                    }
                }

                if (visibleCount == 0)
                {
                    GUILayout.Label("No settings match the current search.");
                }
                GUILayout.EndScrollView();
                if (_preserveContentScrollForNestedEditor)
                {
                    _contentScroll = contentScrollBeforeDraw;
                }
            }, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }

        private void DrawDocumentSection(
            SettingsSectionModel section,
            ICortexSettingsStore settingsStore,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            UiSurface.DrawSectionHeader(section.Title, section.Description);
            DrawDocumentSectionBody(section, settingsStore, snapshot, themeState, state);
            GUILayout.EndVertical();

            var rect = GUILayoutUtility.GetLastRect();
            _sectionAnchors[section.SectionId] = rect.y;
            GUILayout.Space(18f);
        }

        private void DrawDocumentSectionBody(
            SettingsSectionModel section,
            ICortexSettingsStore settingsStore,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state)
        {
            if (section == null)
            {
                return;
            }

            switch (section.SectionKind)
            {
                case SettingsSectionKind.WorkspaceOverview:
                    DrawSourceSetupGuide();
                    return;
                case SettingsSectionKind.WorkspacePaths:
                    DrawWorkspacePathEditors(snapshot, state);
                    return;
                case SettingsSectionKind.WorkspaceSettingsContributions:
                    DrawWorkspaceContributionEditors(snapshot, state);
                    return;
                case SettingsSectionKind.WorkspaceModLinks:
                    DrawLoadedModMappings(state);
                    return;
                case SettingsSectionKind.WorkspaceCurrentPaths:
                    DrawQuickFacts(state);
                    return;
                case SettingsSectionKind.ContributionScope:
                    DrawContributionScope(snapshot, section.ContributionScope, state);
                    return;
                case SettingsSectionKind.Onboarding:
                    DrawOnboardingSettings(state);
                    return;
                case SettingsSectionKind.Themes:
                    DrawThemeRegistry(snapshot, themeState, state);
                    return;
                case SettingsSectionKind.Keybindings:
                    DrawEditorKeybindings(state);
                    return;
                case SettingsSectionKind.Editors:
                    DrawEditorRegistry(snapshot);
                    return;
                case SettingsSectionKind.Actions:
                    DrawActionsPanel(state);
                    return;
            }
        }

        private List<SettingsSectionModel> GetOrderedSections(SettingsDocumentModel document)
        {
            return _searchService.GetOrderedSections(document);
        }

        private void DrawSourceSetupGuide()
        {
            DrawSectionPanel("Overview", delegate
            {
                GUILayout.Label("Use this section to configure the source roots Cortex should search and to link active runtime content back to editable source folders.");
                GUILayout.Label("Workspace Scan Root should point at the folder that contains your editable local source trees.");
                GUILayout.Label("Runtime Content Root should point at the deployed content currently active under the host.");
                GUILayout.Label("Modules can contribute additional settings scopes below, and they will be merged into this same document automatically.");
            });
        }

        private void DrawWorkspacePathEditors(WorkbenchPresentationSnapshot snapshot, CortexShellState state)
        {
            DrawSectionPanel("Workspace Paths", delegate
            {
                DrawSettingPathEditor(snapshot, WorkspaceRootSettingId, CortexHostPathSettings.WorkspaceRootDisplayName, "Folder containing your editable local source trees.");
                DrawSettingPathEditor(snapshot, RuntimeContentRootSettingId, CortexHostPathSettings.RuntimeContentRootDisplayName, "Folder containing deployed runtime content used for source mapping and discovery.");
                DrawSettingPathEditor(snapshot, ReferenceAssemblyRootSettingId, CortexHostPathSettings.ReferenceAssemblyRootDisplayName, "Folder containing host-provided assemblies used for reference browsing.");
                DrawSettingPathEditor(snapshot, AdditionalSourceRootsSettingId, CortexHostPathSettings.AdditionalSourceRootsDisplayName, "Semicolon-separated fallback roots used during source resolution.");
            });
        }

        private void DrawWorkspaceContributionEditors(WorkbenchPresentationSnapshot snapshot, CortexShellState state)
        {
            var contributions = CollectContributionsForScope(snapshot, "Workspace");
            if (contributions.Count == 0)
            {
                return;
            }

            DrawSectionPanel("Workspace Settings", delegate
            {
                for (var i = 0; i < contributions.Count; i++)
                {
                    DrawSettingContribution(contributions[i], state);
                }
            });
        }

        private void DrawLoadedModMappings(CortexShellState state)
        {
            DrawSectionPanel("Active Content Source Links", delegate
            {
                var loadedMods = _loadedModCatalog != null ? _loadedModCatalog.GetLoadedMods() : null;
                if (loadedMods == null || loadedMods.Count == 0)
                {
                    GUILayout.Label("No active content items were discovered.");
                    return;
                }

                var shown = 0;
                for (var i = 0; i < loadedMods.Count; i++)
                {
                    var mod = loadedMods[i];
                    if (mod == null || string.IsNullOrEmpty(mod.ModId))
                    {
                        continue;
                    }

                    DrawLoadedModMappingRow(mod, state);
                    GUILayout.Space(6f);
                    shown++;
                }

                if (shown == 0)
                {
                    GUILayout.Label("No active content items were discovered.");
                }
            });
        }

        private void DrawQuickFacts(CortexShellState state)
        {
            DrawSectionPanel("Current Paths", delegate
            {
                DrawReadOnlyField("Workspace root", state != null && state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty);
                DrawReadOnlyField("Runtime content root", state != null && state.Settings != null ? state.Settings.RuntimeContentRootPath : string.Empty);
                DrawReadOnlyField("Reference assemblies", state != null && state.Settings != null ? state.Settings.ReferenceAssemblyRootPath : string.Empty);
                DrawReadOnlyField("Project catalog", state != null && state.Settings != null ? state.Settings.ProjectCatalogPath : string.Empty);
            });
        }

        private void DrawContributionScope(WorkbenchPresentationSnapshot snapshot, string scope, CortexShellState state)
        {
            var contributions = CollectContributionsForScope(snapshot, scope);
            if (contributions.Count == 0)
            {
                GUILayout.Label("No settings were registered for this scope.");
                return;
            }

            for (var i = 0; i < contributions.Count; i++)
            {
                DrawSettingContribution(contributions[i], state);
            }
        }

        private List<SettingContribution> CollectContributionsForScope(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            return _contributionCollectionService.CollectVisibleContributionsForScope(snapshot, scope, IsContributionVisible);
        }

        private bool HasVisibleContributionsForScope(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            return _contributionCollectionService.HasVisibleContributionsForScope(snapshot, scope, IsContributionVisible);
        }

        private bool ShouldRenderContribution(string scope, SettingContribution contribution)
        {
            return _contributionCollectionService.ShouldRenderContribution(scope, contribution);
        }

        private void DrawSettingPathEditor(WorkbenchPresentationSnapshot snapshot, string settingId, string label, string description)
        {
            var contribution = FindSettingContribution(snapshot, settingId);
            DrawSettingContribution(
                contribution ?? new SettingContribution
                {
                    SettingId = settingId,
                    DisplayName = label,
                    Description = description,
                    DefaultValue = string.Empty,
                    ValueKind = SettingValueKind.String,
                    EditorKind = SettingEditorKind.Path,
                    PlaceholderText = string.Empty
                },
                null);
        }

        private void DrawLoadedModMappingRow(LoadedModInfo mod, CortexShellState state)
        {
            var existing = _projectCatalog != null ? _projectCatalog.GetProject(mod.ModId) : null;
            var existingSourceRoot = existing != null ? existing.SourceRootPath ?? string.Empty : string.Empty;
            var draftKey = mod.ModId ?? string.Empty;
            var draftValue = GetLoadedModDraftValue(mod);
            var inferredSourceRoot = _workspaceService != null ? _workspaceService.FindLikelySourceRoot(mod.RootPath) : string.Empty;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(mod.DisplayName ?? mod.ModId, GUILayout.Height(20f));
            GUILayout.Label("Mod ID: " + (mod.ModId ?? string.Empty));
                GUILayout.Label("Runtime Content Root: " + (mod.RootPath ?? string.Empty));
            if (!string.IsNullOrEmpty(existingSourceRoot))
            {
                GUILayout.Label("Current Source Link: " + existingSourceRoot);
            }
            if (!string.IsNullOrEmpty(inferredSourceRoot))
            {
                GUILayout.Label("Suggested Source Root: " + inferredSourceRoot);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Source Root", GUILayout.Width(100f));
            draftValue = CortexPathField.DrawValueEditor(
                "settings.loadedMod." + draftKey + ".sourceRoot",
                draftValue ?? string.Empty,
                _pathInteractionService,
                new CortexPathFieldOptions
                {
                    AllowBrowse = true,
                    AllowOpen = true,
                    AllowPaste = true,
                    AllowClear = true,
                    BrowseRequest = new PathSelectionRequest
                    {
                        SelectionKind = PathSelectionKind.Folder,
                        Title = "Select source root",
                        InitialPath = !string.IsNullOrEmpty(draftValue)
                            ? draftValue
                            : (!string.IsNullOrEmpty(existingSourceRoot)
                                ? existingSourceRoot
                                : (!string.IsNullOrEmpty(inferredSourceRoot)
                                    ? inferredSourceRoot
                                    : mod.RootPath))
                    }
                },
                GUILayout.Height(24f),
                GUILayout.ExpandWidth(true));
            _loadedModPathDrafts[draftKey] = draftValue;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Current Link", GUILayout.Width(130f), GUILayout.Height(24f)))
            {
                _loadedModPathDrafts[draftKey] = existingSourceRoot;
            }

            if (GUILayout.Button("Use Suggestion", GUILayout.Width(120f), GUILayout.Height(24f)))
            {
                _loadedModPathDrafts[draftKey] = inferredSourceRoot;
            }

            if (GUILayout.Button("Use Workspace Root", GUILayout.Width(140f), GUILayout.Height(24f)))
            {
                _loadedModPathDrafts[draftKey] = GetTextValue(WorkspaceRootSettingId, state != null && state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty);
            }

            if (GUILayout.Button("Link Mod To Source", GUILayout.Width(150f), GUILayout.Height(24f)))
            {
                LinkLoadedModToSource(mod, state);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void EnsureLoaded(WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            if (_loaded)
            {
                return;
            }

            _draftService.Initialize(_draftState, snapshot, themeState, state != null ? state.Settings : null);
            _navigationGroupExpanded.Clear();
            _collapsedNavigationGroups.Clear();
            _openSettingActionMenuId = string.Empty;
            _sessionState.LastNormalizedSearchQuery = string.Empty;

            var settings = state != null && state.Settings != null ? state.Settings : new CortexSettings();
            LoadNavigationGroupState(settings);
            float navigationScrollY;
            float contentScrollY;
            _sessionService.Restore(_sessionState, settings, WorkspaceOverviewSectionId, out navigationScrollY, out contentScrollY);
            _navigationScroll.y = navigationScrollY;
            _contentScroll.y = contentScrollY;
            _loaded = true;
        }

        private void DrawThemeRegistry(WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            DrawSectionPanel("Theme Catalog", delegate
            {
                if (snapshot == null || snapshot.Themes.Count == 0)
                {
                    GUILayout.Label("No themes were registered.");
                    return;
                }

                GUILayout.Label("Choose the shell theme. The selected theme is applied immediately and persisted with Cortex settings.");
                GUILayout.Space(6f);
                var shown = 0;
                for (var i = 0; i < snapshot.Themes.Count; i++)
                {
                    if (_sessionState.ShowModifiedOnly && !string.Equals(snapshot.Themes[i].ThemeId, _selectedThemeId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!MatchesThemeQuery(snapshot.Themes[i]))
                    {
                        continue;
                    }

                    DrawThemeOption(snapshot.Themes[i], themeState, state);
                    shown++;
                }

                if (shown == 0)
                {
                    GUILayout.Label("No themes match the current search.");
                }
            });
        }

        private void DrawEditorKeybindings(CortexShellState state)
        {
            if (state.Settings == null)
            {
                state.Settings = new CortexSettings();
            }

            DrawSectionPanel("Editor Input", delegate
            {
                var showUndoLimit = !_sessionState.ShowModifiedOnly || IsUndoHistoryModified();
                if (!showUndoLimit)
                {
                    return;
                }

                GUILayout.Label("These bindings apply to the Cortex source editor. Key values use Unity KeyCode names such as LeftArrow, Home, Tab, Return, or A.");
                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Undo History Limit", GUILayout.Width(180f));
                var undoLimitKey = "editor.undo.limit";
                _textValues[undoLimitKey] = GUILayout.TextField(GetTextValue(undoLimitKey, state.Settings.EditorUndoHistoryLimit.ToString()), GUILayout.Width(80f), GUILayout.Height(22f));
                int undoLimit;
                if (int.TryParse(_textValues[undoLimitKey], out undoLimit))
                {
                    state.Settings.EditorUndoHistoryLimit = Mathf.Clamp(undoLimit, 10, 512);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset Defaults", GUILayout.Width(120f), GUILayout.Height(24f)))
                {
                    _editorKeybindingService.ResetToDefaults(state.Settings);
                    _textValues[undoLimitKey] = "128";
                    state.Settings.EditorUndoHistoryLimit = 128;
                }
                GUILayout.EndHorizontal();
            });

            var bindings = _editorKeybindingService.GetCommandBindings();
            var currentCategory = string.Empty;
            var shownBindings = 0;
            for (var i = 0; i < bindings.Count; i++)
            {
                if (!MatchesKeybindingQuery(bindings[i]))
                {
                    continue;
                }

                if (_sessionState.ShowModifiedOnly && !IsBindingModified(state.Settings, bindings[i]))
                {
                    continue;
                }

                if (!string.Equals(currentCategory, bindings[i].Category, StringComparison.Ordinal))
                {
                    currentCategory = bindings[i].Category;
                    GUILayout.Space(8f);
                    GUILayout.Label(currentCategory, GUILayout.Height(22f));
                }

                DrawEditorKeybindingRow(state.Settings, bindings[i]);
                GUILayout.Space(4f);
                shownBindings++;
            }

            if (shownBindings == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("No keybindings match the current search.");
            }
        }

        private void DrawEditorKeybindingRow(CortexSettings settings, EditorCommandBindingDefinition definition)
        {
            var binding = GetEditableBinding(settings, definition);
            BeginPropertyRow();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(definition.DisplayName, GUILayout.Height(20f));
            GUILayout.FlexibleSpace();
            if (IsBindingModified(settings, definition))
            {
                DrawSettingTag("Modified", ImguiWorkbenchLayout.GetWarningColor());
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(definition.Description))
            {
                GUILayout.Label(definition.Description);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth + 120f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Key", GUILayout.Width(36f));
            binding.Key = GUILayout.TextField(binding.Key ?? string.Empty, GUILayout.Width(110f), GUILayout.Height(22f));
            binding.Control = GUILayout.Toggle(binding.Control, "Ctrl", GUILayout.Width(52f));
            binding.Shift = GUILayout.Toggle(binding.Shift, "Shift", GUILayout.Width(56f));
            binding.Alt = GUILayout.Toggle(binding.Alt, "Alt", GUILayout.Width(44f));
            if (GUILayout.Button("Reset", GUILayout.Width(60f), GUILayout.Height(22f)))
            {
                ApplyBinding(binding, definition.DefaultBinding);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Current: " + _editorKeybindingService.FormatGesture(binding));
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EndPropertyRow();
        }

        private void DrawEditorRegistry(WorkbenchPresentationSnapshot snapshot)
        {
            DrawSectionPanel("Registered Editors", delegate
            {
                if (snapshot == null || snapshot.Editors.Count == 0)
                {
                    GUILayout.Label("No editors were registered.");
                    return;
                }

                for (var i = 0; i < snapshot.Editors.Count; i++)
                {
                    var editor = snapshot.Editors[i];
                    if (!MatchesEditorQuery(editor))
                    {
                        continue;
                    }

                    BeginPropertyRow();
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(GUILayout.Width(280f));
                    GUILayout.Label(editor.DisplayName, GUILayout.Height(22f));
                    GUILayout.Label("Extension: " + (editor.ResourceExtension ?? string.Empty));
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth));
                    GUILayout.Label("Content Type: " + (editor.ContentType ?? string.Empty));
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    EndPropertyRow();
                    GUILayout.Space(6f);
                }

                var hasVisibleEditors = false;
                for (var i = 0; i < snapshot.Editors.Count; i++)
                {
                    if (MatchesEditorQuery(snapshot.Editors[i]))
                    {
                        hasVisibleEditors = true;
                        break;
                    }
                }

                if (!hasVisibleEditors)
                {
                    GUILayout.Label("No editors match the current search.");
                }
            });
        }

        private void DrawActionsPanel(CortexShellState state)
        {
            DrawSectionPanel("Window Actions", delegate
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Return To Editor", GUILayout.Width(180f), GUILayout.Height(24f)))
                {
                    state.Workbench.EditorContainerId = CortexWorkbenchIds.EditorContainer;
                    state.Workbench.FocusedContainerId = CortexWorkbenchIds.EditorContainer;
                    state.StatusMessage = "Returned to the editor surface.";
                }
                if (GUILayout.Button("Show Logs Window", GUILayout.Width(180f), GUILayout.Height(24f)))
                {
                    if (_viewState != null)
                    {
                        _viewState.ShowDetachedLogsWindow = true;
                    }
                }
                GUILayout.EndHorizontal();
            });
        }

        private void DrawOnboardingSettings(CortexShellState state)
        {
            DrawSectionPanel("Onboarding", delegate
            {
                GUILayout.Label("Run the onboarding flow again to pick a different starting profile, layout, or theme.");
                GUILayout.Label("The onboarding overlay opens above Cortex and keeps the shell blocked while you preview the current workspace behind it.");
                GUILayout.Space(6f);
                DrawReadOnlyField("Current Profile", ResolveOnboardingValue(state != null ? state.Settings : null, delegate(CortexSettings settings) { return settings.DefaultOnboardingProfileId; }, "cortex.onboarding.profile.ide"));
                DrawReadOnlyField("Current Layout", ResolveOnboardingValue(state != null ? state.Settings : null, delegate(CortexSettings settings) { return settings.DefaultOnboardingLayoutPresetId; }, "cortex.onboarding.layout.visual-studio"));
                DrawReadOnlyField("Current Theme", ResolveOnboardingValue(state != null ? state.Settings : null, delegate(CortexSettings settings) { return settings.DefaultOnboardingThemeId; }, "cortex.vs-dark"));
                GUILayout.Space(4f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Run Onboarding Again", GUILayout.Width(188f), GUILayout.Height(24f)))
                {
                    RequestOnboardingReopen(state);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            });
        }

        private void DrawThemeOption(ThemeContribution theme, ThemeState themeState, CortexShellState state)
        {
            if (theme == null)
            {
                return;
            }

            var isSelected = string.Equals(_selectedThemeId, theme.ThemeId, StringComparison.OrdinalIgnoreCase);
            BeginPropertyRow();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(GUILayout.MinWidth(240f), GUILayout.MaxWidth(320f), GUILayout.ExpandWidth(true));
            GUILayout.Label(theme.DisplayName ?? theme.ThemeId, GUILayout.Height(20f));
            GUILayout.Label(theme.ThemeId ?? string.Empty);
            if (!string.IsNullOrEmpty(theme.Description))
            {
                GUILayout.Label(theme.Description);
            }
            GUILayout.EndVertical();

            GUILayout.Space(18f);
            GUILayout.BeginVertical(GUILayout.MinWidth(220f), GUILayout.MaxWidth(360f), GUILayout.ExpandWidth(true));
            var label = isSelected ? "Selected" : "Use Theme";
            if (GUILayout.Toggle(isSelected, label, "button", GUILayout.Width(148f), GUILayout.Height(24f)))
            {
                _selectedThemeId = string.IsNullOrEmpty(theme.ThemeId) ? "cortex.vs-dark" : theme.ThemeId;
                if (themeState != null)
                {
                    themeState.ThemeId = _selectedThemeId;
                }

                if (state.Settings != null)
                {
                    state.Settings.ThemeId = _selectedThemeId;
                }
            }

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            DrawThemeSwatch(theme.BackgroundColor, "BG");
            DrawThemeSwatch(theme.SurfaceColor, "Surface");
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            DrawThemeSwatch(theme.HeaderColor, "Header");
            DrawThemeSwatch(theme.AccentColor, "Accent");
            GUILayout.EndHorizontal();
            GUILayout.Label("Font role: " + (string.IsNullOrEmpty(theme.FontRole) ? "default" : theme.FontRole));
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EndPropertyRow();
            GUILayout.Space(6f);
        }

        private void DrawSettingContribution(SettingContribution contribution, CortexShellState state)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            BeginPropertyRow();
            switch (GetSettingEditorKind(contribution))
            {
                case SettingEditorKind.Choice:
                    DrawChoiceSettingEditor(contribution);
                    break;
                case SettingEditorKind.MultilineText:
                    DrawMultilineSettingEditor(contribution);
                    break;
                case SettingEditorKind.Secret:
                    DrawSecretSettingEditor(contribution);
                    break;
                case SettingEditorKind.Path:
                    DrawTextSettingEditor(contribution, false, false, true);
                    break;
                case SettingEditorKind.Text:
                case SettingEditorKind.Auto:
                default:
                    if (contribution.ValueKind == SettingValueKind.Boolean)
                    {
                        DrawBooleanSettingEditor(contribution, state);
                    }
                    else
                    {
                        DrawTextSettingEditor(contribution, contribution.ValueKind == SettingValueKind.String, contribution.ValueKind == SettingValueKind.String, false);
                    }
                    break;
            }
            EndPropertyRow();
            GUILayout.Space(6f);
        }

        private void DrawTextValueEditor(string settingId, string defaultValue, bool allowPaste, bool allowClear)
        {
            GUILayout.BeginHorizontal();
            _textValues[settingId] = GUILayout.TextField(GetTextValue(settingId, defaultValue), GUILayout.Height(24f), GUILayout.ExpandWidth(true));
            if (allowPaste && GUILayout.Button("Paste", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _textValues[settingId] = GUIUtility.systemCopyBuffer ?? string.Empty;
            }
            if (allowClear && GUILayout.Button("Clear", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _textValues[settingId] = string.Empty;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCompactTextValueEditor(string settingId, string defaultValue, bool allowPaste, bool allowClear)
        {
            GUILayout.BeginHorizontal();
            _textValues[settingId] = GUILayout.TextField(GetTextValue(settingId, defaultValue), GUILayout.Width(CompactEditorWidth), GUILayout.Height(24f));
            if (allowPaste && GUILayout.Button("Paste", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _textValues[settingId] = GUIUtility.systemCopyBuffer ?? string.Empty;
            }
            if (allowClear && GUILayout.Button("Clear", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _textValues[settingId] = string.Empty;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawTextSettingEditor(SettingContribution contribution, bool allowPaste, bool allowClear, bool showPathActions)
        {
            GUILayout.BeginHorizontal();
            DrawSettingLabelColumn(contribution);
            GUILayout.BeginVertical(GUILayout.Width(showPathActions ? CompactEditorWidth + 260f : CompactEditorWidth + 132f));
            if (showPathActions)
            {
                DrawCompactPathValueEditor(contribution);
            }
            else
            {
                DrawCompactTextValueEditor(contribution.SettingId, GetDefaultSerializedValue(contribution), allowPaste, allowClear);
            }
            DrawSettingFooter(contribution);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawMultilineSettingEditor(SettingContribution contribution)
        {
            GUILayout.BeginHorizontal();
            DrawSettingLabelColumn(contribution);
            GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth + 80f));
            _textValues[contribution.SettingId] = DrawScrollableTextArea(
                contribution.SettingId,
                GetTextValue(contribution),
                CompactEditorWidth + 80f,
                112f);
            DrawSettingFooter(contribution);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private string DrawScrollableTextArea(string settingId, string value, float width, float height)
        {
            Vector2 scroll;
            if (!_multilineEditorScrolls.TryGetValue(settingId, out scroll))
            {
                scroll = Vector2.zero;
            }

            scroll = GUILayout.BeginScrollView(
                scroll,
                GUI.skin.box,
                GUILayout.Width(width),
                GUILayout.Height(height));

            var nextValue = GUILayout.TextArea(
                value ?? string.Empty,
                GUILayout.Width(width - 18f),
                GUILayout.ExpandHeight(true));

            GUILayout.EndScrollView();
            var scrollRect = GUILayoutUtility.GetLastRect();

            if (Event.current != null &&
                Event.current.type == EventType.ScrollWheel &&
                scrollRect.Contains(Event.current.mousePosition))
            {
                scroll.y = Mathf.Max(0f, scroll.y + (Event.current.delta.y * 18f));
                _preserveContentScrollForNestedEditor = true;
                Event.current.Use();
            }

            _multilineEditorScrolls[settingId] = scroll;
            return nextValue ?? string.Empty;
        }

        private void DrawSecretSettingEditor(SettingContribution contribution)
        {
            GUILayout.BeginHorizontal();
            DrawSettingLabelColumn(contribution);
            GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth + 132f));
            GUILayout.BeginHorizontal();
            _textValues[contribution.SettingId] = GUILayout.PasswordField(GetTextValue(contribution), '*', GUILayout.Width(CompactEditorWidth), GUILayout.Height(24f));
            if (GUILayout.Button("Paste", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _textValues[contribution.SettingId] = GUIUtility.systemCopyBuffer ?? string.Empty;
            }
            if (GUILayout.Button("Clear", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _textValues[contribution.SettingId] = string.Empty;
            }
            GUILayout.EndHorizontal();
            DrawSettingFooter(contribution);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawChoiceSettingEditor(SettingContribution contribution)
        {
            var options = contribution.Options ?? new SettingChoiceOption[0];
            if (options.Length == 0)
            {
                DrawTextSettingEditor(contribution, false, true, false);
                return;
            }

            var currentValue = GetTextValue(contribution);
            var selectedIndex = FindChoiceIndex(options, currentValue);
            var labels = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                labels[i] = string.IsNullOrEmpty(options[i].DisplayName) ? options[i].Value ?? string.Empty : options[i].DisplayName;
            }

            GUILayout.BeginHorizontal();
            DrawSettingLabelColumn(contribution);
            GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth + 80f));
            var toolbarIndex = selectedIndex < 0 ? 0 : selectedIndex;
            var nextIndex = GUILayout.Toolbar(toolbarIndex, labels, GUILayout.Width(CompactEditorWidth + 80f), GUILayout.Height(24f));
            if (selectedIndex < 0 && string.IsNullOrEmpty(currentValue))
            {
                _textValues[contribution.SettingId] = options[toolbarIndex].Value ?? string.Empty;
                selectedIndex = toolbarIndex;
            }
            else if (nextIndex >= 0 && nextIndex < options.Length && nextIndex != selectedIndex)
            {
                _textValues[contribution.SettingId] = options[nextIndex].Value ?? string.Empty;
                selectedIndex = nextIndex;
            }

            if (selectedIndex >= 0 && selectedIndex < options.Length && !string.IsNullOrEmpty(options[selectedIndex].Description))
            {
                GUILayout.Label(options[selectedIndex].Description);
            }
            DrawSettingFooter(contribution);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawBooleanSettingEditor(SettingContribution contribution, CortexShellState state)
        {
            var value = GetToggleValue(contribution);

            GUILayout.BeginHorizontal();
            DrawSettingLabelColumn(contribution);
            GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth));
            var nextValue = GUILayout.Toggle(value, "Enabled", GUILayout.Height(22f));
            if (nextValue != value)
            {
                value = SetBooleanSettingValue(contribution, state, nextValue);
            }

            DrawSettingFooter(contribution);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            _toggleValues[contribution.SettingId] = value;
        }

        private static SettingEditorKind GetSettingEditorKind(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return SettingEditorKind.Text;
            }

            if (contribution.IsSecret)
            {
                return SettingEditorKind.Secret;
            }

            if (contribution.EditorKind != SettingEditorKind.Auto)
            {
                return contribution.EditorKind;
            }

            return contribution.ValueKind == SettingValueKind.Boolean
                ? SettingEditorKind.Auto
                : SettingEditorKind.Text;
        }

        private static int FindChoiceIndex(SettingChoiceOption[] options, string currentValue)
        {
            if (options == null)
            {
                return -1;
            }

            for (var i = 0; i < options.Length; i++)
            {
                if (options[i] != null && string.Equals(options[i].Value, currentValue, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawSettingLabelColumn(SettingContribution contribution)
        {
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(contribution != null ? contribution.DisplayName ?? contribution.SettingId : string.Empty, GUILayout.Height(20f));
            GUILayout.FlexibleSpace();
            if (contribution != null && IsSearchActive() && MatchesContributionQuery(contribution))
            {
                DrawSettingTag("Match", ImguiWorkbenchLayout.GetAccentColor());
            }
            if (contribution != null && IsSettingModified(contribution))
            {
                DrawSettingTag("Modified", ImguiWorkbenchLayout.GetWarningColor());
            }
            if (contribution != null && HasSettingActions(contribution))
            {
                var isMenuOpen = string.Equals(_openSettingActionMenuId, contribution.SettingId, StringComparison.OrdinalIgnoreCase);
                if (GUILayout.Button(SettingGearGlyph, GUILayout.Width(26f), GUILayout.Height(20f)))
                {
                    _openSettingActionMenuId = isMenuOpen ? string.Empty : contribution.SettingId;
                }
            }
            GUILayout.EndHorizontal();

            if (contribution != null && !string.IsNullOrEmpty(contribution.Description))
            {
                GUILayout.Label(contribution.Description);
            }

            GUILayout.EndVertical();
        }

        private void DrawSettingFooter(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(contribution.HelpText))
            {
                GUILayout.Label(contribution.HelpText);
            }
            else if (!string.IsNullOrEmpty(contribution.PlaceholderText) && string.IsNullOrEmpty(GetDefaultSerializedValue(contribution)))
            {
                GUILayout.Label("Example: " + contribution.PlaceholderText);
            }

            var validation = GetValidationResult(contribution);
            if (validation != null && validation.Severity != SettingValidationSeverity.None && !string.IsNullOrEmpty(validation.Message))
            {
                DrawValidationMessage(validation);
            }

            var defaultValue = GetDefaultSerializedValue(contribution);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                GUILayout.Label("Default: " + defaultValue);
            }

            if (string.Equals(_openSettingActionMenuId, contribution.SettingId, StringComparison.OrdinalIgnoreCase))
            {
                DrawSettingActionsMenu(contribution);
            }
        }

        private static void DrawSettingTag(string text, Color color)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = color;
            GUI.contentColor = GetReadableSwatchTextColor(color);
            GUILayout.Box(text ?? string.Empty, GUILayout.Height(18f));
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            GUILayout.Space(4f);
        }

        private bool HasSettingActions(SettingContribution contribution)
        {
            return contribution != null;
        }

        private void DrawSettingActionsMenu(SettingContribution contribution)
        {
            GUILayout.Space(4f);
            UiSurface.DrawPopupMenuPanel(220f, delegate
            {
                DrawSettingActionButton("Reset to default", delegate
                {
                    ResetSettingToDefault(contribution);
                    SetStatusMessage("Reset '" + (contribution.DisplayName ?? contribution.SettingId) + "' to its default value.");
                });
                DrawSettingActionButton("Copy setting id", delegate
                {
                    GUIUtility.systemCopyBuffer = contribution.SettingId ?? string.Empty;
                    SetStatusMessage("Copied setting id '" + (contribution.SettingId ?? string.Empty) + "'.");
                });
                DrawSettingActionButton("Copy current value", delegate
                {
                    GUIUtility.systemCopyBuffer = GetSerializedDraftValue(contribution);
                    SetStatusMessage("Copied current value for '" + (contribution.DisplayName ?? contribution.SettingId) + "'.");
                });

                var actions = contribution.Actions ?? new SettingActionContribution[0];
                for (var i = 0; i < actions.Length; i++)
                {
                    var action = actions[i];
                    if (action == null || action.Execute == null)
                    {
                        continue;
                    }

                    var capturedAction = action;
                    DrawSettingActionButton(capturedAction.DisplayName ?? capturedAction.ActionId, delegate
                    {
                        capturedAction.Execute(new SettingActionInvocation
                        {
                            SettingId = contribution.SettingId,
                            CurrentValue = GetSerializedDraftValue(contribution),
                            DefaultValue = GetDefaultSerializedValue(contribution),
                            SetDraftValue = delegate(string nextValue) { SetDraftSerializedValue(contribution, nextValue); },
                            SetStatusMessage = SetStatusMessage
                        });
                    });
                }
            });
        }

        private void DrawSettingActionButton(string label, Action onClick)
        {
            if (GUILayout.Button(label ?? string.Empty, GUILayout.Height(22f), GUILayout.ExpandWidth(true)) && onClick != null)
            {
                _openSettingActionMenuId = string.Empty;
                onClick();
            }
        }

        private void ResetSettingToDefault(SettingContribution contribution)
        {
            SetDraftSerializedValue(contribution, GetDefaultSerializedValue(contribution));
        }

        private void SetDraftSerializedValue(SettingContribution contribution, string serializedValue)
        {
            _draftService.SetDraftSerializedValue(_draftState, contribution, serializedValue);
        }

        private bool IsSearchActive()
        {
            return !string.IsNullOrEmpty(GetNormalizedSearchQuery());
        }

        private bool IsSettingModified(SettingContribution contribution)
        {
            return _draftService.IsSettingModified(_draftState, contribution, _shellState != null ? _shellState.Settings : null);
        }

        private SettingValidationResult GetValidationResult(SettingContribution contribution)
        {
            return _draftService.GetValidationResult(_draftState, contribution);
        }

        private void DrawValidationMessage(SettingValidationResult validation)
        {
            if (validation == null || validation.Severity == SettingValidationSeverity.None || string.IsNullOrEmpty(validation.Message))
            {
                return;
            }

            var previousColor = GUI.contentColor;
            GUI.contentColor = GetValidationColor(validation.Severity);
            GUILayout.Label(validation.Message);
            GUI.contentColor = previousColor;
        }

        private static Color GetValidationColor(SettingValidationSeverity severity)
        {
            switch (severity)
            {
                case SettingValidationSeverity.Error:
                    return ImguiWorkbenchLayout.GetErrorColor();
                case SettingValidationSeverity.Warning:
                    return ImguiWorkbenchLayout.GetWarningColor();
                case SettingValidationSeverity.Info:
                    return ImguiWorkbenchLayout.GetAccentColor();
                case SettingValidationSeverity.None:
                default:
                    return ImguiWorkbenchLayout.GetTextColor();
            }
        }

        private void SetStatusMessage(string message)
        {
            if (_shellState != null)
            {
                _shellState.StatusMessage = message ?? string.Empty;
            }
        }

        private void DrawCompactPathValueEditor(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            _textValues[contribution.SettingId] = CortexPathField.DrawValueEditor(
                "settings.setting." + contribution.SettingId,
                GetTextValue(contribution.SettingId, GetDefaultSerializedValue(contribution)),
                _pathInteractionService,
                new CortexPathFieldOptions
                {
                    AllowBrowse = true,
                    AllowOpen = true,
                    AllowPaste = true,
                    AllowClear = true,
                    BrowseRequest = BuildPathSelectionRequest(contribution)
                },
                GUILayout.Height(24f),
                GUILayout.ExpandWidth(true));
        }

        private PathSelectionRequest BuildPathSelectionRequest(SettingContribution contribution)
        {
            var initialValue = GetTextValue(contribution != null ? contribution.SettingId : string.Empty, GetDefaultSerializedValue(contribution));
            if (string.IsNullOrEmpty(initialValue) && contribution != null)
            {
                initialValue = contribution.PlaceholderText ?? string.Empty;
            }

            return new PathSelectionRequest
            {
                SelectionKind = ResolvePathSelectionKind(contribution, initialValue),
                Title = "Select " + (contribution != null && !string.IsNullOrEmpty(contribution.DisplayName) ? contribution.DisplayName : "path"),
                InitialPath = initialValue
            };
        }

        private static PathSelectionKind ResolvePathSelectionKind(SettingContribution contribution, string currentValue)
        {
            if (LooksLikeFilePath(currentValue))
            {
                return PathSelectionKind.OpenFile;
            }

            if (contribution != null && LooksLikeFilePath(contribution.DefaultValue))
            {
                return PathSelectionKind.OpenFile;
            }

            if (contribution != null && LooksLikeFilePath(contribution.PlaceholderText))
            {
                return PathSelectionKind.OpenFile;
            }

            return PathSelectionKind.Folder;
        }

        private static bool LooksLikeFilePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                return !string.IsNullOrEmpty(Path.GetExtension(path));
            }
            catch
            {
                return false;
            }
        }

        private bool SetBooleanSettingValue(SettingContribution contribution, CortexShellState state, bool value)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return value;
            }

            _toggleValues[contribution.SettingId] = value;

            return value;
        }

        private void DrawSectionPanel(string title, Action drawBody)
        {
            UiSurface.DrawSectionPanel(title, drawBody);
        }

        private void BeginPropertyRow()
        {
            UiSurface.BeginPropertyRow();
        }

        private void EndPropertyRow()
        {
            UiSurface.EndPropertyRow();
        }

        private static void DrawReadOnlyField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180f));
            GUILayout.TextField(string.IsNullOrEmpty(value) ? "Not configured" : value, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private static string ResolveOnboardingValue(CortexSettings settings, Func<CortexSettings, string> accessor, string fallback)
        {
            if (settings == null || accessor == null)
            {
                return fallback;
            }

            var value = accessor(settings);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void RequestOnboardingReopen(CortexShellState state)
        {
            if (state == null || state.Workbench == null)
            {
                return;
            }

            state.Workbench.EditorContainerId = CortexWorkbenchIds.EditorContainer;
            state.Workbench.FocusedContainerId = CortexWorkbenchIds.EditorContainer;
            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            state.OpenOnboardingRequested = true;
            state.StatusMessage = "Reopening onboarding.";
        }

        private static void DrawThemeSwatch(string hex, string label)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            var swatchColor = ImguiWorkbenchLayout.ParseColor(hex, Color.white);
            GUI.backgroundColor = swatchColor;
            GUI.contentColor = GetReadableSwatchTextColor(swatchColor);
            GUILayout.Box(label, GUILayout.Width(82f), GUILayout.Height(18f));
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            GUILayout.Space(4f);
        }

        private static Color GetReadableSwatchTextColor(Color swatchColor)
        {
            var luminance = (swatchColor.r * 0.299f) + (swatchColor.g * 0.587f) + (swatchColor.b * 0.114f);
            return luminance >= 0.6f ? Color.black : Color.white;
        }

        private static void ApplyBinding(EditorKeybinding target, EditorKeybinding source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.BindingId = source.BindingId ?? string.Empty;
            target.CommandId = source.CommandId ?? string.Empty;
            target.Key = source.Key ?? string.Empty;
            target.Control = source.Control;
            target.Shift = source.Shift;
            target.Alt = source.Alt;
        }

        private static EditorKeybinding CreateBindingCopy(EditorKeybinding source)
        {
            var copy = new EditorKeybinding();
            ApplyBinding(copy, source);
            return copy;
        }

        private static EditorKeybinding FindBinding(EditorKeybinding[] bindings, string bindingId)
        {
            if (bindings == null || string.IsNullOrEmpty(bindingId))
            {
                return null;
            }

            for (var i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != null && string.Equals(bindings[i].BindingId, bindingId, StringComparison.OrdinalIgnoreCase))
                {
                    return bindings[i];
                }
            }

            return null;
        }

        private EditorKeybinding GetEditableBinding(CortexSettings settings, EditorCommandBindingDefinition definition)
        {
            if (settings == null || definition == null)
            {
                return new EditorKeybinding();
            }

            var existing = FindBinding(settings.EditorKeybindings, definition.BindingId);
            if (existing != null)
            {
                return existing;
            }

            var bindings = new List<EditorKeybinding>();
            if (settings.EditorKeybindings != null)
            {
                for (var i = 0; i < settings.EditorKeybindings.Length; i++)
                {
                    if (settings.EditorKeybindings[i] != null)
                    {
                        bindings.Add(settings.EditorKeybindings[i]);
                    }
                }
            }

            var created = CreateBindingCopy(definition.DefaultBinding);
            bindings.Add(created);
            settings.EditorKeybindings = bindings.ToArray();
            return created;
        }

        private string GetDefaultSerializedValue(SettingContribution contribution)
        {
            return _draftService.GetDefaultSerializedValue(contribution);
        }

        private string GetSerializedDraftValue(SettingContribution contribution)
        {
            return _draftService.GetSerializedDraftValue(_draftState, contribution);
        }

        private string NormalizeSerializedValue(SettingContribution contribution, string rawValue)
        {
            return _draftService.NormalizeSerializedValue(contribution, rawValue);
        }

        private string GetTextValue(SettingContribution contribution)
        {
            return _draftService.GetTextValue(_draftState, contribution);
        }

        private string GetTextValue(string settingId, string defaultValue)
        {
            return _draftService.GetTextValue(_draftState, settingId, defaultValue);
        }

        private bool GetToggleValue(SettingContribution contribution)
        {
            return _draftService.GetToggleValue(_draftState, contribution);
        }

        private List<SettingsSectionModel> GetVisibleSections(SettingsNavigationGroupModel group)
        {
            return _searchService.GetVisibleSections(group, GetNormalizedSearchQuery(), _sessionState.ShowModifiedOnly, GetSectionVisibleItemCount);
        }

        private bool IsSectionVisible(SettingsSectionModel section)
        {
            return _searchService.IsSectionVisible(section, GetNormalizedSearchQuery(), _sessionState.ShowModifiedOnly, GetSectionVisibleItemCount);
        }

        private int GetSectionVisibleItemCount(SettingsSectionModel section)
        {
            if (section == null)
            {
                return 0;
            }

            switch (section.SectionKind)
            {
                case SettingsSectionKind.WorkspacePaths:
                    return CountVisibleSettingsForIds(WorkspaceRootSettingId, RuntimeContentRootSettingId, ReferenceAssemblyRootSettingId, AdditionalSourceRootsSettingId);
                case SettingsSectionKind.WorkspaceSettingsContributions:
                case SettingsSectionKind.ContributionScope:
                    return CountVisibleContributionsForScope(_snapshot, section.ContributionScope);
                case SettingsSectionKind.Themes:
                    return CountVisibleThemes();
                case SettingsSectionKind.Keybindings:
                    return _sessionState.ShowModifiedOnly ? CountModifiedKeybindingRows() : CountVisibleKeybindings();
                case SettingsSectionKind.Editors:
                    return _sessionState.ShowModifiedOnly ? 0 : CountVisibleEditors();
            }

            if (_sessionState.ShowModifiedOnly)
            {
                return 0;
            }

            return MatchesSearch(section.SearchText) ? 1 : 0;
        }

        private int CountVisibleSettingsForIds(params string[] settingIds)
        {
            return _contributionCollectionService.CountVisibleSettingsForIds(_snapshot, IsContributionVisible, settingIds);
        }

        private int CountVisibleContributionsForScope(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            return _contributionCollectionService.CountVisibleContributionsForScope(snapshot, scope, IsContributionVisible);
        }

        private int CountVisibleSections(SettingsNavigationGroupModel group)
        {
            return _searchService.CountVisibleSections(group, GetNormalizedSearchQuery(), _sessionState.ShowModifiedOnly, GetSectionVisibleItemCount);
        }

        private int CountVisibleThemes()
        {
            if (_snapshot == null)
            {
                return 0;
            }

            if (_sessionState.ShowModifiedOnly)
            {
                return IsThemeModified() ? 1 : 0;
            }

            var count = 0;
            for (var i = 0; i < _snapshot.Themes.Count; i++)
            {
                if (MatchesThemeQuery(_snapshot.Themes[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsThemeModified()
        {
            var loadedThemeId = _shellState != null && _shellState.Settings != null
                ? _shellState.Settings.ThemeId
                : "cortex.vs-dark";
            return !string.Equals(loadedThemeId ?? "cortex.vs-dark", _selectedThemeId ?? "cortex.vs-dark", StringComparison.OrdinalIgnoreCase);
        }

        private int CountVisibleEditors()
        {
            if (_snapshot == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < _snapshot.Editors.Count; i++)
            {
                if (MatchesEditorQuery(_snapshot.Editors[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountVisibleKeybindings()
        {
            var definitions = _editorKeybindingService.GetCommandBindings();
            if (definitions == null)
            {
                return 0;
            }

            var count = 1;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (MatchesKeybindingQuery(definitions[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountModifiedKeybindingRows()
        {
            var count = 0;
            if (IsUndoHistoryModified())
            {
                count++;
            }

            var settings = _shellState != null ? _shellState.Settings : null;
            var definitions = _editorKeybindingService.GetCommandBindings();
            if (settings == null || definitions == null)
            {
                return count;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var binding = GetEditableBinding(settings, definitions[i]);
                var defaultBinding = definitions[i].DefaultBinding;
                if (binding == null || defaultBinding == null)
                {
                    continue;
                }

                if (IsBindingModified(settings, definitions[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsUndoHistoryModified()
        {
            var undoLimit = GetTextValue("editor.undo.limit", "128");
            return !string.Equals(undoLimit ?? "128", "128", StringComparison.Ordinal);
        }

        private bool IsBindingModified(CortexSettings settings, EditorCommandBindingDefinition definition)
        {
            if (settings == null || definition == null || definition.DefaultBinding == null)
            {
                return false;
            }

            var binding = GetEditableBinding(settings, definition);
            if (binding == null)
            {
                return false;
            }

            return !string.Equals(binding.Key ?? string.Empty, definition.DefaultBinding.Key ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                binding.Control != definition.DefaultBinding.Control ||
                binding.Shift != definition.DefaultBinding.Shift ||
                binding.Alt != definition.DefaultBinding.Alt;
        }

        private string BuildSearchSummary(SettingsDocumentModel document)
        {
            return _searchService.BuildSearchSummary(document, GetNormalizedSearchQuery(), _sessionState.ShowModifiedOnly, GetSectionVisibleItemCount);
        }

        private void DrawActiveSectionBanner(SettingsDocumentModel document, string renderActiveSectionId)
        {
            var section = FindSection(document, renderActiveSectionId);
            if (section == null)
            {
                return;
            }

            ImguiWorkbenchLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(section.Title, GUILayout.Height(22f));
                GUILayout.Label((string.IsNullOrEmpty(section.GroupTitle) ? "Settings" : section.GroupTitle) +
                    "  |  " +
                    GetSectionVisibleItemCount(section).ToString(CultureInfo.InvariantCulture) +
                    " item(s)", GUILayout.ExpandWidth(true));
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }, GUILayout.Height(48f), GUILayout.ExpandWidth(true));
        }

        private SettingsSectionModel FindSection(SettingsDocumentModel document, string sectionId)
        {
            if (document == null || string.IsNullOrEmpty(sectionId))
            {
                return null;
            }

            var orderedSections = GetOrderedSections(document);
            for (var i = 0; i < orderedSections.Count; i++)
            {
                if (orderedSections[i] != null &&
                    string.Equals(orderedSections[i].SectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return orderedSections[i];
                }
            }

            return null;
        }

        private string BuildSectionNavigationLabel(SettingsSectionModel section)
        {
            if (section == null)
            {
                return string.Empty;
            }

            var label = section.Title ?? string.Empty;
            if (IsSearchActive() || _sessionState.ShowModifiedOnly)
            {
                label += " (" + GetSectionVisibleItemCount(section).ToString(CultureInfo.InvariantCulture) + ")";
            }

            return label;
        }

        private bool MatchesContributionQuery(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            return MatchesSearch(BuildContributionSearchText(contribution));
        }

        private bool IsContributionVisible(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            if (_sessionState.ShowModifiedOnly && !IsSettingModified(contribution))
            {
                return false;
            }

            return MatchesContributionQuery(contribution);
        }

        private bool MatchesThemeQuery(ThemeContribution theme)
        {
            if (theme == null)
            {
                return false;
            }

            return MatchesSearch(
                (theme.DisplayName ?? string.Empty) + " " +
                (theme.Description ?? string.Empty) + " " +
                (theme.ThemeId ?? string.Empty));
        }

        private bool MatchesEditorQuery(EditorContribution editor)
        {
            if (editor == null)
            {
                return false;
            }

            return MatchesSearch(
                (editor.DisplayName ?? string.Empty) + " " +
                (editor.ResourceExtension ?? string.Empty) + " " +
                (editor.ContentType ?? string.Empty));
        }

        private bool MatchesKeybindingQuery(EditorCommandBindingDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return MatchesSearch(
                (definition.DisplayName ?? string.Empty) + " " +
                (definition.Description ?? string.Empty) + " " +
                (definition.Category ?? string.Empty) + " " +
                (definition.CommandId ?? string.Empty));
        }

        private bool MatchesSearch(string text)
        {
            return _searchService.MatchesSearch(GetNormalizedSearchQuery(), text);
        }

        private string GetNormalizedSearchQuery()
        {
            return _sessionService.GetNormalizedSearchQuery(_sessionState, _searchService);
        }

        private void HandleSearchQueryChanged(SettingsDocumentModel document)
        {
            _sessionService.HandleSearchQueryChanged(_sessionState, _searchService, document, GetSectionVisibleItemCount);
        }

        private bool ShouldDefaultGroupExpanded(string groupId)
        {
            if (_collapsedNavigationGroups.Contains(groupId))
            {
                return false;
            }

            var serialized = _shellState != null && _shellState.Settings != null
                ? _shellState.Settings.SettingsCollapsedGroupIds
                : string.Empty;
            if (string.IsNullOrEmpty(serialized))
            {
                return false;
            }

            return true;
        }

        private bool IsNavigationGroupExpandedForDisplay(SettingsNavigationGroupModel group, bool storedExpanded, string activeSectionId)
        {
            if (!string.IsNullOrEmpty(GetNormalizedSearchQuery()))
            {
                return true;
            }

            return storedExpanded || GetActiveSection(group, activeSectionId) != null;
        }

        private SettingsSectionModel GetActiveSection(SettingsNavigationGroupModel group, string activeSectionId)
        {
            if (group == null || string.IsNullOrEmpty(activeSectionId))
            {
                return null;
            }

            for (var i = 0; i < group.Sections.Count; i++)
            {
                if (group.Sections[i] != null &&
                    string.Equals(group.Sections[i].SectionId, activeSectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return group.Sections[i];
                }
            }

            return null;
        }

        private void RevealActiveNavigationTarget(SettingsDocumentModel document)
        {
            var targetId = GetNavigationTargetId(document);
            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            float anchor;
            if (!_navigationAnchors.TryGetValue(targetId, out anchor))
            {
                return;
            }

            var top = _navigationScroll.y;
            var bottom = _navigationScroll.y + 220f;
            if (anchor < top + 28f || anchor > bottom)
            {
                _navigationScroll.y = Mathf.Max(0f, anchor - 36f);
            }
        }

        private string GetNavigationTargetId(SettingsDocumentModel document)
        {
            if (document == null || string.IsNullOrEmpty(_sessionState.ActiveSectionId))
            {
                return string.Empty;
            }

            for (var i = 0; i < document.Groups.Count; i++)
            {
                var group = document.Groups[i];
                var activeSection = GetActiveSection(group, _sessionState.ActiveSectionId);
                if (activeSection == null)
                {
                    continue;
                }

                bool expanded;
                if (!_navigationGroupExpanded.TryGetValue(group.GroupId, out expanded))
                {
                    expanded = ShouldDefaultGroupExpanded(group.GroupId);
                }

                return IsNavigationGroupExpandedForDisplay(group, expanded, _sessionState.ActiveSectionId)
                    ? activeSection.SectionId
                    : group.GroupId;
            }

            return string.Empty;
        }

        private void LoadNavigationGroupState(CortexSettings settings)
        {
            var serialized = settings != null ? settings.SettingsCollapsedGroupIds : string.Empty;
            if (string.IsNullOrEmpty(serialized))
            {
                return;
            }

            var parts = serialized.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                _collapsedNavigationGroups.Add(parts[i]);
            }
        }

        private void PersistNavigationGroupState()
        {
            if (_shellState == null || _shellState.Settings == null)
            {
                return;
            }

            var collapsed = new List<string>();
            _collapsedNavigationGroups.Clear();
            foreach (var pair in _navigationGroupExpanded)
            {
                if (!pair.Value)
                {
                    collapsed.Add(pair.Key);
                    _collapsedNavigationGroups.Add(pair.Key);
                }
            }

            _shellState.Settings.SettingsCollapsedGroupIds = string.Join(";", collapsed.ToArray());
        }

        private void UpdateActiveSection(SettingsDocumentModel document)
        {
            var orderedSections = GetOrderedSections(document);
            if (orderedSections.Count == 0 || _sectionAnchors.Count == 0)
            {
                return;
            }

            var threshold = _contentScroll.y + 32f;
            var active = string.Empty;
            var bestAnchor = float.MinValue;
            for (var i = 0; i < orderedSections.Count; i++)
            {
                var section = orderedSections[i];
                if (!IsSectionVisible(section))
                {
                    continue;
                }

                float anchor;
                if (!_sectionAnchors.TryGetValue(section.SectionId, out anchor))
                {
                    continue;
                }

                if (anchor <= threshold && anchor >= bestAnchor)
                {
                    bestAnchor = anchor;
                    active = section.SectionId;
                }
            }

            if (!string.IsNullOrEmpty(active))
            {
                _sessionState.ActiveSectionId = active;
            }
        }

        private string ResolveRenderActiveSectionId(SettingsDocumentModel document)
        {
            return _searchService.ResolveRenderActiveSectionId(document, _sessionState.ActiveSectionId, GetNormalizedSearchQuery(), _sessionState.ShowModifiedOnly, GetSectionVisibleItemCount);
        }

        private void CommitNavigationInteraction(NavigationInteraction interaction)
        {
            if (interaction == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(interaction.ToggledGroupId))
            {
                bool expanded;
                if (!_navigationGroupExpanded.TryGetValue(interaction.ToggledGroupId, out expanded))
                {
                    expanded = ShouldDefaultGroupExpanded(interaction.ToggledGroupId);
                }

                _navigationGroupExpanded[interaction.ToggledGroupId] = !expanded;
                PersistNavigationGroupState();
            }

            if (!string.IsNullOrEmpty(interaction.RequestedSectionId))
            {
                _sessionService.RequestSection(_sessionState, interaction.RequestedSectionId);
            }
        }

        private void FinalizeNavigationState(SettingsDocumentModel document)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            CommitSearchQuery();
            var jumpedToSection = ApplyPendingSectionJump();
            if (!jumpedToSection)
            {
                UpdateActiveSection(document);
            }

            RevealActiveNavigationTarget(document);
            PersistUiState();
        }

        private bool ApplyPendingSectionJump()
        {
            if (string.IsNullOrEmpty(_sessionState.PendingSectionJumpId))
            {
                return false;
            }

            float anchor;
            if (_sectionAnchors.TryGetValue(_sessionState.PendingSectionJumpId, out anchor))
            {
                _contentScroll.y = Mathf.Max(0f, anchor - 8f);
                _sessionState.PendingSectionJumpId = string.Empty;
                return true;
            }

            return false;
        }

        private void CommitSearchQuery()
        {
            var previousQuery = _sessionState.AppliedSearchQuery ?? string.Empty;
            _sessionService.CommitSearchQuery(_sessionState);
            if (string.Equals(previousQuery, _sessionState.AppliedSearchQuery ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }
        }

        private void PersistUiState()
        {
            if (_shellState == null)
            {
                return;
            }

            if (_shellState.Settings == null)
            {
                _shellState.Settings = new CortexSettings();
            }

            _sessionService.Persist(_sessionState, _shellState.Settings, _navigationScroll.y, _contentScroll.y);
        }

        private static bool IsThemeSetting(SettingContribution contribution)
        {
            return contribution != null && string.Equals(contribution.SettingId, nameof(CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetContributionScope(SettingContribution contribution)
        {
            return string.IsNullOrEmpty(contribution != null ? contribution.Scope : string.Empty)
                ? "General"
                : contribution.Scope;
        }

        private static string BuildScopeDescription(string scope)
        {
            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase))
            {
                return "Additional workspace and project discovery settings.";
            }
            if (string.Equals(scope, "Logs", StringComparison.OrdinalIgnoreCase))
            {
                return "Live log feed settings and file tail behavior.";
            }
            if (string.Equals(scope, "Decompiler", StringComparison.OrdinalIgnoreCase))
            {
                return "Decompiler executable and cache behavior.";
            }
            if (string.Equals(scope, "Language Service", StringComparison.OrdinalIgnoreCase))
            {
                return "External language worker configuration and request limits.";
            }
            if (scope.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                return "AI completion settings contributed under the " + scope + " scope.";
            }
            if (string.Equals(scope, "Editing", StringComparison.OrdinalIgnoreCase))
            {
                return "Editing permissions and write-back behavior.";
            }
            if (string.Equals(scope, "Layout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Window", StringComparison.OrdinalIgnoreCase))
            {
                return "Workbench layout and shell window persistence settings.";
            }
            if (string.Equals(scope, "Build", StringComparison.OrdinalIgnoreCase))
            {
                return "Build defaults and execution limits.";
            }

            return "Settings contributed under the " + scope + " scope.";
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

        private static string BuildKeywordsText(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
            {
                return string.Empty;
            }

            return string.Join(" ", keywords);
        }

        private string BuildScopeSearchText(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            var text = scope + " " + BuildScopeDescription(scope) + " ";
            var sectionContribution = FindSettingSectionContribution(snapshot, scope);
            if (sectionContribution != null)
            {
                text +=
                    (sectionContribution.GroupTitle ?? string.Empty) + " " +
                    (sectionContribution.SectionTitle ?? string.Empty) + " " +
                    (sectionContribution.Description ?? string.Empty) + " " +
                    BuildKeywordsText(sectionContribution.Keywords) + " ";
            }

            if (snapshot == null)
            {
                return text;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (!ShouldRenderContribution(scope, contribution))
                {
                    continue;
                }

                text += BuildContributionSearchText(contribution) + " ";
            }

            return text;
        }

        private static string ClassifySectionGroupId(string scope)
        {
            if (scope.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                return "ai";
            }
            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase))
            {
                return "workspace";
            }
            if (string.Equals(scope, "Logs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Decompiler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Language Service", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Build", StringComparison.OrdinalIgnoreCase))
            {
                return "tooling";
            }
            if (string.Equals(scope, "Editing", StringComparison.OrdinalIgnoreCase))
            {
                return "editor";
            }
            if (string.Equals(scope, "Appearance", StringComparison.OrdinalIgnoreCase))
            {
                return "appearance";
            }
            if (string.Equals(scope, "Layout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Window", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "General", StringComparison.OrdinalIgnoreCase))
            {
                return "shell";
            }

            var scopePrefix = GetScopePrefix(scope);
            return string.IsNullOrEmpty(scopePrefix) ? "extensions" : "ext." + scopePrefix.ToLowerInvariant();
        }

        private static string ClassifySectionGroupTitle(string scope)
        {
            if (scope.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                return "AI";
            }
            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase))
            {
                return "Workspace";
            }
            if (string.Equals(scope, "Logs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Decompiler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Language Service", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Build", StringComparison.OrdinalIgnoreCase))
            {
                return "Tooling";
            }
            if (string.Equals(scope, "Editing", StringComparison.OrdinalIgnoreCase))
            {
                return "Editor";
            }
            if (string.Equals(scope, "Appearance", StringComparison.OrdinalIgnoreCase))
            {
                return "Appearance";
            }
            if (string.Equals(scope, "Layout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Window", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "General", StringComparison.OrdinalIgnoreCase))
            {
                return "Shell";
            }

            var scopePrefix = GetScopePrefix(scope);
            return string.IsNullOrEmpty(scopePrefix) ? "Extensions" : scopePrefix;
        }

        private static string GetScopePrefix(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return string.Empty;
            }

            var separatorIndex = scope.IndexOf(" - ", StringComparison.Ordinal);
            return separatorIndex > 0 ? scope.Substring(0, separatorIndex) : scope;
        }

        private static int GetGroupSortOrder(string groupId)
        {
            switch (groupId)
            {
                case "workspace": return 0;
                case "tooling": return 10;
                case "ai": return 20;
                case "appearance": return 30;
                case "editor": return 40;
                case "shell": return 50;
                case "extensions": return 60;
                default:
                    return groupId != null && groupId.StartsWith("ext.", StringComparison.OrdinalIgnoreCase) ? 60 : 70;
            }
        }

        private static SettingContribution FindSettingContribution(WorkbenchPresentationSnapshot snapshot, string settingId)
        {
            if (snapshot == null || string.IsNullOrEmpty(settingId))
            {
                return null;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (contribution != null && string.Equals(contribution.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    return contribution;
                }
            }

            return null;
        }

        private static SettingSectionContribution FindSettingSectionContribution(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            if (snapshot == null || string.IsNullOrEmpty(scope) || snapshot.SettingSections == null)
            {
                return null;
            }

            for (var i = 0; i < snapshot.SettingSections.Count; i++)
            {
                var contribution = snapshot.SettingSections[i];
                if (contribution != null && string.Equals(contribution.Scope, scope, StringComparison.OrdinalIgnoreCase))
                {
                    return contribution;
                }
            }

            return null;
        }

        private string GetLoadedModDraftValue(LoadedModInfo mod)
        {
            return _loadedModLinkService.GetDraftValue(_draftState, mod, _projectCatalog);
        }

        private void LinkLoadedModToSource(LoadedModInfo mod, CortexShellState state)
        {
            _loadedModLinkService.LinkLoadedModToSource(_draftState, mod, _projectCatalog, _workspaceService, state);
        }

        private void ApplyLoadedModMappings(CortexShellState state)
        {
            _loadedModLinkService.ApplyLoadedModMappings(_draftState, _projectCatalog, _workspaceService, _loadedModCatalog, state);
        }

        private sealed class NavigationInteraction
        {
            public string RequestedSectionId;
            public string ToggledGroupId;

            public void RequestSectionReveal(string sectionId)
            {
                RequestedSectionId = sectionId ?? string.Empty;
            }

            public void ToggleGroup(string groupId)
            {
                ToggledGroupId = groupId ?? string.Empty;
            }
        }

    }
}
