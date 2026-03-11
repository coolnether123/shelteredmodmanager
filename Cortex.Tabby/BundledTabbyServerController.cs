using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using Cortex.Core.Models;

namespace Cortex.Tabby
{
    internal sealed class BundledTabbyServerController : IDisposable
    {
        private const string DefaultBaseUrl = "http://127.0.0.1:5118";
        private readonly Action<string> _log;
        private Process _process;
        private bool _ownsProcess;
        private bool _serverReportedListening;
        private string _lastLogLine = string.Empty;
        private string _lastHealthFailure = string.Empty;

        public BundledTabbyServerController(Action<string> log)
        {
            _log = log;
            LastError = string.Empty;
        }

        public string LastError { get; private set; }

        public bool TryResolveCompletionEndpoint(CortexSettings settings, out string endpoint)
        {
            var effectiveTimeoutMs = TabbyRuntimeSettings.GetEffectiveTimeoutMs(settings);
            endpoint = NormalizeCompletionEndpoint(settings != null ? settings.TabbyServerUrl : string.Empty);
            if (!string.IsNullOrEmpty(endpoint))
            {
                return true;
            }

            if (settings == null || string.IsNullOrEmpty(settings.OllamaModel))
            {
                LastError = "Bundled Tabby server requires an Ollama model when TabbyServerUrl is not set.";
                return false;
            }

            var baseUrl = DefaultBaseUrl;
            endpoint = NormalizeCompletionEndpoint(baseUrl);
            if (IsHealthy(BuildHealthUrl(baseUrl)))
            {
                Log("Bundled Tabby server already running at " + baseUrl + ".");
                return true;
            }

            var serverPath = ResolveServerPath();
            if (string.IsNullOrEmpty(serverPath))
            {
                LastError = "Could not locate Cortex.Tabby.Server under the bundled tabby runtime folder.";
                return false;
            }

            try
            {
                Log("Starting bundled Tabby server. BaseUrl=" + baseUrl +
                    ", OllamaUrl=" + ((settings != null ? settings.OllamaServerUrl : string.Empty) ?? string.Empty) +
                    ", OllamaModel=" + ((settings != null ? settings.OllamaModel : string.Empty) ?? string.Empty) +
                    ", TimeoutMs=" + effectiveTimeoutMs + ".");
                _process = new Process();
                _process.StartInfo = BuildStartInfo(serverPath, BuildArguments(settings, baseUrl));
                _process.EnableRaisingEvents = true;
                _process.OutputDataReceived += OnProcessOutput;
                _process.ErrorDataReceived += OnProcessOutput;
                _serverReportedListening = false;
                if (!_process.Start())
                {
                    LastError = "Bundled Tabby server process did not start.";
                    return false;
                }

                _ownsProcess = true;
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                if (!WaitForHealthy(BuildHealthUrl(baseUrl), 5000))
                {
                    LastError = "Bundled Tabby server did not become healthy. HealthFailure=" +
                        (_lastHealthFailure ?? string.Empty) +
                        ". LastServerLog=" + (_lastLogLine ?? string.Empty);
                    Dispose();
                    return false;
                }

                Log("Bundled Tabby server started. Pid=" + _process.Id + ", Url=" + baseUrl + ".");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message ?? "Failed to start bundled Tabby server.";
                Dispose();
                return false;
            }
        }

        public void Dispose()
        {
            if (_process != null)
            {
                try
                {
                    if (_ownsProcess && !_process.HasExited)
                    {
                        _process.Kill();
                        _process.WaitForExit(2000);
                    }
                }
                catch
                {
                }

                _process.Dispose();
                _process = null;
            }

            _ownsProcess = false;
            _serverReportedListening = false;
        }

        private void OnProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            _lastLogLine = e.Data;
            if (!_serverReportedListening &&
                e.Data.IndexOf("Now listening on:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _serverReportedListening = true;
            }
            Log("[Cortex.Tabby.Server] " + e.Data);
        }

        private static ProcessStartInfo BuildStartInfo(string serverPath, string arguments)
        {
            var startInfo = new ProcessStartInfo();
            var extension = Path.GetExtension(serverPath) ?? string.Empty;
            if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = ResolveDotnetHostPath();
                startInfo.Arguments = "\"" + serverPath + "\" " + arguments;
            }
            else
            {
                startInfo.FileName = serverPath;
                startInfo.Arguments = arguments;
            }

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = Path.GetDirectoryName(serverPath) ?? Environment.CurrentDirectory;
            return startInfo;
        }

        private static string BuildArguments(CortexSettings settings, string baseUrl)
        {
            var effectiveTimeoutMs = TabbyRuntimeSettings.GetEffectiveTimeoutMs(settings);
            return "--urls \"" + baseUrl + "\"" +
                " --ollama-url \"" + ((settings != null ? settings.OllamaServerUrl : string.Empty) ?? string.Empty) + "\"" +
                " --ollama-model \"" + ((settings != null ? settings.OllamaModel : string.Empty) ?? string.Empty) + "\"" +
                " --ollama-api-token \"" + ((settings != null ? settings.OllamaApiToken : string.Empty) ?? string.Empty) + "\"" +
                " --request-timeout-ms \"" + effectiveTimeoutMs + "\"";
        }

        private static string ResolveServerPath()
        {
            try
            {
                var assemblyPath = typeof(BundledTabbyServerController).Assembly.Location;
                var assemblyDir = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
                var candidates = new[]
                {
                    Path.GetFullPath(Path.Combine(Path.Combine(assemblyDir, @"..\tabby"), "Cortex.Tabby.Server.exe")),
                    Path.GetFullPath(Path.Combine(Path.Combine(assemblyDir, @"..\tabby"), "Cortex.Tabby.Server.dll"))
                };

                for (var i = 0; i < candidates.Length; i++)
                {
                    if (File.Exists(candidates[i]))
                    {
                        return candidates[i];
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private bool WaitForHealthy(string healthUrl, int timeoutMs)
        {
            _lastHealthFailure = string.Empty;
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, timeoutMs));
            var attempts = 0;
            while (DateTime.UtcNow < deadline)
            {
                attempts++;
                if (_serverReportedListening)
                {
                    Log("Bundled Tabby server reported listening after " + attempts + " readiness attempt(s).");
                    return true;
                }

                if (IsHealthy(healthUrl))
                {
                    if (attempts > 1)
                    {
                        Log("Bundled Tabby server health probe succeeded after " + attempts + " attempt(s).");
                    }
                    return true;
                }

                Thread.Sleep(200);
            }

            if (_serverReportedListening)
            {
                Log("Bundled Tabby server readiness falling back to server-reported listening state after health probe timeouts.");
                return true;
            }

            Log("Bundled Tabby server health probe failed after " + attempts + " attempt(s). LastFailure=" + (_lastHealthFailure ?? string.Empty) + ".");
            return false;
        }

        private bool IsHealthy(string healthUrl)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(healthUrl);
                request.Method = "GET";
                request.Timeout = 1000;
                request.ReadWriteTimeout = 1000;
                request.Proxy = null;
                request.KeepAlive = false;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    _lastHealthFailure = "HTTP " + (int)response.StatusCode + " " + response.StatusCode + ".";
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException ex)
            {
                var status = ex.Status.ToString();
                var message = ex.Message ?? string.Empty;
                var response = ex.Response as HttpWebResponse;
                _lastHealthFailure = response != null
                    ? "WebException " + status + ", HTTP " + (int)response.StatusCode + " " + response.StatusCode + ", Message=" + message
                    : "WebException " + status + ", Message=" + message;
                return false;
            }
            catch (Exception ex)
            {
                _lastHealthFailure = ex.GetType().FullName + ": " + (ex.Message ?? string.Empty);
                return false;
            }
        }

        private static string BuildHealthUrl(string baseUrl)
        {
            return NormalizeBaseUrl(baseUrl) + "/health";
        }

        private static string NormalizeCompletionEndpoint(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            if (normalized.EndsWith("/api/completion", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return NormalizeBaseUrl(normalized) + "/api/completion";
        }

        private static string NormalizeBaseUrl(string value)
        {
            return (value ?? string.Empty).Trim().TrimEnd('/');
        }

        private static string ResolveDotnetHostPath()
        {
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrEmpty(dotnetRoot))
            {
                var rootCandidate = Path.Combine(dotnetRoot, "dotnet.exe");
                if (File.Exists(rootCandidate))
                {
                    return rootCandidate;
                }
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                var candidate = Path.Combine(Path.Combine(programFiles, "dotnet"), "dotnet.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                var candidate = Path.Combine(Path.Combine(programFilesX86, "dotnet"), "dotnet.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "dotnet";
        }

        private void Log(string message)
        {
            if (_log != null && !string.IsNullOrEmpty(message))
            {
                _log(message);
            }
        }
    }
}
