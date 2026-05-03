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
                    MMLog.WriteDebug("[DeferredPatchCoordinator] Already applied " + timing
                        + " for " + SafeAssemblyName(source.Assembly)
                        + " trigger=" + (trigger ?? string.Empty) + ".");
                    return;
                }

                AppliedGroups.Add(groupKey);
            }

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                MMLog.WriteInfo("[DeferredPatchCoordinator] Applying " + timing
                    + " patches for " + SafeAssemblyName(source.Assembly)
                    + " trigger=" + (trigger ?? string.Empty) + ".");

                PatchRegistryOptions timingOptions = PatchRegistry.CreateTimingOptions(source.Options, timing);
                PatchRegistry.ApplyAssembly(source.Harmony, source.Assembly, timingOptions);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[DeferredPatchCoordinator] Failed applying " + timing
                    + " for " + SafeAssemblyName(source.Assembly) + ": " + ex.Message);
            }
            finally
            {
                LogStartupTiming("Deferred Harmony patch " + SafeAssemblyName(source.Assembly) + " " + timing, timer);
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
