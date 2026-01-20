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

        public int GetMaxSlot()
        {
            var m = LoadManifest();
            if (m.entries == null || m.entries.Length == 0) return 0;
            int max = 0;
            foreach (var e in m.entries)
            {
                if (e.absoluteSlot > max) max = e.absoluteSlot;
            }
            return max;
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
                var slotRoot = DirectoryProvider.SlotRoot(_scenarioId, entry.absoluteSlot, false);
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

                // Always update the manifest to ensure metadata (family, days, timestamp) and mod list are current.
                var newManifest = new SlotManifest
                {
                    lastModified = DateTime.UtcNow.ToString("o"),
                    family_name = info != null ? info.familyName : "Unknown",
                    lastLoadedMods = currentMods.ToArray()
                };

                string slotJson = SerializeSlotManifest(newManifest);
                MMLog.WriteDebug($"[SaveRegistryCore] Updating manifest for Slot {absoluteSlot}. Mods: {currentMods.Count}. JSON:\n{slotJson}");
                File.WriteAllText(path, slotJson);
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
                        // IMPORTANT: Unity's JsonUtility.FromJson() cannot deserialize arrays of custom classes
                        // like SaveEntry[]. It silently returns an empty array. We must use custom parsing.
                        // See: https://docs.unity3d.com/Manual/JSONSerialization.html
                        _manifest = DeserializeManifest(json) ?? new SaveManifest();
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
                        var oldDir = DirectoryProvider.SlotRoot(_scenarioId, entry.absoluteSlot, false);
                        var newDir = DirectoryProvider.SlotRoot(_scenarioId, expectedSlot, false);

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
                    var tmp = path + ".tmp";
                    
                    // Manual JSON serialization - Unity's JsonUtility cannot serialize arrays of custom classes
                    var json = SerializeManifest(m);

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
        
        /// <summary>
        /// Manually serialize SaveManifest to JSON.
        /// 
        /// UNITY LIMITATION: Unity's JsonUtility.ToJson() CANNOT serialize arrays of custom classes.
        /// When you call JsonUtility.ToJson() on a class containing SaveEntry[] (array of custom objects),
        /// it silently outputs an empty array [], losing all save data. This is a known Unity limitation.
        /// See: https://docs.unity3d.com/Manual/JSONSerialization.html
        /// 
        /// This method manually constructs JSON to ensure all SaveEntry and SaveInfo data is preserved.
        /// </summary>
        private static string SerializeManifest(SaveManifest m)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"version\": {m.version},");
            sb.AppendLine("    \"entries\": [");
            
            for (int i = 0; i < m.entries.Length; i++)
            {
                var e = m.entries[i];
                sb.AppendLine("        {");
                sb.AppendLine($"            \"id\": \"{EscapeJson(e.id ?? "")}\",");
                sb.AppendLine($"            \"absoluteSlot\": {e.absoluteSlot},");
                sb.AppendLine($"            \"name\": \"{EscapeJson(e.name ?? "")}\",");
                sb.AppendLine($"            \"createdAt\": \"{EscapeJson(e.createdAt ?? "")}\",");
                sb.AppendLine($"            \"updatedAt\": \"{EscapeJson(e.updatedAt ?? "")}\",");
                sb.AppendLine($"            \"gameVersion\": \"{EscapeJson(e.gameVersion ?? "")}\",");
                sb.AppendLine($"            \"modApiVersion\": \"{EscapeJson(e.modApiVersion ?? "")}\",");
                sb.AppendLine($"            \"scenarioId\": \"{EscapeJson(e.scenarioId ?? "")}\",");
                sb.AppendLine($"            \"scenarioVersion\": \"{EscapeJson(e.scenarioVersion ?? "")}\",");
                sb.AppendLine($"            \"fileSize\": {e.fileSize},");
                sb.AppendLine($"            \"crc32\": {e.crc32},");
                sb.AppendLine($"            \"previewPath\": \"{EscapeJson(e.previewPath ?? "")}\",");
                sb.AppendLine($"            \"extra\": \"{EscapeJson(e.extra ?? "")}\",");
                sb.AppendLine("            \"saveInfo\": {");
                sb.AppendLine($"                \"daysSurvived\": {e.saveInfo?.daysSurvived ?? 0},");
                sb.AppendLine($"                \"difficulty\": {e.saveInfo?.difficulty ?? 1},");
                sb.AppendLine($"                \"fog\": {(e.saveInfo?.fog ?? false).ToString().ToLower()},");
                sb.AppendLine($"                \"mapSize\": {e.saveInfo?.mapSize ?? 0},");
                sb.AppendLine($"                \"rainDiff\": {e.saveInfo?.rainDiff ?? 1},");
                sb.AppendLine($"                \"resourceDiff\": {e.saveInfo?.resourceDiff ?? 1},");
                sb.AppendLine($"                \"breachDiff\": {e.saveInfo?.breachDiff ?? 1},");
                sb.AppendLine($"                \"factionDiff\": {e.saveInfo?.factionDiff ?? 1},");
                sb.AppendLine($"                \"moodDiff\": {e.saveInfo?.moodDiff ?? 1},");
                sb.AppendLine($"                \"familyName\": \"{EscapeJson(e.saveInfo?.familyName ?? "")}\",");
                sb.AppendLine($"                \"saveTime\": \"{EscapeJson(e.saveInfo?.saveTime ?? "")}\"");
                sb.AppendLine("            }");
                sb.Append("        }");
                if (i < m.entries.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            
            sb.AppendLine("    ]");
            sb.AppendLine("}");
            return sb.ToString();
        }
        
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        /// <summary>
        /// Manually deserialize SaveManifest from JSON.
        /// 
        /// UNITY LIMITATION: Unity's JsonUtility.FromJson() CANNOT deserialize arrays of custom classes.
        /// When parsing JSON with SaveEntry[] (array of custom objects), JsonUtility returns an empty array,
        /// even when the JSON contains valid data. This is a known Unity limitation.
        /// See: https://docs.unity3d.com/Manual/JSONSerialization.html
        /// 
        /// This method manually parses JSON to extract all SaveEntry and SaveInfo data.
        /// </summary>
        private static SaveManifest DeserializeManifest(string json)
        {
            var result = new SaveManifest();
            if (string.IsNullOrEmpty(json)) return result;
            
            try
            {
                // Parse version
                result.version = ParseIntField(json, "version", 1);
                
                // Parse entries array
                var entries = new List<SaveEntry>();
                int entriesStart = json.IndexOf("\"entries\"");
                if (entriesStart >= 0)
                {
                    int arrayStart = json.IndexOf('[', entriesStart);
                    if (arrayStart >= 0)
                    {
                        int arrayEnd = FindMatchingBracket(json, arrayStart, '[', ']');
                        if (arrayEnd > arrayStart)
                        {
                            string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                            // Parse each entry object
                            int pos = 0;
                            while (pos < arrayContent.Length)
                            {
                                int objStart = arrayContent.IndexOf('{', pos);
                                if (objStart < 0) break;
                                int objEnd = FindMatchingBracket(arrayContent, objStart, '{', '}');
                                if (objEnd <= objStart) break;
                                
                                string entryJson = arrayContent.Substring(objStart, objEnd - objStart + 1);
                                var entry = ParseSaveEntry(entryJson);
                                if (entry != null) entries.Add(entry);
                                pos = objEnd + 1;
                            }
                        }
                    }
                }
                result.entries = entries.ToArray();
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveRegistryCore] DeserializeManifest error: {ex.Message}");
            }
            
            return result;
        }
        
        private static SaveEntry ParseSaveEntry(string json)
        {
            var entry = new SaveEntry();
            entry.id = ParseStringField(json, "id");
            entry.absoluteSlot = ParseIntField(json, "absoluteSlot", 0);
            entry.name = ParseStringField(json, "name");
            entry.createdAt = ParseStringField(json, "createdAt");
            entry.updatedAt = ParseStringField(json, "updatedAt");
            entry.gameVersion = ParseStringField(json, "gameVersion");
            entry.modApiVersion = ParseStringField(json, "modApiVersion");
            entry.scenarioId = ParseStringField(json, "scenarioId");
            entry.scenarioVersion = ParseStringField(json, "scenarioVersion");
            entry.fileSize = ParseLongField(json, "fileSize", 0);
            entry.crc32 = (uint)ParseLongField(json, "crc32", 0);
            entry.previewPath = ParseStringField(json, "previewPath");
            entry.extra = ParseStringField(json, "extra");
            
            // Parse nested saveInfo
            int saveInfoStart = json.IndexOf("\"saveInfo\"");
            if (saveInfoStart >= 0)
            {
                int objStart = json.IndexOf('{', saveInfoStart);
                if (objStart >= 0)
                {
                    int objEnd = FindMatchingBracket(json, objStart, '{', '}');
                    if (objEnd > objStart)
                    {
                        string infoJson = json.Substring(objStart, objEnd - objStart + 1);
                        entry.saveInfo = ParseSaveInfo(infoJson);
                    }
                }
            }
            
            return entry;
        }
        
        private static SaveInfo ParseSaveInfo(string json)
        {
            var info = new SaveInfo();
            info.daysSurvived = ParseIntField(json, "daysSurvived", 0);
            info.difficulty = ParseIntField(json, "difficulty", 1);
            info.fog = ParseBoolField(json, "fog", false);
            info.mapSize = ParseIntField(json, "mapSize", 0);
            info.rainDiff = ParseIntField(json, "rainDiff", 1);
            info.resourceDiff = ParseIntField(json, "resourceDiff", 1);
            info.breachDiff = ParseIntField(json, "breachDiff", 1);
            info.factionDiff = ParseIntField(json, "factionDiff", 1);
            info.moodDiff = ParseIntField(json, "moodDiff", 1);
            info.familyName = ParseStringField(json, "familyName");
            info.saveTime = ParseStringField(json, "saveTime");
            return info;
        }
        
        private static int FindMatchingBracket(string s, int start, char open, char close)
        {
            int depth = 0;
            for (int i = start; i < s.Length; i++)
            {
                if (s[i] == open) depth++;
                else if (s[i] == close) depth--;
                if (depth == 0) return i;
            }
            return -1;
        }
        
        private static string ParseStringField(string json, string field)
        {
            string pattern = $"\"{field}\"";
            int pos = json.IndexOf(pattern);
            if (pos < 0) return "";
            int colonPos = json.IndexOf(':', pos + pattern.Length);
            if (colonPos < 0) return "";
            int quoteStart = json.IndexOf('"', colonPos + 1);
            if (quoteStart < 0) return "";
            int quoteEnd = quoteStart + 1;
            while (quoteEnd < json.Length)
            {
                if (json[quoteEnd] == '"' && json[quoteEnd - 1] != '\\')
                    break;
                quoteEnd++;
            }
            if (quoteEnd >= json.Length) return "";
            string value = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            // Unescape
            return value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
        
        private static int ParseIntField(string json, string field, int defaultValue)
        {
            string pattern = $"\"{field}\"";
            int pos = json.IndexOf(pattern);
            if (pos < 0) return defaultValue;
            int colonPos = json.IndexOf(':', pos + pattern.Length);
            if (colonPos < 0) return defaultValue;
            int valStart = colonPos + 1;
            while (valStart < json.Length && char.IsWhiteSpace(json[valStart])) valStart++;
            int valEnd = valStart;
            while (valEnd < json.Length && (char.IsDigit(json[valEnd]) || json[valEnd] == '-')) valEnd++;
            if (valEnd <= valStart) return defaultValue;
            if (int.TryParse(json.Substring(valStart, valEnd - valStart), out int result))
                return result;
            return defaultValue;
        }
        
        private static long ParseLongField(string json, string field, long defaultValue)
        {
            string pattern = $"\"{field}\"";
            int pos = json.IndexOf(pattern);
            if (pos < 0) return defaultValue;
            int colonPos = json.IndexOf(':', pos + pattern.Length);
            if (colonPos < 0) return defaultValue;
            int valStart = colonPos + 1;
            while (valStart < json.Length && char.IsWhiteSpace(json[valStart])) valStart++;
            int valEnd = valStart;
            while (valEnd < json.Length && (char.IsDigit(json[valEnd]) || json[valEnd] == '-')) valEnd++;
            if (valEnd <= valStart) return defaultValue;
            if (long.TryParse(json.Substring(valStart, valEnd - valStart), out long result))
                return result;
            return defaultValue;
        }
        
        private static bool ParseBoolField(string json, string field, bool defaultValue)
        {
            string pattern = $"\"{field}\"";
            int pos = json.IndexOf(pattern);
            if (pos < 0) return defaultValue;
            int colonPos = json.IndexOf(':', pos + pattern.Length);
            if (colonPos < 0) return defaultValue;
            int valStart = colonPos + 1;
            while (valStart < json.Length && char.IsWhiteSpace(json[valStart])) valStart++;
            if (valStart + 4 <= json.Length && json.Substring(valStart, 4).ToLower() == "true")
                return true;
            if (valStart + 5 <= json.Length && json.Substring(valStart, 5).ToLower() == "false")
                return false;
            return defaultValue;
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
                
                // Extract all difficulty settings for proper game loading
                entry.saveInfo.rainDiff = info.m_rainDiff;
                entry.saveInfo.resourceDiff = info.m_resourceDiff;
                entry.saveInfo.breachDiff = info.m_breachDiff;
                entry.saveInfo.factionDiff = info.m_factionDiff;
                entry.saveInfo.moodDiff = info.m_moodDiff;
                entry.saveInfo.mapSize = info.m_mapSize;
                entry.saveInfo.fog = info.m_fog;
                
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

        /// <summary>
        /// Serializes a SlotManifest to JSON using manual StringBuilder formatting.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: Unity's JsonUtility.ToJson() has a critical limitation - it CANNOT serialize
        /// arrays of custom classes (like LoadedModInfo[]). When you call JsonUtility.ToJson() on
        /// a SlotManifest, it will silently omit the 'lastLoadedMods' field from the output JSON,
        /// even though the array is populated in memory. This causes saves to appear as having 0 mods.
        /// 
        /// We must use manual StringBuilder formatting to ensure the mod list is actually written.
        /// The companion DeserializeSlotManifest() method uses custom parsing to read this format.
        /// </remarks>
        private static string SerializeSlotManifest(SlotManifest manifest)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"manifestVersion\": {manifest.manifestVersion},");
            sb.AppendLine($"    \"lastModified\": \"{manifest.lastModified}\",");
            sb.AppendLine($"    \"family_name\": \"{manifest.family_name}\"");
            
            if (manifest.lastLoadedMods != null && manifest.lastLoadedMods.Length > 0)
            {
                sb.AppendLine(",");
                sb.AppendLine("    \"lastLoadedMods\": [");
                for (int i = 0; i < manifest.lastLoadedMods.Length; i++)
                {
                    var mod = manifest.lastLoadedMods[i];
                    sb.Append("        { ");
                    sb.Append($"\"modId\": \"{mod.modId}\", ");
                    sb.Append($"\"version\": \"{mod.version}\", ");
                    sb.Append("\"warnings\": [");
                    
                    if (mod.warnings != null && mod.warnings.Length > 0)
                    {
                        for (int w = 0; w < mod.warnings.Length; w++)
                        {
                            sb.Append($"\"{mod.warnings[w]}\"");
                            if (w < mod.warnings.Length - 1) sb.Append(", ");
                        }
                    }
                    
                    sb.Append("] }");
                    if (i < manifest.lastLoadedMods.Length - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine("    ]");
            }
            else
            {
                sb.AppendLine();
            }
            
            sb.Append("}");
            return sb.ToString();
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
        /// Extracts all game settings including difficulty fields.
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
                
                // Extract all difficulty settings for proper game loading
                info.rainDiff = gameInfo.m_rainDiff;
                info.resourceDiff = gameInfo.m_resourceDiff;
                info.breachDiff = gameInfo.m_breachDiff;
                info.factionDiff = gameInfo.m_factionDiff;
                info.moodDiff = gameInfo.m_moodDiff;
                info.mapSize = gameInfo.m_mapSize;
                info.fog = gameInfo.m_fog;
                
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
                    default: return null;
                }
                
                string fullPath = Path.Combine(savesPath, fileName);
                
                if (!File.Exists(fullPath))
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
                MMLog.WriteError($"[SaveRegistryCore] Failed to read vanilla save info for slot {slotNumber}: {ex.Message}");
                return null;  // Return null on error
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

        /// <summary>
        /// Checks if there are gaps in the save slot numbers.
        /// For Standard scenario, slots start at 4 (1-3 are vanilla).
        /// </summary>
        public bool HasGaps()
        {
            var entries = LoadManifest().entries;
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
                MMLog.WriteDebug("[SaveCondenseManager] HasGaps: No custom saves in manifest.");
                return false;
            }

            slots.Sort();
            MMLog.WriteDebug($"[SaveCondenseManager] HasGaps: checking {slots.Count} saves. Scenario: {_scenarioId}");
            int expected = startSlot;
            foreach (var slot in slots)
            {
                if (slot != expected) 
                {
                    MMLog.WriteDebug($"[SaveCondenseManager] HasGaps: GAP FOUND! Expected {expected}, found {slot}");
                    return true;
                }
                expected++;
            }
            MMLog.WriteDebug("[SaveCondenseManager] HasGaps: No gaps found.");
            return false;
        }

        /// <summary>
        /// Runs the condense operation to close gaps in slot numbers.
        /// </summary>
        public void RunCondense()
        {
            var m = LoadManifest();
            CondenseSlots(m);
        }
    }

    /// <summary>
    /// Handles one-time startup check for save slot gaps and user preference for auto-condensing.
    /// </summary>
    public static class SaveCondenseManager
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
                MMLog.WriteDebug("[SaveCondenseManager] Starting startup gap check...");
                var registry = (SaveRegistryCore)ExpandedVanillaSaves.Instance;
                
                if (!registry.HasGaps())
                {
                    return;
                }

                var pref = ReadCondensePreference();
                MMLog.Write($"[SaveCondenseManager] Gaps detected. User preference from INI: '{pref}'");

                if (pref == "yes" || pref == "true")
                {
                    MMLog.Write("[SaveCondenseManager] Auto-condensing saves (user preference: yes).");
                    registry.RunCondense();
                }
                else if (pref == "no" || pref == "false")
                {
                    MMLog.Write("[SaveCondenseManager] Skipping condense (user preference: no).");
                }
                else
                {
                    MMLog.Write("[SaveCondenseManager] Preference is 'ask'. Flagging for prompt on Main Menu.");
                    _pendingPrompt = true;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveCondenseManager] Error during startup check: {ex}");
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
                    MMLog.Write("[SaveCondenseManager] Condensed saves per user request.");
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"[SaveCondenseManager] Error condensing: {ex}");
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
                        MMLog.Write($"[SaveCondenseManager] Read preference: '{v}'");
                        return v;
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug($"[SaveCondenseManager] Error reading preference: {ex.Message}");
            }
            
            MMLog.Write($"[SaveCondenseManager] Read preference: 'ask' (default)");
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
                MMLog.WriteDebug($"[SaveCondenseManager] Saved preference: AutoCondenseSaves={value}");
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveCondenseManager] Error writing preference: {ex}");
            }
        }
    }
}