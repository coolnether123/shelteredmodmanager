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
        private const string DisableUnityLogSuppressionOptionId = "ModAPI.DisableUnityLogSuppression";
        private readonly string _optionsPath;
        private readonly ManagerBooleanOptionDefinition[] _builtInOptions;
        private readonly HashSet<string> _activeApiOwners;
        private static readonly ManagerBooleanOptionDefinition[] SharedBuiltInOptions = new ManagerBooleanOptionDefinition[]
        {
            new ManagerBooleanOptionDefinition
            {
                id = DisableUnityLogSuppressionOptionId,
                owner = "ModAPI",
                label = "Disable Unity Log Suppression",
                description = "Mirrors all Unity warnings/logs into SMM logs. Errors, asserts, and exceptions are always mirrored regardless of this option.",
                defaultValue = false,
                requiresRestart = true,
                sortOrder = 20
            }
        };

        public ManagerBooleanOptionsService()
            : this(null, null)
        {
        }

        public ManagerBooleanOptionsService(IEnumerable<ManagerBooleanOptionDefinition> profileBuiltInOptions, IEnumerable<string> activeApiOwners)
        {
            _optionsPath = Path.Combine(ResolveBinDirectory(), OptionsFileName);
            _builtInOptions = BuildBuiltInOptions(profileBuiltInOptions);
            _activeApiOwners = BuildActiveApiOwners(activeApiOwners);
        }

        public IList<ManagerBooleanOptionRecord> Load()
        {
            ManagerBooleanOptionsFile file = LoadFile();
            if (EnsureBuiltInOptions(file))
                SaveFile(file);

            List<ManagerBooleanOptionRecord> options = new List<ManagerBooleanOptionRecord>();
            if (file.booleans != null)
            {
                for (int i = 0; i < file.booleans.Length; i++)
                {
                    ManagerBooleanOptionRecord record = file.booleans[i];
                    if (record == null)
                        continue;
                    if (!ShouldShowOption(record))
                        continue;
                    options.Add(record);
                }
            }

            options.Sort(CompareOptions);
            return options;
        }

        public void SetBool(string id, bool value)
        {
            if (string.IsNullOrEmpty(id))
                return;

            ManagerBooleanOptionsFile file = LoadFile();
            EnsureBuiltInOptions(file);
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

        private bool EnsureBuiltInOptions(ManagerBooleanOptionsFile file)
        {
            if (file == null)
                return false;
            if (file.booleans == null)
                file.booleans = new ManagerBooleanOptionRecord[0];

            bool changed = false;
            for (int i = 0; i < _builtInOptions.Length; i++)
            {
                ManagerBooleanOptionDefinition definition = _builtInOptions[i];
                if (definition == null || string.IsNullOrEmpty(definition.id))
                    continue;

                int index = FindIndex(file.booleans, definition.id);
                if (index >= 0)
                {
                    ManagerBooleanOptionRecord existing = file.booleans[index];
                    if (existing == null)
                    {
                        file.booleans[index] = CreateRecord(definition, definition.defaultValue);
                        changed = true;
                        continue;
                    }

                    changed |= UpdateMetadata(existing, definition);
                    continue;
                }

                file.booleans = Append(file.booleans, CreateRecord(definition, definition.defaultValue));
                changed = true;
            }

            return changed;
        }

        private static ManagerBooleanOptionDefinition[] BuildBuiltInOptions(IEnumerable<ManagerBooleanOptionDefinition> profileBuiltInOptions)
        {
            List<ManagerBooleanOptionDefinition> options = new List<ManagerBooleanOptionDefinition>();
            options.AddRange(SharedBuiltInOptions);
            if (profileBuiltInOptions != null)
            {
                foreach (ManagerBooleanOptionDefinition option in profileBuiltInOptions)
                {
                    if (option != null && !string.IsNullOrEmpty(option.id))
                        options.Add(option);
                }
            }
            return options.ToArray();
        }

        private static HashSet<string> BuildActiveApiOwners(IEnumerable<string> activeApiOwners)
        {
            HashSet<string> owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            owners.Add("ModAPI");
            if (activeApiOwners != null)
            {
                foreach (string owner in activeApiOwners)
                {
                    if (!string.IsNullOrEmpty(owner))
                        owners.Add(owner);
                }
            }
            return owners;
        }

        private bool ShouldShowOption(ManagerBooleanOptionRecord record)
        {
            if (record == null)
                return false;

            string owner = record.owner ?? string.Empty;
            if (owner.Length == 0 || _activeApiOwners.Contains(owner))
                return true;

            return !IsInactiveApiOwnedOption(record, owner);
        }

        private static bool IsInactiveApiOwnedOption(ManagerBooleanOptionRecord record, string owner)
        {
            if (string.IsNullOrEmpty(owner))
                return false;

            bool apiOwner = owner.EndsWith("API", StringComparison.OrdinalIgnoreCase) ||
                owner.IndexOf(".API", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!apiOwner)
                return false;

            string id = record != null ? record.id : string.Empty;
            return string.IsNullOrEmpty(id) ||
                id.StartsWith(owner + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static ManagerBooleanOptionRecord CreateRecord(ManagerBooleanOptionDefinition definition, bool value)
        {
            return new ManagerBooleanOptionRecord
            {
                id = definition.id,
                owner = definition.owner ?? string.Empty,
                label = definition.label ?? definition.id,
                description = definition.description ?? string.Empty,
                value = value,
                defaultValue = definition.defaultValue,
                requiresRestart = definition.requiresRestart,
                sortOrder = definition.sortOrder
            };
        }

        private static bool UpdateMetadata(ManagerBooleanOptionRecord record, ManagerBooleanOptionDefinition definition)
        {
            bool changed = false;
            changed |= SetStringIfDifferent(ref record.owner, definition.owner ?? string.Empty);
            changed |= SetStringIfDifferent(ref record.label, definition.label ?? definition.id);
            changed |= SetStringIfDifferent(ref record.description, definition.description ?? string.Empty);

            if (record.defaultValue != definition.defaultValue)
            {
                record.defaultValue = definition.defaultValue;
                changed = true;
            }

            if (record.requiresRestart != definition.requiresRestart)
            {
                record.requiresRestart = definition.requiresRestart;
                changed = true;
            }

            if (record.sortOrder != definition.sortOrder)
            {
                record.sortOrder = definition.sortOrder;
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

        private static int FindIndex(ManagerBooleanOptionRecord[] records, string id)
        {
            if (records == null || string.IsNullOrEmpty(id))
                return -1;

            for (int i = 0; i < records.Length; i++)
            {
                ManagerBooleanOptionRecord record = records[i];
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
}
