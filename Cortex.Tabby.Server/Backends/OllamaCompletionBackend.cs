using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Cortex.Tabby.Server.Configuration;
using Cortex.Tabby.Server.Protocol;
using Cortex.Tabby.Server.Services;

namespace Cortex.Tabby.Server.Backends;

public sealed class OllamaCompletionBackend : ICompletionBackend
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaCompletionBackend> _logger;
    private readonly TabbyServerOptions _options;
    private readonly TabbyRequestContextFormatter _formatter;
    private readonly TabbyRequestDebugFormatter _debugFormatter;

    public OllamaCompletionBackend(
        HttpClient httpClient,
        TabbyServerOptions options,
        TabbyRequestContextFormatter formatter,
        TabbyRequestDebugFormatter debugFormatter,
        ILogger<OllamaCompletionBackend> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _formatter = formatter;
        _debugFormatter = debugFormatter;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, options.RequestTimeoutMs));
        if (!string.IsNullOrEmpty(options.OllamaApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.OllamaApiToken);
        }
    }

    public string BackendId
    {
        get { return "ollama"; }
    }

    public async Task<CompletionBackendHealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var tagsUrl = BuildTagsUrl(_options.OllamaBaseUrl);
        try
        {
            _logger.LogInformation(
                "Checking Ollama backend health. TagsUrl={TagsUrl}, Model={Model}.",
                tagsUrl,
                _options.OllamaModel);

            using var response = await _httpClient.GetAsync(tagsUrl, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = "Ollama health request failed: " + (int)response.StatusCode + " " + response.ReasonPhrase +
                    ". BodyPreview=\"" + _debugFormatter.BuildPreview(responseText) + "\".";
                _logger.LogWarning("{Message}", failureMessage);
                return new CompletionBackendHealthStatus
                {
                    IsHealthy = false,
                    ModelAvailable = false,
                    Message = failureMessage,
                    Endpoint = tagsUrl
                };
            }

            var payload = JsonSerializer.Deserialize<OllamaTagsResponse>(responseText, JsonOptions) ?? new OllamaTagsResponse();
            var modelNames = (payload.Models ?? Array.Empty<OllamaModelDescriptor>())
                .Select(model => !string.IsNullOrWhiteSpace(model.Name) ? model.Name : model.Model)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
            var modelAvailable = string.IsNullOrWhiteSpace(_options.OllamaModel) ||
                modelNames.Any(name => string.Equals(name, _options.OllamaModel, StringComparison.OrdinalIgnoreCase));
            var successMessage = "Ollama reachable. ModelAvailable=" + modelAvailable +
                ", Models=" + _debugFormatter.BuildModelSummary(modelNames) + ".";
            _logger.LogInformation("{Message}", successMessage);
            return new CompletionBackendHealthStatus
            {
                IsHealthy = true,
                ModelAvailable = modelAvailable,
                Message = successMessage,
                Endpoint = tagsUrl
            };
        }
        catch (Exception ex)
        {
            var exceptionMessage = "Ollama health request threw " + ex.GetType().Name +
                ": " + ex.Message;
            _logger.LogWarning(ex, "{Message}", exceptionMessage);
            return new CompletionBackendHealthStatus
            {
                IsHealthy = false,
                ModelAvailable = false,
                Message = exceptionMessage,
                Endpoint = tagsUrl
            };
        }
    }

    public async Task<CompletionBackendResult> CompleteAsync(
        TabbyCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var systemPrompt = _formatter.BuildSystemPrompt(request);
        var prompt = request.Segments?.Prefix ?? string.Empty;
        var suffix = request.Segments?.Suffix ?? string.Empty;
        var payload = new OllamaGenerateRequest
        {
            Model = _options.OllamaModel,
            Prompt = prompt,
            Suffix = suffix,
            System = systemPrompt,
            Stream = false,
            Raw = false
        };

        _logger.LogInformation(
            "Dispatching Ollama completion. Model={Model}, Url={Url}, TimeoutMs={TimeoutMs}, {Summary}",
            _options.OllamaModel,
            _options.OllamaBaseUrl,
            _options.RequestTimeoutMs,
            _debugFormatter.BuildRequestSummary(request));
        _logger.LogInformation(
            "Ollama prompt summary: {Summary}",
            _debugFormatter.BuildPromptSummary(systemPrompt, prompt, suffix));

        var stopwatch = Stopwatch.StartNew();
        using var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.PostAsync(_options.OllamaBaseUrl, content, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Ollama completion failed. StatusCode={StatusCode}, Reason={Reason}, DurationMs={DurationMs}, BodyPreview=\"{BodyPreview}\".",
                (int)response.StatusCode,
                response.ReasonPhrase,
                stopwatch.ElapsedMilliseconds,
                _debugFormatter.BuildPreview(responseText));
            throw new InvalidOperationException("Ollama request failed: " + (int)response.StatusCode + " " + response.ReasonPhrase + ". " + responseText);
        }

        var parsed = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseText, JsonOptions) ?? new OllamaGenerateResponse();
        stopwatch.Stop();
        _logger.LogInformation(
            "Ollama completion succeeded. StatusCode={StatusCode}, DurationMs={DurationMs}, ResponseLength={ResponseLength}, ResponsePreview=\"{ResponsePreview}\".",
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            parsed.Response?.Length ?? 0,
            _debugFormatter.BuildPreview(parsed.Response));
        return new CompletionBackendResult
        {
            Choices = string.IsNullOrWhiteSpace(parsed.Response)
                ? Array.Empty<string>()
                : new[] { parsed.Response }
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class OllamaGenerateRequest
    {
        public string Model { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public string Suffix { get; set; } = string.Empty;

        public string System { get; set; } = string.Empty;

        public bool Stream { get; set; }

        public bool Raw { get; set; }
    }

    private sealed class OllamaGenerateResponse
    {
        public string Response { get; set; } = string.Empty;
    }

    private sealed class OllamaTagsResponse
    {
        public OllamaModelDescriptor[] Models { get; set; } = Array.Empty<OllamaModelDescriptor>();
    }

    private sealed class OllamaModelDescriptor
    {
        public string Name { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;
    }

    private static string BuildTagsUrl(string ollamaBaseUrl)
    {
        var normalized = (ollamaBaseUrl ?? string.Empty).Trim();
        if (normalized.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.Substring(0, normalized.Length - "generate".Length) + "tags";
        }

        return normalized.TrimEnd('/') + "/api/tags";
    }
}
