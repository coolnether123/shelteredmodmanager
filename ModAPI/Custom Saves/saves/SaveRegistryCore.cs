using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Saves
{
    /// <summary>
    /// Provides central logic for managing mod-aware save files, including manifest discovery and verification.
    /// </summary>
    internal class SaveRegistryCore : ISaveApi
    {
        private readonly object _lock = new object();
        private readonly string _scenarioId;
        private SaveManifest _manifest;

        public SaveRegistryCore(string scenarioId)
        {
            this._scenarioId = scenarioId;
        }

        // --- ISaveApi Implementation ---
        public SaveEntry Get(string saveId) => GetSave(saveId);
        public SaveEntry Overwrite(string saveId, SaveOverwriteOptions opts, byte[] xmlBytes) => OverwriteSave(saveId, opts, xmlBytes);


        public SaveEntry[] ListSaves()
        {
            return LoadManifest().entries ?? new SaveEntry[0];
        }

        public SaveEntry[] ListSaves(int page, int pageSize)
        {
            var all = LoadManifest().entries;
            if (all == null) return new SaveEntry[0];
            int start = Math.Max(0, page * pageSize);
            if (start >= all.Length) return new SaveEntry[0];
            int count = Math.Min(pageSize, all.Length - start);
            var result = new SaveEntry[count];
            Array.Copy(all, start, result, 0, count);
            MMLog.WriteDebug($"ListSaves scenario={_scenarioId} page={page} size={pageSize} returned={result.Length}");
            return result;
        }

        public SaveEntry GetSave(string saveId)
        {
            foreach (var e in LoadManifest().entries)
                if (e.id == saveId) return e;
            return null;
        }

        public int CountSaves()
        {
            var m = LoadManifest();
            return m.entries != null ? m.entries.Length : 0;
        }

        public SaveEntry CreateSave(SaveCreateOptions opts)
        {
            var m = LoadManifest();
            var id = IdGenerator.NewId();
            var now = DateTime.UtcNow.ToString("o");
            var entry = new SaveEntry
            {
                id = id,
                absoluteSlot = opts.absoluteSlot,
                name = UniqueName(m, NameSanitizer.SanitizeName(opts?.name)),
                createdAt = now,
                updatedAt = now,
                gameVersion = Application.version,
                modApiVersion = "1",
                scenarioId = _scenarioId,
                scenarioVersion = ScenarioRegistry.GetScenario(_scenarioId).version,
                saveInfo = new SaveInfo()
            };

            var list = new List<SaveEntry>(m.entries ?? new SaveEntry[0]);
            list.Add(entry);
            m.entries = list.ToArray();

            SaveManifestFile(m);
            MMLog.WriteDebug($"CreateSave scenario={_scenarioId} name='{entry.name}' id={entry.id}");
            return entry;
        }

        public SaveEntry OverwriteSave(string saveId, SaveOverwriteOptions opts, byte[] xmlBytes)
        {
            var m = LoadManifest();
            for (int i = 0; i < m.entries.Length; i++)
            {
                if (m.entries[i].id == saveId)
                {
                    var entry = m.entries[i];
                    if (opts != null && !string.IsNullOrEmpty(opts.name)) entry.name = NameSanitizer.SanitizeName(opts.name);
                    entry.updatedAt = DateTime.UtcNow.ToString("o");
                    if (xmlBytes != null)
                    {
                        WriteEntryFile(entry.absoluteSlot, xmlBytes, out long size, out uint crc);
                        entry.fileSize = size;
                        entry.crc32 = crc;
                        TryUpdateEntryInfo(entry, xmlBytes);
                        
                        // Update per-slot manifest (Mod List)
                        UpdateSlotManifest(entry.absoluteSlot, entry.saveInfo);
                    }
                    m.entries[i] = entry;
                    SaveManifestFile(m);
                    MMLog.WriteDebug($"OverwriteSave scenario={_scenarioId} id={saveId} slot={entry.absoluteSlot} size={entry.fileSize}");
                    return entry;
                }
            }
            return null;
        }

        public bool DeleteSave(string saveId)
        {
            var m = LoadManifest();
            var list = new List<SaveEntry>(m.entries);
            var idx = list.FindIndex(e => e.id == saveId);
            if (idx < 0) return false;
            var entry = list[idx];
            list.RemoveAt(idx);
            m.entries = list.ToArray();
            SaveManifestFile(m);
            try { File.Delete(DirectoryProvider.EntryPath(_scenarioId, entry.absoluteSlot)); } catch { }
            try { File.Delete(DirectoryProvider.PreviewPath(_scenarioId, saveId)); } catch { }
            
            // Delete manifest.json
            try 
            {
                var slotRoot = DirectoryProvider.SlotRoot(_scenarioId, entry.absoluteSlot);
                var manPath = Path.Combine(slotRoot, "manifest.json");
                if (File.Exists(manPath)) File.Delete(manPath);
            } 
            catch { }

            MMLog.WriteDebug($"DeleteSave scenario={_scenarioId} id={saveId} slot={entry.absoluteSlot}");
            return true;
        }

        internal void UpdateSlotManifest(int absoluteSlot, SaveInfo info)
        {
            try
            {
                var slotRoot = DirectoryProvider.SlotRoot(_scenarioId, absoluteSlot);
                var path = Path.Combine(slotRoot, "manifest.json");

                var currentMods = new List<LoadedModInfo>();
                var loaded = PluginManager.LoadedMods;
                
                if (loaded == null)
                {
                    MMLog.WriteError("[SaveRegistryCore] PluginManager.LoadedMods is NULL!");
                }
                else
                {
                    MMLog.Write($"[SaveRegistryCore] UpdateSlotManifest: Gathering {loaded.Count} active mods.");
                    foreach (var mod in loaded)
                    {
                        if (mod == null) { MMLog.WriteError("[SaveRegistryCore] Found null mod entry in LoadedMods!"); continue; }
                        
                        string warning = mod.About?.missingModWarning;
                        currentMods.Add(new LoadedModInfo 
                        { 
                            modId = mod.Id, 
                            version = mod.Version, 
                            warnings = string.IsNullOrEmpty(warning) ? new string[0] : new string[] { warning }
                        });
                        MMLog.WriteDebug($"[SaveRegistryCore]   - Active: {mod.Id} v{mod.Version}");
                    }
                }

                SlotManifest existing = null;
                if (File.Exists(path))
                {
                    try { existing = DeserializeSlotManifest(File.ReadAllText(path)); } catch { }
                }

                bool changed = true;
                if (existing != null && existing.lastLoadedMods != null)
                {
                    // Compare mods
                    if (existing.lastLoadedMods.Length == currentMods.Count)
                    {
                        bool match = true;
                        for(int i=0; i<currentMods.Count; i++)
                        {
                            var a = existing.lastLoadedMods[i];
                            var b = currentMods[i];
                            if (a.modId != b.modId || a.version != b.version)
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match) changed = false;
                    }
                }

                if (changed || existing == null)
                {
                    var newManifest = new SlotManifest
                    {
                        lastModified = DateTime.UtcNow.ToString("o"),
                        family_name = info != null ? info.familyName : "Unknown",
                        lastLoadedMods = currentMods.ToArray()
                    };
                    string slotJson = SerializeSlotManifest(newManifest);
                    MMLog.Write($"[SaveRegistryCore] SLOT {absoluteSlot} Manifest JSON to write:\n{slotJson}");
                    MMLog.Write($"[SaveRegistryCore] SLOT {absoluteSlot} currentMods.Count = {currentMods.Count}, newManifest.lastLoadedMods.Length = {newManifest.lastLoadedMods.Length}");
                    File.WriteAllText(path, slotJson);
                    MMLog.WriteDebug($"[SaveRegistryCore] Updated manifest.json for slot {absoluteSlot} (Mods Changed)");
                }
                else
                {
                    // Mod configuration is identical to the existing manifest; skipping update.
                    // To strictly adhere to requirements, manifest metadata (including 'lastModified')
                    // is only synchronized when the active mod list changes.
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveRegistryCore] Failed to update slot manifest for slot {absoluteSlot}: {ex}");
            }
        }

        private SaveManifest LoadManifest()
        {
            lock (_lock)
            {
                if (_manifest != null) return _manifest;

                var path = DirectoryProvider.ManifestPath(_scenarioId);
                try
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        _manifest = JsonUtility.FromJson<SaveManifest>(json) ?? new SaveManifest();
                    }
                    else
                    {
                        _manifest = new SaveManifest();
                    }
                }
                catch (Exception ex)
                {
                    MMLog.Write($"Manifest load error for '{_scenarioId}': " + ex.Message);
                    _manifest = new SaveManifest();
                }

                // Auto-discovery: Reconcile manifest with actual files on disk
                ReconcileManifestWithSlots(_manifest);

                return _manifest;
            }
        }

        private void ReconcileManifestWithSlots(SaveManifest m)
        {
            try
            {
                var scenarioRoot = DirectoryProvider.ScenarioRoot(_scenarioId);
                if (!Directory.Exists(scenarioRoot)) return;

                var dirs = Directory.GetDirectories(scenarioRoot, "Slot_*");
                var entries = new List<SaveEntry>(m.entries ?? new SaveEntry[0]);
                bool changed = false;

                foreach (var dir in dirs)
                {
                    var dirName = Path.GetFileName(dir);
                    var numPart = dirName.Substring(5); // "Slot_" is 5 chars
                    if (int.TryParse(numPart, out int absoluteSlot))
                    {
                        var savePath = Path.Combine(dir, "SaveData.xml");
                        if (File.Exists(savePath))
                        {
                            // Check if this slot is already known
                            var existing = entries.Find(e => e.absoluteSlot == absoluteSlot);
                            if (existing == null)
                            {
                                MMLog.Write($"[SaveRegistryCore] Discovered orphaned save in {dirName}. Adding to manifest.");
                                
                                // Create new entry
                                var newEntry = new SaveEntry
                                {
                                    id = IdGenerator.NewId(),
                                    absoluteSlot = absoluteSlot,
                                    name = $"Slot {absoluteSlot}",
                                    createdAt = File.GetCreationTimeUtc(savePath).ToString("o"),
                                    updatedAt = File.GetLastWriteTimeUtc(savePath).ToString("o"),
                                    scenarioId = _scenarioId,
                                    saveInfo = new SaveInfo()
                                };

                                // Parse metadata
                                try
                                {
                                    var bytes = File.ReadAllBytes(savePath);
                                    newEntry.fileSize = bytes.Length;
                                    newEntry.crc32 = CRC32.Compute(bytes);
                                    TryUpdateEntryInfo(newEntry, bytes);
                                }
                                catch (Exception ex)
                                {
                                    MMLog.WriteError($"Error parsing discovered save in {dirName}: " + ex.Message);
                                }

                                entries.Add(newEntry);
                                changed = true;
                            }
                        }
                    }
                }

                if (changed)
                {
                    entries.Sort((a, b) => a.absoluteSlot.CompareTo(b.absoluteSlot));
                    m.entries = entries.ToArray();
                    SaveManifestFile(m);
                }

                CondenseSlots(m); // Auto-condense after discovery
            }
            catch (Exception ex) 
            {
                MMLog.WriteError("[SaveRegistryCore] Error reconciling slots: " + ex.Message);
            }
        }

        private void CondenseSlots(SaveManifest m)
        {
            if (m.entries == null || m.entries.Length == 0) return;

            // Determines starting slot based on scenario type
            int expectedSlot = (_scenarioId == "Standard") ? 4 : 1;
            
            var sorted = new List<SaveEntry>(m.entries);
            sorted.Sort((a, b) => a.absoluteSlot.CompareTo(b.absoluteSlot));

            bool changed = false;
            foreach (var entry in sorted)
            {
                // If this entry is in a reserved slot (e.g. 1-3 for Standard), skip it? 
                // Or if it's somehow there, should we move it to 4?
                // Assuming we only condense valid custom slots.
                if (_scenarioId == "Standard" && entry.absoluteSlot < 4) continue;

                if (entry.absoluteSlot > expectedSlot)
                {
                    // Move it!
                    bool success = false;
                    try
                    {
                        var oldDir = DirectoryProvider.SlotRoot(_scenarioId, entry.absoluteSlot);
                        var newDir = DirectoryProvider.SlotRoot(_scenarioId, expectedSlot);

                        // DirectoryProvider creates dir if not exists, so newDir might exist empty.
                        if (Directory.Exists(newDir))
                        {
                            // If empty, delete it to allow move
                            if (Directory.GetFiles(newDir).Length == 0 && Directory.GetDirectories(newDir).Length == 0)
                            {
                                Directory.Delete(newDir);
                            }
                            else
                            {
                                // Collision! Skip this target slot.
                                MMLog.WriteError($"[CondenseSlots] Cannot move {entry.absoluteSlot} to {expectedSlot} because target exists and is not empty.");
                                expectedSlot++; 
                                continue; 
                            }
                        }

                        // Just in case DirectoryProvider didn't create it or we deleted it:
                         if (Directory.Exists(newDir)) Directory.Delete(newDir);

                        Directory.Move(oldDir, newDir);
                        MMLog.Write($"[CondenseSlots] Moved save from Slot {entry.absoluteSlot} to Slot {expectedSlot}");
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError($"[CondenseSlots] Failed to move slot {entry.absoluteSlot} to {expectedSlot}: {ex.Message}");
                    }
                    
                    if (success)
                    {
                        entry.absoluteSlot = expectedSlot;
                        changed = true;
                        expectedSlot++;
                    }
                    // If move failed, keep entry.absoluteSlot as-is and don't modify manifest
                }
                else if (entry.absoluteSlot == expectedSlot)
                {
                    expectedSlot++;
                }
            }

            if (changed)
            {
                m.entries = sorted.ToArray();
                SaveManifestFile(m);
            }
        }

        private void SaveManifestFile(SaveManifest m)
        {
            lock (_lock)
            {
                _manifest = m;
                try
                {
                    var path = DirectoryProvider.ManifestPath(_scenarioId);
                    MMLog.Write($"[SaveRegistryCore] Saving manifest file to: {path}");
                    var tmp = path + ".tmp";
                    var json = JsonUtility.ToJson(m, true);
                    MMLog.Write($"[SaveRegistryCore] Writing manifest JSON: {json}");

                    File.WriteAllText(tmp, json, Encoding.UTF8);

                    try { File.Replace(tmp, path, null); }
                    catch { File.Copy(tmp, path, true); File.Delete(tmp); }
                }
                catch (Exception ex)
                {
                    MMLog.Write($"Manifest save error for '{_scenarioId}': " + ex.Message);
                }
            }
        }

        private void WriteEntryFile(int absoluteSlot, byte[] xmlBytes, out long fileSize, out uint crc)
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
            }
            catch (Exception ex)
            {
                MMLog.Write($"WriteEntryFile error for Slot_{absoluteSlot}': " + ex.Message);
            }
        }

        private static void TryUpdateEntryInfo(SaveEntry entry, byte[] xmlBytes)
        {
            try
            {
                var sd = new SaveData(xmlBytes);
                var info = sd.info;
                MMLog.Write($"[TryUpdateEntryInfo] Extracted Info: Family='{info.m_familyName}', Days={info.m_daysSurvived}, Difficulty={info.m_diffSetting}, Time='{info.m_saveTime}'");
                entry.saveInfo.daysSurvived = info.m_daysSurvived;
                entry.saveInfo.difficulty = info.m_diffSetting;
                entry.saveInfo.familyName = info.m_familyName;
                entry.saveInfo.saveTime = info.m_saveTime;
                sd.Finished();
                MMLog.Write($"[TryUpdateEntryInfo] Successfully updated SaveEntry '{entry.id}'.");
            }
            catch (Exception ex)
            {
                MMLog.Write("[TryUpdateEntryInfo] CRITICAL parse error: " + ex);
            }
        }

        private static string UniqueName(SaveManifest m, string name)
        {
            if (string.IsNullOrEmpty(name)) name = "Unnamed";
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in m.entries) if (!string.IsNullOrEmpty(e.name)) existing.Add(e.name);
            if (!existing.Contains(name)) return name;
            int i = 2;
            string candidate;
            do { candidate = name + " (" + i++ + ")"; } while (existing.Contains(candidate));
            return candidate;
        }

        private static string SerializeSlotManifest(SlotManifest manifest)
        {
            // Use Unity's JsonUtility for consistent serialization/deserialization
            // Manual StringBuilder approach was prone to formatting errors that JsonUtility.FromJson dislikes
            return JsonUtility.ToJson(manifest, true);
        }


        /// <summary>
        /// Deserializes a SlotManifest from JSON.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: Unity's JsonUtility.FromJson fails to deserialize arrays of objects when 
        /// they are written in compact/inline format like: { "modId": "...", "version": "..." }
        /// 
        /// This happens because early versions used StringBuilder to serialize manifests with 
        /// inline object formatting. JsonUtility is extremely strict about whitespace and only
        /// reliably deserializes the multi-line format it produces itself.
        /// 
        /// To maintain backward compatibility with existing save files, we manually parse the
        /// lastLoadedMods array while letting JsonUtility handle simple scalar fields.
        /// </remarks>
        internal static SlotManifest DeserializeSlotManifest(string json)
        {
            if (string.IsNullOrEmpty(json)) return new SlotManifest();
            
            var result = new SlotManifest();
            
            try
            {
                // Parse basic fields with JsonUtility (it handles these fine)
                result = JsonUtility.FromJson<SlotManifest>(json) ?? new SlotManifest();
                
                // Manual parse for lastLoadedMods array since JsonUtility struggles with inline objects
                var mods = new List<LoadedModInfo>();
                
                // Find the lastLoadedMods array content
                int arrayStart = json.IndexOf("\"lastLoadedMods\"");
                if (arrayStart >= 0)
                {
                    int bracketStart = json.IndexOf('[', arrayStart);
                    if (bracketStart >= 0)
                    {
                        // Find matching closing bracket by counting depth (handles nested [] in warnings)
                        int bracketDepth = 0;
                        int bracketEnd = -1;
                        for (int i = bracketStart; i < json.Length; i++)
                        {
                            if (json[i] == '[') bracketDepth++;
                            else if (json[i] == ']')
                            {
                                bracketDepth--;
                                if (bracketDepth == 0)
                                {
                                    bracketEnd = i;
                                    break;
                                }
                            }
                        }
                        
                        if (bracketEnd > bracketStart)
                        {
                            string arrayContent = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                            
                            // Find each object {...} in the array (also count depth to handle nested objects)
                            int depth = 0;
                            int objStart = -1;
                            for (int i = 0; i < arrayContent.Length; i++)
                            {
                                char c = arrayContent[i];
                                if (c == '{')
                                {
                                    if (depth == 0) objStart = i;
                                    depth++;
                                }
                                else if (c == '}')
                                {
                                    depth--;
                                    if (depth == 0 && objStart >= 0)
                                    {
                                        string objJson = arrayContent.Substring(objStart, i - objStart + 1);
                                        var mod = ParseLoadedModInfo(objJson);
                                        if (mod != null) mods.Add(mod);
                                        objStart = -1;
                                    }
                                }
                            }
                        }
                    }
                }
                
                result.lastLoadedMods = mods.ToArray();
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[DeserializeSlotManifest] Parse error: {ex.Message}");
            }
            
            return result;
        }
        
        private static LoadedModInfo ParseLoadedModInfo(string objJson)
        {
            try
            {
                var info = new LoadedModInfo();
                
                // Extract modId
                info.modId = ExtractJsonStringValue(objJson, "modId");
                
                // Extract version
                info.version = ExtractJsonStringValue(objJson, "version");
                
                // Extract warnings array (simple case - just get string values)
                var warnings = new List<string>();
                int warningsStart = objJson.IndexOf("\"warnings\"");
                if (warningsStart >= 0)
                {
                    int arrStart = objJson.IndexOf('[', warningsStart);
                    int arrEnd = objJson.IndexOf(']', arrStart);
                    if (arrStart >= 0 && arrEnd > arrStart)
                    {
                        string arrContent = objJson.Substring(arrStart + 1, arrEnd - arrStart - 1).Trim();
                        if (!string.IsNullOrEmpty(arrContent))
                        {
                            // Simple string array parsing
                            var parts = arrContent.Split(',');
                            foreach (var part in parts)
                            {
                                string trimmed = part.Trim().Trim('"');
                                if (!string.IsNullOrEmpty(trimmed)) warnings.Add(trimmed);
                            }
                        }
                    }
                }
                info.warnings = warnings.ToArray();
                
                return info;
            }
            catch
            {
                return null;
            }
        }
        
        private static string ExtractJsonStringValue(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            int keyIndex = json.IndexOf(pattern);
            if (keyIndex < 0) return "";
            
            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return "";
            
            // Find the opening quote
            int quoteStart = json.IndexOf('"', colonIndex);
            if (quoteStart < 0) return "";
            
            // Find the closing quote (handle escaped quotes)
            int quoteEnd = quoteStart + 1;
            while (quoteEnd < json.Length)
            {
                if (json[quoteEnd] == '"' && json[quoteEnd - 1] != '\\')
                    break;
                quoteEnd++;
            }
            
            if (quoteEnd >= json.Length) return "";
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        /// <summary>
        /// Reads SaveInfo from a save file's XML data.
        /// Extracts familyName, daysSurvived, and difficulty from the SaveInfo group.
        /// </summary>
        public static SaveInfo ReadSaveInfoFromXml(byte[] xmlBytes)
        {
            var info = new SaveInfo();
            if (xmlBytes == null || xmlBytes.Length == 0) return info;

            try
            {
                var sd = new SaveData(xmlBytes);
                var gameInfo = sd.info;
                info.familyName = gameInfo.m_familyName;
                info.daysSurvived = gameInfo.m_daysSurvived;
                info.difficulty = gameInfo.m_diffSetting;
                info.saveTime = gameInfo.m_saveTime;
                sd.Finished();
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveRegistryCore] Failed to read SaveInfo from XML: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Reads SaveInfo from a vanilla save slot (1-3).
        /// Handles the XOR decryption used by the game's PlatformSave_PC.
        /// </summary>
        public static SaveInfo ReadVanillaSaveInfo(int slotNumber)
        {
            var info = new SaveInfo();
            
            try
            {
                // Get the vanilla save path (same as PlatformSave_PC.GetSavePath)
                string savesPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "saves");
                string fileName;
                
                switch (slotNumber)
                {
                    case 1: fileName = "savedata_01.dat"; break;
                    case 2: fileName = "savedata_02.dat"; break;
                    case 3: fileName = "savedata_03.dat"; break;
                    default: return info;
                }
                
                string fullPath = Path.Combine(savesPath, fileName);
                MMLog.WriteDebug($"[SaveRegistryCore] Looking for vanilla save at: {fullPath}");
                
                if (!File.Exists(fullPath))
                {
                    MMLog.WriteDebug($"[SaveRegistryCore] Vanilla save file not found: {fullPath}");
                    return info;
                }
                
                // Read and decrypt the file (XOR cipher from PlatformSave_PC)
                byte[] encryptedData = File.ReadAllBytes(fullPath);
                byte[] decryptedData = DecryptVanillaSave(encryptedData);
                
                // Parse the decrypted XML
                info = ReadSaveInfoFromXml(decryptedData);
                MMLog.WriteDebug($"[SaveRegistryCore] Read vanilla slot {slotNumber}: family='{info.familyName}', days={info.daysSurvived}");
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveRegistryCore] Failed to read vanilla save info for slot {slotNumber}: {ex.Message}");
            }
            
            return info;
        }

        /// <summary>
        /// Reads the manifest.json for a specific slot.
        /// </summary>
        internal static SlotManifest ReadSlotManifest(string scenarioId, int absoluteSlot)
        {
            try
            {
                var slotRoot = DirectoryProvider.SlotRoot(scenarioId, absoluteSlot);
                var path = Path.Combine(slotRoot, "manifest.json");
                if (File.Exists(path))
                {
                    return DeserializeSlotManifest(File.ReadAllText(path));
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug($"[SaveRegistryCore] Failed to read slot manifest for {scenarioId}/{absoluteSlot}: {ex.Message}");
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
    }
}