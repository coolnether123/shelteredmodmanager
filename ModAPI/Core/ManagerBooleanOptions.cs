using System;
using System.IO;
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

        public static void RegisterBooleanOption(ManagerBooleanOptionDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return;

            lock (Sync)
            {
                ManagerBooleanOptionsFile file = LoadFile();
                int index = FindIndex(file, definition.Id);
                bool value = definition.DefaultValue;

                if (index >= 0)
                    value = file.booleans[index].value;

                ManagerBooleanOptionRecord record = new ManagerBooleanOptionRecord
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

                if (index >= 0)
                    file.booleans[index] = record;
                else
                    file.booleans = Append(file.booleans, record);

                SaveFile(file);
            }
        }

        public static bool GetBool(string id, bool fallback)
        {
            if (string.IsNullOrEmpty(id))
                return fallback;

            lock (Sync)
            {
                ManagerBooleanOptionsFile file = LoadFile();
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
                ManagerBooleanOptionsFile file = LoadFile();
                int index = FindIndex(file, id);
                if (index < 0)
                    return;

                file.booleans[index].value = value;
                SaveFile(file);
            }
        }

        private static ManagerBooleanOptionsFile LoadFile()
        {
            try
            {
                string path = ModApiPaths.ManagerOptionsPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    ManagerBooleanOptionsFile file = JsonUtility.FromJson<ManagerBooleanOptionsFile>(json);
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
            if (file == null)
                file = new ManagerBooleanOptionsFile();
            if (file.booleans == null)
                file.booleans = new ManagerBooleanOptionRecord[0];
            if (file.version <= 0)
                file.version = 1;

            try
            {
                string path = ModApiPaths.ManagerOptionsPath;
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(file, true));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ManagerBooleanOptions] Failed to write manager options: " + ex.Message);
            }
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
    }

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

    [Serializable]
    public sealed class ManagerBooleanOptionsFile
    {
        public int version = 1;
        public ManagerBooleanOptionRecord[] booleans = new ManagerBooleanOptionRecord[0];
    }

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
