using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ModAPI.Saves
{
    public static class CustomSaveRegistry
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, SaveManifest> _manifests = new Dictionary<string, SaveManifest>(StringComparer.OrdinalIgnoreCase);
        public static int MaxEntriesPerScenario = -1; // -1 = unlimited
        public static long MaxScenarioBytes = -1;     // -1 = unlimited

        public static SaveEntry[] ListSaves(string scenarioId, int page, int pageSize)
        {
            var all = LoadManifest(scenarioId).entries;
            if (all == null) return new SaveEntry[0];
            int start = Math.Max(0, page * pageSize);
            if (start >= all.Length) return new SaveEntry[0];
            int count = Math.Min(pageSize, all.Length - start);
            var result = new SaveEntry[count];
            Array.Copy(all, start, result, 0, count);
            MMLog.WriteDebug($"ListSaves scenario={scenarioId} page={page} size={pageSize} returned={result.Length}");
            return result;
        }

        public static SaveEntry GetSave(string scenarioId, string saveId)
        {
            var m = LoadManifest(scenarioId);
            foreach (var e in m.entries) if (e.id == saveId) return e;
            return null;
        }

        public static int CountSaves(string scenarioId)
        {
            var m = LoadManifest(scenarioId);
            return m.entries != null ? m.entries.Length : 0;
        }

        public static SaveEntry FindByPhysicalIndex(int physicalSlot, int page, string scenarioId, int pageSize)
        {
            // physicalSlot is 1..3; map to 0..2 index on page
            int idx = physicalSlot - 1;
            if (idx < 0) return null;
            var list = ListSaves(scenarioId, page, pageSize);
            if (idx >= list.Length) return null;
            return list[idx];
        }

        public static SaveEntry CreateSave(string scenarioId, SaveCreateOptions opts)
        {
            var m = LoadManifest(scenarioId);
            var id = IdGenerator.NewId();
            var now = DateTime.UtcNow.ToString("o");
            var entry = new SaveEntry
            {
                id = id,
                name = UniqueName(scenarioId, NameSanitizer.SanitizeName(opts != null ? opts.name : null)),
                createdAt = now,
                updatedAt = now,
                gameVersion = Application.version,
                modApiVersion = "1",
                scenarioId = scenarioId,
                scenarioVersion = ScenarioRegistry.GetScenario(scenarioId).version,
                fileSize = 0,
                crc32 = 0,
                previewPath = null,
                extra = opts != null ? opts.extraJson : null,
                saveInfo = new SaveInfo()
            };
            if (!CheckQuotaCreate(scenarioId)) { MMLog.Write("Quota exceeded for scenario '" + scenarioId + "'"); return null; }
            AppendEntry(m, entry);
            SaveManifestFile(scenarioId, m);
            MMLog.WriteDebug($"CreateSave scenario={scenarioId} name='{entry.name}' id={entry.id}");
            return entry;
        }

        public static SaveEntry OverwriteSave(string scenarioId, string saveId, SaveOverwriteOptions opts, byte[] xmlBytes)
        {
            var m = LoadManifest(scenarioId);
            for (int i = 0; i < m.entries.Length; i++)
            {
                if (m.entries[i].id == saveId)
                {
                    var entry = m.entries[i];
                    if (opts != null && !string.IsNullOrEmpty(opts.name)) entry.name = NameSanitizer.SanitizeName(opts.name);
                    if (opts != null && !string.IsNullOrEmpty(opts.extraJson)) entry.extra = opts.extraJson;
                    entry.updatedAt = DateTime.UtcNow.ToString("o");
                    if (xmlBytes != null)
                    {
                        WriteEntryFile(scenarioId, saveId, xmlBytes, out long size, out uint crc);
                        entry.fileSize = size;
                        entry.crc32 = crc;
                        TryUpdateEntryInfo(entry, xmlBytes);
                    }
                    m.entries[i] = entry;
                    SaveManifestFile(scenarioId, m);
                    MMLog.WriteDebug($"OverwriteSave scenario={scenarioId} id={saveId} size={entry.fileSize} crc={entry.crc32}");
                    return entry;
                }
            }
            return null;
        }

        public static SaveEntry RenameSave(string scenarioId, string saveId, string newName)
        {
            var m = LoadManifest(scenarioId);
            for (int i = 0; i < m.entries.Length; i++)
            {
                if (m.entries[i].id == saveId)
                {
                    m.entries[i].name = NameSanitizer.SanitizeName(newName);
                    m.entries[i].updatedAt = DateTime.UtcNow.ToString("o");
                    SaveManifestFile(scenarioId, m);
                    return m.entries[i];
                }
            }
            return null;
        }

        public static bool DeleteSave(string scenarioId, string saveId)
        {
            var m = LoadManifest(scenarioId);
            var list = new List<SaveEntry>(m.entries);
            var idx = list.FindIndex(e => e.id == saveId);
            if (idx < 0) return false;
            list.RemoveAt(idx);
            m.entries = list.ToArray();
            SaveManifestFile(scenarioId, m);
            try { File.Delete(DirectoryProvider.EntryPath(scenarioId, saveId)); } catch { }
            try { File.Delete(DirectoryProvider.PreviewPath(scenarioId, saveId)); } catch { }
            MMLog.WriteDebug($"DeleteSave scenario={scenarioId} id={saveId}");
            return true;
        }

        public static void CapturePreview(string scenarioId, string saveId, Texture2D frame)
        {
            PreviewCapture.CapturePNG(scenarioId, saveId, frame);
        }

        // Utility used by Platform proxy
        public static void WriteEntryFile(string scenarioId, string saveId, byte[] xmlBytes, out long fileSize, out uint crc)
        {
            var path = DirectoryProvider.EntryPath(scenarioId, saveId);
            var tmp = path + ".tmp";
            fileSize = 0; crc = 0;
            try
            {
                File.WriteAllBytes(tmp, xmlBytes);
                fileSize = new FileInfo(tmp).Length;
                crc = CRC32.Compute(xmlBytes);
                // Try atomic replace; fallback to move
                try { File.Replace(tmp, path, null); }
                catch { File.Copy(tmp, path, true); File.Delete(tmp); }
            }
            catch (System.Exception ex)
            {
                MMLog.Write("WriteEntryFile error: " + ex.Message);
            }
        }

        private static void TryUpdateEntryInfo(SaveEntry entry, byte[] xmlBytes)
        {
            try
            {
                var sd = new SaveData(xmlBytes);
                var info = sd.info;
                entry.saveInfo.daysSurvived = info.m_daysSurvived;
                entry.saveInfo.difficulty = info.m_diffSetting;
                entry.saveInfo.fog = info.m_fog;
                entry.saveInfo.mapSize = info.m_mapSize;
                entry.saveInfo.rainDiff = info.m_rainDiff;
                entry.saveInfo.resourceDiff = info.m_resourceDiff;
                entry.saveInfo.breachDiff = info.m_breachDiff;
                entry.saveInfo.factionDiff = info.m_factionDiff;
                entry.saveInfo.moodDiff = info.m_moodDiff;
                entry.saveInfo.familyName = info.m_familyName;
                entry.saveInfo.saveTime = info.m_saveTime;
                sd.Finished();
            }
            catch (Exception ex)
            {
                MMLog.Write("TryUpdateEntryInfo parse error: " + ex.Message);
            }
        }

        private static void AppendEntry(SaveManifest m, SaveEntry e)
        {
            var list = new List<SaveEntry>(m.entries ?? new SaveEntry[0]);
            list.Add(e);
            m.entries = list.ToArray();
        }

        public static SaveManifest LoadManifest(string scenarioId)
        {
            lock (_lock)
            {
                SaveManifest m;
                if (_manifests.TryGetValue(scenarioId, out m)) return m;
                var path = DirectoryProvider.ManifestPath(scenarioId);
                try
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        m = JsonUtility.FromJson<SaveManifest>(json);
                        if (m == null) m = new SaveManifest();
                    }
                    else
                    {
                        m = RebuildManifestFromFiles(scenarioId);
                        // Save rebuilt manifest
                        SaveManifestFile(scenarioId, m);
                        MMLog.WriteDebug($"LoadManifest: rebuilt from files for scenario={scenarioId}");
                    }
                }
                catch (System.Exception ex)
                {
                    MMLog.Write("Manifest load error: " + ex.Message + "; attempting rebuild");
                    m = RebuildManifestFromFiles(scenarioId);
                }
                _manifests[scenarioId] = m;
                return m;
            }
        }

        public static void SaveManifestFile(string scenarioId, SaveManifest m)
        {
            lock (_lock)
            {
                _manifests[scenarioId] = m;
                try
                {
                    WriteJsonAtomic(DirectoryProvider.ManifestPath(scenarioId), m);
                }
                catch (System.Exception ex)
                {
                    MMLog.Write("Manifest save error: " + ex.Message);
                }
            }
        }

        // Utility: decode PlatformSave_PC obfuscation to raw xml string
        public static string DecodeToXml(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            // Mirrors PlatformSave_PC xor scheme
            byte[] key = new byte[17] { 172, 242, 115, 58, 254, 222, 170, 33, 48, 13, 167, 21, 139, 109, 74, 186, 171 };
            byte[] order = new byte[17] { 0, 2, 4, 1, 6, 15, 13, 16, 8, 3, 12, 10, 5, 9, 11, 7, 14 };
            int p = 0;
            var buf = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                buf[i] = (byte)(data[i] ^ key[order[p++]]);
                if (p >= order.Length) p = 0;
            }
            return Encoding.UTF8.GetString(buf);
        }

        // Utility: encode raw xml to obfuscated bytes
        public static byte[] EncodeFromXml(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return new byte[0];
            byte[] data = Encoding.UTF8.GetBytes(xml);
            byte[] key = new byte[17] { 172, 242, 115, 58, 254, 222, 170, 33, 48, 13, 167, 21, 139, 109, 74, 186, 171 };
            byte[] order = new byte[17] { 0, 2, 4, 1, 6, 15, 13, 16, 8, 3, 12, 10, 5, 9, 11, 7, 14 };
            int p = 0;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(data[i] ^ key[order[p++]]);
                if (p >= order.Length) p = 0;
            }
            return data;
        }

        private static void WriteJsonAtomic(string path, SaveManifest m)
        {
            var tmp = path + ".tmp";
            var json = JsonUtility.ToJson(m, true);
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(json);
                    sw.Flush();
                    fs.Flush();
                }
                try { File.Replace(tmp, path, null); }
                catch { File.Copy(tmp, path, true); File.Delete(tmp); }
                MMLog.WriteDebug($"Manifest saved: {path}");
            }
            catch (Exception ex)
            {
                MMLog.Write("WriteJsonAtomic error: " + ex.Message);
            }
        }

        private static SaveManifest RebuildManifestFromFiles(string scenarioId)
        {
            var m = new SaveManifest();
            var root = DirectoryProvider.ScenarioRoot(scenarioId);
            if (!Directory.Exists(root)) return m;
            var entries = new List<SaveEntry>();
            foreach (var file in Directory.GetFiles(root, "*.xml"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    var sd = new SaveData(bytes);
                    var info = sd.info;
                    var e = new SaveEntry
                    {
                        id = id,
                        name = id,
                        createdAt = File.GetCreationTimeUtc(file).ToString("o"),
                        updatedAt = File.GetLastWriteTimeUtc(file).ToString("o"),
                        gameVersion = Application.version,
                        modApiVersion = "1",
                        scenarioId = scenarioId,
                        scenarioVersion = ScenarioRegistry.GetScenario(scenarioId).version,
                        fileSize = new FileInfo(file).Length,
                        crc32 = CRC32.Compute(bytes),
                        previewPath = null,
                        extra = null,
                        saveInfo = new SaveInfo
                        {
                            daysSurvived = info.m_daysSurvived,
                            difficulty = info.m_diffSetting,
                            fog = info.m_fog,
                            mapSize = info.m_mapSize,
                            rainDiff = info.m_rainDiff,
                            resourceDiff = info.m_resourceDiff,
                            breachDiff = info.m_breachDiff,
                            factionDiff = info.m_factionDiff,
                            moodDiff = info.m_moodDiff,
                            familyName = info.m_familyName,
                            saveTime = info.m_saveTime
                        }
                    };
                    entries.Add(e);
                }
                catch (Exception ex)
                {
                    // quarantine corrupt
                    try
                    {
                        var corruptDir = DirectoryProvider.CorruptRoot(scenarioId);
                        var dest = Path.Combine(corruptDir, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                        File.Delete(file);
                        MMLog.Write("Quarantined corrupt save '" + file + "': " + ex.Message);
                    }
                    catch { }
                }
            }
            m.entries = entries.ToArray();
            MMLog.WriteDebug($"RebuildManifestFromFiles: scenario={scenarioId} entries={m.entries.Length}");
            return m;
        }

        private static bool CheckQuotaCreate(string scenarioId)
        {
            if (MaxEntriesPerScenario > 0 && CountSaves(scenarioId) >= MaxEntriesPerScenario) return false;
            if (MaxScenarioBytes > 0)
            {
                long total = 0;
                foreach (var e in LoadManifest(scenarioId).entries) total += e.fileSize;
                if (total >= MaxScenarioBytes) return false;
            }
            return true;
        }

        private static string UniqueName(string scenarioId, string name)
        {
            if (string.IsNullOrEmpty(name)) name = "Unnamed";
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in LoadManifest(scenarioId).entries) if (!string.IsNullOrEmpty(e.name)) existing.Add(e.name);
            if (!existing.Contains(name)) return name;
            int i = 2;
            string candidate;
            do { candidate = name + " (" + i++ + ")"; } while (existing.Contains(candidate));
            return candidate;
        }
    }
}
