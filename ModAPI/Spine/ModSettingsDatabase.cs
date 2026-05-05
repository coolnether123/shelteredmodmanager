using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Spine
{
    /// <summary>
    /// Runtime lookup for settings exposed by loaded mods.
    /// Use this when one mod needs to read or, when explicitly allowed, write another mod's settings.
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

            SettingDefinition targetDefinition = null;
            var definitions = provider.GetSettings();
            foreach (var def in definitions)
            {
                if (def.Id == settingId)
                {
                    targetDefinition = def;
                    break;
                }
            }

            if (targetDefinition == null || !targetDefinition.AllowExternalWrite)
                return false;

            ISettingsProvider3 layeredProvider = provider as ISettingsProvider3;
            if (layeredProvider != null)
                return layeredProvider.TrySaveSetting(settingId, value, SettingsWriteTarget.DeclaredScope);

            var settings = provider.GetSettingsObject();
            foreach (var def in provider.GetSettings())
            {
                if (def.Id == settingId)
                {
                    if (def.Validate != null && !def.Validate(value, settings))
                        return false;

                    if (def.Setter != null)
                    {
                        def.Setter(settings, value);
                        def.OnChanged?.Invoke(settings);
                        ISettingsProvider2 persistentProvider = provider as ISettingsProvider2;
                        if (persistentProvider != null)
                            persistentProvider.Save();
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
