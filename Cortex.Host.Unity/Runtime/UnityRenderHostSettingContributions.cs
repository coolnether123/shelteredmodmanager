using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public static class UnityRenderHostSettingContributions
    {
        public static SettingContribution CreateContribution(
            UnityRenderHostCatalog renderHostCatalog,
            string scope,
            int sortOrder)
        {
            var resolvedRenderHostCatalog = renderHostCatalog ?? UnityRenderHostCatalog.CreateDefault();
            return new SettingContribution
            {
                SettingId = UnityRenderHostSettings.RenderHostSettingId,
                DisplayName = "Presentation Mode",
                Description = "Select how Cortex should present its workbench for the current host. Saving applies the new mode live without restarting the game.",
                Scope = scope ?? string.Empty,
                DefaultValue = UnityRenderHostSettings.ImguiRenderHostId,
                ValueKind = SettingValueKind.String,
                SortOrder = sortOrder,
                EditorKind = SettingEditorKind.Choice,
                PlaceholderText = string.Empty,
                HelpText = resolvedRenderHostCatalog.SettingsHelpText,
                Keywords = new[] { "presentation mode", "render host", "renderer", "imgui", "overlay", "avalonia", "desktop host", "external host" },
                Options = resolvedRenderHostCatalog.BuildOptions(),
                AllowEmpty = false,
                RequiresRestart = false
            };
        }

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

            context.RegisterSetting(CreateContribution(renderHostCatalog, scope, sortOrder));
        }
    }
}
