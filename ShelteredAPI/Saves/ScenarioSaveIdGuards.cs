using System;
using System.Collections.Generic;

namespace ShelteredAPI.Saves
{
    internal static class ScenarioSaveIdGuards
    {
        public const string StandardStorageScenarioId = "Standard";
        public const string VanillaStandardScenarioId = "Vanilla.Standard";
        public const string VanillaSurroundedScenarioId = "Vanilla.Surrounded";
        public const string VanillaStasisScenarioId = "Vanilla.Stasis";
        public const string VanillaSurroundedStorageScenarioId = "Surrounded";
        public const string VanillaStasisStorageScenarioId = "Stasis";
        public const string ScenarioAuthoringDraftStorageScenarioId = "ScenarioAuthoringDrafts";

        private static readonly HashSet<string> ReservedStorageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            StandardStorageScenarioId,
            VanillaStandardScenarioId,
            VanillaSurroundedScenarioId,
            VanillaStasisScenarioId,
            VanillaSurroundedStorageScenarioId,
            VanillaStasisStorageScenarioId,
            ScenarioAuthoringDraftStorageScenarioId
        };

        public static bool IsReservedStorageId(string scenarioId)
        {
            string normalized = Normalize(scenarioId);
            return normalized.Length > 0 && ReservedStorageIds.Contains(normalized);
        }

        public static string RequireCustomScenarioId(string scenarioId, string apiName)
        {
            string normalized = Normalize(scenarioId);
            if (normalized.Length == 0)
                throw new ArgumentException("Scenario id is required for the custom scenario save API.", "scenarioId");

            if (ReservedStorageIds.Contains(normalized))
            {
                throw new ArgumentException(
                    "Scenario id '" + normalized + "' is reserved for built-in saves. Use the explicit standard-save API instead of the custom scenario API.",
                    "scenarioId");
            }

            return normalized;
        }

        private static string Normalize(string scenarioId)
        {
            return (scenarioId ?? string.Empty).Trim();
        }
    }
}
