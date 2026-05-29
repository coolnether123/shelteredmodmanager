using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ParalivesAPI.Core
{
    internal sealed class SmmLoadOrderStore
    {
        private readonly string _path;

        internal SmmLoadOrderStore(string gameRoot)
        {
            _path = Path.Combine(Path.Combine(gameRoot, "mods"), "loadorder.json");
        }

        internal Dictionary<string, bool> ReadEnabledMap(List<string> discoveredIds)
        {
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (discoveredIds == null)
                return result;

            bool fileExists = File.Exists(_path);
            HashSet<string> orderedIds = ReadOrderIds();
            for (int i = 0; i < discoveredIds.Count; i++)
            {
                string id = discoveredIds[i];
                if (string.IsNullOrEmpty(id))
                    continue;

                result[id] = !fileExists || orderedIds.Contains(id);
            }

            return result;
        }

        internal bool SetEnabled(string modId, bool enabled)
        {
            if (string.IsNullOrEmpty(modId))
                return false;

            try
            {
                LoadOrderDocument document = ReadDocument();
                if (Contains(document.Order, modId))
                {
                    if (!enabled)
                        document.Order.RemoveAll(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase));
                }
                else if (enabled)
                {
                    document.Order.Add(modId);
                }

                document.EnabledById[modId] = enabled;
                WriteDocument(document);
                return true;
            }
            catch (Exception ex)
            {
                ModAPI.Core.MMLog.WriteWarning("[ParalivesAPI] Failed to update SMM loadorder for " + modId + ": " + ex.Message);
                return false;
            }
        }

        private HashSet<string> ReadOrderIds()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LoadOrderDocument document = ReadDocument();
            for (int i = 0; i < document.Order.Count; i++)
            {
                if (!string.IsNullOrEmpty(document.Order[i]))
                    result.Add(document.Order[i]);
            }

            return result;
        }

        private LoadOrderDocument ReadDocument()
        {
            var document = new LoadOrderDocument();
            if (!File.Exists(_path))
                return document;

            try
            {
                string json = File.ReadAllText(_path);
                AddOrderItems(document.Order, json);
                AddModStates(document.EnabledById, json);
                for (int i = 0; i < document.Order.Count; i++)
                {
                    if (!document.EnabledById.ContainsKey(document.Order[i]))
                        document.EnabledById[document.Order[i]] = true;
                }
            }
            catch (Exception ex)
            {
                ModAPI.Core.MMLog.WriteWarning("[ParalivesAPI] Failed to read SMM loadorder: " + ex.Message);
            }

            return document;
        }

        private void WriteDocument(LoadOrderDocument document)
        {
            string directory = Path.GetDirectoryName(_path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_path, WriteJson(document), Encoding.UTF8);
        }

        private static void AddOrderItems(List<string> order, string json)
        {
            string arrayBody = ExtractJsonArrayBody(json, "order");
            if (arrayBody == null)
                return;

            MatchCollection matches = Regex.Matches(
                arrayBody,
                "\"((?:\\\\.|[^\"\\\\])*)\"",
                RegexOptions.Singleline);

            for (int i = 0; i < matches.Count; i++)
            {
                string id = UnescapeJsonString(matches[i].Groups[1].Value);
                if (string.IsNullOrEmpty(id))
                    continue;

                if (!Contains(order, id))
                    order.Add(id);
            }
        }

        private static void AddModStates(Dictionary<string, bool> states, string json)
        {
            string objectBody = ExtractJsonObjectBody(json, "mods");
            if (objectBody == null)
                return;

            MatchCollection matches = Regex.Matches(
                objectBody,
                "\"((?:\\\\.|[^\"\\\\])*)\"\\s*:\\s*\\{(.*?)\\}",
                RegexOptions.Singleline);

            for (int i = 0; i < matches.Count; i++)
            {
                string id = UnescapeJsonString(matches[i].Groups[1].Value);
                string body = matches[i].Groups[2].Value;
                bool enabled = !Regex.IsMatch(
                    body,
                    "\"enabled\"\\s*:\\s*false",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (!string.IsNullOrEmpty(id))
                    states[id] = enabled;
            }
        }

        private static bool Contains(List<string> ids, string modId)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], modId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string WriteJson(LoadOrderDocument document)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"order\": [");
            for (int i = 0; i < document.Order.Count; i++)
            {
                builder.Append("    \"");
                builder.Append(EscapeJsonString(document.Order[i]));
                builder.Append("\"");
                if (i < document.Order.Count - 1)
                    builder.Append(",");
                builder.AppendLine();
            }
            builder.AppendLine("  ],");
            builder.AppendLine("  \"mods\": {");

            int index = 0;
            foreach (KeyValuePair<string, bool> state in document.EnabledById)
            {
                builder.Append("    \"");
                builder.Append(EscapeJsonString(state.Key));
                builder.AppendLine("\": {");
                builder.Append("      \"enabled\": ");
                builder.AppendLine(state.Value ? "true" : "false");
                builder.Append("    }");
                if (index < document.EnabledById.Count - 1)
                    builder.Append(",");
                builder.AppendLine();
                index++;
            }

            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string ExtractJsonArrayBody(string json, string propertyName)
        {
            int keyIndex = IndexOfJsonProperty(json, propertyName);
            if (keyIndex < 0)
                return null;

            int start = json.IndexOf('[', keyIndex);
            if (start < 0)
                return null;

            int end = FindMatching(json, start, '[', ']');
            if (end < 0)
                return null;

            return json.Substring(start + 1, end - start - 1);
        }

        private static string ExtractJsonObjectBody(string json, string propertyName)
        {
            int keyIndex = IndexOfJsonProperty(json, propertyName);
            if (keyIndex < 0)
                return null;

            int start = json.IndexOf('{', keyIndex);
            if (start < 0)
                return null;

            int end = FindMatching(json, start, '{', '}');
            if (end < 0)
                return null;

            return json.Substring(start + 1, end - start - 1);
        }

        private static int IndexOfJsonProperty(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
                return -1;

            return json.IndexOf("\"" + propertyName + "\"", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindMatching(string json, int start, char open, char close)
        {
            int depth = 0;
            bool quoted = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                    quoted = !quoted;

                if (quoted)
                    continue;

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string EscapeJsonString(string value)
        {
            if (value == null)
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string UnescapeJsonString(string value)
        {
            if (value == null)
                return string.Empty;

            return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private sealed class LoadOrderDocument
        {
            internal readonly List<string> Order = new List<string>();
            internal readonly Dictionary<string, bool> EnabledById = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
