using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>Shared Home and package/export metadata form content.</summary>
    internal static class ScenarioMetadataAuthoringContent
    {
        internal static ScenarioAuthoringInspectorItem[] BuildEditableItems(ScenarioDefinition definition, bool includeId)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Editable("Description", definition != null ? definition.Description : null, ScenarioAuthoringActionIds.ActionDraftDescriptionPrefix, "A short note for people choosing your scenario."));
            items.Add(Editable("Author", definition != null ? definition.Author : null, ScenarioAuthoringActionIds.ActionDraftAuthorPrefix, "The name players should see with this scenario."));
            items.Add(Editable("Version", definition != null ? definition.Version : null, ScenarioAuthoringActionIds.ActionDraftVersionPrefix, "Use a simple version such as 1.0.0."));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionDraftVersionBumpPatch, "+ Patch", "Increase the last version number for a small fix.", true, false, "V+")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionDraftVersionBumpMinor, "+ Minor", "Increase the middle version number for a feature update.", true, false, "V+")));
            items.Add(Editable("Credits", definition != null ? definition.Credits : null, ScenarioAuthoringActionIds.ActionDraftCreditsPrefix, "Optional thanks or contributor credits."));
            items.Add(Editable("Tags", JoinTags(definition != null ? definition.Tags : null), ScenarioAuthoringActionIds.ActionDraftTagsPrefix, "Optional comma-separated tags, such as story, survival, or challenge."));
            if (includeId)
                items.Add(BuildIdItem(definition));
            return items.ToArray();
        }

        internal static ScenarioAuthoringInspectorItem BuildIdItem(ScenarioDefinition definition)
        {
            return Editable("Scenario ID", definition != null ? definition.Id : null, ScenarioAuthoringActionIds.ActionDraftIdPrefix, "Stable technical ID. Change only before sharing your scenario.");
        }

        internal static ScenarioAuthoringInspectorItem[] BuildStatusItems(string scenarioFilePath)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Last Saved", FormatFileTime(scenarioFilePath)));
            ScenarioPublishExportResult last = GetLastExport();
            items.Add(Item.Property("Last Exported", last != null ? last.FormatTimestamp() : "<not exported>"));
            if (last != null && !last.Success)
                items.Add(Item.Text("The last export did not finish: " + (last.Message ?? "Unknown export problem.")));
            return items.ToArray();
        }

        private static ScenarioAuthoringInspectorItem Editable(string label, string value, string actionPrefix, string hint)
        {
            ScenarioAuthoringInspectorItem item = Item.Property(label, Item.Safe(value));
            item.Editable = true;
            item.HoverHint = hint;
            item.Action = Item.Action(actionPrefix, "Commit " + label, hint, true, false, "MD");
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

        private static ScenarioPublishExportResult GetLastExport()
        {
            try
            {
                ScenarioPublishExportService service = ScenarioCompositionRoot.Resolve<ScenarioPublishExportService>();
                return service != null ? service.LastResult : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
