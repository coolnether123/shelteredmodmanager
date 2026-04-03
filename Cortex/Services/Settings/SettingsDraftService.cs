using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Cortex.Core.Models;
using Cortex.Presentation.Models;

namespace Cortex.Services.Settings
{
    internal sealed class SettingsDraftService
    {
        public void Initialize(
            SettingsDraftState draftState,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexSettings settings)
        {
            if (draftState == null)
            {
                return;
            }

            draftState.TextValues.Clear();
            draftState.ToggleValues.Clear();
            draftState.LoadedSerializedValues.Clear();
            draftState.ValidationResults.Clear();
            draftState.LoadedModPathDrafts.Clear();

            var effectiveSettings = settings ?? new CortexSettings();
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    LoadContributionValue(draftState, snapshot.Settings[i], effectiveSettings);
                }
            }

            draftState.TextValues["editor.undo.limit"] = effectiveSettings.EditorUndoHistoryLimit.ToString(CultureInfo.InvariantCulture);
            draftState.SelectedThemeId = !string.IsNullOrEmpty(effectiveSettings.ThemeId)
                ? effectiveSettings.ThemeId
                : themeState != null && !string.IsNullOrEmpty(themeState.ThemeId)
                    ? themeState.ThemeId
                    : "cortex.vs-dark";
        }

        public void LoadContributionValue(SettingsDraftState draftState, SettingContribution contribution, CortexSettings settings)
        {
            if (draftState == null || contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            var serializedValue = ReadPersistedContributionValue(contribution, settings);
            draftState.LoadedSerializedValues[contribution.SettingId] = serializedValue;
            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                draftState.ToggleValues[contribution.SettingId] = string.Equals(serializedValue, "true", StringComparison.OrdinalIgnoreCase);
                return;
            }

            draftState.TextValues[contribution.SettingId] = serializedValue;
        }

        public void ApplyDraft(SettingsDraftState draftState, WorkbenchPresentationSnapshot snapshot, CortexSettings settings)
        {
            if (draftState == null || settings == null)
            {
                return;
            }

            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    ApplyContributionValue(draftState, snapshot.Settings[i], settings);
                }
            }

            settings.ThemeId = string.IsNullOrEmpty(draftState.SelectedThemeId) ? "cortex.vs-dark" : draftState.SelectedThemeId;
        }

        public void ApplyContributionValue(SettingsDraftState draftState, SettingContribution contribution, CortexSettings settings)
        {
            if (draftState == null || contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            WritePersistedContributionValue(contribution, settings, GetSerializedDraftValue(draftState, contribution));
        }

        public void SetDraftSerializedValue(SettingsDraftState draftState, SettingContribution contribution, string serializedValue)
        {
            if (draftState == null || contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            serializedValue = NormalizeSerializedValue(contribution, serializedValue);
            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                draftState.ToggleValues[contribution.SettingId] = string.Equals(serializedValue, "true", StringComparison.OrdinalIgnoreCase);
                return;
            }

            draftState.TextValues[contribution.SettingId] = serializedValue;
        }

        public bool IsSettingModified(SettingsDraftState draftState, SettingContribution contribution, CortexSettings settings)
        {
            if (draftState == null || contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return false;
            }

            string loadedValue;
            if (!draftState.LoadedSerializedValues.TryGetValue(contribution.SettingId, out loadedValue))
            {
                loadedValue = ReadPersistedContributionValue(contribution, settings);
                draftState.LoadedSerializedValues[contribution.SettingId] = loadedValue;
            }

            return !string.Equals(
                NormalizeSerializedValue(contribution, loadedValue),
                GetSerializedDraftValue(draftState, contribution),
                StringComparison.Ordinal);
        }

        public SettingValidationResult GetValidationResult(SettingsDraftState draftState, SettingContribution contribution)
        {
            if (draftState == null || contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return null;
            }

            var serializedValue = GetSerializedDraftValue(draftState, contribution);
            var result = contribution.ValidateValue != null
                ? contribution.ValidateValue(serializedValue)
                : BuildDefaultValidationResult(contribution, serializedValue);

            draftState.ValidationResults[contribution.SettingId] = result ?? new SettingValidationResult
            {
                Severity = SettingValidationSeverity.None,
                Message = string.Empty
            };

            return result;
        }

        public string GetDefaultSerializedValue(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            if (contribution.ReadDefaultValue != null)
            {
                return NormalizeSerializedValue(contribution, contribution.ReadDefaultValue());
            }

            return NormalizeSerializedValue(contribution, contribution.DefaultValue);
        }

        public string GetSerializedDraftValue(SettingsDraftState draftState, SettingContribution contribution)
        {
            if (draftState == null || contribution == null)
            {
                return string.Empty;
            }

            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                return GetToggleValue(draftState, contribution) ? "true" : "false";
            }

            return NormalizeSerializedValue(contribution, GetTextValue(draftState, contribution.SettingId, GetDefaultSerializedValue(contribution)));
        }

        public string GetTextValue(SettingsDraftState draftState, SettingContribution contribution)
        {
            return contribution != null
                ? GetTextValue(draftState, contribution.SettingId, GetDefaultSerializedValue(contribution))
                : string.Empty;
        }

        public string GetTextValue(SettingsDraftState draftState, string settingId, string defaultValue)
        {
            if (draftState == null)
            {
                return defaultValue ?? string.Empty;
            }

            string value;
            return !string.IsNullOrEmpty(settingId) && draftState.TextValues.TryGetValue(settingId, out value)
                ? value ?? string.Empty
                : defaultValue ?? string.Empty;
        }

        public bool GetToggleValue(SettingsDraftState draftState, SettingContribution contribution)
        {
            bool value;
            if (draftState != null && contribution != null && draftState.ToggleValues.TryGetValue(contribution.SettingId, out value))
            {
                return value;
            }

            return contribution != null && string.Equals(GetDefaultSerializedValue(contribution), "true", StringComparison.OrdinalIgnoreCase);
        }

        public string NormalizeSerializedValue(SettingContribution contribution, string rawValue)
        {
            var value = rawValue ?? string.Empty;
            if (contribution == null)
            {
                return value;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
                case SettingValueKind.Integer:
                    int integerValue;
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue)
                        ? integerValue.ToString(CultureInfo.InvariantCulture)
                        : value.Trim();
                case SettingValueKind.Float:
                    float floatValue;
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue)
                        ? floatValue.ToString(CultureInfo.InvariantCulture)
                        : value.Trim();
                case SettingValueKind.String:
                default:
                    return value;
            }
        }

        private SettingValidationResult BuildDefaultValidationResult(SettingContribution contribution, string serializedValue)
        {
            if (contribution == null)
            {
                return null;
            }

            if (contribution.IsRequired && IsNullOrWhiteSpaceCompat(serializedValue))
            {
                return CreateValidation(SettingValidationSeverity.Error, "A value is required.");
            }

            if (IsNullOrWhiteSpaceCompat(serializedValue))
            {
                return null;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Integer:
                    int integerValue;
                    if (!int.TryParse(serializedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                    {
                        return CreateValidation(SettingValidationSeverity.Error, "Enter a valid integer value.");
                    }
                    break;
                case SettingValueKind.Float:
                    float floatValue;
                    if (!float.TryParse(serializedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                    {
                        return CreateValidation(SettingValidationSeverity.Error, "Enter a valid numeric value.");
                    }
                    break;
            }

            var options = contribution.Options ?? new SettingChoiceOption[0];
            if (options.Length > 0 && FindChoiceIndex(options, serializedValue) < 0)
            {
                return CreateValidation(SettingValidationSeverity.Error, "Select one of the registered values.");
            }

            if (contribution.EditorKind == SettingEditorKind.Path &&
                !Directory.Exists(serializedValue) &&
                !File.Exists(serializedValue))
            {
                return CreateValidation(SettingValidationSeverity.Warning, "The path does not exist on disk.");
            }

            if (LooksLikeUrlSetting(contribution))
            {
                Uri uri;
                if (!Uri.TryCreate(serializedValue, UriKind.Absolute, out uri))
                {
                    return CreateValidation(SettingValidationSeverity.Warning, "Enter a valid absolute URL.");
                }
            }

            return null;
        }

        private static int FindChoiceIndex(SettingChoiceOption[] options, string serializedValue)
        {
            if (options == null)
            {
                return -1;
            }

            for (var i = 0; i < options.Length; i++)
            {
                var value = options[i] != null ? options[i].Value : string.Empty;
                if (string.Equals(value ?? string.Empty, serializedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool LooksLikeUrlSetting(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            return EndsWithOrdinalIgnoreCase(contribution.SettingId, "Url") ||
                EndsWithOrdinalIgnoreCase(contribution.SettingId, "Uri") ||
                StartsWithOrdinalIgnoreCase(contribution.PlaceholderText, "http://") ||
                StartsWithOrdinalIgnoreCase(contribution.PlaceholderText, "https://");
        }

        private static bool EndsWithOrdinalIgnoreCase(string value, string suffix)
        {
            return !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(suffix) &&
                value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWithOrdinalIgnoreCase(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(prefix) &&
                value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static SettingValidationResult CreateValidation(SettingValidationSeverity severity, string message)
        {
            return new SettingValidationResult
            {
                Severity = severity,
                Message = message ?? string.Empty
            };
        }

        private static bool IsNullOrWhiteSpaceCompat(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static FieldInfo GetSettingField(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return null;
            }

            return typeof(CortexSettings).GetField(contribution.SettingId, BindingFlags.Public | BindingFlags.Instance);
        }

        private string ReadPersistedContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            if (contribution.ReadSettingsValue != null)
            {
                return NormalizeSerializedValue(contribution, contribution.ReadSettingsValue(settings));
            }

            if (contribution.ReadValue != null)
            {
                return NormalizeSerializedValue(contribution, contribution.ReadValue());
            }

            var field = GetSettingField(contribution);
            if (field == null)
            {
                return ReadModuleSettingValue(settings, contribution.SettingId, GetDefaultSerializedValue(contribution));
            }

            if (settings == null)
            {
                return GetDefaultSerializedValue(contribution);
            }

            var value = field.GetValue(settings);
            if (value == null)
            {
                return GetDefaultSerializedValue(contribution);
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    return NormalizeSerializedValue(contribution, ((bool)value) ? "true" : "false");
                case SettingValueKind.Integer:
                    return NormalizeSerializedValue(contribution, ((int)value).ToString(CultureInfo.InvariantCulture));
                case SettingValueKind.Float:
                    return NormalizeSerializedValue(contribution, ((float)value).ToString(CultureInfo.InvariantCulture));
                case SettingValueKind.String:
                default:
                    return NormalizeSerializedValue(contribution, value.ToString());
            }
        }

        private void WritePersistedContributionValue(SettingContribution contribution, CortexSettings settings, string serializedValue)
        {
            if (contribution == null)
            {
                return;
            }

            serializedValue = NormalizeSerializedValue(contribution, serializedValue);
            if (contribution.WriteSettingsValue != null)
            {
                contribution.WriteSettingsValue(settings, serializedValue);
                return;
            }

            if (contribution.WriteValue != null)
            {
                contribution.WriteValue(serializedValue);
                return;
            }

            var field = GetSettingField(contribution);
            if (field == null)
            {
                WriteModuleSettingValue(settings, contribution.SettingId, serializedValue);
                return;
            }

            if (settings == null)
            {
                return;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    field.SetValue(settings, string.Equals(serializedValue, "true", StringComparison.OrdinalIgnoreCase));
                    break;
                case SettingValueKind.Integer:
                    field.SetValue(settings, ParseInt(serializedValue, ParseInt(GetDefaultSerializedValue(contribution), (int)field.GetValue(settings))));
                    break;
                case SettingValueKind.Float:
                    field.SetValue(settings, ParseFloat(serializedValue, ParseFloat(GetDefaultSerializedValue(contribution), (float)field.GetValue(settings))));
                    break;
                case SettingValueKind.String:
                default:
                    field.SetValue(settings, serializedValue);
                    break;
            }
        }

        private static string ReadModuleSettingValue(CortexSettings settings, string settingId, string fallback)
        {
            if (settings == null || string.IsNullOrEmpty(settingId) || settings.ModuleSettings == null)
            {
                return fallback ?? string.Empty;
            }

            for (var i = 0; i < settings.ModuleSettings.Length; i++)
            {
                var entry = settings.ModuleSettings[i];
                if (entry != null && string.Equals(entry.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value ?? string.Empty;
                }
            }

            return fallback ?? string.Empty;
        }

        private static void WriteModuleSettingValue(CortexSettings settings, string settingId, string serializedValue)
        {
            if (settings == null || string.IsNullOrEmpty(settingId))
            {
                return;
            }

            var entries = new List<ModuleSettingValue>();
            if (settings.ModuleSettings != null)
            {
                for (var i = 0; i < settings.ModuleSettings.Length; i++)
                {
                    if (settings.ModuleSettings[i] != null)
                    {
                        entries.Add(settings.ModuleSettings[i]);
                    }
                }
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    entries[i].Value = serializedValue ?? string.Empty;
                    settings.ModuleSettings = entries.ToArray();
                    return;
                }
            }

            entries.Add(new ModuleSettingValue
            {
                SettingId = settingId,
                Value = serializedValue ?? string.Empty
            });
            settings.ModuleSettings = entries.ToArray();
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            float value;
            return float.TryParse(raw, out value) ? value : fallback;
        }
    }
}
