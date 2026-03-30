using System.Collections.Generic;
using Cortex.Core.Models;

namespace Cortex.Services.Inspector.Relationships
{
    internal sealed class EditorMethodRelationshipsContextService
    {
        private readonly IEditorMethodCallHierarchyMapper _callHierarchyMapper;
        private readonly IEditorMethodRelationshipAugmentationService _augmentationService;

        public EditorMethodRelationshipsContextService()
            : this(
                new EditorMethodCallHierarchyMapper(),
                new EditorMethodRelationshipAugmentationService(new Identity.EditorMethodIdentityResolver()))
        {
        }

        internal EditorMethodRelationshipsContextService(
            IEditorMethodCallHierarchyMapper callHierarchyMapper,
            IEditorMethodRelationshipAugmentationService augmentationService)
        {
            _callHierarchyMapper = callHierarchyMapper;
            _augmentationService = augmentationService;
        }

        public EditorMethodRelationshipsContext BuildContext(CortexMethodInspectorState inspector, EditorCommandTarget target)
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

            context.IncomingCallHierarchy = response.IncomingCalls ?? new Cortex.LanguageService.Protocol.LanguageServiceCallHierarchyItem[0];
            context.OutgoingCallHierarchy = response.OutgoingCalls ?? new Cortex.LanguageService.Protocol.LanguageServiceCallHierarchyItem[0];

            var outgoing = new List<EditorMethodRelationshipItem>();
            var incoming = new List<EditorMethodRelationshipItem>();
            _callHierarchyMapper.AppendRelationships(outgoing, response.OutgoingCalls);
            _augmentationService.AppendOutgoingRelationships(outgoing, target);
            _callHierarchyMapper.AppendRelationships(incoming, response.IncomingCalls);
            _augmentationService.AppendIncomingRelationships(incoming, target);

            context.IncomingCalls = incoming.ToArray();
            context.OutgoingCalls = outgoing.ToArray();
            context.IncomingCallCount = context.IncomingCalls.Length;
            context.OutgoingCallCount = context.OutgoingCalls.Length;
            context.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                ? response.StatusMessage
                : "Method relationships resolved.";
            return context;
        }
    }
}
