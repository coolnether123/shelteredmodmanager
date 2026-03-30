using Cortex.LanguageService.Protocol;
using Cortex.Presentation.Models;

namespace Cortex.Services.Inspector.Relationships
{
    internal sealed class EditorMethodRelationshipItem
    {
        public string Title = string.Empty;
        public string Detail = string.Empty;
        public string SymbolKind = string.Empty;
        public string MetadataName = string.Empty;
        public string ContainingTypeName = string.Empty;
        public string ContainingAssemblyName = string.Empty;
        public string DocumentationCommentId = string.Empty;
        public string DefinitionDocumentPath = string.Empty;
        public LanguageServiceRange DefinitionRange;
        public string Relationship = string.Empty;
        public int CallCount = 1;
        public MethodInspectorActionViewModel[] Actions = new MethodInspectorActionViewModel[0];
    }

    internal sealed class EditorMethodRelationshipsContext
    {
        public bool IsExpanded;
        public bool IsLoading;
        public bool HasResponse;
        public string StatusMessage = string.Empty;
        public LanguageServiceCallHierarchyItem[] IncomingCallHierarchy = new LanguageServiceCallHierarchyItem[0];
        public LanguageServiceCallHierarchyItem[] OutgoingCallHierarchy = new LanguageServiceCallHierarchyItem[0];
        public EditorMethodRelationshipItem[] IncomingCalls = new EditorMethodRelationshipItem[0];
        public EditorMethodRelationshipItem[] OutgoingCalls = new EditorMethodRelationshipItem[0];
        public int IncomingCallCount;
        public int OutgoingCallCount;
    }
}
