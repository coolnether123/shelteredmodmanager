using System;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyEditorContributionRegistrar
    {
        private readonly HarmonyWorkflowController _workflowController;
        private readonly HarmonyInspectorSectionProvider _inspectorProvider;

        public HarmonyEditorContributionRegistrar(HarmonyModuleStateStore stateStore, HarmonyWorkflowController workflowController)
        {
            _workflowController = workflowController ?? new HarmonyWorkflowController(stateStore ?? new HarmonyModuleStateStore());
            _inspectorProvider = new HarmonyInspectorSectionProvider(_workflowController);
        }

        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            context.RegisterEditorAdornment(new WorkbenchEditorAdornmentContribution
            {
                ContributionId = HarmonyPluginIds.EditorAdornmentId,
                SortOrder = 100,
                BuildAdornments = _workflowController.BuildAdornments
            });
            context.RegisterEditorWorkflow(new WorkbenchEditorWorkflowContribution
            {
                ContributionId = HarmonyPluginIds.TemplateWorkflowId,
                SortOrder = 150,
                Synchronize = _workflowController.SynchronizeTemplateSession,
                TryHandleKeyboard = _workflowController.HandleTemplateKeyboard
            });
            context.RegisterEditorWorkflow(new WorkbenchEditorWorkflowContribution
            {
                ContributionId = HarmonyPluginIds.EditorWorkflowId,
                SortOrder = 200,
                IsActive = delegate(WorkbenchEditorWorkflowContext contributionContext)
                {
                    var runtime = contributionContext != null ? HarmonyModuleRuntimeLocator.Get(contributionContext.Runtime) : null;
                    return _workflowController.IsInsertionSelectionActive(runtime);
                },
                Synchronize = _workflowController.SynchronizeInsertionSelection,
                TryHandlePointer = _workflowController.HandleInsertionPointer,
                TryHandleKeyboard = _workflowController.HandleInsertionKeyboard
            });
            context.RegisterMethodInspectorSection(new WorkbenchMethodInspectorSectionContribution
            {
                ContributionId = HarmonyPluginIds.InspectorSectionId,
                SortOrder = 300,
                DefaultExpanded = true,
                CanDisplay = delegate(WorkbenchMethodInspectorContext contributionContext)
                {
                    return _workflowController.IsRuntimeAvailable;
                },
                BuildSection = delegate(WorkbenchMethodInspectorContext contributionContext)
                {
                    return _inspectorProvider.BuildSection(contributionContext);
                },
                TryHandleAction = delegate(WorkbenchMethodInspectorActionContext contributionContext)
                {
                    return _inspectorProvider.HandleAction(contributionContext);
                }
            });
            context.RegisterMethodRelationshipAugmentation(new WorkbenchMethodRelationshipAugmentationContribution
            {
                ContributionId = "cortex.harmony.relationship-augmentations",
                SortOrder = 90,
                BuildIncomingRelationships = _workflowController.BuildIncomingRelationshipAugmentations,
                BuildOutgoingRelationships = _workflowController.BuildOutgoingRelationshipAugmentations
            });
            context.RegisterMethodRelationshipAction(new WorkbenchMethodRelationshipActionContribution
            {
                ContributionId = "cortex.harmony.relationship-actions",
                SortOrder = 100,
                BuildActions = _workflowController.BuildRelationshipActions
            });
        }
    }
}
