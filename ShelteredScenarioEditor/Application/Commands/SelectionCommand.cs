using System.Globalization;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum SelectionCommandKind { Clear, CycleStack, ToggleStack, SelectStackIndex, SelectWeatherEffect, SelectBackdrop, SelectHierarchy }

    internal sealed class SelectionCommand : ScenarioAuthoringCommand
    {
        private SelectionCommand(SelectionCommandKind kind, int index, string targetId, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            Index = index;
            TargetId = targetId;
        }

        public SelectionCommandKind Kind { get; private set; }
        public int Index { get; private set; }
        public string TargetId { get; private set; }
        public static SelectionCommand Clear() { return Simple(SelectionCommandKind.Clear, ScenarioAuthoringActionIds.ActionSelectionClear); }
        public static SelectionCommand CycleStack() { return Simple(SelectionCommandKind.CycleStack, ScenarioAuthoringActionIds.ActionSelectionStackCycle); }
        public static SelectionCommand ToggleStack() { return Simple(SelectionCommandKind.ToggleStack, ScenarioAuthoringActionIds.ActionSelectionStackToggleExpanded); }
        public static SelectionCommand SelectStackIndex(int index) { return new SelectionCommand(SelectionCommandKind.SelectStackIndex, index, null, ScenarioAuthoringActionIds.ActionSelectionStackSelectPrefix + index.ToString(CultureInfo.InvariantCulture)); }
        public static SelectionCommand SelectWeatherEffect(string id) { return Target(SelectionCommandKind.SelectWeatherEffect, id, ScenarioAuthoringActionIds.ActionWeatherEffectSpriteSelectPrefix); }
        public static SelectionCommand SelectBackdrop(string id) { return Target(SelectionCommandKind.SelectBackdrop, id, ScenarioAuthoringActionIds.ActionBackdropSelectPrefix); }
        public static SelectionCommand SelectHierarchy(string id) { return Target(SelectionCommandKind.SelectHierarchy, id, ScenarioAuthoringActionIds.ActionHierarchySelectPrefix); }
        private static SelectionCommand Simple(SelectionCommandKind kind, string id) { return new SelectionCommand(kind, -1, null, id); }
        private static SelectionCommand Target(SelectionCommandKind kind, string id, string prefix) { return new SelectionCommand(kind, -1, id, prefix + (id ?? string.Empty)); }
    }
}
