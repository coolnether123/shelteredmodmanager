using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Util;
using ShelteredAPI.Saves.Backups;
using ShelteredAPI.Saves.Runtime;

namespace ShelteredAPI.Saves
{
    /// <summary>
    /// Provides central logic for managing mod-aware save files.
    /// 
    /// REFACTORED: No longer uses a global manifest.json file.
    /// - Save entries are discovered by scanning Slot_* directories
    /// - Save metadata is read from the XML file on demand
    /// - Per-slot manifest.json files store only mod tracking data
    /// </summary>
    internal class SaveRegistryCore : ISaveApi
    {
        private readonly object _lock = new object();
        private readonly string _scenarioId;
        
        // Cache of discovered entries, keyed by absoluteSlot
        // Invalidated when saves are created/deleted
        private Dictionary<int, SaveEntry> _entryCache;
        private bool _cacheValid = false;

        public SaveRegistryCore(string scenarioId)
        {
            this._scenarioId = scenarioId;
        }

        // --- ISaveApi Implementation ---
        public SaveEntry Get(string saveId) => GetSave(saveId);
        public SaveEntry Overwrite(string saveId, SaveOverwriteOptions opts, byte[] xmlBytes) => OverwriteSave(saveId, opts, xmlBytes);

        /// <summary>
        /// Returns all save entries by scanning Slot_* directories.
        /// </summary>
        public SaveEntry[] ListSaves()
        {
            return GetValidEntriesList().ToArray();
        }

        private List<SaveEntry> GetValidEntriesList()
        {
            var entries = GetAllEntries();
            var results = new List<SaveEntry>();
            
            foreach (var e in entries.Values)
            {
                var savePath = DirectoryProvider.EntryPath(_scenarioId, e.absoluteSlot);
                if (File.Exists(savePath))
                {
                    results.Add(e);
                }
            }

            results.Sort((a, b) => a.absoluteSlot.CompareTo(b.absoluteSlot));
            return results;
        }

        public SaveEntry[] ListSaves(int page, int pageSize)
        {
            var all = ListSaves();
            if (all == null || all.Length == 0) return new SaveEntry[0];
            int start = Math.Max(0, page * pageSize);
            if (start >= all.Length) return new SaveEntry[0];
            int count = Math.Min(pageSize, all.Length - start);
            var result = new SaveEntry[count];
            Array.Copy(all, start, result, 0, count);
            return result;
        }

        public SaveEntry GetSave(string saveId)
        {
            foreach (var e in GetAllEntries().Values)
                if (e.id == saveId) return e;
            return null;
        }

        /// <summary>
        /// Gets a save entry by its absolute slot number.
        /// </summary>
        public SaveEntry GetSaveBySlot(int absoluteSlot)
        {
            var entries = GetAllEntries();
            if (entries.TryGetValue(absoluteSlot, out var entry))
                return entry;
            return null;
        }

        /// <summary>
        /// Deletes a save by its absolute slot number.
        /// </summary>
        public bool DeleteBySlot(int absoluteSlot)
        {
            MMLog.Write($"DeleteBySlot called for slot {absoluteSlot}");

            var deleted = DeleteSlotDirectory(absoluteSlot, "DeleteBySlot", true);
            if (deleted)
            {
                InvalidateCache();
                MMLog.Write($"DeleteBySlot: Successfully deleted slot {absoluteSlot}");
            }
            return deleted;
        }

        public int CountSaves()
        {
            return GetValidEntriesList().Count;
        }

        public int GetMaxSlot()
        {
            var valid = GetValidEntriesList();
            if (valid.Count == 0) return 0;
            return valid[valid.Count - 1].absoluteSlot;
        }

        // NEW: Shutdown Coordination Flag
        public static bool DiscoveryPaused = false;

        public static void PauseDiscovery() => DiscoveryPaused = true;
        public static void ResumeDiscovery() => DiscoveryPaused = false;

        /// <summary>
        /// Discovers all save slots by scanning directories.
        /// Reads metadata from XML files on demand.
        /// </summary>
        private Dictionary<int, SaveEntry> GetAllEntries()
        {
            lock (_lock)
            {
                // CRITICAL FIX: If discovery is paused (during shutdown), do NOT touch the disk.
                // Return whatever we have in cache, or an empty list if nothing.
                if (DiscoveryPaused)
                {
                    return _entryCache ?? new Dictionary<int, SaveEntry>();
                }

                if (_cacheValid && _entryCache != null)
                    return _entryCache;

                _entryCache = new Dictionary<int, SaveEntry>();

                var scenarioRoot = DirectoryProvider.ScenarioRoot(_scenarioId, false);
                if (!Directory.Exists(scenarioRoot))
                {
                    _cacheValid = true;
                    return _entryCache;
                }

                var dirs = Directory.GetDirectories(scenarioRoot, "Slot_*");
                foreach (var dir in dirs)
                {
                    var dirName = Path.GetFileName(dir);
                    var numPart = dirName.Substring(5); // "Slot_" is 5 chars
                    if (int.TryParse(numPart, out int absoluteSlot))
                    {
                        var savePath = Path.Combine(dir, "SaveData.xml");
                        try
                        {
                            // Discover slot even if XML is missing (e.g. newly created slot)
                            var entry = BuildEntryFromSlot(absoluteSlot, savePath);
                            if (entry != null)
                                _entryCache[absoluteSlot] = entry;
                        }
                        catch (Exception ex)
                        {
                            MMLog.WriteError($"Error reading slot {absoluteSlot}: {ex.Message}");
                        }
                    }
                }
                
                _cacheValid = true;
                return _entryCache;
            }
        }

        /// <summary>
        /// Builds a SaveEntry by reading metadata from the XML file.
        /// </summary>
        private SaveEntry BuildEntryFromSlot(int absoluteSlot, string savePath)
        {
            var entry = new SaveEntry
            {
                id = $"{_scenarioId}_{absoluteSlot}", // Stable ID based on scenario and slot
                absoluteSlot = absoluteSlot,
                name = $"Slot {absoluteSlot}",
                scenarioId = _scenarioId,
                saveInfo = new SaveInfo()
            };


            if (File.Exists(savePath))
            {
                try
                {
                    // Read basic file info (safe OS operation)
                    var bytes = File.ReadAllBytes(savePath);
                    entry.createdAt = File.GetCreationTimeUtc(savePath).ToString("o");
                    entry.updatedAt = File.GetLastWriteTimeUtc(savePath).ToString("o");
                    entry.fileSize = bytes.Length;
                    entry.crc32 = CRC32.Compute(bytes);

                    // SAFE: We now use manual regex parsing which doesn't rely on Unity objects.
                    // This is safe to run even during shutdown.
                    TryUpdateEntryInfo(entry, bytes);
                    
                    // Use family name as display name if available
                    if (!string.IsNullOrEmpty(entry.saveInfo?.familyName))
                        entry.name = entry.saveInfo.familyName;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"Error parsing {savePath}: {ex.Message}");
                }
            }
            else
            {
                // Directory exists but no XML yet
                var dir = Path.GetDirectoryName(savePath);
                entry.createdAt = Directory.GetCreationTimeUtc(dir).ToString("o");
                entry.updatedAt = Directory.GetLastWriteTimeUtc(dir).ToString("o");
                entry.name = "New Game";
            }

            return entry;
        }

        /// <summary>
        /// Invalidates the entry cache, forcing re-discovery on next access.
        /// </summary>
        private void InvalidateCache()
        {
            lock (_lock)
            {
                _cacheValid = false;
                _entryCache = null;
            }
        }

        public SaveEntry CreateSave(SaveCreateOptions opts)
        {
            SaveCreateOptions normalized = NormalizeCreateOptions(opts);
            return CreateTransientEntry(normalized);
        }

        private SaveCreateOptions NormalizeCreateOptions(SaveCreateOptions opts)
        {
            SaveCreateOptions normalized = new SaveCreateOptions();
            if (opts != null)
            {
                normalized.name = opts.name;
                normalized.extraJson = opts.extraJson;
                normalized.absoluteSlot = opts.absoluteSlot;
            }

            if (normalized.absoluteSlot <= 0)
                normalized.absoluteSlot = GetNextCreatableSlot();

            return normalized;
        }

        internal int GetNextCreatableSlot()
        {
            int firstSlot = ExpandedVanillaSaves.IsStandardScenario(_scenarioId) ? 4 : 1;
            int maxSlot = firstSlot - 1;

            foreach (int slot in GetAllEntries().Keys)
            {
                if (slot >= firstSlot && slot > maxSlot)
                    maxSlot = slot;
            }

            return maxSlot + 1;
        }

        public SaveEntry OverwriteSave(string saveId, SaveOverwriteOptions opts, byte[] xmlBytes)
        {
            
            // Normal Flow: Find entry by ID (triggers discovery)
            var entry = GetSave(saveId);
            if (entry == null && !TryCreateTransientEntryFromId(saveId, opts, out entry))
                return null;

            SaveBackupService.BackupCustomEntryBeforeOverwrite(entry);

            if (opts != null && !string.IsNullOrEmpty(opts.name))
                entry.name = NameSanitizer.SanitizeName(opts.name);
            entry.updatedAt = DateTime.UtcNow.ToString("o");
            
            if (xmlBytes != null)
            {
                if (!TryWriteEntryFile(entry.absoluteSlot, xmlBytes, out long size, out uint crc))
                    return null;

                entry.fileSize = size;
                entry.crc32 = crc;

                TryUpdateEntryInfo(entry, xmlBytes);
                
                // Update per-slot manifest (Mod List only)
                UpdateSlotManifest(entry.absoluteSlot, entry.saveInfo);
            }
            
            InvalidateCache();
            return entry;
        }

        private SaveEntry CreateTransientEntry(SaveCreateOptions normalized)
        {
            var now = DateTime.UtcNow.ToString("o");
            return new SaveEntry
            {
                id = $"{_scenarioId}_{normalized.absoluteSlot}",
                absoluteSlot = normalized.absoluteSlot,
                name = NameSanitizer.SanitizeName(normalized.name) ?? $"Slot {normalized.absoluteSlot}",
                createdAt = now,
                updatedAt = now,
                gameVersion = Application.version,
                modApiVersion = "1",
                scenarioId = _scenarioId,
                scenarioVersion = ScenarioRegistry.GetScenario(_scenarioId)?.version ?? "1.0",
                saveInfo = new SaveInfo()
            };
        }

        private bool TryCreateTransientEntryFromId(string saveId, SaveOverwriteOptions opts, out SaveEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(saveId))
                return false;

            string prefix = _scenarioId + "_";
            if (!saveId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            int absoluteSlot;
            if (!int.TryParse(saveId.Substring(prefix.Length), out absoluteSlot) || absoluteSlot <= 0)
                return false;

            entry = CreateTransientEntry(new SaveCreateOptions
            {
                absoluteSlot = absoluteSlot,
                name = opts != null ? opts.name : null
            });
            entry.id = saveId;
            return true;
        }

        public bool DeleteSave(string saveId)
        {
            return DeleteSave(saveId, true);
        }

        internal bool TryDeleteSave(string saveId)
        {
            return DeleteSave(saveId, false);
        }

        private bool DeleteSave(string saveId, bool logMissingAsError)
        {
            MMLog.WriteDebug($"DeleteSave called with ID: '{saveId}'");

            var entry = GetSave(saveId);
            if (entry == null)
            {
                if (logMissingAsError)
                    MMLog.WriteError($"DeleteSave: Entry not found for ID '{saveId}'");
                else
                    MMLog.WriteDebug($"DeleteSave: Entry not found for ID '{saveId}'");
                return false;
            }

            MMLog.WriteDebug($"DeleteSave: Found entry - Slot={entry.absoluteSlot}, Name='{entry.name}'");

            var deleted = DeleteSlotDirectory(entry.absoluteSlot, "DeleteSave", false);
            if (!deleted)
            {
                return false;
            }
            
            // Delete preview if exists
            try { File.Delete(DirectoryProvider.PreviewPath(_scenarioId, saveId)); } catch { }

            InvalidateCache();
            MMLog.WriteDebug($"DeleteSave: Completed for slot {entry.absoluteSlot}");
            return true;
        }

        private bool DeleteSlotDirectory(int absoluteSlot, string operation, bool failIfMissing)
        {
            var slotRoot = DirectoryProvider.SlotRoot(_scenarioId, absoluteSlot, false);
            MMLog.WriteDebug(string.Format("{0}: Slot directory = '{1}'", operation, slotRoot));

            try
            {
                if (!Directory.Exists(slotRoot))
                {
                    MMLog.WriteError(string.Format("{0}: Directory does not exist: '{1}'", operation, slotRoot));
                    return !failIfMissing;
                }

                string deletedRoot = DirectoryProvider.DeletedRoot(_scenarioId);
                string deletedName = string.Format("Slot_{0}_{1:yyyyMMdd_HHmmss}", absoluteSlot, DateTime.UtcNow);
                string deletedPath = Path.Combine(deletedRoot, deletedName);
                while (Directory.Exists(deletedPath))
                {
                    deletedPath = Path.Combine(deletedRoot, deletedName + "_" + Path.GetRandomFileName().Replace(".", string.Empty));
                }

                MMLog.WriteDebug(string.Format("{0}: Moving directory '{1}' to '{2}'", operation, slotRoot, deletedPath));
                Directory.Move(slotRoot, deletedPath);
                MMLog.WriteDebug(string.Format("{0}: Directory quarantined successfully", operation));
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError(string.Format("{0}: Failed to delete slot directory: {1}", operation, ex.Message));
                return false;
            }
        }

        internal void UpdateSlotManifest(int absoluteSlot, SaveInfo info)
        {
            try
            {
                var newManifest = CreateCurrentManifestSnapshot(info);
                string manifestPath;
                string error;
                if (!TryWriteSlotManifest(_scenarioId, absoluteSlot, newManifest, out manifestPath, out error))
                {
                    MMLog.WriteError($"FAILED to update slot manifest for Slot {absoluteSlot}: {error}");
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"FAILED to update slot manifest for Slot {absoluteSlot}: {ex}");
            }
        }

        internal static SlotManifest CreateCurrentManifestSnapshot(SaveInfo info)
        {
            return SaveManifestFacts.CaptureCurrent(info);
        }


        // REMOVED: LoadManifest() - now using GetAllEntries() for directory-based discovery


        // REMOVED: ReconcileManifestWithSlots() - discovery now handled by GetAllEntries()

        /// <summary>
        /// Condenses save slots to remove gaps in numbering.
        /// Works directly with directories without using a global manifest.
        /// </summary>
        public void CondenseSlots()
        {
            var entries = ListSaves();
            if (entries == null || entries.Length == 0) return;

            // Determines starting slot based on scenario type
            int expectedSlot = (_scenarioId == "Standard") ? 4 : 1;
            
            bool changed = false;
            foreach (var entry in entries)
            {
                // Skip reserved vanilla slots for Standard scenario
                if (_scenarioId == "Standard" && entry.absoluteSlot < 4) continue;

                if (entry.absoluteSlot > expectedSlot)
                {
                    // Move it!
                    bool success = false;
                    try
                    {
                        var oldDir = DirectoryProvider.SlotRoot(_scenarioId, entry.absoluteSlot, false);
                        var newDir = DirectoryProvider.SlotRoot(_scenarioId, expectedSlot, false);

                        // Check for collision
                        if (Directory.Exists(newDir))
                        {
                            // If empty, delete it to allow move
                            if (Directory.GetFiles(newDir).Length == 0 && Directory.GetDirectories(newDir).Length == 0)
                            {
                                Directory.Delete(newDir);
                            }
                            else
                            {
                                MMLog.WriteError($"Cannot move {entry.absoluteSlot} to {expectedSlot} - target not empty.");
                                expectedSlot++; 
                                continue; 
                            }
                        }

                        Directory.Move(oldDir, newDir);
                        MMLog.Write($"Moved save from Slot {entry.absoluteSlot} to Slot {expectedSlot}");
                        success = true;
                        changed = true;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError($"Failed to move slot {entry.absoluteSlot} to {expectedSlot}: {ex.Message}");
                    }
                    
                    if (success) expectedSlot++;
                }
                else if (entry.absoluteSlot == expectedSlot)
                {
                    expectedSlot++;
                }
            }

            if (changed)
            {
                InvalidateCache();
            }
        }


        // REMOVED: SaveManifestFile() - no longer using global manifest
        

        // REMOVED: SerializeManifest(), DeserializeManifest(), ParseSaveEntry(), ParseSaveInfo()
        // Global manifests are no longer used; per-slot JSON is handled by SerializeSlotManifest().

        private bool TryWriteEntryFile(int absoluteSlot, byte[] xmlBytes, out long fileSize, out uint crc)
        {
            return TryWriteEntryFile(_scenarioId, absoluteSlot, xmlBytes, out fileSize, out crc);
        }

        private static bool TryWriteEntryFile(string scenarioId, int absoluteSlot, byte[] xmlBytes, out long fileSize, out uint crc)
        {
            var path = DirectoryProvider.EntryPath(scenarioId, absoluteSlot);
            var tmp = path + ".tmp";
            fileSize = 0; crc = 0;
            try
            {
                File.WriteAllBytes(tmp, xmlBytes);
                fileSize = new FileInfo(tmp).Length;
                crc = CRC32.Compute(xmlBytes);
                try { File.Replace(tmp, path, null); }
                catch { File.Copy(tmp, path, true); File.Delete(tmp); }
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"FAILED writing entry file for Slot_{absoluteSlot}: {ex.Message}");
                return false;
            }
        }


        private static void TryUpdateEntryInfo(SaveEntry entry, byte[] xmlBytes)
        {
            try
            {
                if (entry.saveInfo == null)
                {
                    entry.saveInfo = new SaveInfo();
                }

                string error;
                if (!SaveInfoXmlMetadataReader.TryRead(xmlBytes, entry.saveInfo, out error))
                {
                    MMLog.WriteWarning("Could not parse save metadata XML: " + error);
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("CRITICAL parse error in metadata: " + ex);
            }
        }



        // REMOVED: UniqueName() - no longer needed, names come from XML

        /// <summary>
        /// Serializes a SlotManifest to JSON without Unity JsonUtility.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: Unity's JsonUtility.ToJson() has a critical limitation - it CANNOT serialize
        /// arrays of custom classes (like LoadedModInfo[]). When you call JsonUtility.ToJson() on
        /// a SlotManifest, it will silently omit the 'lastLoadedMods' field from the output JSON,
        /// even though the array is populated in memory. This causes saves to appear as having 0 mods.
        /// 
        /// We map the DTO through ModAPI.Util.ManualJson so escaping/parsing stays centralized.
        /// </remarks>
        internal static string SerializeSlotManifest(SlotManifest manifest)
        {
            if (manifest == null)
            {
                manifest = new SlotManifest();
            }

            ManualJsonObject root = new ManualJsonObject();
            root.Set("manifestVersion", ManualJsonValue.Number(manifest.manifestVersion));
            root.Set("lastModified", ManualJsonValue.String(manifest.lastModified));
            root.Set("family_name", ManualJsonValue.String(manifest.family_name));
            root.Set("saveScopeId", ManualJsonValue.String(manifest.saveScopeId));
            root.Set("saveId", ManualJsonValue.String(manifest.saveId));
            root.Set("customScenarioId", ManualJsonValue.String(manifest.customScenarioId));
            root.Set("source", ManualJsonValue.String(manifest.source));
            root.Set("sourceSlot", ManualJsonValue.Number(manifest.sourceSlot));
            root.Set("sourceVanillaCrc32", ManualJsonValue.Number(((long)manifest.sourceVanillaCrc32).ToString(CultureInfo.InvariantCulture)));
            root.Set("sourceVanillaLastWriteUtc", ManualJsonValue.String(manifest.sourceVanillaLastWriteUtc));
            root.Set("modApiVersion", ManualJsonValue.String(manifest.modApiVersion));
            root.Set("shelteredApiVersion", ManualJsonValue.String(manifest.shelteredApiVersion));
            root.Set("mapFactsStatus", ManualJsonValue.String(manifest.mapFactsStatus));
            root.Set("hasMapSize", ManualJsonValue.Boolean(manifest.hasMapSize));
            root.Set("mapSize", ManualJsonValue.Number(manifest.mapSize));
            root.Set("runtimeMapFactsStatus", ManualJsonValue.String(manifest.runtimeMapFactsStatus));
            root.Set("runtimeMapWidth", ManualJsonValue.Number(manifest.runtimeMapWidth));
            root.Set("runtimeMapHeight", ManualJsonValue.Number(manifest.runtimeMapHeight));
            root.Set("runtimeMapScaleFactor", ManualJsonValue.String(manifest.runtimeMapScaleFactor));
            root.Set("hasMapSeed", ManualJsonValue.Boolean(manifest.hasMapSeed));
            root.Set("mapSeed", ManualJsonValue.Number(manifest.mapSeed));
            root.Set("queueFactsStatus", ManualJsonValue.String(manifest.queueFactsStatus));
            root.Set("queueSummary", ManualJsonValue.String(manifest.queueSummary));
            root.Set("restoreFactsStatus", ManualJsonValue.String(manifest.restoreFactsStatus));
            root.Set("restoreLineageId", ManualJsonValue.String(manifest.restoreLineageId));

            ManualJsonArray mods = new ManualJsonArray();
            if (manifest.lastLoadedMods != null && manifest.lastLoadedMods.Length > 0)
            {
                for (int i = 0; i < manifest.lastLoadedMods.Length; i++)
                {
                    var mod = manifest.lastLoadedMods[i];
                    if (mod == null)
                    {
                        continue;
                    }

                    ManualJsonObject modJson = new ManualJsonObject();
                    modJson.Set("modId", ManualJsonValue.String(mod.modId));
                    modJson.Set("version", ManualJsonValue.String(mod.version));
                    modJson.Set("requiredModApiVersion", ManualJsonValue.String(mod.requiredModApiVersion));
                    modJson.Set("requiredShelteredApiVersion", ManualJsonValue.String(mod.requiredShelteredApiVersion));
                    ManualJsonArray warnings = new ManualJsonArray();
                    if (mod.warnings != null)
                    {
                        for (int w = 0; w < mod.warnings.Length; w++)
                        {
                            warnings.Add(ManualJsonValue.String(mod.warnings[w]));
                        }
                    }

                    modJson.Set("warnings", ManualJsonValue.Array(warnings));
                    mods.Add(ManualJsonValue.Object(modJson));
                }
            }

            root.Set("lastLoadedMods", ManualJsonValue.Array(mods));
            return ManualJson.Serialize(root, true);
        }

        internal static bool TryWriteSlotManifest(string scenarioId, int absoluteSlot, SlotManifest manifest, out string manifestPath, out string error)
        {
            manifestPath = null;
            error = null;

            if (manifest == null)
            {
                error = "Manifest was null.";
                return false;
            }

            try
            {
                SaveManifestFacts.ApplyStorageIdentityFacts(manifest, scenarioId, absoluteSlot);
                var slotRoot = DirectoryProvider.SlotRoot(scenarioId, absoluteSlot, true);
                manifestPath = Path.Combine(slotRoot, "manifest.json");
                File.WriteAllText(manifestPath, SerializeSlotManifest(manifest));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }



        /// <summary>
        /// Deserializes a SlotManifest from JSON.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: Unity's JsonUtility.FromJson fails to deserialize arrays of objects when 
        /// they are written in compact/inline format like: { "modId": "...", "version": "..." }
        /// 
        /// This happened because early versions used handwritten manifest JSON. JsonUtility is
        /// extremely strict about some manually written formats.
        /// 
        /// To maintain backward compatibility with existing save files, we parse the complete
        /// manifest through ModAPI.Util.ManualJson instead of splitting scalar and array handling.
        /// </remarks>
        internal static SlotManifest DeserializeSlotManifest(string json)
        {
            if (string.IsNullOrEmpty(json)) return new SlotManifest();
            ManualJsonObject root;
            string error;
            if (!ManualJson.TryParseObject(json, out root, out error))
            {
                MMLog.WriteError("DeserializeSlotManifest: Parse error: " + error);
                return new SlotManifest();
            }

            SlotManifest result = new SlotManifest();
            result.manifestVersion = root.GetInt("manifestVersion", result.manifestVersion);
            result.lastModified = root.GetString("lastModified", result.lastModified);
            result.family_name = root.GetString("family_name", result.family_name);
            result.saveScopeId = root.GetString("saveScopeId", result.saveScopeId);
            result.saveId = root.GetString("saveId", result.saveId);
            result.customScenarioId = root.GetString("customScenarioId", result.customScenarioId);
            result.source = root.GetString("source", result.source);
            result.sourceSlot = root.GetInt("sourceSlot", result.sourceSlot);
            result.sourceVanillaCrc32 = ReadUInt32(root, "sourceVanillaCrc32", result.sourceVanillaCrc32);
            result.sourceVanillaLastWriteUtc = root.GetString("sourceVanillaLastWriteUtc", result.sourceVanillaLastWriteUtc);
            result.modApiVersion = root.GetString("modApiVersion", result.modApiVersion);
            result.shelteredApiVersion = root.GetString("shelteredApiVersion", result.shelteredApiVersion);
            result.mapFactsStatus = root.GetString("mapFactsStatus", result.mapFactsStatus);
            result.hasMapSize = root.GetBool("hasMapSize", result.hasMapSize);
            result.mapSize = root.GetInt("mapSize", result.mapSize);
            result.runtimeMapFactsStatus = root.GetString("runtimeMapFactsStatus", result.runtimeMapFactsStatus);
            result.runtimeMapWidth = root.GetInt("runtimeMapWidth", result.runtimeMapWidth);
            result.runtimeMapHeight = root.GetInt("runtimeMapHeight", result.runtimeMapHeight);
            result.runtimeMapScaleFactor = root.GetString("runtimeMapScaleFactor", result.runtimeMapScaleFactor);
            result.hasMapSeed = root.GetBool("hasMapSeed", result.hasMapSeed);
            result.mapSeed = root.GetInt("mapSeed", result.mapSeed);
            result.queueFactsStatus = root.GetString("queueFactsStatus", result.queueFactsStatus);
            result.queueSummary = root.GetString("queueSummary", result.queueSummary);
            result.restoreFactsStatus = root.GetString("restoreFactsStatus", result.restoreFactsStatus);
            result.restoreLineageId = root.GetString("restoreLineageId", result.restoreLineageId);
            result.lastLoadedMods = ReadLoadedMods(root.GetArray("lastLoadedMods"));
            return result;
        }

        private static uint ReadUInt32(ManualJsonObject root, string name, uint fallback)
        {
            ManualJsonValue value = root != null ? root.Get(name) : null;
            if (value == null)
                return fallback;

            string raw = null;
            if (value.Type == ManualJsonValueType.Number)
                raw = value.NumberText;
            else if (value.Type == ManualJsonValueType.String)
                raw = value.StringValue;

            uint parsed;
            return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static LoadedModInfo[] ReadLoadedMods(ManualJsonArray modsJson)
        {
            if (modsJson == null)
            {
                return new LoadedModInfo[0];
            }

            List<LoadedModInfo> mods = new List<LoadedModInfo>();
            for (int i = 0; i < modsJson.Items.Count; i++)
            {
                ManualJsonValue value = modsJson.Items[i];
                ManualJsonObject modJson = value != null && value.Type == ManualJsonValueType.Object ? value.ObjectValue : null;
                if (modJson == null)
                {
                    continue;
                }

                mods.Add(new LoadedModInfo
                {
                    modId = modJson.GetString("modId", string.Empty),
                    version = modJson.GetString("version", string.Empty),
                    requiredModApiVersion = modJson.GetString("requiredModApiVersion", null),
                    requiredShelteredApiVersion = modJson.GetString("requiredShelteredApiVersion", null),
                    warnings = ReadStringArray(modJson.GetArray("warnings"))
                });
            }

            return mods.ToArray();
        }

        private static string[] ReadStringArray(ManualJsonArray array)
        {
            if (array == null)
            {
                return new string[0];
            }

            List<string> values = new List<string>();
            for (int i = 0; i < array.Items.Count; i++)
            {
                ManualJsonValue value = array.Items[i];
                if (value != null && value.Type == ManualJsonValueType.String)
                {
                    values.Add(value.StringValue);
                }
            }

            return values.ToArray();
        }

        /// <summary>
        /// Reads SaveInfo from a save file's XML data.
        /// Extracts all game settings including difficulty fields.
        /// </summary>
        public static SaveInfo ReadSaveInfoFromXml(byte[] xmlBytes)
        {
            SaveInfo info = new SaveInfo();
            string error;
            if (!SaveInfoXmlMetadataReader.TryRead(xmlBytes, info, out error))
            {
                MMLog.WriteError("Failed to read SaveInfo from XML: " + error);
            }

            return info;
        }

        /// <summary>
        /// Reads SaveInfo from a vanilla save slot (1-5).
        /// Handles the XOR decryption used by the game's PlatformSave_PC.
        /// </summary>
        public static SaveInfo ReadVanillaSaveInfo(int slotNumber)
        {
            try
            {
                string fullPath = GetVanillaSavePath(slotNumber);

                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                {
                    return null;  // Return null for empty slots
                }
                
                // Read and decrypt the file (XOR cipher from PlatformSave_PC)
                byte[] encryptedData = File.ReadAllBytes(fullPath);
                byte[] decryptedData = DecryptVanillaSave(encryptedData);
                
                // Parse the decrypted XML
                var info = ReadSaveInfoFromXml(decryptedData);
                return info;
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"Failed to read vanilla save info for slot {slotNumber}: {ex.Message}");
                return null;  // Return null on error
            }
        }

        internal static SaveInfo ReadVanillaSaveInfoFromEncryptedBytes(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return null;

            byte[] decryptedData = DecryptVanillaSave(encryptedData);
            return ReadSaveInfoFromXml(decryptedData);
        }

        internal static string GetVanillaSavePath(int slotNumber)
        {
            try
            {
                string savesPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "saves");
                string fileName;

                switch (slotNumber)
                {
                    case 1: fileName = "savedata_01.dat"; break;
                    case 2: fileName = "savedata_02.dat"; break;
                    case 3: fileName = "savedata_03.dat"; break;
                    case 4: fileName = "savedata_surrounded.dat"; break;
                    case 5: fileName = "savedata_stasis.dat"; break;
                    default: return null;
                }

                return Path.Combine(savesPath, fileName);
            }
            catch
            {
                return null;
            }
        }

        internal static SaveEntry ReadVanillaSaveEntry(int slotNumber, string scenarioId, string saveId, int displaySlot)
        {
            string path = GetVanillaSavePath(slotNumber);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] encryptedData = File.ReadAllBytes(path);
                byte[] decryptedData = DecryptVanillaSave(encryptedData);
                SaveInfo info = ReadSaveInfoFromXml(decryptedData);

                return new SaveEntry
                {
                    id = saveId,
                    absoluteSlot = displaySlot,
                    name = info != null && !string.IsNullOrEmpty(info.familyName) ? info.familyName : "Vanilla Slot",
                    createdAt = File.GetCreationTimeUtc(path).ToString("o"),
                    updatedAt = File.GetLastWriteTimeUtc(path).ToString("o"),
                    fileSize = encryptedData.Length,
                    crc32 = CRC32.Compute(decryptedData),
                    scenarioId = scenarioId,
                    scenarioVersion = ScenarioRegistry.GetScenario(scenarioId)?.version ?? "1.0",
                    saveInfo = info ?? new SaveInfo()
                };
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"Failed to read vanilla save entry for slot {slotNumber}: {ex.Message}");
                return null;
            }
        }

        internal static VanillaMirrorComparisonResult CompareStandardVanillaMirror(int slotNumber)
        {
            VanillaMirrorComparisonResult result = new VanillaMirrorComparisonResult
            {
                SlotNumber = slotNumber,
                SaveType = slotNumber >= 1 && slotNumber <= 3
                    ? (SaveManager.SaveType)slotNumber
                    : SaveManager.SaveType.Invalid,
                VanillaPath = GetVanillaSavePath(slotNumber),
                MirrorPath = DirectoryProvider.EntryPath(ScenarioSaveIdGuards.StandardStorageScenarioId, slotNumber, false)
            };

            if (slotNumber < 1 || slotNumber > 3)
            {
                result.Status = VanillaMirrorComparisonStatus.MissingVanilla;
                result.Error = "Standard vanilla mirror comparison only supports slots 1-3.";
                return result;
            }

            if (string.IsNullOrEmpty(result.VanillaPath) || !File.Exists(result.VanillaPath))
            {
                result.Status = VanillaMirrorComparisonStatus.MissingVanilla;
                TryReadMirrorBytes(result);
                return result;
            }

            try
            {
                byte[] encryptedData = File.ReadAllBytes(result.VanillaPath);
                result.VanillaXmlBytes = DecryptVanillaSave(encryptedData);
                result.SourceVanillaCrc32 = CRC32.Compute(result.VanillaXmlBytes);
                result.SourceVanillaLastWriteUtc = File.GetLastWriteTimeUtc(result.VanillaPath);
            }
            catch (Exception ex)
            {
                result.Status = VanillaMirrorComparisonStatus.MissingVanilla;
                result.Error = ex.Message;
                return result;
            }

            if (!File.Exists(result.MirrorPath))
            {
                result.Status = VanillaMirrorComparisonStatus.MissingMirror;
                return result;
            }

            TryReadMirrorBytes(result);
            result.Status = ByteArraysEqual(result.VanillaXmlBytes, result.MirrorXmlBytes)
                ? VanillaMirrorComparisonStatus.InSync
                : VanillaMirrorComparisonStatus.Diverged;
            return result;
        }

        internal static SaveEntry WriteStandardVanillaMirrorFromVanilla(
            VanillaMirrorComparisonResult comparison,
            bool backupExistingMirror,
            string reason)
        {
            if (comparison == null || comparison.SlotNumber < 1 || comparison.SlotNumber > 3)
                return null;

            byte[] xmlBytes = comparison.VanillaXmlBytes;
            if (xmlBytes == null || xmlBytes.Length == 0)
                return null;

            string scenarioId = ScenarioSaveIdGuards.StandardStorageScenarioId;
            SaveRegistryCore registry = new SaveRegistryCore(scenarioId);
            SaveEntry existing = registry.GetSaveBySlot(comparison.SlotNumber);
            if (backupExistingMirror && existing != null && File.Exists(comparison.MirrorPath))
                SaveBackupService.BackupCustomEntryBeforeOverwrite(existing);

            long fileSize;
            uint crc;
            if (!TryWriteEntryFile(scenarioId, comparison.SlotNumber, xmlBytes, out fileSize, out crc))
                return null;

            SlotManifest manifest = CreateVanillaMirrorManifest(
                ReadSaveInfoFromXml(xmlBytes),
                comparison.SlotNumber,
                comparison.SourceVanillaCrc32,
                comparison.SourceVanillaLastWriteUtc);

            string manifestPath;
            string error;
            if (!TryWriteSlotManifest(scenarioId, comparison.SlotNumber, manifest, out manifestPath, out error))
            {
                MMLog.WriteWarning("[VanillaMirror] Failed to write mirror manifest for slot "
                    + comparison.SlotNumber + ": " + error);
            }

            SaveEntry entry = new SaveRegistryCore(scenarioId).GetSaveBySlot(comparison.SlotNumber);
            MMLog.WriteInfo("[VanillaMirror] Wrote Standard Slot_" + comparison.SlotNumber
                + " from vanilla " + comparison.SaveType
                + ". reason=" + (reason ?? "unspecified") + ".");
            return entry;
        }

        internal static void EnsureStandardVanillaMirrorManifest(VanillaMirrorComparisonResult comparison)
        {
            if (comparison == null
                || comparison.Status != VanillaMirrorComparisonStatus.InSync
                || comparison.SlotNumber < 1
                || comparison.SlotNumber > 3
                || comparison.VanillaXmlBytes == null)
            {
                return;
            }

            SlotManifest manifest = ReadSlotManifest(ScenarioSaveIdGuards.StandardStorageScenarioId, comparison.SlotNumber);
            bool needsWrite = manifest == null
                || !string.Equals(manifest.source, "vanilla-mirror", StringComparison.OrdinalIgnoreCase)
                || manifest.sourceSlot != comparison.SlotNumber
                || manifest.sourceVanillaCrc32 != comparison.SourceVanillaCrc32
                || !string.Equals(
                    manifest.sourceVanillaLastWriteUtc,
                    comparison.SourceVanillaLastWriteUtc.ToString("o", CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase);

            if (!needsWrite)
                return;

            SlotManifest updated = CreateVanillaMirrorManifest(
                ReadSaveInfoFromXml(comparison.VanillaXmlBytes),
                comparison.SlotNumber,
                comparison.SourceVanillaCrc32,
                comparison.SourceVanillaLastWriteUtc);

            string manifestPath;
            string error;
            if (!TryWriteSlotManifest(ScenarioSaveIdGuards.StandardStorageScenarioId, comparison.SlotNumber, updated, out manifestPath, out error))
            {
                MMLog.WriteWarning("[VanillaMirror] Failed to refresh mirror manifest for slot "
                    + comparison.SlotNumber + ": " + error);
            }
        }

        internal static bool TryWriteStandardVanillaMirrorManifestFromSave(
            int slotNumber,
            SaveInfo saveInfo,
            byte[] xmlBytes)
        {
            if (slotNumber < 1 || slotNumber > 3 || xmlBytes == null || xmlBytes.Length == 0)
                return false;

            string vanillaPath = GetVanillaSavePath(slotNumber);
            DateTime lastWriteUtc = !string.IsNullOrEmpty(vanillaPath) && File.Exists(vanillaPath)
                ? File.GetLastWriteTimeUtc(vanillaPath)
                : DateTime.UtcNow;

            SlotManifest manifest = CreateVanillaMirrorManifest(
                saveInfo ?? ReadSaveInfoFromXml(xmlBytes),
                slotNumber,
                CRC32.Compute(xmlBytes),
                lastWriteUtc);

            string manifestPath;
            string error;
            if (!TryWriteSlotManifest(ScenarioSaveIdGuards.StandardStorageScenarioId, slotNumber, manifest, out manifestPath, out error))
            {
                MMLog.WriteWarning("[VanillaMirror] Failed to write synchronized mirror manifest for slot "
                    + slotNumber + ": " + error);
                return false;
            }

            return true;
        }

        internal static bool IsStandardVanillaMirrorEntry(SaveManager.SaveType type, string scenarioId, SaveEntry entry)
        {
            VanillaSaveRoute route;
            return TryGetStandardVanillaMirrorRoute(type, scenarioId, entry, out route);
        }

        internal static bool TryGetStandardVanillaMirrorRoute(
            SaveManager.SaveType type,
            string scenarioId,
            SaveEntry entry,
            out VanillaSaveRoute route)
        {
            route = new VanillaSaveRoute();
            if (entry == null)
                return false;

            if (!string.Equals(
                SaveStorageRouter.NormalizeScenarioId(scenarioId),
                ScenarioSaveIdGuards.StandardStorageScenarioId,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (entry.absoluteSlot < 1 || entry.absoluteSlot > 3)
                return false;

            if (!VanillaSaveRouting.TryGetRoute(type, out route))
                return false;

            return route.VanillaSlotNumber == entry.absoluteSlot
                && string.Equals(route.StorageScenarioId, ScenarioSaveIdGuards.StandardStorageScenarioId, StringComparison.OrdinalIgnoreCase);
        }

        private static SlotManifest CreateVanillaMirrorManifest(
            SaveInfo info,
            int slotNumber,
            uint sourceVanillaCrc32,
            DateTime sourceVanillaLastWriteUtc)
        {
            SlotManifest manifest = SaveManifestFacts.CaptureCurrent(info);
            manifest.source = "vanilla-mirror";
            manifest.sourceSlot = slotNumber;
            manifest.sourceVanillaCrc32 = sourceVanillaCrc32;
            manifest.sourceVanillaLastWriteUtc = sourceVanillaLastWriteUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            return manifest;
        }

        private static void TryReadMirrorBytes(VanillaMirrorComparisonResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.MirrorPath) || !File.Exists(result.MirrorPath))
                return;

            try
            {
                result.MirrorXmlBytes = File.ReadAllBytes(result.MirrorPath);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        internal static void ImportStandardVanillaSlotsIfNeeded()
        {
            for (int slotNumber = 1; slotNumber <= 3; slotNumber++)
                ImportStandardVanillaSlotIfNeeded(slotNumber);
        }

        internal static SaveEntry ImportStandardVanillaSlotIfNeeded(int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > 3)
                return null;

            string scenarioId = ScenarioSaveIdGuards.StandardStorageScenarioId;
            string xmlPath = DirectoryProvider.EntryPath(scenarioId, slotNumber, false);
            VanillaMirrorComparisonResult comparison = CompareStandardVanillaMirror(slotNumber);
            if (comparison.Status == VanillaMirrorComparisonStatus.InSync)
            {
                EnsureStandardVanillaMirrorManifest(comparison);
                return new SaveRegistryCore(scenarioId).GetSaveBySlot(slotNumber);
            }

            if (comparison.Status == VanillaMirrorComparisonStatus.Diverged
                || comparison.Status == VanillaMirrorComparisonStatus.MissingVanilla)
            {
                return File.Exists(xmlPath)
                    ? new SaveRegistryCore(scenarioId).GetSaveBySlot(slotNumber)
                    : null;
            }

            if (comparison.Status != VanillaMirrorComparisonStatus.MissingMirror)
                return null;

            try
            {
                SaveEntry imported = WriteStandardVanillaMirrorFromVanilla(comparison, false, "missing-mirror-import");
                MMLog.WriteInfo("[VanillaImport] Imported vanilla slot " + slotNumber
                    + " into SMM Standard Slot_" + slotNumber + " and left the vanilla file untouched.");
                return imported;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[VanillaImport] Failed to import vanilla slot "
                    + slotNumber + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Reads the manifest.json for a specific slot.
        /// </summary>
        internal static SlotManifest ReadSlotManifest(string scenarioId, int absoluteSlot)
        {
            try
            {
                var slotRoot = DirectoryProvider.SlotRoot(scenarioId, absoluteSlot, false);
                var path = Path.Combine(slotRoot, "manifest.json");
                if (File.Exists(path))
                {
                    return DeserializeSlotManifest(File.ReadAllText(path));
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug($"Failed to read slot manifest for {scenarioId}/{absoluteSlot}: {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// Decrypts vanilla save data using the XOR cipher from PlatformSave_PC.
        /// </summary>
        private static byte[] DecryptVanillaSave(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return encryptedData;
            
            // XOR keys from PlatformSave_PC
            byte[] xorKey = new byte[] { 172, 242, 115, 58, 254, 222, 170, 33, 48, 13, 167, 21, 139, 109, 74, 186, 171 };
            byte[] xorOrder = new byte[] { 0, 2, 4, 1, 6, 15, 13, 16, 8, 3, 12, 10, 5, 9, 11, 7, 14 };
            
            byte[] decrypted = new byte[encryptedData.Length];
            int keyIndex = 0;
            
            for (int i = 0; i < encryptedData.Length; i++)
            {
                decrypted[i] = (byte)(encryptedData[i] ^ xorKey[xorOrder[keyIndex++]]);
                if (keyIndex >= xorOrder.Length)
                    keyIndex = 0;
            }
            
            return decrypted;
        }

        /// <summary>
        /// Checks if there are gaps in the save slot numbers.
        /// For Standard scenario, slots start at 4 (1-3 are vanilla).
        /// </summary>
        public bool HasGaps()
        {
            var entries = ListSaves();
            if (entries == null || entries.Length == 0) return false;

            int startSlot = (_scenarioId == "Standard") ? 4 : 1;
            var slots = new List<int>();
            foreach (var e in entries)
            {
                if (_scenarioId == "Standard" && e.absoluteSlot < 4) continue;
                slots.Add(e.absoluteSlot);
            }
            if (slots.Count == 0) 
            {
                MMLog.WriteDebug("HasGaps: No custom saves in manifest.");
                return false;
            }

            slots.Sort();
            MMLog.WriteDebug($"HasGaps: checking {slots.Count} saves. Scenario: {_scenarioId}");
            int expected = startSlot;
            foreach (var slot in slots)
            {
                if (slot != expected) 
                {
                    MMLog.WriteDebug($"HasGaps: GAP FOUND! Expected {expected}, found {slot}");
                    return true;
                }
                expected++;
            }
            MMLog.WriteDebug("HasGaps: No gaps found.");
            return false;
        }

        /// <summary>
        /// Runs the condense operation to close gaps in slot numbers.
        /// </summary>
        public void RunCondense()
        {
            CondenseSlots();
        }
    }

    /// <summary>
    /// Handles one-time startup check for save slot gaps and user preference for auto-condensing.
    /// </summary>
    internal static class SaveCondenseManager
    {
        private static bool _checked = false;
        private static bool _pendingPrompt = false;

        /// <summary>
        /// Checks for gaps in save slots at startup. If gaps exist and user preference is "ask",
        /// sets a flag to show a prompt when the main menu appears.
        /// </summary>
        public static void CheckOnStartup()
        {
            if (_checked) return;
            _checked = true;

            try
            {
                MMLog.WriteDebug("Starting startup gap check...");
                var registry = (SaveRegistryCore)ExpandedVanillaSaves.Instance;
                
                if (!registry.HasGaps())
                {
                    return;
                }

                var pref = ReadCondensePreference();
                MMLog.Write($"Gaps detected. User preference from INI: '{pref}'");

                if (pref == "yes" || pref == "true")
                {
                    MMLog.Write("Auto-condensing saves (user preference: yes).");
                    registry.RunCondense();
                }
                else if (pref == "no" || pref == "false")
                {
                    MMLog.Write("Skipping condense (user preference: no).");
                }
                else
                {
                    MMLog.Write("Preference is 'ask'. Flagging for prompt on Main Menu.");
                    _pendingPrompt = true;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"Error during startup check: {ex}");
            }
        }

        /// <summary>
        /// Returns true if the user needs to be prompted about condensing.
        /// </summary>
        public static bool NeedsPrompt() => _pendingPrompt;

        /// <summary>
        /// Called when user makes a choice in the prompt dialog.
        /// </summary>
        public static void OnUserChoice(bool condense, bool remember)
        {
            _pendingPrompt = false;

            if (remember)
            {
                WriteCondensePreference(condense ? "yes" : "no");
            }

            if (condense)
            {
                try
                {
                    var registry = (SaveRegistryCore)ExpandedVanillaSaves.Instance;
                    registry.RunCondense();
                    MMLog.Write("Condensed saves per user request.");
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"Error condensing: {ex}");
                }
            }
        }

        private static string ReadCondensePreference()
        {
            try
            {
                var ini = DirectoryProvider.ConfigPath;
                if (!File.Exists(ini)) return "ask";

                foreach (var raw in File.ReadAllLines(ini))
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    var line = raw.Trim();
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var k = line.Substring(0, idx).Trim();
                    var v = line.Substring(idx + 1).Trim().ToLowerInvariant();
                    if (k.Equals("AutoCondenseSaves", StringComparison.OrdinalIgnoreCase))
                    {
                        MMLog.Write($"Read preference: '{v}'");
                        return v;
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug($"Error reading preference: {ex.Message}");
            }
            
            MMLog.Write($"Read preference: 'ask' (default)");
            return "ask";
        }

        private static void WriteCondensePreference(string value)
        {
            try
            {
                var ini = DirectoryProvider.ConfigPath;
                var smmDir = DirectoryProvider.SmmRoot;
                
                if (!Directory.Exists(smmDir))
                    Directory.CreateDirectory(smmDir);

                var lines = new List<string>();
                bool found = false;

                if (File.Exists(ini))
                {
                    foreach (var raw in File.ReadAllLines(ini))
                    {
                        var line = raw.Trim();
                        if (line.StartsWith("AutoCondenseSaves", StringComparison.OrdinalIgnoreCase))
                        {
                            lines.Add($"AutoCondenseSaves={value}");
                            found = true;
                        }
                        else
                        {
                            lines.Add(raw);
                        }
                    }
                }

                if (!found)
                    lines.Add($"AutoCondenseSaves={value}");

                File.WriteAllLines(ini, lines.ToArray());
                MMLog.WriteDebug($"Saved preference: AutoCondenseSaves={value}");
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"Error writing preference: {ex}");
            }
        }
    }
}
