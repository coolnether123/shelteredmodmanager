using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public static class CortexHostPathSettings
    {
        public const string WorkspaceRootSettingId = nameof(CortexSettings.WorkspaceRootPath);
        public const string RuntimeContentRootSettingId = nameof(CortexSettings.RuntimeContentRootPath);
        public const string ReferenceAssemblyRootSettingId = nameof(CortexSettings.ReferenceAssemblyRootPath);
        public const string AdditionalSourceRootsSettingId = nameof(CortexSettings.AdditionalSourceRoots);

        public const string WorkspaceRootDisplayName = "Workspace Scan Root";
        public const string RuntimeContentRootDisplayName = "Runtime Content Root";
        public const string ReferenceAssemblyRootDisplayName = "Reference Assembly Root";
        public const string AdditionalSourceRootsDisplayName = "Additional Source Roots";

        public static string GetEffectiveWorkspaceRoot(CortexSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(settings.WorkspaceRootPath)
                ? settings.WorkspaceRootPath
                : settings.RuntimeContentRootPath ?? string.Empty;
        }
    }
}
