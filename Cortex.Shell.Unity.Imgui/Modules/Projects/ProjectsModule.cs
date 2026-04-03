using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Projects;
using UnityEngine;
using Cortex.Shell.Unity.Imgui;

namespace Cortex.Modules.Projects
{
    public sealed class ProjectsModule
    {
        private string _searchText = string.Empty;
        private Vector2 _projectScroll = Vector2.zero;
        private Vector2 _diagnosticScroll = Vector2.zero;
        private bool _showAdvanced;
        private readonly ProjectWorkspaceInteractionService _workspaceInteractionService = new ProjectWorkspaceInteractionService();
        private readonly ProjectWorkspaceDraft _draft;

        public ProjectsModule()
        {
            _draft = _workspaceInteractionService.CreateDraft();
        }

        public void Draw(
            IProjectCatalog catalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog,
            IPathInteractionService pathInteractionService,
            CortexShellState state)
        {
            var settings = state.Settings ?? new CortexSettings();
            ImguiWorkbenchLayout.DrawTwoPane(
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

            var projects = _workspaceInteractionService.BuildProjectList(catalog, _searchText);
            GUILayout.Label("Projects: " + projects.Count);
            _projectScroll = GUILayout.BeginScrollView(_projectScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                var label = project.DisplayName + "\n" + project.SourceRootPath;
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true), GUILayout.MinHeight(46f)))
                {
                    state.SelectedProject = project.Project;
                    ApplyDraft(_workspaceInteractionService.LoadDraft(project.Project));
                    state.StatusMessage = "Selected project " + project.DisplayName;
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
            GUILayout.Label("Enter the root folder for the source tree you want Cortex to map. Cortex will infer the project id and .csproj automatically.");
            DrawPathField(
                "Source Folder",
                ref _draft.SourceRoot,
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
                        Title = "Select source folder",
                        InitialPath = !string.IsNullOrEmpty(_draft.SourceRoot)
                            ? _draft.SourceRoot
                            : (state != null && state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty)
                    }
                });

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Source Folder", GUILayout.Width(160f)))
            {
                _workspaceInteractionService.ApplySourceFolder(catalog, workspaceService, _draft, state, true);
            }
            if (GUILayout.Button("Import Workspace", GUILayout.Width(120f)))
            {
                _workspaceInteractionService.ImportWorkspaceProjects(catalog, workspaceService, state);
            }
            if (GUILayout.Button("Validate", GUILayout.Width(90f)))
            {
                _draft.ValidationMessage = _workspaceInteractionService.BuildValidation(workspaceService, _workspaceInteractionService.CreateDefinition(_draft));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("Detected Project ID: " + (string.IsNullOrEmpty(_draft.ModId) ? "Not detected yet" : _draft.ModId));
            GUILayout.Label("Detected Project File: " + (string.IsNullOrEmpty(_draft.ProjectFile) ? "Not found yet" : _draft.ProjectFile));
            GUILayout.Label("Selected source folder is used by the editor file tree once the project is saved and selected.");

            _showAdvanced = GUILayout.Toggle(_showAdvanced, "Show Advanced Build Fields");
            if (_showAdvanced)
            {
                DrawField("Project ID", ref _draft.ModId);
                DrawPathField(
                    "Project File",
                    ref _draft.ProjectFile,
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
                            InitialPath = !string.IsNullOrEmpty(_draft.ProjectFile) ? _draft.ProjectFile : _draft.SourceRoot,
                            Filter = "C# Project|*.csproj|All Files|*.*"
                        }
                    });
                DrawField("Build Override", ref _draft.BuildOverride);
                DrawField("Output DLL", ref _draft.OutputAssembly);
                DrawField("Output PDB", ref _draft.OutputPdb);
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Project", GUILayout.Width(120f)))
            {
                _workspaceInteractionService.SaveProject(catalog, workspaceService, _draft, state);
            }

            if (state.SelectedProject != null && GUILayout.Button("Delete Selected", GUILayout.Width(120f)))
            {
                _workspaceInteractionService.DeleteSelected(catalog, _draft, state);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Validation");
            GUILayout.TextArea(string.IsNullOrEmpty(_draft.ValidationMessage) ? "Validate the project to confirm the source root, .csproj path, and build output paths." : _draft.ValidationMessage, GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
            DrawDiagnostics(state);
            GUILayout.EndVertical();
        }

        private void DrawSourceMappingGuide(IProjectWorkspaceService workspaceService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Source Mapping Guide");
            GUILayout.Label("Put the project-tree root in 'Source Folder'. Example: D:\\Projects\\Extensions\\FactionOverhaul");
            GUILayout.Label("This should be the folder that contains the source files and usually the .csproj. Cortex will infer the rest.");
            GUILayout.Label("Workspace root: " + ((state.Settings != null && !string.IsNullOrEmpty(state.Settings.WorkspaceRootPath)) ? state.Settings.WorkspaceRootPath : "Not configured"));
            GUILayout.Label("Runtime content root: " + ((state.Settings != null && !string.IsNullOrEmpty(state.Settings.RuntimeContentRootPath)) ? state.Settings.RuntimeContentRootPath : "Not configured"));

            GUILayout.BeginHorizontal();
            if (state.SelectedProject != null && !string.IsNullOrEmpty(state.SelectedProject.SourceRootPath) && GUILayout.Button("Use Selected Source", GUILayout.Width(140f)))
            {
                _workspaceInteractionService.UseSelectedProjectSource(workspaceService, state.SelectedProject, _draft, state);
            }
            if (GUILayout.Button("Use Workspace Root", GUILayout.Width(140f)))
            {
                _workspaceInteractionService.UseWorkspaceRoot(workspaceService, _draft, state);
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
            var suggestions = _workspaceInteractionService.BuildLoadedModSuggestions(catalog, workspaceService, loadedModCatalog);
            if (suggestions.Count == 0)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Active Content Source Assistant");
            GUILayout.Label("Use this when content is active under the host but Cortex does not yet know where its editable source lives.");

            var shown = 0;
            for (var i = 0; i < suggestions.Count; i++)
            {
                var suggestion = suggestions[i];
                var mod = suggestion.LoadedMod;
                var analysis = suggestion.Analysis;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(mod.ModId + "  |  " + mod.RootPath);
                GUILayout.Label(string.IsNullOrEmpty(suggestion.SuggestedSourceRoot)
                    ? "No obvious source root found. Start from the runtime content root or set a custom workspace path."
                    : "Suggested source folder: " + suggestion.SuggestedSourceRoot);

                GUILayout.BeginHorizontal();
                if (analysis != null && analysis.Definition != null && GUILayout.Button("Insert Mapping", GUILayout.Width(120f)))
                {
                    _workspaceInteractionService.InsertSuggestion(catalog, workspaceService, suggestion, _draft, state);
                }

                if (analysis != null && analysis.Definition != null && GUILayout.Button("Use In Editor", GUILayout.Width(110f)))
                {
                    _workspaceInteractionService.UseSuggestion(workspaceService, suggestion, _draft, state);
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
                GUILayout.Label("All active content items already have a valid source mapping.");
            }

            GUILayout.EndVertical();
        }

        private void ApplyDraft(ProjectWorkspaceDraft draft)
        {
            if (draft == null)
            {
                _workspaceInteractionService.ClearDraft(_draft);
                return;
            }

            _draft.ModId = draft.ModId ?? string.Empty;
            _draft.SourceRoot = draft.SourceRoot ?? string.Empty;
            _draft.ProjectFile = draft.ProjectFile ?? string.Empty;
            _draft.BuildOverride = draft.BuildOverride ?? string.Empty;
            _draft.OutputAssembly = draft.OutputAssembly ?? string.Empty;
            _draft.OutputPdb = draft.OutputPdb ?? string.Empty;
            _draft.ValidationMessage = draft.ValidationMessage ?? string.Empty;
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
