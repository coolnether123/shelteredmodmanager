using System;

namespace Cortex.Core.Models
{
    public enum HarmonyPatchKind
    {
        Unknown = 0,
        Prefix = 1,
        Postfix = 2,
        Transpiler = 3,
        Finalizer = 4,
        InnerPrefix = 5,
        InnerPostfix = 6
    }

    public enum HarmonyPatchGenerationKind
    {
        Prefix = 0,
        Postfix = 1
    }

    public enum HarmonyPatchInsertionAnchorKind
    {
        EndOfFile = 0,
        ExplicitLine = 1,
        NamespaceOrClass = 2,
        SelectedContext = 3
    }

    public sealed class HarmonyPatchCounts
    {
        public int PrefixCount;
        public int PostfixCount;
        public int TranspilerCount;
        public int FinalizerCount;
        public int InnerPrefixCount;
        public int InnerPostfixCount;
        public int TotalCount;
    }

    public sealed class HarmonyPatchOwnerAssociation
    {
        public string OwnerId;
        public string DisplayName;
        public string LoadedModId;
        public string LoadedModRootPath;
        public string ProjectModId;
        public string ProjectSourceRootPath;
        public string AssemblyPath;
        public string MatchReason;
        public bool HasMatch;
    }

    public sealed class HarmonyPatchNavigationTarget
    {
        public string AssemblyPath;
        public int MetadataToken;
        public string DocumentPath;
        public string CachePath;
        public string DeclaringTypeName;
        public string MethodName;
        public string Signature;
        public string DisplayName;
        public int Line;
        public int Column;
        public bool IsDecompilerTarget;
    }

    public sealed class HarmonyPatchEntry
    {
        public HarmonyPatchKind PatchKind;
        public string OwnerId;
        public string OwnerDisplayName;
        public string PatchMethodDeclaringType;
        public string PatchMethodName;
        public string PatchMethodSignature;
        public int Priority;
        public string[] Before;
        public string[] After;
        public int Index;
        public string AssemblyPath;
        public int MetadataToken;
        public HarmonyPatchOwnerAssociation OwnerAssociation;
        public HarmonyPatchNavigationTarget NavigationTarget;
    }

    public sealed class HarmonyPatchOrderExplanationItem
    {
        public int Position;
        public string OwnerId;
        public string PatchMethodName;
        public int Priority;
        public int Index;
        public string[] Before;
        public string[] After;
        public string Explanation;
    }

    public sealed class HarmonyPatchOrderExplanation
    {
        public HarmonyPatchKind PatchKind;
        public string Disclaimer;
        public HarmonyPatchOrderExplanationItem[] Items;
    }

    public sealed class HarmonyMethodPatchSummary
    {
        public HarmonyPatchNavigationTarget Target;
        public string DeclaringType;
        public string MethodName;
        public string Signature;
        public string AssemblyPath;
        public string DocumentPath;
        public string CachePath;
        public string ResolvedMemberDisplayName;
        public string ProjectModId;
        public string ProjectSourceRootPath;
        public string LoadedModId;
        public string LoadedModRootPath;
        public HarmonyPatchCounts Counts;
        public int OwnerCount;
        public string[] Owners;
        public string ConflictHint;
        public HarmonyPatchEntry[] Entries;
        public HarmonyPatchOrderExplanation[] Order;
        public bool IsPatched;
        public DateTime CapturedUtc;
    }

    public sealed class HarmonyPatchSnapshot
    {
        public DateTime GeneratedUtc;
        public HarmonyMethodPatchSummary[] Methods;
        public string StatusMessage;
    }

    public sealed class HarmonyPatchInspectionRequest
    {
        public string AssemblyPath;
        public int MetadataToken;
        public string DeclaringTypeName;
        public string MethodName;
        public string Signature;
        public string DisplayName;
        public string DocumentPath;
        public string CachePath;
        public string DocumentationCommentId;
    }

    public sealed class HarmonyPatchGenerationRequest
    {
        public HarmonyPatchGenerationKind GenerationKind;
        public string TargetAssemblyPath;
        public int TargetMetadataToken;
        public string TargetDeclaringTypeName;
        public string TargetMethodName;
        public string TargetSignature;
        public string TargetDocumentPath;
        public string TargetCachePath;
        public string NamespaceName;
        public string DestinationFilePath;
        public HarmonyPatchInsertionAnchorKind InsertionAnchorKind;
        public int InsertionLine;
        public int InsertionAbsolutePosition;
        public string InsertionContextLabel;
        public string PatchClassName;
        public string PatchMethodName;
        public string HarmonyIdMetadata;
        public bool UseSkipOriginalPattern;
        public bool IncludeInstanceParameter;
        public bool IncludeArgumentParameters;
        public bool IncludeStateParameter;
        public bool IncludeResultParameter;
    }

    public sealed class HarmonyPatchInsertionTarget
    {
        public string FilePath;
        public string DisplayName;
        public bool IsNewFile;
        public bool IsWritable;
        public HarmonyPatchInsertionAnchorKind DefaultAnchorKind;
        public int SuggestedLine;
        public int SuggestedAbsolutePosition;
        public string SuggestedContextLabel;
        public string Reason;
    }

    public sealed class HarmonyPatchGenerationPreview
    {
        public string SnippetText;
        public string PreviewText;
        public int InsertionOffset;
        public int InsertionLine;
        public string InsertionContextLabel;
        public GeneratedTemplatePlaceholder[] Placeholders;
        public string StatusMessage;
        public bool CanApply;
    }

    public sealed class GeneratedTemplatePlaceholder
    {
        public string PlaceholderId;
        public int Start;
        public int Length;
        public string DefaultText;
        public string Description;
    }

    public sealed class GeneratedTemplateSession
    {
        public string DocumentPath;
        public int StartOffset;
        public int EndOffset;
        public int ActivePlaceholderIndex;
        public int DocumentVersion;
        public int LastKnownTextLength;
        public bool Completed;
        public GeneratedTemplatePlaceholder[] Placeholders;
    }
}
