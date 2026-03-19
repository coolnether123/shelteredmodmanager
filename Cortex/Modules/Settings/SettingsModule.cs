using System;
using System.Collections.Generic;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Settings
{
    public sealed class SettingsModule
    {
        private const string SourceSetupSectionId = "settings.sourceSetup";
        private const string WorkspaceOverviewSectionId = "settings.sourceSetup.overview";
        private const string ThemesSectionId = "settings.themes";
        private const string KeybindingsSectionId = "settings.keybindings";
        private const string EditorsSectionId = "settings.editors";
        private const string ActionsSectionId = "settings.actions";
        private const string WorkspaceRootSettingId = "WorkspaceRootPath";
        private const string ModsRootSettingId = "ModsRootPath";
        private const string ManagedAssemblyRootSettingId = "ManagedAssemblyRootPath";
        private const string AdditionalSourceRootsSettingId = "AdditionalSourceRoots";
        private const float NavigationWidth = 268f;
        private const float CompactEditorWidth = 380f;

        private bool _loaded;
        private Vector2 _navigationScroll = Vector2.zero;
        private Vector2 _contentScroll = Vector2.zero;
        private readonly Dictionary<string, string> _textValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _toggleValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _loadedModPathDrafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _sectionAnchors = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _navigationGroupExpanded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _collapsedNavigationGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _selectedThemeId = string.Empty;
        private string _activeSectionId = WorkspaceOverviewSectionId;
        private string _pendingSectionJumpId = string.Empty;
        private string _settingsSearchQuery = string.Empty;
        private string _lastNormalizedSearchQuery = string.Empty;
        private IProjectCatalog _projectCatalog;
        private IProjectWorkspaceService _workspaceService;
        private ILoadedModCatalog _loadedModCatalog;
        private CortexShellState _shellState;
        private readonly IEditorKeybindingService _editorKeybindingService = new EditorKeybindingService();

        public void Draw(
            ICortexSettingsStore settingsStore,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state)
        {
            _projectCatalog = projectCatalog;
            _workspaceService = workspaceService;
            _loadedModCatalog = loadedModCatalog;
            _shellState = state;
            EnsureLoaded(snapshot, themeState, state);

            var document = BuildDocument(snapshot);
            UpdateActiveSection(document);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawToolbar(settingsStore, snapshot, themeState, state);
            GUILayout.Space(6f);
            DrawSearchBar();
            GUILayout.Space(6f);
            HandleSearchQueryChanged(document);
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawNavigation(document);
            GUILayout.Space(8f);
            DrawContentSurface(settingsStore, snapshot, themeState, state, document);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
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

        private void DrawSearchBar()
        {
            CortexIdeLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Search properties", GUILayout.Width(130f), GUILayout.Height(22f));
                _settingsSearchQuery = GUILayout.TextField(_settingsSearchQuery ?? string.Empty, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Clear", GUILayout.Width(64f), GUILayout.Height(24f)))
                {
                    _settingsSearchQuery = string.Empty;
                }
                GUILayout.EndHorizontal();
            }, GUILayout.Height(42f), GUILayout.ExpandWidth(true));
        }

        private void DrawNavigation(SettingsDocument document)
        {
            CortexIdeLayout.DrawGroup(null, delegate
            {
                GUILayout.Label("Property Pages", GUILayout.Height(22f));
                GUILayout.Space(4f);
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
                    DrawNavigationGroup(document.Groups[i]);
                }

                if (visibleGroupCount == 0)
                {
                    GUILayout.Label("No property pages match the current search.");
                }
                GUILayout.EndScrollView();
            }, GUILayout.Width(NavigationWidth + 16f), GUILayout.ExpandHeight(true));
        }

        private void DrawNavigationGroup(SettingsNavigationGroup group)
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
                expanded = !_collapsedNavigationGroups.Contains(group.GroupId);
                _navigationGroupExpanded[group.GroupId] = expanded;
            }

            var searchActive = !string.IsNullOrEmpty(GetNormalizedSearchQuery());
            var effectiveExpanded = searchActive ? true : expanded;

            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.72f);
            GUI.contentColor = CortexIdeLayout.GetTextColor();
            var headerLabel = (expanded ? "▼ " : "▶ ") + group.Title;
            headerLabel = (effectiveExpanded ? "v " : "> ") + group.Title;
            if (GUILayout.Button(headerLabel, GUILayout.Height(26f), GUILayout.ExpandWidth(true)))
            {
                if (searchActive)
                {
                    _activeSectionId = visibleSections[0].SectionId;
                    _pendingSectionJumpId = visibleSections[0].SectionId;
                }
                else
                {
                    expanded = !expanded;
                    _navigationGroupExpanded[group.GroupId] = expanded;
                    PersistNavigationGroupState();
                    effectiveExpanded = expanded;
                }
            }
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;

            if (effectiveExpanded)
            {
                GUILayout.Space(2f);
                for (var i = 0; i < visibleSections.Count; i++)
                {
                    DrawNavigationButton(visibleSections[i], 14f);
                }
            }

            GUILayout.Space(8f);
        }

        private void DrawNavigationButton(SettingsSection section, float indent)
        {
            if (section == null)
            {
                return;
            }

            var isSelected = string.Equals(_activeSectionId, section.SectionId, StringComparison.OrdinalIgnoreCase);
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            GUI.backgroundColor = isSelected
                ? CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetHeaderColor(), 0.16f)
                : CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.82f);
            GUI.contentColor = isSelected ? Color.white : CortexIdeLayout.GetTextColor();
            if (GUILayout.Button(section.Title, GUILayout.Height(24f), GUILayout.ExpandWidth(true)))
            {
                _activeSectionId = section.SectionId;
                _pendingSectionJumpId = section.SectionId;
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            GUILayout.Space(2f);
        }

        private void DrawContentSurface(
            ICortexSettingsStore settingsStore,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexShellState state,
            SettingsDocument document)
        {
            CortexIdeLayout.DrawGroup(null, delegate
            {
                GUILayout.Label("Properties", GUILayout.Height(24f));
                GUILayout.Space(4f);

                _sectionAnchors.Clear();
                _contentScroll = GUILayout.BeginScrollView(_contentScroll, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                var visibleCount = 0;
                for (var i = 0; i < document.Sections.Count; i++)
                {
                    if (IsSectionVisible(document.Sections[i]))
                    {
                        DrawDocumentSection(document.Sections[i], settingsStore, snapshot, themeState, state);
                        visibleCount++;
                    }
                }

                if (visibleCount == 0)
                {
                    GUILayout.Label("No settings match the current search.");
                }
                GUILayout.EndScrollView();

                ApplyPendingSectionJump();
                UpdateActiveSection(document);
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
            GUILayout.Label(section.Title, GUILayout.Height(34f));
            var lineRect = GUILayoutUtility.GetRect(0f, 0f, 1f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            var previousColor = GUI.color;
            GUI.color = CortexIdeLayout.Blend(CortexIdeLayout.GetBorderColor(), CortexIdeLayout.GetTextColor(), 0.2f);
            GUI.Box(lineRect, GUIContent.none);
            GUI.color = previousColor;
            if (!string.IsNullOrEmpty(section.Description))
            {
                GUILayout.Space(4f);
                GUILayout.Label(section.Description);
            }

            GUILayout.Space(10f);
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

            document.Sections.Add(CreateThemesSection());
            document.Sections.Add(CreateKeybindingsSection());
            document.Sections.Add(CreateEditorsSection());
            document.Sections.Add(CreateActionsSection());
            BuildNavigationGroups(document);
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

        private SettingsSection CreateWorkspaceOverviewSection()
        {
            return new SettingsSection(
                WorkspaceOverviewSectionId,
                "workspace",
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
                "Paths",
                "Configure the main folders Cortex uses for source resolution, mod discovery, and reference browsing.",
                "workspace paths folders source roots mods root managed assemblies additional roots",
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
                "Loaded Mods",
                "Bind running mods to editable source roots so navigation and project discovery can resolve correctly.",
                "workspace loaded mods links source links running mods mod mapping",
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
                "Themes",
                "Manage the registered workbench themes and choose the active shell theme.",
                "appearance themes colors theme catalog shell theme",
                0,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawThemeRegistry(snapshot, themeState, state);
                });
        }

        private SettingsSection CreateKeybindingsSection()
        {
            return new SettingsSection(
                KeybindingsSectionId,
                "editor",
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
                "Actions",
                "Convenience actions related to windows, logs, and the shell.",
                "shell actions logs return to editor window actions",
                0,
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawActionsPanel(state);
                });
        }

        private static void DrawSourceSetupGuide()
        {
            DrawSectionPanel("Overview", delegate
            {
                GUILayout.Label("Use this section to configure the source roots Cortex should search and to link loaded in-game mods back to editable source folders.");
                GUILayout.Label("Workspace Scan Root should point at the folder that contains your local mod projects.");
                GUILayout.Label("Loaded Mods Root should point at the live mod installs currently being used by the game.");
                GUILayout.Label("Modules can contribute additional settings scopes below, and they will be merged into this same document automatically.");
            });
        }

        private void DrawWorkspacePathEditors(WorkbenchPresentationSnapshot snapshot, CortexShellState state)
        {
            DrawSectionPanel("Workspace Paths", delegate
            {
                DrawSettingPathEditor(snapshot, WorkspaceRootSettingId, "Workspace Scan Root", "Folder containing your editable local mod projects.");
                DrawSettingPathEditor(snapshot, ModsRootSettingId, "Loaded Mods Root", "Folder containing the live installed mods used in-game.");
                DrawSettingPathEditor(snapshot, ManagedAssemblyRootSettingId, "Managed DLL Root", "Folder containing the game's managed assemblies for reference browsing.");
                DrawSettingPathEditor(snapshot, AdditionalSourceRootsSettingId, "Additional Source Roots", "Semicolon-separated fallback roots used during source resolution.");
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
            DrawSectionPanel("Loaded Mod Source Links", delegate
            {
                var loadedMods = _loadedModCatalog != null ? _loadedModCatalog.GetLoadedMods() : null;
                if (loadedMods == null || loadedMods.Count == 0)
                {
                    GUILayout.Label("No running mods were discovered.");
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
                    GUILayout.Label("No running mods were discovered.");
                }
            });
        }

        private static void DrawQuickFacts(CortexShellState state)
        {
            DrawSectionPanel("Current Paths", delegate
            {
                DrawReadOnlyField("Workspace root", state != null && state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty);
                DrawReadOnlyField("Loaded mods root", state != null && state.Settings != null ? state.Settings.ModsRootPath : string.Empty);
                DrawReadOnlyField("Managed assemblies", state != null && state.Settings != null ? state.Settings.ManagedAssemblyRootPath : string.Empty);
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
                if (!ShouldRenderContribution(scope, contribution) || !MatchesContributionQuery(contribution))
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
                if (ShouldRenderContribution(scope, snapshot.Settings[i]))
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
                string.Equals(contribution.SettingId, ModsRootSettingId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contribution.SettingId, ManagedAssemblyRootSettingId, StringComparison.OrdinalIgnoreCase) ||
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
            GUILayout.Label("Live Mod Root: " + (mod.RootPath ?? string.Empty));
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
            draftValue = GUILayout.TextField(draftValue ?? string.Empty, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
            _loadedModPathDrafts[draftKey] = draftValue;
            if (GUILayout.Button("Paste", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _loadedModPathDrafts[draftKey] = GUIUtility.systemCopyBuffer ?? string.Empty;
            }
            if (GUILayout.Button("Clear", GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                _loadedModPathDrafts[draftKey] = string.Empty;
            }
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
            _loadedModPathDrafts.Clear();
            _navigationGroupExpanded.Clear();
            _collapsedNavigationGroups.Clear();
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
            _activeSectionId = WorkspaceOverviewSectionId;
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
            GUILayout.Label(definition.DisplayName, GUILayout.Height(20f));
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

        private void DrawThemeOption(ThemeContribution theme, ThemeState themeState, CortexShellState state)
        {
            if (theme == null)
            {
                return;
            }

            var isSelected = string.Equals(_selectedThemeId, theme.ThemeId, StringComparison.OrdinalIgnoreCase);
            BeginPropertyRow();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.Label(theme.DisplayName ?? theme.ThemeId, GUILayout.Height(20f));
            GUILayout.Label(theme.ThemeId ?? string.Empty);
            if (!string.IsNullOrEmpty(theme.Description))
            {
                GUILayout.Label(theme.Description);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth));
            var label = isSelected ? "Selected" : "Use Theme";
            if (GUILayout.Toggle(isSelected, label, "button", GUILayout.Width(120f), GUILayout.Height(24f)))
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
            GUILayout.BeginVertical(GUILayout.Width(CompactEditorWidth + 132f));
            DrawCompactTextValueEditor(contribution.SettingId, contribution.DefaultValue, allowPaste, allowClear);
            if (showPathActions)
            {
                DrawClipboardActions(contribution.SettingId);
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
            var value = GetTextValue(contribution);
            value = GUILayout.TextArea(value, GUILayout.Width(CompactEditorWidth + 80f), GUILayout.MinHeight(88f));
            _textValues[contribution.SettingId] = value ?? string.Empty;
            DrawSettingFooter(contribution);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
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

        private static void DrawSettingLabelColumn(SettingContribution contribution)
        {
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.Label(contribution.DisplayName ?? contribution.SettingId, GUILayout.Height(20f));
            if (!string.IsNullOrEmpty(contribution.Description))
            {
                GUILayout.Label(contribution.Description);
            }
            GUILayout.EndVertical();
        }

        private static void DrawSettingFooter(SettingContribution contribution)
        {
            if (!string.IsNullOrEmpty(contribution.HelpText))
            {
                GUILayout.Label(contribution.HelpText);
            }
            else if (!string.IsNullOrEmpty(contribution.PlaceholderText) && string.IsNullOrEmpty(contribution.DefaultValue))
            {
                GUILayout.Label("Example: " + contribution.PlaceholderText);
            }

            if (!string.IsNullOrEmpty(contribution.DefaultValue))
            {
                GUILayout.Label("Default: " + contribution.DefaultValue);
            }
        }

        private void DrawClipboardActions(string settingId)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Clipboard", GUILayout.Width(116f), GUILayout.Height(22f)))
            {
                _textValues[settingId] = GUIUtility.systemCopyBuffer ?? string.Empty;
            }
            if (GUILayout.Button("Clear Value", GUILayout.Width(92f), GUILayout.Height(22f)))
            {
                _textValues[settingId] = string.Empty;
            }
            GUILayout.EndHorizontal();
        }

        private bool SetBooleanSettingValue(SettingContribution contribution, CortexShellState state, bool value)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return value;
            }

            _toggleValues[contribution.SettingId] = value;

            if (state != null && state.Settings != null)
            {
                var field = GetSettingField(contribution);
                if (field != null && field.FieldType == typeof(bool))
                {
                    field.SetValue(state.Settings, value);
                }
            }

            return value;
        }

        private static void DrawSectionPanel(string title, Action drawBody)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(title, GUILayout.Height(24f));
            GUILayout.BeginHorizontal();
            var previousColor = GUI.color;
            GUI.color = CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetBorderColor(), 0.4f);
            GUILayout.Box(GUIContent.none, GUILayout.Width(2f), GUILayout.MinHeight(56f), GUILayout.ExpandHeight(true));
            GUI.color = previousColor;
            GUILayout.Space(14f);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (drawBody != null)
            {
                drawBody();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10f);
        }

        private static void BeginPropertyRow()
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            var previousColor = GUI.color;
            GUI.color = CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetBorderColor(), 0.45f);
            GUILayout.Box(GUIContent.none, GUILayout.Width(2f), GUILayout.MinHeight(44f), GUILayout.ExpandHeight(true));
            GUI.color = previousColor;
            GUILayout.Space(14f);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        }

        private static void EndPropertyRow()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void DrawReadOnlyField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180f));
            GUILayout.TextField(string.IsNullOrEmpty(value) ? "Not configured" : value, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private static void DrawThemeSwatch(string hex, string label)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            var swatchColor = CortexIdeLayout.ParseColor(hex, Color.white);
            GUI.backgroundColor = swatchColor;
            GUI.contentColor = GetReadableSwatchTextColor(swatchColor);
            GUILayout.Box(label, GUILayout.Width(70f), GUILayout.Height(18f));
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
            if (themeState != null)
            {
                themeState.ThemeId = state.Settings.ThemeId;
            }
        }

        private void LoadContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            var field = GetSettingField(contribution);
            if (field == null)
            {
                return;
            }

            var value = field.GetValue(settings);
            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                _toggleValues[contribution.SettingId] = value is bool && (bool)value;
                return;
            }

            if (value == null)
            {
                _textValues[contribution.SettingId] = contribution.DefaultValue ?? string.Empty;
                return;
            }

            if (contribution.ValueKind == SettingValueKind.Float)
            {
                _textValues[contribution.SettingId] = ((float)value).ToString("F0");
                return;
            }

            _textValues[contribution.SettingId] = value.ToString();
        }

        private void ApplyContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            var field = GetSettingField(contribution);
            if (field == null)
            {
                return;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    field.SetValue(settings, GetToggleValue(contribution));
                    break;
                case SettingValueKind.Integer:
                    field.SetValue(settings, ParseInt(GetTextValue(contribution), ParseInt(contribution.DefaultValue, (int)field.GetValue(settings))));
                    break;
                case SettingValueKind.Float:
                    field.SetValue(settings, ParseFloat(GetTextValue(contribution), ParseFloat(contribution.DefaultValue, (float)field.GetValue(settings))));
                    break;
                case SettingValueKind.String:
                default:
                    field.SetValue(settings, GetTextValue(contribution));
                    break;
            }
        }

        private static FieldInfo GetSettingField(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return null;
            }

            return typeof(CortexSettings).GetField(contribution.SettingId, BindingFlags.Public | BindingFlags.Instance);
        }

        private string GetTextValue(SettingContribution contribution)
        {
            return contribution != null
                ? GetTextValue(contribution.SettingId, contribution.DefaultValue)
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

            return contribution != null && string.Equals(contribution.DefaultValue, "true", StringComparison.OrdinalIgnoreCase);
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
            return section != null && MatchesSearch(section.SearchText);
        }

        private bool MatchesContributionQuery(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            return MatchesSearch(BuildContributionSearchText(contribution));
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
            return (_settingsSearchQuery ?? string.Empty).Trim().ToLowerInvariant();
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

            for (var i = 0; i < document.Sections.Count; i++)
            {
                if (IsSectionVisible(document.Sections[i]))
                {
                    _activeSectionId = document.Sections[i].SectionId;
                    _pendingSectionJumpId = document.Sections[i].SectionId;
                    break;
                }
            }
        }

        private void LoadNavigationGroupState(CortexSettings settings)
        {
            var serialized = settings != null ? settings.SettingsCollapsedGroupIds : string.Empty;
            if (!string.IsNullOrEmpty(serialized))
            {
                var parts = serialized.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < parts.Length; i++)
                {
                    _collapsedNavigationGroups.Add(parts[i]);
                }
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
            if (document == null || document.Sections.Count == 0 || _sectionAnchors.Count == 0)
            {
                return;
            }

            var threshold = _contentScroll.y + 32f;
            var active = string.Empty;
            var bestAnchor = float.MinValue;
            for (var i = 0; i < document.Sections.Count; i++)
            {
                var section = document.Sections[i];
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

        private void ApplyPendingSectionJump()
        {
            if (string.IsNullOrEmpty(_pendingSectionJumpId))
            {
                return;
            }

            float anchor;
            if (_sectionAnchors.TryGetValue(_pendingSectionJumpId, out anchor))
            {
                _contentScroll.y = Mathf.Max(0f, anchor - 8f);
                _pendingSectionJumpId = string.Empty;
            }
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

            if (_projectCatalog == null || _workspaceService == null)
            {
                state.StatusMessage = "Project mapping services are unavailable.";
                return;
            }

            var sourceRoot = GetLoadedModDraftValue(mod);
            if (string.IsNullOrEmpty(sourceRoot))
            {
                state.StatusMessage = "Set a source root before linking the loaded mod.";
                return;
            }

            var analysis = _workspaceService.AnalyzeSourceRoot(sourceRoot, mod.ModId);
            if (analysis == null || analysis.Definition == null)
            {
                state.StatusMessage = "Source analysis is unavailable for that path.";
                return;
            }

            if (!analysis.Success)
            {
                state.StatusMessage = analysis.StatusMessage ?? "Could not link loaded mod to the supplied source root.";
                for (var i = 0; i < analysis.Diagnostics.Count; i++)
                {
                    state.Diagnostics.Add(analysis.Diagnostics[i]);
                }
                return;
            }

            _projectCatalog.Upsert(analysis.Definition);
            state.SelectedProject = _projectCatalog.GetProject(mod.ModId) ?? analysis.Definition;
            _loadedModPathDrafts[mod.ModId] = analysis.Definition.SourceRootPath ?? sourceRoot;
            for (var i = 0; i < analysis.Diagnostics.Count; i++)
            {
                state.Diagnostics.Add(analysis.Diagnostics[i]);
            }

            state.StatusMessage = "Linked loaded mod " + mod.ModId + " to " + (analysis.Definition.SourceRootPath ?? string.Empty) + ".";
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

                var analysis = _workspaceService.AnalyzeSourceRoot(draftValue, mod.ModId);
                if (analysis == null || analysis.Definition == null || !analysis.Success)
                {
                    if (analysis != null)
                    {
                        for (var diagnosticIndex = 0; diagnosticIndex < analysis.Diagnostics.Count; diagnosticIndex++)
                        {
                            state.Diagnostics.Add(analysis.Diagnostics[diagnosticIndex]);
                        }
                    }

                    continue;
                }

                _projectCatalog.Upsert(analysis.Definition);
                _loadedModPathDrafts[mod.ModId] = analysis.Definition.SourceRootPath ?? draftValue;
                for (var diagnosticIndex = 0; diagnosticIndex < analysis.Diagnostics.Count; diagnosticIndex++)
                {
                    state.Diagnostics.Add(analysis.Diagnostics[diagnosticIndex]);
                }

                if (state.SelectedProject == null || string.Equals(state.SelectedProject.ModId, mod.ModId, StringComparison.OrdinalIgnoreCase))
                {
                    state.SelectedProject = _projectCatalog.GetProject(mod.ModId) ?? analysis.Definition;
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
            public readonly string Title;
            public readonly string Description;
            public readonly string SearchText;
            public readonly int SortOrder;
            public readonly Action<ICortexSettingsStore, WorkbenchPresentationSnapshot, ThemeState, CortexShellState> DrawBody;

            public SettingsSection(
                string sectionId,
                string groupId,
                string groupTitle,
                string title,
                string description,
                string searchText,
                int sortOrder,
                Action<ICortexSettingsStore, WorkbenchPresentationSnapshot, ThemeState, CortexShellState> drawBody)
            {
                SectionId = sectionId;
                GroupId = groupId;
                GroupTitle = groupTitle;
                Title = title;
                Description = description;
                SearchText = searchText;
                SortOrder = sortOrder;
                DrawBody = drawBody;
            }
        }
    }
}
