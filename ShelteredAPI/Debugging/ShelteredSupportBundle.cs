using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Util;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Debugging
{
    /// <summary>
    /// Selects an optional save manifest and log limit for support-bundle capture.
    /// </summary>
    [Serializable]
    public sealed class SupportBundleRequest
    {
        public string saveScopeId;
        public string saveId;
        public int absoluteSlot;
        public int maxLogEntries = 200;
    }

    /// <summary>
    /// One structured diagnostics section in a support-bundle snapshot.
    /// </summary>
    [Serializable]
    public sealed class SupportBundleSection
    {
        public string id;
        public string status;
        public string[] facts = new string[0];
    }

    /// <summary>
    /// In-memory report captured for a player support request.
    /// </summary>
    [Serializable]
    public sealed class SupportBundleSnapshot
    {
        public int bundleVersion = 1;
        public string capturedAtUtc;
        public string gameVersion;
        public string unityVersion;
        public string architecture;
        public string modApiVersion;
        public string shelteredApiVersion;
        public LoadedModInfo[] activeMods = new LoadedModInfo[0];
        public SlotManifest saveManifest;
        public SupportBundleSection[] diagnostics = new SupportBundleSection[0];
        public string[] logs = new string[0];
    }

    /// <summary>
    /// Captures diagnostic facts for support without requiring optional runtime services.
    /// </summary>
    public static class ShelteredSupportBundle
    {
        public static SupportBundleSnapshot Capture()
        {
            return Capture(null);
        }

        public static SupportBundleSnapshot Capture(SupportBundleRequest request)
        {
            SupportBundleSnapshot snapshot = new SupportBundleSnapshot
            {
                capturedAtUtc = DateTime.UtcNow.ToString("o"),
                gameVersion = RuntimeCompat.GameVersion,
                unityVersion = RuntimeCompat.UnityVersion,
                architecture = RuntimeCompat.Architecture,
                modApiVersion = RuntimeCompat.ModApiVersion,
                shelteredApiVersion = SaveManifestFacts.GetShelteredApiVersion(),
                activeMods = SaveManifestFacts.CaptureCurrent(null).lastLoadedMods
            };

            List<SupportBundleSection> sections = new List<SupportBundleSection>();
            sections.Add(CaptureApiVersionSection(snapshot));
            snapshot.saveManifest = CaptureSaveSection(request, sections);
            sections.Add(CaptureMapSection(snapshot.saveManifest));
            sections.Add(CaptureQueueSection(snapshot.saveManifest));
            sections.Add(CaptureRestoreSection(snapshot.saveManifest));
            sections.Add(CaptureUnityLogSuppressionSection());
            sections.Add(CapturePatchReportSection());
            sections.Add(CaptureRandomSection());
            sections.Add(CaptureBackgroundWorkSection());

            snapshot.diagnostics = sections.ToArray();
            snapshot.logs = CaptureLogs(request != null ? request.maxLogEntries : 200);
            return snapshot;
        }

        public static string ExportJson()
        {
            return ExportJson(null);
        }

        public static string ExportJson(SupportBundleRequest request)
        {
            return Serialize(Capture(request));
        }

        private static SupportBundleSection CaptureApiVersionSection(SupportBundleSnapshot snapshot)
        {
            return Available("api-versions",
                "modApiVersion=" + ValueOrUnknown(snapshot.modApiVersion),
                "shelteredApiVersion=" + ValueOrUnknown(snapshot.shelteredApiVersion),
                "gameVersion=" + ValueOrUnknown(snapshot.gameVersion),
                "unityVersion=" + ValueOrUnknown(snapshot.unityVersion),
                "architecture=" + ValueOrUnknown(snapshot.architecture));
        }

        private static SlotManifest CaptureSaveSection(SupportBundleRequest request, List<SupportBundleSection> sections)
        {
            List<string> facts = new List<string>();
            string scopeId = request != null ? request.saveScopeId : null;
            string saveId = request != null ? request.saveId : null;
            int absoluteSlot = request != null ? request.absoluteSlot : 0;
            string runtimeStatus = "unavailable";

            try
            {
                if (ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.SaveRuntime))
                {
                    ISaveRuntimeAdapter adapter;
                    if (GameRuntimeApis.TryGetSaveRuntime(out adapter) && adapter != null)
                    {
                        runtimeStatus = "available";
                        facts.Add("heartbeat=" + ValueOrUnknown(adapter.GetQuitHeartbeatDetail()));
                        IModSaveContext context = adapter.GetCurrentSaveContext();
                        if (context != null)
                        {
                            facts.Add("activeScopeId=" + ValueOrUnknown(context.SaveScopeId));
                            facts.Add("activeSaveId=" + ValueOrUnknown(context.SaveId));
                            facts.Add("activeSlot=" + context.SlotIndex);
                            if (string.IsNullOrEmpty(scopeId))
                                scopeId = context.SaveScopeId;
                            if (string.IsNullOrEmpty(saveId))
                                saveId = context.SaveId;
                            if (absoluteSlot <= 0)
                                absoluteSlot = context.SlotIndex;
                        }
                        else
                        {
                            facts.Add("activeSaveContext=unknown");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                runtimeStatus = "unknown";
                facts.Add("runtimeProbeError=" + ex.GetType().Name);
            }

            facts.Add("selectedScopeId=" + ValueOrUnknown(scopeId));
            facts.Add("selectedSaveId=" + ValueOrUnknown(saveId));
            facts.Add("selectedSlot=" + (absoluteSlot > 0 ? absoluteSlot.ToString() : "unknown"));

            SlotManifest manifest = null;
            if (!string.IsNullOrEmpty(scopeId) && absoluteSlot > 0)
            {
                manifest = SaveRegistryCore.ReadSlotManifest(scopeId, absoluteSlot);
                facts.Add("manifest=" + (manifest != null ? "available" : "unknown"));
            }
            else
            {
                facts.Add("manifest=unknown");
            }

            string sectionStatus = manifest != null ? "available" : runtimeStatus;
            sections.Add(new SupportBundleSection { id = "save", status = sectionStatus, facts = facts.ToArray() });
            return manifest;
        }

        private static SupportBundleSection CaptureMapSection(SlotManifest manifest)
        {
            List<string> facts = new List<string>();
            bool hasFacts = false;
            if (manifest != null && manifest.hasMapSize)
            {
                facts.Add("savedMapSize=" + manifest.mapSize);
                facts.Add("savedMapSource=save-manifest");
                hasFacts = true;
            }
            else
            {
                facts.Add("savedMapSize=unknown");
            }

            SlotManifest runtimeFacts = SaveManifestFacts.CaptureRuntimeMapFacts();
            facts.Add("runtimeStatus=" + ValueOrUnknown(runtimeFacts.runtimeMapFactsStatus));
            if (string.Equals(runtimeFacts.runtimeMapFactsStatus, "available", StringComparison.OrdinalIgnoreCase))
            {
                facts.Add("runtimeWidth=" + runtimeFacts.runtimeMapWidth);
                facts.Add("runtimeHeight=" + runtimeFacts.runtimeMapHeight);
                facts.Add("runtimeScaleFactor=" + ValueOrUnknown(runtimeFacts.runtimeMapScaleFactor));
                facts.Add("runtimeMapSeed=" + (runtimeFacts.hasMapSeed ? runtimeFacts.mapSeed.ToString() : "unknown"));
                hasFacts = true;
            }

            return new SupportBundleSection
            {
                id = "map",
                status = hasFacts ? "available" : ValueOrUnknown(runtimeFacts.runtimeMapFactsStatus),
                facts = facts.ToArray()
            };
        }

        private static SupportBundleSection CaptureQueueSection(SlotManifest manifest)
        {
            string status = manifest != null ? manifest.queueFactsStatus : "unavailable";
            string summary = manifest != null ? manifest.queueSummary : null;
            List<string> facts = new List<string>();
            facts.Add(string.IsNullOrEmpty(summary) ? "No save-wide queue diagnostic snapshot is available." : summary);
            Type queueFacade = typeof(ShelteredSupportBundle).Assembly.GetType("ShelteredAPI.Queues.ShelteredQueues", false);
            facts.Add("ownerScopedSnapshotApi=" + (queueFacade != null ? "available" : "unavailable"));
            return new SupportBundleSection { id = "queue", status = ValueOrUnknown(status), facts = facts.ToArray() };
        }

        private static SupportBundleSection CaptureRestoreSection(SlotManifest manifest)
        {
            if (manifest == null)
                return Unknown("restore", "No selected manifest is available for restore-lineage inspection.");

            List<string> facts = new List<string>();
            facts.Add("status=" + ValueOrUnknown(manifest.restoreFactsStatus));
            if (!string.IsNullOrEmpty(manifest.restoreLineageId))
                facts.Add("lineageId=" + manifest.restoreLineageId);

            string status = string.Equals(manifest.restoreFactsStatus, "backup-lineage-recorded", StringComparison.OrdinalIgnoreCase)
                ? "available"
                : ValueOrUnknown(manifest.restoreFactsStatus);
            return new SupportBundleSection { id = "restore", status = status, facts = facts.ToArray() };
        }

        private static SupportBundleSection CaptureUnityLogSuppressionSection()
        {
            List<string> facts = new List<string>();
            try
            {
                Type filterType = typeof(MMLog).Assembly.GetType("ModAPI.Core.UnityLogFilter", false);
                FieldInfo syncField = filterType != null
                    ? filterType.GetField("Sync", BindingFlags.NonPublic | BindingFlags.Static)
                    : null;
                FieldInfo countsField = filterType != null
                    ? filterType.GetField("SuppressedCounts", BindingFlags.NonPublic | BindingFlags.Static)
                    : null;
                object sync = syncField != null ? syncField.GetValue(null) : null;
                IDictionary counts = countsField != null ? countsField.GetValue(null) as IDictionary : null;
                if (sync == null || counts == null)
                    return Unavailable("unity-log-suppression", "Unity log suppression does not expose a readable runtime summary.");

                lock (sync)
                {
                    foreach (DictionaryEntry pair in counts)
                        facts.Add(ValueOrUnknown(Convert.ToString(pair.Key)) + "=" + Convert.ToString(pair.Value));
                }

                if (facts.Count == 0)
                    facts.Add("pendingSuppressedMessages=0");
                return new SupportBundleSection { id = "unity-log-suppression", status = "available", facts = facts.ToArray() };
            }
            catch (Exception ex)
            {
                return Unavailable("unity-log-suppression", "Unity log suppression summary probe failed: " + ex.GetType().Name);
            }
        }

        private static SupportBundleSection CaptureRandomSection()
        {
            try
            {
                Type randomType = typeof(MMLog).Assembly.GetType("ModAPI.Core.ModRandom", false);
                if (randomType == null)
                    return Unavailable("random", "No public random diagnostics source is available in the current runtime.");

                return Available("random",
                    "initialized=" + ReadStaticPropertyOrUnknown(randomType, "IsInitialized"),
                    "deterministic=" + ReadStaticPropertyOrUnknown(randomType, "IsDeterministic"),
                    "currentSeed=" + ReadStaticPropertyOrUnknown(randomType, "CurrentSeed"),
                    "currentStep=" + ReadStaticPropertyOrUnknown(randomType, "CurrentStep"),
                    "streamDiagnostics=unavailable");
            }
            catch (Exception ex)
            {
                return Unavailable("random", "Random diagnostic facts could not be read: " + ex.GetType().Name);
            }
        }

        private static SupportBundleSection CapturePatchReportSection()
        {
            try
            {
                Type registryType = typeof(MMLog).Assembly.GetType("ModAPI.Harmony.PatchRegistry", false);
                MethodInfo getLatest = registryType != null
                    ? registryType.GetMethod("GetLatestReport", BindingFlags.Public | BindingFlags.Static)
                    : null;
                if (getLatest == null)
                    return Unavailable("patch-report", "No public patch report snapshot is available in the current runtime.");

                object report = getLatest.Invoke(null, null);
                if (report == null)
                    return Unknown("patch-report", "Patch reporting is installed, but no report has been captured yet.");

                return Available("patch-report",
                    "assemblyName=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "AssemblyName"))),
                    "sourceName=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "SourceName"))),
                    "triggerName=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "TriggerName"))),
                    "discovered=" + ReadArrayLength(report, "Discovered"),
                    "applied=" + ReadArrayLength(report, "Applied"),
                    "skipped=" + ReadArrayLength(report, "Skipped"),
                    "missingPolicy=" + ReadArrayLength(report, "MissingPolicy"),
                    "conflicts=" + ReadArrayLength(report, "Conflicts"));
            }
            catch (Exception ex)
            {
                return Unavailable("patch-report", "Patch report probe failed: " + ex.GetType().Name);
            }
        }

        private static SupportBundleSection CaptureBackgroundWorkSection()
        {
            try
            {
                Type threadsType = typeof(MMLog).Assembly.GetType("ModAPI.Core.ModThreads", false);
                MethodInfo getDiagnostics = threadsType != null
                    ? threadsType.GetMethod("GetDiagnostics", BindingFlags.Public | BindingFlags.Static)
                    : null;
                if (getDiagnostics == null)
                    return Unavailable("background-work", "No public background-work diagnostics snapshot is available in the current runtime.");

                object report = getDiagnostics.Invoke(null, null);
                if (report == null)
                    return Unknown("background-work", "Background diagnostics provider returned no report.");

                return Available("background-work",
                    "queued=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Queued"))),
                    "running=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Running"))),
                    "completed=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Completed"))),
                    "canceled=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Canceled"))),
                    "failed=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Failed"))),
                    "staleSkipped=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "StaleSkipped"))),
                    "throttled=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Throttled"))),
                    "active=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Active"))),
                    "waiting=" + ValueOrUnknown(Convert.ToString(ReadProperty(report, "Waiting"))));
            }
            catch (Exception ex)
            {
                return Unavailable("background-work", "Background diagnostics probe failed: " + ex.GetType().Name);
            }
        }

        private static string[] CaptureLogs(int maxEntries)
        {
            int limit = maxEntries > 0 ? Math.Min(maxEntries, 1000) : 200;
            List<MMLog.LogEntry> entries = MMLog.GetRecentEntries(MMLog.LogLevel.Debug, limit);
            List<string> lines = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                MMLog.LogEntry entry = entries[i];
                if (entry == null)
                    continue;

                lines.Add(entry.Timestamp.ToString("o")
                    + " [" + entry.Level + "]"
                    + " [" + entry.Category + "]"
                    + " [" + ValueOrUnknown(entry.Source) + "] "
                    + (entry.Message ?? string.Empty));
            }

            return lines.ToArray();
        }

        private static string Serialize(SupportBundleSnapshot snapshot)
        {
            ManualJsonObject root = new ManualJsonObject();
            root.Set("bundleVersion", ManualJsonValue.Number(snapshot.bundleVersion));
            root.Set("capturedAtUtc", ManualJsonValue.String(snapshot.capturedAtUtc));
            root.Set("gameVersion", ManualJsonValue.String(snapshot.gameVersion));
            root.Set("unityVersion", ManualJsonValue.String(snapshot.unityVersion));
            root.Set("architecture", ManualJsonValue.String(snapshot.architecture));
            root.Set("modApiVersion", ManualJsonValue.String(snapshot.modApiVersion));
            root.Set("shelteredApiVersion", ManualJsonValue.String(snapshot.shelteredApiVersion));
            root.Set("activeMods", ManualJsonValue.Array(SerializeMods(snapshot.activeMods)));

            ManualJsonObject manifestJson;
            string manifestError;
            if (snapshot.saveManifest != null
                && ManualJson.TryParseObject(SaveRegistryCore.SerializeSlotManifest(snapshot.saveManifest), out manifestJson, out manifestError))
            {
                root.Set("saveManifest", ManualJsonValue.Object(manifestJson));
            }
            else
            {
                root.Set("saveManifest", ManualJsonValue.Null());
            }

            ManualJsonArray sections = new ManualJsonArray();
            for (int i = 0; i < snapshot.diagnostics.Length; i++)
            {
                SupportBundleSection section = snapshot.diagnostics[i];
                if (section == null)
                    continue;

                ManualJsonObject value = new ManualJsonObject();
                value.Set("id", ManualJsonValue.String(section.id));
                value.Set("status", ManualJsonValue.String(section.status));
                value.Set("facts", ManualJsonValue.Array(SerializeStrings(section.facts)));
                sections.Add(ManualJsonValue.Object(value));
            }
            root.Set("diagnostics", ManualJsonValue.Array(sections));
            root.Set("logs", ManualJsonValue.Array(SerializeStrings(snapshot.logs)));
            return ManualJson.Serialize(root, true);
        }

        private static ManualJsonArray SerializeMods(LoadedModInfo[] mods)
        {
            ManualJsonArray values = new ManualJsonArray();
            if (mods == null)
                return values;

            for (int i = 0; i < mods.Length; i++)
            {
                LoadedModInfo mod = mods[i];
                if (mod == null)
                    continue;

                ManualJsonObject value = new ManualJsonObject();
                value.Set("modId", ManualJsonValue.String(mod.modId));
                value.Set("version", ManualJsonValue.String(mod.version));
                value.Set("requiredModApiVersion", ManualJsonValue.String(mod.requiredModApiVersion));
                value.Set("requiredShelteredApiVersion", ManualJsonValue.String(mod.requiredShelteredApiVersion));
                value.Set("warnings", ManualJsonValue.Array(SerializeStrings(mod.warnings)));
                values.Add(ManualJsonValue.Object(value));
            }

            return values;
        }

        private static ManualJsonArray SerializeStrings(string[] strings)
        {
            ManualJsonArray values = new ManualJsonArray();
            if (strings == null)
                return values;

            for (int i = 0; i < strings.Length; i++)
                values.Add(ManualJsonValue.String(strings[i]));
            return values;
        }

        private static SupportBundleSection Available(string id, params string[] facts)
        {
            return new SupportBundleSection { id = id, status = "available", facts = facts ?? new string[0] };
        }

        private static SupportBundleSection Unknown(string id, params string[] facts)
        {
            return new SupportBundleSection { id = id, status = "unknown", facts = facts ?? new string[0] };
        }

        private static SupportBundleSection Unavailable(string id, params string[] facts)
        {
            return new SupportBundleSection { id = id, status = "unavailable", facts = facts ?? new string[0] };
        }

        private static object ReadProperty(object source, string name)
        {
            PropertyInfo property = source != null
                ? source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                : null;
            return property != null ? property.GetValue(source, null) : null;
        }

        private static int ReadArrayLength(object source, string propertyName)
        {
            Array values = ReadProperty(source, propertyName) as Array;
            return values != null ? values.Length : 0;
        }

        private static string ReadStaticPropertyOrUnknown(Type source, string name)
        {
            PropertyInfo property = source != null
                ? source.GetProperty(name, BindingFlags.Public | BindingFlags.Static)
                : null;
            return property != null ? ValueOrUnknown(Convert.ToString(property.GetValue(null, null))) : "unavailable";
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrEmpty(value) ? "unknown" : value;
        }
    }
}
