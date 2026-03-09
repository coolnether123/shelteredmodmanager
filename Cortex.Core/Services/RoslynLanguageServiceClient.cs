using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;

namespace Cortex.Core.Services
{
    public sealed class RoslynLanguageServiceClient : ILanguageServiceClient
    {
        private readonly string _workerPath;
        private readonly int _timeoutMs;
        private readonly object _sync = new object();
        private readonly Queue<LanguageServiceEnvelope> _responses = new Queue<LanguageServiceEnvelope>();
        private readonly AutoResetEvent _responseArrived = new AutoResetEvent(false);
        private Process _process;
        private Thread _readerThread;
        private string _lastError;
        private int _nextRequestId;

        public RoslynLanguageServiceClient(string workerPath, int timeoutMs)
        {
            _workerPath = workerPath ?? string.Empty;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : 15000;
        }

        public bool IsEnabled
        {
            get { return !string.IsNullOrEmpty(_workerPath); }
        }

        public bool IsRunning
        {
            get { return _process != null && !_process.HasExited; }
        }

        public string LastError
        {
            get { return _lastError ?? string.Empty; }
        }

        public LanguageServiceInitializeResponse Initialize(LanguageServiceInitializeRequest request)
        {
            return SendRequest<LanguageServiceInitializeRequest, LanguageServiceInitializeResponse>(LanguageServiceCommands.Initialize, request);
        }

        public LanguageServiceStatusResponse GetStatus()
        {
            return SendRequest<LanguageServiceStatusRequest, LanguageServiceStatusResponse>(LanguageServiceCommands.Status, new LanguageServiceStatusRequest());
        }

        public LanguageServiceAnalysisResponse AnalyzeDocument(LanguageServiceDocumentRequest request)
        {
            return SendRequest<LanguageServiceDocumentRequest, LanguageServiceAnalysisResponse>(LanguageServiceCommands.AnalyzeDocument, request);
        }

        public LanguageServiceHoverResponse GetHover(LanguageServiceHoverRequest request)
        {
            return SendRequest<LanguageServiceHoverRequest, LanguageServiceHoverResponse>(LanguageServiceCommands.Hover, request);
        }

        public LanguageServiceDefinitionResponse GoToDefinition(LanguageServiceDefinitionRequest request)
        {
            return SendRequest<LanguageServiceDefinitionRequest, LanguageServiceDefinitionResponse>(LanguageServiceCommands.GoToDefinition, request);
        }

        public void Shutdown()
        {
            lock (_sync)
            {
                try
                {
                    if (IsRunning)
                    {
                        SendRequest<object, LanguageServiceOperationResponse>(LanguageServiceCommands.Shutdown, new object());
                    }
                }
                catch
                {
                }

                DisposeProcess();
            }
        }

        public void Dispose()
        {
            Shutdown();
            _responseArrived.Close();
        }

        private TResponse SendRequest<TRequest, TResponse>(string command, TRequest payload)
            where TResponse : LanguageServiceOperationResponse, new()
        {
            var failure = new TResponse
            {
                Success = false,
                StatusMessage = string.Empty
            };

            lock (_sync)
            {
                if (!IsEnabled)
                {
                    failure.StatusMessage = "Roslyn language service is not configured.";
                    return failure;
                }

                try
                {
                    EnsureProcessStarted();
                    var envelope = new LanguageServiceEnvelope
                    {
                        RequestId = "req-" + Interlocked.Increment(ref _nextRequestId),
                        Command = command ?? string.Empty,
                        Success = true,
                        PayloadJson = ManualJson.Serialize(payload),
                        ErrorMessage = string.Empty
                    };

                    _process.StandardInput.WriteLine(ManualJson.Serialize(envelope));
                    _process.StandardInput.Flush();

                    var response = WaitForResponse(envelope.RequestId);
                    if (response == null)
                    {
                        failure.StatusMessage = string.IsNullOrEmpty(_lastError)
                            ? "Roslyn worker did not respond."
                            : _lastError;
                        return failure;
                    }

                    if (!response.Success)
                    {
                        failure.StatusMessage = string.IsNullOrEmpty(response.ErrorMessage)
                            ? "Roslyn worker returned an error."
                            : response.ErrorMessage;
                        _lastError = failure.StatusMessage;
                        return failure;
                    }

                    if (string.IsNullOrEmpty(response.PayloadJson))
                    {
                        return new TResponse
                        {
                            Success = true,
                            StatusMessage = string.Empty
                        };
                    }

                    var typed = ManualJson.Deserialize<TResponse>(response.PayloadJson);
                    if (typed == null)
                    {
                        failure.StatusMessage = "Roslyn worker returned an unreadable payload.";
                        return failure;
                    }

                    return typed;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    failure.StatusMessage = ex.Message;
                    DisposeProcess();
                    return failure;
                }
            }
        }

        private void EnsureProcessStarted()
        {
            if (IsRunning)
            {
                return;
            }

            DisposeProcess();

            if (!File.Exists(_workerPath))
            {
                throw new FileNotFoundException("Roslyn worker was not found.", _workerPath);
            }

            _process = new Process();
            _process.StartInfo = BuildStartInfo(_workerPath);
            if (!_process.Start())
            {
                throw new InvalidOperationException("Failed to start Roslyn worker.");
            }

            _process.ErrorDataReceived += OnProcessErrorDataReceived;
            _process.BeginErrorReadLine();

            _readerThread = new Thread(ReadOutputLoop);
            _readerThread.Name = "Cortex.RoslynLanguageServiceClient";
            _readerThread.IsBackground = true;
            _readerThread.Start();
            _lastError = string.Empty;
        }

        private static ProcessStartInfo BuildStartInfo(string workerPath)
        {
            var startInfo = new ProcessStartInfo();
            var extension = Path.GetExtension(workerPath) ?? string.Empty;
            if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = "dotnet";
                startInfo.Arguments = "\"" + workerPath + "\"";
            }
            else
            {
                startInfo.FileName = workerPath;
                startInfo.Arguments = string.Empty;
            }

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = Path.GetDirectoryName(workerPath) ?? Environment.CurrentDirectory;
            return startInfo;
        }

        private void ReadOutputLoop()
        {
            try
            {
                while (_process != null && !_process.HasExited)
                {
                    var line = _process.StandardOutput.ReadLine();
                    if (string.IsNullOrEmpty(line))
                    {
                        if (_process.HasExited)
                        {
                            break;
                        }

                        continue;
                    }

                    var envelope = ManualJson.Deserialize<LanguageServiceEnvelope>(line);
                    if (envelope == null)
                    {
                        _lastError = "Roslyn worker produced an unreadable response.";
                        continue;
                    }

                    lock (_responses)
                    {
                        _responses.Enqueue(envelope);
                    }

                    _responseArrived.Set();
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _responseArrived.Set();
            }
        }

        private LanguageServiceEnvelope WaitForResponse(string requestId)
        {
            var timeoutAt = DateTime.UtcNow.AddMilliseconds(_timeoutMs);
            while (DateTime.UtcNow <= timeoutAt)
            {
                LanguageServiceEnvelope envelope = null;
                lock (_responses)
                {
                    if (_responses.Count > 0)
                    {
                        envelope = _responses.Dequeue();
                    }
                }

                if (envelope != null)
                {
                    if (string.Equals(envelope.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
                    {
                        return envelope;
                    }

                    continue;
                }

                if (_process == null || _process.HasExited)
                {
                    return null;
                }

                var remaining = timeoutAt - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                _responseArrived.WaitOne(remaining);
            }

            _lastError = "Roslyn worker timed out after " + _timeoutMs + " ms.";
            return null;
        }

        private void DisposeProcess()
        {
            try
            {
                if (_process != null)
                {
                    if (!_process.HasExited)
                    {
                        try
                        {
                            _process.StandardInput.Close();
                        }
                        catch
                        {
                        }

                        _process.Kill();
                    }

                    _process.Close();
                }
            }
            catch
            {
            }
            finally
            {
                _process = null;
                _readerThread = null;
                lock (_responses)
                {
                    _responses.Clear();
                }
            }
        }

        private void OnProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _lastError = e.Data;
            }
        }
    }
}
