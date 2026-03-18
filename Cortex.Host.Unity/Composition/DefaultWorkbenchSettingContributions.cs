using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Unity.Composition
{
    internal sealed class DefaultWorkbenchSettingContributions
    {
        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            context.RegisterSetting(nameof(CortexSettings.WorkspaceRootPath), "Workspace Scan Root", "Tell Cortex where to scan for editable workspace sources.", "Workspace", string.Empty, SettingValueKind.String, 0);
            context.RegisterSetting(nameof(CortexSettings.ModsRootPath), "Loaded Mods Root", "Points to the live mod folder used for source mapping and discovery.", "Workspace", string.Empty, SettingValueKind.String, 10);
            context.RegisterSetting(nameof(CortexSettings.ManagedAssemblyRootPath), "Game Managed DLLs", "Used to locate assemblies for reference browsing and decompilation.", "Workspace", string.Empty, SettingValueKind.String, 20);
            context.RegisterSetting(nameof(CortexSettings.AdditionalSourceRoots), "Extra Source Roots", "Semicolon-separated fallback roots for source resolution.", "Workspace", string.Empty, SettingValueKind.String, 30);
            context.RegisterSetting(nameof(CortexSettings.ProjectCatalogPath), "Project Catalog", "Path to the persisted Cortex project catalog file.", "Workspace", string.Empty, SettingValueKind.String, 40);
            context.RegisterSetting(nameof(CortexSettings.CortexPluginSearchRoots), "Cortex Plugin Roots", "Semicolon-separated roots scanned for external Cortex workbench plugins.", "Workspace", string.Empty, SettingValueKind.String, 50);

            context.RegisterSetting(nameof(CortexSettings.LogFilePath), "Live Log File", "Optional file that is tailed under the live in-memory log feed.", "Logs", string.Empty, SettingValueKind.String, 0);
            context.RegisterSetting(nameof(CortexSettings.MaxRecentLogs), "Max Recent Logs", "Maximum number of live log entries to keep in the in-memory feed.", "Logs", "300", SettingValueKind.Integer, 10);
            context.RegisterSetting(nameof(CortexSettings.AutoScrollLogs), "Auto-scroll Log List", "Keep the live log list pinned to the newest entry.", "Logs", "true", SettingValueKind.Boolean, 20);
            context.RegisterSetting(nameof(CortexSettings.ShowLogBacklog), "Show File Tail History", "Optional raw file tail for lines that were written before Cortex attached or were not captured in the live feed.", "Logs", "false", SettingValueKind.Boolean, 30);

            context.RegisterSetting(nameof(CortexSettings.DecompilerPathOverride), "Decompiler Override", "Optional path to a custom decompiler executable.", "Decompiler", string.Empty, SettingValueKind.String, 0);
            context.RegisterSetting(nameof(CortexSettings.DecompilerCachePath), "Decompiler Cache", "Location used to cache generated source from runtime and reference browsing.", "Decompiler", string.Empty, SettingValueKind.String, 10);
            context.RegisterSetting(nameof(CortexSettings.EnableRoslynLanguageService), "Enable Roslyn Worker", "Start the external Roslyn language service for diagnostics, hover info, and semantic classification.", "Language Service", "true", SettingValueKind.Boolean, 0);
            context.RegisterSetting(nameof(CortexSettings.RoslynServicePathOverride), "Roslyn Worker Path", "Optional override for the external Cortex.Roslyn.Worker executable or DLL.", "Language Service", string.Empty, SettingValueKind.String, 10);
            context.RegisterSetting(nameof(CortexSettings.RoslynServiceTimeoutMs), "Roslyn Timeout (ms)", "Maximum request time for the external Roslyn language service.", "Language Service", "15000", SettingValueKind.Integer, 20);
            context.RegisterSetting(nameof(CortexSettings.EnableCompletionAugmentation), "Enable AI Completion", "Enable the modular completion augmentation pipeline. Cortex remains in control of which document text and related snippets are supplied to providers.", "AI", "false", SettingValueKind.Boolean, 0);
            context.RegisterSetting(nameof(CortexSettings.CompletionAugmentationProviderId), "Active Provider", "Provider id used by the augmentation pipeline. Built-in ids are tabby, ollama, and openrouter.", "AI", CompletionAugmentationProviderIds.Tabby, SettingValueKind.String, 10);
            context.RegisterSetting(nameof(CortexSettings.CompletionAugmentationAdditionalInstructions), "Additional Instructions", "Optional extra completion instructions appended to provider defaults when supported. Tabby ignores this because Cortex cannot override Tabby's internal prompt template through its completion API.", "AI", string.Empty, SettingValueKind.String, 20);
            context.RegisterSetting(nameof(CortexSettings.CompletionAugmentationReplaceProviderPrompt), "Replace Provider Prompt", "When enabled, providers that support prompt steering use the additional instructions instead of their built-in default prompt text. Tabby ignores this setting.", "AI", "false", SettingValueKind.Boolean, 30);
            context.RegisterSetting(nameof(CortexSettings.CompletionAugmentationIncludeOpenDocumentSnippets), "Include Open Doc Snippets", "Allow Cortex to attach snippets from other open source documents. Providers only receive snippets Cortex explicitly selected here and do not crawl your workspace on their own.", "AI", "true", SettingValueKind.Boolean, 40);
            context.RegisterSetting(nameof(CortexSettings.CompletionAugmentationSnippetDocumentLimit), "Snippet Document Limit", "Maximum number of other open source documents Cortex may sample for provider context.", "AI", "3", SettingValueKind.Integer, 50);
            context.RegisterSetting(nameof(CortexSettings.CompletionAugmentationSnippetCharacterLimit), "Snippet Character Limit", "Maximum characters sampled from each related document snippet.", "AI", "800", SettingValueKind.Integer, 60);

            context.RegisterSetting(nameof(CortexSettings.EnableTabbyCompletion), "Enable Tabby Provider", "Allow the Tabby provider to be selected. Leave Tabby Server URL blank to use the bundled local Cortex.Tabby.Server host backed by Ollama.", "AI - Tabby", "false", SettingValueKind.Boolean, 0);
            context.RegisterSetting(nameof(CortexSettings.TabbyServerUrl), "Tabby Server URL", "Optional explicit Tabby-compatible endpoint, for example http://localhost:8080. Leave blank to let Cortex launch the bundled local Tabby host.", "AI - Tabby", string.Empty, SettingValueKind.String, 10);
            context.RegisterSetting(nameof(CortexSettings.TabbyApiToken), "Tabby API Token", "Optional bearer token used when the Tabby server requires authorization.", "AI - Tabby", string.Empty, SettingValueKind.String, 20);
            context.RegisterSetting(nameof(CortexSettings.TabbyRequestTimeoutMs), "Tabby Timeout (ms)", "Maximum time allowed for Tabby completion requests.", "AI - Tabby", "8000", SettingValueKind.Integer, 30);

            context.RegisterSetting(nameof(CortexSettings.OllamaServerUrl), "Ollama Server URL", "Base URL for the Ollama server, for example http://localhost:11434.", "AI - Ollama", "http://localhost:11434", SettingValueKind.String, 0);
            context.RegisterSetting(nameof(CortexSettings.OllamaApiToken), "Ollama API Token", "Optional bearer token for Ollama when the endpoint sits behind authenticated routing.", "AI - Ollama", string.Empty, SettingValueKind.String, 10);
            context.RegisterSetting(nameof(CortexSettings.OllamaModel), "Ollama Model", "Model name used for direct Ollama completion requests.", "AI - Ollama", string.Empty, SettingValueKind.String, 20);
            context.RegisterSetting(nameof(CortexSettings.OllamaSystemPrompt), "Ollama System Prompt", "Default system prompt used for direct Ollama completions. Additional instructions append to this unless Replace Provider Prompt is enabled.", "AI - Ollama", CompletionAugmentationPromptDefaults.OllamaSystemPrompt, SettingValueKind.String, 30);
            context.RegisterSetting(nameof(CortexSettings.OllamaRequestTimeoutMs), "Ollama Timeout (ms)", "Maximum time allowed for direct Ollama completion requests.", "AI - Ollama", "8000", SettingValueKind.Integer, 40);

            context.RegisterSetting(nameof(CortexSettings.OpenRouterBaseUrl), "OpenRouter Base URL", "Base URL for OpenRouter, normally https://openrouter.ai/api/v1.", "AI - OpenRouter", "https://openrouter.ai/api/v1", SettingValueKind.String, 0);
            context.RegisterSetting(nameof(CortexSettings.OpenRouterApiKey), "OpenRouter API Key", "API key used for direct OpenRouter completion requests.", "AI - OpenRouter", string.Empty, SettingValueKind.String, 10);
            context.RegisterSetting(nameof(CortexSettings.OpenRouterModel), "OpenRouter Model", "Model name used for direct OpenRouter completion requests. Use a model that supports completion/insertion style requests.", "AI - OpenRouter", string.Empty, SettingValueKind.String, 20);
            context.RegisterSetting(nameof(CortexSettings.OpenRouterPromptPreamble), "OpenRouter Prompt Preamble", "Default prompt preamble used ahead of the current document prefix. Additional instructions append to this unless Replace Provider Prompt is enabled.", "AI - OpenRouter", CompletionAugmentationPromptDefaults.OpenRouterPromptPreamble, SettingValueKind.String, 30);
            context.RegisterSetting(nameof(CortexSettings.OpenRouterAppUrl), "OpenRouter App URL", "Optional HTTP-Referer value sent to OpenRouter.", "AI - OpenRouter", string.Empty, SettingValueKind.String, 40);
            context.RegisterSetting(nameof(CortexSettings.OpenRouterAppTitle), "OpenRouter App Title", "Optional X-Title value sent to OpenRouter.", "AI - OpenRouter", "Cortex", SettingValueKind.String, 50);
            context.RegisterSetting(nameof(CortexSettings.OpenRouterRequestTimeoutMs), "OpenRouter Timeout (ms)", "Maximum time allowed for direct OpenRouter completion requests.", "AI - OpenRouter", "10000", SettingValueKind.Integer, 60);

            context.RegisterSetting(nameof(CortexSettings.DefaultBuildConfiguration), "Default Build Config", "Default build configuration used by build tooling.", "Build", "Debug", SettingValueKind.String, 0);
            context.RegisterSetting(nameof(CortexSettings.BuildTimeoutMs), "Build Timeout (ms)", "Maximum time allowed for build execution before timing out.", "Build", "300000", SettingValueKind.Integer, 10);

            context.RegisterSetting(nameof(CortexSettings.ThemeId), "Theme", "Active workbench theme identifier.", "Appearance", "cortex.vs-dark", SettingValueKind.String, 0);
            context.RegisterSetting(nameof(CortexSettings.EnableFileEditing), "Enable File Editing", "Allow source files to be unlocked for direct editing inside Cortex.", "Editing", "false", SettingValueKind.Boolean, 0);
            context.RegisterSetting(nameof(CortexSettings.EnableFileSaving), "Enable File Saving", "Allow Save and Save All to write snapshot-based changes back into source files.", "Editing", "false", SettingValueKind.Boolean, 10);
            context.RegisterSetting(nameof(CortexSettings.LogsPaneWidth), "Logs Pane Width", "Preferred width for the logs/details split.", "Layout", "520", SettingValueKind.Float, 0);
            context.RegisterSetting(nameof(CortexSettings.ProjectsPaneWidth), "Projects Pane Width", "Preferred width for the side host.", "Layout", "360", SettingValueKind.Float, 10);
            context.RegisterSetting(nameof(CortexSettings.EditorFilePaneWidth), "Secondary Tool Width", "Preferred width for the right-side tool window host.", "Layout", "420", SettingValueKind.Float, 20);
            context.RegisterSetting(nameof(CortexSettings.WindowX), "Window X", "Saved shell position on the X axis.", "Window", "70", SettingValueKind.Float, 0);
            context.RegisterSetting(nameof(CortexSettings.WindowY), "Window Y", "Saved shell position on the Y axis.", "Window", "70", SettingValueKind.Float, 10);
            context.RegisterSetting(nameof(CortexSettings.WindowWidth), "Window Width", "Saved shell width.", "Window", "1180", SettingValueKind.Float, 20);
            context.RegisterSetting(nameof(CortexSettings.WindowHeight), "Window Height", "Saved shell height.", "Window", "760", SettingValueKind.Float, 30);
        }
    }
}
