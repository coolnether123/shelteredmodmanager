using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Runtime;

namespace Cortex.Host.Unity.Runtime
{
    public static class UnityRenderHostSettings
    {
        public const string RenderHostSettingId = RenderHostPresentationIds.RenderHostSettingId;
        public const string LegacyImguiRenderHostId = RenderHostPresentationIds.LegacyImguiRenderHostId;
        public const string ImguiRenderHostId = RenderHostPresentationIds.ImguiInProcessRenderHostId;
        public const string DearImguiRenderHostId = RenderHostPresentationIds.DearImguiInProcessRenderHostId;
        public const string OverlayInProcessRenderHostId = RenderHostPresentationIds.OverlayInProcessLegacyRenderHostId;
        public const string AvaloniaExternalRenderHostId = RenderHostPresentationIds.AvaloniaExternalRenderHostId;

        public static string LoadSelectedRenderHostId(ICortexHostEnvironment environment)
        {
            if (environment == null)
            {
                return DearImguiRenderHostId;
            }

            var store = new JsonCortexSettingsStore(environment.SettingsFilePath);
            return ReadSelectedRenderHostId(store.Load());
        }

        public static string ReadSelectedRenderHostId(CortexSettings settings)
        {
            var configuredValue = ReadModuleSettingValue(settings, RenderHostSettingId);
            return string.IsNullOrEmpty(configuredValue)
                ? DearImguiRenderHostId
                : NormalizeRenderHostId(configuredValue);
        }

        public static string NormalizeRenderHostId(string renderHostId)
        {
            if (string.Equals(renderHostId, LegacyImguiRenderHostId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(renderHostId, ImguiRenderHostId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(renderHostId, OverlayInProcessRenderHostId, System.StringComparison.OrdinalIgnoreCase))
            {
                return ImguiRenderHostId;
            }

            if (string.Equals(renderHostId, DearImguiRenderHostId, System.StringComparison.OrdinalIgnoreCase))
            {
                return DearImguiRenderHostId;
            }

            return DearImguiRenderHostId;
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
