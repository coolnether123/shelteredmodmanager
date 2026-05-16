using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Util;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Shared manager/runtime boolean options persisted in SMM/bin/manager_options.json.
    /// Runtime systems register metadata here; the desktop Manager edits the values.
    /// </summary>
    public static class ManagerBooleanOptions
    {
        private static readonly object Sync = new object();
        private static ManagerBooleanOptionsFile _cachedFile;
        private static bool _cacheLoaded;

        public static void RegisterBooleanOption(ManagerBooleanOptionDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return;

            lock (Sync)
            {
                ManagerBooleanOptionsFile file = GetCachedFile();
                int index = FindIndex(file, definition.Id);
                bool value = definition.DefaultValue;

                if (index >= 0)
                    value = file.booleans[index].value;

                if (index >= 0)
                {
                    if (file.booleans[index] == null)
                    {
                        file.booleans[index] = CreateRecord(definition, value);
                        SaveFile(file);
                    }
                    else if (UpdateRecordMetadata(file.booleans[index], definition, value))
                    {
                        SaveFile(file);
                    }
                }
                else
                {
                    ManagerBooleanOptionRecord record = CreateRecord(definition, value);
                    file.booleans = Append(file.booleans, record);
                    SaveFile(file);
                }
            }
        }

        public static bool GetBool(string id, bool fallback)
        {
            if (string.IsNullOrEmpty(id))
                return fallback;

            lock (Sync)
            {
                ManagerBooleanOptionsFile file = GetCachedFile();
                int index = FindIndex(file, id);
                return index >= 0 ? file.booleans[index].value : fallback;
            }
        }

        public static void SetBool(string id, bool value)
        {
            if (string.IsNullOrEmpty(id))
                return;

            lock (Sync)
            {
                ManagerBooleanOptionsFile file = GetCachedFile();
                int index = FindIndex(file, id);
                if (index < 0)
                    return;

                if (file.booleans[index].value == value)
                    return;

                file.booleans[index].value = value;
                SaveFile(file);
            }
        }

        private static ManagerBooleanOptionsFile GetCachedFile()
        {
            if (!_cacheLoaded || _cachedFile == null)
            {
                _cachedFile = LoadFile();
                _cacheLoaded = true;
            }

            NormalizeFile(_cachedFile);
            return _cachedFile;
        }

        private static ManagerBooleanOptionsFile LoadFile()
        {
            try
            {
                string path = ModApiPaths.ManagerOptionsPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    ManagerBooleanOptionsFile file = DeserializeFile(json);
                    if (file != null)
                    {
                        if (file.booleans == null)
                            file.booleans = new ManagerBooleanOptionRecord[0];
                        if (file.version <= 0)
                            file.version = 1;
                        return file;
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ManagerBooleanOptions] Failed to read manager options: " + ex.Message);
            }

            return new ManagerBooleanOptionsFile();
        }

        private static void SaveFile(ManagerBooleanOptionsFile file)
        {
            NormalizeFile(file);

            try
            {
                string path = ModApiPaths.ManagerOptionsPath;
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, SerializeFile(file));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
                _cachedFile = file;
                _cacheLoaded = true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ManagerBooleanOptions] Failed to write manager options: " + ex.Message);
            }
        }

        private static void NormalizeFile(ManagerBooleanOptionsFile file)
        {
            if (file == null)
                return;
            if (file.booleans == null)
                file.booleans = new ManagerBooleanOptionRecord[0];
            if (file.version <= 0)
                file.version = 1;
        }

        private static ManagerBooleanOptionRecord CreateRecord(ManagerBooleanOptionDefinition definition, bool value)
        {
            return new ManagerBooleanOptionRecord
            {
                id = definition.Id,
                owner = definition.Owner ?? string.Empty,
                label = definition.Label ?? definition.Id,
                description = definition.Description ?? string.Empty,
                value = value,
                defaultValue = definition.DefaultValue,
                requiresRestart = definition.RequiresRestart,
                sortOrder = definition.SortOrder
            };
        }

        private static bool UpdateRecordMetadata(ManagerBooleanOptionRecord record, ManagerBooleanOptionDefinition definition, bool value)
        {
            if (record == null || definition == null)
                return false;

            bool changed = false;
            changed |= SetStringIfDifferent(ref record.owner, definition.Owner ?? string.Empty);
            changed |= SetStringIfDifferent(ref record.label, definition.Label ?? definition.Id);
            changed |= SetStringIfDifferent(ref record.description, definition.Description ?? string.Empty);

            if (record.defaultValue != definition.DefaultValue)
            {
                record.defaultValue = definition.DefaultValue;
                changed = true;
            }

            if (record.requiresRestart != definition.RequiresRestart)
            {
                record.requiresRestart = definition.RequiresRestart;
                changed = true;
            }

            if (record.sortOrder != definition.SortOrder)
            {
                record.sortOrder = definition.SortOrder;
                changed = true;
            }

            if (record.value != value && string.IsNullOrEmpty(record.id))
            {
                record.value = value;
                changed = true;
            }

            return changed;
        }

        private static bool SetStringIfDifferent(ref string target, string value)
        {
            if (string.Equals(target ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal))
                return false;

            target = value ?? string.Empty;
            return true;
        }

        private static int FindIndex(ManagerBooleanOptionsFile file, string id)
        {
            if (file == null || file.booleans == null)
                return -1;

            for (int i = 0; i < file.booleans.Length; i++)
            {
                ManagerBooleanOptionRecord record = file.booleans[i];
                if (record != null && string.Equals(record.id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static ManagerBooleanOptionRecord[] Append(ManagerBooleanOptionRecord[] records, ManagerBooleanOptionRecord record)
        {
            if (records == null)
                records = new ManagerBooleanOptionRecord[0];

            ManagerBooleanOptionRecord[] next = new ManagerBooleanOptionRecord[records.Length + 1];
            for (int i = 0; i < records.Length; i++)
                next[i] = records[i];
            next[next.Length - 1] = record;
            return next;
        }

        // Unity 5.6 JsonUtility silently omits arrays of custom classes, so this
        // manager-owned file maps its schema through the shared manual JSON utility.
        private static ManagerBooleanOptionsFile DeserializeFile(string json)
        {
            ManagerBooleanOptionsFile file = new ManagerBooleanOptionsFile();
            if (string.IsNullOrEmpty(json))
                return file;

            ManualJsonObject root;
            string error;
            if (!ManualJson.TryParseObject(json, out root, out error))
            {
                MMLog.WriteWarning("[ManagerBooleanOptions] Invalid manager options JSON: " + error);
                file.booleans = new ManagerBooleanOptionRecord[0];
                return file;
            }

            file.version = root.GetInt("version", 1);
            List<ManagerBooleanOptionRecord> records = new List<ManagerBooleanOptionRecord>();
            ManualJsonArray booleans = root.GetArray("booleans");
            if (booleans == null)
            {
                file.booleans = new ManagerBooleanOptionRecord[0];
                return file;
            }

            for (int i = 0; i < booleans.Items.Count; i++)
            {
                ManualJsonValue value = booleans.Items[i];
                ManualJsonObject recordJson = value != null && value.Type == ManualJsonValueType.Object ? value.ObjectValue : null;
                ManagerBooleanOptionRecord record = DeserializeRecord(recordJson);
                if (record != null && !string.IsNullOrEmpty(record.id))
                    records.Add(record);
            }

            file.booleans = records.ToArray();
            return file;
        }

        private static ManagerBooleanOptionRecord DeserializeRecord(ManualJsonObject json)
        {
            if (json == null)
                return null;

            return new ManagerBooleanOptionRecord
            {
                id = json.GetString("id", string.Empty),
                owner = json.GetString("owner", string.Empty),
                label = json.GetString("label", string.Empty),
                description = json.GetString("description", string.Empty),
                value = json.GetBool("value", false),
                defaultValue = json.GetBool("defaultValue", false),
                requiresRestart = json.GetBool("requiresRestart", true),
                sortOrder = json.GetInt("sortOrder", 0)
            };
        }

        private static string SerializeFile(ManagerBooleanOptionsFile file)
        {
            NormalizeFile(file);

            ManualJsonObject root = new ManualJsonObject();
            root.Set("version", ManualJsonValue.Number(file.version));
            ManualJsonArray booleans = new ManualJsonArray();
            for (int i = 0; i < file.booleans.Length; i++)
            {
                ManagerBooleanOptionRecord record = file.booleans[i];
                if (record == null)
                    continue;

                ManualJsonObject recordJson = new ManualJsonObject();
                recordJson.Set("id", ManualJsonValue.String(record.id));
                recordJson.Set("owner", ManualJsonValue.String(record.owner));
                recordJson.Set("label", ManualJsonValue.String(record.label));
                recordJson.Set("description", ManualJsonValue.String(record.description));
                recordJson.Set("value", ManualJsonValue.Boolean(record.value));
                recordJson.Set("defaultValue", ManualJsonValue.Boolean(record.defaultValue));
                recordJson.Set("requiresRestart", ManualJsonValue.Boolean(record.requiresRestart));
                recordJson.Set("sortOrder", ManualJsonValue.Number(record.sortOrder));
                booleans.Add(ManualJsonValue.Object(recordJson));
            }

            root.Set("booleans", ManualJsonValue.Array(booleans));
            return ManualJson.Serialize(root, true);
        }
    }

    /// <summary>
    /// Metadata for a manager-editable boolean runtime option.
    /// Register definitions during startup so the desktop manager can display labels and restart requirements.
    /// </summary>
    public sealed class ManagerBooleanOptionDefinition
    {
        public string Id;
        public string Owner;
        public string Label;
        public string Description;
        public bool DefaultValue = true;
        public bool RequiresRestart = true;
        public int SortOrder;
    }

    /// <summary>
    /// JSON file shape for all manager-owned boolean option values.
    /// Mods should use <see cref="ManagerBooleanOptions"/> instead of editing this directly.
    /// </summary>
    [Serializable]
    public sealed class ManagerBooleanOptionsFile
    {
        public int version = 1;
        public ManagerBooleanOptionRecord[] booleans = new ManagerBooleanOptionRecord[0];
    }

    /// <summary>
    /// Persisted value and display metadata for one boolean option.
    /// </summary>
    [Serializable]
    public sealed class ManagerBooleanOptionRecord
    {
        public string id;
        public string owner;
        public string label;
        public string description;
        public bool value;
        public bool defaultValue;
        public bool requiresRestart;
        public int sortOrder;
    }
}
