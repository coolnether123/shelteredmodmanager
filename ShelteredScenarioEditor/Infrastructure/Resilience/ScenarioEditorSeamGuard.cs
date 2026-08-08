using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;

namespace ShelteredScenarioEditor.Infrastructure.Resilience
{
    internal enum ScenarioEditorSeamRecoveryPolicy
    {
        RetryOnce,
        DisableSeamAndDegrade,
        RestoreState
    }

    /// <summary>
    /// Contains failure recovery and health reporting for editor-only reflection seams.
    /// Runtime API seams remain owned by ShelteredAPI.
    /// </summary>
    internal static class ScenarioEditorSeamGuard
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, SeamHealth> Health =
            new Dictionary<string, SeamHealth>(StringComparer.OrdinalIgnoreCase);

        internal static bool Run(
            string name,
            ScenarioEditorSeamRecoveryPolicy policy,
            Action action,
            string playerMessage,
            Action recovery,
            out string message)
        {
            object ignored;
            return Try(
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

        internal static bool Try<T>(
            string name,
            ScenarioEditorSeamRecoveryPolicy policy,
            Func<T> action,
            T fallback,
            string playerMessage,
            Action recovery,
            out T value,
            out string message)
        {
            string seamName = string.IsNullOrEmpty(name) ? "unnamed-editor-seam" : name;
            string degradation = string.IsNullOrEmpty(playerMessage)
                ? seamName + " unavailable - scenario editor still usable."
                : playerMessage;
            SeamHealth health;
            lock (Sync)
            {
                if (!Health.TryGetValue(seamName, out health))
                {
                    health = new SeamHealth { Name = seamName };
                    Health.Add(seamName, health);
                }

                if (health.Disabled)
                {
                    value = fallback;
                    message = degradation;
                    return false;
                }
            }

            Exception error;
            if (TryInvoke(action, out value, out error))
            {
                MarkSuccess(health);
                message = null;
                return true;
            }

            MarkFailure(health, error, degradation);
            if (policy == ScenarioEditorSeamRecoveryPolicy.RetryOnce)
            {
                if (TryInvoke(action, out value, out error))
                {
                    MarkSuccess(health);
                    message = null;
                    return true;
                }
                MarkFailure(health, error, degradation);
            }

            if (policy == ScenarioEditorSeamRecoveryPolicy.RestoreState && recovery != null)
            {
                try
                {
                    recovery();
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioEditorSeamGuard] Recovery failed for '" + seamName + "': " + ex + ".");
                }
            }
            else if (policy == ScenarioEditorSeamRecoveryPolicy.DisableSeamAndDegrade)
            {
                lock (Sync)
                    health.Disabled = true;
            }

            value = fallback;
            message = degradation;
            return false;
        }

        internal static string BuildSystemHealthLine()
        {
            lock (Sync)
            {
                foreach (SeamHealth health in Health.Values)
                {
                    if (health.Disabled || health.Failures > 0 && health.LastFailureTicks >= health.LastSuccessTicks)
                        return "System Health: " + health.PlayerMessage;
                }
            }
            return null;
        }

        internal static void ResetForDiagnostics()
        {
            lock (Sync)
                Health.Clear();
        }

        private static bool TryInvoke<T>(Func<T> action, out T value, out Exception error)
        {
            try
            {
                value = action != null ? action() : default(T);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                TargetInvocationException invocation = ex as TargetInvocationException;
                error = invocation != null && invocation.InnerException != null ? invocation.InnerException : ex;
                value = default(T);
                return false;
            }
        }

        private static void MarkSuccess(SeamHealth health)
        {
            lock (Sync)
                health.LastSuccessTicks = DateTime.UtcNow.Ticks;
        }

        private static void MarkFailure(SeamHealth health, Exception error, string playerMessage)
        {
            lock (Sync)
            {
                health.Failures++;
                health.LastFailureTicks = DateTime.UtcNow.Ticks;
                health.PlayerMessage = playerMessage;
            }
            MMLog.WriteWarning("[ScenarioEditorSeamGuard] Seam '" + health.Name + "' failed: "
                + (error != null ? error.ToString() : "unknown") + ".");
        }

        private sealed class SeamHealth
        {
            internal string Name;
            internal int Failures;
            internal long LastFailureTicks;
            internal long LastSuccessTicks;
            internal bool Disabled;
            internal string PlayerMessage;
        }
    }
}
