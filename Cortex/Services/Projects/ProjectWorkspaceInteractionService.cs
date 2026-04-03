using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services.Projects
{
    internal sealed class ProjectWorkspaceDraft
    {
        public string ModId = string.Empty;
        public string SourceRoot = string.Empty;
        public string ProjectFile = string.Empty;
        public string BuildOverride = string.Empty;
        public string OutputAssembly = string.Empty;
        public string OutputPdb = string.Empty;
        public string ValidationMessage = string.Empty;
    }

    internal sealed class ProjectWorkspaceListItem
    {
        public CortexProjectDefinition Project;
        public string DisplayName = string.Empty;
        public string SourceRootPath = string.Empty;
    }

    internal sealed class LoadedModProjectSuggestion
    {
        public LoadedModInfo LoadedMod;
        public string SuggestedSourceRoot = string.Empty;
        public ProjectWorkspaceAnalysis Analysis;
    }

    internal sealed class ProjectWorkspaceInteractionService
    {
        public ProjectWorkspaceDraft CreateDraft()
        {
            return new ProjectWorkspaceDraft();
        }

        public ProjectWorkspaceDraft LoadDraft(CortexProjectDefinition project)
        {
            var draft = CreateDraft();
            ApplyProjectToDraft(project, draft);
            return draft;
        }

        public void ClearDraft(ProjectWorkspaceDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            draft.ModId = string.Empty;
            draft.SourceRoot = string.Empty;
            draft.ProjectFile = string.Empty;
            draft.BuildOverride = string.Empty;
            draft.OutputAssembly = string.Empty;
            draft.OutputPdb = string.Empty;
            draft.ValidationMessage = string.Empty;
        }

        public IList<ProjectWorkspaceListItem> BuildProjectList(IProjectCatalog catalog, string searchText)
        {
            var items = new List<ProjectWorkspaceListItem>();
            var projects = catalog != null ? catalog.GetProjects() : null;
            if (projects == null)
            {
                return items;
            }

            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                var displayName = project.GetDisplayName();
                if (!MatchesProject(displayName, project.SourceRootPath, searchText))
                {
                    continue;
                }

                items.Add(new ProjectWorkspaceListItem
                {
                    Project = project,
                    DisplayName = displayName,
                    SourceRootPath = project.SourceRootPath ?? string.Empty
                });
            }

            return items;
        }

        public IList<LoadedModProjectSuggestion> BuildLoadedModSuggestions(
            IProjectCatalog catalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog)
        {
            var suggestions = new List<LoadedModProjectSuggestion>();
            var loadedMods = loadedModCatalog != null ? loadedModCatalog.GetLoadedMods() : null;
            if (loadedMods == null)
            {
                return suggestions;
            }

            for (var i = 0; i < loadedMods.Count; i++)
            {
                var mod = loadedMods[i];
                if (mod == null || string.IsNullOrEmpty(mod.ModId))
                {
                    continue;
                }

                var existing = catalog != null ? catalog.GetProject(mod.ModId) : null;
                if (existing != null &&
                    !string.IsNullOrEmpty(existing.SourceRootPath) &&
                    Directory.Exists(existing.SourceRootPath))
                {
                    continue;
                }

                var sourceRoot = workspaceService != null
                    ? workspaceService.FindLikelySourceRoot(mod.RootPath)
                    : string.Empty;
                var analysis = workspaceService != null
                    ? workspaceService.AnalyzeSourceRoot(sourceRoot, mod.ModId)
                    : null;

                suggestions.Add(new LoadedModProjectSuggestion
                {
                    LoadedMod = mod,
                    SuggestedSourceRoot = sourceRoot ?? string.Empty,
                    Analysis = analysis
                });
            }

            return suggestions;
        }

        public ProjectWorkspaceAnalysis PrepareAnalysis(
            IProjectWorkspaceService workspaceService,
            ProjectWorkspaceDraft draft,
            string preferredModId,
            CortexShellState state)
        {
            var analysis = workspaceService != null
                ? workspaceService.AnalyzeSourceRoot(draft != null ? draft.SourceRoot : string.Empty, preferredModId)
                : null;
            if (analysis == null)
            {
                if (state != null)
                {
                    state.StatusMessage = "Project workspace analysis is unavailable.";
                    state.Diagnostics.Add(state.StatusMessage);
                }

                return null;
            }

            AppendDiagnostics(state, analysis.Diagnostics);
            if (state != null)
            {
                state.StatusMessage = analysis.StatusMessage;
            }

            return analysis;
        }

        public bool ApplySourceFolder(
            IProjectCatalog catalog,
            IProjectWorkspaceService workspaceService,
            ProjectWorkspaceDraft draft,
            CortexShellState state,
            bool saveProject)
        {
            var analysis = PrepareAnalysis(workspaceService, draft, draft != null ? draft.ModId : string.Empty, state);
            if (analysis == null || !analysis.Success || analysis.Definition == null)
            {
                return false;
            }

            ApplyDraftAdvancedFields(draft, analysis.Definition);
            ApplyProjectToDraft(analysis.Definition, draft);
            draft.ValidationMessage = BuildValidation(workspaceService, analysis.Definition);

            if (saveProject && catalog != null)
            {
                catalog.Upsert(analysis.Definition);
                if (state != null)
                {
                    state.SelectedProject = catalog.GetProject(analysis.Definition.ModId) ?? analysis.Definition;
                }
            }

            if (state != null)
            {
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                state.StatusMessage = "Applied source folder for " + analysis.Definition.GetDisplayName() + ".";
            }

            return true;
        }

        public void ImportWorkspaceProjects(IProjectCatalog catalog, IProjectWorkspaceService workspaceService, CortexShellState state)
        {
            var root = state != null && state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty;
            var importResult = workspaceService != null
                ? workspaceService.DiscoverWorkspaceProjects(root)
                : new ProjectWorkspaceImportResult();
            for (var i = 0; i < importResult.Definitions.Count; i++)
            {
                if (catalog != null)
                {
                    catalog.Upsert(importResult.Definitions[i]);
                }
            }

            if (state != null)
            {
                state.StatusMessage = importResult.StatusMessage;
                AppendDiagnostics(state, importResult.Diagnostics);
            }
        }

        public void SaveProject(IProjectCatalog catalog, IProjectWorkspaceService workspaceService, ProjectWorkspaceDraft draft, CortexShellState state)
        {
            var definition = CreateDefinition(draft);
            if (catalog != null)
            {
                catalog.Upsert(definition);
            }

            if (state != null)
            {
                state.SelectedProject = catalog != null ? catalog.GetProject(definition.ModId) ?? definition : definition;
                state.StatusMessage = "Saved project " + definition.GetDisplayName();
                state.Diagnostics.Add(
                    "Saved project mapping for " +
                    definition.GetDisplayName() +
                    " using source folder " +
                    (definition.SourceRootPath ?? string.Empty) +
                    ".");
            }

            if (draft != null)
            {
                draft.ValidationMessage = BuildValidation(workspaceService, definition);
            }
        }

        public void DeleteSelected(IProjectCatalog catalog, ProjectWorkspaceDraft draft, CortexShellState state)
        {
            var selected = state != null ? state.SelectedProject : null;
            if (selected != null && catalog != null)
            {
                catalog.Remove(selected.ModId);
            }

            if (state != null)
            {
                state.SelectedProject = null;
                state.StatusMessage = "Deleted selected project.";
                state.Diagnostics.Add("Deleted the selected project mapping.");
            }

            ClearDraft(draft);
        }

        public void UseSelectedProjectSource(IProjectWorkspaceService workspaceService, CortexProjectDefinition project, ProjectWorkspaceDraft draft, CortexShellState state)
        {
            ApplyProjectToDraft(project, draft);
            if (draft != null)
            {
                draft.ValidationMessage = BuildValidation(workspaceService, project);
            }

            if (state != null && project != null)
            {
                state.StatusMessage = "Loaded the selected project's source mapping into the editor.";
                state.Diagnostics.Add("Prepared the selected project's source folder for editing.");
            }
        }

        public void UseWorkspaceRoot(IProjectWorkspaceService workspaceService, ProjectWorkspaceDraft draft, CortexShellState state)
        {
            if (draft == null)
            {
                return;
            }

            draft.SourceRoot = state != null && state.Settings != null
                ? state.Settings.WorkspaceRootPath ?? string.Empty
                : string.Empty;
            PrepareAnalysis(workspaceService, draft, string.Empty, state);
        }

        public void InsertSuggestion(
            IProjectCatalog catalog,
            IProjectWorkspaceService workspaceService,
            LoadedModProjectSuggestion suggestion,
            ProjectWorkspaceDraft draft,
            CortexShellState state)
        {
            if (suggestion == null || suggestion.Analysis == null || suggestion.Analysis.Definition == null)
            {
                return;
            }

            if (catalog != null)
            {
                catalog.Upsert(suggestion.Analysis.Definition);
            }

            var project = catalog != null
                ? catalog.GetProject(suggestion.LoadedMod != null ? suggestion.LoadedMod.ModId : suggestion.Analysis.Definition.ModId) ?? suggestion.Analysis.Definition
                : suggestion.Analysis.Definition;
            ApplyProjectToDraft(project, draft);
            if (draft != null)
            {
                draft.ValidationMessage = BuildValidation(workspaceService, project);
            }

            if (state != null)
            {
                state.SelectedProject = project;
                var modId = suggestion.LoadedMod != null ? suggestion.LoadedMod.ModId : suggestion.Analysis.Definition.ModId;
                state.StatusMessage = "Inserted source mapping for loaded mod " + modId + ".";
                state.Diagnostics.Add("Inserted mapping for loaded mod " + modId + ".");
            }
        }

        public void UseSuggestion(IProjectWorkspaceService workspaceService, LoadedModProjectSuggestion suggestion, ProjectWorkspaceDraft draft, CortexShellState state)
        {
            if (suggestion == null || suggestion.Analysis == null || suggestion.Analysis.Definition == null)
            {
                return;
            }

            ApplyProjectToDraft(suggestion.Analysis.Definition, draft);
            if (draft != null)
            {
                draft.ValidationMessage = BuildValidation(workspaceService, suggestion.Analysis.Definition);
            }

            if (state != null)
            {
                var modId = suggestion.LoadedMod != null ? suggestion.LoadedMod.ModId : suggestion.Analysis.Definition.ModId;
                state.StatusMessage = "Prepared source mapping fields for " + modId + ".";
                state.Diagnostics.Add("Prepared inferred source folder for loaded mod " + modId + ".");
            }
        }

        public string BuildValidation(IProjectWorkspaceService workspaceService, CortexProjectDefinition definition)
        {
            var validation = workspaceService != null ? workspaceService.Validate(definition) : new ProjectValidationResult();
            return string.Join("\n", validation.Lines.ToArray());
        }

        public CortexProjectDefinition CreateDefinition(ProjectWorkspaceDraft draft)
        {
            var modId = draft != null ? draft.ModId : string.Empty;
            var sourceRoot = draft != null ? draft.SourceRoot : string.Empty;
            if (string.IsNullOrEmpty(modId) && !string.IsNullOrEmpty(sourceRoot))
            {
                modId = Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            return new CortexProjectDefinition
            {
                ModId = modId,
                SourceRootPath = sourceRoot,
                ProjectFilePath = draft != null ? draft.ProjectFile : string.Empty,
                BuildCommandOverride = draft != null ? draft.BuildOverride : string.Empty,
                OutputAssemblyPath = draft != null ? draft.OutputAssembly : string.Empty,
                OutputPdbPath = draft != null ? draft.OutputPdb : string.Empty
            };
        }

        private static void ApplyDraftAdvancedFields(ProjectWorkspaceDraft draft, CortexProjectDefinition definition)
        {
            if (draft == null || definition == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(draft.BuildOverride))
            {
                definition.BuildCommandOverride = draft.BuildOverride;
            }

            if (!string.IsNullOrEmpty(draft.OutputAssembly))
            {
                definition.OutputAssemblyPath = draft.OutputAssembly;
            }

            if (!string.IsNullOrEmpty(draft.OutputPdb))
            {
                definition.OutputPdbPath = draft.OutputPdb;
            }
        }

        private static void ApplyProjectToDraft(CortexProjectDefinition project, ProjectWorkspaceDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            draft.ModId = project != null ? project.ModId ?? string.Empty : string.Empty;
            draft.SourceRoot = project != null ? project.SourceRootPath ?? string.Empty : string.Empty;
            draft.ProjectFile = project != null ? project.ProjectFilePath ?? string.Empty : string.Empty;
            draft.BuildOverride = project != null ? project.BuildCommandOverride ?? string.Empty : string.Empty;
            draft.OutputAssembly = project != null ? project.OutputAssemblyPath ?? string.Empty : string.Empty;
            draft.OutputPdb = project != null ? project.OutputPdbPath ?? string.Empty : string.Empty;
        }

        private static void AppendDiagnostics(CortexShellState state, IList<string> diagnostics)
        {
            if (state == null || diagnostics == null)
            {
                return;
            }

            for (var i = 0; i < diagnostics.Count; i++)
            {
                state.Diagnostics.Add(diagnostics[i]);
            }
        }

        private static bool MatchesProject(string displayName, string sourceRootPath, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                return true;
            }

            return (displayName ?? string.Empty).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (sourceRootPath ?? string.Empty).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
