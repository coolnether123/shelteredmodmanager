using System;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Commands
{
    internal sealed class ScenarioLaunchSetupCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioDraftSnapshotService _snapshotService;

        public ScenarioLaunchSetupCommandHandler(IScenarioEditorService editorService, ScenarioDraftSnapshotService snapshotService)
        {
            _editorService = editorService;
            _snapshotService = snapshotService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = !string.IsNullOrEmpty(actionId) && actionId.StartsWith("launch_setup.", StringComparison.Ordinal);
            message = null;
            if (!handled) return false;
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null) { message = "No active scenario definition."; return false; }
            if (definition.LaunchSetup == null) definition.LaunchSetup = ScenarioLaunchSetupDefinition.CreateDefault();

            ScenarioDefinition before = ScenarioDefinitionCloner.Clone(definition);
            bool changed = Apply(definition.LaunchSetup, actionId, out message);
            if (!changed) return false;
            ScenarioAuthoringHistoryService.Instance.RecordAuthoringChange(before, "Change play experience", ScenarioDirtySection.LaunchSetup, ScenarioEditCategory.LaunchSetup);
            session.MarkDraftChanged(ScenarioDirtySection.LaunchSetup, ScenarioEditCategory.LaunchSetup);
            string ignored;
            if (_snapshotService != null) _snapshotService.TryAutosaveCurrent("play experience change", out ignored);
            return true;
        }

        private static bool Apply(ScenarioLaunchSetupDefinition setup, string actionId, out string message)
        {
            message = null;
            const string modePrefix = "launch_setup.mode.";
            if (actionId.StartsWith(modePrefix, StringComparison.Ordinal))
            {
                ScenarioLaunchSetupMode mode;
                try { mode = (ScenarioLaunchSetupMode)Enum.Parse(typeof(ScenarioLaunchSetupMode), actionId.Substring(modePrefix.Length), true); }
                catch { message = "Unknown play experience mode."; return false; }
                if (setup.Mode == mode) { message = "Play experience is already " + mode + "."; return false; }
                setup.Mode = mode;
                message = "Play experience set to " + mode + ".";
                return true;
            }

            const string selectablePrefix = "launch_setup.selectable.";
            if (actionId.StartsWith(selectablePrefix, StringComparison.Ordinal))
            {
                ScenarioDifficultyCategoryDefinition category = GetOrCreate(setup, actionId.Substring(selectablePrefix.Length));
                if (category == null) { message = "Unknown difficulty category."; return false; }
                category.PlayerSelectable = !category.PlayerSelectable;
                message = category.PlayerSelectable ? "Player can change " + category.Id + "." : "Scenario locks " + category.Id + ".";
                return true;
            }

            const string valuePrefix = "launch_setup.value.";
            if (actionId.StartsWith(valuePrefix, StringComparison.Ordinal))
            {
                string payload = actionId.Substring(valuePrefix.Length);
                int separator = payload.LastIndexOf('.');
                int delta;
                if (separator <= 0 || !int.TryParse(payload.Substring(separator + 1), out delta)) { message = "Difficulty value action is invalid."; return false; }
                ScenarioDifficultyCategoryDefinition category = GetOrCreate(setup, payload.Substring(0, separator));
                if (category == null) { message = "Unknown difficulty category."; return false; }
                int maximum = category.Id == ScenarioDifficultyCategoryIds.MapSize ? 2 : category.Id == ScenarioDifficultyCategoryIds.Fog ? 1 : 3;
                int next = Math.Max(0, Math.Min(maximum, category.AuthoredValue + delta));
                if (next == category.AuthoredValue) { message = "Authored " + category.Id + " value is already at its limit."; return false; }
                category.AuthoredValue = next;
                message = "Updated authored " + category.Id + " value.";
                return true;
            }
            message = "Play experience action was not recognized.";
            return false;
        }

        private static ScenarioDifficultyCategoryDefinition GetOrCreate(ScenarioLaunchSetupDefinition setup, string id)
        {
            if (!ScenarioDifficultyCategoryIds.IsKnown(id)) return null;
            for (int i = 0; setup.Categories != null && i < setup.Categories.Count; i++)
                if (setup.Categories[i] != null && string.Equals(setup.Categories[i].Id, id, StringComparison.OrdinalIgnoreCase)) return setup.Categories[i];
            ScenarioDifficultyCategoryDefinition category = new ScenarioDifficultyCategoryDefinition { Id = id, AuthoredValue = id == ScenarioDifficultyCategoryIds.MapSize || id == ScenarioDifficultyCategoryIds.Fog ? 0 : 1, PlayerSelectable = true };
            setup.Categories.Add(category);
            return category;
        }
    }
}
