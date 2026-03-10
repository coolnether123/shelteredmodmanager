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
        private const int MinimumInitializeTimeoutMs = 120000;
        private readonly string _workerPath;
        private readonly int _timeoutMs;
        private readonly Action<string> _log;
        private readonly object _sync = new object();
        private readonly Queue<PendingRequest> _outgoing = new Queue<PendingRequest>();
        private readonly Queue<LanguageServiceEnvelope> _completed = new Queue<LanguageServiceEnvelope>();
        private readonly Dictionary<string, PendingRequest> _pendingById = new Dictionary<string, PendingRequest>(StringComparer.OrdinalIgnoreCase);
        private readonly AutoResetEvent _outgoingSignal = new AutoResetEvent(false);
        private Process _process;
        private Thread _readerThread;
        private Thread _writerThread;
        private string _lastError;
        private int _nextRequestId;
        private bool _shutdownRequested;

        public RoslynLanguageServiceClient(string workerPath, int timeoutMs, Action<string> log)
        {
            _workerPath = workerPath ?? string.Empty;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : 15000;
            _log = log;
        }

        public bool IsEnabled
        {
            get { return !string.IsNullOrEmpty(_workerPath); }
        }

        public bool IsRunning
        {
            get
            {
                lock (_sync)
                {
                    return _process != null && !_process.HasExited;
                }
            }
        }

        public string LastError
        {
            get { return _lastError ?? string.Empty; }
        }

        public string QueueInitialize(LanguageServiceInitializeRequest request)
        {
            return QueueRequest(LanguageServiceCommands.Initialize, request, GetInitializeTimeoutMs());
        }

        public string QueueStatus()
        {
            return QueueRequest(LanguageServiceCommands.Status, new LanguageServiceStatusRequest(), _timeoutMs);
        }

        public string QueueAnalyzeDocument(LanguageServiceDocumentRequest request)
        {
            return QueueRequest(LanguageServiceCommands.AnalyzeDocument, request, _timeoutMs);
        }

        public string QueueHover(LanguageServiceHoverRequest request)
        {
            return QueueRequest(LanguageServiceCommands.Hover, request, _timeoutMs);
        }

        public string QueueGoToDefinition(LanguageServiceDefinitionRequest request)
        {
            return QueueRequest(LanguageServiceCommands.GoToDefinition, request, _timeoutMs);
        }

        public bool TryDequeueResponse(out LanguageServiceEnvelope envelope)
        {
            lock (_sync)
            {
                PumpTimeouts_NoLock();
                if (_completed.Count == 0)
                {
                    envelope = null;
                    return false;
                }

                envelope = _completed.Dequeue();
                return true;
            }
        }

        public LanguageServiceInitializeResponse Initialize(LanguageServiceInitializeRequest request)
        {
            return SendRequest<LanguageServiceInitializeResponse>(LanguageServiceCommands.Initialize, request);
        }

        public LanguageServiceStatusResponse GetStatus()
        {
            return SendRequest<LanguageServiceStatusResponse>(LanguageServiceCommands.Status, new LanguageServiceStatusRequest());
        }

        public LanguageServiceAnalysisResponse AnalyzeDocument(LanguageServiceDocumentRequest request)
        {
            return SendRequest<LanguageServiceAnalysisResponse>(LanguageServiceCommands.AnalyzeDocument, request);
        }

        public LanguageServiceHoverResponse GetHover(LanguageServiceHoverRequest request)
        {
            return SendRequest<LanguageServiceHoverResponse>(LanguageServiceCommands.Hover, request);
        }

        public LanguageServiceDefinitionResponse GoToDefinition(LanguageServiceDefinitionRequest request)
        {
            return SendRequest<LanguageServiceDefinitionResponse>(LanguageServiceCommands.GoToDefinition, request);
        }

        public void Shutdown()
        {
            lock (_sync)
            {
                _shutdownRequested = true;
                _outgoingSignal.Set();
                DisposeProcess_NoLock();
            }
        }

        public void Dispose()
        {
            Shutdown();
            _outgoingSignal.Close();
        }

        private string QueueRequest<TRequest>(string command, TRequest payload, int timeoutMs)
        {
            lock (_sync)
            {
                if (!IsEnabled)
                {
                    _lastError = "Roslyn language service is not configured.";
                    return string.Empty;
                }

                EnsureProcessStarted_NoLock();
                var pending = CreatePendingRequest(command, payload, false, timeoutMs);
                _outgoing.Enqueue(pending);
                _pendingById[pending.Envelope.RequestId] = pending;
                Log("Queued request. Command=" + (command ?? string.Empty) +
                    ", RequestId=" + pending.Envelope.RequestId +
                    ", TimeoutMs=" + pending.TimeoutMs +
                    ", ProcessRunning=" + (_process != null && !_process.HasExited) + ".");
                _outgoingSignal.Set();
                return pending.Envelope.RequestId;
            }
        }

        private TResponse SendRequest<TResponse>(string command, object payload)
            where TResponse : LanguageServiceOperationResponse, new()
        {
            PendingRequest pending;
            lock (_sync)
            {
                if (!IsEnabled)
                {
                    return new TResponse
                    {
                        Success = false,
                        StatusMessage = "Roslyn language service is not configured."
                    };
                }

                EnsureProcessStarted_NoLock();
                pending = CreatePendingRequest(command, payload, true, _timeoutMs);
                _outgoing.Enqueue(pending);
                _pendingById[pending.Envelope.RequestId] = pending;
                _outgoingSignal.Set();
            }

            var timeoutAt = DateTime.UtcNow.AddMilliseconds(_timeoutMs);
            while (DateTime.UtcNow <= timeoutAt)
            {
                if (pending.WaitHandle != null && pending.WaitHandle.WaitOne(50, false))
                {
                    break;
                }

                lock (_sync)
                {
                    PumpTimeouts_NoLock();
                    if (_process == null || _process.HasExited)
                    {
                        break;
                    }
                }
            }

            var response = pending.Response;
            if (response == null)
            {
                return new TResponse
                {
                    Success = false,
                    StatusMessage = string.IsNullOrEmpty(_lastError)
                        ? "Roslyn worker did not respond."
                        : _lastError
                };
            }

            if (!response.Success)
            {
                return new TResponse
                {
                    Success = false,
                    StatusMessage = string.IsNullOrEmpty(response.ErrorMessage)
                        ? "Roslyn worker returned an error."
                        : response.ErrorMessage
                };
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
            return typed ?? new TResponse
            {
                Success = false,
                StatusMessage = "Roslyn worker returned an unreadable payload."
            };
        }

        private PendingRequest CreatePendingRequest<TRequest>(string command, TRequest payload, bool waitForCompletion, int timeoutMs)
        {
            return new PendingRequest
            {
                Envelope = new LanguageServiceEnvelope
                {
                    RequestId = "req-" + Interlocked.Increment(ref _nextRequestId),
                    Command = command ?? string.Empty,
                    Success = true,
                    PayloadJson = ManualJson.Serialize(payload),
                    ErrorMessage = string.Empty
                },
                QueuedUtc = DateTime.UtcNow,
                TimeoutMs = timeoutMs > 0 ? timeoutMs : _timeoutMs,
                WaitHandle = waitForCompletion ? new ManualResetEvent(false) : null
            };
        }

        private int GetInitializeTimeoutMs()
        {
            return Math.Max(_timeoutMs, MinimumInitializeTimeoutMs);
        }

        private void EnsureProcessStarted_NoLock()
        {
            if (_process != null && !_process.HasExited)
            {
                return;
            }

            DisposeProcess_NoLock();
            _shutdownRequested = false;

            if (!File.Exists(_workerPath))
            {
                throw new FileNotFoundException("Roslyn worker was not found.", _workerPath);
            }

            _process = new Process();
            _process.StartInfo = BuildStartInfo(_workerPath);
            _process.EnableRaisingEvents = true;
            _process.Exited += OnProcessExited;
            Log("Starting worker process. FileName=" + _process.StartInfo.FileName +
                ", Arguments=" + _process.StartInfo.Arguments +
                ", WorkingDirectory=" + _process.StartInfo.WorkingDirectory + ".");
            if (!_process.Start())
            {
                throw new InvalidOperationException("Failed to start Roslyn worker.");
            }
            Log("Worker process started. Pid=" + _process.Id + ".");

            _process.ErrorDataReceived += OnProcessErrorDataReceived;
            _process.BeginErrorReadLine();

            _readerThread = new Thread(ReadOutputLoop);
            _readerThread.Name = "Cortex.RoslynLanguageServiceClient.Reader";
            _readerThread.IsBackground = true;
            _readerThread.Start();
            Log("Worker reader thread started.");

            _writerThread = new Thread(WriteInputLoop);
            _writerThread.Name = "Cortex.RoslynLanguageServiceClient.Writer";
            _writerThread.IsBackground = true;
            _writerThread.Start();
            Log("Worker writer thread started.");
            _lastError = string.Empty;
        }

        private static ProcessStartInfo BuildStartInfo(string workerPath)
        {
            var startInfo = new ProcessStartInfo();
            var extension = Path.GetExtension(workerPath) ?? string.Empty;
            if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = ResolveDotnetHostPath();
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

        private void WriteInputLoop()
        {
            while (true)
            {
                PendingRequest pending = null;
                lock (_sync)
                {
                    if (_shutdownRequested)
                    {
                        return;
                    }

                    PumpTimeouts_NoLock();
                    if (_outgoing.Count > 0)
                    {
                        pending = _outgoing.Dequeue();
                    }
                }

                if (pending == null)
                {
                    _outgoingSignal.WaitOne(50, false);
                    continue;
                }

                try
                {
                    lock (_sync)
                    {
                        if (_process == null || _process.HasExited)
                        {
                            FailPending_NoLock(pending.Envelope.RequestId, "Roslyn worker is not running.");
                            continue;
                        }

                        Log("Writing request to worker. Command=" +
                            (pending.Envelope != null ? pending.Envelope.Command ?? string.Empty : string.Empty) +
                            ", RequestId=" + (pending.Envelope != null ? pending.Envelope.RequestId ?? string.Empty : string.Empty) + ".");
                        _process.StandardInput.WriteLine(ManualJson.Serialize(pending.Envelope));
                        _process.StandardInput.Flush();
                        Log("Request write complete. RequestId=" +
                            (pending.Envelope != null ? pending.Envelope.RequestId ?? string.Empty : string.Empty) + ".");
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        _lastError = ex.Message;
                        Log("Request write failed. RequestId=" +
                            (pending.Envelope != null ? pending.Envelope.RequestId ?? string.Empty : string.Empty) +
                            ", Error=" + ex.Message + ".");
                        FailPending_NoLock(pending.Envelope.RequestId, ex.Message);
                        DisposeProcess_NoLock();
                    }
                }
            }
        }

        private void ReadOutputLoop()
        {
            try
            {
                while (true)
                {
                    Process process;
                    lock (_sync)
                    {
                        process = _process;
                        if (_shutdownRequested || process == null || process.HasExited)
                        {
                            return;
                        }
                    }

                    var line = process.StandardOutput.ReadLine();
                    if (line == null)
                    {
                        lock (_sync)
                        {
                            if (_process == null || _process.HasExited)
                            {
                                if (string.IsNullOrEmpty(_lastError))
                                {
                                    _lastError = "Roslyn worker exited before responding.";
                                }

                                DisposeProcess_NoLock();
                                return;
                            }
                        }

                        continue;
                    }

                    if (line.Length == 0)
                    {
                        lock (_sync)
                        {
                            if (_process == null || _process.HasExited)
                            {
                                if (string.IsNullOrEmpty(_lastError))
                                {
                                    _lastError = "Roslyn worker exited before responding.";
                                }

                                DisposeProcess_NoLock();
                                return;
                            }
                        }

                        continue;
                    }

                    var envelope = ManualJson.Deserialize<LanguageServiceEnvelope>(line);
                    lock (_sync)
                    {
                        if (envelope == null)
                        {
                            _lastError = "Roslyn worker produced an unreadable response.";
                            Log("Worker produced unreadable response line.");
                            continue;
                        }

                        Log("Received worker response. Command=" + (envelope.Command ?? string.Empty) +
                            ", RequestId=" + (envelope.RequestId ?? string.Empty) +
                            ", Success=" + envelope.Success +
                            ", Error=" + (envelope.ErrorMessage ?? string.Empty) + ".");

                        if (string.IsNullOrEmpty(envelope.RequestId))
                        {
                            _lastError = !string.IsNullOrEmpty(envelope.ErrorMessage)
                                ? envelope.ErrorMessage
                                : "Roslyn worker returned an uncorrelated error response.";
                            Log("Worker returned uncorrelated failure. RawError=" + (_lastError ?? string.Empty) + ".");
                            FailAllPending_NoLock(_lastError);
                            continue;
                        }

                        PendingRequest pending;
                        if (_pendingById.TryGetValue(envelope.RequestId ?? string.Empty, out pending))
                        {
                            _pendingById.Remove(envelope.RequestId ?? string.Empty);
                            pending.Response = envelope;
                            if (pending.WaitHandle != null)
                            {
                                pending.WaitHandle.Set();
                            }
                        }

                        _completed.Enqueue(envelope);
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _lastError = ex.Message;
                    Log("Reader loop crashed. Error=" + ex.Message + ".");
                    FailAllPending_NoLock(ex.Message);
                }
            }
        }

        private void PumpTimeouts_NoLock()
        {
            if (_pendingById.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var expiredIds = new List<string>();
            foreach (var pair in _pendingById)
            {
                if ((now - pair.Value.QueuedUtc).TotalMilliseconds > pair.Value.TimeoutMs)
                {
                    expiredIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < expiredIds.Count; i++)
            {
                var requestId = expiredIds[i];
                PendingRequest pending;
                var timeoutMs = _timeoutMs;
                if (_pendingById.TryGetValue(requestId, out pending) && pending != null && pending.TimeoutMs > 0)
                {
                    timeoutMs = pending.TimeoutMs;
                }

                _lastError = "Roslyn worker timed out after " + timeoutMs + " ms.";
                Log("Request timed out. RequestId=" + requestId + ", TimeoutMs=" + timeoutMs + ".");
                FailPending_NoLock(requestId, _lastError);
            }
        }

        private void FailPending_NoLock(string requestId, string message)
        {
            PendingRequest pending;
            if (!_pendingById.TryGetValue(requestId ?? string.Empty, out pending))
            {
                return;
            }

            _pendingById.Remove(requestId ?? string.Empty);
            var envelope = new LanguageServiceEnvelope
            {
                RequestId = requestId ?? string.Empty,
                Command = pending.Envelope != null ? pending.Envelope.Command : string.Empty,
                Success = false,
                PayloadJson = string.Empty,
                ErrorMessage = message ?? "Roslyn worker failed."
            };
            pending.Response = envelope;
            if (pending.WaitHandle != null)
            {
                pending.WaitHandle.Set();
            }

            Log("Completing pending request with failure. Command=" +
                (pending.Envelope != null ? pending.Envelope.Command ?? string.Empty : string.Empty) +
                ", RequestId=" + (requestId ?? string.Empty) +
                ", Error=" + (message ?? string.Empty) + ".");
            _completed.Enqueue(envelope);
        }

        private void FailAllPending_NoLock(string message)
        {
            var pendingIds = new List<string>(_pendingById.Keys);
            for (var i = 0; i < pendingIds.Count; i++)
            {
                FailPending_NoLock(pendingIds[i], message);
            }
        }

        private void DisposeProcess_NoLock()
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
                Log("Disposing worker process. HadProcess=" + (_process != null) +
                    ", LastError=" + (_lastError ?? string.Empty) + ".");
                _process = null;
                _readerThread = null;
                _writerThread = null;
                FailAllPending_NoLock(string.IsNullOrEmpty(_lastError) ? "Roslyn worker stopped." : _lastError);
                _outgoing.Clear();
            }
        }

        private void OnProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _lastError = e.Data;
                Log("Worker stderr: " + e.Data);
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            lock (_sync)
            {
                if (_shutdownRequested || _process == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(_lastError))
                {
                    _lastError = "Roslyn worker exited before responding.";
                }

                var exitCode = 0;
                try
                {
                    exitCode = _process.ExitCode;
                }
                catch
                {
                }

                Log("Worker process exited. ExitCode=" + exitCode + ", LastError=" + (_lastError ?? string.Empty) + ".");
                DisposeProcess_NoLock();
            }
        }

        private void Log(string message)
        {
            if (_log == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                _log("[Cortex.Roslyn.Client] " + message);
            }
            catch
            {
            }
        }

        private sealed class PendingRequest
        {
            public LanguageServiceEnvelope Envelope;
            public DateTime QueuedUtc;
            public int TimeoutMs;
            public ManualResetEvent WaitHandle;
            public LanguageServiceEnvelope Response;
        }
    }
}
