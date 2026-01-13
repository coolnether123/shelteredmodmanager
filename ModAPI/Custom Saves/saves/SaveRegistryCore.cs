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
                if (existing != null)
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

                        entry.absoluteSlot = expectedSlot;
                        changed = true;
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError($"[CondenseSlots] Failed to move slot {entry.absoluteSlot} to {expectedSlot}: {ex.Message}");
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
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"manifestVersion\": {manifest.manifestVersion},");
            sb.AppendLine($"    \"lastModified\": \"{EscapeJson(manifest.lastModified)}\",");
            sb.AppendLine($"    \"family_name\": \"{EscapeJson(manifest.family_name)}\",");
            
            // Serialize lastLoadedMods array
            sb.Append("    \"lastLoadedMods\": [");
            if (manifest.lastLoadedMods != null && manifest.lastLoadedMods.Length > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < manifest.lastLoadedMods.Length; i++)
                {
                    var mod = manifest.lastLoadedMods[i];
                    sb.Append("        {");
                    sb.Append($" \"modId\": \"{EscapeJson(mod.modId)}\",");
                    sb.Append($" \"version\": \"{EscapeJson(mod.version)}\",");
                    sb.Append(" \"warnings\": [");
                    if (mod.warnings != null && mod.warnings.Length > 0)
                    {
                        for (int j = 0; j < mod.warnings.Length; j++)
                        {
                            sb.Append($"\"{EscapeJson(mod.warnings[j])}\"");
                            if (j < mod.warnings.Length - 1) sb.Append(", ");
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
                sb.AppendLine("]");
            }
            
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Deserializes a SlotManifest from its custom JSON representation.
        /// Utilizes a lightweight line-based parser to avoid dependency on complex JSON libraries in the core loader.
        /// </summary>
        internal static SlotManifest DeserializeSlotManifest(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            
            var manifest = new SlotManifest();
            var mods = new List<LoadedModInfo>();
            
            try
            {
                var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    if (line.Contains("\"manifestVersion\""))
                    {
                        manifest.manifestVersion = ExtractInt(line);
                    }
                    else if (line.Contains("\"lastModified\""))
                    {
                        manifest.lastModified = ExtractString(line);
                    }
                    else if (line.Contains("\"family_name\""))
                    {
                        manifest.family_name = ExtractString(line);
                    }
                    else if (line.Contains("\"modId\""))
                    {
                        // Start of a mod entry
                        var mod = new LoadedModInfo();
                        mod.modId = ExtractString(line);
                        
                        // Look ahead for version and warnings on the same line
                        if (i < lines.Length && line.Contains("\"version\""))
                        {
                            var versionStart = line.IndexOf("\"version\"");
                            mod.version = ExtractString(line.Substring(versionStart));
                        }
                        
                        mods.Add(mod);
                    }
                }
                
                manifest.lastLoadedMods = mods.ToArray();
            }
            catch
            {
                // Fall back to empty manifest
                manifest.lastLoadedMods = new LoadedModInfo[0];
            }
            
            return manifest;
        }

        private static string ExtractString(string line)
        {
            var start = line.IndexOf(":") + 1;
            if (start <= 0) return "";
            
            var valueStart = line.IndexOf("\"", start) + 1;
            if (valueStart <= start) return "";
            
            var valueEnd = line.IndexOf("\"", valueStart);
            if (valueEnd <= valueStart) return "";
            
            return line.Substring(valueStart, valueEnd - valueStart);
        }

        private static int ExtractInt(string line)
        {
            var start = line.IndexOf(":") + 1;
            if (start <= 0) return 0;
            
            var valueStr = line.Substring(start).Trim().TrimEnd(',');
            int result;
            int.TryParse(valueStr, out result);
            return result;
        }
    }
}