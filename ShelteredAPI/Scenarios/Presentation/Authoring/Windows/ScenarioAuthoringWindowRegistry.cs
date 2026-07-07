using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Windows{
    internal static class ScenarioAuthoringWindowIds
    {
        public const string Scenario = "scenario";
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
        public const string AssetBrowser = "asset_browser";
        public const string Publish = "publish";
        public const string Settings = "editor_settings";
        public const string PixelEditor = "pixel_editor";
    }

    internal sealed class ScenarioAuthoringWindowRegistry
    {
        private readonly List<ScenarioAuthoringWindowDefinition> _definitions = new List<ScenarioAuthoringWindowDefinition>();

        public ScenarioAuthoringWindowRegistry()
        {
            Register(Create(ScenarioAuthoringWindowIds.Scenario, "Home", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.Scenario, 0, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Bunker, true, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.Hierarchy, "Hierarchy", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.Hierarchy, 1, 640f, 460f, 360f, 220f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.SelectionStack, "Selection Stack", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.SelectionStack, 2, 620f, 360f, 360f, 180f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.TilesPalette, "Build Palette", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.TilesPalette, 4, 760f, 520f, 420f, 260f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, true, false));
            Register(Create(ScenarioAuthoringWindowIds.Inspector, "Selection", ScenarioAuthoringShellDock.Right, ScenarioAuthoringWindowContentKind.Inspector, 5, 380f, 620f, 320f, 260f, ScenarioAuthoringShellRendererKind.Inspector, ScenarioStageKind.None, true, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.BuildTools, "Tool Workspace", ScenarioAuthoringShellDock.Bottom, ScenarioAuthoringWindowContentKind.BuildTools, 6, 980f, 520f, 640f, 320f, ScenarioAuthoringShellRendererKind.BottomTray, ScenarioStageKind.None, true, false, true, true, true));
            Register(Create(ScenarioAuthoringWindowIds.Triggers, "Timeline", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.Triggers, 7, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Events, false, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.Survivors, "Cast", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.Survivors, 8, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.People, false, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.Stockpile, "Supplies", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.Stockpile, 9, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.InventoryStorage, false, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.Quests, "Story", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.Quests, 10, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Quests, false, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.Map, "Map", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.Map, 11, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Map, false, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.AssetBrowser, "Assets", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.AssetBrowser, 12, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Assets, false, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.Publish, "Publish", ScenarioAuthoringShellDock.Overlay, ScenarioAuthoringWindowContentKind.Publish, 13, 920f, 600f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.Publish, false, false, false, false, true));
            Register(Create(ScenarioAuthoringWindowIds.PixelEditor, "Pixel Editor", ScenarioAuthoringShellDock.Floating, ScenarioAuthoringWindowContentKind.PixelEditor, 14, 860f, 620f, 620f, 420f, ScenarioAuthoringShellRendererKind.Standard, ScenarioStageKind.None, false, false, false, false, false));
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
