using System;
using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    public enum EditorSurfaceKind
    {
        Unknown = 0,
        Source = 1,
        ReadOnlyCode = 2,
        Decompiled = 3,
        Peek = 4,
        Inspector = 5
    }

    /// <summary>
    /// Immutable-like snapshot of the current editor context for one surface.
    /// Features consume this snapshot instead of holding private symbol/target state.
    /// </summary>
    public sealed class EditorContextSnapshot
    {
        public string ContextKey = string.Empty;
        public string SurfaceId = string.Empty;
        public string PaneId = string.Empty;
        public EditorSurfaceKind SurfaceKind;
        public string DocumentPath = string.Empty;
        public int DocumentVersion;
        public DocumentKind DocumentKind;
        public int CaretIndex = -1;
        public int SelectionAnchorIndex = -1;
        public int SelectionStart = -1;
        public int SelectionEnd = -1;
        public bool HasSelection;
        public string SelectionText = string.Empty;
        public int TargetStart = -1;
        public int TargetLength = -1;
        public string FocusTokenText = string.Empty;
        public string HoverKey = string.Empty;
        public string ContainingMemberName = string.Empty;
        public DateTime CapturedUtc = DateTime.UtcNow;
        public EditorCommandTarget Target;
        public EditorSemanticContext Semantic = new EditorSemanticContext();

        public EditorContextSnapshot Clone()
        {
            return new EditorContextSnapshot
            {
                ContextKey = ContextKey ?? string.Empty,
                SurfaceId = SurfaceId ?? string.Empty,
                PaneId = PaneId ?? string.Empty,
                SurfaceKind = SurfaceKind,
                DocumentPath = DocumentPath ?? string.Empty,
                DocumentVersion = DocumentVersion,
                DocumentKind = DocumentKind,
                CaretIndex = CaretIndex,
                SelectionAnchorIndex = SelectionAnchorIndex,
                SelectionStart = SelectionStart,
                SelectionEnd = SelectionEnd,
                HasSelection = HasSelection,
                SelectionText = SelectionText ?? string.Empty,
                TargetStart = TargetStart,
                TargetLength = TargetLength,
                FocusTokenText = FocusTokenText ?? string.Empty,
                HoverKey = HoverKey ?? string.Empty,
                ContainingMemberName = ContainingMemberName ?? string.Empty,
                CapturedUtc = CapturedUtc,
                Target = Target != null ? Target.Clone() : null,
                Semantic = Semantic != null ? Semantic.Clone() : new EditorSemanticContext()
            };
        }
    }

    public sealed class EditorSemanticContext
    {
        public string QualifiedSymbolDisplay = string.Empty;
        public string SymbolKind = string.Empty;
        public string MetadataName = string.Empty;
        public string ContainingTypeName = string.Empty;
        public string ContainingAssemblyName = string.Empty;
        public string DocumentationCommentId = string.Empty;
        public string HoverText = string.Empty;
        public string DefinitionDocumentPath = string.Empty;
        public int DefinitionLine;
        public int DefinitionColumn;
        public int DefinitionStart = -1;
        public int DefinitionLength = -1;
        public LanguageServiceHoverResponse HoverResponse;
        public EditorResolvedHoverContent HoverContent;

        public EditorSemanticContext Clone()
        {
            return new EditorSemanticContext
            {
                QualifiedSymbolDisplay = QualifiedSymbolDisplay ?? string.Empty,
                SymbolKind = SymbolKind ?? string.Empty,
                MetadataName = MetadataName ?? string.Empty,
                ContainingTypeName = ContainingTypeName ?? string.Empty,
                ContainingAssemblyName = ContainingAssemblyName ?? string.Empty,
                DocumentationCommentId = DocumentationCommentId ?? string.Empty,
                HoverText = HoverText ?? string.Empty,
                DefinitionDocumentPath = DefinitionDocumentPath ?? string.Empty,
                DefinitionLine = DefinitionLine,
                DefinitionColumn = DefinitionColumn,
                DefinitionStart = DefinitionStart,
                DefinitionLength = DefinitionLength,
                HoverResponse = HoverResponse,
                HoverContent = HoverContent != null ? HoverContent.Clone() : null
            };
        }
    }

    public enum EditorHoverNavigationKind
    {
        None = 0,
        Symbol = 1
    }

    public sealed class EditorHoverNavigationTarget
    {
        public EditorHoverNavigationKind Kind;
        public string Label = string.Empty;
        public string SymbolDisplay = string.Empty;
        public string SymbolKind = string.Empty;
        public string MetadataName = string.Empty;
        public string ContainingTypeName = string.Empty;
        public string ContainingAssemblyName = string.Empty;
        public string DocumentationCommentId = string.Empty;
        public string DefinitionDocumentPath = string.Empty;
        public LanguageServiceRange DefinitionRange;

        public EditorHoverNavigationTarget Clone()
        {
            return new EditorHoverNavigationTarget
            {
                Kind = Kind,
                Label = Label ?? string.Empty,
                SymbolDisplay = SymbolDisplay ?? string.Empty,
                SymbolKind = SymbolKind ?? string.Empty,
                MetadataName = MetadataName ?? string.Empty,
                ContainingTypeName = ContainingTypeName ?? string.Empty,
                ContainingAssemblyName = ContainingAssemblyName ?? string.Empty,
                DocumentationCommentId = DocumentationCommentId ?? string.Empty,
                DefinitionDocumentPath = DefinitionDocumentPath ?? string.Empty,
                DefinitionRange = DefinitionRange
            };
        }
    }

    public sealed class EditorHoverSection
    {
        public string Title = string.Empty;
        public string Text = string.Empty;
        public EditorHoverContentPart[] DisplayParts = new EditorHoverContentPart[0];

        public EditorHoverSection Clone()
        {
            return new EditorHoverSection
            {
                Title = Title ?? string.Empty,
                Text = Text ?? string.Empty,
                DisplayParts = CloneParts(DisplayParts)
            };
        }

        private static EditorHoverContentPart[] CloneParts(EditorHoverContentPart[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return new EditorHoverContentPart[0];
            }

            var clone = new EditorHoverContentPart[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                clone[i] = parts[i] != null ? parts[i].Clone() : null;
            }

            return clone;
        }
    }

    public sealed class EditorHoverContentPart
    {
        public string Text = string.Empty;
        public string Classification = string.Empty;
        public bool IsInteractive;
        public string SummaryText = string.Empty;
        public string DocumentationText = string.Empty;
        public EditorHoverSection[] SupplementalSections = new EditorHoverSection[0];
        public EditorHoverNavigationTarget NavigationTarget;

        public EditorHoverContentPart Clone()
        {
            var supplemental = SupplementalSections;
            var supplementalClone = new EditorHoverSection[supplemental != null ? supplemental.Length : 0];
            for (var i = 0; supplemental != null && i < supplemental.Length; i++)
            {
                supplementalClone[i] = supplemental[i] != null ? supplemental[i].Clone() : null;
            }

            return new EditorHoverContentPart
            {
                Text = Text ?? string.Empty,
                Classification = Classification ?? string.Empty,
                IsInteractive = IsInteractive,
                SummaryText = SummaryText ?? string.Empty,
                DocumentationText = DocumentationText ?? string.Empty,
                SupplementalSections = supplementalClone,
                NavigationTarget = NavigationTarget != null ? NavigationTarget.Clone() : null
            };
        }
    }

    public sealed class EditorResolvedHoverContent
    {
        public string Key = string.Empty;
        public string ContextKey = string.Empty;
        public string DocumentPath = string.Empty;
        public int DocumentVersion;
        public string QualifiedPath = string.Empty;
        public string SymbolDisplay = string.Empty;
        public string SummaryText = string.Empty;
        public string DocumentationText = string.Empty;
        public EditorHoverContentPart[] SignatureParts = new EditorHoverContentPart[0];
        public EditorHoverSection[] SupplementalSections = new EditorHoverSection[0];
        public int OverloadIndex = -1;
        public int OverloadCount;
        public string OverloadSummary = string.Empty;
        public EditorHoverNavigationTarget PrimaryNavigationTarget;

        public EditorResolvedHoverContent Clone()
        {
            var signatureParts = SignatureParts;
            var signatureClone = new EditorHoverContentPart[signatureParts != null ? signatureParts.Length : 0];
            for (var i = 0; signatureParts != null && i < signatureParts.Length; i++)
            {
                signatureClone[i] = signatureParts[i] != null ? signatureParts[i].Clone() : null;
            }

            var supplementalSections = SupplementalSections;
            var supplementalClone = new EditorHoverSection[supplementalSections != null ? supplementalSections.Length : 0];
            for (var i = 0; supplementalSections != null && i < supplementalSections.Length; i++)
            {
                supplementalClone[i] = supplementalSections[i] != null ? supplementalSections[i].Clone() : null;
            }

            return new EditorResolvedHoverContent
            {
                Key = Key ?? string.Empty,
                ContextKey = ContextKey ?? string.Empty,
                DocumentPath = DocumentPath ?? string.Empty,
                DocumentVersion = DocumentVersion,
                QualifiedPath = QualifiedPath ?? string.Empty,
                SymbolDisplay = SymbolDisplay ?? string.Empty,
                SummaryText = SummaryText ?? string.Empty,
                DocumentationText = DocumentationText ?? string.Empty,
                SignatureParts = signatureClone,
                SupplementalSections = supplementalClone,
                OverloadIndex = OverloadIndex,
                OverloadCount = OverloadCount,
                OverloadSummary = OverloadSummary ?? string.Empty,
                PrimaryNavigationTarget = PrimaryNavigationTarget != null ? PrimaryNavigationTarget.Clone() : null
            };
        }
    }
}
