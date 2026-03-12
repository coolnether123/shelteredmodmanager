using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;

namespace Cortex.Tabby
{
    /// <summary>
    /// Thin asynchronous client for Tabby's completion HTTP API.
    /// The shell only depends on a generic completion augmentation contract so
    /// future context enrichment or provider swaps stay localized here.
    /// </summary>
    public sealed class TabbyCompletionClient : ICompletionAugmentationClient
    {
        private const int PrefixWindowLength = 8192;
        private const int SuffixWindowLength = 2048;
        private readonly object _sync = new object();
        private readonly Queue<CompletionAugmentationResult> _completed = new Queue<CompletionAugmentationResult>();
        private readonly Dictionary<string, ActiveRequestState> _activeRequests = new Dictionary<string, ActiveRequestState>();
        private readonly Action<string> _log;
        private readonly IDisposable _ownedResource;
        private int _nextRequestId;
        private bool _disposed;

        public TabbyCompletionClient(string endpoint, string apiToken, int timeoutMs, Action<string> log)
            : this(endpoint, apiToken, timeoutMs, log, null)
        {
        }

        public TabbyCompletionClient(string endpoint, string apiToken, int timeoutMs, Action<string> log, IDisposable ownedResource)
        {
            Endpoint = NormalizeEndpoint(endpoint);
            ApiToken = apiToken ?? string.Empty;
            TimeoutMs = timeoutMs > 0 ? timeoutMs : 8000;
            _log = log;
            _ownedResource = ownedResource;
            LastError = string.Empty;
        }

        /// <summary>
        /// Normalized completion endpoint. Base server URLs are expanded to /api/completion.
        /// </summary>
        public string Endpoint { get; private set; }

        /// <summary>
        /// Optional bearer token supplied to Tabby.
        /// </summary>
        public string ApiToken { get; private set; }

        public string ProviderId
        {
            get { return CompletionAugmentationProviderIds.Tabby; }
        }

        public int TimeoutMs { get; private set; }

        public string LastError { get; private set; }

        public bool IsEnabled
        {
            get { return !_disposed && !string.IsNullOrEmpty(Endpoint); }
        }

        public string QueueCompletion(CompletionAugmentationRequest request)
        {
            if (!IsEnabled || request == null)
            {
                LastError = "Tabby completion is not configured.";
                return string.Empty;
            }

            var requestId = "tabby-" + Interlocked.Increment(ref _nextRequestId);
            var activeRequest = new ActiveRequestState();
            lock (_sync)
            {
                _activeRequests[requestId] = activeRequest;
            }
            Log("Queueing completion request " + requestId +
                ". Endpoint=" + Endpoint +
                ", Document=" + (request.DocumentPath ?? string.Empty) +
                ", Position=" + request.AbsolutePosition +
                ", PrefixLength=" + ((request.PrefixText ?? string.Empty).Length) +
                ", SuffixLength=" + ((request.SuffixText ?? string.Empty).Length) +
                ", RelatedSnippets=" + (request.RelatedSnippets != null ? request.RelatedSnippets.Length : 0) + ".");
            ThreadPool.QueueUserWorkItem(delegate
            {
                var response = ExecuteCompletion(request, activeRequest);
                lock (_sync)
                {
                    _activeRequests.Remove(requestId);
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

        public bool CancelCompletion(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            ActiveRequestState activeRequest;
            lock (_sync)
            {
                if (!_activeRequests.TryGetValue(requestId, out activeRequest))
                {
                    return false;
                }
            }

            activeRequest.Cancelled = true;
            try
            {
                if (activeRequest.HttpRequest != null)
                {
                    activeRequest.HttpRequest.Abort();
                }
            }
            catch
            {
            }

            try
            {
                if (activeRequest.TcpClient != null)
                {
                    activeRequest.TcpClient.Close();
                }
            }
            catch
            {
            }

            Log("Canceled completion request " + requestId + ".");
            return true;
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
            lock (_sync)
            {
                foreach (var activeRequest in _activeRequests.Values)
                {
                    activeRequest.Cancelled = true;
                    try
                    {
                        if (activeRequest.HttpRequest != null)
                        {
                            activeRequest.HttpRequest.Abort();
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (activeRequest.TcpClient != null)
                        {
                            activeRequest.TcpClient.Close();
                        }
                    }
                    catch
                    {
                    }
                }

                _activeRequests.Clear();
            }
            if (_ownedResource != null)
            {
                _ownedResource.Dispose();
            }
        }

        private LanguageServiceCompletionResponse ExecuteCompletion(CompletionAugmentationRequest request, ActiveRequestState activeRequest)
        {
            try
            {
                var payload = BuildPayload(request);
                var json = ManualJson.Serialize(payload);
                Uri endpointUri;
                if (TryCreateLoopbackEndpoint(Endpoint, out endpointUri))
                {
                    return ExecuteCompletionOverTcp(request, endpointUri, json ?? string.Empty, activeRequest);
                }

                var requestBody = Encoding.UTF8.GetBytes(json ?? string.Empty);
                var httpRequest = (HttpWebRequest)WebRequest.Create(Endpoint);
                if (activeRequest != null)
                {
                    activeRequest.HttpRequest = httpRequest;
                }
                httpRequest.Method = "POST";
                httpRequest.ContentType = "application/json";
                httpRequest.Accept = "application/json";
                httpRequest.Timeout = TimeoutMs;
                httpRequest.ReadWriteTimeout = TimeoutMs;
                httpRequest.Proxy = null;
                httpRequest.KeepAlive = false;
                httpRequest.SendChunked = false;
                httpRequest.ContentLength = requestBody.Length;
                if (httpRequest.ServicePoint != null)
                {
                    httpRequest.ServicePoint.Expect100Continue = false;
                    httpRequest.ServicePoint.UseNagleAlgorithm = false;
                }
                if (!string.IsNullOrEmpty(ApiToken))
                {
                    httpRequest.Headers["Authorization"] = "Bearer " + ApiToken;
                }

                Log("Opening request stream. Endpoint=" + Endpoint +
                    ", BodyBytes=" + requestBody.Length +
                    ", TimeoutMs=" + TimeoutMs + ".");
                using (var stream = httpRequest.GetRequestStream())
                {
                    stream.Write(requestBody, 0, requestBody.Length);
                    stream.Flush();
                }
                Log("Request body sent. Endpoint=" + Endpoint +
                    ", BodyBytes=" + requestBody.Length + ".");

                Log("Awaiting completion response from " + Endpoint + ".");
                using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
                using (var stream = httpResponse.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null))
                {
                    var responseJson = reader.ReadToEnd();
                    Log("HTTP response received. StatusCode=" + (int)httpResponse.StatusCode +
                        ", Description=" + (httpResponse.StatusDescription ?? string.Empty) + ".");
                    var payloadResponse = ManualJson.Deserialize<TabbyCompletionPayloadResponse>(responseJson) ?? new TabbyCompletionPayloadResponse();
                    Log("Completion response received. Choices=" + (payloadResponse.choices != null ? payloadResponse.choices.Length : 0) +
                        ", Document=" + (request.DocumentPath ?? string.Empty) + ".");
                    return ConvertResponse(request, payloadResponse);
                }
            }
            catch (WebException ex)
            {
                LastError = activeRequest != null && activeRequest.Cancelled
                    ? "canceled"
                    : ReadWebException(ex);
                Log("Completion request failed. Status=" + ex.Status +
                    ", Message=" + LastError);
                return BuildErrorResponse(request, LastError);
            }
            catch (Exception ex)
            {
                LastError = ex.Message ?? "Unknown Tabby completion failure.";
                Log("Completion request failed: " + ex);
                return BuildErrorResponse(request, LastError);
            }
        }

        private static TabbyCompletionPayloadRequest BuildPayload(CompletionAugmentationRequest request)
        {
            return new TabbyCompletionPayloadRequest
            {
                language = request.LanguageId ?? "plaintext",
                filepath = request.RelativeDocumentPath ?? BuildRelativePath(request.DocumentPath, request.WorkspaceRootPath),
                git_url = string.Empty,
                user = Environment.UserName ?? string.Empty,
                declarations = request.Declarations ?? new string[0],
                current_line_prefix = request.CurrentLinePrefixText ?? string.Empty,
                current_line_suffix = request.CurrentLineSuffixText ?? string.Empty,
                relevant_snippets = BuildRelevantSnippets(request.RelatedSnippets),
                segments = new TabbyCompletionSegments
                {
                    prefix = TrimToWindow(request.PrefixText, PrefixWindowLength, true),
                    suffix = TrimToWindow(request.SuffixText, SuffixWindowLength, false)
                }
            };
        }

        private static LanguageServiceCompletionResponse ConvertResponse(
            CompletionAugmentationRequest request,
            TabbyCompletionPayloadResponse payload)
        {
            var completions = new List<string>();
            if (payload != null && payload.choices != null)
            {
                for (var i = 0; i < payload.choices.Length; i++)
                {
                    var choice = payload.choices[i];
                    var text = choice != null ? choice.text ?? string.Empty : string.Empty;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    completions.Add(text);
                }
            }

            return CompletionAugmentationProviderSupport.BuildSuccessResponse(
                request,
                completions.ToArray(),
                "Tabby");
        }

        private static LanguageServiceCompletionResponse BuildErrorResponse(CompletionAugmentationRequest request, string message)
        {
            return CompletionAugmentationProviderSupport.BuildErrorResponse(request, message);
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return string.Empty;
            }

            var normalized = endpoint.Trim();
            if (normalized.EndsWith("/api/completion", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return normalized.TrimEnd('/') + "/api/completion";
        }

        private LanguageServiceCompletionResponse ExecuteCompletionOverTcp(
            CompletionAugmentationRequest request,
            Uri endpointUri,
            string json,
            ActiveRequestState activeRequest)
        {
            var requestBody = Encoding.UTF8.GetBytes(json ?? string.Empty);
            var port = endpointUri.Port > 0 ? endpointUri.Port : 80;
            var pathAndQuery = string.IsNullOrEmpty(endpointUri.PathAndQuery) ? "/" : endpointUri.PathAndQuery;
            Log("Opening TCP connection. Endpoint=" + endpointUri +
                ", BodyBytes=" + requestBody.Length +
                ", TimeoutMs=" + TimeoutMs + ".");
            using (var client = new TcpClient())
            {
                if (activeRequest != null)
                {
                    activeRequest.TcpClient = client;
                }
                client.SendTimeout = TimeoutMs;
                client.ReceiveTimeout = TimeoutMs;
                var connectResult = client.BeginConnect(endpointUri.Host, port, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(TimeoutMs))
                {
                    client.Close();
                    throw new WebException("The request timed out", WebExceptionStatus.Timeout);
                }

                client.EndConnect(connectResult);
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = TimeoutMs;
                    stream.WriteTimeout = TimeoutMs;

                    var headerBuilder = new StringBuilder();
                    headerBuilder.Append("POST ").Append(pathAndQuery).Append(" HTTP/1.1\r\n");
                    headerBuilder.Append("Host: ").Append(endpointUri.Host).Append(":").Append(port).Append("\r\n");
                    headerBuilder.Append("Content-Type: application/json\r\n");
                    headerBuilder.Append("Accept: application/json\r\n");
                    headerBuilder.Append("Connection: close\r\n");
                    headerBuilder.Append("Content-Length: ").Append(requestBody.Length).Append("\r\n");
                    if (!string.IsNullOrEmpty(ApiToken))
                    {
                        headerBuilder.Append("Authorization: Bearer ").Append(ApiToken).Append("\r\n");
                    }

                    headerBuilder.Append("\r\n");
                    var headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(requestBody, 0, requestBody.Length);
                    stream.Flush();
                    Log("TCP request body sent. Endpoint=" + endpointUri +
                        ", BodyBytes=" + requestBody.Length + ".");

                    var responseBytes = ReadToEnd(stream);
                    var parsedResponse = ParseHttpResponse(responseBytes);
                    var statusLine = parsedResponse.StatusLine ?? string.Empty;
                    Log("TCP response received. StatusLine=" + statusLine + ".");
                    if (string.IsNullOrEmpty(statusLine) || statusLine.IndexOf(" 200 ", StringComparison.Ordinal) < 0)
                    {
                        LastError = string.IsNullOrEmpty(statusLine) ? "Tabby server returned an empty response." : statusLine;
                        return BuildErrorResponse(request, LastError);
                    }

                    Log("TCP response headers. TransferEncoding=" + GetHeaderValue(parsedResponse.Headers, "Transfer-Encoding") +
                        ", ContentLength=" + GetHeaderValue(parsedResponse.Headers, "Content-Length") + ".");
                    var responseBody = Encoding.UTF8.GetString(parsedResponse.BodyBytes ?? new byte[0]);
                    var payloadResponse = ManualJson.Deserialize<TabbyCompletionPayloadResponse>(responseBody) ?? new TabbyCompletionPayloadResponse();
                    Log("Completion response received. Choices=" + (payloadResponse.choices != null ? payloadResponse.choices.Length : 0) +
                        ", Document=" + (request.DocumentPath ?? string.Empty) + ".");
                    return ConvertResponse(request, payloadResponse);
                }
            }
        }

        private static byte[] ReadToEnd(Stream stream)
        {
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[4096];
                while (true)
                {
                    var read = stream.Read(chunk, 0, chunk.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    buffer.Write(chunk, 0, read);
                }

                return buffer.ToArray();
            }
        }

        private static ParsedHttpResponse ParseHttpResponse(byte[] responseBytes)
        {
            var result = new ParsedHttpResponse();
            if (responseBytes == null || responseBytes.Length == 0)
            {
                result.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result.BodyBytes = new byte[0];
                return result;
            }

            var headerEnd = FindHeaderEnd(responseBytes);
            if (headerEnd < 0)
            {
                result.StatusLine = string.Empty;
                result.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result.BodyBytes = responseBytes;
                return result;
            }

            var headerText = Encoding.ASCII.GetString(responseBytes, 0, headerEnd);
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            result.StatusLine = lines.Length > 0 ? lines[0] : string.Empty;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = separatorIndex + 1 < line.Length ? line.Substring(separatorIndex + 1).Trim() : string.Empty;
                headers[key] = value;
            }

            result.Headers = headers;
            var bodyOffset = headerEnd + 4;
            var bodyLength = Math.Max(0, responseBytes.Length - bodyOffset);
            var rawBody = new byte[bodyLength];
            if (bodyLength > 0)
            {
                Buffer.BlockCopy(responseBytes, bodyOffset, rawBody, 0, bodyLength);
            }

            result.BodyBytes = IsChunked(headers)
                ? DecodeChunkedBody(rawBody)
                : rawBody;
            return result;
        }

        private static int FindHeaderEnd(byte[] responseBytes)
        {
            for (var i = 0; i <= responseBytes.Length - 4; i++)
            {
                if (responseBytes[i] == '\r' &&
                    responseBytes[i + 1] == '\n' &&
                    responseBytes[i + 2] == '\r' &&
                    responseBytes[i + 3] == '\n')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsChunked(IDictionary<string, string> headers)
        {
            var transferEncoding = GetHeaderValue(headers, "Transfer-Encoding");
            return !string.IsNullOrEmpty(transferEncoding) &&
                transferEncoding.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static byte[] DecodeChunkedBody(byte[] rawBody)
        {
            if (rawBody == null || rawBody.Length == 0)
            {
                return new byte[0];
            }

            using (var input = new MemoryStream(rawBody))
            using (var output = new MemoryStream())
            {
                while (true)
                {
                    var sizeLine = ReadAsciiLine(input);
                    if (string.IsNullOrEmpty(sizeLine))
                    {
                        break;
                    }

                    var semicolonIndex = sizeLine.IndexOf(';');
                    var sizeToken = semicolonIndex >= 0 ? sizeLine.Substring(0, semicolonIndex) : sizeLine;
                    int chunkSize;
                    if (!int.TryParse(sizeToken.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out chunkSize))
                    {
                        break;
                    }

                    if (chunkSize <= 0)
                    {
                        break;
                    }

                    var chunk = new byte[chunkSize];
                    var totalRead = 0;
                    while (totalRead < chunkSize)
                    {
                        var read = input.Read(chunk, totalRead, chunkSize - totalRead);
                        if (read <= 0)
                        {
                            break;
                        }

                        totalRead += read;
                    }

                    output.Write(chunk, 0, totalRead);

                    if (input.Position + 1 < input.Length)
                    {
                        input.ReadByte();
                        input.ReadByte();
                    }
                }

                return output.ToArray();
            }
        }

        private static string ReadAsciiLine(Stream stream)
        {
            var builder = new StringBuilder();
            while (true)
            {
                var value = stream.ReadByte();
                if (value < 0)
                {
                    break;
                }

                if (value == '\r')
                {
                    var next = stream.ReadByte();
                    if (next != '\n' && next >= 0 && stream.CanSeek)
                    {
                        stream.Position--;
                    }
                    break;
                }

                if (value == '\n')
                {
                    break;
                }

                builder.Append((char)value);
            }

            return builder.ToString();
        }

        private static string GetHeaderValue(IDictionary<string, string> headers, string key)
        {
            if (headers == null || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            string value;
            return headers.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static bool TryCreateLoopbackEndpoint(string endpoint, out Uri uri)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildRelativePath(string documentPath, string workspaceRootPath)
        {
            if (string.IsNullOrEmpty(documentPath))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(workspaceRootPath))
            {
                return documentPath.Replace('\\', '/');
            }

            try
            {
                var root = AppendDirectorySeparator(workspaceRootPath);
                var rootUri = new Uri(root, UriKind.Absolute);
                var pathUri = new Uri(documentPath, UriKind.Absolute);
                return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
            }
            catch
            {
                return documentPath.Replace('\\', '/');
            }
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return path.EndsWith("\\", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal)
                ? path
                : path + "\\";
        }

        private static TabbyRelevantSnippet[] BuildRelevantSnippets(CompletionAugmentationSnippet[] snippets)
        {
            if (snippets == null || snippets.Length == 0)
            {
                return new TabbyRelevantSnippet[0];
            }

            var results = new List<TabbyRelevantSnippet>(snippets.Length);
            for (var i = 0; i < snippets.Length; i++)
            {
                var snippet = snippets[i];
                if (snippet == null || string.IsNullOrEmpty(snippet.Content))
                {
                    continue;
                }

                results.Add(new TabbyRelevantSnippet
                {
                    filepath = snippet.RelativePath ?? snippet.SourceId ?? string.Empty,
                    content = snippet.Content
                });
            }

            return results.ToArray();
        }

        private static string ReadWebException(WebException ex)
        {
            return CompletionAugmentationProviderSupport.ReadWebException("Tabby request failed.", ex);
        }

        private void Log(string message)
        {
            if (_log != null && !string.IsNullOrEmpty(message))
            {
                _log("[Cortex.Tabby] " + message);
            }
        }

        private sealed class ActiveRequestState
        {
            public HttpWebRequest HttpRequest;
            public TcpClient TcpClient;
            public bool Cancelled;
        }

        private sealed class TabbyCompletionPayloadRequest
        {
            public string language;
            public string filepath;
            public string git_url;
            public string user;
            public string[] declarations;
            public string current_line_prefix;
            public string current_line_suffix;
            public TabbyRelevantSnippet[] relevant_snippets;
            public TabbyCompletionSegments segments;
        }

        private sealed class TabbyCompletionSegments
        {
            public string prefix;
            public string suffix;
        }

        private sealed class TabbyCompletionPayloadResponse
        {
            public string id;
            public TabbyCompletionChoice[] choices;
        }

        private sealed class TabbyCompletionChoice
        {
            public int index;
            public string text;
        }

        private sealed class TabbyRelevantSnippet
        {
            public string filepath;
            public string content;
        }

        private sealed class ParsedHttpResponse
        {
            public string StatusLine;
            public IDictionary<string, string> Headers;
            public byte[] BodyBytes;
        }

        private static string TrimToWindow(string text, int maxLength, bool keepEnd)
        {
            var value = text ?? string.Empty;
            if (value.Length <= maxLength)
            {
                return value;
            }

            return keepEnd
                ? value.Substring(value.Length - maxLength, maxLength)
                : value.Substring(0, maxLength);
        }
    }

}
