using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.Projects
{
    public sealed class ProjectsModule
    {
        private string _modId = string.Empty;
        private string _sourceRoot = string.Empty;
        private string _projectFile = string.Empty;
        private string _buildOverride = string.Empty;
        private string _outputAssembly = string.Empty;
        private string _outputPdb = string.Empty;
        private string _searchText = string.Empty;
        private string _validationMessage = string.Empty;
        private Vector2 _projectScroll = Vector2.zero;
        private Vector2 _diagnosticScroll = Vector2.zero;
        private bool _showAdvanced;

        public void Draw(
            IProjectCatalog catalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog,
            IPathInteractionService pathInteractionService,
            CortexShellState state)
        {
            var settings = state.Settings ?? new CortexSettings();
            CortexIdeLayout.DrawTwoPane(
                settings.ProjectsPaneWidth,
                300f,
                delegate { DrawProjectList(catalog, state); },
                delegate { DrawProjectEditor(catalog, workspaceService, loadedModCatalog, pathInteractionService, state); });
        }

        private void DrawProjectList(IProjectCatalog catalog, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Configured Projects");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(46f));
            _searchText = GUILayout.TextField(_searchText, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            var projects = catalog.GetProjects();
            GUILayout.Label("Projects: " + projects.Count);
            _projectScroll = GUILayout.BeginScrollView(_projectScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                var displayName = project.GetDisplayName();
                if (!string.IsNullOrEmpty(_searchText) &&
                    displayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (project.SourceRootPath ?? string.Empty).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var label = displayName + "\n" + (project.SourceRootPath ?? string.Empty);
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true), GUILayout.MinHeight(46f)))
                {
                    state.SelectedProject = project;
                    Load(project);
                    state.StatusMessage = "Selected project " + project.GetDisplayName();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void DrawProjectEditor(
            IProjectCatalog catalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog,
            IPathInteractionService pathInteractionService,
            CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawLoadedModAssistant(catalog, workspaceService, loadedModCatalog, state);
            DrawSourceMappingGuide(workspaceService, state);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Project Setup");
            GUILayout.Label("Enter the root folder for your mod project. Cortex will infer the project id and .csproj automatically.");
            DrawPathField(
                "Mod Source Folder",
                ref _sourceRoot,
                pathInteractionService,
                new CortexPathFieldOptions
                {
                    AllowBrowse = true,
                    AllowOpen = true,
                    AllowPaste = true,
                    AllowClear = true,
                    BrowseRequest = new PathSelectionRequest
                    {
                        SelectionKind = PathSelectionKind.Folder,
                        Title = "Select mod source folder",
                        InitialPath = !string.IsNullOrEmpty(_sourceRoot)
                            ? _sourceRoot
                            : (state != null && state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty)
                    }
                });

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Source Folder", GUILayout.Width(160f)))
            {
                ApplySourceFolder(catalog, workspaceService, state, true);
            }
            if (GUILayout.Button("Import Workspace", GUILayout.Width(120f)))
            {
                ImportWorkspaceProjects(catalog, workspaceService, state);
            }
            if (GUILayout.Button("Validate", GUILayout.Width(90f)))
            {
                _validationMessage = BuildValidation(workspaceService, CreateDefinition());
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("Detected Mod ID: " + (string.IsNullOrEmpty(_modId) ? "Not detected yet" : _modId));
            GUILayout.Label("Detected Project File: " + (string.IsNullOrEmpty(_projectFile) ? "Not found yet" : _projectFile));
            GUILayout.Label("Selected source folder is used by the editor file tree once the project is saved and selected.");

            _showAdvanced = GUILayout.Toggle(_showAdvanced, "Show Advanced Build Fields");
            if (_showAdvanced)
            {
                DrawField("Mod ID", ref _modId);
                DrawPathField(
                    "Project File",
                    ref _projectFile,
                    pathInteractionService,
                    new CortexPathFieldOptions
                    {
                        AllowBrowse = true,
                        AllowOpen = true,
                        AllowPaste = true,
                        AllowClear = true,
                        BrowseRequest = new PathSelectionRequest
                        {
                            SelectionKind = PathSelectionKind.OpenFile,
                            Title = "Select project file",
                            InitialPath = !string.IsNullOrEmpty(_projectFile) ? _projectFile : _sourceRoot,
                            Filter = "C# Project|*.csproj|All Files|*.*"
                        }
                    });
                DrawField("Build Override", ref _buildOverride);
                DrawField("Output DLL", ref _outputAssembly);
                DrawField("Output PDB", ref _outputPdb);
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Project", GUILayout.Width(120f)))
            {
                SaveProject(catalog, workspaceService, state);
            }

            if (state.SelectedProject != null && GUILayout.Button("Delete Selected", GUILayout.Width(120f)))
            {
                catalog.Remove(state.SelectedProject.ModId);
                state.SelectedProject = null;
                _validationMessage = string.Empty;
                ClearFields();
                state.StatusMessage = "Deleted selected project.";
                state.Diagnostics.Add("Deleted the selected project mapping.");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Validation");
            GUILayout.TextArea(string.IsNullOrEmpty(_validationMessage) ? "Validate the project to confirm the source root, .csproj path, and build output paths." : _validationMessage, GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
            DrawDiagnostics(state);
            GUILayout.EndVertical();
        }

        private void DrawSourceMappingGuide(IProjectWorkspaceService workspaceService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Source Mapping Guide");
            GUILayout.Label("Put your mod's project-tree root in 'Mod Source Folder'. Example: D:\\Projects\\Sheltered Modding\\Faction-Overhaul\\Faction Overhaul");
            GUILayout.Label("This should be the folder that contains the mod's .cs files and usually the .csproj. Cortex will infer the rest.");
            GUILayout.Label("Workspace root: " + ((state.Settings != null && !string.IsNullOrEmpty(state.Settings.WorkspaceRootPath)) ? state.Settings.WorkspaceRootPath : "Not configured"));
            GUILayout.Label("Loaded mods root: " + ((state.Settings != null && !string.IsNullOrEmpty(state.Settings.ModsRootPath)) ? state.Settings.ModsRootPath : "Not configured"));

            GUILayout.BeginHorizontal();
            if (state.SelectedProject != null && !string.IsNullOrEmpty(state.SelectedProject.SourceRootPath) && GUILayout.Button("Use Selected Source", GUILayout.Width(140f)))
            {
                Load(state.SelectedProject);
                _validationMessage = BuildValidation(workspaceService, state.SelectedProject);
                state.StatusMessage = "Loaded the selected project's source mapping into the editor.";
                state.Diagnostics.Add("Prepared the selected project's source folder for editing.");
            }
            if (GUILayout.Button("Use Workspace Root", GUILayout.Width(140f)))
            {
                _sourceRoot = state.Settings != null ? state.Settings.WorkspaceRootPath ?? string.Empty : string.Empty;
                PrepareAnalysis(workspaceService, _sourceRoot, string.Empty, state);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawDiagnostics(CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(132f));
            GUILayout.Label("Cortex Workflow Diagnostics");
            _diagnosticScroll = GUILayout.BeginScrollView(_diagnosticScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            if (state == null || state.Diagnostics.Entries.Count == 0)
            {
                GUILayout.Label("No workflow diagnostics yet. Apply a source folder to see detection results.");
            }
            else
            {
                for (var i = state.Diagnostics.Entries.Count - 1; i >= 0; i--)
                {
                    GUILayout.Label(state.Diagnostics.Entries[i]);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawLoadedModAssistant(IProjectCatalog catalog, IProjectWorkspaceService workspaceService, ILoadedModCatalog loadedModCatalog, CortexShellState state)
        {
            var loadedMods = loadedModCatalog != null ? loadedModCatalog.GetLoadedMods() : null;
            if (loadedMods == null || loadedMods.Count == 0)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Loaded Mod Source Assistant");
            GUILayout.Label("Use this when a mod is loaded in-game but Cortex does not yet know where its editable source lives.");

            var shown = 0;
            for (var i = 0; i < loadedMods.Count; i++)
            {
                var mod = loadedMods[i];
                if (mod == null || string.IsNullOrEmpty(mod.ModId))
                {
                    continue;
                }

                var existing = catalog.GetProject(mod.ModId);
                if (existing != null && !string.IsNullOrEmpty(existing.SourceRootPath) && Directory.Exists(existing.SourceRootPath))
                {
                    continue;
                }

                var inferredSourceRoot = workspaceService != null ? workspaceService.FindLikelySourceRoot(mod.RootPath) : string.Empty;
                var analysis = workspaceService != null ? workspaceService.AnalyzeSourceRoot(inferredSourceRoot, mod.ModId) : null;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(mod.ModId + "  |  " + mod.RootPath);
                GUILayout.Label(string.IsNullOrEmpty(inferredSourceRoot)
                    ? "No obvious source root found. Start from the mod root or set a custom workspace path."
                    : "Suggested mod source folder: " + inferredSourceRoot);

                GUILayout.BeginHorizontal();
                if (analysis != null && analysis.Definition != null && GUILayout.Button("Insert Mapping", GUILayout.Width(120f)))
                {
                    catalog.Upsert(analysis.Definition);
                    state.SelectedProject = catalog.GetProject(mod.ModId) ?? analysis.Definition;
                    Load(state.SelectedProject);
                    _validationMessage = BuildValidation(workspaceService, state.SelectedProject);
                    state.StatusMessage = "Inserted source mapping for loaded mod " + mod.ModId + ".";
                    state.Diagnostics.Add("Inserted mapping for loaded mod " + mod.ModId + ".");
                }

                if (analysis != null && analysis.Definition != null && GUILayout.Button("Use In Editor", GUILayout.Width(110f)))
                {
                    Load(analysis.Definition);
                    _validationMessage = BuildValidation(workspaceService, analysis.Definition);
                    state.StatusMessage = "Prepared source mapping fields for " + mod.ModId + ".";
                    state.Diagnostics.Add("Prepared inferred source folder for loaded mod " + mod.ModId + ".");
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                shown++;
                if (shown >= 3)
                {
                    break;
                }
            }

            if (shown == 0)
            {
                GUILayout.Label("All loaded mods already have a valid source mapping.");
            }

            GUILayout.EndVertical();
        }

        private void ImportWorkspaceProjects(IProjectCatalog catalog, IProjectWorkspaceService workspaceService, CortexShellState state)
        {
            var root = state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty;
            var importResult = workspaceService != null ? workspaceService.DiscoverWorkspaceProjects(root) : new ProjectWorkspaceImportResult();
            for (var i = 0; i < importResult.Definitions.Count; i++)
            {
                catalog.Upsert(importResult.Definitions[i]);
            }

            state.StatusMessage = importResult.StatusMessage;
            for (var i = 0; i < importResult.Diagnostics.Count; i++)
            {
                state.Diagnostics.Add(importResult.Diagnostics[i]);
            }
        }

        private void ApplySourceFolder(IProjectCatalog catalog, IProjectWorkspaceService workspaceService, CortexShellState state, bool saveProject)
        {
            var analysis = PrepareAnalysis(workspaceService, _sourceRoot, _modId, state);
            if (analysis == null || !analysis.Success || analysis.Definition == null)
            {
                return;
            }

            PreserveAdvancedFields(analysis.Definition);
            Load(analysis.Definition);
            _validationMessage = BuildValidation(workspaceService, analysis.Definition);

            if (saveProject)
            {
                catalog.Upsert(analysis.Definition);
                state.SelectedProject = catalog.GetProject(analysis.Definition.ModId) ?? analysis.Definition;
            }

            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            state.StatusMessage = "Applied source folder for " + analysis.Definition.GetDisplayName() + ".";
        }

        private ProjectWorkspaceAnalysis PrepareAnalysis(IProjectWorkspaceService workspaceService, string sourceRoot, string preferredModId, CortexShellState state)
        {
            var analysis = workspaceService != null ? workspaceService.AnalyzeSourceRoot(sourceRoot, preferredModId) : null;
            if (analysis == null)
            {
                state.StatusMessage = "Project workspace analysis is unavailable.";
                state.Diagnostics.Add(state.StatusMessage);
                return null;
            }

            for (var i = 0; i < analysis.Diagnostics.Count; i++)
            {
                state.Diagnostics.Add(analysis.Diagnostics[i]);
            }

            state.StatusMessage = analysis.StatusMessage;
            return analysis;
        }

        private void SaveProject(IProjectCatalog catalog, IProjectWorkspaceService workspaceService, CortexShellState state)
        {
            var definition = CreateDefinition();
            catalog.Upsert(definition);
            state.SelectedProject = catalog.GetProject(definition.ModId) ?? definition;
            _validationMessage = BuildValidation(workspaceService, definition);
            state.StatusMessage = "Saved project " + definition.GetDisplayName();
            state.Diagnostics.Add("Saved project mapping for " + definition.GetDisplayName() + " using source folder " + (definition.SourceRootPath ?? string.Empty) + ".");
        }

        private void PreserveAdvancedFields(CortexProjectDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_buildOverride))
            {
                definition.BuildCommandOverride = _buildOverride;
            }
            if (!string.IsNullOrEmpty(_outputAssembly))
            {
                definition.OutputAssemblyPath = _outputAssembly;
            }
            if (!string.IsNullOrEmpty(_outputPdb))
            {
                definition.OutputPdbPath = _outputPdb;
            }
        }

        private string BuildValidation(IProjectWorkspaceService workspaceService, CortexProjectDefinition definition)
        {
            var validation = workspaceService != null ? workspaceService.Validate(definition) : new ProjectValidationResult();
            return string.Join("\n", validation.Lines.ToArray());
        }

        private CortexProjectDefinition CreateDefinition()
        {
            var modId = _modId;
            if (string.IsNullOrEmpty(modId) && !string.IsNullOrEmpty(_sourceRoot))
            {
                modId = Path.GetFileName(_sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            return new CortexProjectDefinition
            {
                ModId = modId,
                SourceRootPath = _sourceRoot,
                ProjectFilePath = _projectFile,
                BuildCommandOverride = _buildOverride,
                OutputAssemblyPath = _outputAssembly,
                OutputPdbPath = _outputPdb
            };
        }

        private void Load(CortexProjectDefinition project)
        {
            _modId = project != null ? project.ModId ?? string.Empty : string.Empty;
            _sourceRoot = project != null ? project.SourceRootPath ?? string.Empty : string.Empty;
            _projectFile = project != null ? project.ProjectFilePath ?? string.Empty : string.Empty;
            _buildOverride = project != null ? project.BuildCommandOverride ?? string.Empty : string.Empty;
            _outputAssembly = project != null ? project.OutputAssemblyPath ?? string.Empty : string.Empty;
            _outputPdb = project != null ? project.OutputPdbPath ?? string.Empty : string.Empty;
        }

        private void ClearFields()
        {
            _modId = string.Empty;
            _sourceRoot = string.Empty;
            _projectFile = string.Empty;
            _buildOverride = string.Empty;
            _outputAssembly = string.Empty;
            _outputPdb = string.Empty;
        }

        private static void DrawField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130f));
            value = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private static void DrawPathField(
            string label,
            ref string value,
            IPathInteractionService pathInteractionService,
            CortexPathFieldOptions options)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130f));
            value = CortexPathField.DrawValueEditor(
                "projects." + label.Replace(" ", string.Empty),
                value ?? string.Empty,
                pathInteractionService,
                options,
                GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
    }
}
