using System.Collections.Generic;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Domain.Map{
    /// <summary>
    /// Top-level authored world-map data for a scenario.
    /// Locations, routes, loot, and encounter tables are neutral data until the game runtime projects them onto Sheltered's map.
    /// </summary>
    public class MapAuthoringDefinition
    {
        public MapAuthoringDefinition()
        {
            Locations = new List<MapLocationDefinition>();
            Markers = new List<MapMarkerDefinition>();
            Boundaries = new List<MapBoundaryDefinition>();
            TerrainPatches = new List<MapTerrainPatchDefinition>();
            LootTables = new List<MapLootTableDefinition>();
            EncounterTables = new List<MapEncounterTableDefinition>();
            Routes = new List<ExpeditionRouteDefinition>();
        }

        public string StartLocationId { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string DefaultTerrainId { get; set; }
        public List<MapLocationDefinition> Locations { get; private set; }
        public List<MapMarkerDefinition> Markers { get; private set; }
        public List<MapBoundaryDefinition> Boundaries { get; private set; }
        public List<MapTerrainPatchDefinition> TerrainPatches { get; private set; }
        public List<MapLootTableDefinition> LootTables { get; private set; }
        public List<MapEncounterTableDefinition> EncounterTables { get; private set; }
        public List<ExpeditionRouteDefinition> Routes { get; private set; }
    }

    /// <summary>
    /// Authored point of interest or route endpoint on the scenario map.
    /// </summary>
    public class MapLocationDefinition
    {
        public MapLocationDefinition()
        {
            Properties = new List<ScenarioProperty>();
            Searchable = true;
            DiscoveredAtStart = true;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Kind { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; }
        public bool Searchable { get; set; }
        public bool DiscoveredAtStart { get; set; }
        public string MarkerId { get; set; }
        public string BoundaryId { get; set; }
        public string TerrainId { get; set; }
        public string LootTableId { get; set; }
        public string EncounterTableId { get; set; }
        public string RequiredGateId { get; set; }
        public int Danger { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Visual and semantic category for a scenario map marker.
    /// </summary>
    public enum MapMarkerKind
    {
        PointOfInterest = 0,
        Shelter = 1,
        House = 2,
        Town = 3,
        City = 4,
        Resource = 5,
        Quest = 6,
        Hazard = 7,
        Custom = 8
    }

    /// <summary>
    /// Visual marker shown on the scenario map.
    /// Markers can be linked to locations or boundaries and gated through runtime visibility.
    /// </summary>
    public class MapMarkerDefinition
    {
        public MapMarkerDefinition()
        {
            Kind = MapMarkerKind.PointOfInterest;
            VisibleAtStart = true;
            Tags = new List<string>();
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public MapMarkerKind Kind { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string IconId { get; set; }
        public string LocationId { get; set; }
        public string BoundaryId { get; set; }
        public bool VisibleAtStart { get; set; }
        public List<string> Tags { get; private set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Semantic role for a polygon or rectangular map boundary.
    /// </summary>
    public enum MapBoundaryKind
    {
        Region = 0,
        SearchArea = 1,
        BlockedArea = 2,
        EncounterZone = 3,
        LootZone = 4,
        TerrainZone = 5,
        Custom = 6
    }

    /// <summary>
    /// Region, blocked area, loot zone, encounter zone, or terrain area on the map.
    /// </summary>
    public class MapBoundaryDefinition
    {
        public MapBoundaryDefinition()
        {
            Kind = MapBoundaryKind.Region;
            Points = new List<MapPointDefinition>();
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public MapBoundaryKind Kind { get; set; }
        public float? MinX { get; set; }
        public float? MinY { get; set; }
        public float? MaxX { get; set; }
        public float? MaxY { get; set; }
        public string TerrainId { get; set; }
        public string LootTableId { get; set; }
        public string EncounterTableId { get; set; }
        public string RequiredGateId { get; set; }
        public List<MapPointDefinition> Points { get; private set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Serializable two-dimensional map point.
    /// </summary>
    public class MapPointDefinition
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>
    /// Shape used by a terrain patch brush.
    /// </summary>
    public enum MapTerrainBrushShape
    {
        Rectangle = 0,
        Circle = 1,
        Polygon = 2
    }

    /// <summary>
    /// Authored terrain override on the scenario map.
    /// Priority controls which patch wins when patches overlap.
    /// </summary>
    public class MapTerrainPatchDefinition
    {
        public MapTerrainPatchDefinition()
        {
            Shape = MapTerrainBrushShape.Rectangle;
            Points = new List<MapPointDefinition>();
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string TerrainId { get; set; }
        public MapTerrainBrushShape Shape { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Radius { get; set; }
        public int Priority { get; set; }
        public string BoundaryId { get; set; }
        public List<MapPointDefinition> Points { get; private set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Named loot table that map locations or boundaries can reference.
    /// </summary>
    public class MapLootTableDefinition
    {
        public MapLootTableDefinition()
        {
            Entries = new List<MapLootEntryDefinition>();
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public List<MapLootEntryDefinition> Entries { get; private set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Weighted item entry in a map loot table.
    /// </summary>
    public class MapLootEntryDefinition
    {
        public MapLootEntryDefinition()
        {
            MinQuantity = 1;
            MaxQuantity = 1;
            Weight = 1;
            Chance = 1f;
        }

        public string ItemId { get; set; }
        public int MinQuantity { get; set; }
        public int MaxQuantity { get; set; }
        public int Weight { get; set; }
        public float Chance { get; set; }
    }

    /// <summary>
    /// Named encounter table that map locations or boundaries can reference.
    /// </summary>
    public class MapEncounterTableDefinition
    {
        public MapEncounterTableDefinition()
        {
            Entries = new List<MapEncounterEntryDefinition>();
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public List<MapEncounterEntryDefinition> Entries { get; private set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Weighted encounter entry used when the scenario map rolls an encounter.
    /// </summary>
    public class MapEncounterEntryDefinition
    {
        public MapEncounterEntryDefinition()
        {
            MinCount = 1;
            MaxCount = 1;
            Weight = 1;
            Chance = 1f;
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string EncounterType { get; set; }
        public string FactionId { get; set; }
        public string PersonalityId { get; set; }
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
        public int Weight { get; set; }
        public float Chance { get; set; }
        public string LootTableId { get; set; }
        public string QuestId { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Authored route between map locations.
    /// Use gates to lock routes until scenario conditions are met.
    /// </summary>
    public class ExpeditionRouteDefinition
    {
        public ExpeditionRouteDefinition()
        {
            Waypoints = new List<MapPointDefinition>();
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string FromLocationId { get; set; }
        public string ToLocationId { get; set; }
        public bool OneWay { get; set; }
        public float Distance { get; set; }
        public int Risk { get; set; }
        public string RequiredGateId { get; set; }
        public List<MapPointDefinition> Waypoints { get; private set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }
}
