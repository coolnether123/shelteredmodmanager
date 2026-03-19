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

            if (!context.IsLanguageServiceReadyForDocumentWork)
            {
                context.ResetLanguageTrackingForInactiveDocument();
                return;
            }

            requestDispatcher.UpdateLanguageHover(context);
            requestDispatcher.UpdateLanguageDefinition(context);
            requestDispatcher.ProcessDocumentLanguageAnalysis(context);
        }
    }
}
