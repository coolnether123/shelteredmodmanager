using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Reads and writes runtime boolean options registered by ModAPI into SMM/bin/manager_options.json.
    /// </summary>
    public sealed class ManagerBooleanOptionsService
    {
        private const string OptionsFileName = "manager_options.json";
        private readonly string _optionsPath;

        public ManagerBooleanOptionsService()
        {
            _optionsPath = Path.Combine(ResolveBinDirectory(), OptionsFileName);
        }

        public IList<ManagerBooleanOptionRecord> Load()
        {
            ManagerBooleanOptionsFile file = LoadFile();
            List<ManagerBooleanOptionRecord> options = new List<ManagerBooleanOptionRecord>();
            if (file.booleans != null)
                options.AddRange(file.booleans);

            options.Sort(CompareOptions);
            return options;
        }

        public void SetBool(string id, bool value)
        {
            if (string.IsNullOrEmpty(id))
                return;

            ManagerBooleanOptionsFile file = LoadFile();
            if (file.booleans == null)
                file.booleans = new ManagerBooleanOptionRecord[0];

            for (int i = 0; i < file.booleans.Length; i++)
            {
                ManagerBooleanOptionRecord record = file.booleans[i];
                if (record == null || !string.Equals(record.id, id, StringComparison.OrdinalIgnoreCase))
                    continue;

                record.value = value;
                SaveFile(file);
                return;
            }
        }

        private ManagerBooleanOptionsFile LoadFile()
        {
            try
            {
                if (File.Exists(_optionsPath))
                {
                    string json = File.ReadAllText(_optionsPath);
                    ManagerBooleanOptionsFile file = new JavaScriptSerializer().Deserialize<ManagerBooleanOptionsFile>(json);
                    if (file != null)
                    {
                        if (file.version <= 0)
                            file.version = 1;
                        if (file.booleans == null)
                            file.booleans = new ManagerBooleanOptionRecord[0];
                        return file;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to read runtime manager options: " + ex.Message);
            }

            return new ManagerBooleanOptionsFile();
        }

        private void SaveFile(ManagerBooleanOptionsFile file)
        {
            try
            {
                if (file == null)
                    file = new ManagerBooleanOptionsFile();
                if (file.version <= 0)
                    file.version = 1;
                if (file.booleans == null)
                    file.booleans = new ManagerBooleanOptionRecord[0];

                string dir = Path.GetDirectoryName(_optionsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tmp = _optionsPath + ".tmp";
                string json = new JavaScriptSerializer().Serialize(file);
                File.WriteAllText(tmp, json);
                if (File.Exists(_optionsPath))
                    File.Delete(_optionsPath);
                File.Move(tmp, _optionsPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to write runtime manager options: " + ex.Message);
            }
        }

        private static string ResolveBinDirectory()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string smmDir = Path.Combine(exeDir, "SMM");

            if (Directory.Exists(smmDir))
                return Path.Combine(smmDir, "bin");

            return Path.Combine(exeDir, "bin");
        }

        private static int CompareOptions(ManagerBooleanOptionRecord left, ManagerBooleanOptionRecord right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int sort = left.sortOrder.CompareTo(right.sortOrder);
            if (sort != 0) return sort;

            sort = string.Compare(left.owner ?? string.Empty, right.owner ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (sort != 0) return sort;

            return string.Compare(left.label ?? left.id, right.label ?? right.id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
