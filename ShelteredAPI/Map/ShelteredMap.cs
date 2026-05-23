using ShelteredAPI.Map.Internal;

namespace ShelteredAPI.Map
{
    /// <summary>
    /// Stable facade for active expedition-map facts and map-generation policy intent.
    /// </summary>
    public static class ShelteredMap
    {
        /// <summary>Captures the currently available read-only expedition map context.</summary>
        public static ExpeditionMapContext Current
        {
            get { return ExpeditionMapContextReader.Capture(); }
        }

        /// <summary>Captures the currently available read-only expedition map context.</summary>
        public static ExpeditionMapContext GetCurrentContext()
        {
            return ExpeditionMapContextReader.Capture();
        }

        /// <summary>Registers non-settlement location-density intent.</summary>
        public static MapPolicyRegistrationResult RegisterLocationDensityPolicy(LocationDensityPolicy policy)
        {
            return MapGenerationPolicyRegistry.Register(policy);
        }

        /// <summary>Registers settlement-density intent.</summary>
        public static MapPolicyRegistrationResult RegisterTownDensityPolicy(TownDensityPolicy policy)
        {
            return MapGenerationPolicyRegistry.Register(policy);
        }

        /// <summary>Registers quest placement eligibility intent.</summary>
        public static MapPolicyRegistrationResult RegisterQuestPlacementPolicy(QuestPlacementPolicy policy)
        {
            return MapGenerationPolicyRegistry.Register(policy);
        }

        /// <summary>Registers faction-zone placement eligibility intent.</summary>
        public static MapPolicyRegistrationResult RegisterFactionZonePlacementPolicy(FactionZonePlacementPolicy policy)
        {
            return MapGenerationPolicyRegistry.Register(policy);
        }

        /// <summary>Registers home-shelter placement intent.</summary>
        public static MapPolicyRegistrationResult RegisterHomeShelterPlacementPolicy(HomeShelterPlacementPolicy policy)
        {
            return MapGenerationPolicyRegistry.Register(policy);
        }

        /// <summary>Registers special-item region eligibility intent.</summary>
        public static MapPolicyRegistrationResult RegisterSpecialItemRegionEligibilityPolicy(SpecialItemRegionEligibilityPolicy policy)
        {
            return MapGenerationPolicyRegistry.Register(policy);
        }

        /// <summary>Removes all categories registered under one source and policy identifier.</summary>
        public static int UnregisterPolicy(string sourceId, string policyId)
        {
            return MapGenerationPolicyRegistry.Unregister(sourceId, policyId);
        }

        /// <summary>Removes all map-generation policies owned by a source identifier.</summary>
        public static int ClearPolicies(string sourceId)
        {
            return MapGenerationPolicyRegistry.Clear(sourceId);
        }

        /// <summary>Returns a deterministic resolved snapshot of all registered policy intent.</summary>
        public static MapGenerationPolicySnapshot ResolveGenerationPolicies()
        {
            return MapGenerationPolicyRegistry.Resolve();
        }
    }
}
