using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Abstractions
{
    public interface ILanguageRuntimeControl
    {
        void Start(LanguageRuntimeConfiguration configuration);
        void Reload(LanguageRuntimeConfiguration configuration);
        void Advance();
        void Shutdown();
    }

    public interface ILanguageRuntimeQuery
    {
        LanguageRuntimeSnapshot GetSnapshot();
    }

    public interface ILanguageEditorOperations
    {
        void DispatchDocumentAnalysis();
        void DispatchHover();
        void DispatchDefinition();
        void DispatchCompletion();
        void DispatchSignatureHelp();
        void DispatchSemanticOperations();
        void DispatchMethodInspectorCallHierarchy();
    }

    public interface ILanguageProviderFactory
    {
        LanguageProviderDescriptor Descriptor { get; }
        string BuildConfigurationFingerprint(LanguageRuntimeConfiguration configuration);
        bool TryCreate(LanguageRuntimeConfiguration configuration, out ILanguageProviderSession session, out string unavailableReason);
    }

    public interface ILanguageProviderSession : System.IDisposable
    {
        LanguageProviderDescriptor Descriptor { get; }
        string ConfigurationFingerprint { get; }
        string LastError { get; }
        bool IsRunning { get; }

        void Start(LanguageServiceInitializeRequest request);
        void Advance();
        bool TryCancelRequest(string requestId);
        bool TryDequeueMessage(out LanguageRuntimeMessage message);

        string QueueStatus();
        string QueueAnalyzeDocument(LanguageServiceDocumentRequest request);
        string QueueHover(LanguageServiceHoverRequest request);
        string QueueGoToDefinition(LanguageServiceDefinitionRequest request);
        string QueueCompletion(LanguageServiceCompletionRequest request);
        string QueueSignatureHelp(LanguageServiceSignatureHelpRequest request);
        string QueueSymbolContext(LanguageServiceSymbolContextRequest request);
        string QueueRenamePreview(LanguageServiceRenameRequest request);
        string QueueReferences(LanguageServiceReferencesRequest request);
        string QueueGoToBase(LanguageServiceBaseSymbolRequest request);
        string QueueGoToImplementation(LanguageServiceImplementationRequest request);
        string QueueCallHierarchy(LanguageServiceCallHierarchyRequest request);
        string QueueValueSource(LanguageServiceValueSourceRequest request);
        string QueueDocumentTransformPreview(LanguageServiceDocumentTransformRequest request);

        void Shutdown();
    }
}
