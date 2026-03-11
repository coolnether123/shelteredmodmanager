using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    /// <summary>
    /// Well-known built-in completion augmentation provider identifiers.
    /// Providers are referenced by stable string ids so settings remain forward-compatible.
    /// </summary>
    public static class CompletionAugmentationProviderIds
    {
        public const string Tabby = "tabby";
        public const string Ollama = "ollama";
        public const string OpenRouter = "openrouter";
    }

    /// <summary>
    /// Shared default prompt fragments used by providers that support prompt steering.
    /// </summary>
    public static class CompletionAugmentationPromptDefaults
    {
        public const string OllamaSystemPrompt = "Continue the code at the cursor. Return code only. Preserve the surrounding style, syntax, and naming conventions.";
        public const string OpenRouterPromptPreamble = "Continue the code at the cursor. Return code only. Preserve the surrounding style, syntax, and naming conventions.";
    }

    /// <summary>
    /// A Cortex-selected snippet that may be supplied to a completion provider.
    /// Providers must only use context explicitly provided here and must not infer
    /// additional workspace files on their own.
    /// </summary>
    public sealed class CompletionAugmentationSnippet
    {
        public string SourceId;
        public string DisplayName;
        public string RelativePath;
        public string Content;
        public float Score;
    }

    /// <summary>
    /// Provider-neutral completion request built and fully curated by Cortex.
    /// This model is intentionally richer than the Roslyn worker request so
    /// future providers can reuse the same context contract.
    /// </summary>
    public sealed class CompletionAugmentationRequest
    {
        public string ProviderId;
        public string DocumentPath;
        public string ProjectFilePath;
        public string WorkspaceRootPath;
        public string RelativeDocumentPath;
        public string LanguageId;
        public string DocumentText;
        public int DocumentVersion;
        public int AbsolutePosition;
        public bool ExplicitInvocation;
        public string TriggerCharacter;
        public string PrefixText;
        public string SuffixText;
        public string SelectedText;
        public string AdditionalInstructions;
        public bool ReplaceProviderPrompt;
        public string[] Declarations;
        public CompletionAugmentationSnippet[] RelatedSnippets;
    }

    /// <summary>
    /// Normalized completion result returned by an augmentation provider.
    /// Provider-specific wire formats are translated into the common
    /// LanguageServiceCompletionResponse so the UI remains decoupled.
    /// </summary>
    public sealed class CompletionAugmentationResult
    {
        public string RequestId;
        public string ProviderId;
        public LanguageServiceCompletionResponse Response;
    }
}
