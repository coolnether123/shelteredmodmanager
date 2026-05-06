using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        // manager-owned file uses a small explicit serializer for its fixed shape.
        private static ManagerBooleanOptionsFile DeserializeFile(string json)
        {
            ManagerBooleanOptionsFile file = new ManagerBooleanOptionsFile();
            if (string.IsNullOrEmpty(json))
                return file;

            file.version = ExtractJsonInt(json, "version", 1);

            string booleansJson = ExtractJsonArrayContent(json, "booleans");
            if (booleansJson == null)
            {
                file.booleans = new ManagerBooleanOptionRecord[0];
                return file;
            }

            List<ManagerBooleanOptionRecord> records = new List<ManagerBooleanOptionRecord>();
            List<string> objectBodies = SplitJsonObjectBodies(booleansJson);
            for (int i = 0; i < objectBodies.Count; i++)
            {
                ManagerBooleanOptionRecord record = DeserializeRecord(objectBodies[i]);
                if (record != null && !string.IsNullOrEmpty(record.id))
                    records.Add(record);
            }

            file.booleans = records.ToArray();
            return file;
        }

        private static ManagerBooleanOptionRecord DeserializeRecord(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return new ManagerBooleanOptionRecord
            {
                id = ExtractJsonString(json, "id", string.Empty),
                owner = ExtractJsonString(json, "owner", string.Empty),
                label = ExtractJsonString(json, "label", string.Empty),
                description = ExtractJsonString(json, "description", string.Empty),
                value = ExtractJsonBool(json, "value", false),
                defaultValue = ExtractJsonBool(json, "defaultValue", false),
                requiresRestart = ExtractJsonBool(json, "requiresRestart", true),
                sortOrder = ExtractJsonInt(json, "sortOrder", 0)
            };
        }

        private static string SerializeFile(ManagerBooleanOptionsFile file)
        {
            NormalizeFile(file);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("    \"version\": " + file.version.ToString() + ",");
            builder.AppendLine("    \"booleans\": [");

            bool wroteRecord = false;
            for (int i = 0; i < file.booleans.Length; i++)
            {
                ManagerBooleanOptionRecord record = file.booleans[i];
                if (record == null)
                    continue;

                if (wroteRecord)
                    builder.AppendLine(",");

                builder.AppendLine("        {");
                AppendJsonString(builder, "id", record.id, true);
                AppendJsonString(builder, "owner", record.owner, true);
                AppendJsonString(builder, "label", record.label, true);
                AppendJsonString(builder, "description", record.description, true);
                AppendJsonBool(builder, "value", record.value, true);
                AppendJsonBool(builder, "defaultValue", record.defaultValue, true);
                AppendJsonBool(builder, "requiresRestart", record.requiresRestart, true);
                builder.Append("            \"sortOrder\": ");
                builder.Append(record.sortOrder.ToString());
                builder.AppendLine();
                builder.Append("        }");
                wroteRecord = true;
            }

            if (wroteRecord)
                builder.AppendLine();
            builder.AppendLine("    ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendJsonString(StringBuilder builder, string name, string value, bool trailingComma)
        {
            builder.Append("            \"");
            builder.Append(name);
            builder.Append("\": \"");
            builder.Append(EscapeJsonString(value));
            builder.Append("\"");
            if (trailingComma)
                builder.Append(",");
            builder.AppendLine();
        }

        private static void AppendJsonBool(StringBuilder builder, string name, bool value, bool trailingComma)
        {
            builder.Append("            \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
            if (trailingComma)
                builder.Append(",");
            builder.AppendLine();
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32 || c > 126)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        private static int ExtractJsonInt(string json, string name, int defaultValue)
        {
            string value = ExtractJsonRawValue(json, name);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            int parsed;
            return int.TryParse(value, out parsed) ? parsed : defaultValue;
        }

        private static bool ExtractJsonBool(string json, string name, bool defaultValue)
        {
            string value = ExtractJsonRawValue(json, name);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : defaultValue;
        }

        private static string ExtractJsonString(string json, string name, string defaultValue)
        {
            int valueIndex = FindJsonValueStart(json, name);
            if (valueIndex < 0 || valueIndex >= json.Length || json[valueIndex] != '"')
                return defaultValue;

            int index = valueIndex + 1;
            StringBuilder builder = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"')
                    return builder.ToString();

                if (c != '\\' || index >= json.Length)
                {
                    builder.Append(c);
                    continue;
                }

                char escaped = json[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 <= json.Length)
                        {
                            string hex = json.Substring(index, 4);
                            int charCode;
                            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out charCode))
                            {
                                builder.Append((char)charCode);
                                index += 4;
                            }
                        }
                        break;
                    default:
                        builder.Append(escaped);
                        break;
                }
            }

            return defaultValue;
        }

        private static string ExtractJsonRawValue(string json, string name)
        {
            int valueIndex = FindJsonValueStart(json, name);
            if (valueIndex < 0)
                return null;

            int end = valueIndex;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
                end++;

            return json.Substring(valueIndex, end - valueIndex).Trim();
        }

        private static int FindJsonValueStart(string json, string name)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(name))
                return -1;

            string token = "\"" + name + "\"";
            int propertyIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (propertyIndex < 0)
                return -1;

            int colonIndex = json.IndexOf(':', propertyIndex + token.Length);
            if (colonIndex < 0)
                return -1;

            int valueIndex = colonIndex + 1;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex]))
                valueIndex++;

            return valueIndex;
        }

        private static string ExtractJsonArrayContent(string json, string name)
        {
            int valueIndex = FindJsonValueStart(json, name);
            if (valueIndex < 0 || valueIndex >= json.Length || json[valueIndex] != '[')
                return null;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = valueIndex; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '[')
                {
                    depth++;
                    continue;
                }

                if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(valueIndex + 1, i - valueIndex - 1);
                }
            }

            return null;
        }

        private static List<string> SplitJsonObjectBodies(string json)
        {
            List<string> bodies = new List<string>();
            if (string.IsNullOrEmpty(json))
                return bodies;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            int start = -1;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{')
                {
                    if (depth == 0)
                        start = i;
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        bodies.Add(json.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return bodies;
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
