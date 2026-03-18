using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Modules.Build;
using Cortex.Modules.Editor;
using Cortex.Modules.FileExplorer;
using Cortex.Modules.Logs;
using Cortex.Modules.Projects;
using Cortex.Modules.Reference;
using Cortex.Modules.Runtime;
using Cortex.Modules.Search;
using Cortex.Modules.Settings;
using Cortex.Presentation.Models;

namespace Cortex.Shell
{
    internal sealed class CortexShellBuiltInModuleRegistrar
    {
        public void RegisterBuiltIns(CortexShellModuleContributionRegistry registry, CortexShellModuleServices services)
        {
            if (registry == null || services == null)
            {
                return;
            }

            registry.Register(new LogsModuleContribution(services, services, services, services));
            registry.Register(new ProjectsModuleContribution(services, services));
            registry.Register(new FileExplorerModuleContribution(services, services, services));
            registry.Register(new EditorModuleContribution(services, services, services, services, services));
            registry.Register(new BuildModuleContribution(services, services, services, services));
            registry.Register(new ReferenceModuleContribution(services, services, services));
            registry.Register(new SearchModuleContribution(services, services, services));
            registry.Register(new RuntimeToolsModuleContribution(services, services));
            registry.Register(new SettingsModuleContribution(services, services, services, services));
        }
    }

    internal abstract class CortexShellModuleBase : ICortexShellModule
    {
        private readonly string _containerId;

        protected CortexShellModuleBase(string containerId)
        {
            _containerId = containerId ?? string.Empty;
        }

        public string GetUnavailableMessage()
        {
            var missingDependencies = new List<string>();
            CollectMissingDependencies(missingDependencies);
            return missingDependencies.Count > 0
                ? "Module '" + _containerId + "' is missing required services: " + string.Join(", ", missingDependencies.ToArray()) + "."
                : string.Empty;
        }

        public abstract void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow);

        protected abstract void CollectMissingDependencies(List<string> missingDependencies);

        protected static void AddMissing(List<string> missingDependencies, string dependencyName, object dependency)
        {
            if (missingDependencies == null || string.IsNullOrEmpty(dependencyName) || dependency != null)
            {
                return;
            }

            missingDependencies.Add(dependencyName);
        }
    }

    internal sealed class LogsModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellNavigationCapability _navigation;
        private readonly ICortexShellSourceCapability _source;
        private readonly ICortexShellRuntimeLogCapability _runtimeLogs;

        public LogsModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellNavigationCapability navigation,
            ICortexShellSourceCapability source,
            ICortexShellRuntimeLogCapability runtimeLogs)
        {
            _state = state;
            _navigation = navigation;
            _source = source;
            _runtimeLogs = runtimeLogs;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.LogsContainer, typeof(LogsShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new LogsShellModule(_state, _navigation, _source, _runtimeLogs);
        }

        private sealed class LogsShellModule : CortexShellModuleBase
        {
            private readonly LogsModule _module = new LogsModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellNavigationCapability _navigation;
            private readonly ICortexShellSourceCapability _source;
            private readonly ICortexShellRuntimeLogCapability _runtimeLogs;

            public LogsShellModule(
                ICortexShellStateCapability state,
                ICortexShellNavigationCapability navigation,
                ICortexShellSourceCapability source,
                ICortexShellRuntimeLogCapability runtimeLogs)
                : base(CortexWorkbenchIds.LogsContainer)
            {
                _state = state;
                _navigation = navigation;
                _source = source;
                _runtimeLogs = runtimeLogs;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _runtimeLogs != null ? _runtimeLogs.RuntimeLogFeed : null,
                    _source != null ? _source.SourcePathResolver : null,
                    _navigation != null ? _navigation.NavigationService : null,
                    _state != null ? _state.State : null,
                    detachedWindow);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "NavigationService", _navigation != null ? _navigation.NavigationService : null);
                AddMissing(missingDependencies, "SourcePathResolver", _source != null ? _source.SourcePathResolver : null);
                AddMissing(missingDependencies, "RuntimeLogFeed", _runtimeLogs != null ? _runtimeLogs.RuntimeLogFeed : null);
            }
        }
    }

    internal sealed class ProjectsModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellProjectCapability _project;

        public ProjectsModuleContribution(ICortexShellStateCapability state, ICortexShellProjectCapability project)
        {
            _state = state;
            _project = project;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.ProjectsContainer, typeof(ProjectsShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new ProjectsShellModule(_state, _project);
        }

        private sealed class ProjectsShellModule : CortexShellModuleBase
        {
            private readonly ProjectsModule _module = new ProjectsModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellProjectCapability _project;

            public ProjectsShellModule(ICortexShellStateCapability state, ICortexShellProjectCapability project)
                : base(CortexWorkbenchIds.ProjectsContainer)
            {
                _state = state;
                _project = project;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _project != null ? _project.ProjectCatalog : null,
                    _project != null ? _project.ProjectWorkspaceService : null,
                    _project != null ? _project.LoadedModCatalog : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "ProjectCatalog", _project != null ? _project.ProjectCatalog : null);
                AddMissing(missingDependencies, "ProjectWorkspaceService", _project != null ? _project.ProjectWorkspaceService : null);
                AddMissing(missingDependencies, "LoadedModCatalog", _project != null ? _project.LoadedModCatalog : null);
            }
        }
    }

    internal sealed class FileExplorerModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellNavigationCapability _navigation;
        private readonly ICortexShellWorkspaceBrowserCapability _workspace;

        public FileExplorerModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellNavigationCapability navigation,
            ICortexShellWorkspaceBrowserCapability workspace)
        {
            _state = state;
            _navigation = navigation;
            _workspace = workspace;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.FileExplorerContainer, typeof(FileExplorerShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new FileExplorerShellModule(_state, _navigation, _workspace);
        }

        private sealed class FileExplorerShellModule : CortexShellModuleBase
        {
            private readonly FileExplorerModule _module = new FileExplorerModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellNavigationCapability _navigation;
            private readonly ICortexShellWorkspaceBrowserCapability _workspace;

            public FileExplorerShellModule(
                ICortexShellStateCapability state,
                ICortexShellNavigationCapability navigation,
                ICortexShellWorkspaceBrowserCapability workspace)
                : base(CortexWorkbenchIds.FileExplorerContainer)
            {
                _state = state;
                _navigation = navigation;
                _workspace = workspace;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _workspace != null ? _workspace.WorkspaceBrowserService : null,
                    _workspace != null ? _workspace.DecompilerExplorerService : null,
                    _navigation != null ? _navigation.NavigationService : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "NavigationService", _navigation != null ? _navigation.NavigationService : null);
                AddMissing(missingDependencies, "WorkspaceBrowserService", _workspace != null ? _workspace.WorkspaceBrowserService : null);
                AddMissing(missingDependencies, "DecompilerExplorerService", _workspace != null ? _workspace.DecompilerExplorerService : null);
            }
        }
    }

    internal sealed class EditorModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellNavigationCapability _navigation;
        private readonly ICortexShellDocumentCapability _document;
        private readonly ICortexShellWorkbenchCapability _workbench;
        private readonly ICortexShellSearchCapability _search;

        public EditorModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellNavigationCapability navigation,
            ICortexShellDocumentCapability document,
            ICortexShellWorkbenchCapability workbench,
            ICortexShellSearchCapability search)
        {
            _state = state;
            _navigation = navigation;
            _document = document;
            _workbench = workbench;
            _search = search;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.EditorContainer, typeof(EditorShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new EditorShellModule(_state, _navigation, _document, _workbench, _search);
        }

        private sealed class EditorShellModule : CortexShellModuleBase
        {
            private readonly EditorModule _module = new EditorModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellNavigationCapability _navigation;
            private readonly ICortexShellDocumentCapability _document;
            private readonly ICortexShellWorkbenchCapability _workbench;
            private readonly ICortexShellSearchCapability _search;

            public EditorShellModule(
                ICortexShellStateCapability state,
                ICortexShellNavigationCapability navigation,
                ICortexShellDocumentCapability document,
                ICortexShellWorkbenchCapability workbench,
                ICortexShellSearchCapability search)
                : base(CortexWorkbenchIds.EditorContainer)
            {
                _state = state;
                _navigation = navigation;
                _document = document;
                _workbench = workbench;
                _search = search;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _document != null ? _document.DocumentService : null,
                    _navigation != null ? _navigation.NavigationService : null,
                    _workbench != null ? _workbench.CommandRegistry : null,
                    _workbench != null ? _workbench.ContributionRegistry : null,
                    _search != null ? _search.WorkbenchSearchService : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "NavigationService", _navigation != null ? _navigation.NavigationService : null);
                AddMissing(missingDependencies, "DocumentService", _document != null ? _document.DocumentService : null);
                AddMissing(missingDependencies, "CommandRegistry", _workbench != null ? _workbench.CommandRegistry : null);
                AddMissing(missingDependencies, "ContributionRegistry", _workbench != null ? _workbench.ContributionRegistry : null);
                AddMissing(missingDependencies, "WorkbenchSearchService", _search != null ? _search.WorkbenchSearchService : null);
            }
        }
    }

    internal sealed class BuildModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellNavigationCapability _navigation;
        private readonly ICortexShellSourceCapability _source;
        private readonly ICortexShellBuildCapability _build;

        public BuildModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellNavigationCapability navigation,
            ICortexShellSourceCapability source,
            ICortexShellBuildCapability build)
        {
            _state = state;
            _navigation = navigation;
            _source = source;
            _build = build;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.BuildContainer, typeof(BuildShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new BuildShellModule(_state, _navigation, _source, _build);
        }

        private sealed class BuildShellModule : CortexShellModuleBase
        {
            private readonly BuildModule _module = new BuildModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellNavigationCapability _navigation;
            private readonly ICortexShellSourceCapability _source;
            private readonly ICortexShellBuildCapability _build;

            public BuildShellModule(
                ICortexShellStateCapability state,
                ICortexShellNavigationCapability navigation,
                ICortexShellSourceCapability source,
                ICortexShellBuildCapability build)
                : base(CortexWorkbenchIds.BuildContainer)
            {
                _state = state;
                _navigation = navigation;
                _source = source;
                _build = build;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _build != null ? _build.BuildCommandResolver : null,
                    _build != null ? _build.BuildExecutor : null,
                    _build != null ? _build.RestartCoordinator : null,
                    _source != null ? _source.SourcePathResolver : null,
                    _navigation != null ? _navigation.NavigationService : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "NavigationService", _navigation != null ? _navigation.NavigationService : null);
                AddMissing(missingDependencies, "SourcePathResolver", _source != null ? _source.SourcePathResolver : null);
                AddMissing(missingDependencies, "BuildCommandResolver", _build != null ? _build.BuildCommandResolver : null);
                AddMissing(missingDependencies, "BuildExecutor", _build != null ? _build.BuildExecutor : null);
                AddMissing(missingDependencies, "RestartCoordinator", _build != null ? _build.RestartCoordinator : null);
            }
        }
    }

    internal sealed class ReferenceModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellNavigationCapability _navigation;
        private readonly ICortexShellReferenceCapability _reference;

        public ReferenceModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellNavigationCapability navigation,
            ICortexShellReferenceCapability reference)
        {
            _state = state;
            _navigation = navigation;
            _reference = reference;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.ReferenceContainer, typeof(ReferenceShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new ReferenceShellModule(_state, _navigation, _reference);
        }

        private sealed class ReferenceShellModule : CortexShellModuleBase
        {
            private readonly ReferenceModule _module = new ReferenceModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellNavigationCapability _navigation;
            private readonly ICortexShellReferenceCapability _reference;

            public ReferenceShellModule(
                ICortexShellStateCapability state,
                ICortexShellNavigationCapability navigation,
                ICortexShellReferenceCapability reference)
                : base(CortexWorkbenchIds.ReferenceContainer)
            {
                _state = state;
                _navigation = navigation;
                _reference = reference;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _reference != null ? _reference.ReferenceCatalogService : null,
                    _navigation != null ? _navigation.NavigationService : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "NavigationService", _navigation != null ? _navigation.NavigationService : null);
                AddMissing(missingDependencies, "ReferenceCatalogService", _reference != null ? _reference.ReferenceCatalogService : null);
            }
        }
    }

    internal sealed class SearchModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellNavigationCapability _navigation;
        private readonly ICortexShellSearchCapability _search;

        public SearchModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellNavigationCapability navigation,
            ICortexShellSearchCapability search)
        {
            _state = state;
            _navigation = navigation;
            _search = search;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.SearchContainer, typeof(SearchShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new SearchShellModule(_state, _navigation, _search);
        }

        private sealed class SearchShellModule : CortexShellModuleBase
        {
            private readonly SearchModule _module = new SearchModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellNavigationCapability _navigation;
            private readonly ICortexShellSearchCapability _search;

            public SearchShellModule(
                ICortexShellStateCapability state,
                ICortexShellNavigationCapability navigation,
                ICortexShellSearchCapability search)
                : base(CortexWorkbenchIds.SearchContainer)
            {
                _state = state;
                _navigation = navigation;
                _search = search;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _search != null ? _search.WorkbenchSearchService : null,
                    _navigation != null ? _navigation.NavigationService : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "NavigationService", _navigation != null ? _navigation.NavigationService : null);
                AddMissing(missingDependencies, "WorkbenchSearchService", _search != null ? _search.WorkbenchSearchService : null);
            }
        }
    }

    internal sealed class RuntimeToolsModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellRuntimeToolCapability _runtimeTools;

        public RuntimeToolsModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellRuntimeToolCapability runtimeTools)
        {
            _state = state;
            _runtimeTools = runtimeTools;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.RuntimeContainer, typeof(RuntimeToolsShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new RuntimeToolsShellModule(_state, _runtimeTools);
        }

        private sealed class RuntimeToolsShellModule : CortexShellModuleBase
        {
            private readonly RuntimeToolsModule _module = new RuntimeToolsModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellRuntimeToolCapability _runtimeTools;

            public RuntimeToolsShellModule(
                ICortexShellStateCapability state,
                ICortexShellRuntimeToolCapability runtimeTools)
                : base(CortexWorkbenchIds.RuntimeContainer)
            {
                _state = state;
                _runtimeTools = runtimeTools;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _runtimeTools != null ? _runtimeTools.RuntimeToolBridge : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "RuntimeToolBridge", _runtimeTools != null ? _runtimeTools.RuntimeToolBridge : null);
            }
        }
    }

    internal sealed class SettingsModuleContribution : ICortexShellModuleContribution
    {
        private readonly ICortexShellStateCapability _state;
        private readonly ICortexShellSettingsCapability _settings;
        private readonly ICortexShellProjectCapability _project;
        private readonly ICortexShellWorkbenchCapability _workbench;

        public SettingsModuleContribution(
            ICortexShellStateCapability state,
            ICortexShellSettingsCapability settings,
            ICortexShellProjectCapability project,
            ICortexShellWorkbenchCapability workbench)
        {
            _state = state;
            _settings = settings;
            _project = project;
            _workbench = workbench;
            Descriptor = new CortexShellModuleDescriptor(CortexWorkbenchIds.SettingsContainer, typeof(SettingsShellModule));
        }

        public CortexShellModuleDescriptor Descriptor { get; private set; }

        public ICortexShellModule CreateModule()
        {
            return new SettingsShellModule(_state, _settings, _project, _workbench);
        }

        private sealed class SettingsShellModule : CortexShellModuleBase
        {
            private readonly SettingsModule _module = new SettingsModule();
            private readonly ICortexShellStateCapability _state;
            private readonly ICortexShellSettingsCapability _settings;
            private readonly ICortexShellProjectCapability _project;
            private readonly ICortexShellWorkbenchCapability _workbench;

            public SettingsShellModule(
                ICortexShellStateCapability state,
                ICortexShellSettingsCapability settings,
                ICortexShellProjectCapability project,
                ICortexShellWorkbenchCapability workbench)
                : base(CortexWorkbenchIds.SettingsContainer)
            {
                _state = state;
                _settings = settings;
                _project = project;
                _workbench = workbench;
            }

            public override void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
            {
                _module.Draw(
                    _settings != null ? _settings.SettingsStore : null,
                    _project != null ? _project.ProjectCatalog : null,
                    _project != null ? _project.ProjectWorkspaceService : null,
                    _project != null ? _project.LoadedModCatalog : null,
                    snapshot,
                    _workbench != null ? _workbench.ThemeState : null,
                    _state != null ? _state.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _state != null ? _state.State : null);
                AddMissing(missingDependencies, "SettingsStore", _settings != null ? _settings.SettingsStore : null);
                AddMissing(missingDependencies, "ProjectCatalog", _project != null ? _project.ProjectCatalog : null);
                AddMissing(missingDependencies, "ProjectWorkspaceService", _project != null ? _project.ProjectWorkspaceService : null);
                AddMissing(missingDependencies, "LoadedModCatalog", _project != null ? _project.LoadedModCatalog : null);
                AddMissing(missingDependencies, "ThemeState", _workbench != null ? _workbench.ThemeState : null);
            }
        }
    }
}
