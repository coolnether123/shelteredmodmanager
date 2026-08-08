using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Util;

namespace ModAPI.Core
{
    /// <summary>Metadata for a manager-editable boolean runtime option.</summary>
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
            if (definition == null || string.IsNullOrEmpty(definition.Id)) return;
            lock (Sync)
            {
                ManagerBooleanOptionsFile file = GetCachedFile();
                ManagerBooleanOptionDescriptor descriptor = new ManagerBooleanOptionDescriptor
                {
                    Id = definition.Id,
                    Owner = definition.Owner,
                    Label = definition.Label,
                    Description = definition.Description,
                    DefaultValue = definition.DefaultValue,
                    RequiresRestart = definition.RequiresRestart,
                    SortOrder = definition.SortOrder
                };
                if (ManagerBooleanOptionPolicy.MergeDefinition(file, descriptor)) SaveFile(file);
            }
        }

        public static bool GetBool(string id, bool fallback)
        {
            if (string.IsNullOrEmpty(id)) return fallback;
            lock (Sync)
            {
                ManagerBooleanOptionRecord record = ManagerBooleanOptionPolicy.FindRecord(GetCachedFile(), id);
                return record != null ? record.value : fallback;
            }
        }

        public static void SetBool(string id, bool value)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (Sync)
            {
                ManagerBooleanOptionsFile file = GetCachedFile();
                if (ManagerBooleanOptionPolicy.TrySetValue(file, id, value)) SaveFile(file);
            }
        }

        private static ManagerBooleanOptionsFile GetCachedFile()
        {
            if (!_cacheLoaded || _cachedFile == null)
            {
                _cachedFile = LoadFile();
                _cacheLoaded = true;
            }
            ManagerBooleanOptionPolicy.Normalize(_cachedFile);
            return _cachedFile;
        }

        private static ManagerBooleanOptionsFile LoadFile()
        {
            try
            {
                string path = ModApiPaths.ManagerOptionsPath;
                if (File.Exists(path))
                {
                    ManagerBooleanOptionsFile file = DeserializeFile(File.ReadAllText(path));
                    if (file != null)
                    {
                        ManagerBooleanOptionPolicy.Normalize(file);
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
            ManagerBooleanOptionPolicy.Normalize(file);
            try
            {
                string path = ModApiPaths.ManagerOptionsPath;
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, SerializeFile(file));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                _cachedFile = file;
                _cacheLoaded = true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ManagerBooleanOptions] Failed to write manager options: " + ex.Message);
            }
        }

        // Unity 5.6 JsonUtility silently omits arrays of custom classes, so the
        // runtime adapter maps the shared schema through the manual JSON utility.
        private static ManagerBooleanOptionsFile DeserializeFile(string json)
        {
            ManagerBooleanOptionsFile file = new ManagerBooleanOptionsFile();
            if (string.IsNullOrEmpty(json)) return file;
            ManualJsonObject root;
            string error;
            if (!ManualJson.TryParseObject(json, out root, out error))
            {
                MMLog.WriteWarning("[ManagerBooleanOptions] Invalid manager options JSON: " + error);
                return file;
            }

            file.version = root.GetInt("version", 1);
            List<ManagerBooleanOptionRecord> records = new List<ManagerBooleanOptionRecord>();
            ManualJsonArray booleans = root.GetArray("booleans");
            if (booleans == null) return file;
            for (int i = 0; i < booleans.Items.Count; i++)
            {
                ManualJsonValue value = booleans.Items[i];
                ManualJsonObject jsonRecord = value != null && value.Type == ManualJsonValueType.Object ? value.ObjectValue : null;
                ManagerBooleanOptionRecord record = DeserializeRecord(jsonRecord);
                if (record != null && !string.IsNullOrEmpty(record.id)) records.Add(record);
            }
            file.booleans = records.ToArray();
            return file;
        }

        private static ManagerBooleanOptionRecord DeserializeRecord(ManualJsonObject json)
        {
            if (json == null) return null;
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
            ManagerBooleanOptionPolicy.Normalize(file);
            ManualJsonObject root = new ManualJsonObject();
            root.Set("version", ManualJsonValue.Number(file.version));
            ManualJsonArray booleans = new ManualJsonArray();
            for (int i = 0; i < file.booleans.Length; i++)
            {
                ManagerBooleanOptionRecord record = file.booleans[i];
                if (record == null) continue;
                ManualJsonObject json = new ManualJsonObject();
                json.Set("id", ManualJsonValue.String(record.id));
                json.Set("owner", ManualJsonValue.String(record.owner));
                json.Set("label", ManualJsonValue.String(record.label));
                json.Set("description", ManualJsonValue.String(record.description));
                json.Set("value", ManualJsonValue.Boolean(record.value));
                json.Set("defaultValue", ManualJsonValue.Boolean(record.defaultValue));
                json.Set("requiresRestart", ManualJsonValue.Boolean(record.requiresRestart));
                json.Set("sortOrder", ManualJsonValue.Number(record.sortOrder));
                booleans.Add(ManualJsonValue.Object(json));
            }
            root.Set("booleans", ManualJsonValue.Array(booleans));
            return ManualJson.Serialize(root, true);
        }
    }
}
