using System;
using System.Collections.Generic;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    /// <summary>Focused command handling for editable package metadata.</summary>
    internal static class ScenarioMetadataAuthoringActions
    {
        internal static bool TryHandle(string actionId, ScenarioEditorSession session, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (string.IsNullOrEmpty(actionId))
                return false;

            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
                return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionDraftVersionBumpPatch, StringComparison.Ordinal))
            {
                definition.Version = ScenarioMetadataDefaults.BumpVersion(definition.Version, false);
                return Changed(session, "Version bumped to " + definition.Version + ".", out handled, out message);
            }
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionDraftVersionBumpMinor, StringComparison.Ordinal))
            {
                definition.Version = ScenarioMetadataDefaults.BumpVersion(definition.Version, true);
                return Changed(session, "Version bumped to " + definition.Version + ".", out handled, out message);
            }

            string value;
            if (TryDecode(actionId, ScenarioAuthoringActionIds.ActionDraftDescriptionPrefix, out value))
            {
                definition.Description = value.Trim();
                return Changed(session, "Scenario description updated.", out handled, out message);
            }
            if (TryDecode(actionId, ScenarioAuthoringActionIds.ActionDraftGoalPrefix, out value))
            {
                definition.Goal = value.Trim();
                return Changed(session, "Scenario goal updated.", out handled, out message);
            }
            if (TryDecode(actionId, ScenarioAuthoringActionIds.ActionDraftAuthorPrefix, out value))
            {
                definition.Author = value.Trim();
                return Changed(session, "Scenario author updated.", out handled, out message);
            }
            if (TryDecode(actionId, ScenarioAuthoringActionIds.ActionDraftVersionPrefix, out value))
            {
                definition.Version = value.Trim();
                return Changed(session, "Scenario version updated.", out handled, out message);
            }
            if (TryDecode(actionId, ScenarioAuthoringActionIds.ActionDraftCreditsPrefix, out value))
            {
                definition.Credits = value.Trim();
                return Changed(session, "Scenario credits updated.", out handled, out message);
            }
            if (TryDecode(actionId, ScenarioAuthoringActionIds.ActionDraftTagsPrefix, out value))
            {
                ReplaceTags(definition.Tags, value);
                return Changed(session, "Scenario tags updated.", out handled, out message);
            }
            if (TryDecode(actionId, ScenarioAuthoringActionIds.ActionDraftIdPrefix, out value))
            {
                string id = value.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    handled = true;
                    message = "Scenario ID cannot be empty.";
                    return true;
                }

                definition.Id = id;
                return Changed(session, "Scenario ID updated. Keep this stable after sharing.", out handled, out message);
            }

            return false;
        }

        private static bool Changed(ScenarioEditorSession session, string changedMessage, out bool handled, out string message)
        {
            session.MarkDraftChanged(ScenarioDirtySection.Meta);
            handled = true;
            message = changedMessage;
            return true;
        }

        private static bool TryDecode(string actionId, string prefix, out string value)
        {
            value = null;
            if (!actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            value = ScenarioAuthoringActionCodec.DecodeToken(actionId.Substring(prefix.Length)) ?? string.Empty;
            return true;
        }

        private static void ReplaceTags(List<string> tags, string raw)
        {
            if (tags == null)
                return;

            tags.Clear();
            string[] entries = (raw ?? string.Empty).Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string tag = entries[i].Trim();
                if (string.IsNullOrEmpty(tag) || tags.Contains(tag))
                    continue;
                tags.Add(tag);
            }
        }
    }
}
