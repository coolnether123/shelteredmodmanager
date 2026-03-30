using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Requests
{
    internal interface IEditorLanguageRequestFactory
    {
        LanguageServiceSymbolContextRequest BuildSymbolContextRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        LanguageServiceHoverRequest BuildHoverRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        LanguageServiceDefinitionRequest BuildDefinitionRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        LanguageServiceRenameRequest BuildRenameRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition, string newName);
        LanguageServiceReferencesRequest BuildReferencesRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        LanguageServiceBaseSymbolRequest BuildBaseSymbolRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        LanguageServiceImplementationRequest BuildImplementationRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        LanguageServiceCallHierarchyRequest BuildCallHierarchyRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        LanguageServiceValueSourceRequest BuildValueSourceRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition);
        string BuildCompletionRequestKey(string documentPath, int documentVersion, int absolutePosition, bool explicitInvocation, string triggerCharacter);
        LanguageServiceSignatureHelpRequest BuildSignatureHelpRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition, bool explicitInvocation, string triggerCharacter);
        LanguageServiceDocumentTransformRequest BuildDocumentTransformRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, string commandId, string title, string applyLabel, bool organizeImports, bool simplifyNames, bool formatDocument);
    }
}
