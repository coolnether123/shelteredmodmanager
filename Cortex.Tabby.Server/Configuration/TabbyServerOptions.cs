namespace Cortex.Tabby.Server.Configuration;

public sealed class TabbyServerOptions
{
    public const string DefaultListenUrl = "http://127.0.0.1:5118";
    public const string DefaultOllamaUrl = "http://127.0.0.1:11434";

    public string ListenUrl { get; init; } = DefaultListenUrl;

    public string OllamaBaseUrl { get; init; } = DefaultOllamaUrl;

    public string OllamaApiToken { get; init; } = string.Empty;

    public string OllamaModel { get; init; } = string.Empty;

    public int RequestTimeoutMs { get; init; } = 15000;

    public string CompletionUrl
    {
        get { return BuildAbsoluteUrl("/api/completion"); }
    }

    public string HealthUrl
    {
        get { return BuildAbsoluteUrl("/health"); }
    }

    private string BuildAbsoluteUrl(string relativePath)
    {
        return NormalizeBaseUrl(ListenUrl).TrimEnd('/') + relativePath;
    }

    public static string NormalizeBaseUrl(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? DefaultListenUrl : value.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed.TrimEnd('/') : trimmed;
    }

    public static string NormalizeOllamaUrl(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? DefaultOllamaUrl : value.Trim();
        return trimmed.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed.TrimEnd('/') + "/api/generate";
    }
}
