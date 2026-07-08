using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;

namespace ShelteredAPI.Infrastructure
{
    internal enum SeamRecoveryPolicy
    {
        RetryOnce,
        DisableSeamAndDegrade,
        RestoreState
    }

    internal sealed class SeamHealthSnapshot
    {
        public string Name { get; set; }
        public string Policy { get; set; }
        public bool LastSuccess { get; set; }
        public int FailureCount { get; set; }
        public string LastError { get; set; }
        public bool Degraded { get; set; }
        public bool Disabled { get; set; }
        public string LastPlayerMessage { get; set; }
    }

    internal static class SeamGuard
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, SeamHealthRecord> Records = new Dictionary<string, SeamHealthRecord>(StringComparer.OrdinalIgnoreCase);
        private static string _lastPlayerMessage;

        public static bool Run(
            string name,
            SeamRecoveryPolicy policy,
            Action action,
            string playerMessage,
            Action recovery,
            out string message)
        {
            object ignored;
            return Try<object>(
                name,
                policy,
                delegate
                {
                    if (action != null)
                        action();
                    return null;
                },
                null,
                playerMessage,
                recovery,
                out ignored,
                out message);
        }

        public static bool Try<T>(
            string name,
            SeamRecoveryPolicy policy,
            Func<T> action,
            T fallback,
            string playerMessage,
            Action recovery,
            out T value,
            out string message)
        {
            string seamName = NormalizeName(name);
            string degradation = NormalizeMessage(playerMessage, seamName);
            value = fallback;
            message = null;

            SeamHealthRecord record = GetOrCreate(seamName, policy);
            if (record.Disabled)
            {
                message = string.IsNullOrEmpty(record.LastPlayerMessage) ? degradation : record.LastPlayerMessage;
                SetPlayerMessage(message);
                return false;
            }

            Exception firstError;
            if (TryExecute(action, out value, out firstError))
            {
                MarkSuccess(record);
                return true;
            }

            MarkFailure(record, policy, firstError, degradation);
            if (policy == SeamRecoveryPolicy.RetryOnce)
            {
                Exception retryError;
                if (TryExecute(action, out value, out retryError))
                {
                    MarkSuccess(record);
                    return true;
                }

                MarkFailure(record, policy, retryError, degradation);
            }

            if (policy == SeamRecoveryPolicy.RestoreState && recovery != null)
                TryRecover(record, recovery);
            if (policy == SeamRecoveryPolicy.DisableSeamAndDegrade)
                record.Disabled = true;

            message = degradation;
            SetPlayerMessage(message);
            return false;
        }

        public static void ResetForTests()
        {
            lock (Sync)
            {
                Records.Clear();
                _lastPlayerMessage = null;
            }
        }

        public static SeamHealthSnapshot[] GetHealthSnapshots()
        {
            lock (Sync)
            {
                SeamHealthSnapshot[] snapshots = new SeamHealthSnapshot[Records.Count];
                int index = 0;
                foreach (SeamHealthRecord record in Records.Values)
                    snapshots[index++] = record.ToSnapshot();
                return snapshots;
            }
        }

        public static bool HasDegradedSeams()
        {
            lock (Sync)
            {
                foreach (SeamHealthRecord record in Records.Values)
                {
                    if (record.IsDegraded)
                        return true;
                }
                return false;
            }
        }

        public static string BuildSystemHealthLine()
        {
            lock (Sync)
            {
                foreach (SeamHealthRecord record in Records.Values)
                {
                    if (record.IsDegraded)
                        return "System Health: " + (string.IsNullOrEmpty(record.LastPlayerMessage) ? record.Name + " degraded." : record.LastPlayerMessage);
                }
            }

            return null;
        }

        public static string LastPlayerMessage
        {
            get
            {
                lock (Sync)
                    return _lastPlayerMessage;
            }
        }

        private static bool TryExecute<T>(Func<T> action, out T value, out Exception error)
        {
            value = default(T);
            error = null;
            try
            {
                if (action != null)
                    value = action();
                return true;
            }
            catch (Exception ex)
            {
                error = Unwrap(ex);
                return false;
            }
        }

        private static void TryRecover(SeamHealthRecord record, Action recovery)
        {
            try
            {
                recovery();
                record.RecoveryFired = true;
            }
            catch (Exception ex)
            {
                Exception unwrapped = Unwrap(ex);
                record.LastError = (record.LastError ?? string.Empty) + " Recovery failed: " + unwrapped.Message;
                MMLog.WriteWarning("[SeamGuard] Recovery failed for seam '" + record.Name + "': " + unwrapped + ".");
            }
        }

        private static void MarkSuccess(SeamHealthRecord record)
        {
            lock (Sync)
            {
                record.LastSuccess = true;
                record.LastSuccessTicks = DateTime.UtcNow.Ticks;
                record.LastError = null;
            }
        }

        private static void MarkFailure(SeamHealthRecord record, SeamRecoveryPolicy policy, Exception error, string playerMessage)
        {
            lock (Sync)
            {
                record.Policy = policy;
                record.LastSuccess = false;
                record.FailureCount++;
                record.LastFailureTicks = DateTime.UtcNow.Ticks;
                record.LastError = error != null ? error.Message : "Unknown seam failure.";
                record.LastPlayerMessage = playerMessage;
            }

            MMLog.WriteWarning("[SeamGuard] Seam '" + record.Name + "' failed under policy " + policy + ": " + (error != null ? error.ToString() : "unknown") + ".");
        }

        private static SeamHealthRecord GetOrCreate(string name, SeamRecoveryPolicy policy)
        {
            lock (Sync)
            {
                SeamHealthRecord record;
                if (!Records.TryGetValue(name, out record))
                {
                    record = new SeamHealthRecord();
                    record.Name = name;
                    record.Policy = policy;
                    Records.Add(name, record);
                }
                else
                {
                    record.Policy = policy;
                }

                return record;
            }
        }

        private static void SetPlayerMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            lock (Sync)
                _lastPlayerMessage = message;
        }

        private static string NormalizeName(string name)
        {
            return string.IsNullOrEmpty(name) ? "unnamed-seam" : name;
        }

        private static string NormalizeMessage(string message, string name)
        {
            if (!string.IsNullOrEmpty(message))
                return message;

            return name + " unavailable - scenario still playable.";
        }

        private static Exception Unwrap(Exception ex)
        {
            TargetInvocationException invocation = ex as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : ex;
        }

        private sealed class SeamHealthRecord
        {
            public string Name;
            public SeamRecoveryPolicy Policy;
            public bool LastSuccess;
            public long LastSuccessTicks;
            public long LastFailureTicks;
            public int FailureCount;
            public string LastError;
            public bool Disabled;
            public bool RecoveryFired;
            public string LastPlayerMessage;

            public bool IsDegraded
            {
                get { return Disabled || (FailureCount > 0 && LastFailureTicks >= LastSuccessTicks); }
            }

            public SeamHealthSnapshot ToSnapshot()
            {
                return new SeamHealthSnapshot
                {
                    Name = Name,
                    Policy = Policy.ToString(),
                    LastSuccess = LastSuccess,
                    FailureCount = FailureCount,
                    LastError = LastError,
                    Degraded = IsDegraded,
                    Disabled = Disabled,
                    LastPlayerMessage = LastPlayerMessage
                };
            }
        }
    }
}
