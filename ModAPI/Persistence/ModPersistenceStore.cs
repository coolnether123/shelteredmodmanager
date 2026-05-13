using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Persistence
{
    internal sealed class ModPersistenceStore
    {
        private readonly string _modId;

        internal ModPersistenceStore(string modId)
        {
            _modId = modId;
        }

        internal string Serialize(Dictionary<string, object> registeredData)
        {
            var containerObj = new ModPersistenceData();
            foreach (var kv in registeredData)
            {
                containerObj.entries.Add(new ModDataEntry { key = kv.Key, json = JsonUtility.ToJson(kv.Value) });
            }

            return SerializeContainer(containerObj);
        }

        internal void Write(string rootPath, string json)
        {
            string modFilePath = GetCurrentFilePath(rootPath);
            string modDataFolder = Path.GetDirectoryName(modFilePath);
            if (!Directory.Exists(modDataFolder)) Directory.CreateDirectory(modDataFolder);

            File.WriteAllText(modFilePath, json);
            CleanupLegacyFile(rootPath);
        }

        internal ModPersistenceLoadResult Load(string rootPath)
        {
            string modFilePath = ResolveReadableFilePath(rootPath);
            if (modFilePath == null) return null;

            string json = File.ReadAllText(modFilePath);
            var container = DeserializeContainer(json);
            return new ModPersistenceLoadResult(container, Path.GetFileName(modFilePath), IsLegacyFilePath(rootPath, modFilePath));
        }

        internal string GetCurrentFilePath(string rootPath)
        {
            return Path.Combine(Path.Combine(Path.Combine(rootPath, "mods"), _modId), "data.json");
        }

        private string ResolveReadableFilePath(string rootPath)
        {
            string currentFilePath = GetCurrentFilePath(rootPath);
            if (File.Exists(currentFilePath)) return currentFilePath;

            string legacyFilePath = GetLegacyFilePath(rootPath);
            return File.Exists(legacyFilePath) ? legacyFilePath : null;
        }

        private void CleanupLegacyFile(string rootPath)
        {
            string legacyFilePath = GetLegacyFilePath(rootPath);
            if (!File.Exists(legacyFilePath)) return;

            try
            {
                File.Delete(legacyFilePath);
                MMLog.WriteInfo("[SaveSystem] Migration complete for " + _modId + ": Cleaned up legacy file " + Path.GetFileName(legacyFilePath));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveSystem] Failed to clean up legacy file for " + _modId + ": " + ex.Message);
            }
        }

        private bool IsLegacyFilePath(string rootPath, string filePath)
        {
            return string.Equals(GetLegacyFilePath(rootPath), filePath, StringComparison.OrdinalIgnoreCase);
        }

        private string GetLegacyFilePath(string rootPath)
        {
            return Path.Combine(rootPath, "mod_" + _modId.Replace('.', '_') + "_data.json");
        }

        private static string SerializeContainer(ModPersistenceData data)
        {
            if (data == null)
            {
                data = new ModPersistenceData();
            }

            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("    \"entries\": [");

            bool wroteEntry = false;
            if (data.entries != null)
            {
                for (int i = 0; i < data.entries.Count; i++)
                {
                    ModDataEntry entry = data.entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    if (wroteEntry)
                    {
                        builder.AppendLine(",");
                    }

                    builder.AppendLine("        {");
                    AppendJsonString(builder, "key", entry.key, true, 12);
                    AppendJsonString(builder, "json", entry.json ?? string.Empty, false, 12);
                    builder.Append("        }");
                    wroteEntry = true;
                }
            }

            if (wroteEntry)
            {
                builder.AppendLine();
            }

            builder.AppendLine("    ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static ModPersistenceData DeserializeContainer(string json)
        {
            var data = new ModPersistenceData();
            if (string.IsNullOrEmpty(json))
            {
                return data;
            }

            string entriesJson = ExtractJsonArrayContent(json, "entries");
            if (entriesJson == null)
            {
                return data;
            }

            List<string> records = SplitJsonObjectBodies(entriesJson);
            for (int i = 0; i < records.Count; i++)
            {
                string key = ExtractJsonString(records[i], "key", string.Empty);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                data.entries.Add(new ModDataEntry
                {
                    key = key,
                    json = ExtractJsonString(records[i], "json", string.Empty)
                });
            }

            return data;
        }

        private static void AppendJsonString(StringBuilder builder, string name, string value, bool comma, int spaces)
        {
            builder.Append(new string(' ', spaces));
            builder.Append("\"").Append(EscapeJson(name)).Append("\": ");
            builder.Append("\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
            builder.AppendLine();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
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

        private static string ExtractJsonArrayContent(string json, string propertyName)
        {
            int nameIndex = FindJsonProperty(json, propertyName);
            if (nameIndex < 0)
            {
                return null;
            }

            int colon = json.IndexOf(':', nameIndex);
            if (colon < 0)
            {
                return null;
            }

            int start = -1;
            for (int i = colon + 1; i < json.Length; i++)
            {
                if (!char.IsWhiteSpace(json[i]))
                {
                    if (json[i] != '[')
                    {
                        return null;
                    }

                    start = i;
                    break;
                }
            }

            if (start < 0)
            {
                return null;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(start + 1, i - start - 1);
                    }
                }
            }

            return null;
        }

        private static List<string> SplitJsonObjectBodies(string json)
        {
            var records = new List<string>();
            if (string.IsNullOrEmpty(json))
            {
                return records;
            }

            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        records.Add(json.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return records;
        }

        private static int FindJsonProperty(string json, string propertyName)
        {
            string quoted = "\"" + propertyName + "\"";
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i <= json.Length - quoted.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    if (string.CompareOrdinal(json, i, quoted, 0, quoted.Length) == 0)
                    {
                        return i;
                    }
                    inString = true;
                }
            }

            return -1;
        }

        private static string ExtractJsonString(string json, string propertyName, string defaultValue)
        {
            int nameIndex = FindJsonProperty(json, propertyName);
            if (nameIndex < 0)
            {
                return defaultValue;
            }

            int colon = json.IndexOf(':', nameIndex);
            if (colon < 0)
            {
                return defaultValue;
            }

            int start = -1;
            for (int i = colon + 1; i < json.Length; i++)
            {
                if (!char.IsWhiteSpace(json[i]))
                {
                    if (json[i] != '"')
                    {
                        return defaultValue;
                    }

                    start = i + 1;
                    break;
                }
            }

            if (start < 0)
            {
                return defaultValue;
            }

            var builder = new StringBuilder();
            bool escaped = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    switch (c)
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
                            if (i + 4 < json.Length)
                            {
                                string hex = json.Substring(i + 1, 4);
                                int value;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value))
                                {
                                    builder.Append((char)value);
                                    i += 4;
                                }
                            }
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    return builder.ToString();
                }
                else
                {
                    builder.Append(c);
                }
            }

            return defaultValue;
        }
    }

    internal sealed class ModPersistenceLoadResult
    {
        internal ModPersistenceLoadResult(ModPersistenceData data, string fileName, bool isLegacy)
        {
            Data = data;
            FileName = fileName;
            IsLegacy = isLegacy;
        }

        internal ModPersistenceData Data { get; private set; }
        internal string FileName { get; private set; }
        internal bool IsLegacy { get; private set; }
    }
}
