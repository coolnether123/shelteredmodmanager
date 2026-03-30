using Cortex.Core.Models;

namespace Cortex.Plugin.Harmony
{
    internal static class HarmonyPluginIds
    {
        public const string ContainerId = "cortex.harmony";
        public const string PluginId = ContainerId;
        public const string ModuleId = "cortex.harmony.module";
        public const string ViewId = ContainerId + ".main";

        public const string OpenWindowCommandId = "cortex.window.harmony";
        public const string ViewPatchesCommandId = "cortex.harmony.viewPatches";
        public const string GeneratePrefixCommandId = "cortex.harmony.generatePrefix";
        public const string GeneratePostfixCommandId = "cortex.harmony.generatePostfix";
        public const string RefreshCommandId = "cortex.harmony.refresh";
        public const string CopySummaryCommandId = "cortex.harmony.copySummary";

        public const string InspectorSectionId = "cortex.harmony.inspector.section";
        public const string ExplorerFilterId = "cortex.explorer.filter.harmony.patched";
        public const string EditorAdornmentId = "cortex.harmony.editor.badge";
        public const string EditorWorkflowId = "cortex.harmony.editor.insertion";
        public const string TemplateWorkflowId = "cortex.harmony.editor.template";
    }
}
