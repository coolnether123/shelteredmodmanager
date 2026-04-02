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
using UnityEngine;
using Cortex.Services.Editor.Input;
using Cortex.Services.Onboarding;

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
        private readonly Dictionary<string, string> _textValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _toggleValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _loadedSerializedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SettingValidationResult> _validationResults = new Dictionary<string, SettingValidationResult>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector2> _multilineEditorScrolls = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        private bool _preserveContentScrollForNestedEditor;
        private readonly Dictionary<string, string> _loadedModPathDrafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _sectionAnchors = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _navigationAnchors = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _navigationGroupExpanded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _collapsedNavigationGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _selectedThemeId = string.Empty;
        private string _activeSectionId = WorkspaceOverviewSectionId;
        private string _pendingSectionJumpId = string.Empty;
        private string _settingsSearchQuery = string.Empty;
        private string _appliedSettingsSearchQuery = string.Empty;
        private string _lastNormalizedSearchQuery = string.Empty;
        private string _openSettingActionMenuId = string.Empty;
        private bool _showModifiedOnly;
        private IProjectCatalog _projectCatalog;
        private IProjectWorkspaceService _workspaceService;
        private ILoadedModCatalog _loadedModCatalog;
        private IPathInteractionService _pathInteractionService;
        private WorkbenchPresentationSnapshot _snapshot;
        private CortexShellState _shellState;
        private IWorkbenchUiSurface _uiSurface;
        private readonly IEditorKeybindingService _editorKeybindingService = new EditorKeybindingService();
        private readonly CortexLoadedModSourceLinkService _loadedModSourceLinkService = new CortexLoadedModSourceLinkService();

        private IWorkbenchUiSurface UiSurface
        {
            get { return _uiSurface ?? CortexUi.DefaultSurface; }
        }

        public void Draw(
            ICortexSettingsStore settingsStore,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog,
            IPathInteractionService pathInteractionService,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state,
            IWorkbenchUiSurface uiSurface)
        {
            _projectCatalog = projectCatalog;
            _workspaceService = workspaceService;
            _loadedModCatalog = loadedModCatalog;
            _pathInteractionService = pathInteractionService;
            _snapshot = snapshot;
            _shellState = state;
            _uiSurface = uiSurface ?? CortexUi.DefaultSurface;
            EnsureLoaded(snapshot, themeState, state);

            var document = BuildDocument(snapshot);
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
            CortexIdeLayout.DrawGroup(null, delegate
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
                    Apply(snapshot, themeState, state);
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
                    state.Settings = new CortexSettings();
                    if (themeState != null)
                    {
                        themeState.ThemeId = state.Settings.ThemeId;
                    }

                    _loaded = false;
                    _contentScroll = Vector2.zero;
                    _pendingSectionJumpId = WorkspaceOverviewSectionId;
                    EnsureLoaded(snapshot, themeState, state);
                    state.StatusMessage = "Reset settings fields to defaults.";
                }

                GUILayout.EndHorizontal();
            }, GUILayout.Height(56f), GUILayout.ExpandWidth(true));
        }

        private void DrawSearchBar(SettingsDocument document)
        {
            _settingsSearchQuery = UiSurface
                .DrawSearchToolbar("Search properties", _settingsSearchQuery, 42f, true);

            CortexIdeLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                _showModifiedOnly = GUILayout.Toggle(_showModifiedOnly, "Show modified only", GUILayout.Width(140f));
                GUILayout.Space(8f);
                GUILayout.Label(BuildSearchSummary(document), GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
        }

        private void DrawNavigation(SettingsDocument document, string renderActiveSectionId, NavigationInteraction interaction)
        {
            CortexIdeLayout.DrawGroup(null, delegate
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

        private void DrawNavigationGroup(SettingsNavigationGroup group, string renderActiveSectionId, NavigationInteraction interaction)
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
            if (searchActive || _showModifiedOnly)
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

        private void DrawNavigationButton(SettingsSection section, float indent, string renderActiveSectionId, NavigationInteraction interaction)
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

        private void DrawCollapsedActiveSection(SettingsSection activeSection)
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
            SettingsDocument document,
            string renderActiveSectionId)
        {
            CortexIdeLayout.DrawGroup(null, delegate
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
            SettingsSection section,
            ICortexSettingsStore settingsStore,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            UiSurface.DrawSectionHeader(section.Title, section.Description);
            if (section.DrawBody != null)
            {
                section.DrawBody(settingsStore, snapshot, themeState, state);
            }
            GUILayout.EndVertical();

            var rect = GUILayoutUtility.GetLastRect();
            _sectionAnchors[section.SectionId] = rect.y;
            GUILayout.Space(18f);
        }

        private SettingsDocument BuildDocument(WorkbenchPresentationSnapshot snapshot)
        {
            var document = new SettingsDocument();
            document.Sections.Add(CreateWorkspaceOverviewSection());
            document.Sections.Add(CreateWorkspacePathsSection());
            if (HasVisibleContributionsForScope(snapshot, "Workspace"))
            {
                document.Sections.Add(CreateWorkspaceSettingsSection(snapshot));
            }
            document.Sections.Add(CreateWorkspaceModLinksSection());
            document.Sections.Add(CreateWorkspaceCurrentPathsSection());
            var contributedSections = new List<SettingsSection>();

            var seenScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    var contribution = snapshot.Settings[i];
                    if (contribution == null || string.IsNullOrEmpty(contribution.SettingId) || IsThemeSetting(contribution))
                    {
                        continue;
                    }

                    var scope = GetContributionScope(contribution);
                    if (!seenScopes.Add(scope) || !HasVisibleContributionsForScope(snapshot, scope))
                    {
                        continue;
                    }

                    contributedSections.Add(CreateContributionSection(snapshot, scope));
                }
            }

            contributedSections.Sort(delegate(SettingsSection left, SettingsSection right)
            {
                var order = GetGroupSortOrder(left != null ? left.GroupId : string.Empty)
                    .CompareTo(GetGroupSortOrder(right != null ? right.GroupId : string.Empty));
                if (order != 0)
                {
                    return order;
                }

                order = (left != null ? left.SortOrder : int.MaxValue)
                    .CompareTo(right != null ? right.SortOrder : int.MaxValue);
                if (order != 0)
                {
                    return order;
                }

                return string.Compare(
                    left != null ? left.Title : string.Empty,
                    right != null ? right.Title : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < contributedSections.Count; i++)
            {
                document.Sections.Add(contributedSections[i]);
            }

            document.Sections.Add(CreateOnboardingSection());
            document.Sections.Add(CreateThemesSection());
            document.Sections.Add(CreateKeybindingsSection());
            document.Sections.Add(CreateEditorsSection());
            document.Sections.Add(CreateActionsSection());
            BuildNavigationGroups(document);
            SynchronizeDocumentSectionOrder(document);
            return document;
        }

        private void BuildNavigationGroups(SettingsDocument document)
        {
            var groupsById = new Dictionary<string, SettingsNavigationGroup>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < document.Sections.Count; i++)
            {
                var section = document.Sections[i];
                var groupId = string.IsNullOrEmpty(section.GroupId) ? "general" : section.GroupId;
                SettingsNavigationGroup group;
                if (!groupsById.TryGetValue(groupId, out group))
                {
                    group = new SettingsNavigationGroup(groupId, section.GroupTitle, GetGroupSortOrder(groupId));
                    groupsById[groupId] = group;
                    document.Groups.Add(group);
                }

                group.Sections.Add(section);
            }

            for (var i = 0; i < document.Groups.Count; i++)
            {
                document.Groups[i].Sections.Sort(delegate(SettingsSection left, SettingsSection right)
                {
                    var order = (left != null ? left.SortOrder : int.MaxValue)
                        .CompareTo(right != null ? right.SortOrder : int.MaxValue);
                    return order != 0
                        ? order
                        : string.Compare(left != null ? left.Title : string.Empty, right != null ? right.Title : string.Empty, StringComparison.OrdinalIgnoreCase);
                });
            }

            document.Groups.Sort(delegate(SettingsNavigationGroup left, SettingsNavigationGroup right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static void SynchronizeDocumentSectionOrder(SettingsDocument document)
        {
            if (document == null || document.Groups.Count == 0)
            {
                return;
            }

            var orderedSections = new List<SettingsSection>();
            for (var i = 0; i < document.Groups.Count; i++)
            {
                var group = document.Groups[i];
                if (group == null || group.Sections == null)
                {
                    continue;
                }

                for (var j = 0; j < group.Sections.Count; j++)
                {
                    if (group.Sections[j] != null)
                    {
                        orderedSections.Add(group.Sections[j]);
                    }
                }
            }

            document.Sections.Clear();
            for (var i = 0; i < orderedSections.Count; i++)
            {
                document.Sections.Add(orderedSections[i]);
            }
        }

        private static List<SettingsSection> GetOrderedSections(SettingsDocument document)
        {
            var orderedSections = new List<SettingsSection>();
            if (document == null)
            {
                return orderedSections;
            }

            var seenSectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document.Groups != null && document.Groups.Count > 0)
            {
                for (var i = 0; i < document.Groups.Count; i++)
                {
                    var group = document.Groups[i];
                    if (group == null || group.Sections == null)
                    {
                        continue;
                    }

                    for (var j = 0; j < group.Sections.Count; j++)
                    {
                        var section = group.Sections[j];
                        if (section == null)
                        {
                            continue;
                        }

                        var sectionId = section.SectionId ?? string.Empty;
                        if (seenSectionIds.Add(sectionId))
                        {
                            orderedSections.Add(section);
                        }
                    }
                }
            }

            if (orderedSections.Count > 0)
            {
                return orderedSections;
            }

            if (document.Sections == null)
            {
                return orderedSections;
            }

            for (var i = 0; i < document.Sections.Count; i++)
            {
                if (document.Sections[i] != null)
                {
                    orderedSections.Add(document.Sections[i]);
                }
            }

            return orderedSections;
        }

        private SettingsSection CreateWorkspaceOverviewSection()
        {
            return new SettingsSection(
                WorkspaceOverviewSectionId,
                "workspace",
                "Workspace",
                "Workspace",
                "General",
                "Configure where Cortex finds editable sources and how the settings document is organized.",
                "workspace general source setup overview project roots",
                0,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawSourceSetupGuide();
                });
        }

        private SettingsSection CreateWorkspacePathsSection()
        {
            return new SettingsSection(
                SourceSetupSectionId + ".paths",
                "workspace",
                "Workspace",
                "Workspace",
                "Paths",
                "Configure the main folders Cortex uses for source resolution, mod discovery, and reference browsing.",
                "workspace paths folders source roots runtime content reference assemblies additional roots",
                10,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawWorkspacePathEditors(snapshot, state);
                });
        }

        private SettingsSection CreateWorkspaceSettingsSection(WorkbenchPresentationSnapshot snapshot)
        {
            return new SettingsSection(
                SourceSetupSectionId + ".settings",
                "workspace",
                "Workspace",
                "Workspace",
                "Advanced",
                "Additional workspace settings contributed by Cortex modules and extensions.",
                BuildScopeSearchText(snapshot, "Workspace"),
                20,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot innerSnapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawWorkspaceContributionEditors(innerSnapshot, state);
                });
        }

        private SettingsSection CreateWorkspaceModLinksSection()
        {
            return new SettingsSection(
                SourceSetupSectionId + ".mods",
                "workspace",
                "Workspace",
                "Workspace",
                "Loaded Content",
                "Bind active content items to editable source roots so navigation and project discovery can resolve correctly.",
                "workspace loaded content links source links active content source mapping",
                30,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawLoadedModMappings(state);
                });
        }

        private SettingsSection CreateWorkspaceCurrentPathsSection()
        {
            return new SettingsSection(
                SourceSetupSectionId + ".facts",
                "workspace",
                "Workspace",
                "Workspace",
                "Current Values",
                "Review the effective workspace-related paths currently loaded into the shell.",
                "workspace current paths values effective paths facts",
                40,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawQuickFacts(state);
                });
        }

        private SettingsSection CreateContributionSection(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            var sectionContribution = FindSettingSectionContribution(snapshot, scope);
            return new SettingsSection(
                sectionContribution != null && !string.IsNullOrEmpty(sectionContribution.SectionId)
                    ? sectionContribution.SectionId
                    : "scope." + scope.ToLowerInvariant(),
                sectionContribution != null && !string.IsNullOrEmpty(sectionContribution.GroupId)
                    ? sectionContribution.GroupId
                    : ClassifySectionGroupId(scope),
                sectionContribution != null && !string.IsNullOrEmpty(sectionContribution.GroupTitle)
                    ? sectionContribution.GroupTitle
                    : ClassifySectionGroupTitle(scope),
                scope,
                sectionContribution != null && !string.IsNullOrEmpty(sectionContribution.SectionTitle)
                    ? sectionContribution.SectionTitle
                    : scope,
                sectionContribution != null && !string.IsNullOrEmpty(sectionContribution.Description)
                    ? sectionContribution.Description
                    : BuildScopeDescription(scope),
                BuildScopeSearchText(snapshot, scope) + " " + BuildKeywordsText(sectionContribution != null ? sectionContribution.Keywords : null),
                sectionContribution != null ? sectionContribution.SortOrder : 0,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot innerSnapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawContributionScope(innerSnapshot, scope, state);
                });
        }

        private SettingsSection CreateThemesSection()
        {
            return new SettingsSection(
                ThemesSectionId,
                "appearance",
                "Appearance",
                "Appearance",
                "Themes",
                "Manage the registered workbench themes and choose the active shell theme.",
                "appearance themes colors theme catalog shell theme",
                0,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawThemeRegistry(snapshot, themeState, state);
                });
        }

        private SettingsSection CreateOnboardingSection()
        {
            return new SettingsSection(
                OnboardingSectionId,
                "shell",
                "Shell",
                "Shell",
                "Onboarding",
                "Reopen the Cortex onboarding flow and review the current startup selections.",
                "onboarding setup welcome wizard profile layout theme reopen rerun defaults",
                -10,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawOnboardingSettings(state);
                });
        }

        private SettingsSection CreateKeybindingsSection()
        {
            return new SettingsSection(
                KeybindingsSectionId,
                "editor",
                "Editor",
                "Editor",
                "Keybindings",
                "Configure editor shortcuts, multi-caret commands, and undo history.",
                "editor keybindings shortcuts undo history input commands",
                10,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawEditorKeybindings(state);
                });
        }

        private SettingsSection CreateEditorsSection()
        {
            return new SettingsSection(
                EditorsSectionId,
                "editor",
                "Editor",
                "Editor",
                "Registered Editors",
                "Review the editors and content handlers currently available in Cortex.",
                "editor registered editors content handlers extensions content types",
                20,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawEditorRegistry(snapshot);
                });
        }

        private SettingsSection CreateActionsSection()
        {
            return new SettingsSection(
                ActionsSectionId,
                "shell",
                "Shell",
                "Shell",
                "Actions",
                "Convenience actions related to windows, logs, and the shell.",
                "shell actions logs return to editor window actions",
                0,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawActionsPanel(state);
                });
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
            var results = new List<SettingContribution>();
            if (snapshot == null || snapshot.Settings.Count == 0)
            {
                return results;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (!ShouldRenderContribution(scope, contribution) || !IsContributionVisible(contribution))
                {
                    continue;
                }

                results.Add(contribution);
            }

            return results;
        }

        private bool HasVisibleContributionsForScope(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            if (snapshot == null || snapshot.Settings.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                if (ShouldRenderContribution(scope, snapshot.Settings[i]) && IsContributionVisible(snapshot.Settings[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldRenderContribution(string scope, SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId) || IsThemeSetting(contribution))
            {
                return false;
            }

            if (!string.Equals(GetContributionScope(contribution), scope, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase) &&
                IsWorkspacePathContribution(contribution))
            {
                return false;
            }

            return true;
        }

        private static bool IsWorkspacePathContribution(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            return string.Equals(contribution.SettingId, WorkspaceRootSettingId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contribution.SettingId, RuntimeContentRootSettingId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contribution.SettingId, ReferenceAssemblyRootSettingId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contribution.SettingId, AdditionalSourceRootsSettingId, StringComparison.OrdinalIgnoreCase);
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

            _textValues.Clear();
            _toggleValues.Clear();
            _loadedSerializedValues.Clear();
            _validationResults.Clear();
            _loadedModPathDrafts.Clear();
            _navigationGroupExpanded.Clear();
            _collapsedNavigationGroups.Clear();
            _openSettingActionMenuId = string.Empty;
            _lastNormalizedSearchQuery = string.Empty;

            var settings = state.Settings ?? new CortexSettings();
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    LoadContributionValue(snapshot.Settings[i], settings);
                }
            }

            _textValues["editor.undo.limit"] = settings.EditorUndoHistoryLimit.ToString();
            _selectedThemeId = !string.IsNullOrEmpty(settings.ThemeId)
                ? settings.ThemeId
                : themeState != null && !string.IsNullOrEmpty(themeState.ThemeId)
                    ? themeState.ThemeId
                    : "cortex.vs-dark";
            LoadNavigationGroupState(settings);
            _settingsSearchQuery = settings.SettingsSearchQuery ?? string.Empty;
            _appliedSettingsSearchQuery = settings.SettingsSearchQuery ?? string.Empty;
            _showModifiedOnly = settings.SettingsShowModifiedOnly;
            _navigationScroll.y = Mathf.Max(0f, settings.SettingsNavigationScrollY);
            _contentScroll.y = Mathf.Max(0f, settings.SettingsContentScrollY);
            _activeSectionId = !string.IsNullOrEmpty(settings.SettingsActiveSectionId)
                ? settings.SettingsActiveSectionId
                : WorkspaceOverviewSectionId;
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
                    if (_showModifiedOnly && !string.Equals(snapshot.Themes[i].ThemeId, _selectedThemeId, StringComparison.OrdinalIgnoreCase))
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
                var showUndoLimit = !_showModifiedOnly || IsUndoHistoryModified();
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

                if (_showModifiedOnly && !IsBindingModified(state.Settings, bindings[i]))
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
                DrawSettingTag("Modified", CortexIdeLayout.GetWarningColor());
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
                    state.Logs.ShowDetachedWindow = true;
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
                DrawSettingTag("Match", CortexIdeLayout.GetAccentColor());
            }
            if (contribution != null && IsSettingModified(contribution))
            {
                DrawSettingTag("Modified", CortexIdeLayout.GetWarningColor());
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
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            serializedValue = NormalizeSerializedValue(contribution, serializedValue);
            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                _toggleValues[contribution.SettingId] = string.Equals(serializedValue, "true", StringComparison.OrdinalIgnoreCase);
                return;
            }

            _textValues[contribution.SettingId] = serializedValue;
        }

        private bool IsSearchActive()
        {
            return !string.IsNullOrEmpty(GetNormalizedSearchQuery());
        }

        private bool IsSettingModified(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return false;
            }

            string loadedValue;
            if (!_loadedSerializedValues.TryGetValue(contribution.SettingId, out loadedValue))
            {
                loadedValue = ReadPersistedContributionValue(contribution, _shellState != null ? _shellState.Settings : null);
                _loadedSerializedValues[contribution.SettingId] = loadedValue;
            }

            return !string.Equals(
                NormalizeSerializedValue(contribution, loadedValue),
                GetSerializedDraftValue(contribution),
                StringComparison.Ordinal);
        }

        private SettingValidationResult GetValidationResult(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return null;
            }

            var serializedValue = GetSerializedDraftValue(contribution);
            var result = contribution.ValidateValue != null
                ? contribution.ValidateValue(serializedValue)
                : BuildDefaultValidationResult(contribution, serializedValue);

            _validationResults[contribution.SettingId] = result ?? new SettingValidationResult
            {
                Severity = SettingValidationSeverity.None,
                Message = string.Empty
            };

            return result;
        }

        private SettingValidationResult BuildDefaultValidationResult(SettingContribution contribution, string serializedValue)
        {
            if (contribution == null)
            {
                return null;
            }

            if (contribution.IsRequired && IsNullOrWhiteSpaceCompat(serializedValue))
            {
                return CreateValidation(SettingValidationSeverity.Error, "A value is required.");
            }

            if (IsNullOrWhiteSpaceCompat(serializedValue))
            {
                return null;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Integer:
                    int integerValue;
                    if (!int.TryParse(serializedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                    {
                        return CreateValidation(SettingValidationSeverity.Error, "Enter a valid integer value.");
                    }
                    break;
                case SettingValueKind.Float:
                    float floatValue;
                    if (!float.TryParse(serializedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                    {
                        return CreateValidation(SettingValidationSeverity.Error, "Enter a valid numeric value.");
                    }
                    break;
            }

            var options = contribution.Options ?? new SettingChoiceOption[0];
            if (options.Length > 0 && FindChoiceIndex(options, serializedValue) < 0)
            {
                return CreateValidation(SettingValidationSeverity.Error, "Select one of the registered values.");
            }

            if (GetSettingEditorKind(contribution) == SettingEditorKind.Path &&
                !Directory.Exists(serializedValue) &&
                !File.Exists(serializedValue))
            {
                return CreateValidation(SettingValidationSeverity.Warning, "The path does not exist on disk.");
            }

            if (LooksLikeUrlSetting(contribution))
            {
                Uri uri;
                if (!Uri.TryCreate(serializedValue, UriKind.Absolute, out uri))
                {
                    return CreateValidation(SettingValidationSeverity.Warning, "Enter a valid absolute URL.");
                }
            }

            return null;
        }

        private static bool LooksLikeUrlSetting(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            return EndsWithOrdinalIgnoreCase(contribution.SettingId, "Url") ||
                EndsWithOrdinalIgnoreCase(contribution.SettingId, "Uri") ||
                StartsWithOrdinalIgnoreCase(contribution.PlaceholderText, "http://") ||
                StartsWithOrdinalIgnoreCase(contribution.PlaceholderText, "https://");
        }

        private static bool EndsWithOrdinalIgnoreCase(string value, string suffix)
        {
            return !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(suffix) &&
                value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWithOrdinalIgnoreCase(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(prefix) &&
                value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static SettingValidationResult CreateValidation(SettingValidationSeverity severity, string message)
        {
            return new SettingValidationResult
            {
                Severity = severity,
                Message = message ?? string.Empty
            };
        }

        private static bool IsNullOrWhiteSpaceCompat(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
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
                    return CortexIdeLayout.GetErrorColor();
                case SettingValidationSeverity.Warning:
                    return CortexIdeLayout.GetWarningColor();
                case SettingValidationSeverity.Info:
                    return CortexIdeLayout.GetAccentColor();
                case SettingValidationSeverity.None:
                default:
                    return CortexIdeLayout.GetTextColor();
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
            var swatchColor = CortexIdeLayout.ParseColor(hex, Color.white);
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

        private void Apply(WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            if (state.Settings == null)
            {
                state.Settings = new CortexSettings();
            }

            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    var contribution = snapshot.Settings[i];
                    if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
                    {
                        continue;
                    }

                    ApplyContributionValue(contribution, state.Settings);
                }
            }

            state.Settings.ThemeId = string.IsNullOrEmpty(_selectedThemeId) ? "cortex.vs-dark" : _selectedThemeId;
            state.Settings.SettingsActiveSectionId = _activeSectionId ?? string.Empty;
            state.Settings.SettingsNavigationScrollY = _navigationScroll.y;
            state.Settings.SettingsContentScrollY = _contentScroll.y;
            state.Settings.SettingsSearchQuery = _appliedSettingsSearchQuery ?? string.Empty;
            state.Settings.SettingsShowModifiedOnly = _showModifiedOnly;
            if (themeState != null)
            {
                themeState.ThemeId = state.Settings.ThemeId;
            }
        }

        private void LoadContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            var serializedValue = ReadPersistedContributionValue(contribution, settings);
            _loadedSerializedValues[contribution.SettingId] = serializedValue;
            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                _toggleValues[contribution.SettingId] = string.Equals(serializedValue, "true", StringComparison.OrdinalIgnoreCase);
                return;
            }

            _textValues[contribution.SettingId] = serializedValue;
        }

        private void ApplyContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            WritePersistedContributionValue(contribution, settings, GetSerializedDraftValue(contribution));
        }

        private static FieldInfo GetSettingField(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return null;
            }

            return typeof(CortexSettings).GetField(contribution.SettingId, BindingFlags.Public | BindingFlags.Instance);
        }

        private string GetDefaultSerializedValue(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            if (contribution.ReadDefaultValue != null)
            {
                return NormalizeSerializedValue(contribution, contribution.ReadDefaultValue());
            }

            return NormalizeSerializedValue(contribution, contribution.DefaultValue);
        }

        private string ReadPersistedContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            if (contribution.ReadSettingsValue != null)
            {
                return NormalizeSerializedValue(contribution, contribution.ReadSettingsValue(settings));
            }

            if (contribution.ReadValue != null)
            {
                return NormalizeSerializedValue(contribution, contribution.ReadValue());
            }

            var field = GetSettingField(contribution);
            if (field == null)
            {
                return ReadModuleSettingValue(settings, contribution.SettingId, GetDefaultSerializedValue(contribution));
            }

            if (settings == null)
            {
                return GetDefaultSerializedValue(contribution);
            }

            var value = field.GetValue(settings);
            if (value == null)
            {
                return GetDefaultSerializedValue(contribution);
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    return NormalizeSerializedValue(contribution, ((bool)value) ? "true" : "false");
                case SettingValueKind.Integer:
                    return NormalizeSerializedValue(contribution, ((int)value).ToString(CultureInfo.InvariantCulture));
                case SettingValueKind.Float:
                    return NormalizeSerializedValue(contribution, ((float)value).ToString(CultureInfo.InvariantCulture));
                case SettingValueKind.String:
                default:
                    return NormalizeSerializedValue(contribution, value.ToString());
            }
        }

        private void WritePersistedContributionValue(SettingContribution contribution, CortexSettings settings, string serializedValue)
        {
            if (contribution == null)
            {
                return;
            }

            serializedValue = NormalizeSerializedValue(contribution, serializedValue);
            if (contribution.WriteSettingsValue != null)
            {
                contribution.WriteSettingsValue(settings, serializedValue);
                return;
            }

            if (contribution.WriteValue != null)
            {
                contribution.WriteValue(serializedValue);
                return;
            }

            var field = GetSettingField(contribution);
            if (field == null)
            {
                WriteModuleSettingValue(settings, contribution.SettingId, serializedValue);
                return;
            }

            if (settings == null)
            {
                return;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    field.SetValue(settings, string.Equals(serializedValue, "true", StringComparison.OrdinalIgnoreCase));
                    break;
                case SettingValueKind.Integer:
                    field.SetValue(settings, ParseInt(serializedValue, ParseInt(GetDefaultSerializedValue(contribution), (int)field.GetValue(settings))));
                    break;
                case SettingValueKind.Float:
                    field.SetValue(settings, ParseFloat(serializedValue, ParseFloat(GetDefaultSerializedValue(contribution), (float)field.GetValue(settings))));
                    break;
                case SettingValueKind.String:
                default:
                    field.SetValue(settings, serializedValue);
                    break;
            }
        }

        private static string ReadModuleSettingValue(CortexSettings settings, string settingId, string fallback)
        {
            if (settings == null || string.IsNullOrEmpty(settingId) || settings.ModuleSettings == null)
            {
                return fallback ?? string.Empty;
            }

            for (var i = 0; i < settings.ModuleSettings.Length; i++)
            {
                var entry = settings.ModuleSettings[i];
                if (entry != null && string.Equals(entry.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value ?? string.Empty;
                }
            }

            return fallback ?? string.Empty;
        }

        private static void WriteModuleSettingValue(CortexSettings settings, string settingId, string serializedValue)
        {
            if (settings == null || string.IsNullOrEmpty(settingId))
            {
                return;
            }

            var entries = new List<ModuleSettingValue>();
            if (settings.ModuleSettings != null)
            {
                for (var i = 0; i < settings.ModuleSettings.Length; i++)
                {
                    if (settings.ModuleSettings[i] != null)
                    {
                        entries.Add(settings.ModuleSettings[i]);
                    }
                }
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    entries[i].Value = serializedValue ?? string.Empty;
                    settings.ModuleSettings = entries.ToArray();
                    return;
                }
            }

            entries.Add(new ModuleSettingValue
            {
                SettingId = settingId,
                Value = serializedValue ?? string.Empty
            });
            settings.ModuleSettings = entries.ToArray();
        }

        private string GetSerializedDraftValue(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                return GetToggleValue(contribution) ? "true" : "false";
            }

            return NormalizeSerializedValue(contribution, GetTextValue(contribution.SettingId, GetDefaultSerializedValue(contribution)));
        }

        private static string NormalizeSerializedValue(SettingContribution contribution, string rawValue)
        {
            var value = rawValue ?? string.Empty;
            if (contribution == null)
            {
                return value;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
                case SettingValueKind.Integer:
                    int integerValue;
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue)
                        ? integerValue.ToString(CultureInfo.InvariantCulture)
                        : value.Trim();
                case SettingValueKind.Float:
                    float floatValue;
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue)
                        ? floatValue.ToString(CultureInfo.InvariantCulture)
                        : value.Trim();
                case SettingValueKind.String:
                default:
                    return value;
            }
        }

        private string GetTextValue(SettingContribution contribution)
        {
            return contribution != null
                ? GetTextValue(contribution.SettingId, GetDefaultSerializedValue(contribution))
                : string.Empty;
        }

        private string GetTextValue(string settingId, string defaultValue)
        {
            string value;
            return !string.IsNullOrEmpty(settingId) && _textValues.TryGetValue(settingId, out value)
                ? value ?? string.Empty
                : defaultValue ?? string.Empty;
        }

        private bool GetToggleValue(SettingContribution contribution)
        {
            bool value;
            if (contribution != null && _toggleValues.TryGetValue(contribution.SettingId, out value))
            {
                return value;
            }

            return contribution != null && string.Equals(GetDefaultSerializedValue(contribution), "true", StringComparison.OrdinalIgnoreCase);
        }

        private List<SettingsSection> GetVisibleSections(SettingsNavigationGroup group)
        {
            var visibleSections = new List<SettingsSection>();
            if (group == null)
            {
                return visibleSections;
            }

            for (var i = 0; i < group.Sections.Count; i++)
            {
                if (IsSectionVisible(group.Sections[i]))
                {
                    visibleSections.Add(group.Sections[i]);
                }
            }

            return visibleSections;
        }

        private bool IsSectionVisible(SettingsSection section)
        {
            if (section == null)
            {
                return false;
            }

            if (_showModifiedOnly)
            {
                return GetSectionVisibleItemCount(section) > 0;
            }

            return MatchesSearch(section.SearchText) || GetSectionVisibleItemCount(section) > 0;
        }

        private int GetSectionVisibleItemCount(SettingsSection section)
        {
            if (section == null)
            {
                return 0;
            }

            if (string.Equals(section.SectionId, SourceSetupSectionId + ".paths", StringComparison.OrdinalIgnoreCase))
            {
                return CountVisibleSettingsForIds(WorkspaceRootSettingId, RuntimeContentRootSettingId, ReferenceAssemblyRootSettingId, AdditionalSourceRootsSettingId);
            }

            if (!string.IsNullOrEmpty(section.Scope) &&
                (string.Equals(section.SectionId, SourceSetupSectionId + ".settings", StringComparison.OrdinalIgnoreCase) ||
                 section.SectionId.StartsWith("scope.", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(section.Scope, "Workspace", StringComparison.OrdinalIgnoreCase) ||
                 (_snapshot != null && FindSettingSectionContribution(_snapshot, section.Scope) != null)))
            {
                return CountVisibleContributionsForScope(_snapshot, section.Scope);
            }

            if (string.Equals(section.SectionId, ThemesSectionId, StringComparison.OrdinalIgnoreCase))
            {
                return CountVisibleThemes();
            }

            if (string.Equals(section.SectionId, KeybindingsSectionId, StringComparison.OrdinalIgnoreCase))
            {
                return _showModifiedOnly ? CountModifiedKeybindingRows() : CountVisibleKeybindings();
            }

            if (string.Equals(section.SectionId, EditorsSectionId, StringComparison.OrdinalIgnoreCase))
            {
                return _showModifiedOnly ? 0 : CountVisibleEditors();
            }

            if (_showModifiedOnly)
            {
                return 0;
            }

            return MatchesSearch(section.SearchText) ? 1 : 0;
        }

        private int CountVisibleSettingsForIds(params string[] settingIds)
        {
            if (settingIds == null || settingIds.Length == 0 || _snapshot == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < settingIds.Length; i++)
            {
                var contribution = FindSettingContribution(_snapshot, settingIds[i]);
                if (contribution != null && IsContributionVisible(contribution))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountVisibleContributionsForScope(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            if (snapshot == null || string.IsNullOrEmpty(scope))
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (ShouldRenderContribution(scope, contribution) && IsContributionVisible(contribution))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountVisibleSections(SettingsNavigationGroup group)
        {
            if (group == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < group.Sections.Count; i++)
            {
                if (IsSectionVisible(group.Sections[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountVisibleThemes()
        {
            if (_snapshot == null)
            {
                return 0;
            }

            if (_showModifiedOnly)
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

        private string BuildSearchSummary(SettingsDocument document)
        {
            var visibleSections = 0;
            var visibleItems = 0;
            if (document != null)
            {
                var orderedSections = GetOrderedSections(document);
                for (var i = 0; i < orderedSections.Count; i++)
                {
                    var section = orderedSections[i];
                    if (!IsSectionVisible(section))
                    {
                        continue;
                    }

                    visibleSections++;
                    visibleItems += Mathf.Max(1, GetSectionVisibleItemCount(section));
                }
            }

            if (_showModifiedOnly)
            {
                return visibleItems.ToString(CultureInfo.InvariantCulture) + " modified item(s) across " +
                    visibleSections.ToString(CultureInfo.InvariantCulture) + " section(s).";
            }

            if (IsSearchActive())
            {
                return visibleItems.ToString(CultureInfo.InvariantCulture) + " match(es) across " +
                    visibleSections.ToString(CultureInfo.InvariantCulture) + " section(s).";
            }

            return visibleSections.ToString(CultureInfo.InvariantCulture) + " section(s) available.";
        }

        private void DrawActiveSectionBanner(SettingsDocument document, string renderActiveSectionId)
        {
            var section = FindSection(document, renderActiveSectionId);
            if (section == null)
            {
                return;
            }

            CortexIdeLayout.DrawGroup(null, delegate
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

        private static SettingsSection FindSection(SettingsDocument document, string sectionId)
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

        private string BuildSectionNavigationLabel(SettingsSection section)
        {
            if (section == null)
            {
                return string.Empty;
            }

            var label = section.Title ?? string.Empty;
            if (IsSearchActive() || _showModifiedOnly)
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

            if (_showModifiedOnly && !IsSettingModified(contribution))
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
            var query = GetNormalizedSearchQuery();
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            var haystack = (text ?? string.Empty).ToLowerInvariant();
            var terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < terms.Length; i++)
            {
                if (haystack.IndexOf(terms[i], StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private string GetNormalizedSearchQuery()
        {
            return (_appliedSettingsSearchQuery ?? string.Empty).Trim().ToLowerInvariant();
        }

        private void HandleSearchQueryChanged(SettingsDocument document)
        {
            var query = GetNormalizedSearchQuery();
            if (string.Equals(query, _lastNormalizedSearchQuery, StringComparison.Ordinal))
            {
                return;
            }

            _lastNormalizedSearchQuery = query;
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            var orderedSections = GetOrderedSections(document);
            for (var i = 0; i < orderedSections.Count; i++)
            {
                if (IsSectionVisible(orderedSections[i]))
                {
                    _activeSectionId = orderedSections[i].SectionId;
                    _pendingSectionJumpId = orderedSections[i].SectionId;
                    break;
                }
            }
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

        private bool IsNavigationGroupExpandedForDisplay(SettingsNavigationGroup group, bool storedExpanded, string activeSectionId)
        {
            if (!string.IsNullOrEmpty(GetNormalizedSearchQuery()))
            {
                return true;
            }

            return storedExpanded || GetActiveSection(group, activeSectionId) != null;
        }

        private SettingsSection GetActiveSection(SettingsNavigationGroup group, string activeSectionId)
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

        private void RevealActiveNavigationTarget(SettingsDocument document)
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

        private string GetNavigationTargetId(SettingsDocument document)
        {
            if (document == null || string.IsNullOrEmpty(_activeSectionId))
            {
                return string.Empty;
            }

            for (var i = 0; i < document.Groups.Count; i++)
            {
                var group = document.Groups[i];
                var activeSection = GetActiveSection(group, _activeSectionId);
                if (activeSection == null)
                {
                    continue;
                }

                bool expanded;
                if (!_navigationGroupExpanded.TryGetValue(group.GroupId, out expanded))
                {
                    expanded = ShouldDefaultGroupExpanded(group.GroupId);
                }

                return IsNavigationGroupExpandedForDisplay(group, expanded, _activeSectionId)
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

        private void UpdateActiveSection(SettingsDocument document)
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
                _activeSectionId = active;
            }
        }

        private string ResolveRenderActiveSectionId(SettingsDocument document)
        {
            var orderedSections = GetOrderedSections(document);
            if (orderedSections.Count == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < orderedSections.Count; i++)
            {
                var section = orderedSections[i];
                if (!IsSectionVisible(section))
                {
                    continue;
                }

                if (string.Equals(section.SectionId, _activeSectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return _activeSectionId;
                }
            }

            for (var i = 0; i < orderedSections.Count; i++)
            {
                if (IsSectionVisible(orderedSections[i]))
                {
                    return orderedSections[i].SectionId;
                }
            }

            return string.Empty;
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
                _activeSectionId = interaction.RequestedSectionId;
                _pendingSectionJumpId = interaction.RequestedSectionId;
            }
        }

        private void FinalizeNavigationState(SettingsDocument document)
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
            if (string.IsNullOrEmpty(_pendingSectionJumpId))
            {
                return false;
            }

            float anchor;
            if (_sectionAnchors.TryGetValue(_pendingSectionJumpId, out anchor))
            {
                _contentScroll.y = Mathf.Max(0f, anchor - 8f);
                _pendingSectionJumpId = string.Empty;
                return true;
            }

            return false;
        }

        private void CommitSearchQuery()
        {
            var nextQuery = (_settingsSearchQuery ?? string.Empty).Trim();
            if (string.Equals(_appliedSettingsSearchQuery ?? string.Empty, nextQuery, StringComparison.Ordinal))
            {
                return;
            }

            _appliedSettingsSearchQuery = nextQuery;
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

            _shellState.Settings.SettingsActiveSectionId = _activeSectionId ?? string.Empty;
            _shellState.Settings.SettingsNavigationScrollY = _navigationScroll.y;
            _shellState.Settings.SettingsContentScrollY = _contentScroll.y;
            _shellState.Settings.SettingsSearchQuery = _appliedSettingsSearchQuery ?? string.Empty;
            _shellState.Settings.SettingsShowModifiedOnly = _showModifiedOnly;
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
            if (mod == null || string.IsNullOrEmpty(mod.ModId))
            {
                return string.Empty;
            }

            string value;
            if (_loadedModPathDrafts.TryGetValue(mod.ModId, out value))
            {
                return value ?? string.Empty;
            }

            var existing = _projectCatalog != null ? _projectCatalog.GetProject(mod.ModId) : null;
            value = existing != null ? existing.SourceRootPath ?? string.Empty : string.Empty;
            _loadedModPathDrafts[mod.ModId] = value;
            return value;
        }

        private void LinkLoadedModToSource(LoadedModInfo mod, CortexShellState state)
        {
            if (mod == null || string.IsNullOrEmpty(mod.ModId))
            {
                return;
            }

            var sourceRoot = GetLoadedModDraftValue(mod);
            var linkResult = _loadedModSourceLinkService.LinkLoadedModToSource(mod, sourceRoot, _projectCatalog, _workspaceService);
            for (var i = 0; i < linkResult.Diagnostics.Length; i++)
            {
                state.Diagnostics.Add(linkResult.Diagnostics[i]);
            }

            if (!linkResult.Success || linkResult.Definition == null)
            {
                state.StatusMessage = linkResult.StatusMessage;
                return;
            }

            state.SelectedProject = _projectCatalog.GetProject(mod.ModId) ?? linkResult.Definition;
            _loadedModPathDrafts[mod.ModId] = linkResult.Definition.SourceRootPath ?? sourceRoot;
            state.StatusMessage = "Linked loaded mod " + mod.ModId + " to " + (linkResult.Definition.SourceRootPath ?? string.Empty) + ".";
        }

        private void ApplyLoadedModMappings(CortexShellState state)
        {
            if (_projectCatalog == null || _workspaceService == null || _loadedModCatalog == null)
            {
                return;
            }

            var loadedMods = _loadedModCatalog.GetLoadedMods();
            if (loadedMods == null || loadedMods.Count == 0)
            {
                return;
            }

            for (var i = 0; i < loadedMods.Count; i++)
            {
                var mod = loadedMods[i];
                if (mod == null || string.IsNullOrEmpty(mod.ModId))
                {
                    continue;
                }

                string draftValue;
                if (!_loadedModPathDrafts.TryGetValue(mod.ModId, out draftValue) || string.IsNullOrEmpty(draftValue))
                {
                    continue;
                }

                var existing = _projectCatalog.GetProject(mod.ModId);
                if (existing != null &&
                    string.Equals(existing.SourceRootPath ?? string.Empty, draftValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var linkResult = _loadedModSourceLinkService.LinkLoadedModToSource(mod, draftValue, _projectCatalog, _workspaceService);
                if (!linkResult.Success || linkResult.Definition == null)
                {
                    for (var diagnosticIndex = 0; diagnosticIndex < linkResult.Diagnostics.Length; diagnosticIndex++)
                    {
                        state.Diagnostics.Add(linkResult.Diagnostics[diagnosticIndex]);
                    }

                    continue;
                }

                _loadedModPathDrafts[mod.ModId] = linkResult.Definition.SourceRootPath ?? draftValue;
                for (var diagnosticIndex = 0; diagnosticIndex < linkResult.Diagnostics.Length; diagnosticIndex++)
                {
                    state.Diagnostics.Add(linkResult.Diagnostics[diagnosticIndex]);
                }

                if (state.SelectedProject == null || string.Equals(state.SelectedProject.ModId, mod.ModId, StringComparison.OrdinalIgnoreCase))
                {
                    state.SelectedProject = _projectCatalog.GetProject(mod.ModId) ?? linkResult.Definition;
                }
            }
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            float value;
            return float.TryParse(raw, out value) ? value : fallback;
        }

        private sealed class SettingsDocument
        {
            public readonly List<SettingsSection> Sections = new List<SettingsSection>();
            public readonly List<SettingsNavigationGroup> Groups = new List<SettingsNavigationGroup>();
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

        private sealed class SettingsNavigationGroup
        {
            public readonly string GroupId;
            public readonly string Title;
            public readonly int SortOrder;
            public readonly List<SettingsSection> Sections = new List<SettingsSection>();

            public SettingsNavigationGroup(string groupId, string title, int sortOrder)
            {
                GroupId = groupId ?? string.Empty;
                Title = string.IsNullOrEmpty(title) ? "General" : title;
                SortOrder = sortOrder;
            }
        }

        private sealed class SettingsSection
        {
            public readonly string SectionId;
            public readonly string GroupId;
            public readonly string GroupTitle;
            public readonly string Scope;
            public readonly string Title;
            public readonly string Description;
            public readonly string SearchText;
            public readonly int SortOrder;
            public readonly Action<ICortexSettingsStore, WorkbenchPresentationSnapshot, ThemeState, CortexShellState> DrawBody;

            public SettingsSection(
                string sectionId,
                string groupId,
                string groupTitle,
                string scope,
                string title,
                string description,
                string searchText,
                int sortOrder,
                Action<ICortexSettingsStore, WorkbenchPresentationSnapshot, ThemeState, CortexShellState> drawBody)
            {
                SectionId = sectionId;
                GroupId = groupId;
                GroupTitle = groupTitle;
                Scope = scope;
                Title = title;
                Description = description;
                SearchText = searchText;
                SortOrder = sortOrder;
                DrawBody = drawBody;
            }
        }
    }
}
