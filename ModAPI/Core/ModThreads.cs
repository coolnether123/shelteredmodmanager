using System;
using System.Collections.Generic;
using System.Threading;

namespace ModAPI.Core
{
    public static class ModThreads
    {
        private static readonly object _pendingMainThreadLock = new object();
        private static readonly Queue<Action> _pendingMainThread = new Queue<Action>();

        /// <summary>
        /// Runs a fire-and-forget action on a background ThreadPool thread.
        /// </summary>
        /// <param name="action">Background work. Must not touch UnityEngine objects.</param>
        public static void RunAsync(Action action)
        {
            if (action == null) return;
            ThreadPool.QueueUserWorkItem(state => {
                try {
                    action();
                } catch (Exception ex) {
                    MMLog.WriteError("[ModThreads] Async Exception: " + ex);
                }
            });
        }

        /// <summary>
        /// v1.3: Runs a background calculation and marshals its result back to Unity's main thread.
        /// </summary>
        /// <typeparam name="TResult">Result type produced by the background work.</typeparam>
        /// <param name="work">Background calculation. Must be Unity-object free.</param>
        /// <param name="onMainThread">Main-thread callback to consume the result.</param>
        /// <remarks>
        /// Typical usage: expensive calculations off-thread, then apply the result to game state in <paramref name="onMainThread"/>.
        /// </remarks>
        public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread)
        {
            RunAsync(work, onMainThread, null);
        }

        /// <summary>
        /// v1.3: Runs a background calculation and marshals result/error back to Unity's main thread.
        /// </summary>
        /// <typeparam name="TResult">Result type produced by the background work.</typeparam>
        /// <param name="work">Background calculation. Must be Unity-object free.</param>
        /// <param name="onMainThread">Main-thread callback to consume the result.</param>
        /// <param name="onError">Main-thread callback for background exceptions. If null, errors are logged.</param>
        public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError)
        {
            if (work == null) return;

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    var result = work();
                    if (onMainThread != null)
                    {
                        QueueMainThread(delegate
                        {
                            try { onMainThread(result); }
                            catch (Exception cbEx) { MMLog.WriteError("[ModThreads] Main-thread callback Exception: " + cbEx); }
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (onError != null)
                    {
                        QueueMainThread(delegate
                        {
                            try { onError(ex); }
                            catch (Exception errEx) { MMLog.WriteError("[ModThreads] Error callback Exception: " + errEx); }
                        });
                    }
                    else
                    {
                        MMLog.WriteError("[ModThreads] Async Exception: " + ex);
                    }
                }
            });
        }

        private static void QueueMainThread(Action action)
        {
            if (action == null) return;
            if (PluginRunner.IsQuitting) return;

            var runner = PluginRunner.Instance;
            if (runner != null)
            {
                runner.Enqueue(action);
                return;
            }

            lock (_pendingMainThreadLock)
            {
                _pendingMainThread.Enqueue(action);
            }
        }

        /// <summary>
        /// v1.3: Flushes callbacks completed before PluginRunner became available.
        /// Must be called on Unity main thread.
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
