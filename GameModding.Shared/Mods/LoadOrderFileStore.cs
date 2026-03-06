using System;
using System.Collections.Generic;
using System.IO;
using GameModding.Shared.Serialization;

namespace GameModding.Shared.Mods
{
    [Serializable]
    public sealed class LoadOrderFile
    {
        public string[] order;
    }

    public static class LoadOrderFileStore
    {
        public static string[] ReadOrder(string modsPath)
        {
            try
            {
                var orderPath = GetPath(modsPath);
                if (!File.Exists(orderPath))
                {
                    return new string[0];
                }

                var json = File.ReadAllText(orderPath);
                var data = ManualJson.Deserialize<LoadOrderFile>(json);
                return data != null && data.order != null ? data.order : new string[0];
            }
            catch
            {
                return new string[0];
            }
        }

        public static void WriteOrder(string modsPath, IEnumerable<string> modIds)
        {
            if (string.IsNullOrEmpty(modsPath))
            {
                throw new ArgumentNullException("modsPath");
            }

            if (!Directory.Exists(modsPath))
            {
                Directory.CreateDirectory(modsPath);
            }

            var ids = new List<string>();
            foreach (var modId in modIds)
            {
                if (!string.IsNullOrEmpty(modId))
                {
                    ids.Add(modId);
                }
            }

            var data = new LoadOrderFile { order = ids.ToArray() };
            var json = ManualJson.Serialize(data);
            File.WriteAllText(GetPath(modsPath), json);
        }

        public static string GetPath(string modsPath)
        {
            return Path.Combine(modsPath ?? string.Empty, "loadorder.json");
        }
    }
}
