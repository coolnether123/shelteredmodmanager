using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyExplorerFilterRegistrar
    {
        private readonly HarmonyWorkflowController _workflowController;

        public HarmonyExplorerFilterRegistrar(HarmonyWorkflowController workflowController)
        {
            _workflowController = workflowController ?? new HarmonyWorkflowController(new HarmonyModuleStateStore());
        }

        public void Register(WorkbenchPluginContext context)
        {
            if (context == null || context.ContributionRegistry == null)
            {
                return;
            }

            context.ContributionRegistry.RegisterExplorerFilter(new ExplorerFilterContribution
            {
                FilterId = HarmonyPluginIds.ExplorerFilterId,
                DisplayName = "Harmony Patched",
                Description = "Show only decompiler nodes that currently have live Harmony patches.",
                Scope = ExplorerFilterScope.Decompiler,
                SortOrder = 100,
                CreateMatcher = delegate(ExplorerFilterRuntimeContext runtimeContext)
                {
                    var runtime = HarmonyModuleRuntimeLocator.Get(context.Runtime);
                    return _workflowController.IsRuntimeAvailable
                        ? _workflowController.BuildExplorerMatcher(runtime, runtimeContext)
                        : null;
                }
            });
        }
    }
}
