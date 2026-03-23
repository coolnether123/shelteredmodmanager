using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    public sealed class EditorResolvedContextAction
    {
        public string ActionId;
        public string CommandId;
        public string ContextId;
        public string Group;
        public string Title;
        public string Description;
        public string ShortcutText;
        public string RequiredCapability;
        public string DisabledReason;
        public int SortOrder;
        public EditorContextActionPlacement Placements;
        public bool Enabled;
    }

    public enum SemanticWorkbenchViewKind
    {
        None = 0,
        References = 1,
        RenamePreview = 2,
        PeekDefinition = 3,
        CallHierarchy = 4,
        ValueSource = 5,
        UnitTestGeneration = 6,
        BaseSymbols = 7,
        Implementations = 8,
        DocumentEditPreview = 9
    }

    public enum SemanticRequestKind
    {
        None = 0,
        SymbolContext = 1,
        RenamePreview = 2,
        References = 3,
        PeekDefinition = 4,
        BaseSymbol = 5,
        Implementations = 6,
        CallHierarchy = 7,
        ValueSource = 8,
        DocumentTransformPreview = 9
    }

    public static class SemanticCapabilityNames
    {
        public const string SymbolContext = "symbol-context";
        public const string Rename = "rename";
        public const string References = "references";
        public const string Definition = "definition";
        public const string BaseSymbol = "base-symbol";
        public const string Implementations = "implementations";
        public const string CallHierarchy = "call-hierarchy";
        public const string ValueSource = "value-source";
        public const string UnitTestGeneration = "unit-test-generation";
    }

    public sealed class UnitTestGenerationPlan
    {
        public string SymbolDisplay;
        public string SymbolName;
        public string SymbolKind;
        public string TestProjectPath;
        public string OutputFilePath;
        public string GeneratedText;
        public string StatusMessage;
        public bool CanApply;
    }

    public sealed class DocumentEditPreviewPlan
    {
        public string CommandId = string.Empty;
        public string Title = string.Empty;
        public string ApplyLabel = string.Empty;
        public string StatusMessage = string.Empty;
        public string PrimaryDocumentPath = string.Empty;
        public LanguageServiceDocumentChange[] Documents = new LanguageServiceDocumentChange[0];
        public bool CanApply;
    }
}
