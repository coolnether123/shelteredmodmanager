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
        private const string SourceSetupPageId = "source.setup";
        private const string ThemesPageId = "themes";
        private const string KeybindingsPageId = "keybindings";
        private const string EditorsPageId = "editors";
        private const string ActionsPageId = "actions";
        private const string WorkspaceRootSettingId = "WorkspaceRootPath";
        private const string ModsRootSettingId = "ModsRootPath";
        private const string ManagedAssemblyRootSettingId = "ManagedAssemblyRootPath";
        private const string AdditionalSourceRootsSettingId = "AdditionalSourceRoots";

        private bool _loaded;
        private Vector2 _navigationScroll = Vector2.zero;
        private Vector2 _contentScroll = Vector2.zero;
        private readonly Dictionary<string, string> _textValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _toggleValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _expandedGroups = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _loadedModPathDrafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _selectedThemeId = string.Empty;
        private string _selectedPageId = SourceSetupPageId;
        private IProjectCatalog _projectCatalog;
        private IProjectWorkspaceService _workspaceService;
        private ILoadedModCatalog _loadedModCatalog;
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
            EnsureLoaded(snapshot, themeState, state);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawToolbar(settingsStore, snapshot, themeState, state);
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawNavigation(snapshot);
            GUILayout.Space(8f);
            DrawContentSurface(settingsStore, snapshot, themeState, state);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawToolbar(ICortexSettingsStore settingsStore, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            CortexIdeLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Cortex Settings", GUILayout.Height(24f), GUILayout.Width(180f));
                GUILayout.Label("Property pages for workspace, tooling, appearance, and shell behavior.", GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save", GUILayout.Width(100f), GUILayout.Height(24f)))
                {
                    Apply(snapshot, themeState, state);
                    ApplyLoadedModMappings(state);
                    settingsStore.Save(state.Settings);
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
                    EnsureLoaded(snapshot, themeState, state);
                    state.StatusMessage = "Reset settings fields to defaults.";
                }

                GUILayout.EndHorizontal();
            }, GUILayout.Height(56f), GUILayout.ExpandWidth(true));
        }

        private void DrawNavigation(WorkbenchPresentationSnapshot snapshot)
        {
            var pages = BuildPages(snapshot);
            CortexIdeLayout.DrawGroup(null, delegate
            {
                GUILayout.Label("Pages", GUILayout.Height(22f));
                _navigationScroll = GUILayout.BeginScrollView(_navigationScroll, GUI.skin.box, GUILayout.Width(220f), GUILayout.ExpandHeight(true));
                for (var i = 0; i < pages.Count; i++)
                {
                    DrawNavigationButton(pages[i]);
                }
                GUILayout.EndScrollView();
            }, GUILayout.Width(236f), GUILayout.ExpandHeight(true));
        }

        private void DrawNavigationButton(SettingsPage page)
        {
            if (page == null)
            {
                return;
            }

            var isSelected = string.Equals(_selectedPageId, page.PageId, StringComparison.OrdinalIgnoreCase);
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = isSelected
                ? CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetHeaderColor(), 0.2f)
                : CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.65f);
            GUI.contentColor = isSelected ? Color.white : CortexIdeLayout.GetTextColor();
            if (GUILayout.Button(page.Title, GUILayout.Height(28f), GUILayout.ExpandWidth(true)))
            {
                _selectedPageId = page.PageId;
                _contentScroll = Vector2.zero;
            }

            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            GUILayout.Space(2f);
        }

        private void DrawContentSurface(ICortexSettingsStore settingsStore, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            var selectedPage = FindSelectedPage(snapshot);
            CortexIdeLayout.DrawGroup(null, delegate
            {
                GUILayout.Label(selectedPage.Title, GUILayout.Height(24f));
                if (!string.IsNullOrEmpty(selectedPage.Description))
                {
                    GUILayout.Label(selectedPage.Description);
                    GUILayout.Space(6f);
                }

                _contentScroll = GUILayout.BeginScrollView(_contentScroll, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                selectedPage.DrawBody(settingsStore, snapshot, themeState, state);
                GUILayout.EndScrollView();
            }, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }

        private SettingsPage FindSelectedPage(WorkbenchPresentationSnapshot snapshot)
        {
            var pages = BuildPages(snapshot);
            for (var i = 0; i < pages.Count; i++)
            {
                if (string.Equals(pages[i].PageId, _selectedPageId, StringComparison.OrdinalIgnoreCase))
                {
                    return pages[i];
                }
            }

            _selectedPageId = pages.Count > 0 ? pages[0].PageId : SourceSetupPageId;
            return pages.Count > 0 ? pages[0] : CreateSourceSetupPage();
        }

        private List<SettingsPage> BuildPages(WorkbenchPresentationSnapshot snapshot)
        {
            var pages = new List<SettingsPage>();
            pages.Add(CreateSourceSetupPage());

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

                    var scope = string.IsNullOrEmpty(contribution.Scope) ? "General" : contribution.Scope;
                    if (seenScopes.Add(scope))
                    {
                        pages.Add(CreateScopePage(scope));
                    }
                }
            }

            pages.Add(CreateThemesPage());
            pages.Add(CreateKeybindingsPage());
            pages.Add(CreateEditorsPage());
            pages.Add(CreateActionsPage());
            return pages;
        }

        private SettingsPage CreateSourceSetupPage()
        {
            return new SettingsPage(
                SourceSetupPageId,
                "Source Setup",
                "Configure where Cortex finds editable sources, live mods, and related assets.",
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawSourceSetupGuide(state);
                    GUILayout.Space(10f);
                    DrawWorkspacePathEditors(snapshot, state);
                    GUILayout.Space(10f);
                    DrawLoadedModMappings(state);
                    GUILayout.Space(10f);
                    DrawQuickFacts(state);
                });
        }

        private SettingsPage CreateScopePage(string scope)
        {
            return new SettingsPage(
                "scope." + scope.ToLowerInvariant(),
                scope,
                "Configure " + scope.ToLowerInvariant() + " behavior for the Cortex shell.",
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawSettingsScope(snapshot, scope, state);
                });
        }

        private SettingsPage CreateThemesPage()
        {
            return new SettingsPage(
                ThemesPageId,
                "Themes",
                "Manage the registered workbench themes and choose the active shell theme.",
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawThemeRegistry(snapshot, themeState, state);
                });
        }

        private SettingsPage CreateKeybindingsPage()
        {
            return new SettingsPage(
                KeybindingsPageId,
                "Keybindings",
                "Configure editor shortcuts, multi-caret commands, and undo history.",
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawEditorKeybindings(state);
                });
        }

        private SettingsPage CreateEditorsPage()
        {
            return new SettingsPage(
                EditorsPageId,
                "Editors",
                "Registered editors describe the available file handlers and content types.",
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawEditorRegistry(snapshot);
                });
        }

        private SettingsPage CreateActionsPage()
        {
            return new SettingsPage(
                ActionsPageId,
                "Actions",
                "Convenience actions related to settings, logs, and the shell.",
                delegate(ICortexSettingsStore store, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
                {
                    DrawActionsPanel(state);
                });
        }

        private static void DrawSourceSetupGuide(CortexShellState state)
        {
            DrawSectionPanel("Overview", delegate
            {
                GUILayout.Label("Use this page to configure the source roots Cortex should search and to link loaded in-game mods back to your editable source folders.");
                GUILayout.Label("Workspace Scan Root should point at the folder that contains your local mod projects.");
                GUILayout.Label("Loaded Mods Root should point at the live mod installs currently being used by the game.");
                GUILayout.Label("Use the loaded-mod mapping section below to bind a running mod directly to its source root.");
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

        private void DrawSettingPathEditor(WorkbenchPresentationSnapshot snapshot, string settingId, string label, string description)
        {
            var contribution = FindSettingContribution(snapshot, settingId);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(label, GUILayout.Height(20f));
            if (!string.IsNullOrEmpty(description))
            {
                GUILayout.Label(description);
            }

            DrawTextValueEditor(settingId, contribution != null ? contribution.DefaultValue : string.Empty, true, true);
            GUILayout.EndVertical();
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
            _expandedGroups.Clear();
            _loadedModPathDrafts.Clear();

            var settings = state.Settings ?? new CortexSettings();
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    var contribution = snapshot.Settings[i];
                    LoadContributionValue(contribution, settings);
                    if (contribution != null)
                    {
                        var scope = string.IsNullOrEmpty(contribution.Scope) ? "General" : contribution.Scope;
                        _expandedGroups[scope] = true;
                    }
                }
            }

            _textValues["editor.undo.limit"] = settings.EditorUndoHistoryLimit.ToString();

            _selectedThemeId = !string.IsNullOrEmpty(settings.ThemeId)
                ? settings.ThemeId
                : themeState != null && !string.IsNullOrEmpty(themeState.ThemeId)
                    ? themeState.ThemeId
                    : "cortex.vs-dark";
            _loaded = true;
        }

        private void DrawSettingsScope(WorkbenchPresentationSnapshot snapshot, string scope, CortexShellState state)
        {
            if (snapshot == null || snapshot.Settings.Count == 0)
            {
                GUILayout.Label("No settings contributions were registered.");
                return;
            }

            DrawExpandableSection(scope, true, delegate
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    var contribution = snapshot.Settings[i];
                    if (contribution == null || string.IsNullOrEmpty(contribution.SettingId) || IsThemeSetting(contribution))
                    {
                        continue;
                    }

                    var contributionScope = string.IsNullOrEmpty(contribution.Scope) ? "General" : contribution.Scope;
                    if (!string.Equals(contributionScope, scope, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DrawSettingContribution(contribution, state);
                }
            });
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
                for (var i = 0; i < snapshot.Themes.Count; i++)
                {
                    DrawThemeOption(snapshot.Themes[i], themeState, state);
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
            for (var i = 0; i < bindings.Count; i++)
            {
                if (!string.Equals(currentCategory, bindings[i].Category, StringComparison.Ordinal))
                {
                    currentCategory = bindings[i].Category;
                    GUILayout.Space(8f);
                    GUILayout.Label(currentCategory, GUILayout.Height(22f));
                }

                DrawEditorKeybindingRow(state.Settings, bindings[i]);
                GUILayout.Space(4f);
            }
        }

        private void DrawEditorKeybindingRow(CortexSettings settings, EditorCommandBindingDefinition definition)
        {
            var binding = GetEditableBinding(settings, definition);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(definition.DisplayName, GUILayout.Height(20f));
            if (!string.IsNullOrEmpty(definition.Description))
            {
                GUILayout.Label(definition.Description);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Key", GUILayout.Width(36f));
            binding.Key = GUILayout.TextField(binding.Key ?? string.Empty, GUILayout.Width(110f), GUILayout.Height(22f));
            binding.Control = GUILayout.Toggle(binding.Control, "Ctrl", GUILayout.Width(52f));
            binding.Shift = GUILayout.Toggle(binding.Shift, "Shift", GUILayout.Width(56f));
            binding.Alt = GUILayout.Toggle(binding.Alt, "Alt", GUILayout.Width(44f));
            GUILayout.Label("Current: " + _editorKeybindingService.FormatGesture(binding), GUILayout.Width(180f));
            if (GUILayout.Button("Reset", GUILayout.Width(60f), GUILayout.Height(22f)))
            {
                ApplyBinding(binding, definition.DefaultBinding);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void DrawEditorRegistry(WorkbenchPresentationSnapshot snapshot)
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
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label(editor.DisplayName, GUILayout.Height(22f));
                    GUILayout.Label("Extension: " + (editor.ResourceExtension ?? string.Empty));
                    GUILayout.Label("Content Type: " + (editor.ContentType ?? string.Empty));
                    GUILayout.EndVertical();
                    GUILayout.Space(6f);
                }
            });
        }

        private void DrawActionsPanel(CortexShellState state)
        {
            DrawSectionPanel("Window Actions", delegate
            {
                GUILayout.BeginHorizontal();
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
            GUILayout.BeginVertical(GUI.skin.box);
            var label = theme.DisplayName + "  [" + theme.ThemeId + "]" + (string.Equals(theme.ThemeId, "cortex.vs-dark", StringComparison.OrdinalIgnoreCase) ? "  Default" : string.Empty);
            if (GUILayout.Toggle(isSelected, label, "button", GUILayout.Height(24f)))
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
            if (!string.IsNullOrEmpty(theme.Description))
            {
                GUILayout.Label(theme.Description);
            }

            GUILayout.Label("Font role: " + (string.IsNullOrEmpty(theme.FontRole) ? "default" : theme.FontRole));
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void DrawSettingContribution(SettingContribution contribution, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                DrawBooleanSettingEditor(contribution, state);
                DrawSettingMeta(contribution);
                GUILayout.EndVertical();
                GUILayout.Space(6f);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(260f));
            GUILayout.Label(contribution.DisplayName ?? contribution.SettingId, GUILayout.Height(20f));
            if (!string.IsNullOrEmpty(contribution.Description))
            {
                GUILayout.Label(contribution.Description);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawTextValueEditor(
                contribution.SettingId,
                contribution.DefaultValue,
                contribution.ValueKind == SettingValueKind.String,
                contribution.ValueKind == SettingValueKind.String);
            if (!string.IsNullOrEmpty(contribution.DefaultValue))
            {
                GUILayout.Label("Default: " + contribution.DefaultValue);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
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

        private void DrawBooleanSettingEditor(SettingContribution contribution, CortexShellState state)
        {
            var value = GetToggleValue(contribution);
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            var displayName = contribution.DisplayName ?? contribution.SettingId;

            GUILayout.Label(displayName, GUILayout.Height(20f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(48f));

            GUI.backgroundColor = value
                ? CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.6f)
                : CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetHeaderColor(), 0.2f);
            GUI.contentColor = value ? CortexIdeLayout.GetTextColor() : Color.white;
            if (GUILayout.Button("Disabled", GUILayout.Width(88f), GUILayout.Height(24f)))
            {
                value = SetBooleanSettingValue(contribution, state, false);
            }

            GUI.backgroundColor = value
                ? CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetHeaderColor(), 0.2f)
                : CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.6f);
            GUI.contentColor = value ? Color.white : CortexIdeLayout.GetTextColor();
            if (GUILayout.Button("Enabled", GUILayout.Width(88f), GUILayout.Height(24f)))
            {
                value = SetBooleanSettingValue(contribution, state, true);
            }

            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            GUILayout.Space(10f);
            GUILayout.Label(value ? "Current: Enabled" : "Current: Disabled", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            _toggleValues[contribution.SettingId] = value;
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

        private static void DrawSettingMeta(SettingContribution contribution)
        {
            if (!string.IsNullOrEmpty(contribution.Description))
            {
                GUILayout.Label(contribution.Description);
            }

            if (!string.IsNullOrEmpty(contribution.DefaultValue))
            {
                GUILayout.Label("Default: " + contribution.DefaultValue);
            }
        }

        private void DrawExpandableSection(string key, bool defaultExpanded, Action drawBody)
        {
            bool isExpanded;
            if (!_expandedGroups.TryGetValue(key, out isExpanded))
            {
                isExpanded = defaultExpanded;
                _expandedGroups[key] = isExpanded;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            var label = (isExpanded ? "▼ " : "► ") + key;
            if (GUILayout.Button(label, GUILayout.Height(24f), GUILayout.ExpandWidth(true)))
            {
                isExpanded = !isExpanded;
                _expandedGroups[key] = isExpanded;
            }
            GUILayout.EndHorizontal();

            if (isExpanded && drawBody != null)
            {
                GUILayout.Space(4f);
                drawBody();
            }

            GUILayout.EndVertical();
        }

        private static void DrawSectionPanel(string title, Action drawBody)
        {
            CortexIdeLayout.DrawGroup(title, delegate
            {
                if (drawBody != null)
                {
                    drawBody();
                }
            }, GUILayout.ExpandWidth(true));
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

        private static bool IsThemeSetting(SettingContribution contribution)
        {
            return contribution != null && string.Equals(contribution.SettingId, nameof(CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase);
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

        private sealed class SettingsPage
        {
            public readonly string PageId;
            public readonly string Title;
            public readonly string Description;
            public readonly Action<ICortexSettingsStore, WorkbenchPresentationSnapshot, ThemeState, CortexShellState> DrawBody;

            public SettingsPage(
                string pageId,
                string title,
                string description,
                Action<ICortexSettingsStore, WorkbenchPresentationSnapshot, ThemeState, CortexShellState> drawBody)
            {
                PageId = pageId;
                Title = title;
                Description = description;
                DrawBody = drawBody;
            }
        }
    }
}
