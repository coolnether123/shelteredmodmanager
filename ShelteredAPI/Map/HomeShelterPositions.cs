namespace ShelteredAPI.Map
{
    /// <summary>
    /// Public registration DTO for the active home shelter or bunker map position.
    /// Mods should register resolved home positions here instead of exposing mod-specific
    /// reflection APIs to other map consumers.
    /// </summary>
    public sealed class HomeShelterPositionRegistration
    {
        /// <summary>Creates a primary, visible, active home-shelter registration.</summary>
        public HomeShelterPositionRegistration()
        {
            SourceId = string.Empty;
            HomeId = "home-shelter";
            DisplayName = "Home Shelter";
            SourceReason = string.Empty;
            IsPrimary = true;
            IsActive = true;
            IsVisible = true;
            IsOnline = true;
            GenerateStartingLocations = true;
            MinimumEdgeDistanceInCells = 1;
        }

        /// <summary>Owning mod or integration identifier.</summary>
        public string SourceId { get; set; }
        /// <summary>Stable home/bunker identifier within the owning source.</summary>
        public string HomeId { get; set; }
        /// <summary>Display label for diagnostics and map consumers.</summary>
        public string DisplayName { get; set; }
        /// <summary>Logical owner/player id. Single-player home shelter should use 0.</summary>
        public int OwnerId { get; set; }
        /// <summary>Whether this registration represents the primary player home.</summary>
        public bool IsPrimary { get; set; }
        /// <summary>Whether this registration should be preferred for active-context reads.</summary>
        public bool IsActive { get; set; }
        /// <summary>Whether this home should be visible to map consumers.</summary>
        public bool IsVisible { get; set; }
        /// <summary>Whether this home owner is currently online/available.</summary>
        public bool IsOnline { get; set; }
        /// <summary>Whether map generation should create initial nearby locations around this home shelter.</summary>
        public bool GenerateStartingLocations { get; set; }
        /// <summary>Minimum cells this home should remain away from the map edge.</summary>
        public int MinimumEdgeDistanceInCells { get; set; }
        /// <summary>Resolution priority. Higher values win ties across sources.</summary>
        public int Priority { get; set; }
        /// <summary>Optional world-space expedition-map position.</summary>
        public ExpeditionMapWorldPosition? WorldPosition { get; set; }
        /// <summary>Optional grid-space expedition-map position.</summary>
        public ExpeditionMapGridPosition? GridPosition { get; set; }
        /// <summary>Optional map-pixel expedition-map position.</summary>
        public ExpeditionMapPixelPosition? MapPosition { get; set; }
        /// <summary>Short diagnostic reason or source label for this resolution.</summary>
        public string SourceReason { get; set; }
    }

    /// <summary>
    /// Read-only detached snapshot of one resolved home shelter or bunker position.
    /// </summary>
    public sealed class HomeShelterPositionSnapshot
    {
        internal HomeShelterPositionSnapshot()
        {
            SourceId = string.Empty;
            HomeId = string.Empty;
            DisplayName = string.Empty;
            SourceReason = string.Empty;
        }

        /// <summary>Owning mod or integration identifier.</summary>
        public string SourceId { get; internal set; }
        /// <summary>Stable home/bunker identifier within the owning source.</summary>
        public string HomeId { get; internal set; }
        /// <summary>Display label for diagnostics and map consumers.</summary>
        public string DisplayName { get; internal set; }
        /// <summary>Logical owner/player id.</summary>
        public int OwnerId { get; internal set; }
        /// <summary>Whether this snapshot represents the primary player home.</summary>
        public bool IsPrimary { get; internal set; }
        /// <summary>Whether this snapshot represents the active-context home.</summary>
        public bool IsActive { get; internal set; }
        /// <summary>Whether this home should be visible to map consumers.</summary>
        public bool IsVisible { get; internal set; }
        /// <summary>Whether this home owner is currently online/available.</summary>
        public bool IsOnline { get; internal set; }
        /// <summary>Whether map generation should create initial nearby locations around this home shelter.</summary>
        public bool GenerateStartingLocations { get; internal set; }
        /// <summary>Minimum cells this home should remain away from the map edge.</summary>
        public int MinimumEdgeDistanceInCells { get; internal set; }
        /// <summary>Resolution priority used by the registry.</summary>
        public int Priority { get; internal set; }
        /// <summary>Whether <see cref="WorldPosition"/> contains a resolved value.</summary>
        public bool HasWorldPosition { get; internal set; }
        /// <summary>World-space expedition-map position when available.</summary>
        public ExpeditionMapWorldPosition WorldPosition { get; internal set; }
        /// <summary>Whether <see cref="GridPosition"/> contains a resolved value.</summary>
        public bool HasGridPosition { get; internal set; }
        /// <summary>Grid-space expedition-map position when available.</summary>
        public ExpeditionMapGridPosition GridPosition { get; internal set; }
        /// <summary>Whether <see cref="MapPosition"/> contains a resolved value.</summary>
        public bool HasMapPosition { get; internal set; }
        /// <summary>Map-pixel expedition-map position when available.</summary>
        public ExpeditionMapPixelPosition MapPosition { get; internal set; }
        /// <summary>Short diagnostic reason or source label for this resolution.</summary>
        public string SourceReason { get; internal set; }

        internal HomeShelterPositionSnapshot Clone()
        {
            return new HomeShelterPositionSnapshot
            {
                SourceId = SourceId,
                HomeId = HomeId,
                DisplayName = DisplayName,
                OwnerId = OwnerId,
                IsPrimary = IsPrimary,
                IsActive = IsActive,
                IsVisible = IsVisible,
                IsOnline = IsOnline,
                GenerateStartingLocations = GenerateStartingLocations,
                MinimumEdgeDistanceInCells = MinimumEdgeDistanceInCells,
                Priority = Priority,
                HasWorldPosition = HasWorldPosition,
                WorldPosition = WorldPosition,
                HasGridPosition = HasGridPosition,
                GridPosition = GridPosition,
                HasMapPosition = HasMapPosition,
                MapPosition = MapPosition,
                SourceReason = SourceReason
            };
        }
    }
}
