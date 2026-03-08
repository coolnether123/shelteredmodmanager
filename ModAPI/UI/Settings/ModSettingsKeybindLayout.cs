using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Spine;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal sealed class ModSettingsKeybindDisplayEntry
    {
        public readonly SettingDefinition Primary;
        public readonly SettingDefinition Secondary;

        public ModSettingsKeybindDisplayEntry(SettingDefinition primary, SettingDefinition secondary)
        {
            Primary = primary;
            Secondary = secondary;
        }
    }

    internal static class ModSettingsKeybindLayout
    {
        internal static bool ShouldUseWideKeybindLayout(List<SettingDefinition> visibleItems, List<SettingDefinition> allDefs)
        {
            return HasPairedKeybindDefinitions(visibleItems) || HasPairedKeybindDefinitions(allDefs);
        }

        internal static List<ModSettingsKeybindDisplayEntry> BuildDisplayEntries(List<SettingDefinition> visibleItems, List<SettingDefinition> allDefs, bool pairKeybinds)
        {
            var entries = new List<ModSettingsKeybindDisplayEntry>();
            if (visibleItems == null)
                return entries;

            if (!pairKeybinds)
            {
                for (int i = 0; i < visibleItems.Count; i++)
                {
                    SettingDefinition def = visibleItems[i];
                    if (def != null)
                        entries.Add(new ModSettingsKeybindDisplayEntry(def, null));
                }

                return entries;
            }

            var visibleById = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);
            var allById = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);
            var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < visibleItems.Count; i++)
            {
                SettingDefinition def = visibleItems[i];
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    visibleById[def.Id] = def;
            }

            if (allDefs != null)
            {
                for (int i = 0; i < allDefs.Count; i++)
                {
                    SettingDefinition def = allDefs[i];
                    if (def != null && !string.IsNullOrEmpty(def.Id))
                        allById[def.Id] = def;
                }
            }

            for (int i = 0; i < visibleItems.Count; i++)
            {
                SettingDefinition def = visibleItems[i];
                if (def == null || string.IsNullOrEmpty(def.Id) || consumed.Contains(def.Id))
                    continue;

                if (def.Type != SettingType.Keybind)
                {
                    consumed.Add(def.Id);
                    entries.Add(new ModSettingsKeybindDisplayEntry(def, null));
                    continue;
                }

                string baseId = GetKeybindActionBaseId(def.Id);
                if (string.IsNullOrEmpty(baseId))
                {
                    consumed.Add(def.Id);
                    entries.Add(new ModSettingsKeybindDisplayEntry(def, null));
                    continue;
                }

                string primaryId = baseId + ".primary";
                string secondaryId = baseId + ".secondary";

                SettingDefinition primary = null;
                SettingDefinition secondary = null;
                visibleById.TryGetValue(primaryId, out primary);
                visibleById.TryGetValue(secondaryId, out secondary);

                if (primary == null)
                    allById.TryGetValue(primaryId, out primary);
                if (secondary == null)
                    allById.TryGetValue(secondaryId, out secondary);

                if (primary == null && secondary == null)
                {
                    consumed.Add(def.Id);
                    entries.Add(new ModSettingsKeybindDisplayEntry(def, null));
                    continue;
                }

                if (primary == null)
                    primary = def;

                consumed.Add(primary.Id);
                if (secondary != null)
                    consumed.Add(secondary.Id);

                entries.Add(new ModSettingsKeybindDisplayEntry(primary, secondary));
            }

            return entries;
        }

        internal static bool IsSectionHeaderEntry(ModSettingsKeybindDisplayEntry entry)
        {
            return entry != null
                && entry.Secondary == null
                && entry.Primary != null
                && (entry.Primary.Type == SettingType.Header
                    || (!string.IsNullOrEmpty(entry.Primary.Id)
                        && entry.Primary.Id.StartsWith("CatHeader_", StringComparison.OrdinalIgnoreCase)));
        }

        internal static string GetKeybindActionBaseId(string settingId)
        {
            if (string.IsNullOrEmpty(settingId))
                return null;

            if (settingId.EndsWith(".primary", StringComparison.OrdinalIgnoreCase))
                return settingId.Substring(0, settingId.Length - ".primary".Length);
            if (settingId.EndsWith(".secondary", StringComparison.OrdinalIgnoreCase))
                return settingId.Substring(0, settingId.Length - ".secondary".Length);

            return null;
        }

        internal static string GetActionLabel(SettingDefinition primaryDef, SettingDefinition secondaryDef)
        {
            if (primaryDef != null && !string.IsNullOrEmpty(primaryDef.Label))
                return primaryDef.Label.Replace(" (Alt)", string.Empty);

            if (secondaryDef != null && !string.IsNullOrEmpty(secondaryDef.Label))
                return secondaryDef.Label.Replace(" (Alt)", string.Empty);

            return "UNNAMED ACTION";
        }

        internal static string FormatKeyCode(KeyCode key)
        {
            if (key == KeyCode.None)
                return "UNBOUND";

            string raw = key.ToString();
            if (raw.StartsWith("Alpha", StringComparison.Ordinal) && raw.Length == 6)
                return raw.Substring(5);
            if (raw.StartsWith("Keypad", StringComparison.Ordinal))
                return "KP " + HumanizeKeyName(raw.Substring(6)).ToUpperInvariant();
            if (raw.EndsWith("Arrow", StringComparison.Ordinal))
                return raw.Replace("Arrow", string.Empty).ToUpperInvariant();
            if (raw == "Mouse0")
                return "MOUSE LEFT";
            if (raw == "Mouse1")
                return "MOUSE RIGHT";
            if (raw == "Mouse2")
                return "MOUSE MIDDLE";

            return HumanizeKeyName(raw).ToUpperInvariant();
        }

        internal static string HumanizeKeyName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length + 8);
            char prev = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                        sb.Append(' ');
                    prev = c;
                    continue;
                }

                bool addSpace =
                    i > 0 &&
                    ((char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev)))
                    || (char.IsDigit(c) && char.IsLetter(prev))
                    || (char.IsLetter(c) && char.IsDigit(prev)));

                if (addSpace && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    sb.Append(' ');

                sb.Append(c);
                prev = c;
            }

            return sb.ToString().Trim();
        }

        private static bool HasPairedKeybindDefinitions(List<SettingDefinition> items)
        {
            if (items == null)
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                SettingDefinition def = items[i];
                if (def == null || def.Type != SettingType.Keybind || string.IsNullOrEmpty(def.Id))
                    continue;

                if (GetKeybindActionBaseId(def.Id) != null)
                    return true;
            }

            return false;
        }
    }
}
