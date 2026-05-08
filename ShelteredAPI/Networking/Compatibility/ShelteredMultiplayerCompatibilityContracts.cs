using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.Compatibility
{
    internal sealed class ShelteredMultiplayerCompatibilityInput
    {
        public ShelteredMultiplayerCompatibilityInput()
        {
            SmmVersion = string.Empty;
            ModApiVersion = string.Empty;
            ShelteredApiVersion = string.Empty;
            ModApiNetworkingVersion = string.Empty;
            ProtocolVersion = string.Empty;
            ScenarioId = string.Empty;
            ScenarioStorageId = string.Empty;
            EnabledMods = new List<ShelteredMultiplayerCompatibilityMod>();
            CustomContentIds = new List<string>();
            CustomRecipeIds = new List<string>();
        }

        public string SmmVersion;
        public string ModApiVersion;
        public string ShelteredApiVersion;
        public string ModApiNetworkingVersion;
        public string ProtocolVersion;
        public string ScenarioId;
        public string ScenarioStorageId;
        public List<ShelteredMultiplayerCompatibilityMod> EnabledMods;
        public List<string> CustomContentIds;
        public List<string> CustomRecipeIds;
    }

    internal sealed class ShelteredMultiplayerCompatibilityMod
    {
        public ShelteredMultiplayerCompatibilityMod()
        {
            ModId = string.Empty;
            Version = string.Empty;
            RequiredModApiVersion = string.Empty;
            RequiredShelteredApiVersion = string.Empty;
        }

        public string ModId;
        public string Version;
        public string RequiredModApiVersion;
        public string RequiredShelteredApiVersion;
        public bool NonGameplayAffecting;

        public ShelteredMultiplayerCompatibilityMod Copy()
        {
            return new ShelteredMultiplayerCompatibilityMod
            {
                ModId = ModId ?? string.Empty,
                Version = Version ?? string.Empty,
                RequiredModApiVersion = RequiredModApiVersion ?? string.Empty,
                RequiredShelteredApiVersion = RequiredShelteredApiVersion ?? string.Empty,
                NonGameplayAffecting = NonGameplayAffecting
            };
        }
    }

    internal sealed class ShelteredMultiplayerCompatibilityHash
    {
        public ShelteredMultiplayerCompatibilityHash(
            string hash,
            string normalizedManifest,
            ShelteredMultiplayerCompatibilityInput input)
        {
            Hash = hash ?? string.Empty;
            NormalizedManifest = normalizedManifest ?? string.Empty;
            Input = input;
        }

        public readonly string Hash;
        public readonly string NormalizedManifest;
        public readonly ShelteredMultiplayerCompatibilityInput Input;
    }

    internal sealed class ShelteredMultiplayerCompatibilityValidationResult
    {
        public ShelteredMultiplayerCompatibilityValidationResult(bool compatible, string[] mismatches)
        {
            Compatible = compatible;
            Mismatches = mismatches ?? new string[0];
        }

        public readonly bool Compatible;
        public readonly string[] Mismatches;

        public string Message
        {
            get { return Mismatches.Length == 0 ? string.Empty : string.Join("; ", Mismatches); }
        }
    }
}
