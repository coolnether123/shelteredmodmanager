using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Loading
{
    internal static class ModLoadOrderReader
    {
        /// <summary>
        /// Reads and normalizes <c>loadorder.json</c> into a unique lowercase ID list.
        /// Returns null when the file is missing or unreadable, matching the loader's "enable all" fallback.
        /// </summary>
        internal static List<string> Read(string modsRoot)
        {
            var orderedIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = Path.Combine(modsRoot ?? string.Empty, "loadorder.json");
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path);
                string[] order = ReadOrderArray(json);

                if (order == null)
                {
                    MMLog.Write("loadorder.json exists but no readable 'order' array was found. Treating as explicit empty load order.");
                    return new List<string>();
                }

                foreach (string raw in order)
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    string id = raw.Trim().ToLowerInvariant();
                    if (seen.Add(id)) orderedIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Failed to read loadorder.json: " + ex.Message);
                return null;
            }

            return orderedIds;
        }

        private static string[] ReadOrderArray(string json)
        {
            SimpleLoadOrder obj = JsonUtility.FromJson<SimpleLoadOrder>(json);
            if (obj != null && obj.order != null) return obj.order;

            // Robust fallback parser for loadorder.json formats that JsonUtility can fail on.
            return TryExtractOrderArray(json);
        }

        private static string[] TryExtractOrderArray(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                int keyPos = json.IndexOf("\"order\"", StringComparison.OrdinalIgnoreCase);
                if (keyPos < 0) return null;

                int arrayStart = json.IndexOf('[', keyPos);
                if (arrayStart < 0) return null;

                int arrayEnd = FindArrayEnd(json, arrayStart);
                if (arrayEnd < 0) return null;

                string arrayBody = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                MatchCollection matches = Regex.Matches(
                    arrayBody,
                    "\"((?:\\\\.|[^\"\\\\])*)\"",
                    RegexOptions.Singleline);

                var result = new List<string>();
                for (int i = 0; i < matches.Count; i++)
                {
                    string raw = matches[i].Groups[1].Value;
                    if (!string.IsNullOrEmpty(raw))
                        result.Add(raw.Replace("\\\"", "\"").Replace("\\\\", "\\"));
                }

                return result.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static int FindArrayEnd(string json, int arrayStart)
        {
            int depth = 0;
            for (int i = arrayStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return -1;
        }

        [Serializable]
        private sealed class SimpleLoadOrder
        {
#pragma warning disable 0649
            public string[] order;
#pragma warning restore 0649
        }
    }
}
