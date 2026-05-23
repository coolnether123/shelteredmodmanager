using System;
using System.Collections.Generic;
using System.Threading;

namespace ModAPI.Core
{
    /// <summary>
    /// Controls how a keyed background result is treated when newer work is submitted for the same source and key.
    /// </summary>
    public enum ModThreadStaleResultPolicy
    {
        /// <summary>Deliver every result, including results from older keyed work.</summary>
        DeliverAll = 0,
        /// <summary>Allow prior background work to finish, but do not deliver its stale continuation.</summary>
        SkipIfSuperseded = 1,
        /// <summary>Request cancellation of prior work and do not deliver its continuation.</summary>
        CancelPreviousAndSkip = 2
    }

    /// <summary>
    /// Options for a background work submission.
    /// </summary>
    public sealed class ModThreadOptions
    {
        /// <summary>
        /// Optional mod ID or neutral source label used to scope keys, throttling, and diagnostics.
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// Optional key identifying replaceable work within <see cref="SourceId"/>.
        /// </summary>
        public string WorkKey { get; set; }

        /// <summary>
        /// Determines whether newer keyed work suppresses older result continuations.
        /// </summary>
        public ModThreadStaleResultPolicy StaleResultPolicy { get; set; }

        /// <summary>
        /// Maximum dispatched background delegates for this source. Zero means unlimited.
        /// </summary>
        public int MaxConcurrentPerSource { get; set; }

        public ModThreadOptions()
        {
            StaleResultPolicy = ModThreadStaleResultPolicy.DeliverAll;
            MaxConcurrentPerSource = 0;
        }
    }

    /// <summary>
    /// Cooperative cancellation and completion state for one background work submission.
    /// </summary>
    public sealed class ModThreadHandle
    {
        private readonly object _lock = new object();
        private bool _isCancellationRequested;
        private bool _isRunning;
        private bool _isCompleted;
        private bool _wasCanceled;
        private bool _wasStale;
        private Exception _error;

        internal object WorkItemToken;

        internal ModThreadHandle(string sourceId, string workKey)
        {
            SourceId = sourceId;
            WorkKey = workKey;
        }

        /// <summary>Optional source label copied from the submission options.</summary>
        public string SourceId { get; private set; }

        /// <summary>Optional keyed-work label copied from the submission options.</summary>
        public string WorkKey { get; private set; }

        /// <summary>True once cancellation has been requested for this work.</summary>
        public bool IsCancellationRequested
        {
            get { lock (_lock) { return _isCancellationRequested; } }
        }

        /// <summary>True while the background delegate is executing.</summary>
        public bool IsRunning
        {
            get { lock (_lock) { return _isRunning; } }
        }

        /// <summary>True once no background delegate or main-thread continuation remains to execute.</summary>
        public bool IsCompleted
        {
            get { lock (_lock) { return _isCompleted; } }
        }

        /// <summary>True when cancellation prevented the work result from being delivered.</summary>
        public bool WasCanceled
        {
            get { lock (_lock) { return _wasCanceled; } }
        }

        /// <summary>True when a newer keyed submission caused this continuation to be skipped.</summary>
        public bool WasStale
        {
            get { lock (_lock) { return _wasStale; } }
        }

        /// <summary>Background or main-thread callback exception, if the submission failed.</summary>
        public Exception Error
        {
            get { lock (_lock) { return _error; } }
        }

        /// <summary>
        /// Requests cooperative cancellation. Running delegates should inspect
        /// <see cref="IsCancellationRequested"/> and return promptly.
        /// </summary>
        public void Cancel()
        {
            ModThreads.Cancel(this);
        }

        internal void RequestCancellation()
        {
            lock (_lock)
            {
                _isCancellationRequested = true;
            }
        }

        internal void MarkRunning()
        {
            lock (_lock)
            {
                _isRunning = true;
            }
        }

        internal void MarkBackgroundFinished()
        {
            lock (_lock)
            {
                _isRunning = false;
            }
        }

        internal void MarkCanceled()
        {
            lock (_lock)
            {
                _wasCanceled = true;
            }
        }

        internal void MarkStale()
        {
            lock (_lock)
            {
                _wasStale = true;
            }
        }

        internal void MarkError(Exception error)
        {
            lock (_lock)
            {
                _error = error;
            }
        }

        internal void MarkCompleted()
        {
            lock (_lock)
            {
                _isRunning = false;
                _isCompleted = true;
            }
        }
    }

    /// <summary>
    /// Current activity for a source that has dispatched or throttled work.
    /// </summary>
    public sealed class ModThreadSourceReport
    {
        public string SourceId { get; internal set; }
        public int InFlight { get; internal set; }
        public int Waiting { get; internal set; }
    }

    /// <summary>
    /// Lifetime counters and current activity for <see cref="ModThreads"/>.
    /// </summary>
    public sealed class ModThreadDiagnosticsReport
    {
        public long Queued { get; internal set; }
        public long Running { get; internal set; }
        public long Completed { get; internal set; }
        public long Canceled { get; internal set; }
        public long Failed { get; internal set; }
        public long StaleSkipped { get; internal set; }
        public long Throttled { get; internal set; }
        public int Active { get; internal set; }
        public int Waiting { get; internal set; }
        public ModThreadSourceReport[] Sources { get; internal set; }
    }

    /// <summary>
    /// Background work helpers that marshal results back to Unity's main thread.
    /// Never touch Unity objects from the background delegate; apply game changes in the main-thread callback.
    /// </summary>
    public static class ModThreads
    {
        private const string UnlabeledSource = "(unlabeled)";

        private enum WorkState
        {
            New = 0,
            WaitingForThrottle = 1,
            QueuedForWorker = 2,
            Running = 3,
            ContinuationQueued = 4,
            ContinuationRunning = 5,
            Finished = 6
        }

        private sealed class SourceState
        {
            internal string SourceId;
            internal int InFlight;
            internal readonly Queue<WorkItem> Waiting = new Queue<WorkItem>();
        }

        private sealed class WorkItem
        {
            internal ModThreadOptions Options;
            internal ModThreadHandle Handle;
            internal SourceState Source;
            internal string SourceKey;
            internal string SupersessionKey;
            internal Action<WorkItem> Execute;
            internal WorkState State;
            internal bool SlotReserved;
            internal bool Superseded;
            internal bool HasOutcome;
        }

        private static readonly object _pendingMainThreadLock = new object();
        private static readonly Queue<Action> _pendingMainThread = new Queue<Action>();
        private static readonly object _stateLock = new object();
        private static readonly Dictionary<string, SourceState> _sources =
            new Dictionary<string, SourceState>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, WorkItem> _latestKeyedWork =
            new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase);

        private static long _queued;
        private static long _running;
        private static long _completed;
        private static long _canceled;
        private static long _failed;
        private static long _staleSkipped;
        private static long _throttled;
        private static int _active;
        private static int _waiting;

        /// <summary>
        /// Runs fire-and-forget work on a background ThreadPool thread.
        /// Exceptions are caught and written to the ModAPI log.
        /// </summary>
        /// <param name="action">Background work. Must not touch UnityEngine objects.</param>
        public static void RunAsync(Action action)
        {
            if (action == null) return;
            RunAsync(delegate(ModThreadHandle handle) { action(); }, null);
        }

        /// <summary>
        /// Runs cancelable fire-and-forget work on a background ThreadPool thread.
        /// </summary>
        /// <param name="action">Background work. Must not touch UnityEngine objects.</param>
        /// <param name="options">Neutral source, key, stale-result, and throttle options.</param>
        /// <returns>A handle used for cooperative cancellation and state inspection.</returns>
        public static ModThreadHandle RunAsync(Action action, ModThreadOptions options)
        {
            if (action == null) return null;
            return RunAsync(delegate(ModThreadHandle handle) { action(); }, options);
        }

        /// <summary>
        /// Runs cancelable fire-and-forget work that can observe its cancellation handle.
        /// </summary>
        /// <param name="action">Background work. Must not touch UnityEngine objects.</param>
        /// <param name="options">Neutral source, key, stale-result, and throttle options.</param>
        /// <returns>A handle used for cooperative cancellation and state inspection.</returns>
        public static ModThreadHandle RunAsync(Action<ModThreadHandle> action, ModThreadOptions options)
        {
            if (action == null) return null;

            return Submit(options, delegate(WorkItem item)
            {
                try
                {
                    action(item.Handle);
                    CompleteActionSuccess(item);
                }
                catch (Exception ex)
                {
                    CompleteBackgroundFailure(item, ex, null);
                }
            });
        }

        /// <summary>
        /// Runs a background calculation and marshals its result back to Unity's main thread.
        /// </summary>
        /// <typeparam name="TResult">Result type produced by the background work.</typeparam>
        /// <param name="work">Background calculation. Must be Unity-object free.</param>
        /// <param name="onMainThread">Main-thread callback to consume the result.</param>
        /// <remarks>
        /// Typical usage: expensive calculations off-thread, then apply the result to game state in <paramref name="onMainThread"/>.
        /// </remarks>
        public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread)
        {
            RunAsync(work, onMainThread, (Action<Exception>)null);
        }

        /// <summary>
        /// Runs a background calculation and marshals result or error handling back to Unity's main thread.
        /// </summary>
        /// <typeparam name="TResult">Result type produced by the background work.</typeparam>
        /// <param name="work">Background calculation. Must be Unity-object free.</param>
        /// <param name="onMainThread">Main-thread callback to consume the result.</param>
        /// <param name="onError">Main-thread callback for background exceptions. If null, errors are logged.</param>
        public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError)
        {
            if (work == null) return;
            RunAsync<TResult>(delegate(ModThreadHandle handle) { return work(); }, onMainThread, onError, null);
        }

        /// <summary>
        /// Runs a cancelable background calculation and marshals its result to the main thread.
        /// </summary>
        public static ModThreadHandle RunAsync<TResult>(
            Func<TResult> work,
            Action<TResult> onMainThread,
            ModThreadOptions options)
        {
            if (work == null) return null;
            return RunAsync<TResult>(delegate(ModThreadHandle handle) { return work(); }, onMainThread, null, options);
        }

        /// <summary>
        /// Runs a cancelable background calculation and marshals result or error handling to the main thread.
        /// </summary>
        public static ModThreadHandle RunAsync<TResult>(
            Func<TResult> work,
            Action<TResult> onMainThread,
            Action<Exception> onError,
            ModThreadOptions options)
        {
            if (work == null) return null;
            return RunAsync<TResult>(delegate(ModThreadHandle handle) { return work(); }, onMainThread, onError, options);
        }

        /// <summary>
        /// Runs cancelable background work that can observe its handle and marshals its result to the main thread.
        /// </summary>
        public static ModThreadHandle RunAsync<TResult>(
            Func<ModThreadHandle, TResult> work,
            Action<TResult> onMainThread,
            ModThreadOptions options)
        {
            return RunAsync<TResult>(work, onMainThread, null, options);
        }

        /// <summary>
        /// Runs cancelable background work that can observe its handle and marshals result or error handling to the main thread.
        /// </summary>
        public static ModThreadHandle RunAsync<TResult>(
            Func<ModThreadHandle, TResult> work,
            Action<TResult> onMainThread,
            Action<Exception> onError,
            ModThreadOptions options)
        {
            if (work == null) return null;

            return Submit(options, delegate(WorkItem item)
            {
                try
                {
                    TResult result = work(item.Handle);
                    CompleteResultSuccess(item, result, onMainThread);
                }
                catch (Exception ex)
                {
                    CompleteBackgroundFailure(item, ex, onError);
                }
            });
        }

        /// <summary>
        /// Gets a thread-safe snapshot of lifetime counters and current throttling activity.
        /// </summary>
        public static ModThreadDiagnosticsReport GetDiagnostics()
        {
            lock (_stateLock)
            {
                List<ModThreadSourceReport> reports = new List<ModThreadSourceReport>();
                foreach (SourceState source in _sources.Values)
                {
                    reports.Add(new ModThreadSourceReport
                    {
                        SourceId = source.SourceId,
                        InFlight = source.InFlight,
                        Waiting = source.Waiting.Count
                    });
                }

                reports.Sort(delegate(ModThreadSourceReport left, ModThreadSourceReport right)
                {
                    return string.Compare(left.SourceId, right.SourceId, StringComparison.OrdinalIgnoreCase);
                });

                return new ModThreadDiagnosticsReport
                {
                    Queued = _queued,
                    Running = _running,
                    Completed = _completed,
                    Canceled = _canceled,
                    Failed = _failed,
                    StaleSkipped = _staleSkipped,
                    Throttled = _throttled,
                    Active = _active,
                    Waiting = _waiting,
                    Sources = reports.ToArray()
                };
            }
        }

        internal static void Cancel(ModThreadHandle handle)
        {
            if (handle == null) return;

            WorkItem item = handle.WorkItemToken as WorkItem;
            if (item == null)
            {
                handle.RequestCancellation();
                return;
            }

            List<WorkItem> dispatch = new List<WorkItem>();
            lock (_stateLock)
            {
                RequestCancellationLocked(item, dispatch);
            }

            DispatchItems(dispatch);
        }

        private static ModThreadHandle Submit(ModThreadOptions options, Action<WorkItem> execute)
        {
            ModThreadOptions snapshot = SnapshotOptions(options);
            WorkItem item = new WorkItem();
            item.Options = snapshot;
            item.SourceKey = NormalizeSourceId(snapshot.SourceId);
            item.SupersessionKey = BuildSupersessionKey(item.SourceKey, snapshot);
            item.Handle = new ModThreadHandle(snapshot.SourceId, snapshot.WorkKey);
            item.Handle.WorkItemToken = item;
            item.Execute = execute;
            item.State = WorkState.New;

            List<WorkItem> dispatch = new List<WorkItem>();
            lock (_stateLock)
            {
                _queued++;
                RegisterSupersessionLocked(item, dispatch);

                SourceState source = GetSourceLocked(item.SourceKey, snapshot.SourceId);
                item.Source = source;
                if (ShouldThrottleLocked(item))
                {
                    item.State = WorkState.WaitingForThrottle;
                    source.Waiting.Enqueue(item);
                    _waiting++;
                    _throttled++;
                }
                else
                {
                    ReserveAndDispatchLocked(item, dispatch);
                }
            }

            DispatchItems(dispatch);
            return item.Handle;
        }

        private static ModThreadOptions SnapshotOptions(ModThreadOptions options)
        {
            ModThreadOptions snapshot = new ModThreadOptions();
            if (options == null) return snapshot;
            if (options.MaxConcurrentPerSource < 0)
                throw new ArgumentOutOfRangeException("options.MaxConcurrentPerSource", "Maximum concurrent work cannot be negative.");

            snapshot.SourceId = NormalizeOptionalValue(options.SourceId);
            snapshot.WorkKey = NormalizeOptionalValue(options.WorkKey);
            snapshot.StaleResultPolicy = options.StaleResultPolicy;
            snapshot.MaxConcurrentPerSource = options.MaxConcurrentPerSource;
            return snapshot;
        }

        private static string NormalizeOptionalValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string NormalizeSourceId(string sourceId)
        {
            return string.IsNullOrEmpty(sourceId) ? UnlabeledSource : sourceId;
        }

        private static string BuildSupersessionKey(string sourceKey, ModThreadOptions options)
        {
            if (options == null || string.IsNullOrEmpty(options.WorkKey)) return null;
            if (options.StaleResultPolicy == ModThreadStaleResultPolicy.DeliverAll) return null;
            return sourceKey + "|" + options.WorkKey;
        }

        private static SourceState GetSourceLocked(string sourceKey, string sourceId)
        {
            SourceState source;
            if (!_sources.TryGetValue(sourceKey, out source))
            {
                source = new SourceState();
                source.SourceId = string.IsNullOrEmpty(sourceId) ? UnlabeledSource : sourceId;
                _sources[sourceKey] = source;
            }
            return source;
        }

        private static void RegisterSupersessionLocked(WorkItem item, List<WorkItem> dispatch)
        {
            if (string.IsNullOrEmpty(item.SupersessionKey)) return;

            WorkItem prior;
            if (_latestKeyedWork.TryGetValue(item.SupersessionKey, out prior)
                && prior != null
                && prior.State != WorkState.Finished)
            {
                prior.Superseded = true;
                if (item.Options.StaleResultPolicy == ModThreadStaleResultPolicy.CancelPreviousAndSkip)
                {
                    RequestCancellationLocked(prior, dispatch);
                }
            }

            _latestKeyedWork[item.SupersessionKey] = item;
        }

        private static bool ShouldThrottleLocked(WorkItem item)
        {
            int limit = item.Options.MaxConcurrentPerSource;
            return limit > 0 && item.Source.InFlight >= limit;
        }

        private static void ReserveAndDispatchLocked(WorkItem item, List<WorkItem> dispatch)
        {
            item.State = WorkState.QueuedForWorker;
            item.SlotReserved = true;
            item.Source.InFlight++;
            dispatch.Add(item);
        }

        private static void PromoteWaitingLocked(SourceState source, List<WorkItem> dispatch)
        {
            if (source == null) return;

            while (source.Waiting.Count > 0)
            {
                WorkItem next = source.Waiting.Peek();
                if (next.State != WorkState.WaitingForThrottle)
                {
                    source.Waiting.Dequeue();
                    continue;
                }

                if (ShouldThrottleLocked(next)) return;

                source.Waiting.Dequeue();
                _waiting--;
                ReserveAndDispatchLocked(next, dispatch);
            }
        }

        private static void RequestCancellationLocked(WorkItem item, List<WorkItem> dispatch)
        {
            if (item == null || item.State == WorkState.Finished) return;

            item.Handle.RequestCancellation();
            if (item.State != WorkState.WaitingForThrottle) return;

            RemoveWaitingLocked(item.Source, item);
            _waiting--;
            CountCanceledLocked(item);
            FinishLocked(item);
            PromoteWaitingLocked(item.Source, dispatch);
            RemoveIdleSourceLocked(item.Source);
        }

        private static void RemoveWaitingLocked(SourceState source, WorkItem target)
        {
            if (source == null || source.Waiting.Count == 0) return;

            Queue<WorkItem> retained = new Queue<WorkItem>();
            while (source.Waiting.Count > 0)
            {
                WorkItem item = source.Waiting.Dequeue();
                if (!object.ReferenceEquals(item, target))
                    retained.Enqueue(item);
            }
            while (retained.Count > 0)
            {
                source.Waiting.Enqueue(retained.Dequeue());
            }
        }

        private static void DispatchItems(List<WorkItem> dispatch)
        {
            if (dispatch == null) return;

            for (int i = 0; i < dispatch.Count; i++)
            {
                WorkItem item = dispatch[i];
                try
                {
                    bool accepted = ThreadPool.QueueUserWorkItem(new WaitCallback(RunWorker), item);
                    if (!accepted)
                    {
                        CompleteDispatchFailure(item, new InvalidOperationException("ThreadPool rejected background work."));
                    }
                }
                catch (Exception ex)
                {
                    CompleteDispatchFailure(item, ex);
                }
            }
        }

        private static void RunWorker(object state)
        {
            WorkItem item = state as WorkItem;
            if (item == null) return;

            bool run = false;
            List<WorkItem> dispatch = new List<WorkItem>();
            lock (_stateLock)
            {
                if (item.State != WorkState.QueuedForWorker) return;

                if (item.Handle.IsCancellationRequested)
                {
                    ReleaseWorkerSlotLocked(item, dispatch);
                    CountCanceledLocked(item);
                    FinishLocked(item);
                }
                else
                {
                    item.State = WorkState.Running;
                    item.Handle.MarkRunning();
                    _running++;
                    _active++;
                    run = true;
                }
            }

            DispatchItems(dispatch);
            if (!run) return;

            try
            {
                item.Execute(item);
            }
            catch (Exception ex)
            {
                CompleteBackgroundFailure(item, ex, null);
            }
        }

        private static void CompleteDispatchFailure(WorkItem item, Exception error)
        {
            bool logError = false;
            List<WorkItem> dispatch = new List<WorkItem>();
            lock (_stateLock)
            {
                if (item.State != WorkState.QueuedForWorker) return;

                ReleaseWorkerSlotLocked(item, dispatch);
                if (item.Handle.IsCancellationRequested)
                {
                    CountCanceledLocked(item);
                }
                else
                {
                    CountFailedLocked(item, error);
                    logError = true;
                }
                FinishLocked(item);
            }

            if (logError)
                MMLog.WriteError(BuildLogPrefix(item) + " Async dispatch Exception: " + error);
            DispatchItems(dispatch);
        }

        private static void CompleteActionSuccess(WorkItem item)
        {
            List<WorkItem> dispatch = new List<WorkItem>();
            lock (_stateLock)
            {
                if (item.State != WorkState.Running) return;

                ReleaseWorkerSlotLocked(item, dispatch);
                if (item.Handle.IsCancellationRequested)
                    CountCanceledLocked(item);
                else
                    CountCompletedLocked(item);
                FinishLocked(item);
            }

            DispatchItems(dispatch);
        }

        private static void CompleteResultSuccess<TResult>(
            WorkItem item,
            TResult result,
            Action<TResult> onMainThread)
        {
            bool queueContinuation = false;
            List<WorkItem> dispatch = new List<WorkItem>();
            lock (_stateLock)
            {
                if (item.State != WorkState.Running) return;

                ReleaseWorkerSlotLocked(item, dispatch);
                if (item.Handle.IsCancellationRequested)
                {
                    CountCanceledLocked(item);
                    FinishLocked(item);
                }
                else if (onMainThread == null)
                {
                    CountCompletedLocked(item);
                    FinishLocked(item);
                }
                else
                {
                    item.State = WorkState.ContinuationQueued;
                    queueContinuation = true;
                }
            }

            DispatchItems(dispatch);
            if (queueContinuation)
            {
                bool queued = QueueMainThread(delegate
                {
                    InvokeResultContinuation(item, result, onMainThread);
                });
                if (!queued)
                    CompleteUndeliveredResult(item);
            }
        }

        private static void CompleteBackgroundFailure(WorkItem item, Exception error, Action<Exception> onError)
        {
            bool logError = false;
            bool queueContinuation = false;
            List<WorkItem> dispatch = new List<WorkItem>();
            lock (_stateLock)
            {
                if (item.State != WorkState.Running) return;

                ReleaseWorkerSlotLocked(item, dispatch);
                if (item.Handle.IsCancellationRequested)
                {
                    CountCanceledLocked(item);
                    FinishLocked(item);
                }
                else
                {
                    CountFailedLocked(item, error);
                    if (onError == null)
                    {
                        logError = true;
                        FinishLocked(item);
                    }
                    else
                    {
                        item.State = WorkState.ContinuationQueued;
                        queueContinuation = true;
                    }
                }
            }

            DispatchItems(dispatch);
            if (logError)
                MMLog.WriteError(BuildLogPrefix(item) + " Async Exception: " + error);

            if (queueContinuation)
            {
                bool queued = QueueMainThread(delegate
                {
                    InvokeErrorContinuation(item, error, onError);
                });
                if (!queued)
                    CompleteUndeliveredError(item);
            }
        }

        private static void InvokeResultContinuation<TResult>(
            WorkItem item,
            TResult result,
            Action<TResult> onMainThread)
        {
            if (!TryBeginContinuation(item)) return;

            try
            {
                onMainThread(result);
                lock (_stateLock)
                {
                    if (item.State != WorkState.ContinuationRunning) return;
                    CountCompletedLocked(item);
                    FinishLocked(item);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError(BuildLogPrefix(item) + " Main-thread callback Exception: " + ex);
                lock (_stateLock)
                {
                    if (item.State != WorkState.ContinuationRunning) return;
                    CountFailedLocked(item, ex);
                    FinishLocked(item);
                }
            }
        }

        private static void InvokeErrorContinuation(
            WorkItem item,
            Exception error,
            Action<Exception> onError)
        {
            if (!TryBeginContinuation(item)) return;

            try
            {
                onError(error);
            }
            catch (Exception errEx)
            {
                MMLog.WriteError(BuildLogPrefix(item) + " Error callback Exception: " + errEx);
            }
            finally
            {
                lock (_stateLock)
                {
                    if (item.State == WorkState.ContinuationRunning)
                        FinishLocked(item);
                }
            }
        }

        private static bool TryBeginContinuation(WorkItem item)
        {
            lock (_stateLock)
            {
                if (item.State != WorkState.ContinuationQueued) return false;

                if (item.Handle.IsCancellationRequested)
                {
                    CountCanceledLocked(item);
                    FinishLocked(item);
                    return false;
                }

                if (item.Superseded)
                {
                    CountStaleSkippedLocked(item);
                    FinishLocked(item);
                    return false;
                }

                item.State = WorkState.ContinuationRunning;
                return true;
            }
        }

        private static void CompleteUndeliveredResult(WorkItem item)
        {
            lock (_stateLock)
            {
                if (item.State != WorkState.ContinuationQueued) return;
                item.Handle.RequestCancellation();
                CountCanceledLocked(item);
                FinishLocked(item);
            }
        }

        private static void CompleteUndeliveredError(WorkItem item)
        {
            lock (_stateLock)
            {
                if (item.State == WorkState.ContinuationQueued)
                    FinishLocked(item);
            }
        }

        private static void ReleaseWorkerSlotLocked(WorkItem item, List<WorkItem> dispatch)
        {
            if (item.State == WorkState.Running)
            {
                _active--;
                item.Handle.MarkBackgroundFinished();
            }

            if (!item.SlotReserved || item.Source == null) return;

            item.SlotReserved = false;
            item.Source.InFlight--;
            PromoteWaitingLocked(item.Source, dispatch);
            RemoveIdleSourceLocked(item.Source);
        }

        private static void RemoveIdleSourceLocked(SourceState source)
        {
            if (source == null || source.InFlight != 0 || source.Waiting.Count != 0) return;

            SourceState registered;
            if (_sources.TryGetValue(NormalizeSourceId(source.SourceId), out registered)
                && object.ReferenceEquals(source, registered))
            {
                _sources.Remove(NormalizeSourceId(source.SourceId));
            }
        }

        private static void CountCompletedLocked(WorkItem item)
        {
            if (item.HasOutcome) return;
            item.HasOutcome = true;
            _completed++;
        }

        private static void CountCanceledLocked(WorkItem item)
        {
            if (!item.HasOutcome)
            {
                item.HasOutcome = true;
                _canceled++;
            }
            item.Handle.MarkCanceled();
        }

        private static void CountFailedLocked(WorkItem item, Exception error)
        {
            if (!item.HasOutcome)
            {
                item.HasOutcome = true;
                _failed++;
            }
            item.Handle.MarkError(error);
        }

        private static void CountStaleSkippedLocked(WorkItem item)
        {
            _staleSkipped++;
            if (!item.HasOutcome)
                item.HasOutcome = true;
            item.Handle.MarkStale();
        }

        private static void FinishLocked(WorkItem item)
        {
            item.State = WorkState.Finished;
            item.Handle.MarkCompleted();

            if (string.IsNullOrEmpty(item.SupersessionKey)) return;

            WorkItem latest;
            if (_latestKeyedWork.TryGetValue(item.SupersessionKey, out latest)
                && object.ReferenceEquals(latest, item))
            {
                _latestKeyedWork.Remove(item.SupersessionKey);
            }
        }

        private static string BuildLogPrefix(WorkItem item)
        {
            if (item == null || item.Options == null || string.IsNullOrEmpty(item.Options.SourceId))
                return "[ModThreads]";
            return "[ModThreads:" + item.Options.SourceId + "]";
        }

        private static bool QueueMainThread(Action action)
        {
            if (action == null) return false;
            if (PluginRunner.IsQuitting) return false;

            PluginRunner runner = PluginRunner.Instance;
            if (runner != null)
            {
                runner.Enqueue(action);
                return true;
            }

            lock (_pendingMainThreadLock)
            {
                _pendingMainThread.Enqueue(action);
            }
            return true;
        }

        /// <summary>
        /// Flushes callbacks completed before <see cref="PluginRunner"/> became available.
        /// Must be called on Unity's main thread.
        /// </summary>
        internal static void FlushPendingMainThreadCallbacks()
        {
            if (PluginRunner.IsQuitting) return;
            if (PluginRunner.MainThreadId != 0 && !PluginRunner.IsMainThread)
            {
                MMLog.WarnOnce("ModThreads.FlushPendingMainThreadCallbacks.NonMainThread",
                    "FlushPendingMainThreadCallbacks called from non-main thread; callbacks were not executed.");
                return;
            }

            while (true)
            {
                Action next = null;
                lock (_pendingMainThreadLock)
                {
                    if (_pendingMainThread.Count == 0) break;
                    next = _pendingMainThread.Dequeue();
                }

                try { next(); }
                catch (Exception ex) { MMLog.WriteError("[ModThreads] Pending callback Exception: " + ex); }
            }
        }
    }
}
