using System;

namespace ModAPI.Core
{
    /// <summary>Pure schema policy shared by the desktop and Unity persistence adapters.</summary>
    internal static class ManagerBooleanOptionPolicy
    {
        internal static void Normalize(ManagerBooleanOptionsFile file)
        {
            if (file == null) return;
            if (file.version <= 0) file.version = 1;
            if (file.booleans == null) file.booleans = new ManagerBooleanOptionRecord[0];
        }

        internal static bool MergeDefinition(ManagerBooleanOptionsFile file, ManagerBooleanOptionDescriptor definition)
        {
            if (file == null || definition == null || string.IsNullOrEmpty(definition.Id)) return false;
            Normalize(file);
            ManagerBooleanOptionRecord record = FindRecord(file, definition.Id);
            if (record == null)
            {
                ManagerBooleanOptionRecord[] next = new ManagerBooleanOptionRecord[file.booleans.Length + 1];
                Array.Copy(file.booleans, next, file.booleans.Length);
                next[next.Length - 1] = new ManagerBooleanOptionRecord
                {
                    id = definition.Id,
                    owner = definition.Owner ?? string.Empty,
                    label = definition.Label ?? definition.Id,
                    description = definition.Description ?? string.Empty,
                    value = definition.DefaultValue,
                    defaultValue = definition.DefaultValue,
                    requiresRestart = definition.RequiresRestart,
                    sortOrder = definition.SortOrder
                };
                file.booleans = next;
                return true;
            }

            bool changed = false;
            changed |= SetString(ref record.owner, definition.Owner ?? string.Empty);
            changed |= SetString(ref record.label, definition.Label ?? definition.Id);
            changed |= SetString(ref record.description, definition.Description ?? string.Empty);
            if (record.defaultValue != definition.DefaultValue) { record.defaultValue = definition.DefaultValue; changed = true; }
            if (record.requiresRestart != definition.RequiresRestart) { record.requiresRestart = definition.RequiresRestart; changed = true; }
            if (record.sortOrder != definition.SortOrder) { record.sortOrder = definition.SortOrder; changed = true; }
            return changed;
        }

        internal static ManagerBooleanOptionRecord FindRecord(ManagerBooleanOptionsFile file, string id)
        {
            if (file == null || string.IsNullOrEmpty(id)) return null;
            Normalize(file);
            for (int i = 0; i < file.booleans.Length; i++)
            {
                ManagerBooleanOptionRecord record = file.booleans[i];
                if (record != null && string.Equals(record.id, id, StringComparison.OrdinalIgnoreCase)) return record;
            }
            return null;
        }

        internal static bool TrySetValue(ManagerBooleanOptionsFile file, string id, bool value)
        {
            if (file == null || string.IsNullOrEmpty(id)) return false;
            ManagerBooleanOptionRecord record = FindRecord(file, id);
            if (record == null || record.value == value) return false;
            record.value = value;
            return true;
        }

        private static bool SetString(ref string target, string value)
        {
            if (string.Equals(target ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal)) return false;
            target = value ?? string.Empty;
            return true;
        }
    }
}
