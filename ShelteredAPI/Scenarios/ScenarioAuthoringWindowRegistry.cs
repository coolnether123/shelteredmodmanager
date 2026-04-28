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
            Register(Create(ScenarioAuthoringWindowIds.Scenario, "Scenario", ScenarioAuthoringShellDock.Left, ScenarioAuthoringWindowContentKind.Scenario, 0, 304f, 232f, 260f, 120f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, true, false));
            Register(Create(ScenarioAuthoringWindowIds.Hierarchy, "Hierarchy", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Hierarchy, 0, 336f, 452f, 280f, 180f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.SelectionStack, "Selection Stack", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.SelectionStack, 1, 336f, 300f, 260f, 140f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Layers, "Layers", ScenarioAuthoringShellDock.Left, ScenarioAuthoringWindowContentKind.Layers, 1, 304f, 188f, 260f, 120f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, true, false));
            Register(Create(ScenarioAuthoringWindowIds.TilesPalette, "Tiles Palette", ScenarioAuthoringShellDock.Left, ScenarioAuthoringWindowContentKind.TilesPalette, 2, 304f, 322f, 260f, 160f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, false, false));
            Register(Create(ScenarioAuthoringWindowIds.Inspector, "Inspector", ScenarioAuthoringShellDock.Right, ScenarioAuthoringWindowContentKind.Inspector, 0, 292f, 520f, 260f, 220f, ScenarioAuthoringShellRendererKind.Inspector, ScenarioStageKind.None, true, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.BuildTools, "Asset Browser", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.BuildTools, 0, 940f, 520f, 540f, 260f, ScenarioAuthoringShellRendererKind.BottomTray, ScenarioStageKind.None, true, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Triggers, "Triggers / Events", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Triggers, 1, 880f, 520f, 560f, 360f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Events, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Survivors, "Survivors", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Survivors, 2, 880f, 520f, 560f, 360f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.People, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Stockpile, "Stockpile", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Stockpile, 3, 880f, 520f, 560f, 360f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.InventoryStorage, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Quests, "Quests", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Quests, 4, 880f, 520f, 560f, 360f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Quests, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Map, "Map", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Map, 5, 880f, 520f, 560f, 360f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Map, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Publish, "Publish", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Publish, 6, 880f, 520f, 560f, 360f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Publish, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Calendar, "Calendar", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Calendar, 7, 720f, 520f, 520f, 260f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Settings, "Editor Settings", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Empty, 0, 720f, 520f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, false, false));
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
