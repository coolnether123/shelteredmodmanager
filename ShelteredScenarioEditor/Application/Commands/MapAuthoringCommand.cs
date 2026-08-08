using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum MapAuthoringCommandKind
    {
        OpenMap,
        CloseMap,
        CaptureSelection,
        SetMode,
        SetBrushShape,
        SetBrushSize,
        SelectWorldPosition,
        ClickWorldPosition,
        SelectLocation,
        BeginDuplicateLocation,
        EditLocationField,
        ToggleLocationField,
        CycleLocationIcon
    }

    internal enum MapAuthoringModeKind
    {
        Select,
        Place,
        Move,
        PaintTerrain
    }

    internal enum MapLocationFieldKind
    {
        DisplayName,
        Kind,
        IconId,
        Danger,
        LootTableId,
        EncounterTableId,
        Searchable,
        VisibleAtStart,
        DiscoveredAtStart,
        HiddenUntilDiscovered,
        ReplaceGeneratedLoot
    }

    /// <summary>
    /// Structured Map authoring intent. Automation ids identify controls and runs,
    /// but the handler consumes these typed values and never decodes the id.
    /// </summary>
    internal sealed class MapAuthoringCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private static readonly ScenarioAuthoringCommandPolicy WorldPolicy = ScenarioAuthoringCommandPolicy.World;
        private static readonly ScenarioAuthoringCommandPolicy WorldClickPolicy = ScenarioAuthoringCommandPolicy.WorldSafetySnapshot;

        private MapAuthoringCommand(
            string automationId,
            MapAuthoringCommandKind kind,
            ScenarioAuthoringCommandPolicy policy)
            : base(automationId, policy)
        {
            Kind = kind;
        }

        internal MapAuthoringCommandKind Kind { get; private set; }
        internal MapAuthoringModeKind Mode { get; private set; }
        internal MapTerrainBrushShape BrushShape { get; private set; }
        internal int BrushSize { get; private set; }
        internal float WorldX { get; private set; }
        internal float WorldY { get; private set; }
        internal string TerrainId { get; private set; }
        internal string LocationId { get; private set; }
        internal MapLocationFieldKind LocationField { get; private set; }
        internal string Value { get; private set; }

        internal static MapAuthoringCommand OpenMap()
        {
            return Simple(ScenarioAuthoringActionIds.ActionMapAuthoringOpen, MapAuthoringCommandKind.OpenMap);
        }

        internal static MapAuthoringCommand CloseMap()
        {
            return Simple(ScenarioAuthoringActionIds.ActionMapAuthoringClose, MapAuthoringCommandKind.CloseMap);
        }

        internal static MapAuthoringCommand CaptureSelection()
        {
            return Simple(ScenarioAuthoringActionIds.ActionMapAuthoringCaptureSelection, MapAuthoringCommandKind.CaptureSelection);
        }

        internal static MapAuthoringCommand SetMode(MapAuthoringModeKind mode, string terrainId)
        {
            string automationId = ScenarioAuthoringActionIds.ActionMapAuthoringModeSelect;
            if (mode == MapAuthoringModeKind.Place)
                automationId = ScenarioAuthoringActionIds.ActionMapAuthoringModePlace;
            else if (mode == MapAuthoringModeKind.Move)
                automationId = ScenarioAuthoringActionIds.ActionMapAuthoringModeMove;
            else if (mode == MapAuthoringModeKind.PaintTerrain)
                automationId = TerrainAutomationId(terrainId);

            MapAuthoringCommand command = new MapAuthoringCommand(automationId, MapAuthoringCommandKind.SetMode, WorldPolicy);
            command.Mode = mode;
            command.TerrainId = terrainId;
            return command;
        }

        internal static MapAuthoringCommand SetBrushShape(MapTerrainBrushShape shape)
        {
            string automationId = shape == MapTerrainBrushShape.Rectangle
                ? ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeSquare
                : ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeCircle;
            MapAuthoringCommand command = new MapAuthoringCommand(automationId, MapAuthoringCommandKind.SetBrushShape, WorldPolicy);
            command.BrushShape = shape;
            return command;
        }

        internal static MapAuthoringCommand SetBrushSize(int size)
        {
            string automationId = ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize3;
            if (size == 1) automationId = ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize1;
            else if (size == 5) automationId = ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize5;
            else if (size == 7) automationId = ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize7;
            MapAuthoringCommand command = new MapAuthoringCommand(automationId, MapAuthoringCommandKind.SetBrushSize, WorldPolicy);
            command.BrushSize = size;
            return command;
        }

        internal static MapAuthoringCommand SelectWorldPosition(float worldX, float worldY)
        {
            MapAuthoringCommand command = new MapAuthoringCommand(
                WorldPositionAutomationId(ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, worldX, worldY),
                MapAuthoringCommandKind.SelectWorldPosition,
                WorldPolicy);
            command.WorldX = worldX;
            command.WorldY = worldY;
            return command;
        }

        internal static MapAuthoringCommand ClickWorldPosition(float worldX, float worldY)
        {
            MapAuthoringCommand command = new MapAuthoringCommand(
                WorldPositionAutomationId(ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix, worldX, worldY),
                MapAuthoringCommandKind.ClickWorldPosition,
                WorldClickPolicy);
            command.WorldX = worldX;
            command.WorldY = worldY;
            return command;
        }

        internal static MapAuthoringCommand SelectLocation(string locationId)
        {
            return ForLocation(
                ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix,
                MapAuthoringCommandKind.SelectLocation,
                locationId,
                WorldPolicy);
        }

        internal static MapAuthoringCommand BeginDuplicateLocation(string locationId)
        {
            return ForLocation(
                ScenarioAuthoringActionIds.ActionMapLocationDuplicatePrefix,
                MapAuthoringCommandKind.BeginDuplicateLocation,
                locationId,
                WorldPolicy);
        }

        internal static MapAuthoringCommand EditLocationField(MapLocationFieldKind field, string locationId, string value)
        {
            string automationId = ScenarioAuthoringActionIds.ActionMapLocationEditPrefix
                + FieldName(field) + "." + ScenarioAutomationIdCodec.EncodeToken(locationId) + ".";
            if (value != null)
                automationId += ScenarioAutomationIdCodec.EncodeToken(value);
            MapAuthoringCommand command = new MapAuthoringCommand(automationId, MapAuthoringCommandKind.EditLocationField, WorldPolicy);
            command.LocationField = field;
            command.LocationId = locationId;
            command.Value = value;
            return command;
        }

        internal static MapAuthoringCommand ToggleLocationField(MapLocationFieldKind field, string locationId)
        {
            MapAuthoringCommand command = new MapAuthoringCommand(
                ScenarioAuthoringActionIds.ActionMapLocationTogglePrefix + FieldName(field) + "." + ScenarioAutomationIdCodec.EncodeToken(locationId),
                MapAuthoringCommandKind.ToggleLocationField,
                WorldPolicy);
            command.LocationField = field;
            command.LocationId = locationId;
            return command;
        }

        internal static MapAuthoringCommand CycleLocationIcon(string locationId)
        {
            return ForLocation(
                ScenarioAuthoringActionIds.ActionMapLocationCycleIconPrefix,
                MapAuthoringCommandKind.CycleLocationIcon,
                locationId,
                WorldPolicy);
        }

        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            return Kind == MapAuthoringCommandKind.EditLocationField
                ? EditLocationField(LocationField, LocationId, value)
                : this;
        }

        internal static string FieldName(MapLocationFieldKind field)
        {
            switch (field)
            {
                case MapLocationFieldKind.Kind: return "kind";
                case MapLocationFieldKind.IconId: return "iconId";
                case MapLocationFieldKind.Danger: return "danger";
                case MapLocationFieldKind.LootTableId: return "lootTableId";
                case MapLocationFieldKind.EncounterTableId: return "encounterTableId";
                case MapLocationFieldKind.Searchable: return "searchable";
                case MapLocationFieldKind.VisibleAtStart: return "visibleAtStart";
                case MapLocationFieldKind.DiscoveredAtStart: return "discoveredAtStart";
                case MapLocationFieldKind.HiddenUntilDiscovered: return "hiddenUntilDiscovered";
                case MapLocationFieldKind.ReplaceGeneratedLoot: return "replaceGeneratedLoot";
                default: return "displayName";
            }
        }

        private static MapAuthoringCommand Simple(string automationId, MapAuthoringCommandKind kind)
        {
            return new MapAuthoringCommand(automationId, kind, WorldPolicy);
        }

        private static MapAuthoringCommand ForLocation(
            string prefix,
            MapAuthoringCommandKind kind,
            string locationId,
            ScenarioAuthoringCommandPolicy policy)
        {
            MapAuthoringCommand command = new MapAuthoringCommand(
                ScenarioAutomationIdCodec.BuildTokenActionId(prefix, locationId),
                kind,
                policy);
            command.LocationId = locationId;
            return command;
        }

        private static string WorldPositionAutomationId(string prefix, float worldX, float worldY)
        {
            string token = worldX.ToString(CultureInfo.InvariantCulture) + "," + worldY.ToString(CultureInfo.InvariantCulture);
            return ScenarioAutomationIdCodec.BuildTokenActionId(prefix, token);
        }

        private static string TerrainAutomationId(string terrainId)
        {
            if (string.Equals(terrainId, "Woodland", System.StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainTrees;
            if (string.Equals(terrainId, "Mountains", System.StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainMountains;
            if (string.Equals(terrainId, "NowhereSpecial", System.StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainClear;
            return ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainGeneratedBlend;
        }
    }
}
