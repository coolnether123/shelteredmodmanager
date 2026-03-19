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
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;

namespace Cortex
{
    internal sealed class CortexShellBuiltInModuleRegistrar
    {
        public void RegisterBuiltIns(CortexShellModuleContributionRegistry registry, CortexShellModuleServices services)
        {
            if (registry == null || services == null)
            {
                return;
            }

            registry.Register(new LogsModuleContribution(services));
            registry.Register(new ProjectsModuleContribution(services));
            registry.Register(new FileExplorerModuleContribution(services));
            registry.Register(new EditorModuleContribution(services));
            registry.Register(new BuildModuleContribution(services));
            registry.Register(new ReferenceModuleContribution(services));
            registry.Register(new SearchModuleContribution(services));
            registry.Register(new RuntimeToolsModuleContribution(services));
            registry.Register(new SettingsModuleContribution(services));
        }
    }

    internal abstract class CortexShellModuleBase : IWorkbenchModule
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

        public abstract void Render(WorkbenchModuleRenderContext context, bool detachedWindow);

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

    internal sealed class LogsModuleContribution : IWorkbenchModuleContribution
    {
        private readonly ILogsModuleServices _services;

        public LogsModuleContribution(ILogsModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.LogsContainer, typeof(LogsShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new LogsShellModule(_services);
        }

        private sealed class LogsShellModule : CortexShellModuleBase
        {
            private readonly LogsModule _module = new LogsModule();
            private readonly ILogsModuleServices _services;

            public LogsShellModule(ILogsModuleServices services)
                : base(CortexWorkbenchIds.LogsContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.RuntimeLogFeed : null,
                    _services != null ? _services.SourcePathResolver : null,
                    _services != null ? _services.NavigationService : null,
                    _services != null ? _services.State : null,
                    detachedWindow);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "NavigationService", _services != null ? _services.NavigationService : null);
                AddMissing(missingDependencies, "SourcePathResolver", _services != null ? _services.SourcePathResolver : null);
                AddMissing(missingDependencies, "RuntimeLogFeed", _services != null ? _services.RuntimeLogFeed : null);
            }
        }
    }

    internal sealed class ProjectsModuleContribution : IWorkbenchModuleContribution
    {
        private readonly IProjectsModuleServices _services;

        public ProjectsModuleContribution(IProjectsModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.ProjectsContainer, typeof(ProjectsShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new ProjectsShellModule(_services);
        }

        private sealed class ProjectsShellModule : CortexShellModuleBase
        {
            private readonly ProjectsModule _module = new ProjectsModule();
            private readonly IProjectsModuleServices _services;

            public ProjectsShellModule(IProjectsModuleServices services)
                : base(CortexWorkbenchIds.ProjectsContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.ProjectCatalog : null,
                    _services != null ? _services.ProjectWorkspaceService : null,
                    _services != null ? _services.LoadedModCatalog : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "ProjectCatalog", _services != null ? _services.ProjectCatalog : null);
                AddMissing(missingDependencies, "ProjectWorkspaceService", _services != null ? _services.ProjectWorkspaceService : null);
                AddMissing(missingDependencies, "LoadedModCatalog", _services != null ? _services.LoadedModCatalog : null);
            }
        }
    }

    internal sealed class FileExplorerModuleContribution : IWorkbenchModuleContribution
    {
        private readonly IFileExplorerModuleServices _services;

        public FileExplorerModuleContribution(IFileExplorerModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.FileExplorerContainer, typeof(FileExplorerShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new FileExplorerShellModule(_services);
        }

        private sealed class FileExplorerShellModule : CortexShellModuleBase
        {
            private readonly FileExplorerModule _module = new FileExplorerModule();
            private readonly IFileExplorerModuleServices _services;

            public FileExplorerShellModule(IFileExplorerModuleServices services)
                : base(CortexWorkbenchIds.FileExplorerContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.WorkspaceBrowserService : null,
                    _services != null ? _services.DecompilerExplorerService : null,
                    _services != null ? _services.NavigationService : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "NavigationService", _services != null ? _services.NavigationService : null);
                AddMissing(missingDependencies, "WorkspaceBrowserService", _services != null ? _services.WorkspaceBrowserService : null);
                AddMissing(missingDependencies, "DecompilerExplorerService", _services != null ? _services.DecompilerExplorerService : null);
            }
        }
    }

    internal sealed class EditorModuleContribution : IWorkbenchModuleContribution
    {
        private readonly IEditorModuleServices _services;

        public EditorModuleContribution(IEditorModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.EditorContainer, typeof(EditorShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new EditorShellModule(_services);
        }

        private sealed class EditorShellModule : CortexShellModuleBase
        {
            private readonly EditorModule _module = new EditorModule();
            private readonly IEditorModuleServices _services;

            public EditorShellModule(IEditorModuleServices services)
                : base(CortexWorkbenchIds.EditorContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.DocumentService : null,
                    _services != null ? _services.NavigationService : null,
                    _services != null ? _services.CommandRegistry : null,
                    _services != null ? _services.ContributionRegistry : null,
                    _services != null ? _services.WorkbenchSearchService : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "NavigationService", _services != null ? _services.NavigationService : null);
                AddMissing(missingDependencies, "DocumentService", _services != null ? _services.DocumentService : null);
                AddMissing(missingDependencies, "CommandRegistry", _services != null ? _services.CommandRegistry : null);
                AddMissing(missingDependencies, "ContributionRegistry", _services != null ? _services.ContributionRegistry : null);
                AddMissing(missingDependencies, "WorkbenchSearchService", _services != null ? _services.WorkbenchSearchService : null);
            }
        }
    }

    internal sealed class BuildModuleContribution : IWorkbenchModuleContribution
    {
        private readonly IBuildModuleServices _services;

        public BuildModuleContribution(IBuildModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.BuildContainer, typeof(BuildShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new BuildShellModule(_services);
        }

        private sealed class BuildShellModule : CortexShellModuleBase
        {
            private readonly BuildModule _module = new BuildModule();
            private readonly IBuildModuleServices _services;

            public BuildShellModule(IBuildModuleServices services)
                : base(CortexWorkbenchIds.BuildContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.BuildCommandResolver : null,
                    _services != null ? _services.BuildExecutor : null,
                    _services != null ? _services.RestartCoordinator : null,
                    _services != null ? _services.SourcePathResolver : null,
                    _services != null ? _services.NavigationService : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "NavigationService", _services != null ? _services.NavigationService : null);
                AddMissing(missingDependencies, "SourcePathResolver", _services != null ? _services.SourcePathResolver : null);
                AddMissing(missingDependencies, "BuildCommandResolver", _services != null ? _services.BuildCommandResolver : null);
                AddMissing(missingDependencies, "BuildExecutor", _services != null ? _services.BuildExecutor : null);
                AddMissing(missingDependencies, "RestartCoordinator", _services != null ? _services.RestartCoordinator : null);
            }
        }
    }

    internal sealed class ReferenceModuleContribution : IWorkbenchModuleContribution
    {
        private readonly IReferenceModuleServices _services;

        public ReferenceModuleContribution(IReferenceModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.ReferenceContainer, typeof(ReferenceShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new ReferenceShellModule(_services);
        }

        private sealed class ReferenceShellModule : CortexShellModuleBase
        {
            private readonly ReferenceModule _module = new ReferenceModule();
            private readonly IReferenceModuleServices _services;

            public ReferenceShellModule(IReferenceModuleServices services)
                : base(CortexWorkbenchIds.ReferenceContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.ReferenceCatalogService : null,
                    _services != null ? _services.NavigationService : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "NavigationService", _services != null ? _services.NavigationService : null);
                AddMissing(missingDependencies, "ReferenceCatalogService", _services != null ? _services.ReferenceCatalogService : null);
            }
        }
    }

    internal sealed class SearchModuleContribution : IWorkbenchModuleContribution
    {
        private readonly ISearchModuleServices _services;

        public SearchModuleContribution(ISearchModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.SearchContainer, typeof(SearchShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new SearchShellModule(_services);
        }

        private sealed class SearchShellModule : CortexShellModuleBase
        {
            private readonly SearchModule _module = new SearchModule();
            private readonly ISearchModuleServices _services;

            public SearchShellModule(ISearchModuleServices services)
                : base(CortexWorkbenchIds.SearchContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.WorkbenchSearchService : null,
                    _services != null ? _services.NavigationService : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "NavigationService", _services != null ? _services.NavigationService : null);
                AddMissing(missingDependencies, "WorkbenchSearchService", _services != null ? _services.WorkbenchSearchService : null);
            }
        }
    }

    internal sealed class RuntimeToolsModuleContribution : IWorkbenchModuleContribution
    {
        private readonly IRuntimeToolsModuleServices _services;

        public RuntimeToolsModuleContribution(IRuntimeToolsModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.RuntimeContainer, typeof(RuntimeToolsShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new RuntimeToolsShellModule(_services);
        }

        private sealed class RuntimeToolsShellModule : CortexShellModuleBase
        {
            private readonly RuntimeToolsModule _module = new RuntimeToolsModule();
            private readonly IRuntimeToolsModuleServices _services;

            public RuntimeToolsShellModule(IRuntimeToolsModuleServices services)
                : base(CortexWorkbenchIds.RuntimeContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.RuntimeToolBridge : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "RuntimeToolBridge", _services != null ? _services.RuntimeToolBridge : null);
            }
        }
    }

    internal sealed class SettingsModuleContribution : IWorkbenchModuleContribution
    {
        private readonly ISettingsModuleServices _services;

        public SettingsModuleContribution(ISettingsModuleServices services)
        {
            _services = services;
            Descriptor = new WorkbenchModuleDescriptor(CortexWorkbenchIds.SettingsContainer, typeof(SettingsShellModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule()
        {
            return new SettingsShellModule(_services);
        }

        private sealed class SettingsShellModule : CortexShellModuleBase
        {
            private readonly SettingsModule _module = new SettingsModule();
            private readonly ISettingsModuleServices _services;

            public SettingsShellModule(ISettingsModuleServices services)
                : base(CortexWorkbenchIds.SettingsContainer)
            {
                _services = services;
            }

            public override void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                _module.Draw(
                    _services != null ? _services.SettingsStore : null,
                    _services != null ? _services.ProjectCatalog : null,
                    _services != null ? _services.ProjectWorkspaceService : null,
                    _services != null ? _services.LoadedModCatalog : null,
                    context != null ? context.Snapshot : new WorkbenchPresentationSnapshot(),
                    _services != null ? _services.ThemeState : null,
                    _services != null ? _services.State : null);
            }

            protected override void CollectMissingDependencies(List<string> missingDependencies)
            {
                AddMissing(missingDependencies, "State", _services != null ? _services.State : null);
                AddMissing(missingDependencies, "SettingsStore", _services != null ? _services.SettingsStore : null);
                AddMissing(missingDependencies, "ProjectCatalog", _services != null ? _services.ProjectCatalog : null);
                AddMissing(missingDependencies, "ProjectWorkspaceService", _services != null ? _services.ProjectWorkspaceService : null);
                AddMissing(missingDependencies, "LoadedModCatalog", _services != null ? _services.LoadedModCatalog : null);
                AddMissing(missingDependencies, "ThemeState", _services != null ? _services.ThemeState : null);
            }
        }
    }
}
