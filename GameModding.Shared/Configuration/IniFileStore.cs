using System;
using System.Collections.Generic;
using System.IO;

namespace GameModding.Shared.Configuration
{
    public static class IniFileStore
    {
        public static Dictionary<string, string> Read(string path)
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return data;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }

                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }

                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    data[parts[0].Trim()] = parts[1].Trim();
                }
            }

            return data;
        }

        public static void Write(string path, IDictionary<string, string> data, string headerComment)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(headerComment))
            {
                lines.Add("# " + headerComment);
            }

            lines.Add("# Last modified: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add(string.Empty);

            var keys = new List<string>(data.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                lines.Add(key + "=" + (data[key] ?? string.Empty));
            }

            File.WriteAllLines(path, lines.ToArray());
        }
    }
}
