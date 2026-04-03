using System.Text;
using Cortex.Contracts.Completion;
using Cortex.Tabby.Server.Protocol;

namespace Cortex.Tabby.Server.Services;

public sealed class TabbyRequestContextFormatter
{
    private const string DefaultInstruction = CompletionAugmentationPromptContract.StrictCodeCompletionInstruction;

    public string BuildSystemPrompt(TabbyCompletionRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine(DefaultInstruction);

        AppendLine(builder, "Language", request.Language);
        AppendLine(builder, "File", request.FilePath);
        AppendLine(builder, "User", request.User);
        AppendLine(builder, "GitUrl", request.GitUrl);

        if (request.Declarations is { Length: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("[Declarations]");
            foreach (var declaration in request.Declarations)
            {
                if (!string.IsNullOrWhiteSpace(declaration))
                {
                    builder.AppendLine(declaration);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentLinePrefix) ||
            !string.IsNullOrWhiteSpace(request.CurrentLineSuffix))
        {
            builder.AppendLine();
            builder.AppendLine("[Current Line]");
            AppendLine(builder, "Prefix", request.CurrentLinePrefix);
            AppendLine(builder, "Suffix", request.CurrentLineSuffix);
        }

        if (request.RelevantSnippets is { Length: > 0 })
        {
            for (var i = 0; i < request.RelevantSnippets.Length; i++)
            {
                var snippet = request.RelevantSnippets[i];
                if (snippet == null || string.IsNullOrWhiteSpace(snippet.Content))
                {
                    continue;
                }

                builder.AppendLine();
                builder.Append("[Relevant Snippet ");
                builder.Append(i + 1);
                builder.Append("]");
                if (!string.IsNullOrWhiteSpace(snippet.FilePath))
                {
                    builder.Append(' ');
                    builder.Append(snippet.FilePath);
                }

                builder.AppendLine();
                builder.AppendLine(snippet.Content);
            }
        }

        return builder.ToString().Trim();
    }

    private static void AppendLine(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(value.Trim());
    }
}
