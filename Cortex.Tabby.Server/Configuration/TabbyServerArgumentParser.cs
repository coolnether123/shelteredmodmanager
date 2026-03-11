namespace Cortex.Tabby.Server.Configuration;

public static class TabbyServerArgumentParser
{
    public static TabbyServerArgumentParseResult Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("--", StringComparison.Ordinal))
            {
                return TabbyServerArgumentParseResult.Fail("Unexpected argument '" + token + "'.");
            }

            var key = token.Substring(2);
            if (i + 1 >= args.Length)
            {
                return TabbyServerArgumentParseResult.Fail("Missing value for argument '--" + key + "'.");
            }

            values[key] = args[++i];
        }

        var timeout = 15000;
        if (values.TryGetValue("request-timeout-ms", out var timeoutText) &&
            !int.TryParse(timeoutText, out timeout))
        {
            return TabbyServerArgumentParseResult.Fail("Invalid integer for '--request-timeout-ms'.");
        }

        var model = GetValue(values, "ollama-model");
        if (string.IsNullOrWhiteSpace(model))
        {
            return TabbyServerArgumentParseResult.Fail("Missing required '--ollama-model' value.");
        }

        return TabbyServerArgumentParseResult.Ok(new TabbyServerOptions
        {
            ListenUrl = TabbyServerOptions.NormalizeBaseUrl(GetValue(values, "urls")),
            OllamaBaseUrl = TabbyServerOptions.NormalizeOllamaUrl(GetValue(values, "ollama-url")),
            OllamaApiToken = GetValue(values, "ollama-api-token"),
            OllamaModel = model,
            RequestTimeoutMs = timeout > 0 ? timeout : 15000
        });
    }

    private static string GetValue(IDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
    }
}

public sealed class TabbyServerArgumentParseResult
{
    public bool Success { get; private init; }

    public string ErrorMessage { get; private init; } = string.Empty;

    public TabbyServerOptions? Options { get; private init; }

    public static TabbyServerArgumentParseResult Ok(TabbyServerOptions options)
    {
        return new TabbyServerArgumentParseResult
        {
            Success = true,
            Options = options
        };
    }

    public static TabbyServerArgumentParseResult Fail(string message)
    {
        return new TabbyServerArgumentParseResult
        {
            Success = false,
            ErrorMessage = message ?? string.Empty
        };
    }
}
