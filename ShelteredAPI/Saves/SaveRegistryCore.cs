using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Util;
using ShelteredAPI.Saves.Backups;

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
            var now = DateTime.UtcNow.ToString("o");
            var entry = new SaveEntry
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

            // Ensure slot directory exists
            DirectoryProvider.SlotRoot(_scenarioId, normalized.absoluteSlot, true);
            
            InvalidateCache();
            return entry;
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
            if (entry == null) return null;

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
            var currentMods = new List<LoadedModInfo>();

            foreach (var mod in ModRuntime.GetLoadedModsSnapshot())
            {
                if (mod == null) continue;

                string warning = mod.About?.missingModWarning;
                currentMods.Add(new LoadedModInfo
                {
                    modId = mod.Id,
                    version = mod.Version,
                    warnings = string.IsNullOrEmpty(warning) ? new string[0] : new string[] { warning }
                });
            }

            return new SlotManifest
            {
                lastModified = DateTime.UtcNow.ToString("o"),
                family_name = info != null ? info.familyName : "Unknown",
                lastLoadedMods = currentMods.ToArray()
            };
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
            var path = DirectoryProvider.EntryPath(_scenarioId, absoluteSlot);
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
            result.lastLoadedMods = ReadLoadedMods(root.GetArray("lastLoadedMods"));
            return result;
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
