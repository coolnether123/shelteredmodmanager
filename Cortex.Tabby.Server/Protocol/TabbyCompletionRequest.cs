using System.Text.Json.Serialization;

namespace Cortex.Tabby.Server.Protocol;

public sealed class TabbyCompletionRequest
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "plaintext";

    [JsonPropertyName("filepath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("git_url")]
    public string GitUrl { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("declarations")]
    public string[] Declarations { get; set; } = Array.Empty<string>();

    [JsonPropertyName("current_line_prefix")]
    public string CurrentLinePrefix { get; set; } = string.Empty;

    [JsonPropertyName("current_line_suffix")]
    public string CurrentLineSuffix { get; set; } = string.Empty;

    [JsonPropertyName("relevant_snippets")]
    public TabbyRelevantSnippet[] RelevantSnippets { get; set; } = Array.Empty<TabbyRelevantSnippet>();

    [JsonPropertyName("segments")]
    public TabbyCompletionSegments Segments { get; set; } = new();
}

public sealed class TabbyCompletionSegments
{
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("suffix")]
    public string Suffix { get; set; } = string.Empty;
}

public sealed class TabbyRelevantSnippet
{
    [JsonPropertyName("filepath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
