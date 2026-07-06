using System;
using System.Globalization;
using ModAPI.Core;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal static class ScenarioSeedPolicy
    {
        public static bool TryApplyForScenario(ScenarioDefinition definition, string reason, out string message)
        {
            message = null;
            if (definition == null || !definition.SeedOverride.HasValue)
                return false;

            long rawSeed = definition.SeedOverride.Value;
            if (rawSeed < int.MinValue || rawSeed > int.MaxValue)
            {
                message = "Fixed scenario seed is outside the supported Int32 range; ModRandom seed was unchanged.";
                MMLog.WriteWarning("[ScenarioSeedPolicy] Refused fixed seed " + rawSeed.ToString(CultureInfo.InvariantCulture)
                    + " for scenario '" + (definition.Id ?? string.Empty) + "' reason=" + (reason ?? string.Empty) + ".");
                return false;
            }

            int seed = (int)rawSeed;
            ModRandom.ResetForSaveSeed(seed);
            message = "Fixed scenario seed applied to ModRandom: " + seed.ToString(CultureInfo.InvariantCulture) + ".";
            MMLog.WriteInfo("[ScenarioSeedPolicy] Applied fixed ModRandom seed " + seed.ToString(CultureInfo.InvariantCulture)
                + " for scenario '" + (definition.Id ?? string.Empty) + "' reason=" + (reason ?? string.Empty) + ".");
            return true;
        }
    }
}
