using System;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.People
{
    /// <summary>
    /// One source of truth for vanilla strength/weakness conflicts. Validation,
    /// authoring pickers, and runtime sanitization all consume this policy.
    /// </summary>
    internal static class ScenarioSurvivorTraitConflictRules
    {
        public static bool ConflictsWithSelection(FamilyMemberConfig member, bool selectingStrength, object candidate)
        {
            if (member == null || member.Traits == null || candidate == null)
                return false;

            if (selectingStrength)
            {
                Traits.Weakness pairedWeakness;
                if (!TryGetPairedWeakness((Traits.Strength)candidate, out pairedWeakness))
                    return false;

                for (int i = 0; i < member.Traits.Count; i++)
                {
                    Traits.Weakness weakness;
                    if (TryParseWeakness(member.Traits[i], out weakness) && weakness == pairedWeakness)
                        return true;
                }

                return false;
            }

            Traits.Strength pairedStrength;
            if (!TryGetPairedStrength((Traits.Weakness)candidate, out pairedStrength))
                return false;

            for (int i = 0; i < member.Traits.Count; i++)
            {
                Traits.Strength strength;
                if (TryParseStrength(member.Traits[i], out strength) && strength == pairedStrength)
                    return true;
            }

            return false;
        }

        public static bool HasConflict(FamilyMemberConfig member, out Traits.Strength strength, out Traits.Weakness weakness)
        {
            strength = Traits.Strength.Max;
            weakness = Traits.Weakness.Max;
            if (member == null || member.Traits == null)
                return false;

            for (int i = 0; i < member.Traits.Count; i++)
            {
                Traits.Strength candidateStrength;
                if (!TryParseStrength(member.Traits[i], out candidateStrength))
                    continue;

                Traits.Weakness pairedWeakness;
                if (!TryGetPairedWeakness(candidateStrength, out pairedWeakness))
                    continue;

                for (int j = 0; j < member.Traits.Count; j++)
                {
                    Traits.Weakness candidateWeakness;
                    if (TryParseWeakness(member.Traits[j], out candidateWeakness) && candidateWeakness == pairedWeakness)
                    {
                        strength = candidateStrength;
                        weakness = candidateWeakness;
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TryParseStrength(string value, out Traits.Strength strength)
        {
            strength = Traits.Strength.Max;
            string trimmed = TrimPrefix(value, "Strength:");
            if (trimmed == null)
                return false;

            try
            {
                strength = (Traits.Strength)Enum.Parse(typeof(Traits.Strength), trimmed, true);
                return strength != Traits.Strength.Max;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryParseWeakness(string value, out Traits.Weakness weakness)
        {
            weakness = Traits.Weakness.Max;
            string trimmed = TrimPrefix(value, "Weakness:");
            if (trimmed == null)
                return false;

            try
            {
                weakness = (Traits.Weakness)Enum.Parse(typeof(Traits.Weakness), trimmed, true);
                return weakness != Traits.Weakness.Max;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetPairedWeakness(Traits.Strength strength, out Traits.Weakness weakness)
        {
            weakness = Traits.Weakness.Max;
            int index = (int)strength;
            if (strength == Traits.Strength.Max || index < 0 || index >= (int)Traits.Weakness.Max)
                return false;

            weakness = (Traits.Weakness)index;
            return true;
        }

        public static bool TryGetPairedStrength(Traits.Weakness weakness, out Traits.Strength strength)
        {
            strength = Traits.Strength.Max;
            int index = (int)weakness;
            if (weakness == Traits.Weakness.Max || index < 0 || index >= (int)Traits.Strength.Max)
                return false;

            strength = (Traits.Strength)index;
            return true;
        }

        private static string TrimPrefix(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(prefix.Length).Trim()
                : trimmed;
        }
    }
}
