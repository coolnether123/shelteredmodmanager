namespace Cortex
{
    internal sealed class CortexShellLanguageCoordinator
    {
        public void UpdateLanguageService(
            CortexShellLanguageRuntimeContext context,
            CortexShellLanguageRequestDispatcher requestDispatcher,
            CortexShellLanguageResponseProcessor responseProcessor)
        {
            if (context == null || requestDispatcher == null || responseProcessor == null)
            {
                return;
            }

            context.EnsureLanguageServiceStarted();
            responseProcessor.ProcessLanguageResponses(context);
            context.ProcessCompletionAugmentationResponses();
            context.LogLanguageInitializationProgress();
            requestDispatcher.UpdateLanguageCompletion(context);
            requestDispatcher.UpdateLanguageSignatureHelp(context);

            if (!context.IsLanguageServiceReadyForDocumentWork)
            {
                context.ResetLanguageTrackingForInactiveDocument();
                return;
            }

            requestDispatcher.UpdateLanguageHover(context);
            requestDispatcher.UpdateLanguageDefinition(context);
            requestDispatcher.UpdateMethodInspectorCallHierarchy(context);
            requestDispatcher.UpdateSemanticOperation(context);
            requestDispatcher.ProcessDocumentLanguageAnalysis(context);
        }
    }
}
