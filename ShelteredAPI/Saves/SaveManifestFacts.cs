using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Util;

namespace ShelteredAPI.Saves
{
    /// <summary>
    /// Collects only concrete, persisted save compatibility facts for slot manifests.
    /// </summary>
    internal static class SaveManifestFacts
    {
        internal const int CurrentManifestVersion = 2;

        internal static SlotManifest CaptureCurrent(SaveInfo info)
        {
            List<LoadedModInfo> currentMods = new List<LoadedModInfo>();
            foreach (ModEntry mod in ModRuntime.GetLoadedModsSnapshot())
            {
                if (mod == null)
                    continue;

                string warning = mod.About != null ? mod.About.missingModWarning : null;
                currentMods.Add(new LoadedModInfo
                {
                    modId = mod.Id,
                    version = mod.Version,
                    requiredModApiVersion = ResolveRequiredVersion(
                        mod.About != null ? mod.About.requiredModApiVersion : null,
                        mod.About != null ? mod.About.modApiVersion : null),
                    requiredShelteredApiVersion = ResolveRequiredVersion(
                        mod.About != null ? mod.About.requiredShelteredApiVersion : null,
                        mod.About != null ? mod.About.shelteredApiVersion : null),
                    warnings = string.IsNullOrEmpty(warning) ? new string[0] : new[] { warning }
                });
            }

            SlotManifest manifest = new SlotManifest
            {
                manifestVersion = CurrentManifestVersion,
                lastModified = DateTime.UtcNow.ToString("o"),
                family_name = info != null ? info.familyName : "Unknown",
                modApiVersion = RuntimeCompat.ModApiVersion,
                shelteredApiVersion = GetShelteredApiVersion(),
                mapFactsStatus = info != null && info.hasMapSizeMetadata ? "captured" : "unknown",
                hasMapSize = info != null && info.hasMapSizeMetadata,
                mapSize = info != null ? info.mapSize : 0,
                queueFactsStatus = "unavailable",
                queueSummary = "No queue diagnostic snapshot was registered when this save was written.",
                restoreFactsStatus = "unknown",
                lastLoadedMods = currentMods.ToArray()
            };

            ApplyRuntimeMapFacts(manifest);
            return manifest;
        }

        internal static void ApplyStorageIdentityFacts(SlotManifest manifest, string scenarioId, int absoluteSlot)
        {
            if (manifest == null)
                return;

            string scopeId = NormalizeScopeId(scenarioId);
            manifest.manifestVersion = Math.Max(manifest.manifestVersion, CurrentManifestVersion);
            manifest.saveScopeId = scopeId;
            manifest.saveId = ResolveSaveId(scopeId, absoluteSlot);
            manifest.customScenarioId = ScenarioSaveIdGuards.IsReservedStorageId(scopeId) ? null : scopeId;

            if (string.IsNullOrEmpty(manifest.modApiVersion))
                manifest.modApiVersion = RuntimeCompat.ModApiVersion;
            if (string.IsNullOrEmpty(manifest.shelteredApiVersion))
                manifest.shelteredApiVersion = GetShelteredApiVersion();
            if (string.IsNullOrEmpty(manifest.mapFactsStatus))
                manifest.mapFactsStatus = "unknown";
            if (string.IsNullOrEmpty(manifest.queueFactsStatus))
                manifest.queueFactsStatus = "unavailable";
            if (string.IsNullOrEmpty(manifest.queueSummary))
                manifest.queueSummary = "No queue diagnostic snapshot was registered when this save was written.";

            if (string.IsNullOrEmpty(manifest.runtimeMapFactsStatus)
                || string.Equals(manifest.runtimeMapFactsStatus, "unavailable", StringComparison.OrdinalIgnoreCase))
            {
                ApplyRuntimeMapFacts(manifest);
            }

            ApplyRestoreFacts(manifest, scopeId, absoluteSlot);
        }

        internal static string GetShelteredApiVersion()
        {
            Version version = typeof(SlotManifest).Assembly.GetName().Version;
            return version == null
                ? "unknown"
                : string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }

        internal static SlotManifest CaptureRuntimeMapFacts()
        {
            SlotManifest facts = new SlotManifest();
            ApplyRuntimeMapFacts(facts);
            return facts;
        }

        private static string ResolveRequiredVersion(string required, string legacy)
        {
            return !string.IsNullOrEmpty(required) ? required : legacy;
        }

        private static void ApplyRuntimeMapFacts(SlotManifest manifest)
        {
            if (manifest == null)
                return;

            manifest.runtimeMapFactsStatus = "unavailable";
            try
            {
                Type facade = typeof(SlotManifest).Assembly.GetType("ShelteredAPI.Map.ShelteredMap", false);
                MethodInfo method = facade != null
                    ? facade.GetMethod("GetCurrentContext", BindingFlags.Public | BindingFlags.Static)
                    : null;
                object context = method != null ? method.Invoke(null, null) : null;
                if (context == null)
                    return;

                bool isAvailable = ReadBoolProperty(context, "IsAvailable", false);
                if (!isAvailable)
                {
                    manifest.runtimeMapFactsStatus = "unavailable";
                    return;
                }

                manifest.runtimeMapFactsStatus = ReadBoolProperty(context, "IsValid", false) ? "available" : "unknown";
                manifest.runtimeMapWidth = ReadIntProperty(context, "CurrentWidth", 0);
                manifest.runtimeMapHeight = ReadIntProperty(context, "CurrentHeight", 0);
                if (ReadBoolProperty(context, "HasScaleFactor", false))
                {
                    object scale = ReadProperty(context, "ScaleFactor");
                    if (scale != null)
                        manifest.runtimeMapScaleFactor = Convert.ToString(scale, CultureInfo.InvariantCulture);
                }

                manifest.hasMapSeed = ReadBoolProperty(context, "HasMapSeed", false);
                manifest.mapSeed = ReadIntProperty(context, "MapSeed", 0);
            }
            catch
            {
                manifest.runtimeMapFactsStatus = "unknown";
            }
        }

        private static object ReadProperty(object source, string name)
        {
            PropertyInfo property = source != null
                ? source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                : null;
            return property != null ? property.GetValue(source, null) : null;
        }

        private static bool ReadBoolProperty(object source, string name, bool fallback)
        {
            object value = ReadProperty(source, name);
            return value is bool ? (bool)value : fallback;
        }

        private static int ReadIntProperty(object source, string name, int fallback)
        {
            object value = ReadProperty(source, name);
            return value is int ? (int)value : fallback;
        }

        private static string NormalizeScopeId(string scenarioId)
        {
            return string.IsNullOrEmpty(scenarioId)
                ? ScenarioSaveIdGuards.StandardStorageScenarioId
                : scenarioId;
        }

        private static string ResolveSaveId(string scopeId, int absoluteSlot)
        {
            if (string.Equals(scopeId, ScenarioSaveIdGuards.StandardStorageScenarioId, StringComparison.OrdinalIgnoreCase)
                && absoluteSlot >= 1
                && absoluteSlot <= 3)
            {
                return "Slot" + absoluteSlot;
            }

            if (string.Equals(scopeId, ScenarioSaveIdGuards.VanillaSurroundedStorageScenarioId, StringComparison.OrdinalIgnoreCase)
                && absoluteSlot == 1)
            {
                return ScenarioSaveIdGuards.VanillaSurroundedSaveId;
            }

            if (string.Equals(scopeId, ScenarioSaveIdGuards.VanillaStasisStorageScenarioId, StringComparison.OrdinalIgnoreCase)
                && absoluteSlot == 1)
            {
                return ScenarioSaveIdGuards.VanillaStasisSaveId;
            }

            return scopeId + "_" + absoluteSlot;
        }

        private static void ApplyRestoreFacts(SlotManifest manifest, string scopeId, int absoluteSlot)
        {
            manifest.restoreFactsStatus = "not-recorded";
            manifest.restoreLineageId = null;

            if (absoluteSlot <= 0)
            {
                manifest.restoreFactsStatus = "unknown";
                return;
            }

            try
            {
                string identityPath = Path.Combine(
                    DirectoryProvider.SlotRoot(scopeId, absoluteSlot, false),
                    "backup.identity.json");
                if (!File.Exists(identityPath))
                    return;

                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(identityPath), out root, out error))
                {
                    manifest.restoreFactsStatus = "unknown";
                    return;
                }

                string lineageId = root.GetString("lineageId", string.Empty);
                if (!string.IsNullOrEmpty(lineageId))
                {
                    manifest.restoreFactsStatus = "backup-lineage-recorded";
                    manifest.restoreLineageId = lineageId;
                }
            }
            catch
            {
                manifest.restoreFactsStatus = "unknown";
            }
        }
    }
}
