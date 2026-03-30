using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyPluginComposition
    {
        private readonly HarmonyModuleStateStore _stateStore;
        private readonly HarmonyWorkflowController _workflowController;
        private readonly HarmonyCommandRegistrar _commandRegistrar;
        private readonly HarmonyEditorContributionRegistrar _editorRegistrar;
        private readonly HarmonyExplorerFilterRegistrar _explorerRegistrar;

        public HarmonyPluginComposition()
        {
            _stateStore = new HarmonyModuleStateStore();
            _workflowController = new HarmonyWorkflowController(_stateStore);
            _commandRegistrar = new HarmonyCommandRegistrar(_stateStore, _workflowController);
            _editorRegistrar = new HarmonyEditorContributionRegistrar(_stateStore, _workflowController);
            _explorerRegistrar = new HarmonyExplorerFilterRegistrar(_workflowController);
        }

        public void Register(WorkbenchPluginContext context)
        {
            context.RegisterViewContainer(
                HarmonyPluginIds.ContainerId,
                "Harmony",
                WorkbenchHostLocation.SecondarySideHost,
                40,
                true,
                ModuleActivationKind.OnCommand,
                HarmonyPluginIds.OpenWindowCommandId,
                HarmonyPluginIds.ContainerId);
            context.RegisterView(
                HarmonyPluginIds.ViewId,
                HarmonyPluginIds.ContainerId,
                "Harmony",
                HarmonyPluginIds.ViewId,
                0,
                true);
            context.RegisterIcon(new IconContribution
            {
                IconId = HarmonyPluginIds.ContainerId,
                Alias = "HM"
            });
            context.RegisterModule(new HarmonyModuleContribution(_stateStore, _workflowController));

            _commandRegistrar.Register(context);
            _editorRegistrar.Register(context);
            _explorerRegistrar.Register(context);
        }
    }
}
