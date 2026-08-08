using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal static class Item
    {
        public static ScenarioAuthoringInspectorAction Action(ScenarioAuthoringCommand command, string label, string hint, bool enabled, bool emphasized, string iconText = null, string detail = null, string badge = null)
        {
            return ScenarioInspectorItemFactory.Action(command, label, hint, enabled, emphasized, iconText, detail, badge);
        }

        public static ScenarioAuthoringInspectorAction Action(string id, string label, string hint, bool enabled, bool emphasized, string iconText = null, string detail = null, string badge = null)
        {
            return ScenarioInspectorItemFactory.Action(id, label, hint, enabled, emphasized, iconText, detail, badge);
        }

        public static ScenarioAuthoringInspectorAction Action(ShellUxCommand command, string label, string hint, bool enabled, bool emphasized, string iconText = null, string detail = null, string badge = null)
        {
            ScenarioAuthoringInspectorAction action = ScenarioInspectorItemFactory.Action(command != null ? command.AutomationId : string.Empty, label, hint, enabled, emphasized, iconText, detail, badge);
            action.Command = command;
            return action;
        }

        public static ScenarioAuthoringInspectorItem Text(string value) { return ScenarioInspectorItemFactory.Text(value); }
        public static ScenarioAuthoringInspectorItem Property(string label, string value) { return ScenarioInspectorItemFactory.Property(label, value); }
        public static ScenarioAuthoringInspectorItem Property(string label, string value, string detail) { return ScenarioInspectorItemFactory.Property(label, value, detail); }
        public static ScenarioAuthoringInspectorItem ActionItem(ScenarioAuthoringInspectorAction action) { return ScenarioInspectorItemFactory.ActionItem(action); }
        public static string Safe(string value) { return ScenarioInspectorItemFactory.Safe(value); }
        public static string FormatTarget(ScenarioAuthoringTarget target) { return ScenarioAuthoringPresentationUtilities.FormatTarget(target); }
        public static int CountDirtyFlags(ScenarioEditorSession editorSession) { return ScenarioAuthoringPresentationUtilities.CountDirtyFlags(editorSession); }
        public static int CountSpriteSwaps(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.CountSpriteSwaps(definition); }
        public static int CountSceneSpritePlacements(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.CountSceneSpritePlacements(definition); }
        public static int CountFamilyMembers(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.CountFamilyMembers(definition); }
        public static int CountInventoryStacks(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.CountInventoryStacks(definition); }
        public static int CountInventoryTotal(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.CountInventoryTotal(definition); }
        public static int CountObjectPlacements(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.CountObjectPlacements(definition); }
        public static string SummarizeFamily(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.SummarizeFamily(definition); }
        public static string SummarizeInventory(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.SummarizeInventory(definition); }
        public static string SummarizeObjectPlacements(ScenarioDefinition definition) { return ScenarioAuthoringPresentationUtilities.SummarizeObjectPlacements(definition); }
    }
}
