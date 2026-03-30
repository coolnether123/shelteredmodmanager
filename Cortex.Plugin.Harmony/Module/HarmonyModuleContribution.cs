using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyModuleContribution : IWorkbenchModuleContribution
    {
        private readonly HarmonyModuleStateStore _stateStore;
        private readonly HarmonyWorkflowController _workflowController;

        public HarmonyModuleContribution(HarmonyModuleStateStore stateStore, HarmonyWorkflowController workflowController)
        {
            _stateStore = stateStore ?? new HarmonyModuleStateStore();
            _workflowController = workflowController ?? new HarmonyWorkflowController(_stateStore);
            Descriptor = new WorkbenchModuleDescriptor(HarmonyPluginIds.ModuleId, HarmonyPluginIds.ContainerId, typeof(HarmonyModule));
        }

        public WorkbenchModuleDescriptor Descriptor { get; private set; }

        public IWorkbenchModule CreateModule(IWorkbenchModuleRuntime runtime)
        {
            return new HarmonyModule(_stateStore, _workflowController);
        }
    }
}
