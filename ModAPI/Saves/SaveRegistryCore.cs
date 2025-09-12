﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ModAPI.Saves
{
    /// <summary>
    /// Internal core logic for managing a single collection of save files within a specific directory.
    /// This class is not public and is used by ExpandedVanillaSaves and ScenarioSaves.
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
                        WriteEntryFile(saveId, xmlBytes, out long size, out uint crc);
                        entry.fileSize = size;
                        entry.crc32 = crc;
                        TryUpdateEntryInfo(entry, xmlBytes);
                    }
                    m.entries[i] = entry;
                    SaveManifestFile(m);
                    MMLog.WriteDebug($"OverwriteSave scenario={_scenarioId} id={saveId} size={entry.fileSize}");
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
            list.RemoveAt(idx);
            m.entries = list.ToArray();
            SaveManifestFile(m);
            try { File.Delete(DirectoryProvider.EntryPath(_scenarioId, saveId)); } catch { }
            try { File.Delete(DirectoryProvider.PreviewPath(_scenarioId, saveId)); } catch { }
            MMLog.WriteDebug($"DeleteSave scenario={_scenarioId} id={saveId}");
            return true;
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
                        // Don't rebuild here, just create a new one. Rebuilding can be a separate utility.
                        _manifest = new SaveManifest();
                    }
                }
                catch (Exception ex)
                {
                    MMLog.Write($"Manifest load error for '{_scenarioId}': " + ex.Message);
                    _manifest = new SaveManifest(); // Start fresh on error
                }
                return _manifest;
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

        private void WriteEntryFile(string saveId, byte[] xmlBytes, out long fileSize, out uint crc)
        {
            var path = DirectoryProvider.EntryPath(_scenarioId, saveId);
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
                MMLog.Write($"WriteEntryFile error for '{_scenarioId}/{saveId}': " + ex.Message);
            }
        }

        private static void TryUpdateEntryInfo(SaveEntry entry, byte[] xmlBytes)
        {
            try
            {
                var sd = new SaveData(xmlBytes);
                var info = sd.info;
                MMLog.Write($"[TryUpdateEntryInfo] Extracted Info: Family='{info.m_familyName}', Days={info.m_daysSurvived}, Difficulty={info.m_diffSetting}");
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
    }
}