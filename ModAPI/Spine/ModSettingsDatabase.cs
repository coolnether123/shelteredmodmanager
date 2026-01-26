using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Spine
{
    /// <summary>
    /// Interoperability Layer. Allows mods like "New Game Plus" to query settings
    /// from other mods at runtime.
    /// </summary>
    public static class ModSettingsDatabase
    {
        public static ISettingsProvider GetSettingsProvider(string modId)
        {
            var entry = ModRegistry.GetMod(modId);
            return entry?.SettingsProvider;
        }

        public static object GetSettingsObject(string modId)
        {
            return GetSettingsProvider(modId)?.GetSettingsObject();
        }

        public static IEnumerable<SettingDefinition> GetDefinitions(string modId)
        {
            return GetSettingsProvider(modId)?.GetSettings();
        }

        /// <summary>
        /// Attempts to write a value to another mod's settings.
        /// Only succeeds if AllowExternalWrite is true for that setting.
        /// </summary>
        public static bool TryWriteSetting(string modId, string settingId, object value)
        {
            var provider = GetSettingsProvider(modId);
            if (provider == null) return false;

            var settings = provider.GetSettingsObject();
            var definitions = provider.GetSettings();

            foreach (var def in definitions)
            {
                if (def.Id == settingId)
                {
                    if (!def.AllowExternalWrite) return false;

                    var field = settings.GetType().GetField(def.FieldName);
                    if (field != null)
                    {
                        field.SetValue(settings, value);
                        def.OnChanged?.Invoke(settings);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
