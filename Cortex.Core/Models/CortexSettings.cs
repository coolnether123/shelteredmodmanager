using System;

namespace Cortex.Core.Models
{
    [Serializable]
    public sealed class CortexSettings
    {
        public string WorkspaceRootPath;
        public string ModsRootPath;
        public string ManagedAssemblyRootPath;
        public string AdditionalSourceRoots;
        public string CortexPluginSearchRoots;
        public string LogFilePath;
        public string ProjectCatalogPath;
        public string DecompilerPathOverride;
        public string DecompilerCachePath;
        public bool EnableRoslynLanguageService;
        public string RoslynServicePathOverride;
        public int RoslynServiceTimeoutMs;
        public bool EnableCompletionAugmentation;
        public string CompletionAugmentationProviderId;
        public string CompletionAugmentationAdditionalInstructions;
        public bool CompletionAugmentationReplaceProviderPrompt;
        public bool CompletionAugmentationIncludeOpenDocumentSnippets;
        public int CompletionAugmentationSnippetDocumentLimit;
        public int CompletionAugmentationSnippetCharacterLimit;
        public bool EnableTabbyCompletion;
        public string TabbyServerUrl;
        public string TabbyApiToken;
        public int TabbyRequestTimeoutMs;
        public string OllamaServerUrl;
        public string OllamaApiToken;
        public string OllamaModel;
        public string OllamaSystemPrompt;
        public int OllamaRequestTimeoutMs;
        public string OpenRouterBaseUrl;
        public string OpenRouterApiKey;
        public string OpenRouterModel;
        public string OpenRouterPromptPreamble;
        public string OpenRouterAppUrl;
        public string OpenRouterAppTitle;
        public int OpenRouterRequestTimeoutMs;
        public string ThemeId;
        public string DefaultBuildConfiguration;
        public int BuildTimeoutMs;
        public int MaxRecentLogs;
        public bool AutoScrollLogs;
        public bool ShowLogBacklog;
        public bool EnableFileEditing;
        public bool EnableFileSaving;
        public int EditorUndoHistoryLimit;
        public EditorKeybinding[] EditorKeybindings;
        public float LogsPaneWidth;
        public float ProjectsPaneWidth;
        public float EditorFilePaneWidth;
        public float WindowX;
        public float WindowY;
        public float WindowWidth;
        public float WindowHeight;
        public string SettingsCollapsedGroupIds;

        public CortexSettings()
        {
            WorkspaceRootPath = string.Empty;
            ModsRootPath = string.Empty;
            ManagedAssemblyRootPath = string.Empty;
            AdditionalSourceRoots = string.Empty;
            CortexPluginSearchRoots = string.Empty;
            LogFilePath = string.Empty;
            ProjectCatalogPath = string.Empty;
            DecompilerPathOverride = string.Empty;
            DecompilerCachePath = string.Empty;
            EnableRoslynLanguageService = true;
            RoslynServicePathOverride = string.Empty;
            RoslynServiceTimeoutMs = 15000;
            EnableCompletionAugmentation = false;
            CompletionAugmentationProviderId = CompletionAugmentationProviderIds.Tabby;
            CompletionAugmentationAdditionalInstructions = string.Empty;
            CompletionAugmentationReplaceProviderPrompt = false;
            CompletionAugmentationIncludeOpenDocumentSnippets = true;
            CompletionAugmentationSnippetDocumentLimit = 3;
            CompletionAugmentationSnippetCharacterLimit = 800;
            EnableTabbyCompletion = false;
            TabbyServerUrl = string.Empty;
            TabbyApiToken = string.Empty;
            TabbyRequestTimeoutMs = 8000;
            OllamaServerUrl = "http://localhost:11434";
            OllamaApiToken = string.Empty;
            OllamaModel = string.Empty;
            OllamaSystemPrompt = CompletionAugmentationPromptDefaults.OllamaSystemPrompt;
            OllamaRequestTimeoutMs = 8000;
            OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
            OpenRouterApiKey = string.Empty;
            OpenRouterModel = string.Empty;
            OpenRouterPromptPreamble = CompletionAugmentationPromptDefaults.OpenRouterPromptPreamble;
            OpenRouterAppUrl = string.Empty;
            OpenRouterAppTitle = "Cortex";
            OpenRouterRequestTimeoutMs = 10000;
            ThemeId = "cortex.vs-dark";
            DefaultBuildConfiguration = "Debug";
            BuildTimeoutMs = 300000;
            MaxRecentLogs = 300;
            AutoScrollLogs = true;
            ShowLogBacklog = false;
            EnableFileEditing = false;
            EnableFileSaving = false;
            EditorUndoHistoryLimit = 128;
            EditorKeybindings = new EditorKeybinding[0];
            LogsPaneWidth = 520f;
            ProjectsPaneWidth = 360f;
            EditorFilePaneWidth = 420f;
            WindowX = 70f;
            WindowY = 70f;
            WindowWidth = 1180f;
            WindowHeight = 760f;
            SettingsCollapsedGroupIds = string.Empty;
        }
    }
}
