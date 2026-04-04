using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public static class UnityRenderHostSettingContributions
    {
        public static void Register(
            WorkbenchPluginContext context,
            UnityRenderHostCatalog renderHostCatalog,
            string scope,
            int sortOrder)
        {
            if (context == null)
            {
                return;
            }

            var resolvedRenderHostCatalog = renderHostCatalog ?? UnityRenderHostCatalog.CreateDefault();
            context.RegisterSetting(
                UnityRenderHostSettings.RenderHostSettingId,
                "Render Host",
                "Select how Cortex should present its workbench for the current host.",
                scope ?? string.Empty,
                UnityRenderHostSettings.ImguiRenderHostId,
                SettingValueKind.String,
                sortOrder,
                SettingEditorKind.Choice,
                string.Empty,
                resolvedRenderHostCatalog.SettingsHelpText,
                new[] { "render host", "renderer", "imgui", "avalonia", "desktop host", "external host" },
                resolvedRenderHostCatalog.BuildOptions(),
                false);
        }
    }
}
