using System;

namespace Cortex.Core.Models
{
    [Serializable]
    public sealed class CortexSettings
    {
        public string WorkspaceRootPath;
        public string ModsRootPath;
        public string ManagedAssemblyRootPath;
        public string AdditionalSourceRoots;
        public string LogFilePath;
        public string ProjectCatalogPath;
        public string DecompilerPathOverride;
        public string DecompilerCachePath;
        public string ThemeId;
        public string DefaultBuildConfiguration;
        public int BuildTimeoutMs;
        public int MaxRecentLogs;
        public bool AutoScrollLogs;
        public bool ShowLogBacklog;
        public bool EnableFileEditing;
        public bool EnableFileSaving;
        public float LogsPaneWidth;
        public float ProjectsPaneWidth;
        public float EditorFilePaneWidth;
        public float WindowX;
        public float WindowY;
        public float WindowWidth;
        public float WindowHeight;

        public CortexSettings()
        {
            WorkspaceRootPath = string.Empty;
            ModsRootPath = string.Empty;
            ManagedAssemblyRootPath = string.Empty;
            AdditionalSourceRoots = string.Empty;
            LogFilePath = string.Empty;
            ProjectCatalogPath = string.Empty;
            DecompilerPathOverride = string.Empty;
            DecompilerCachePath = string.Empty;
            ThemeId = "cortex.vs-dark";
            DefaultBuildConfiguration = "Debug";
            BuildTimeoutMs = 300000;
            MaxRecentLogs = 300;
            AutoScrollLogs = true;
            ShowLogBacklog = false;
            EnableFileEditing = false;
            EnableFileSaving = false;
            LogsPaneWidth = 520f;
            ProjectsPaneWidth = 360f;
            EditorFilePaneWidth = 420f;
            WindowX = 70f;
            WindowY = 70f;
            WindowWidth = 1180f;
            WindowHeight = 760f;
        }
    }
}
