using System;
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
    internal sealed class CortexShellModuleDescriptorCatalog
    {
        private readonly Dictionary<string, CortexShellModuleDescriptor> _descriptors = new Dictionary<string, CortexShellModuleDescriptor>(StringComparer.OrdinalIgnoreCase);

        public CortexShellModuleDescriptorCatalog()
        {
            RegisterBuiltInDescriptors();
        }

        /// <summary>
        /// Resolves the built-in descriptor registered for a workbench container.
        /// </summary>
        /// <param name="containerId">The container identifier to resolve.</param>
        /// <returns>The matching descriptor when one is registered; otherwise <c>null</c>.</returns>
        public CortexShellModuleDescriptor FindDescriptor(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            CortexShellModuleDescriptor descriptor;
            return _descriptors.TryGetValue(containerId, out descriptor) ? descriptor : null;
        }

        private void RegisterBuiltInDescriptors()
        {
            Register(CreateLogsDescriptor());
            Register(CreateProjectsDescriptor());
            Register(CreateFileExplorerDescriptor());
            Register(CreateEditorDescriptor());
            Register(CreateBuildDescriptor());
            Register(CreateReferenceDescriptor());
            Register(CreateSearchDescriptor());
            Register(CreateRuntimeDescriptor());
            Register(CreateSettingsDescriptor());
        }

        private static CortexShellModuleDescriptor CreateLogsDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.LogsContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellNavigationCapability),
                    typeof(ICortexShellSourceCapability),
                    typeof(ICortexShellRuntimeLogCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<LogsModule>(CortexWorkbenchIds.LogsContainer, delegate { return new LogsModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<LogsModule>(CortexWorkbenchIds.LogsContainer, delegate { return new LogsModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var navigation = context.Capabilities.Get<ICortexShellNavigationCapability>();
                    var source = context.Capabilities.Get<ICortexShellSourceCapability>();
                    var runtimeLogs = context.Capabilities.Get<ICortexShellRuntimeLogCapability>();
                    if (module != null && state != null && navigation != null && source != null && runtimeLogs != null)
                    {
                        module.Draw(runtimeLogs.RuntimeLogFeed, source.SourcePathResolver, navigation.NavigationService, state.State, detachedWindow);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateProjectsDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.ProjectsContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellProjectCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<ProjectsModule>(CortexWorkbenchIds.ProjectsContainer, delegate { return new ProjectsModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<ProjectsModule>(CortexWorkbenchIds.ProjectsContainer, delegate { return new ProjectsModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var project = context.Capabilities.Get<ICortexShellProjectCapability>();
                    if (module != null && state != null && project != null)
                    {
                        module.Draw(project.ProjectCatalog, project.ProjectWorkspaceService, project.LoadedModCatalog, state.State);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateFileExplorerDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.FileExplorerContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellNavigationCapability),
                    typeof(ICortexShellWorkspaceBrowserCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<FileExplorerModule>(CortexWorkbenchIds.FileExplorerContainer, delegate { return new FileExplorerModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<FileExplorerModule>(CortexWorkbenchIds.FileExplorerContainer, delegate { return new FileExplorerModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var navigation = context.Capabilities.Get<ICortexShellNavigationCapability>();
                    var workspace = context.Capabilities.Get<ICortexShellWorkspaceBrowserCapability>();
                    if (module != null && state != null && navigation != null && workspace != null)
                    {
                        module.Draw(workspace.WorkspaceBrowserService, workspace.DecompilerExplorerService, navigation.NavigationService, state.State);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateEditorDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.EditorContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellNavigationCapability),
                    typeof(ICortexShellDocumentCapability),
                    typeof(ICortexShellWorkbenchCapability),
                    typeof(ICortexShellSearchCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<EditorModule>(CortexWorkbenchIds.EditorContainer, delegate { return new EditorModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<EditorModule>(CortexWorkbenchIds.EditorContainer, delegate { return new EditorModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var navigation = context.Capabilities.Get<ICortexShellNavigationCapability>();
                    var document = context.Capabilities.Get<ICortexShellDocumentCapability>();
                    var workbench = context.Capabilities.Get<ICortexShellWorkbenchCapability>();
                    var search = context.Capabilities.Get<ICortexShellSearchCapability>();
                    if (module != null && state != null && navigation != null && document != null && workbench != null && search != null)
                    {
                        module.Draw(
                            document.DocumentService,
                            navigation.NavigationService,
                            workbench.WorkbenchRuntime != null ? workbench.WorkbenchRuntime.CommandRegistry : null,
                            workbench.WorkbenchRuntime != null ? workbench.WorkbenchRuntime.ContributionRegistry : null,
                            search.WorkbenchSearchService,
                            state.State);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateBuildDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.BuildContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellNavigationCapability),
                    typeof(ICortexShellSourceCapability),
                    typeof(ICortexShellBuildCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<BuildModule>(CortexWorkbenchIds.BuildContainer, delegate { return new BuildModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<BuildModule>(CortexWorkbenchIds.BuildContainer, delegate { return new BuildModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var navigation = context.Capabilities.Get<ICortexShellNavigationCapability>();
                    var source = context.Capabilities.Get<ICortexShellSourceCapability>();
                    var build = context.Capabilities.Get<ICortexShellBuildCapability>();
                    if (module != null && state != null && navigation != null && source != null && build != null)
                    {
                        module.Draw(build.BuildCommandResolver, build.BuildExecutor, build.RestartCoordinator, source.SourcePathResolver, navigation.NavigationService, state.State);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateReferenceDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.ReferenceContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellNavigationCapability),
                    typeof(ICortexShellReferenceCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<ReferenceModule>(CortexWorkbenchIds.ReferenceContainer, delegate { return new ReferenceModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<ReferenceModule>(CortexWorkbenchIds.ReferenceContainer, delegate { return new ReferenceModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var navigation = context.Capabilities.Get<ICortexShellNavigationCapability>();
                    var reference = context.Capabilities.Get<ICortexShellReferenceCapability>();
                    if (module != null && state != null && navigation != null && reference != null)
                    {
                        module.Draw(reference.ReferenceCatalogService, navigation.NavigationService, state.State);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateSearchDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.SearchContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellNavigationCapability),
                    typeof(ICortexShellSearchCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<SearchModule>(CortexWorkbenchIds.SearchContainer, delegate { return new SearchModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<SearchModule>(CortexWorkbenchIds.SearchContainer, delegate { return new SearchModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var navigation = context.Capabilities.Get<ICortexShellNavigationCapability>();
                    var search = context.Capabilities.Get<ICortexShellSearchCapability>();
                    if (module != null && state != null && navigation != null && search != null)
                    {
                        module.Draw(search.WorkbenchSearchService, navigation.NavigationService, state.State);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateRuntimeDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.RuntimeContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellRuntimeToolCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<RuntimeToolsModule>(CortexWorkbenchIds.RuntimeContainer, delegate { return new RuntimeToolsModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<RuntimeToolsModule>(CortexWorkbenchIds.RuntimeContainer, delegate { return new RuntimeToolsModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var runtimeTools = context.Capabilities.Get<ICortexShellRuntimeToolCapability>();
                    if (module != null && state != null && runtimeTools != null)
                    {
                        module.Draw(runtimeTools.RuntimeToolBridge, state.State);
                    }
                });
        }

        private static CortexShellModuleDescriptor CreateSettingsDescriptor()
        {
            return new CortexShellModuleDescriptor(
                CortexWorkbenchIds.SettingsContainer,
                new Type[]
                {
                    typeof(ICortexShellStateCapability),
                    typeof(ICortexShellSettingsCapability),
                    typeof(ICortexShellProjectCapability),
                    typeof(ICortexShellWorkbenchCapability)
                },
                delegate(CortexShellModuleCompositionService composition)
                {
                    composition.GetOrCreate<SettingsModule>(CortexWorkbenchIds.SettingsContainer, delegate { return new SettingsModule(); });
                },
                delegate(CortexShellModuleCompositionService composition, CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    var module = composition.GetOrCreate<SettingsModule>(CortexWorkbenchIds.SettingsContainer, delegate { return new SettingsModule(); });
                    var state = context.Capabilities.Get<ICortexShellStateCapability>();
                    var settings = context.Capabilities.Get<ICortexShellSettingsCapability>();
                    var project = context.Capabilities.Get<ICortexShellProjectCapability>();
                    var workbench = context.Capabilities.Get<ICortexShellWorkbenchCapability>();
                    if (module != null && state != null && settings != null && project != null && workbench != null)
                    {
                        module.Draw(
                            settings.SettingsStore,
                            project.ProjectCatalog,
                            project.ProjectWorkspaceService,
                            project.LoadedModCatalog,
                            snapshot,
                            workbench.WorkbenchRuntime != null ? workbench.WorkbenchRuntime.ThemeState : null,
                            state.State);
                    }
                });
        }

        private void Register(CortexShellModuleDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrEmpty(descriptor.ContainerId))
            {
                return;
            }

            _descriptors[descriptor.ContainerId] = descriptor;
        }
    }
}
