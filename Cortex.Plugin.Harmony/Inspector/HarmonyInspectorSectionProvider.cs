using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyInspectorSectionProvider
    {
        private readonly HarmonyWorkflowController _workflowController;

        public HarmonyInspectorSectionProvider(HarmonyWorkflowController workflowController)
        {
            _workflowController = workflowController ?? new HarmonyWorkflowController(new HarmonyModuleStateStore());
        }

        public Cortex.Presentation.Models.MethodInspectorSectionViewModel BuildSection(WorkbenchMethodInspectorContext context)
        {
            return _workflowController.BuildInspectorSection(context);
        }

        public WorkbenchMethodInspectorActionResult HandleAction(WorkbenchMethodInspectorActionContext context)
        {
            return _workflowController.HandleInspectorAction(context);
        }
    }
}
