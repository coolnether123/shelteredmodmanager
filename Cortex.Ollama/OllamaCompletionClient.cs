using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;

namespace Cortex.Ollama
{
    public sealed class OllamaCompletionClient : ICompletionAugmentationClient
    {
        private readonly object _sync = new object();
        private readonly Queue<CompletionAugmentationResult> _completed = new Queue<CompletionAugmentationResult>();
        private readonly Action<string> _log;
        private readonly string _endpoint;
        private readonly string _apiToken;
        private readonly string _model;
        private readonly string _systemPrompt;
        private readonly int _timeoutMs;
        private int _nextRequestId;
        private bool _disposed;

        public OllamaCompletionClient(CortexSettings settings, Action<string> log)
        {
            _endpoint = NormalizeEndpoint(settings != null ? settings.OllamaServerUrl : string.Empty);
            _apiToken = settings != null ? settings.OllamaApiToken ?? string.Empty : string.Empty;
            _model = settings != null ? settings.OllamaModel ?? string.Empty : string.Empty;
            _systemPrompt = settings != null ? settings.OllamaSystemPrompt ?? CompletionAugmentationPromptDefaults.OllamaSystemPrompt : CompletionAugmentationPromptDefaults.OllamaSystemPrompt;
            _timeoutMs = settings != null && settings.OllamaRequestTimeoutMs > 0 ? settings.OllamaRequestTimeoutMs : 8000;
            _log = log;
            LastError = string.Empty;
        }

        public bool IsEnabled
        {
            get { return !_disposed && !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_model); }
        }

        public string ProviderId
        {
            get { return CompletionAugmentationProviderIds.Ollama; }
        }

        public string LastError { get; private set; }

        public string QueueCompletion(CompletionAugmentationRequest request)
        {
            if (!IsEnabled || request == null)
            {
                LastError = "Ollama completion is not configured.";
                return string.Empty;
            }

            var requestId = "ollama-" + Interlocked.Increment(ref _nextRequestId);
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

        public void Dispose()
        {
            _disposed = true;
        }

        private LanguageServiceCompletionResponse ExecuteCompletion(CompletionAugmentationRequest request)
        {
            try
            {
                var payload = new OllamaGenerateRequest
                {
                    model = _model,
                    prompt = request.PrefixText ?? string.Empty,
                    suffix = request.SuffixText ?? string.Empty,
                    system = ComposeSystemPrompt(request),
                    stream = false,
                    raw = false
                };
                var json = ManualJson.Serialize(payload);
                var httpRequest = (HttpWebRequest)WebRequest.Create(_endpoint);
                httpRequest.Method = "POST";
                httpRequest.ContentType = "application/json";
                httpRequest.Accept = "application/json";
                httpRequest.Timeout = _timeoutMs;
                httpRequest.ReadWriteTimeout = _timeoutMs;
                if (!string.IsNullOrEmpty(_apiToken))
                {
                    httpRequest.Headers["Authorization"] = "Bearer " + _apiToken;
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
                    var payloadResponse = ManualJson.Deserialize<OllamaGenerateResponse>(responseJson) ?? new OllamaGenerateResponse();
                    return CompletionAugmentationProviderSupport.BuildSuccessResponse(request, payloadResponse.response, "Ollama");
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
                LastError = ex.Message ?? "Unknown Ollama completion failure.";
                Log("Completion request failed: " + ex);
                return BuildErrorResponse(request, LastError);
            }
        }

        private string ComposeSystemPrompt(CompletionAugmentationRequest request)
        {
            var instructions = CompletionAugmentationProviderSupport.ResolveInstructionText(
                _systemPrompt ?? CompletionAugmentationPromptDefaults.OllamaSystemPrompt,
                request);
            var context = CompletionAugmentationProviderSupport.BuildCuratedContextBlock(request);
            if (string.IsNullOrEmpty(context))
            {
                return instructions;
            }

            if (string.IsNullOrEmpty(instructions))
            {
                return context;
            }

            return instructions + "\n\n" + context;
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
            if (trimmed.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return trimmed.TrimEnd('/') + "/api/generate";
        }

        private static string ReadWebException(WebException ex)
        {
            return CompletionAugmentationProviderSupport.ReadWebException("Ollama request failed.", ex);
        }

        private void Log(string message)
        {
            if (_log != null && !string.IsNullOrEmpty(message))
            {
                _log("[Cortex.Ollama] " + message);
            }
        }

        private sealed class OllamaGenerateRequest
        {
            public string model;
            public string prompt;
            public string suffix;
            public string system;
            public bool stream;
            public bool raw;
        }

        private sealed class OllamaGenerateResponse
        {
            public string response;
        }
    }
}
