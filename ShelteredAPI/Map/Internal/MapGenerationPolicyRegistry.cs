using System;
using System.Collections.Generic;

namespace ShelteredAPI.Map.Internal
{
    internal static class MapGenerationPolicyRegistry
    {
        private const float MinimumMultiplier = 0.01f;
        private const float MaximumMultiplier = 100f;
        private static readonly object Sync = new object();
        private static readonly List<ExpeditionMapGenerationPolicy> Policies = new List<ExpeditionMapGenerationPolicy>();

        internal static MapPolicyRegistrationResult Register<TPolicy>(TPolicy policy)
            where TPolicy : ExpeditionMapGenerationPolicy
        {
            ExpeditionMapGenerationPolicy ownedPolicy = CopyPolicy(policy);
            string error = Validate(ownedPolicy);
            if (error != null)
                return MapPolicyRegistrationResult.Failed(error);

            lock (Sync)
            {
                bool replaced = false;
                for (int i = Policies.Count - 1; i >= 0; i--)
                {
                    ExpeditionMapGenerationPolicy existing = Policies[i];
                    if (existing != null
                        && existing.GetType() == ownedPolicy.GetType()
                        && string.Equals(existing.SourceId, ownedPolicy.SourceId, StringComparison.Ordinal)
                        && string.Equals(existing.PolicyId, ownedPolicy.PolicyId, StringComparison.Ordinal))
                    {
                        Policies.RemoveAt(i);
                        replaced = true;
                    }
                }

                Policies.Add(ownedPolicy);
                return MapPolicyRegistrationResult.Ok(replaced);
            }
        }

        internal static int Unregister(string sourceId, string policyId)
        {
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(policyId))
                return 0;

            int removed = 0;
            lock (Sync)
            {
                for (int i = Policies.Count - 1; i >= 0; i--)
                {
                    ExpeditionMapGenerationPolicy policy = Policies[i];
                    if (policy != null
                        && string.Equals(policy.SourceId, sourceId, StringComparison.Ordinal)
                        && string.Equals(policy.PolicyId, policyId, StringComparison.Ordinal))
                    {
                        Policies.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
        }

        internal static int Clear(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return 0;

            int removed = 0;
            lock (Sync)
            {
                for (int i = Policies.Count - 1; i >= 0; i--)
                {
                    ExpeditionMapGenerationPolicy policy = Policies[i];
                    if (policy != null && string.Equals(policy.SourceId, sourceId, StringComparison.Ordinal))
                    {
                        Policies.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
        }

        internal static MapGenerationPolicySnapshot Resolve()
        {
            List<ExpeditionMapGenerationPolicy> ordered;
            lock (Sync)
            {
                ordered = new List<ExpeditionMapGenerationPolicy>(Policies);
            }

            ordered.Sort(ComparePolicies);
            MapGenerationPolicySnapshot result = new MapGenerationPolicySnapshot();
            result.PolicyCount = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
            {
                ExpeditionMapGenerationPolicy policy = ordered[i];
                LocationDensityPolicy locations = policy as LocationDensityPolicy;
                if (locations != null)
                {
                    result.LocationDensityMultiplier = Multiply(result.LocationDensityMultiplier, locations.Multiplier);
                    continue;
                }

                TownDensityPolicy towns = policy as TownDensityPolicy;
                if (towns != null)
                {
                    result.TownDensityMultiplier = Multiply(result.TownDensityMultiplier, towns.Multiplier);
                    continue;
                }

                QuestPlacementPolicy quests = policy as QuestPlacementPolicy;
                if (quests != null)
                {
                    int minimum = result.QuestMinimumHomeDistanceInCells;
                    int? maximum = result.QuestMaximumHomeDistanceInCells;
                    MergeDistance(
                        quests.MinimumHomeDistanceInCells,
                        quests.MaximumHomeDistanceInCells,
                        ref minimum,
                        ref maximum);
                    result.QuestMinimumHomeDistanceInCells = minimum;
                    result.QuestMaximumHomeDistanceInCells = maximum;
                    continue;
                }

                FactionZonePlacementPolicy factions = policy as FactionZonePlacementPolicy;
                if (factions != null)
                {
                    int minimum = result.FactionZoneMinimumHomeDistanceInCells;
                    int? maximum = result.FactionZoneMaximumHomeDistanceInCells;
                    MergeDistance(
                        factions.MinimumHomeDistanceInCells,
                        factions.MaximumHomeDistanceInCells,
                        ref minimum,
                        ref maximum);
                    result.FactionZoneMinimumHomeDistanceInCells = minimum;
                    result.FactionZoneMaximumHomeDistanceInCells = maximum;
                    continue;
                }

                HomeShelterPlacementPolicy home = policy as HomeShelterPlacementPolicy;
                if (home != null)
                {
                    result.HomeShelterMinimumEdgeDistanceInCells = Math.Max(
                        result.HomeShelterMinimumEdgeDistanceInCells,
                        home.MinimumEdgeDistanceInCells);
                    if (home.PreferredGridPosition.HasValue)
                    {
                        result.PreferredHomeShelterGridPosition = home.PreferredGridPosition.Value;
                        result.HasPreferredHomeShelterGridPosition = true;
                    }
                    continue;
                }

                SpecialItemRegionEligibilityPolicy items = policy as SpecialItemRegionEligibilityPolicy;
                if (items != null)
                {
                    int minimum = result.SpecialItemMinimumHomeDistanceInCells;
                    int? maximum = result.SpecialItemMaximumHomeDistanceInCells;
                    MergeDistance(
                        items.MinimumHomeDistanceInCells,
                        items.MaximumHomeDistanceInCells,
                        ref minimum,
                        ref maximum);
                    result.SpecialItemMinimumHomeDistanceInCells = minimum;
                    result.SpecialItemMaximumHomeDistanceInCells = maximum;
                }
            }

            return result;
        }

        private static string Validate(ExpeditionMapGenerationPolicy policy)
        {
            if (policy == null)
                return "Policy cannot be null.";
            if (string.IsNullOrEmpty(policy.SourceId))
                return "SourceId is required.";
            if (string.IsNullOrEmpty(policy.PolicyId))
                return "PolicyId is required.";

            LocationDensityPolicy locations = policy as LocationDensityPolicy;
            if (locations != null)
                return ValidateMultiplier(locations.Multiplier);

            TownDensityPolicy towns = policy as TownDensityPolicy;
            if (towns != null)
                return ValidateMultiplier(towns.Multiplier);

            QuestPlacementPolicy quests = policy as QuestPlacementPolicy;
            if (quests != null)
                return ValidateDistance(quests.MinimumHomeDistanceInCells, quests.MaximumHomeDistanceInCells);

            FactionZonePlacementPolicy factions = policy as FactionZonePlacementPolicy;
            if (factions != null)
                return ValidateDistance(factions.MinimumHomeDistanceInCells, factions.MaximumHomeDistanceInCells);

            HomeShelterPlacementPolicy home = policy as HomeShelterPlacementPolicy;
            if (home != null)
                return home.MinimumEdgeDistanceInCells < 0 ? "MinimumEdgeDistanceInCells cannot be negative." : null;

            SpecialItemRegionEligibilityPolicy items = policy as SpecialItemRegionEligibilityPolicy;
            if (items != null)
                return ValidateDistance(items.MinimumHomeDistanceInCells, items.MaximumHomeDistanceInCells);

            return "Unsupported map generation policy type.";
        }

        private static ExpeditionMapGenerationPolicy CopyPolicy(ExpeditionMapGenerationPolicy policy)
        {
            if (policy == null)
                return null;

            LocationDensityPolicy locations = policy as LocationDensityPolicy;
            if (locations != null)
                return CopyIdentity(locations, new LocationDensityPolicy { Multiplier = locations.Multiplier });

            TownDensityPolicy towns = policy as TownDensityPolicy;
            if (towns != null)
                return CopyIdentity(towns, new TownDensityPolicy { Multiplier = towns.Multiplier });

            QuestPlacementPolicy quests = policy as QuestPlacementPolicy;
            if (quests != null)
            {
                return CopyIdentity(quests, new QuestPlacementPolicy
                {
                    MinimumHomeDistanceInCells = quests.MinimumHomeDistanceInCells,
                    MaximumHomeDistanceInCells = quests.MaximumHomeDistanceInCells
                });
            }

            FactionZonePlacementPolicy factions = policy as FactionZonePlacementPolicy;
            if (factions != null)
            {
                return CopyIdentity(factions, new FactionZonePlacementPolicy
                {
                    MinimumHomeDistanceInCells = factions.MinimumHomeDistanceInCells,
                    MaximumHomeDistanceInCells = factions.MaximumHomeDistanceInCells
                });
            }

            HomeShelterPlacementPolicy home = policy as HomeShelterPlacementPolicy;
            if (home != null)
            {
                return CopyIdentity(home, new HomeShelterPlacementPolicy
                {
                    PreferredGridPosition = home.PreferredGridPosition,
                    MinimumEdgeDistanceInCells = home.MinimumEdgeDistanceInCells
                });
            }

            SpecialItemRegionEligibilityPolicy items = policy as SpecialItemRegionEligibilityPolicy;
            if (items != null)
            {
                return CopyIdentity(items, new SpecialItemRegionEligibilityPolicy
                {
                    MinimumHomeDistanceInCells = items.MinimumHomeDistanceInCells,
                    MaximumHomeDistanceInCells = items.MaximumHomeDistanceInCells
                });
            }

            return policy;
        }

        private static TPolicy CopyIdentity<TPolicy>(ExpeditionMapGenerationPolicy source, TPolicy target)
            where TPolicy : ExpeditionMapGenerationPolicy
        {
            target.SourceId = source.SourceId;
            target.PolicyId = source.PolicyId;
            target.Priority = source.Priority;
            return target;
        }

        private static string ValidateMultiplier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                return "Multiplier must be finite and greater than zero.";
            return null;
        }

        private static string ValidateDistance(int minimum, int? maximum)
        {
            if (minimum < 0)
                return "MinimumHomeDistanceInCells cannot be negative.";
            if (maximum.HasValue && maximum.Value < minimum)
                return "MaximumHomeDistanceInCells cannot be less than MinimumHomeDistanceInCells.";
            return null;
        }

        private static void MergeDistance(int minimum, int? maximum, ref int resolvedMinimum, ref int? resolvedMaximum)
        {
            resolvedMinimum = Math.Max(resolvedMinimum, minimum);
            if (maximum.HasValue
                && (!resolvedMaximum.HasValue || maximum.Value < resolvedMaximum.Value))
            {
                resolvedMaximum = maximum.Value;
            }
        }

        private static float Multiply(float current, float multiplier)
        {
            float value = current * multiplier;
            if (value < MinimumMultiplier)
                return MinimumMultiplier;
            if (value > MaximumMultiplier || float.IsInfinity(value))
                return MaximumMultiplier;
            return value;
        }

        private static int ComparePolicies(ExpeditionMapGenerationPolicy left, ExpeditionMapGenerationPolicy right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
                return priority;

            int source = string.Compare(left.SourceId, right.SourceId, StringComparison.Ordinal);
            if (source != 0)
                return source;

            int id = string.Compare(left.PolicyId, right.PolicyId, StringComparison.Ordinal);
            if (id != 0)
                return id;

            return string.Compare(left.GetType().FullName, right.GetType().FullName, StringComparison.Ordinal);
        }
    }
}
