using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Unity.Composition
{
    internal sealed class DefaultWorkbenchViewContributions
    {
        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            context.RegisterViewContainer(CortexWorkbenchIds.FileExplorerContainer, "Solution Explorer", WorkbenchHostLocation.SecondarySideHost, 0, true, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.FileExplorerContainer, CortexWorkbenchIds.FileExplorerContainer);
            context.RegisterViewContainer(CortexWorkbenchIds.LogsContainer, "Logs", WorkbenchHostLocation.PanelHost, 0, true, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.LogsContainer, CortexWorkbenchIds.LogsContainer);
            context.RegisterViewContainer(CortexWorkbenchIds.BuildContainer, "Build", WorkbenchHostLocation.PanelHost, 10, true, ModuleActivationKind.OnCommand, "cortex.build.execute", CortexWorkbenchIds.BuildContainer);
            context.RegisterViewContainer(CortexWorkbenchIds.RuntimeContainer, "Runtime", WorkbenchHostLocation.PanelHost, 20, true, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.RuntimeContainer, CortexWorkbenchIds.RuntimeContainer);
            context.RegisterViewContainer(CortexWorkbenchIds.ProjectsContainer, "Projects", WorkbenchHostLocation.SecondarySideHost, 10, true, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.ProjectsContainer, CortexWorkbenchIds.ProjectsContainer);
            context.RegisterViewContainer(CortexWorkbenchIds.ReferenceContainer, "References", WorkbenchHostLocation.SecondarySideHost, 20, true, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.ReferenceContainer, CortexWorkbenchIds.ReferenceContainer);
            context.RegisterViewContainer(CortexWorkbenchIds.SearchContainer, "Search", WorkbenchHostLocation.PanelHost, 30, true, ModuleActivationKind.OnCommand, "cortex.editor.find", CortexWorkbenchIds.SearchContainer);
            context.RegisterViewContainer(CortexWorkbenchIds.EditorContainer, "Editor", WorkbenchHostLocation.DocumentHost, 0, true, ModuleActivationKind.OnDocumentRestore, CortexWorkbenchIds.EditorContainer, CortexWorkbenchIds.EditorContainer);

            RegisterDefaultView(context, CortexWorkbenchIds.FileExplorerContainer, "Solution Explorer");
            RegisterDefaultView(context, CortexWorkbenchIds.LogsContainer, "Logs");
            RegisterDefaultView(context, CortexWorkbenchIds.BuildContainer, "Build");
            RegisterDefaultView(context, CortexWorkbenchIds.RuntimeContainer, "Runtime");
            RegisterDefaultView(context, CortexWorkbenchIds.ProjectsContainer, "Projects");
            RegisterDefaultView(context, CortexWorkbenchIds.ReferenceContainer, "References");
            RegisterDefaultView(context, CortexWorkbenchIds.SearchContainer, "Search");
            RegisterDefaultView(context, CortexWorkbenchIds.EditorContainer, "Editor");

            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.FileExplorerContainer, Alias = "EX" });
            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.LogsContainer, Alias = "LG" });
            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.ProjectsContainer, Alias = "PJ" });
            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.ReferenceContainer, Alias = "RF" });
            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.SearchContainer, Alias = "SR" });
            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.EditorContainer, Alias = "ED" });
            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.BuildContainer, Alias = "BL" });
            context.RegisterIcon(new IconContribution { IconId = CortexWorkbenchIds.RuntimeContainer, Alias = "RT" });

            context.RegisterEditor("cortex.editor.code", "Code Editor", ".cs", "text/x-csharp", 0);
            context.RegisterEditor("cortex.editor.text", "Text Editor", ".txt", "text/plain", 10);
            context.RegisterEditor("cortex.editor.log", "Log Viewer", ".log", "text/plain", 20);
        }

        private static void RegisterDefaultView(WorkbenchPluginContext context, string containerId, string title)
        {
            context.RegisterView(containerId + ".main", containerId, title, containerId + ".main", 0, true);
        }
    }
}
