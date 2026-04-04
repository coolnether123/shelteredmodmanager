using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Sheltered.Runtime
{
    internal static class ShelteredRenderHostSettings
    {
        public const string RenderHostSettingId = "sheltered.renderHostId";
        public const string ImguiRenderHostId = "imgui";
        public const string AvaloniaExternalRenderHostId = "avalonia.external";

        public static string LoadSelectedRenderHostId(ICortexHostEnvironment environment)
        {
            if (environment == null)
            {
                return ImguiRenderHostId;
            }

            var store = new JsonCortexSettingsStore(environment.SettingsFilePath);
            return ReadSelectedRenderHostId(store.Load());
        }

        public static string ReadSelectedRenderHostId(CortexSettings settings)
        {
            return NormalizeRenderHostId(ReadModuleSettingValue(settings, RenderHostSettingId));
        }

        public static string NormalizeRenderHostId(string renderHostId)
        {
            if (string.Equals(renderHostId, AvaloniaExternalRenderHostId, System.StringComparison.OrdinalIgnoreCase))
            {
                return AvaloniaExternalRenderHostId;
            }

            return ImguiRenderHostId;
        }

        private static string ReadModuleSettingValue(CortexSettings settings, string settingId)
        {
            if (settings == null || string.IsNullOrEmpty(settingId) || settings.ModuleSettings == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < settings.ModuleSettings.Length; i++)
            {
                var entry = settings.ModuleSettings[i];
                if (entry != null &&
                    string.Equals(entry.SettingId, settingId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
