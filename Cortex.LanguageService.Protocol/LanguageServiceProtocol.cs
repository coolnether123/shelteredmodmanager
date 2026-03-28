namespace Cortex.LanguageService.Protocol
{
    /// <summary>
    /// Well-known command names understood by the external Roslyn worker.
    /// </summary>
    public static class LanguageServiceCommands
    {
        /// <summary>
        /// Initializes the worker with workspace and project context.
        /// </summary>
        public const string Initialize = "initialize";

        /// <summary>
        /// Requests the current runtime and cache status of the worker.
        /// </summary>
        public const string Status = "status";

        /// <summary>
        /// Requests cancellation for an in-flight worker operation.
        /// </summary>
        public const string CancelRequest = "cancel-request";

        /// <summary>
        /// Requests diagnostics and semantic classifications for a document.
        /// </summary>
        public const string AnalyzeDocument = "analyze-document";

        /// <summary>
        /// Requests hover information for a symbol at a given line and column.
        /// </summary>
        public const string Hover = "hover";

        /// <summary>
        /// Requests the source definition location for a symbol at a given line and column.
        /// </summary>
        public const string GoToDefinition = "go-to-definition";

        /// <summary>
        /// Requests completion items for a symbol position.
        /// </summary>
        public const string Completion = "completion";

        /// <summary>
        /// Requests active signature help for an invocation position.
        /// </summary>
        public const string SignatureHelp = "signature-help";

        /// <summary>
        /// Requests normalized semantic symbol context for the current cursor position.
        /// </summary>
        public const string SymbolContext = "symbol-context";

        /// <summary>
        /// Requests a semantic rename preview for the current symbol.
        /// </summary>
        public const string RenamePreview = "rename-preview";

        /// <summary>
        /// Requests semantic references for the current symbol.
        /// </summary>
        public const string FindReferences = "find-references";

        /// <summary>
        /// Requests distinct base-symbol navigation targets.
        /// </summary>
        public const string GoToBase = "go-to-base";

        /// <summary>
        /// Requests distinct implementation navigation targets.
        /// </summary>
        public const string GoToImplementation = "go-to-implementation";

        /// <summary>
        /// Requests semantic call hierarchy for the current symbol.
        /// </summary>
        public const string CallHierarchy = "call-hierarchy";

        /// <summary>
        /// Requests semantic value-source tracking for the current symbol.
        /// </summary>
        public const string ValueSource = "value-source";

        /// <summary>
        /// Requests a previewable document cleanup transform.
        /// </summary>
        public const string DocumentTransformPreview = "document-transform-preview";

        /// <summary>
        /// Requests an orderly worker shutdown.
        /// </summary>
        public const string Shutdown = "shutdown";
    }

    /// <summary>
    /// Envelope used for every request and response sent over the worker transport.
    /// </summary>
    public sealed class LanguageServiceEnvelope
    {
        /// <summary>
        /// Correlates a response back to the originating request.
        /// </summary>
        public string RequestId;

        /// <summary>
        /// Identifies the worker command being invoked.
        /// </summary>
        public string Command;

        /// <summary>
        /// Indicates whether the operation completed successfully.
        /// </summary>
        public bool Success;

        /// <summary>
        /// Contains the serialized payload for the operation.
        /// </summary>
        public string PayloadJson;

        /// <summary>
        /// Contains failure details when <see cref="Success"/> is false.
        /// </summary>
        public string ErrorMessage;
    }

    /// <summary>
    /// Base response for worker operations.
    /// </summary>
    public class LanguageServiceOperationResponse
    {
        /// <summary>
        /// Indicates whether the operation completed successfully.
        /// </summary>
        public bool Success;

        /// <summary>
        /// Human-readable operation status.
        /// </summary>
        public string StatusMessage;
    }

    /// <summary>
    /// Initialization payload sent to the Roslyn worker.
    /// </summary>
    public sealed class LanguageServiceInitializeRequest
    {
        /// <summary>
        /// Preferred root of the active workspace.
        /// </summary>
        public string WorkspaceRootPath;

        /// <summary>
        /// Source roots that should be searched for documents and projects.
        /// </summary>
        public string[] SourceRoots;

        /// <summary>
        /// Explicit project files already known by Cortex.
        /// </summary>
        public string[] ProjectFilePaths;

        /// <summary>
        /// Explicit solution files already known by Cortex.
        /// </summary>
        public string[] SolutionFilePaths;

        /// <summary>
        /// Supplemental managed assembly paths that should be visible to Roslyn.
        /// </summary>
        public string[] ReferenceAssemblyPaths;
    }

    /// <summary>
    /// Response returned after the worker initializes.
    /// </summary>
    public sealed class LanguageServiceInitializeResponse : LanguageServiceOperationResponse
    {
        /// <summary>
        /// Friendly worker display name.
        /// </summary>
        public string WorkerName;

        /// <summary>
        /// Worker assembly version.
        /// </summary>
        public string WorkerVersion;

        /// <summary>
        /// Runtime version used by the worker.
        /// </summary>
        public string RuntimeVersion;

        /// <summary>
        /// Declared capabilities exposed by the worker.
        /// </summary>
        public string[] Capabilities;
    }

    /// <summary>
    /// Empty request used to query worker status.
    /// </summary>
    public sealed class LanguageServiceStatusRequest
    {
    }

    /// <summary>
    /// Cancellation payload sent to the worker.
    /// </summary>
    public sealed class LanguageServiceCancelRequest
    {
        public string TargetRequestId;
    }

    /// <summary>
    /// Response returned after a cancellation attempt.
    /// </summary>
    public sealed class LanguageServiceCancelResponse : LanguageServiceOperationResponse
    {
        public string TargetRequestId;
        public bool Cancelled;
    }

    /// <summary>
    /// Snapshot of worker availability and loaded project state.
    /// </summary>
    public sealed class LanguageServiceStatusResponse : LanguageServiceOperationResponse
    {
        /// <summary>
        /// Friendly worker display name.
        /// </summary>
        public string WorkerName;

        /// <summary>
        /// Worker assembly version.
        /// </summary>
        public string WorkerVersion;

        /// <summary>
        /// Runtime version used by the worker.
        /// </summary>
        public string RuntimeVersion;

        /// <summary>
        /// Declared capabilities exposed by the worker.
        /// </summary>
        public string[] Capabilities;

        /// <summary>
        /// Number of cached projects currently loaded in the worker.
        /// </summary>
        public int CachedProjectCount;

        /// <summary>
        /// Project paths currently cached by the worker.
        /// </summary>
        public string[] LoadedProjectPaths;

        /// <summary>
        /// Indicates whether the worker is actively running.
        /// </summary>
        public bool IsRunning;
    }

    /// <summary>
    /// Request payload used to analyze a source document.
    /// </summary>
    public sealed class LanguageServiceDocumentRequest
    {
        /// <summary>
        /// Absolute path of the document being analyzed.
        /// </summary>
        public string DocumentPath;

        /// <summary>
        /// Preferred owning project path when already known.
        /// </summary>
        public string ProjectFilePath;

        /// <summary>
        /// Active workspace root.
        /// </summary>
        public string WorkspaceRootPath;

        /// <summary>
        /// Candidate source roots used for fallback project discovery.
        /// </summary>
        public string[] SourceRoots;

        /// <summary>
        /// Current text snapshot to analyze.
        /// </summary>
        public string DocumentText;

        /// <summary>
        /// Monotonic Cortex-side version for the current text snapshot.
        /// </summary>
        public int DocumentVersion;

        /// <summary>
        /// Indicates whether compiler diagnostics should be returned.
        /// </summary>
        public bool IncludeDiagnostics;

        /// <summary>
        /// Indicates whether classified spans should be returned.
        /// </summary>
        public bool IncludeClassifications;

        /// <summary>
        /// Zero-based start offset for an incremental classification refresh.
        /// Use -1 to classify the full document.
        /// </summary>
        public int ClassificationRangeStart;

        /// <summary>
        /// Length of the incremental classification refresh span.
        /// Ignored when <see cref="ClassificationRangeStart"/> is less than zero.
        /// </summary>
        public int ClassificationRangeLength;
    }

    /// <summary>
    /// Shared request payload used by semantic symbol operations.
    /// </summary>
    public class LanguageServiceSymbolRequest
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public string WorkspaceRootPath;
        public string[] SourceRoots;
        public string DocumentText;
        public int DocumentVersion;
        public int Line;
        public int Column;
        public int AbsolutePosition;
    }

    /// <summary>
    /// Request payload used to resolve hover details for a symbol.
    /// </summary>
    public sealed class LanguageServiceHoverRequest
    {
        /// <summary>
        /// Absolute path of the document being inspected.
        /// </summary>
        public string DocumentPath;

        /// <summary>
        /// Preferred owning project path when already known.
        /// </summary>
        public string ProjectFilePath;

        /// <summary>
        /// Active workspace root.
        /// </summary>
        public string WorkspaceRootPath;

        /// <summary>
        /// Candidate source roots used for fallback project discovery.
        /// </summary>
        public string[] SourceRoots;

        /// <summary>
        /// Current text snapshot to inspect.
        /// </summary>
        public string DocumentText;

        /// <summary>
        /// Monotonic Cortex-side version for the current text snapshot.
        /// </summary>
        public int DocumentVersion;

        /// <summary>
        /// One-based line number for the hover position.
        /// </summary>
        public int Line;

        /// <summary>
        /// One-based column number for the hover position.
        /// </summary>
        public int Column;

        /// <summary>
        /// Zero-based absolute position for the hover token when Cortex already knows the exact source offset.
        /// </summary>
        public int AbsolutePosition;
    }

    /// <summary>
    /// Request payload used to resolve the source definition for a symbol.
    /// </summary>
    public sealed class LanguageServiceDefinitionRequest
    {
        /// <summary>
        /// Absolute path of the document being inspected.
        /// </summary>
        public string DocumentPath;

        /// <summary>
        /// Preferred owning project path when already known.
        /// </summary>
        public string ProjectFilePath;

        /// <summary>
        /// Active workspace root.
        /// </summary>
        public string WorkspaceRootPath;

        /// <summary>
        /// Candidate source roots used for fallback project discovery.
        /// </summary>
        public string[] SourceRoots;

        /// <summary>
        /// Current text snapshot to inspect.
        /// </summary>
        public string DocumentText;

        /// <summary>
        /// Monotonic Cortex-side version for the current text snapshot.
        /// </summary>
        public int DocumentVersion;

        /// <summary>
        /// One-based line number for the lookup position.
        /// </summary>
        public int Line;

        /// <summary>
        /// One-based column number for the lookup position.
        /// </summary>
        public int Column;

        /// <summary>
        /// Zero-based absolute position for the definition token when Cortex already knows the exact source offset.
        /// </summary>
        public int AbsolutePosition;
    }

    /// <summary>
    /// Request payload used to resolve completion suggestions at a given position.
    /// </summary>
    public sealed class LanguageServiceCompletionRequest
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public string WorkspaceRootPath;
        public string[] SourceRoots;
        public string DocumentText;
        public int DocumentVersion;
        public int Line;
        public int Column;
        public int AbsolutePosition;
        public bool ExplicitInvocation;
        public string TriggerCharacter;
    }

    /// <summary>
    /// Request payload used to resolve active signature help.
    /// </summary>
    public sealed class LanguageServiceSignatureHelpRequest
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public string WorkspaceRootPath;
        public string[] SourceRoots;
        public string DocumentText;
        public int DocumentVersion;
        public int Line;
        public int Column;
        public int AbsolutePosition;
        public bool ExplicitInvocation;
        public string TriggerCharacter;
    }

    /// <summary>
    /// Request payload used to resolve normalized symbol identity details.
    /// </summary>
    public sealed class LanguageServiceSymbolContextRequest : LanguageServiceSymbolRequest
    {
    }

    /// <summary>
    /// Request payload used to preview a semantic rename.
    /// </summary>
    public sealed class LanguageServiceRenameRequest : LanguageServiceSymbolRequest
    {
        public string NewName;
    }

    /// <summary>
    /// Request payload used to resolve semantic references.
    /// </summary>
    public sealed class LanguageServiceReferencesRequest : LanguageServiceSymbolRequest
    {
    }

    /// <summary>
    /// Request payload used to resolve semantic base-symbol navigation targets.
    /// </summary>
    public sealed class LanguageServiceBaseSymbolRequest : LanguageServiceSymbolRequest
    {
    }

    /// <summary>
    /// Request payload used to resolve semantic implementation targets.
    /// </summary>
    public sealed class LanguageServiceImplementationRequest : LanguageServiceSymbolRequest
    {
    }

    /// <summary>
    /// Request payload used to resolve call hierarchy details.
    /// </summary>
    public sealed class LanguageServiceCallHierarchyRequest : LanguageServiceSymbolRequest
    {
    }

    /// <summary>
    /// Request payload used to resolve semantic value-source details.
    /// </summary>
    public sealed class LanguageServiceValueSourceRequest : LanguageServiceSymbolRequest
    {
    }

    /// <summary>
    /// Request payload used to preview a Roslyn-backed document transform.
    /// </summary>
    public sealed class LanguageServiceDocumentTransformRequest
    {
        public string CommandId;
        public string Title;
        public string ApplyLabel;
        public string DocumentPath;
        public string ProjectFilePath;
        public string WorkspaceRootPath;
        public string[] SourceRoots;
        public string DocumentText;
        public int DocumentVersion;
        public bool OrganizeImports;
        public bool SimplifyNames;
        public bool FormatDocument;
    }

    /// <summary>
    /// Represents a text range in both line/column and absolute offset form.
    /// </summary>
    public sealed class LanguageServiceRange
    {
        public int StartLine;
        public int StartColumn;
        public int EndLine;
        public int EndColumn;
        public int Start;
        public int Length;
    }

    /// <summary>
    /// Interactive symbol fragment exposed within a hover signature.
    /// </summary>
    public sealed class LanguageServiceHoverDisplayPart
    {
        public string Text;
        public string Classification;
        public bool IsInteractive;
        public string SymbolDisplay;
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public string DocumentationXml;
        public string DocumentationText;
        public LanguageServiceHoverSection[] SupplementalSections;
        public string DefinitionDocumentPath;
        public LanguageServiceRange DefinitionRange;
    }

    /// <summary>
    /// Structured supplemental hover content emitted separately from canonical documentation text.
    /// </summary>
    public sealed class LanguageServiceHoverSection
    {
        public string Kind;
        public string Title;
        public string Text;
        public LanguageServiceHoverDisplayPart[] DisplayParts;
    }

    /// <summary>
    /// Normalized semantic source location emitted by navigation, references,
    /// call hierarchy, rename preview, and value-source workflows.
    /// </summary>
    public sealed class LanguageServiceSymbolLocation
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public string SymbolDisplay;
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public LanguageServiceRange Range;
        public string LineText;
        public string PreviewText;
        public string Relationship;
        public bool IsPrimary;
        public bool IsDefinition;
        public bool IsWrite;
        public bool IsDeclaration;
    }

    /// <summary>
    /// A single workspace edit produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceTextEdit
    {
        public LanguageServiceRange Range;
        public string OldText;
        public string NewText;
        public string PreviewText;
    }

    /// <summary>
    /// All edits produced for a single document.
    /// </summary>
    public sealed class LanguageServiceDocumentChange
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public string DisplayPath;
        public int ChangeCount;
        public LanguageServiceTextEdit[] Edits;
    }

    /// <summary>
    /// Base symbol metadata response used by semantic workflows.
    /// </summary>
    public class LanguageServiceSymbolResponse : LanguageServiceOperationResponse
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public int DocumentVersion;
        public string SymbolDisplay;
        public string QualifiedSymbolDisplay;
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public string DocumentationXml;
        public string DocumentationText;
        public LanguageServiceRange Range;
        public string DefinitionDocumentPath;
        public LanguageServiceRange DefinitionRange;
    }

    /// <summary>
    /// Base response for workflows that return a set of source locations.
    /// </summary>
    public class LanguageServiceLocationResponse : LanguageServiceSymbolResponse
    {
        public int TotalLocationCount;
        public LanguageServiceSymbolLocation[] Locations;
    }

    /// <summary>
    /// A grouped call relationship entry.
    /// </summary>
    public sealed class LanguageServiceCallHierarchyItem
    {
        public string SymbolDisplay;
        public string QualifiedSymbolDisplay;
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public string Relationship;
        public int CallCount;
        public LanguageServiceSymbolLocation[] Locations;
    }

    /// <summary>
    /// One semantic value-source item.
    /// </summary>
    public sealed class LanguageServiceValueSourceItem
    {
        public string FlowKind;
        public string SymbolDisplay;
        public string Relationship;
        public LanguageServiceSymbolLocation Location;
    }

    /// <summary>
    /// Compiler or analyzer diagnostic produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceDiagnostic
    {
        public string Id;
        public string Severity;
        public string Message;
        public string Category;
        public string FilePath;
        public int Line;
        public int Column;
        public int EndLine;
        public int EndColumn;
    }

    /// <summary>
    /// Semantic or lexical classification span for syntax coloring.
    /// </summary>
    public sealed class LanguageServiceClassifiedSpan
    {
        public string Classification;
        public string SemanticTokenType;
        public int Start;
        public int Length;
        public int Line;
        public int Column;
    }

    /// <summary>
    /// Combined diagnostics and classification results for a document.
    /// </summary>
    public sealed class LanguageServiceAnalysisResponse : LanguageServiceOperationResponse
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public int DocumentVersion;
        public LanguageServiceDiagnostic[] Diagnostics;
        public LanguageServiceClassifiedSpan[] Classifications;
    }

    /// <summary>
    /// Hover details for a symbol resolved by Roslyn.
    /// </summary>
    public sealed class LanguageServiceHoverResponse : LanguageServiceOperationResponse
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public int DocumentVersion;
        public string SymbolDisplay;
        public string QualifiedSymbolDisplay;
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public string DocumentationXml;
        public string DocumentationText;
        public LanguageServiceHoverSection[] SupplementalSections;
        public LanguageServiceRange Range;
        public string DefinitionDocumentPath;
        public LanguageServiceRange DefinitionRange;
        public LanguageServiceHoverDisplayPart[] DisplayParts;
    }

    /// <summary>
    /// Source definition details resolved by Roslyn.
    /// </summary>
    public sealed class LanguageServiceDefinitionResponse : LanguageServiceOperationResponse
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public int DocumentVersion;
        public string SymbolDisplay;
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public string DocumentationXml;
        public string DocumentationText;
        public LanguageServiceRange Range;
        public string PreviewText;
        public int PreviewStartLine;
    }

    /// <summary>
    /// One completion candidate returned by Roslyn.
    /// </summary>
    public sealed class LanguageServiceCompletionItem
    {
        public string DisplayText;
        public string InsertText;
        public string FilterText;
        public string SortText;
        public string InlineDescription;
        public string Kind;
        public bool IsPreselected;
    }

    /// <summary>
    /// Completion suggestions resolved by Roslyn.
    /// </summary>
    public sealed class LanguageServiceCompletionResponse : LanguageServiceOperationResponse
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public int DocumentVersion;
        public LanguageServiceRange ReplacementRange;
        public LanguageServiceCompletionItem[] Items;
    }

    /// <summary>
    /// One parameter entry inside a signature-help item.
    /// </summary>
    public sealed class LanguageServiceSignatureHelpParameter
    {
        public string Name;
        public string Display;
        public string Documentation;
        public bool IsOptional;
    }

    /// <summary>
    /// One callable signature returned by Roslyn.
    /// </summary>
    public sealed class LanguageServiceSignatureHelpItem
    {
        public string PrefixDisplay;
        public string SeparatorDisplay;
        public string SuffixDisplay;
        public string Documentation;
        public LanguageServiceSignatureHelpParameter[] Parameters;
    }

    /// <summary>
    /// Active signature-help response returned by Roslyn.
    /// </summary>
    public sealed class LanguageServiceSignatureHelpResponse : LanguageServiceOperationResponse
    {
        public string DocumentPath;
        public string ProjectFilePath;
        public int DocumentVersion;
        public LanguageServiceRange ApplicableRange;
        public int ActiveSignatureIndex;
        public int ActiveParameterIndex;
        public LanguageServiceSignatureHelpItem[] Items;
    }

    /// <summary>
    /// Normalized semantic context response for the current symbol.
    /// </summary>
    public sealed class LanguageServiceSymbolContextResponse : LanguageServiceSymbolResponse
    {
    }

    /// <summary>
    /// Semantic rename preview produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceRenameResponse : LanguageServiceSymbolResponse
    {
        public string OldName;
        public string NewName;
        public int TotalChangeCount;
        public LanguageServiceDocumentChange[] Documents;
    }

    /// <summary>
    /// Semantic references produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceReferencesResponse : LanguageServiceLocationResponse
    {
    }

    /// <summary>
    /// Distinct base-symbol navigation targets produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceBaseSymbolResponse : LanguageServiceLocationResponse
    {
    }

    /// <summary>
    /// Distinct implementation targets produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceImplementationResponse : LanguageServiceLocationResponse
    {
    }

    /// <summary>
    /// Semantic call hierarchy produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceCallHierarchyResponse : LanguageServiceSymbolResponse
    {
        public LanguageServiceCallHierarchyItem[] IncomingCalls;
        public LanguageServiceCallHierarchyItem[] OutgoingCalls;
    }

    /// <summary>
    /// Semantic value-source tracking produced by Roslyn.
    /// </summary>
    public sealed class LanguageServiceValueSourceResponse : LanguageServiceSymbolResponse
    {
        public LanguageServiceValueSourceItem[] Items;
    }

    /// <summary>
    /// Previewable document-transform response returned by Roslyn.
    /// </summary>
    public sealed class LanguageServiceDocumentTransformResponse : LanguageServiceOperationResponse
    {
        public string CommandId;
        public string Title;
        public string ApplyLabel;
        public string DocumentPath;
        public string ProjectFilePath;
        public int DocumentVersion;
        public bool CanApply;
        public LanguageServiceDocumentChange[] Documents;
    }

}
