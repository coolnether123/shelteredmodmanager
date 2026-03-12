using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;

namespace Cortex.OpenRouter
{
    public sealed class OpenRouterCompletionClient : ICompletionAugmentationClient
    {
        private readonly object _sync = new object();
        private readonly Queue<CompletionAugmentationResult> _completed = new Queue<CompletionAugmentationResult>();
        private readonly Action<string> _log;
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _promptPreamble;
        private readonly string _appUrl;
        private readonly string _appTitle;
        private readonly int _timeoutMs;
        private int _nextRequestId;
        private bool _disposed;

        public OpenRouterCompletionClient(CortexSettings settings, Action<string> log)
        {
            _endpoint = NormalizeEndpoint(settings != null ? settings.OpenRouterBaseUrl : string.Empty);
            _apiKey = settings != null ? settings.OpenRouterApiKey ?? string.Empty : string.Empty;
            _model = settings != null ? settings.OpenRouterModel ?? string.Empty : string.Empty;
            _promptPreamble = settings != null ? settings.OpenRouterPromptPreamble ?? CompletionAugmentationPromptDefaults.OpenRouterPromptPreamble : CompletionAugmentationPromptDefaults.OpenRouterPromptPreamble;
            _appUrl = settings != null ? settings.OpenRouterAppUrl ?? string.Empty : string.Empty;
            _appTitle = settings != null ? settings.OpenRouterAppTitle ?? "Cortex" : "Cortex";
            _timeoutMs = settings != null && settings.OpenRouterRequestTimeoutMs > 0 ? settings.OpenRouterRequestTimeoutMs : 10000;
            _log = log;
            LastError = string.Empty;
        }

        public bool IsEnabled
        {
            get { return !_disposed && !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_model); }
        }

        public string ProviderId
        {
            get { return CompletionAugmentationProviderIds.OpenRouter; }
        }

        public string LastError { get; private set; }

        public string QueueCompletion(CompletionAugmentationRequest request)
        {
            if (!IsEnabled || request == null)
            {
                LastError = "OpenRouter completion is not configured.";
                return string.Empty;
            }

            var requestId = "openrouter-" + Interlocked.Increment(ref _nextRequestId);
            ThreadPool.QueueUserWorkItem(delegate
            {
                var response = ExecuteCompletion(request);
                lock (_sync)
                {
                    _completed.Enqueue(new CompletionAugmentationResult
                    {
                        RequestId = requestId,
                        ProviderId = ProviderId,
                        Response = response
                    });
                }
            });
            return requestId;
        }

        public bool TryDequeueResponse(out CompletionAugmentationResult result)
        {
            lock (_sync)
            {
                if (_completed.Count == 0)
                {
                    result = null;
                    return false;
                }

                result = _completed.Dequeue();
                return true;
            }
        }

        public bool CancelCompletion(string requestId)
        {
            return false;
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private LanguageServiceCompletionResponse ExecuteCompletion(CompletionAugmentationRequest request)
        {
            try
            {
                var payload = new OpenRouterCompletionRequest
                {
                    model = _model,
                    prompt = ComposePrompt(request),
                    suffix = request.SuffixText ?? string.Empty,
                    max_tokens = 192
                };
                var json = ManualJson.Serialize(payload);
                var httpRequest = (HttpWebRequest)WebRequest.Create(_endpoint);
                httpRequest.Method = "POST";
                httpRequest.ContentType = "application/json";
                httpRequest.Accept = "application/json";
                httpRequest.Timeout = _timeoutMs;
                httpRequest.ReadWriteTimeout = _timeoutMs;
                httpRequest.Headers["Authorization"] = "Bearer " + _apiKey;
                if (!string.IsNullOrEmpty(_appUrl))
                {
                    httpRequest.Headers["HTTP-Referer"] = _appUrl;
                }
                if (!string.IsNullOrEmpty(_appTitle))
                {
                    httpRequest.Headers["X-Title"] = _appTitle;
                }

                using (var stream = httpRequest.GetRequestStream())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                }

                using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
                using (var stream = httpResponse.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null))
                {
                    var responseJson = reader.ReadToEnd();
                    var payloadResponse = ManualJson.Deserialize<OpenRouterCompletionResponse>(responseJson) ?? new OpenRouterCompletionResponse();
                    var text = ExtractText(payloadResponse);
                    return CompletionAugmentationProviderSupport.BuildSuccessResponse(request, text, "OpenRouter");
                }
            }
            catch (WebException ex)
            {
                LastError = ReadWebException(ex);
                Log("Completion request failed: " + LastError);
                return BuildErrorResponse(request, LastError);
            }
            catch (Exception ex)
            {
                LastError = ex.Message ?? "Unknown OpenRouter completion failure.";
                Log("Completion request failed: " + ex);
                return BuildErrorResponse(request, LastError);
            }
        }

        private string ComposePrompt(CompletionAugmentationRequest request)
        {
            var instructions = CompletionAugmentationProviderSupport.ResolveInstructionText(
                _promptPreamble ?? CompletionAugmentationPromptDefaults.OpenRouterPromptPreamble,
                request);
            var context = CompletionAugmentationProviderSupport.BuildCuratedContextBlock(request);
            var builder = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(instructions))
            {
                builder.AppendLine(instructions);
            }

            if (!string.IsNullOrEmpty(context))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine(context);
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Continue the following document prefix at the cursor. The exact suffix is supplied separately by Cortex.");
            }

            builder.Append(request != null ? request.PrefixText ?? string.Empty : string.Empty);
            return builder.ToString();
        }

        private static string ExtractText(OpenRouterCompletionResponse payload)
        {
            if (payload == null || payload.choices == null || payload.choices.Length == 0 || payload.choices[0] == null)
            {
                return string.Empty;
            }

            return payload.choices[0].text ?? string.Empty;
        }

        private static LanguageServiceCompletionResponse BuildErrorResponse(CompletionAugmentationRequest request, string message)
        {
            return CompletionAugmentationProviderSupport.BuildErrorResponse(request, message);
        }

        private static string NormalizeEndpoint(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return string.Empty;
            }

            var trimmed = baseUrl.Trim();
            if (trimmed.EndsWith("/completions", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return trimmed.TrimEnd('/') + "/completions";
        }

        private static string ReadWebException(WebException ex)
        {
            return CompletionAugmentationProviderSupport.ReadWebException("OpenRouter request failed.", ex);
        }

        private void Log(string message)
        {
            if (_log != null && !string.IsNullOrEmpty(message))
            {
                _log("[Cortex.OpenRouter] " + message);
            }
        }

        private sealed class OpenRouterCompletionRequest
        {
            public string model;
            public string prompt;
            public string suffix;
            public int max_tokens;
        }

        private sealed class OpenRouterCompletionResponse
        {
            public OpenRouterChoice[] choices;
        }

        private sealed class OpenRouterChoice
        {
            public string text;
        }
    }
}
