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
    internal sealed class RuntimeDesktopBridgeWorkspaceFeature
    {
        private readonly CortexShellState _shellState;
        private readonly Func<IProjectCatalog> _projectCatalogAccessor;
        private readonly Func<ShellSettings> _settingsAccessor;
        private readonly ProjectWorkspaceService _projectWorkspaceService = new ProjectWorkspaceService();
        private readonly List<WorkspaceProjectDefinition> _projects = new List<WorkspaceProjectDefinition>();
        private WorkspaceProjectDefinition _selectedProject;
        private WorkspaceFileNode _workspaceTreeRoot;
        private string _previewFilePath = string.Empty;
        private string _previewText = string.Empty;
        private string _cachedWorkspaceToken = string.Empty;

        public RuntimeDesktopBridgeWorkspaceFeature(
            CortexShellState shellState,
            Func<IProjectCatalog> projectCatalogAccessor,
            Func<ShellSettings> settingsAccessor)
        {
            _shellState = shellState ?? new CortexShellState();
            _projectCatalogAccessor = projectCatalogAccessor;
            _settingsAccessor = settingsAccessor;
        }

        public void Initialize()
        {
            LoadProjectsFromCatalog();
            SelectProjectById(ResolveSelectedProjectId(_shellState.SelectedProject), false);
            RefreshWorkspaceTree();
            _cachedWorkspaceToken = BuildWorkspaceToken();
        }

        public bool SynchronizeFromRuntime()
        {
            LoadProjectsFromCatalog();
            SelectProjectById(ResolveSelectedProjectId(_shellState.SelectedProject), false);
            RefreshWorkspaceTree();

            var currentToken = BuildWorkspaceToken();
            if (string.Equals(_cachedWorkspaceToken, currentToken, StringComparison.Ordinal))
            {
                return false;
            }

            _cachedWorkspaceToken = currentToken;
            return true;
        }

        public void RefreshFromSettings()
        {
            RefreshWorkspaceTree();
            _cachedWorkspaceToken = BuildWorkspaceToken();
        }

        public bool TryApplyIntent(BridgeIntentMessage intent, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (intent == null)
            {
                return false;
            }

            switch (intent.IntentType)
            {
                case BridgeIntentType.AnalyzeWorkspace:
                    AnalyzeWorkspace();
                    statusMessage = _shellState.StatusMessage ?? "Analyzed workspace root.";
                    break;
                case BridgeIntentType.ImportWorkspace:
                    ImportWorkspace();
                    statusMessage = _shellState.StatusMessage ?? "Imported workspace projects.";
                    break;
                case BridgeIntentType.SelectProject:
                    SelectProjectById(intent.ProjectId, true);
                    statusMessage = _shellState.StatusMessage ?? "Selected project.";
                    break;
                case BridgeIntentType.OpenFilePreview:
                    OpenFilePreview(intent.FilePath);
                    statusMessage = _shellState.StatusMessage ?? "Opened file preview.";
                    break;
                default:
                    return false;
            }

            _cachedWorkspaceToken = BuildWorkspaceToken();
            return true;
        }

        public WorkspaceBridgeSnapshot BuildSnapshot()
        {
            var snapshot = new WorkspaceBridgeSnapshot
            {
                WorkspaceRootPath = ResolveWorkspaceRootPath(),
                SelectedProjectId = _selectedProject != null ? _selectedProject.ProjectId ?? string.Empty : string.Empty,
                WorkspaceTreeRoot = RuntimeDesktopBridgeModelCloner.CloneWorkspaceFileNode(_workspaceTreeRoot),
                PreviewFilePath = _previewFilePath ?? string.Empty,
                PreviewText = _previewText ?? string.Empty
            };

            snapshot.Projects.AddRange(_projects.Select(RuntimeDesktopBridgeModelCloner.CloneProjectDefinition));
            return snapshot;
        }

        private void AnalyzeWorkspace()
        {
            var analysis = _projectWorkspaceService.AnalyzeSourceRoot(ResolveWorkspaceRootPath(), _selectedProject != null ? _selectedProject.ProjectId : string.Empty);
            _projects.Clear();
            if (analysis.Success && analysis.Definition != null)
            {
                _projects.Add(RuntimeDesktopBridgeModelCloner.CloneProjectDefinition(analysis.Definition));
                _selectedProject = _projects[0];
                UpsertProjectCatalog(_selectedProject);
                ApplySelectedProjectToRuntime(_selectedProject);
            }

            RefreshWorkspaceTree();
            _shellState.StatusMessage = analysis.StatusMessage ?? "Analyzed workspace root.";
        }

        private void ImportWorkspace()
        {
            var result = _projectWorkspaceService.DiscoverWorkspaceProjects(ResolveWorkspaceRootPath());
            _projects.Clear();
            foreach (var definition in result.Definitions.OrderBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var cloned = RuntimeDesktopBridgeModelCloner.CloneProjectDefinition(definition);
                _projects.Add(cloned);
                UpsertProjectCatalog(cloned);
            }

            _selectedProject = _projects.Count > 0 ? _projects[0] : null;
            ApplySelectedProjectToRuntime(_selectedProject);
            RefreshWorkspaceTree();
            _shellState.StatusMessage = result.StatusMessage ?? "Imported workspace projects.";
        }

        private void SelectProjectById(string projectId, bool updateStatusMessage)
        {
            _selectedProject = _projects.FirstOrDefault(project => string.Equals(project.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                ?? (_projects.Count > 0 ? _projects[0] : null);
            ApplySelectedProjectToRuntime(_selectedProject);
            RefreshWorkspaceTree();
            if (updateStatusMessage)
            {
                _shellState.StatusMessage = _selectedProject != null
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
            _shellState.StatusMessage = "Opened " + Path.GetFileName(filePath) + ".";
        }

        private void LoadProjectsFromCatalog()
        {
            var selectedProjectId = ResolveSelectedProjectId(_shellState.SelectedProject);
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
                : ResolveWorkspaceRootPath();
            _workspaceTreeRoot = _projectWorkspaceService.BuildWorkspaceTree(rootPath);
            if (!string.IsNullOrEmpty(_previewFilePath) && !File.Exists(_previewFilePath))
            {
                _previewFilePath = string.Empty;
                _previewText = string.Empty;
            }
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

        private string ResolveWorkspaceRootPath()
        {
            var settings = _settingsAccessor != null ? _settingsAccessor() : null;
            return settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty;
        }

        private string BuildWorkspaceToken()
        {
            var projectToken = string.Join(
                ";",
                _projects.Select(project => (project.ProjectId ?? string.Empty) + "|" + (project.SourceRootPath ?? string.Empty) + "|" + (project.ProjectFilePath ?? string.Empty)).ToArray());
            return string.Join(
                "|",
                new[]
                {
                    ResolveWorkspaceRootPath(),
                    projectToken,
                    _selectedProject != null ? _selectedProject.ProjectId ?? string.Empty : string.Empty,
                    _previewFilePath ?? string.Empty,
                    (!string.IsNullOrEmpty(_previewFilePath) && File.Exists(_previewFilePath)).ToString()
                });
        }

        private static string ResolveSelectedProjectId(CortexProjectDefinition definition)
        {
            return definition != null ? definition.ModId ?? string.Empty : string.Empty;
        }
    }
}
