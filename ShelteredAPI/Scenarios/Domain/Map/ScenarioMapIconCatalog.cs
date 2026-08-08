using System;

namespace ShelteredAPI.Scenarios.Domain.Map{
    internal static class ScenarioMapIconCatalog
    {
        private static readonly string[] KnownIconIds = new[]
        {
            "MapIcon_Shelter",
            "MapIcon_House",
            "MapIcon_SmallHouse",
            "MapIcon_MediumHouse",
            "MapIcon_LargeHouse",
            "MapIcon_Town",
            "MapIcon_City",
            "MapIcon_Church",
            "MapIcon_School",
            "MapIcon_Hospital",
            "MapIcon_PoliceStation",
            "MapIcon_Prison",
            "MapIcon_Supermarket",
            "MapIcon_PetrolStation",
            "MapIcon_RecyclingCentre",
            "MapIcon_Scrapyard",
            "MapIcon_LumberYard",
            "MapIcon_Reservoir",
            "MapIcon_Cave",
            "MapIcon_Mine",
            "MapIcon_CrashSite",
            "MapIcon_MysteryHatch",
            "MapIcon_Quest",
            "MapIcon_Unknown"
        };

        internal static string[] GetKnownIconIds()
        {
            string[] copy = new string[KnownIconIds.Length];
            Array.Copy(KnownIconIds, copy, KnownIconIds.Length);
            return copy;
        }

        internal static bool IsKnownIconId(string iconId)
        {
            if (string.IsNullOrEmpty(iconId))
                return true;

            for (int i = 0; i < KnownIconIds.Length; i++)
            {
                if (string.Equals(KnownIconIds[i], iconId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
