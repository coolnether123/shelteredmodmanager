using System;
using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioAuthoringWindowIds
    {
        public const string Scenario = "scenario";
        public const string Layers = "layers";
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
            Register(ScenarioAuthoringWindowIds.Scenario, "Scenario", ScenarioAuthoringShellDock.Left, false, false, false, 0, 304f, 232f, 260f, 120f);
            Register(ScenarioAuthoringWindowIds.Layers, "Layers", ScenarioAuthoringShellDock.Left, false, false, false, 1, 304f, 188f, 260f, 120f);
            Register(ScenarioAuthoringWindowIds.TilesPalette, "Tiles Palette", ScenarioAuthoringShellDock.Left, false, false, false, 2, 304f, 322f, 260f, 160f);
            Register(ScenarioAuthoringWindowIds.Inspector, "Inspector", ScenarioAuthoringShellDock.Right, true, false, true, 0, 292f, 520f, 260f, 220f);
            Register(ScenarioAuthoringWindowIds.BuildTools, "Asset Picker", ScenarioAuthoringShellDock.Bottom, true, false, true, 0, 940f, 272f, 540f, 180f);
            Register(ScenarioAuthoringWindowIds.Triggers, "Triggers / Events", ScenarioAuthoringShellDock.Floating, false, false, false, 1, 880f, 520f, 560f, 360f);
            Register(ScenarioAuthoringWindowIds.Survivors, "Survivors", ScenarioAuthoringShellDock.Floating, false, false, false, 2, 880f, 520f, 560f, 360f);
            Register(ScenarioAuthoringWindowIds.Stockpile, "Stockpile", ScenarioAuthoringShellDock.Floating, false, false, false, 3, 880f, 520f, 560f, 360f);
            Register(ScenarioAuthoringWindowIds.Quests, "Quests", ScenarioAuthoringShellDock.Floating, false, false, false, 4, 880f, 520f, 560f, 360f);
            Register(ScenarioAuthoringWindowIds.Map, "Map", ScenarioAuthoringShellDock.Floating, false, false, false, 5, 880f, 520f, 560f, 360f);
            Register(ScenarioAuthoringWindowIds.Publish, "Publish", ScenarioAuthoringShellDock.Floating, false, false, false, 6, 880f, 520f, 560f, 360f);
            Register(ScenarioAuthoringWindowIds.Calendar, "Calendar", ScenarioAuthoringShellDock.Bottom, false, false, false, 7, 940f, 272f, 540f, 180f);
            Register(ScenarioAuthoringWindowIds.Settings, "Editor Settings", ScenarioAuthoringShellDock.Floating, false, false, true, 0, 720f, 520f, 620f, 420f);
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

        private void Register(
            string id,
            string title,
            ScenarioAuthoringShellDock dock,
            bool visible,
            bool collapsed,
            bool pinned,
            int order,
            float width,
            float height,
            float minWidth,
            float minHeight)
        {
            _definitions.Add(new ScenarioAuthoringWindowDefinition
            {
                Id = id,
                Title = title,
                Dock = dock,
                DefaultVisible = visible,
                DefaultCollapsed = collapsed,
                DefaultPinned = pinned,
                Order = order,
                DefaultWidth = width,
                DefaultHeight = height,
                MinWidth = minWidth,
                MinHeight = minHeight
            });
        }
    }
}
