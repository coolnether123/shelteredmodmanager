using Cortex.Tabby.Server.Protocol;

namespace Cortex.Tabby.Server.Services;

public sealed class TabbyRequestDebugFormatter
{
    private const int PreviewLength = 400;
    private const int ModelListPreviewCount = 8;

    public string BuildRequestSummary(TabbyCompletionRequest? request)
    {
        return "File=" + (request?.FilePath ?? string.Empty) +
            ", Language=" + (request?.Language ?? string.Empty) +
            ", PrefixLength=" + (request?.Segments?.Prefix?.Length ?? 0) +
            ", SuffixLength=" + (request?.Segments?.Suffix?.Length ?? 0) +
            ", Snippets=" + (request?.RelevantSnippets?.Length ?? 0) +
            ", Declarations=" + (request?.Declarations?.Length ?? 0) + ".";
    }

    public string BuildPromptSummary(string? systemPrompt, string? prefix, string? suffix)
    {
        return "SystemLength=" + (systemPrompt?.Length ?? 0) +
            ", PrefixLength=" + (prefix?.Length ?? 0) +
            ", SuffixLength=" + (suffix?.Length ?? 0) +
            ", SystemPreview=\"" + BuildPreview(systemPrompt) + "\"" +
            ", PrefixPreview=\"" + BuildPreview(prefix) + "\"" +
            ", SuffixPreview=\"" + BuildPreview(suffix) + "\".";
    }

    public string BuildChoiceSummary(string[]? choices)
    {
        var values = choices ?? Array.Empty<string>();
        return "ChoiceCount=" + values.Length +
            ", FirstChoicePreview=\"" + BuildPreview(values.FirstOrDefault()) + "\".";
    }

    public string BuildModelSummary(IEnumerable<string>? modelNames)
    {
        var values = modelNames == null
            ? Array.Empty<string>()
            : modelNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Take(ModelListPreviewCount)
                .ToArray();
        return values.Length == 0
            ? "<none>"
            : string.Join(", ", values);
    }

    public string BuildPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = string.Join(" ", value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= PreviewLength)
        {
            return normalized;
        }

        return normalized.Substring(0, PreviewLength) + "...";
    }
}
