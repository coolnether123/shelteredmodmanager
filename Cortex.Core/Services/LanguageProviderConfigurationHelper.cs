using System;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public static class LanguageProviderConfigurationHelper
    {
        public static LanguageProviderConfiguration FindConfiguration(CortexSettings settings, string providerId)
        {
            var configurations = settings != null ? settings.LanguageProviderConfigurations : null;
            if (configurations != null)
            {
                for (var i = 0; i < configurations.Length; i++)
                {
                    var candidate = configurations[i];
                    if (candidate == null || !string.Equals(candidate.ProviderId ?? string.Empty, providerId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return Clone(candidate);
                }
            }

            return new LanguageProviderConfiguration
            {
                ProviderId = providerId ?? string.Empty,
                Settings = new LanguageProviderSettingValue[0]
            };
        }

        public static string GetSettingValue(LanguageProviderConfiguration configuration, string settingId)
        {
            var settings = configuration != null ? configuration.Settings : null;
            if (settings == null || string.IsNullOrEmpty(settingId))
            {
                return string.Empty;
            }

            for (var i = 0; i < settings.Length; i++)
            {
                var setting = settings[i];
                if (setting != null && string.Equals(setting.SettingId ?? string.Empty, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    return setting.Value ?? string.Empty;
                }
            }

            return string.Empty;
        }

        public static string GetSettingValue(CortexSettings settings, string providerId, string settingId)
        {
            return GetSettingValue(FindConfiguration(settings, providerId), settingId);
        }

        public static void SetSettingValue(CortexSettings settings, string providerId, string settingId, string value)
        {
            if (settings == null || string.IsNullOrEmpty(providerId) || string.IsNullOrEmpty(settingId))
            {
                return;
            }

            var configurations = settings.LanguageProviderConfigurations ?? new LanguageProviderConfiguration[0];
            var configurationIndex = -1;
            for (var i = 0; i < configurations.Length; i++)
            {
                var candidate = configurations[i];
                if (candidate != null && string.Equals(candidate.ProviderId ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    configurationIndex = i;
                    break;
                }
            }

            var updatedConfiguration = configurationIndex >= 0
                ? Clone(configurations[configurationIndex])
                : new LanguageProviderConfiguration { ProviderId = providerId, Settings = new LanguageProviderSettingValue[0] };

            var updatedSettings = new System.Collections.Generic.List<LanguageProviderSettingValue>();
            var existingSettings = updatedConfiguration.Settings ?? new LanguageProviderSettingValue[0];
            var replaced = false;
            for (var i = 0; i < existingSettings.Length; i++)
            {
                var entry = existingSettings[i];
                if (entry == null)
                {
                    continue;
                }

                if (string.Equals(entry.SettingId ?? string.Empty, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    updatedSettings.Add(new LanguageProviderSettingValue
                    {
                        SettingId = settingId,
                        Value = value ?? string.Empty
                    });
                    replaced = true;
                    continue;
                }

                updatedSettings.Add(new LanguageProviderSettingValue
                {
                    SettingId = entry.SettingId ?? string.Empty,
                    Value = entry.Value ?? string.Empty
                });
            }

            if (!replaced)
            {
                updatedSettings.Add(new LanguageProviderSettingValue
                {
                    SettingId = settingId,
                    Value = value ?? string.Empty
                });
            }

            updatedConfiguration.ProviderId = providerId ?? string.Empty;
            updatedConfiguration.Settings = updatedSettings.ToArray();

            var configurationList = new System.Collections.Generic.List<LanguageProviderConfiguration>();
            for (var i = 0; i < configurations.Length; i++)
            {
                if (i == configurationIndex)
                {
                    configurationList.Add(updatedConfiguration);
                    continue;
                }

                if (configurations[i] != null)
                {
                    configurationList.Add(Clone(configurations[i]));
                }
            }

            if (configurationIndex < 0)
            {
                configurationList.Add(updatedConfiguration);
            }

            settings.LanguageProviderConfigurations = configurationList.ToArray();
        }

        public static LanguageProviderConfiguration Clone(LanguageProviderConfiguration configuration)
        {
            if (configuration == null)
            {
                return new LanguageProviderConfiguration();
            }

            var settings = configuration.Settings ?? new LanguageProviderSettingValue[0];
            var clonedSettings = new LanguageProviderSettingValue[settings.Length];
            for (var i = 0; i < settings.Length; i++)
            {
                clonedSettings[i] = settings[i] != null
                    ? new LanguageProviderSettingValue
                    {
                        SettingId = settings[i].SettingId ?? string.Empty,
                        Value = settings[i].Value ?? string.Empty
                    }
                    : new LanguageProviderSettingValue();
            }

            return new LanguageProviderConfiguration
            {
                ProviderId = configuration.ProviderId ?? string.Empty,
                Settings = clonedSettings
            };
        }
    }
}
