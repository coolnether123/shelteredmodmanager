using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using ModAPI.Core;
using ShelteredModManager.Shared.ScenarioEditor;

namespace Manager.Core.Services
{
    /// <summary>Desktop serialization and path adapter for runtime-owned boolean options.</summary>
    internal sealed class ManagerBooleanOptionsService
    {
        private const string OptionsFileName = "manager_options.json";
        private const string DisableUnityLogSuppressionOptionId = "ModAPI.DisableUnityLogSuppression";
        private readonly string _optionsPath;
        private static readonly ManagerBooleanOptionDescriptor[] BuiltInOptions =
        {
            new ManagerBooleanOptionDescriptor
            {
                Id = ScenarioEditorBooleanOptionDescriptor.Id,
                Owner = ScenarioEditorBooleanOptionDescriptor.Owner,
                Label = ScenarioEditorBooleanOptionDescriptor.Label,
                Description = ScenarioEditorBooleanOptionDescriptor.Description,
                DefaultValue = ScenarioEditorBooleanOptionDescriptor.DefaultValue,
                RequiresRestart = ScenarioEditorBooleanOptionDescriptor.RequiresRestart,
                SortOrder = ScenarioEditorBooleanOptionDescriptor.SortOrder
            },
            new ManagerBooleanOptionDescriptor
            {
                Id = DisableUnityLogSuppressionOptionId,
                Owner = "ModAPI",
                Label = "Disable Unity Log Suppression",
                Description = "Mirrors all Unity warnings/logs into SMM logs. Errors, asserts, and exceptions are always mirrored regardless of this option.",
                DefaultValue = false,
                RequiresRestart = true,
                SortOrder = 20
            }
        };

        internal ManagerBooleanOptionsService()
        {
            _optionsPath = Path.Combine(ResolveBinDirectory(), OptionsFileName);
        }

        internal IList<ManagerBooleanOptionRecord> Load()
        {
            ManagerBooleanOptionsFile file = LoadFile();
            if (EnsureBuiltInOptions(file)) SaveFile(file);
            List<ManagerBooleanOptionRecord> options = new List<ManagerBooleanOptionRecord>();
            if (file.booleans != null) options.AddRange(file.booleans);
            options.Sort(CompareOptions);
            return options;
        }

        internal void SetBool(string id, bool value)
        {
            if (string.IsNullOrEmpty(id)) return;
            ManagerBooleanOptionsFile file = LoadFile();
            EnsureBuiltInOptions(file);
            if (ManagerBooleanOptionPolicy.TrySetValue(file, id, value)) SaveFile(file);
        }

        private static bool EnsureBuiltInOptions(ManagerBooleanOptionsFile file)
        {
            if (file == null) return false;
            bool changed = false;
            for (int i = 0; i < BuiltInOptions.Length; i++)
                changed |= ManagerBooleanOptionPolicy.MergeDefinition(file, BuiltInOptions[i]);
            return changed;
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
                        ManagerBooleanOptionPolicy.Normalize(file);
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
                if (file == null) file = new ManagerBooleanOptionsFile();
                ManagerBooleanOptionPolicy.Normalize(file);
                string dir = Path.GetDirectoryName(_optionsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmp = _optionsPath + ".tmp";
                string json = new JavaScriptSerializer().Serialize(file);
                File.WriteAllText(tmp, json);
                if (File.Exists(_optionsPath)) File.Delete(_optionsPath);
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
            return Directory.Exists(smmDir) ? Path.Combine(smmDir, "bin") : Path.Combine(exeDir, "bin");
        }

        private static int CompareOptions(ManagerBooleanOptionRecord left, ManagerBooleanOptionRecord right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int sort = left.sortOrder.CompareTo(right.sortOrder);
            if (sort != 0) return sort;
            sort = string.Compare(left.owner ?? string.Empty, right.owner ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return sort != 0 ? sort : string.Compare(left.label ?? left.id, right.label ?? right.id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
