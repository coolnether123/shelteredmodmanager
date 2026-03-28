using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class EditorMethodRelationshipsContext
    {
        public bool IsExpanded;
        public bool IsLoading;
        public bool HasResponse;
        public string StatusMessage = string.Empty;
        public LanguageServiceCallHierarchyItem[] IncomingCalls = new LanguageServiceCallHierarchyItem[0];
        public LanguageServiceCallHierarchyItem[] OutgoingCalls = new LanguageServiceCallHierarchyItem[0];
        public int IncomingCallCount;
        public int OutgoingCallCount;
    }

    internal sealed class EditorMethodRelationshipsContextService
    {
        public EditorMethodRelationshipsContext BuildContext(CortexMethodInspectorState inspector)
        {
            var context = new EditorMethodRelationshipsContext();
            if (inspector == null)
            {
                context.StatusMessage = "Method relationships are not available.";
                return context;
            }

            context.IsExpanded = inspector.RelationshipsExpanded;
            if (!inspector.RelationshipsExpanded)
            {
                context.StatusMessage = "Expand this section to analyze method relationships.";
                return context;
            }

            if (!inspector.RelationshipsRequested || !string.IsNullOrEmpty(inspector.RelationshipsRequestKey))
            {
                context.IsLoading = true;
                context.StatusMessage = !string.IsNullOrEmpty(inspector.RelationshipsStatusMessage)
                    ? inspector.RelationshipsStatusMessage
                    : "Analyzing method relationships.";
                return context;
            }

            var response = inspector.RelationshipsCallHierarchy;
            if (response == null)
            {
                context.StatusMessage = !string.IsNullOrEmpty(inspector.RelationshipsStatusMessage)
                    ? inspector.RelationshipsStatusMessage
                    : "Method relationship analysis has not produced any results yet.";
                return context;
            }

            context.HasResponse = true;
            if (!response.Success)
            {
                context.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                    ? response.StatusMessage
                    : "Method relationship analysis failed for the selected method.";
                return context;
            }

            context.IncomingCalls = response.IncomingCalls ?? new LanguageServiceCallHierarchyItem[0];
            context.OutgoingCalls = response.OutgoingCalls ?? new LanguageServiceCallHierarchyItem[0];
            context.IncomingCallCount = context.IncomingCalls.Length;
            context.OutgoingCallCount = context.OutgoingCalls.Length;
            context.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                ? response.StatusMessage
                : "Method relationships resolved.";
            return context;
        }
    }
}
