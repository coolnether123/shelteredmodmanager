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
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public string DocumentationXml;
        public string DocumentationText;
        public LanguageServiceRange Range;
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
    }
}
