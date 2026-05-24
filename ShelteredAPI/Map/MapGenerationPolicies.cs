using System;

namespace ShelteredAPI.Map
{
    /// <summary>
    /// Shared identity and ordering metadata for one map-generation intent.
    /// Policies with higher priority resolve later; ties are ordered by source and policy id.
    /// </summary>
    public abstract class ExpeditionMapGenerationPolicy
    {
        /// <summary>Creates an empty policy for object-initializer registration.</summary>
        protected ExpeditionMapGenerationPolicy()
        {
        }

        /// <summary>Creates a policy with stable registration identity and priority.</summary>
        protected ExpeditionMapGenerationPolicy(string sourceId, string policyId, int priority)
        {
            SourceId = sourceId;
            PolicyId = policyId;
            Priority = priority;
        }

        /// <summary>Stable identifier of the mod or integration registering this intent.</summary>
        public string SourceId { get; set; }
        /// <summary>Stable identifier for this source's policy registration.</summary>
        public string PolicyId { get; set; }
        /// <summary>Resolution ordering priority; higher values resolve later.</summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Multiplicative intent for non-settlement generated searchable locations.
    /// </summary>
    public sealed class LocationDensityPolicy : ExpeditionMapGenerationPolicy
    {
        /// <summary>Creates a no-op density policy for object-initializer registration.</summary>
        public LocationDensityPolicy()
        {
            Multiplier = 1f;
        }

        /// <summary>Creates a location-density policy.</summary>
        public LocationDensityPolicy(string sourceId, string policyId, float multiplier, int priority)
            : base(sourceId, policyId, priority)
        {
            Multiplier = multiplier;
        }

        /// <summary>Positive multiplicative location-density intent.</summary>
        public float Multiplier { get; set; }
    }

    /// <summary>
    /// Multiplicative intent for vanilla city, town, and village generation counts.
    /// </summary>
    public sealed class TownDensityPolicy : ExpeditionMapGenerationPolicy
    {
        /// <summary>Creates a no-op town-density policy for object-initializer registration.</summary>
        public TownDensityPolicy()
        {
            Multiplier = 1f;
        }

        /// <summary>Creates a town-density policy.</summary>
        public TownDensityPolicy(string sourceId, string policyId, float multiplier, int priority)
            : base(sourceId, policyId, priority)
        {
            Multiplier = multiplier;
        }

        /// <summary>Positive multiplicative settlement-density intent.</summary>
        public float Multiplier { get; set; }
    }

    /// <summary>
    /// Distance eligibility intent for quest locations, measured in vanilla grid cells from home.
    /// </summary>
    public sealed class QuestPlacementPolicy : ExpeditionMapGenerationPolicy
    {
        /// <summary>Creates an unrestricted quest-placement policy for object-initializer registration.</summary>
        public QuestPlacementPolicy()
        {
        }

        /// <summary>Creates a quest-placement distance policy.</summary>
        public QuestPlacementPolicy(string sourceId, string policyId, int minimumHomeDistanceInCells, int? maximumHomeDistanceInCells, int priority)
            : base(sourceId, policyId, priority)
        {
            MinimumHomeDistanceInCells = minimumHomeDistanceInCells;
            MaximumHomeDistanceInCells = maximumHomeDistanceInCells;
        }

        /// <summary>Minimum grid-cell distance from home permitted by this intent.</summary>
        public int MinimumHomeDistanceInCells { get; set; }
        /// <summary>Optional maximum grid-cell distance from home permitted by this intent.</summary>
        public int? MaximumHomeDistanceInCells { get; set; }
    }

    /// <summary>
    /// Distance eligibility intent for faction-zone placement, measured in vanilla grid cells from home.
    /// </summary>
    public sealed class FactionZonePlacementPolicy : ExpeditionMapGenerationPolicy
    {
        /// <summary>Creates an unrestricted faction-zone policy for object-initializer registration.</summary>
        public FactionZonePlacementPolicy()
        {
        }

        /// <summary>Creates a faction-zone distance policy.</summary>
        public FactionZonePlacementPolicy(string sourceId, string policyId, int minimumHomeDistanceInCells, int? maximumHomeDistanceInCells, int priority)
            : base(sourceId, policyId, priority)
        {
            MinimumHomeDistanceInCells = minimumHomeDistanceInCells;
            MaximumHomeDistanceInCells = maximumHomeDistanceInCells;
        }

        /// <summary>Minimum grid-cell distance from home permitted by this intent.</summary>
        public int MinimumHomeDistanceInCells { get; set; }
        /// <summary>Optional maximum grid-cell distance from home permitted by this intent.</summary>
        public int? MaximumHomeDistanceInCells { get; set; }
    }

    /// <summary>
    /// Intent for choosing the home shelter map cell. A preferred position is optional so mods can
    /// contribute edge-distance safety without choosing a competing absolute location.
    /// </summary>
    public sealed class HomeShelterPlacementPolicy : ExpeditionMapGenerationPolicy
    {
        /// <summary>Creates an unrestricted home-shelter policy for object-initializer registration.</summary>
        public HomeShelterPlacementPolicy()
        {
        }

        /// <summary>Creates a home-shelter placement policy.</summary>
        public HomeShelterPlacementPolicy(
            string sourceId,
            string policyId,
            ExpeditionMapGridPosition? preferredGridPosition,
            int minimumEdgeDistanceInCells,
            int priority)
            : base(sourceId, policyId, priority)
        {
            PreferredGridPosition = preferredGridPosition;
            MinimumEdgeDistanceInCells = minimumEdgeDistanceInCells;
        }

        /// <summary>Optional requested grid cell; the final highest-priority request resolves.</summary>
        public ExpeditionMapGridPosition? PreferredGridPosition { get; set; }
        /// <summary>Minimum cells a candidate home location should remain away from a map edge.</summary>
        public int MinimumEdgeDistanceInCells { get; set; }
    }

    /// <summary>
    /// Distance eligibility intent for regions that can receive vanilla special items.
    /// </summary>
    public sealed class SpecialItemRegionEligibilityPolicy : ExpeditionMapGenerationPolicy
    {
        /// <summary>Creates an unrestricted special-item policy for object-initializer registration.</summary>
        public SpecialItemRegionEligibilityPolicy()
        {
        }

        /// <summary>Creates a special-item eligibility distance policy.</summary>
        public SpecialItemRegionEligibilityPolicy(
            string sourceId,
            string policyId,
            int minimumHomeDistanceInCells,
            int? maximumHomeDistanceInCells,
            int priority)
            : base(sourceId, policyId, priority)
        {
            MinimumHomeDistanceInCells = minimumHomeDistanceInCells;
            MaximumHomeDistanceInCells = maximumHomeDistanceInCells;
        }

        /// <summary>Minimum grid-cell distance from home permitted by this intent.</summary>
        public int MinimumHomeDistanceInCells { get; set; }
        /// <summary>Optional maximum grid-cell distance from home permitted by this intent.</summary>
        public int? MaximumHomeDistanceInCells { get; set; }
    }

    /// <summary>
    /// Result returned when a mod registers one policy intent.
    /// </summary>
    public sealed class MapPolicyRegistrationResult
    {
        internal static MapPolicyRegistrationResult Ok(bool replacedExisting)
        {
            return new MapPolicyRegistrationResult { Success = true, ReplacedExisting = replacedExisting };
        }

        internal static MapPolicyRegistrationResult Failed(string errorMessage)
        {
            return new MapPolicyRegistrationResult { ErrorMessage = errorMessage };
        }

        /// <summary>Whether the policy was accepted into the registry.</summary>
        public bool Success { get; private set; }
        /// <summary>Whether a registration of the same category, source, and id was replaced.</summary>
        public bool ReplacedExisting { get; private set; }
        /// <summary>Validation failure detail when registration did not succeed.</summary>
        public string ErrorMessage { get; private set; }
    }

    /// <summary>
    /// Deterministically resolved policy intent. A snapshot with <see cref="PolicyCount"/> equal to
    /// zero represents vanilla behavior: density multipliers are one and placement is unrestricted.
    /// </summary>
    public sealed class MapGenerationPolicySnapshot
    {
        internal MapGenerationPolicySnapshot()
        {
            LocationDensityMultiplier = 1f;
            TownDensityMultiplier = 1f;
            PolicyConflictSummary = string.Empty;
        }

        /// <summary>Number of registrations included in this resolution.</summary>
        public int PolicyCount { get; internal set; }
        /// <summary>Combined non-settlement location-density intent.</summary>
        public float LocationDensityMultiplier { get; internal set; }
        /// <summary>Combined settlement-density intent.</summary>
        public float TownDensityMultiplier { get; internal set; }
        /// <summary>Resolved minimum quest distance from home in cells.</summary>
        public int QuestMinimumHomeDistanceInCells { get; internal set; }
        /// <summary>Resolved optional maximum quest distance from home in cells.</summary>
        public int? QuestMaximumHomeDistanceInCells { get; internal set; }
        /// <summary>Resolved minimum faction-zone distance from home in cells.</summary>
        public int FactionZoneMinimumHomeDistanceInCells { get; internal set; }
        /// <summary>Resolved optional maximum faction-zone distance from home in cells.</summary>
        public int? FactionZoneMaximumHomeDistanceInCells { get; internal set; }
        /// <summary>Resolved minimum home location distance from the map edge.</summary>
        public int HomeShelterMinimumEdgeDistanceInCells { get; internal set; }
        /// <summary>Whether a preferred home grid position was resolved.</summary>
        public bool HasPreferredHomeShelterGridPosition { get; internal set; }
        /// <summary>Highest-priority resolved preferred home grid position, when available.</summary>
        public ExpeditionMapGridPosition PreferredHomeShelterGridPosition { get; internal set; }
        /// <summary>Resolved minimum special-item region distance from home in cells.</summary>
        public int SpecialItemMinimumHomeDistanceInCells { get; internal set; }
        /// <summary>Resolved optional maximum special-item region distance from home in cells.</summary>
        public int? SpecialItemMaximumHomeDistanceInCells { get; internal set; }
        /// <summary>Whether one or more registered policies produced contradictory resolved constraints.</summary>
        public bool HasPolicyConflicts { get; internal set; }
        /// <summary>Human-readable conflict summary for diagnostics and support bundles.</summary>
        public string PolicyConflictSummary { get; internal set; }

        /// <summary>Checks whether a quest candidate satisfies combined distance constraints.</summary>
        public bool IsQuestPlacementEligible(ExpeditionMapGridPosition home, ExpeditionMapGridPosition candidate)
        {
            return IsDistanceEligible(home, candidate, QuestMinimumHomeDistanceInCells, QuestMaximumHomeDistanceInCells);
        }

        /// <summary>Checks whether a faction-zone candidate satisfies combined distance constraints.</summary>
        public bool IsFactionZonePlacementEligible(ExpeditionMapGridPosition home, ExpeditionMapGridPosition candidate)
        {
            return IsDistanceEligible(home, candidate, FactionZoneMinimumHomeDistanceInCells, FactionZoneMaximumHomeDistanceInCells);
        }

        /// <summary>Checks whether a special-item candidate satisfies combined distance constraints.</summary>
        public bool IsSpecialItemRegionEligible(ExpeditionMapGridPosition home, ExpeditionMapGridPosition candidate)
        {
            return IsDistanceEligible(home, candidate, SpecialItemMinimumHomeDistanceInCells, SpecialItemMaximumHomeDistanceInCells);
        }

        /// <summary>Checks whether a home candidate satisfies map bounds and edge constraints.</summary>
        public bool IsHomeShelterPlacementEligible(ExpeditionMapGridPosition candidate, int mapWidth, int mapHeight)
        {
            if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= mapWidth || candidate.Y >= mapHeight)
                return false;

            int edgeDistance = Math.Min(
                Math.Min(candidate.X, mapWidth - candidate.X - 1),
                Math.Min(candidate.Y, mapHeight - candidate.Y - 1));
            return edgeDistance >= HomeShelterMinimumEdgeDistanceInCells;
        }

        /// <summary>Checks whether the resolved preferred home location is usable for a generated map size.</summary>
        public bool IsPreferredHomeShelterPlacementEligible(int mapWidth, int mapHeight)
        {
            return HasPreferredHomeShelterGridPosition
                && IsHomeShelterPlacementEligible(PreferredHomeShelterGridPosition, mapWidth, mapHeight);
        }

        private static bool IsDistanceEligible(
            ExpeditionMapGridPosition home,
            ExpeditionMapGridPosition candidate,
            int minimum,
            int? maximum)
        {
            int distance = Math.Max(Math.Abs(candidate.X - home.X), Math.Abs(candidate.Y - home.Y));
            return distance >= minimum && (!maximum.HasValue || distance <= maximum.Value);
        }
    }
}
