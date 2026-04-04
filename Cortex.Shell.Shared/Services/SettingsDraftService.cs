using System;
using System.Globalization;
using System.Reflection;
using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Shared.Services
{
    public sealed class SettingsDraftService
    {
        public void Initialize(SettingsDraftState draftState, WorkbenchCatalogSnapshot catalog, ShellSettings settings)
        {
            if (draftState == null)
            {
                return;
            }

            draftState.Values.Clear();
            draftState.LoadedValues.Clear();

            var effectiveSettings = settings ?? new ShellSettings();
            if (catalog == null)
            {
                return;
            }

            for (var i = 0; i < catalog.Settings.Count; i++)
            {
                var setting = catalog.Settings[i];
                if (setting == null || string.IsNullOrEmpty(setting.SettingId))
                {
                    continue;
                }

                var value = ReadValue(effectiveSettings, setting);
                draftState.Values[setting.SettingId] = value;
                draftState.LoadedValues[setting.SettingId] = value;
            }
        }

        public void Apply(SettingsDraftState draftState, WorkbenchCatalogSnapshot catalog, ShellSettings settings)
        {
            if (draftState == null || catalog == null || settings == null)
            {
                return;
            }

            for (var i = 0; i < catalog.Settings.Count; i++)
            {
                var setting = catalog.Settings[i];
                if (setting == null || string.IsNullOrEmpty(setting.SettingId))
                {
                    continue;
                }

                string value;
                if (!draftState.Values.TryGetValue(setting.SettingId, out value))
                {
                    value = setting.DefaultValue ?? string.Empty;
                }

                WriteValue(settings, setting, value);
            }
        }

        public bool IsModified(SettingsDraftState draftState, string settingId)
        {
            if (draftState == null || string.IsNullOrEmpty(settingId))
            {
                return false;
            }

            string currentValue;
            string loadedValue;
            if (!draftState.Values.TryGetValue(settingId, out currentValue))
            {
                currentValue = string.Empty;
            }

            if (!draftState.LoadedValues.TryGetValue(settingId, out loadedValue))
            {
                loadedValue = string.Empty;
            }

            return !string.Equals(currentValue, loadedValue, StringComparison.Ordinal);
        }

        private static string ReadValue(ShellSettings settings, SettingDescriptor setting)
        {
            var property = typeof(ShellSettings).GetProperty(setting.SettingId, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || settings == null)
            {
                return setting.DefaultValue ?? string.Empty;
            }

            var value = property.GetValue(settings, null);
            if (value == null)
            {
                return setting.DefaultValue ?? string.Empty;
            }

            switch (setting.ValueKind)
            {
                case ShellSettingValueKind.Boolean:
                    return (bool)value ? "true" : "false";
                case ShellSettingValueKind.Integer:
                    return ((int)value).ToString(CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        private static void WriteValue(ShellSettings settings, SettingDescriptor setting, string serializedValue)
        {
            var property = typeof(ShellSettings).GetProperty(setting.SettingId, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || settings == null)
            {
                return;
            }

            switch (setting.ValueKind)
            {
                case ShellSettingValueKind.Boolean:
                    property.SetValue(settings, string.Equals(serializedValue, "true", StringComparison.OrdinalIgnoreCase), null);
                    break;
                case ShellSettingValueKind.Integer:
                    int integerValue;
                    property.SetValue(settings, int.TryParse(serializedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue) ? integerValue : 0, null);
                    break;
                default:
                    property.SetValue(settings, serializedValue ?? string.Empty, null);
                    break;
            }
        }
    }
}
