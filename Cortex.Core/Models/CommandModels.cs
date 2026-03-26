namespace Cortex.Core.Models
{
    public delegate void CommandHandler(CommandExecutionContext context);
    public delegate bool CommandEnablement(CommandExecutionContext context);

    public sealed class CommandDefinition
    {
        public string CommandId;
        public string DisplayName;
        public string Category;
        public string IconId;
        public string Description;
        public string DefaultGesture;
        public int SortOrder;
        public bool ShowInPalette;
        public bool IsGlobal;
    }

    public sealed class CommandExecutionContext
    {
        public string ActiveContainerId;
        public string ActiveDocumentId;
        public string FocusedRegionId;
        public object Parameter;
    }

    public static class EditorContextIds
    {
        public const string Document = "cortex.editor.document";
        public const string Symbol = "cortex.editor.symbol";
    }

    public sealed class EditorCommandTarget
    {
        public EditorCommandTarget()
        {
            ContextKey = string.Empty;
            SurfaceId = string.Empty;
            ContextId = string.Empty;
            DocumentPath = string.Empty;
            SymbolText = string.Empty;
            QualifiedSymbolDisplay = string.Empty;
            SymbolKind = string.Empty;
            MetadataName = string.Empty;
            ContainingTypeName = string.Empty;
            ContainingAssemblyName = string.Empty;
            DocumentationCommentId = string.Empty;
            HoverText = string.Empty;
            SelectionText = string.Empty;
            DefinitionDocumentPath = string.Empty;
            CaretIndex = -1;
            SelectionStart = -1;
            SelectionEnd = -1;
            DefinitionStart = -1;
            DefinitionLength = -1;
        }

        public string ContextKey;
        public string SurfaceId;
        public string ContextId;
        public string DocumentPath;
        public string SymbolText;
        public string QualifiedSymbolDisplay;
        public string SymbolKind;
        public string MetadataName;
        public string ContainingTypeName;
        public string ContainingAssemblyName;
        public string DocumentationCommentId;
        public string HoverText;
        public int Line;
        public int Column;
        public int AbsolutePosition;
        public int CaretIndex;
        public int SelectionStart;
        public int SelectionEnd;
        public string SelectionText;
        public bool HasSelection;
        public DocumentKind DocumentKind;
        public bool SupportsEditing;
        public bool IsEditModeEnabled;
        public bool CanToggleEditMode;
        public bool CanGoToDefinition;
        public string DefinitionDocumentPath;
        public int DefinitionLine;
        public int DefinitionColumn;
        public int DefinitionStart;
        public int DefinitionLength;

        public EditorCommandTarget Clone()
        {
            return new EditorCommandTarget
            {
                ContextKey = ContextKey ?? string.Empty,
                SurfaceId = SurfaceId ?? string.Empty,
                ContextId = ContextId ?? string.Empty,
                DocumentPath = DocumentPath ?? string.Empty,
                SymbolText = SymbolText ?? string.Empty,
                QualifiedSymbolDisplay = QualifiedSymbolDisplay ?? string.Empty,
                SymbolKind = SymbolKind ?? string.Empty,
                MetadataName = MetadataName ?? string.Empty,
                ContainingTypeName = ContainingTypeName ?? string.Empty,
                ContainingAssemblyName = ContainingAssemblyName ?? string.Empty,
                DocumentationCommentId = DocumentationCommentId ?? string.Empty,
                HoverText = HoverText ?? string.Empty,
                Line = Line,
                Column = Column,
                AbsolutePosition = AbsolutePosition,
                CaretIndex = CaretIndex,
                SelectionStart = SelectionStart,
                SelectionEnd = SelectionEnd,
                SelectionText = SelectionText ?? string.Empty,
                HasSelection = HasSelection,
                DocumentKind = DocumentKind,
                SupportsEditing = SupportsEditing,
                IsEditModeEnabled = IsEditModeEnabled,
                CanToggleEditMode = CanToggleEditMode,
                CanGoToDefinition = CanGoToDefinition,
                DefinitionDocumentPath = DefinitionDocumentPath ?? string.Empty,
                DefinitionLine = DefinitionLine,
                DefinitionColumn = DefinitionColumn,
                DefinitionStart = DefinitionStart,
                DefinitionLength = DefinitionLength
            };
        }
    }

    public enum EditorCommandExecutionKind
    {
        Unavailable = 0,
        Direct = 1,
        SourceRedirect = 2,
        PreviewApply = 3
    }

    public sealed class EditorCommandAvailability
    {
        public bool Visible = true;
        public bool Enabled;
        public string DisabledReason = string.Empty;
        public string Description = string.Empty;
        public EditorCommandExecutionKind ExecutionKind;
    }

    public sealed class EditorCommandInvocation
    {
        public string ActiveContainerId = string.Empty;
        public string ActiveDocumentId = string.Empty;
        public string FocusedRegionId = string.Empty;
        public EditorCommandTarget Target;
    }

    public sealed class CommandHandlerRegistration
    {
        public string CommandId;
        public CommandHandler Handler;
        public CommandEnablement CanExecute;
    }

    public sealed class CommandBinding
    {
        public string CommandId;
        public string Gesture;
    }
}
