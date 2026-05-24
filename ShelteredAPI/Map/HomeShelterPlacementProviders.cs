namespace ShelteredAPI.Map
{
    /// <summary>
    /// Provider contract for mods that choose where the primary home shelter should be placed.
    /// ShelteredAPI owns provider ordering, coordinate normalization, and publication.
    /// </summary>
    public interface IHomeShelterPlacementProvider
    {
        /// <summary>
        /// Attempts to resolve a home shelter placement for the current map-generation pass.
        /// </summary>
        bool TryResolve(HomeShelterPlacementContext context, out HomeShelterPlacementResult result);
    }

    /// <summary>
    /// Registration for a home-shelter placement provider.
    /// </summary>
    public sealed class HomeShelterPlacementProviderRegistration
    {
        /// <summary>Creates an empty registration for object-initializer usage.</summary>
        public HomeShelterPlacementProviderRegistration()
        {
            SourceId = string.Empty;
            ProviderId = "home-shelter-placement";
        }

        /// <summary>Owning mod or integration identifier.</summary>
        public string SourceId { get; set; }
        /// <summary>Stable provider identifier within the owning source.</summary>
        public string ProviderId { get; set; }
        /// <summary>Provider ordering priority. Higher values are tried first.</summary>
        public int Priority { get; set; }
        /// <summary>Placement provider implementation.</summary>
        public IHomeShelterPlacementProvider Provider { get; set; }
        /// <summary>Optional listener notified after this provider's placement is accepted and published.</summary>
        public IHomeShelterPlacementResolutionListener ResolutionListener { get; set; }
    }

    /// <summary>
    /// Optional callback for providers that need to sync local compatibility state after
    /// ShelteredAPI accepts and publishes a placement.
    /// </summary>
    public interface IHomeShelterPlacementResolutionListener
    {
        /// <summary>Called after a provider result becomes the active published home-shelter placement.</summary>
        void OnHomeShelterPlacementResolved(HomeShelterPlacementResolution resolution);
    }

    /// <summary>
    /// Read-only notification payload for an accepted provider placement.
    /// </summary>
    public sealed class HomeShelterPlacementResolution
    {
        internal HomeShelterPlacementResolution(
            string sourceId,
            string providerId,
            string requestReason,
            HomeShelterPositionSnapshot snapshot)
        {
            SourceId = sourceId ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            RequestReason = requestReason ?? string.Empty;
            Snapshot = snapshot;
        }

        /// <summary>Owning mod or integration identifier.</summary>
        public string SourceId { get; private set; }
        /// <summary>Stable provider identifier within the owning source.</summary>
        public string ProviderId { get; private set; }
        /// <summary>Reason supplied by the ShelteredAPI placement request.</summary>
        public string RequestReason { get; private set; }
        /// <summary>Published home-shelter snapshot accepted by ShelteredAPI.</summary>
        public HomeShelterPositionSnapshot Snapshot { get; private set; }
    }

    /// <summary>
    /// Read-only map-generation context passed to home-shelter placement providers.
    /// </summary>
    public sealed class HomeShelterPlacementContext
    {
        internal HomeShelterPlacementContext(
            int mapWidth,
            int mapHeight,
            float worldWidth,
            float worldHeight,
            bool fromLiveMap,
            MapGenerationPolicySnapshot policies)
        {
            MapWidth = mapWidth;
            MapHeight = mapHeight;
            WorldWidth = worldWidth;
            WorldHeight = worldHeight;
            FromLiveMap = fromLiveMap;
            Policies = policies;
        }

        /// <summary>Current generated expedition-map grid width.</summary>
        public int MapWidth { get; private set; }
        /// <summary>Current generated expedition-map grid height.</summary>
        public int MapHeight { get; private set; }
        /// <summary>Current expedition-map world width.</summary>
        public float WorldWidth { get; private set; }
        /// <summary>Current expedition-map world height.</summary>
        public float WorldHeight { get; private set; }
        /// <summary>Whether the dimensions came from live game managers instead of defaults.</summary>
        public bool FromLiveMap { get; private set; }
        /// <summary>Resolved map-generation policy intent active for this placement pass.</summary>
        public MapGenerationPolicySnapshot Policies { get; private set; }

        /// <summary>Returns whether a grid cell is inside the current map and allowed by home-placement policy.</summary>
        public bool IsHomeShelterPlacementEligible(ExpeditionMapGridPosition gridPosition)
        {
            return Policies == null
                ? IsInsideMap(gridPosition)
                : Policies.IsHomeShelterPlacementEligible(gridPosition, MapWidth, MapHeight);
        }

        /// <summary>Returns whether a grid cell is inside the current map bounds.</summary>
        public bool IsInsideMap(ExpeditionMapGridPosition gridPosition)
        {
            return MapWidth > 0
                && MapHeight > 0
                && gridPosition.X >= 0
                && gridPosition.Y >= 0
                && gridPosition.X < MapWidth
                && gridPosition.Y < MapHeight;
        }

        /// <summary>Converts world coordinates to grid coordinates using current map dimensions.</summary>
        public bool TryWorldToGrid(ExpeditionMapWorldPosition worldPosition, out ExpeditionMapGridPosition gridPosition)
        {
            gridPosition = new ExpeditionMapGridPosition();
            if (!CanConvert() || !IsFinite(worldPosition.X) || !IsFinite(worldPosition.Y))
                return false;

            int x = (int)((worldPosition.X + (WorldWidth * 0.5f)) * (MapWidth / WorldWidth));
            int y = (int)((worldPosition.Y + (WorldHeight * 0.5f)) * (MapHeight / WorldHeight));
            if (x >= MapWidth) x = MapWidth - 1;
            if (y >= MapHeight) y = MapHeight - 1;
            gridPosition = new ExpeditionMapGridPosition(x, y);
            return true;
        }

        /// <summary>Converts a grid cell to its center world position using current map dimensions.</summary>
        public bool TryGridToWorldCenter(ExpeditionMapGridPosition gridPosition, out ExpeditionMapWorldPosition worldPosition)
        {
            worldPosition = new ExpeditionMapWorldPosition();
            if (!CanConvert() || !IsInsideMap(gridPosition))
                return false;

            float cellWidth = WorldWidth / MapWidth;
            float cellHeight = WorldHeight / MapHeight;
            worldPosition = new ExpeditionMapWorldPosition(
                (-WorldWidth * 0.5f) + ((gridPosition.X + 0.5f) * cellWidth),
                (-WorldHeight * 0.5f) + ((gridPosition.Y + 0.5f) * cellHeight));
            return true;
        }

        private bool CanConvert()
        {
            return MapWidth > 0 && MapHeight > 0 && WorldWidth > 0f && WorldHeight > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Placement result returned by a home-shelter placement provider.
    /// </summary>
    public sealed class HomeShelterPlacementResult
    {
        /// <summary>Creates a primary, active, visible home-shelter placement.</summary>
        public HomeShelterPlacementResult()
        {
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

        /// <summary>Stable home/bunker identifier within the provider source.</summary>
        public string HomeId { get; set; }
        /// <summary>Display label for diagnostics and map consumers.</summary>
        public string DisplayName { get; set; }
        /// <summary>Logical owner/player id. Single-player home shelter should use 0.</summary>
        public int OwnerId { get; set; }
        /// <summary>Whether this result represents the primary player home.</summary>
        public bool IsPrimary { get; set; }
        /// <summary>Whether this result should be preferred for active-context reads.</summary>
        public bool IsActive { get; set; }
        /// <summary>Whether this home should be visible to map consumers.</summary>
        public bool IsVisible { get; set; }
        /// <summary>Whether this home owner is currently online/available.</summary>
        public bool IsOnline { get; set; }
        /// <summary>Whether map generation should create initial nearby locations around this home shelter.</summary>
        public bool GenerateStartingLocations { get; set; }
        /// <summary>Optional world-space expedition-map position.</summary>
        public ExpeditionMapWorldPosition? WorldPosition { get; set; }
        /// <summary>Optional grid-space expedition-map position.</summary>
        public ExpeditionMapGridPosition? GridPosition { get; set; }
        /// <summary>Optional map-pixel expedition-map position.</summary>
        public ExpeditionMapPixelPosition? MapPosition { get; set; }
        /// <summary>Minimum cells the home should remain away from the map edge.</summary>
        public int MinimumEdgeDistanceInCells { get; set; }
        /// <summary>Short diagnostic reason or source label for this resolution.</summary>
        public string SourceReason { get; set; }
    }
}
