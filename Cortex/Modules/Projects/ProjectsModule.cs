using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using ModAPI.Core;
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

        public void Draw(IProjectCatalog catalog, CortexShellState state)
        {
            var settings = state.Settings ?? new CortexSettings();
            CortexIdeLayout.DrawTwoPane(
                settings.ProjectsPaneWidth,
                300f,
                delegate { DrawProjectList(catalog, state); },
                delegate { DrawProjectEditor(catalog, state); });
        }

        private void DrawProjectList(IProjectCatalog catalog, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.Width(360f));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Configured Projects");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(46f));
            _searchText = GUILayout.TextField(_searchText, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Import Workspace", GUILayout.Width(120f)))
            {
                ImportWorkspaceProjects(catalog, state);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            var projects = catalog.GetProjects();
            GUILayout.Label("Projects: " + projects.Count);
            _projectScroll = GUILayout.BeginScrollView(_projectScroll, GUI.skin.box, GUILayout.Height(540f));
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
                    _validationMessage = BuildValidation(project);
                    state.StatusMessage = "Selected project " + project.GetDisplayName();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void DrawProjectEditor(IProjectCatalog catalog, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawLoadedModAssistant(catalog, state);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Project Setup");
            DrawField("Mod ID", ref _modId);
            DrawField("Source Root", ref _sourceRoot);
            DrawField("Project File", ref _projectFile);
            DrawField("Build Override", ref _buildOverride);
            DrawField("Output DLL", ref _outputAssembly);
            DrawField("Output PDB", ref _outputPdb);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Detect .csproj", GUILayout.Width(140f)))
            {
                AutoDetectProjectFile();
            }
            if (GUILayout.Button("Use Defaults", GUILayout.Width(120f)))
            {
                ApplyDefaultsFromProjectPath();
            }
            if (GUILayout.Button("Validate", GUILayout.Width(90f)))
            {
                _validationMessage = BuildValidation(CreateDefinition());
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Project", GUILayout.Width(120f)))
            {
                var definition = CreateDefinition();
                catalog.Upsert(definition);
                state.SelectedProject = catalog.GetProject(definition.ModId) ?? definition;
                _validationMessage = BuildValidation(definition);
                state.StatusMessage = "Saved project " + definition.GetDisplayName();
            }

            if (state.SelectedProject != null && GUILayout.Button("Delete Selected", GUILayout.Width(120f)))
            {
                catalog.Remove(state.SelectedProject.ModId);
                state.SelectedProject = null;
                _validationMessage = string.Empty;
                ClearFields();
                state.StatusMessage = "Deleted selected project.";
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Validation");
            GUILayout.TextArea(string.IsNullOrEmpty(_validationMessage) ? "Validate the project to confirm the source root, .csproj path, and build output paths." : _validationMessage, GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void DrawLoadedModAssistant(IProjectCatalog catalog, CortexShellState state)
        {
            var loadedMods = ModRegistry.GetLoadedMods();
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
                if (mod == null || string.IsNullOrEmpty(mod.Id))
                {
                    continue;
                }

                var existing = catalog.GetProject(mod.Id);
                if (existing != null && !string.IsNullOrEmpty(existing.SourceRootPath) && Directory.Exists(existing.SourceRootPath))
                {
                    continue;
                }

                var inferredSourceRoot = InferSourceRoot(mod);
                var inferredProjectFile = InferProjectFile(inferredSourceRoot);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(mod.Id + "  |  " + mod.RootPath);
                GUILayout.Label(string.IsNullOrEmpty(inferredSourceRoot)
                    ? "No obvious source root found. Start from the mod root or set a custom workspace path."
                    : "Suggested source root: " + inferredSourceRoot);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Insert Mapping", GUILayout.Width(120f)))
                {
                    var definition = existing ?? new CortexProjectDefinition();
                    definition.ModId = mod.Id;
                    definition.SourceRootPath = inferredSourceRoot;
                    definition.ProjectFilePath = inferredProjectFile;
                    definition.BuildCommandOverride = existing != null ? existing.BuildCommandOverride : string.Empty;
                    definition.OutputAssemblyPath = existing != null ? existing.OutputAssemblyPath : string.Empty;
                    definition.OutputPdbPath = existing != null ? existing.OutputPdbPath : string.Empty;

                    catalog.Upsert(definition);
                    state.SelectedProject = catalog.GetProject(mod.Id) ?? definition;
                    Load(state.SelectedProject);
                    _validationMessage = BuildValidation(state.SelectedProject);
                    state.StatusMessage = "Inserted source mapping for loaded mod " + mod.Id + ".";
                }

                if (!string.IsNullOrEmpty(inferredSourceRoot) && GUILayout.Button("Use In Editor", GUILayout.Width(110f)))
                {
                    _modId = mod.Id;
                    _sourceRoot = inferredSourceRoot;
                    _projectFile = inferredProjectFile;
                    _validationMessage = BuildValidation(CreateDefinition());
                    state.StatusMessage = "Prepared source mapping fields for " + mod.Id + ".";
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

        private void ImportWorkspaceProjects(IProjectCatalog catalog, CortexShellState state)
        {
            var root = state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                state.StatusMessage = "Workspace root is not configured.";
                return;
            }

            string[] projectFiles;
            try
            {
                projectFiles = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                state.StatusMessage = "Workspace import failed: " + ex.Message;
                return;
            }

            var imported = 0;
            for (var i = 0; i < projectFiles.Length; i++)
            {
                var projectFile = projectFiles[i];
                if (projectFile.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    projectFile.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var definition = new CortexProjectDefinition();
                definition.ProjectFilePath = projectFile;
                definition.SourceRootPath = Path.GetDirectoryName(projectFile);
                definition.ModId = Path.GetFileNameWithoutExtension(projectFile);
                definition.BuildCommandOverride = string.Empty;
                definition.OutputAssemblyPath = string.Empty;
                definition.OutputPdbPath = string.Empty;
                catalog.Upsert(definition);
                imported++;
            }

            state.StatusMessage = "Imported " + imported + " project definitions from workspace root.";
        }

        private void AutoDetectProjectFile()
        {
            if (string.IsNullOrEmpty(_sourceRoot) || !Directory.Exists(_sourceRoot))
            {
                return;
            }

            var files = CortexModuleUtil.FindFilesSafe(_sourceRoot, "*.csproj");
            if (files.Length > 0)
            {
                _projectFile = files[0];
                if (string.IsNullOrEmpty(_modId))
                {
                    _modId = Path.GetFileNameWithoutExtension(_projectFile);
                }
            }
        }

        private void ApplyDefaultsFromProjectPath()
        {
            if (!string.IsNullOrEmpty(_projectFile) && File.Exists(_projectFile))
            {
                if (string.IsNullOrEmpty(_sourceRoot))
                {
                    _sourceRoot = Path.GetDirectoryName(_projectFile);
                }
                if (string.IsNullOrEmpty(_modId))
                {
                    _modId = Path.GetFileNameWithoutExtension(_projectFile);
                }
            }

            if (string.IsNullOrEmpty(_modId) && !string.IsNullOrEmpty(_sourceRoot))
            {
                _modId = Path.GetFileName(_sourceRoot);
            }
        }

        private CortexProjectDefinition CreateDefinition()
        {
            ApplyDefaultsFromProjectPath();
            var definition = new CortexProjectDefinition();
            definition.ModId = _modId;
            definition.SourceRootPath = _sourceRoot;
            definition.ProjectFilePath = _projectFile;
            definition.BuildCommandOverride = _buildOverride;
            definition.OutputAssemblyPath = _outputAssembly;
            definition.OutputPdbPath = _outputPdb;
            return definition;
        }

        private string BuildValidation(CortexProjectDefinition definition)
        {
            if (definition == null)
            {
                return "No project selected.";
            }

            var lines = new List<string>();
            lines.Add("Mod ID: " + (string.IsNullOrEmpty(definition.ModId) ? "Missing" : definition.ModId));
            lines.Add("Source Root: " + (Directory.Exists(definition.SourceRootPath) ? "OK" : "Missing"));
            lines.Add("Project File: " + (File.Exists(definition.ProjectFilePath) ? "OK" : "Missing"));
            lines.Add(string.IsNullOrEmpty(definition.OutputAssemblyPath) ? "Output DLL: Resolved from project file." : "Output DLL: " + definition.OutputAssemblyPath);
            lines.Add(string.IsNullOrEmpty(definition.OutputPdbPath) ? "Output PDB: Resolved from project file." : "Output PDB: " + definition.OutputPdbPath);
            return string.Join("\n", lines.ToArray());
        }

        private void Load(CortexProjectDefinition project)
        {
            _modId = project.ModId ?? string.Empty;
            _sourceRoot = project.SourceRootPath ?? string.Empty;
            _projectFile = project.ProjectFilePath ?? string.Empty;
            _buildOverride = project.BuildCommandOverride ?? string.Empty;
            _outputAssembly = project.OutputAssemblyPath ?? string.Empty;
            _outputPdb = project.OutputPdbPath ?? string.Empty;
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
            GUILayout.Label(label, GUILayout.Width(100f));
            value = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private static string InferSourceRoot(ModEntry mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.RootPath) || !Directory.Exists(mod.RootPath))
            {
                return string.Empty;
            }

            var candidates = new[]
            {
                Path.Combine(mod.RootPath, "Source"),
                Path.Combine(mod.RootPath, "src"),
                Path.Combine(mod.RootPath, "Scripts"),
                mod.RootPath
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                if (Directory.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return string.Empty;
        }

        private static string InferProjectFile(string sourceRoot)
        {
            if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                return string.Empty;
            }

            string[] projectFiles;
            try
            {
                projectFiles = Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories);
            }
            catch
            {
                return string.Empty;
            }

            for (var i = 0; i < projectFiles.Length; i++)
            {
                var projectFile = projectFiles[i];
                if (projectFile.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    projectFile.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                return projectFile;
            }

            return string.Empty;
        }
    }
}
