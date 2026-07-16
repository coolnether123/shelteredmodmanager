using System;
using System.Globalization;
using ModAPI.Core;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Harmony;

namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal static class ScenarioSeedPolicy
    {
        public static bool TryApplyForScenario(ScenarioDefinition definition, string reason, out string message)
        {
            message = null;
            if (definition == null || !definition.SeedOverride.HasValue)
            {
                // Random-seed scenarios deliberately retain Unity's shared RNG state.
                ModRandomBridge.SetScenarioFixedSeedActive(false);
                return false;
            }

            long rawSeed = definition.SeedOverride.Value;
            if (rawSeed < int.MinValue || rawSeed > int.MaxValue)
            {
                message = "Fixed scenario seed is outside the supported Int32 range; ModRandom seed was unchanged.";
                MMLog.WriteWarning("[ScenarioSeedPolicy] Refused fixed seed " + rawSeed.ToString(CultureInfo.InvariantCulture)
                    + " for scenario '" + (definition.Id ?? string.Empty) + "' reason=" + (reason ?? string.Empty) + ".");
                return false;
            }

            int seed = (int)rawSeed;
            // The redirect batch is expensive to install and has no effect while the bridge is
            // in Unity pass-through mode. Install it at the one policy boundary that can enable
            // fixed scenario RNG, before the gate or seed is changed, so map generation remains
            // deterministic without charging every vanilla/random-seed startup for 175 patches.
            ScenarioRngPatches.Install();
            ModRandom.ResetForSaveSeed(seed);
            ModRandomBridge.SetScenarioFixedSeedActive(true);
            message = "Fixed scenario seed applied to ModRandom: " + seed.ToString(CultureInfo.InvariantCulture) + ".";
            MMLog.WriteInfo("[ScenarioSeedPolicy] Applied fixed ModRandom seed " + seed.ToString(CultureInfo.InvariantCulture)
                + " for scenario '" + (definition.Id ?? string.Empty) + "' reason=" + (reason ?? string.Empty) + ".");
            return true;
        }
    }
}
