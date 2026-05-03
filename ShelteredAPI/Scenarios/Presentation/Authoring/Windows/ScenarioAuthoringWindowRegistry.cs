using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioAuthoringWindowIds
    {
        public const string Scenario = "scenario";
        public const string Layers = "layers";
        public const string Hierarchy = "hierarchy";
        public const string SelectionStack = "selection_stack";
        public const string TilesPalette = "tiles_palette";
        public const string Inspector = "inspector";
        public const string BuildTools = "build_tools";
        public const string Triggers = "triggers_events";
        public const string Survivors = "survivors";
        public const string Stockpile = "stockpile";
        public const string Quests = "quests";
        public const string Map = "map";
        public const string Publish = "publish";
        public const string Calendar = "calendar";
        public const string Settings = "editor_settings";
    }

    internal sealed class ScenarioAuthoringWindowRegistry
    {
        private readonly List<ScenarioAuthoringWindowDefinition> _definitions = new List<ScenarioAuthoringWindowDefinition>();

        public ScenarioAuthoringWindowRegistry()
        {
            Register(Create(ScenarioAuthoringWindowIds.Scenario, "Overview", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Scenario, 0, 760f, 420f, 420f, 240f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, true, false));
            Register(Create(ScenarioAuthoringWindowIds.Hierarchy, "Hierarchy", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Hierarchy, 1, 640f, 460f, 360f, 220f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.SelectionStack, "Selection Stack", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.SelectionStack, 2, 620f, 360f, 360f, 180f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Layers, "Layers", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Layers, 3, 560f, 360f, 360f, 180f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, true, false));
            Register(Create(ScenarioAuthoringWindowIds.TilesPalette, "Room Palette", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.TilesPalette, 4, 760f, 520f, 420f, 260f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, false, false));
            Register(Create(ScenarioAuthoringWindowIds.Inspector, "Selection", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Inspector, 5, 380f, 620f, 320f, 260f, ScenarioAuthoringShellRendererKind.Inspector, ScenarioStageKind.None, true, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.BuildTools, "Art Tray", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.BuildTools, 6, 980f, 520f, 640f, 320f, ScenarioAuthoringShellRendererKind.BottomTray, ScenarioStageKind.None, true, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Triggers, "Timeline", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Triggers, 7, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Events, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Survivors, "Cast", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Survivors, 8, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.People, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Stockpile, "Supplies", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Stockpile, 9, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.InventoryStorage, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Quests, "Story", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Quests, 10, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Quests, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Map, "Map", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Map, 11, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Map, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Publish, "Publish", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Publish, 12, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Publish, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Calendar, "Schedule", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Calendar, 13, 760f, 420f, 520f, 260f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Settings, "Workshop Settings", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Empty, 14, 760f, 540f, 520f, 360f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, false, false));
        }

        public ScenarioAuthoringWindowDefinition[] GetDefinitions()
        {
            return _definitions.ToArray();
        }

        public ScenarioAuthoringWindowDefinition Find(string id)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioAuthoringWindowDefinition definition = _definitions[i];
                if (definition != null && string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase))
                    return definition;
            }

            return null;
        }

        private static ScenarioAuthoringWindowDefinition Create(
            string id,
            string title,
            ScenarioAuthoringShellDock dock,
            ScenarioAuthoringWindowContentKind contentKind,
            int order,
            float width,
            float height,
            float minWidth,
            float minHeight,
            ScenarioAuthoringShellRendererKind rendererKind,
            ScenarioStageKind workspaceStage,
            bool visible,
            bool collapsed,
            bool pinned,
            bool menuVisible,
            bool workspaceTabVisible)
        {
            return new ScenarioAuthoringWindowDefinition
            {
                Id = id,
                Title = title,
                Dock = dock,
                WorkspaceStage = workspaceStage,
                RendererKind = rendererKind,
                ContentKind = contentKind,
                DefaultVisible = visible,
                DefaultCollapsed = collapsed,
                DefaultPinned = pinned,
                MenuVisible = menuVisible,
                WorkspaceTabVisible = workspaceTabVisible,
                Order = order,
                DefaultWidth = width,
                DefaultHeight = height,
                MinWidth = minWidth,
                MinHeight = minHeight
            };
        }

        private void Register(ScenarioAuthoringWindowDefinition definition)
        {
            if (definition != null)
                _definitions.Add(definition);
        }
    }
}
