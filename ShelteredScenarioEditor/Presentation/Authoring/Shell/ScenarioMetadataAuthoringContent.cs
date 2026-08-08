using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    /// <summary>Shared Home and package/export metadata form content.</summary>
    internal static class ScenarioMetadataAuthoringContent
    {
        internal static ScenarioAuthoringInspectorItem[] BuildEditableItems(ScenarioDefinition definition, bool includeId)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Editable("Description", definition != null ? definition.Description : null, ScenarioMetadataField.Description, "A short note for people choosing your scenario."));
            items.Add(Editable("Author", definition != null ? definition.Author : null, ScenarioMetadataField.Author, "The name players should see with this scenario."));
            items.Add(Editable("Version", definition != null ? definition.Version : null, ScenarioMetadataField.Version, "Use a simple version such as 1.0.0."));
            items.Add(Item.ActionItem(Item.Action(EditorLifecycleCommand.BumpPatchVersion, "+ Patch", "Increase the last version number for a small fix.", true, false, "V+")));
            items.Add(Item.ActionItem(Item.Action(EditorLifecycleCommand.BumpMinorVersion, "+ Minor", "Increase the middle version number for a feature update.", true, false, "V+")));
            items.Add(Editable("Credits", definition != null ? definition.Credits : null, ScenarioMetadataField.Credits, "Optional thanks or contributor credits."));
            items.Add(Editable("Tags", JoinTags(definition != null ? definition.Tags : null), ScenarioMetadataField.Tags, "Optional comma-separated tags, such as story, survival, or challenge."));
            if (includeId)
                items.Add(BuildIdItem(definition));
            return items.ToArray();
        }

        internal static ScenarioAuthoringInspectorItem BuildIdItem(ScenarioDefinition definition)
        {
            return Editable("Scenario ID", definition != null ? definition.Id : null, ScenarioMetadataField.Id, "Stable technical ID. Change only before sharing your scenario.");
        }

        internal static ScenarioAuthoringInspectorItem[] BuildStatusItems(
            string scenarioFilePath,
            ScenarioPublishExportService publishService)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Last Saved", FormatFileTime(scenarioFilePath)));
            ScenarioPublishExportResult last = publishService != null ? publishService.LastResult : null;
            items.Add(Item.Property("Last Exported", last != null ? last.FormatTimestamp() : "<not exported>"));
            if (last != null && !last.Success)
                items.Add(Item.Text("The last export did not finish: " + (last.Message ?? "Unknown export problem.")));
            return items.ToArray();
        }

        private static ScenarioAuthoringInspectorItem Editable(string label, string value, ScenarioMetadataField field, string hint)
        {
            ScenarioAuthoringInspectorItem item = Item.Property(label, Item.Safe(value));
            item.Editable = true;
            item.HoverHint = hint;
            item.Action = Item.Action(EditorLifecycleCommand.Metadata(field, string.Empty), "Commit " + label, hint, true, false, "MD");
            return item;
        }

        private static string JoinTags(List<string> tags)
        {
            return tags == null || tags.Count == 0 ? string.Empty : string.Join(", ", tags.ToArray());
        }

        private static string FormatFileTime(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath) || !File.Exists(scenarioFilePath))
                return "<not saved>";
            return File.GetLastWriteTimeUtc(scenarioFilePath).ToString("u", CultureInfo.InvariantCulture);
        }

    }
}
