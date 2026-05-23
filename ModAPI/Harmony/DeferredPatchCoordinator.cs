using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Applies governed Harmony patch timing groups exactly once after their safe runtime trigger fires.
    /// </summary>
    public static class DeferredPatchCoordinator
    {
        private static readonly object Sync = new object();
        private static readonly List<PatchSource> Sources = new List<PatchSource>();
        private static readonly HashSet<string> AppliedGroups = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> ApplyingGroups = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> LastFailures = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Registers an assembly that may contain deferred governed patch hosts.
        /// </summary>
        public static void RegisterSource(HarmonyLib.Harmony harmony, Assembly assembly, PatchRegistryOptions options)
        {
            if (harmony == null || assembly == null)
                return;

            lock (Sync)
            {
                string key = SourceKey(assembly);
                for (int i = 0; i < Sources.Count; i++)
                {
                    if (string.Equals(Sources[i].Key, key, StringComparison.Ordinal))
                    {
                        Sources[i].Harmony = harmony;
                        Sources[i].Options = options;
                        return;
                    }
                }

                Sources.Add(new PatchSource
                {
                    Key = key,
                    Harmony = harmony,
                    Assembly = assembly,
                    Options = options
                });
            }
        }

        /// <summary>
        /// Applies all registered assembly patch hosts in the requested timing group once.
        /// </summary>
        public static void Apply(PatchStartupTiming timing, string trigger)
        {
            PatchSource[] sources;
            lock (Sync)
            {
                sources = Sources.ToArray();
            }

            for (int i = 0; i < sources.Length; i++)
                ApplySource(sources[i], timing, trigger);
        }

        private static void ApplySource(PatchSource source, PatchStartupTiming timing, string trigger)
        {
            if (source == null || source.Harmony == null || source.Assembly == null)
                return;

            string groupKey = source.Key + "|" + timing;
            lock (Sync)
            {
                if (AppliedGroups.Contains(groupKey))
                {
                    MMLog.WriteDebug("Already applied " + timing
                        + " for " + SafeAssemblyName(source.Assembly)
                        + " trigger=" + (trigger ?? string.Empty) + ".");
                    return;
                }

                if (ApplyingGroups.Contains(groupKey))
                {
                    MMLog.WriteDebug("Deferred patch group already applying " + timing
                        + " for " + SafeAssemblyName(source.Assembly)
                        + " trigger=" + (trigger ?? string.Empty) + ".");
                    return;
                }

                string previousFailure;
                if (LastFailures.TryGetValue(groupKey, out previousFailure) && !string.IsNullOrEmpty(previousFailure))
                {
                    MMLog.WriteDebug("Retrying deferred patch group after previous failure " + timing
                        + " for " + SafeAssemblyName(source.Assembly)
                        + " trigger=" + (trigger ?? string.Empty) + ".");
                }

                ApplyingGroups.Add(groupKey);
            }

            Stopwatch timer = Stopwatch.StartNew();
            bool applied = false;
            try
            {
                MMLog.WriteInfo("Applying " + timing
                    + " patches for " + SafeAssemblyName(source.Assembly)
                    + " trigger=" + (trigger ?? string.Empty) + ".");

                PatchRegistryOptions timingOptions = PatchRegistry.CreateTimingOptions(source.Options, timing);
                timingOptions.TriggerName = trigger ?? string.Empty;
                PatchRegistry.ApplyAssembly(source.Harmony, source.Assembly, timingOptions);
                applied = true;
                lock (Sync)
                {
                    AppliedGroups.Add(groupKey);
                    LastFailures.Remove(groupKey);
                }
            }
            catch (Exception ex)
            {
                lock (Sync)
                {
                    LastFailures[groupKey] = ex.ToString();
                }
                MMLog.WriteWarning("Failed applying " + timing
                    + " for " + SafeAssemblyName(source.Assembly) + ": " + ex.Message
                    + ". The group remains retryable on a later trigger.");
            }
            finally
            {
                lock (Sync)
                {
                    ApplyingGroups.Remove(groupKey);
                }

                LogStartupTiming("Deferred Harmony patch " + SafeAssemblyName(source.Assembly) + " " + timing, timer);
                if (applied)
                {
                    MMLog.WriteInfo("Deferred Harmony patch group applied: " + timing
                        + " for " + SafeAssemblyName(source.Assembly) + ".");
                }
            }
        }

        private static void LogStartupTiming(string phaseName, Stopwatch timer)
        {
            if (timer == null)
                return;

            timer.Stop();
            MMLog.WriteWithSource(
                MMLog.LogLevel.Info,
                MMLog.LogCategory.General,
                "StartupTiming",
                phaseName + " took " + timer.ElapsedMilliseconds + "ms.");
        }

        private static string SourceKey(Assembly assembly)
        {
            try { return assembly != null ? assembly.FullName : "<null>"; }
            catch { return "<unknown>"; }
        }

        private static string SafeAssemblyName(Assembly assembly)
        {
            try { return assembly != null ? assembly.GetName().Name : "<null>"; }
            catch { return "<unknown>"; }
        }

        private sealed class PatchSource
        {
            public string Key;
            public HarmonyLib.Harmony Harmony;
            public Assembly Assembly;
            public PatchRegistryOptions Options;
        }
    }
}
