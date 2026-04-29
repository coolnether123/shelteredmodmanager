using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
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

    public class MapPointDefinition
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public enum MapTerrainBrushShape
    {
        Rectangle = 0,
        Circle = 1,
        Polygon = 2
    }

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
