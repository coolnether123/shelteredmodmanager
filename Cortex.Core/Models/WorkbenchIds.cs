namespace Cortex.Core.Models
{
    public static class CortexWorkbenchIds
    {
        public const string LogsContainer = "cortex.logs";
        public const string ProjectsContainer = "cortex.projects";
        public const string EditorContainer = "cortex.editor";
        public const string BuildContainer = "cortex.build";
        public const string ReferenceContainer = "cortex.reference";
        public const string RuntimeContainer = "cortex.runtime";
        public const string SettingsContainer = "cortex.settings";

        /// <summary>
        /// Standalone file-hierarchy explorer (left-side panel, always visible).
        /// Replaces the navigator pane that was previously embedded inside the editor module.
        /// </summary>
        public const string FileExplorerContainer = "cortex.fileexplorer";
    }
}
