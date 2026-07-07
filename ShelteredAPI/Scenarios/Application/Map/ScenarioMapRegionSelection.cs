namespace ShelteredAPI.Scenarios.Application.Map{
    internal sealed class ScenarioMapRegionSelection
    {
        public string SelectionId { get; set; }
        public string LocationId { get; set; }
        public string DisplayName { get; set; }
        public string RegionName { get; set; }
        public string TownName { get; set; }
        public string Category { get; set; }
        public string Topography { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public bool Searchable { get; set; }
        public bool VisibleOnMap { get; set; }
        public bool Discovered { get; set; }
        public bool HiddenUntilDiscovered { get; set; }
        public bool HasItems { get; set; }
        public bool HasQuest { get; set; }
        public bool HasHiddenItems { get; set; }
        public int MaxItems { get; set; }
        public int LocationSpecificLootTypeCount { get; set; }
        public float MinSearchTime { get; set; }
        public float MaxSearchTime { get; set; }
        public int SearchNpcRevealChance { get; set; }
        public int OpenGroundEncounterChance { get; set; }
        public int OpenGroundFactionEncounterChance { get; set; }
        public int AnimalEncounterChance { get; set; }
        public bool Captured { get; set; }
        public string CapturedLocationId { get; set; }
        public string Source { get; set; }

        public ScenarioMapRegionSelection Copy()
        {
            return new ScenarioMapRegionSelection
            {
                SelectionId = SelectionId,
                LocationId = LocationId,
                DisplayName = DisplayName,
                RegionName = RegionName,
                TownName = TownName,
                Category = Category,
                Topography = Topography,
                GridX = GridX,
                GridY = GridY,
                WorldX = WorldX,
                WorldY = WorldY,
                Searchable = Searchable,
                VisibleOnMap = VisibleOnMap,
                Discovered = Discovered,
                HiddenUntilDiscovered = HiddenUntilDiscovered,
                HasItems = HasItems,
                HasQuest = HasQuest,
                HasHiddenItems = HasHiddenItems,
                MaxItems = MaxItems,
                LocationSpecificLootTypeCount = LocationSpecificLootTypeCount,
                MinSearchTime = MinSearchTime,
                MaxSearchTime = MaxSearchTime,
                SearchNpcRevealChance = SearchNpcRevealChance,
                OpenGroundEncounterChance = OpenGroundEncounterChance,
                OpenGroundFactionEncounterChance = OpenGroundFactionEncounterChance,
                AnimalEncounterChance = AnimalEncounterChance,
                Captured = Captured,
                CapturedLocationId = CapturedLocationId,
                Source = Source
            };
        }
    }
}
