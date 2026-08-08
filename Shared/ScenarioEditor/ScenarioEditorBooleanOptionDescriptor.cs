namespace ShelteredModManager.Shared.ScenarioEditor
{
    /// <summary>Canonical metadata for the optional scenario-editor runtime switch.</summary>
    internal static class ScenarioEditorBooleanOptionDescriptor
    {
        public const string Id = "ShelteredScenarioEditor.Enabled";
        public const string Owner = "ShelteredScenarioEditor";
        public const string Label = "Custom Scenario Editor";
        public const string Description =
            "Enables the optional custom scenario editor and its Add New Scenario entry. Installed custom scenarios remain available while the editor is disabled.";
        public const bool DefaultValue = false;
        public const bool RequiresRestart = true;
        public const int SortOrder = 100;
    }
}
