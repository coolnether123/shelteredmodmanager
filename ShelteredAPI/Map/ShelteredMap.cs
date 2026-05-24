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

        /// <summary>Registers or updates the resolved home shelter/bunker position for map consumers.</summary>
        public static MapPolicyRegistrationResult RegisterHomeShelterPosition(HomeShelterPositionRegistration registration)
        {
            return HomeShelterPositionRegistry.Register(registration);
        }

        /// <summary>Removes one resolved home shelter/bunker position registration.</summary>
        public static int UnregisterHomeShelterPosition(string sourceId, string homeId)
        {
            return HomeShelterPositionRegistry.Unregister(sourceId, homeId);
        }

        /// <summary>Removes all resolved home shelter/bunker positions owned by one source.</summary>
        public static int ClearHomeShelterPositions(string sourceId)
        {
            return HomeShelterPositionRegistry.Clear(sourceId);
        }

        /// <summary>Attempts to get the primary home shelter/bunker position snapshot.</summary>
        public static bool TryGetPrimaryHomeShelter(out HomeShelterPositionSnapshot snapshot)
        {
            return HomeShelterPositionRegistry.TryGetPrimary(out snapshot);
        }

        /// <summary>Attempts to get the active-context home shelter/bunker position snapshot.</summary>
        public static bool TryGetActiveHomeShelter(out HomeShelterPositionSnapshot snapshot)
        {
            return HomeShelterPositionRegistry.TryGetActive(out snapshot);
        }

        /// <summary>Converts expedition world coordinates to the active map grid when possible.</summary>
        public static bool TryWorldToGrid(
            ExpeditionMapWorldPosition worldPosition,
            out ExpeditionMapGridPosition gridPosition)
        {
            return ExpeditionMapCoordinateConverter.TryWorldToGrid(worldPosition, out gridPosition);
        }

        /// <summary>Converts expedition grid coordinates to the cell-center world position when possible.</summary>
        public static bool TryGridToWorldCenter(
            ExpeditionMapGridPosition gridPosition,
            out ExpeditionMapWorldPosition worldPosition)
        {
            return ExpeditionMapCoordinateConverter.TryGridToWorldCenter(gridPosition, out worldPosition);
        }

        /// <summary>Converts expedition world coordinates to vanilla map-pixel coordinates when possible.</summary>
        public static bool TryWorldToMapPixels(
            ExpeditionMapWorldPosition worldPosition,
            out ExpeditionMapPixelPosition mapPosition)
        {
            return ExpeditionMapCoordinateConverter.TryWorldToMapPixels(worldPosition, out mapPosition);
        }

        /// <summary>Converts vanilla map-pixel coordinates to expedition world coordinates when possible.</summary>
        public static bool TryMapPixelsToWorld(
            ExpeditionMapPixelPosition mapPosition,
            out ExpeditionMapWorldPosition worldPosition)
        {
            return ExpeditionMapCoordinateConverter.TryMapPixelsToWorld(mapPosition, out worldPosition);
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
