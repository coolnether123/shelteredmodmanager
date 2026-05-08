using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.Compatibility
{
    internal sealed class ShelteredMultiplayerCompatibilityValidator
    {
        private readonly ShelteredMultiplayerCompatibilityHasher _hasher;

        public ShelteredMultiplayerCompatibilityValidator()
            : this(new ShelteredMultiplayerCompatibilityHasher())
        {
        }

        public ShelteredMultiplayerCompatibilityValidator(ShelteredMultiplayerCompatibilityHasher hasher)
        {
            _hasher = hasher ?? new ShelteredMultiplayerCompatibilityHasher();
        }

        public ShelteredMultiplayerCompatibilityValidationResult Validate(
            ShelteredMultiplayerCompatibilityInput host,
            ShelteredMultiplayerCompatibilityInput client,
            bool multiplayerActive)
        {
            if (!multiplayerActive)
                return new ShelteredMultiplayerCompatibilityValidationResult(true, new string[0]);

            ShelteredMultiplayerCompatibilityHash hostHash = _hasher.ComputeHash(host);
            ShelteredMultiplayerCompatibilityHash clientHash = _hasher.ComputeHash(client);
            List<string> mismatches = new List<string>();

            CompareField(mismatches, "SMM version", hostHash.Input.SmmVersion, clientHash.Input.SmmVersion);
            CompareField(mismatches, "ModAPI version", hostHash.Input.ModApiVersion, clientHash.Input.ModApiVersion);
            CompareField(mismatches, "ShelteredAPI version", hostHash.Input.ShelteredApiVersion, clientHash.Input.ShelteredApiVersion);
            CompareField(mismatches, "ModAPI.Networking version", hostHash.Input.ModApiNetworkingVersion, clientHash.Input.ModApiNetworkingVersion);
            CompareField(mismatches, "Protocol version", hostHash.Input.ProtocolVersion, clientHash.Input.ProtocolVersion);
            CompareField(mismatches, "Scenario id", hostHash.Input.ScenarioId, clientHash.Input.ScenarioId);
            CompareField(mismatches, "Scenario storage id", hostHash.Input.ScenarioStorageId, clientHash.Input.ScenarioStorageId);
            CompareMods(mismatches, hostHash.Input.EnabledMods, clientHash.Input.EnabledMods);
            CompareStringList(mismatches, "Custom content id", hostHash.Input.CustomContentIds, clientHash.Input.CustomContentIds);
            CompareStringList(mismatches, "Custom recipe id", hostHash.Input.CustomRecipeIds, clientHash.Input.CustomRecipeIds);

            if (mismatches.Count == 0
                && !string.Equals(hostHash.Hash, clientHash.Hash, StringComparison.Ordinal))
            {
                mismatches.Add("Compatibility hash differs: host=" + hostHash.Hash + ", client=" + clientHash.Hash + ".");
            }

            return new ShelteredMultiplayerCompatibilityValidationResult(mismatches.Count == 0, mismatches.ToArray());
        }

        public ShelteredMultiplayerCompatibilityValidationResult ValidateHashes(
            string hostHash,
            string clientHash,
            bool multiplayerActive)
        {
            if (!multiplayerActive)
                return new ShelteredMultiplayerCompatibilityValidationResult(true, new string[0]);

            if (string.Equals(Normalize(hostHash), Normalize(clientHash), StringComparison.Ordinal))
                return new ShelteredMultiplayerCompatibilityValidationResult(true, new string[0]);

            return new ShelteredMultiplayerCompatibilityValidationResult(
                false,
                new string[] { "Compatibility hash differs: host=" + Normalize(hostHash) + ", client=" + Normalize(clientHash) + "." });
        }

        private static void CompareMods(
            List<string> mismatches,
            List<ShelteredMultiplayerCompatibilityMod> hostMods,
            List<ShelteredMultiplayerCompatibilityMod> clientMods)
        {
            Dictionary<string, ShelteredMultiplayerCompatibilityMod> hostById = ToModMap(hostMods);
            Dictionary<string, ShelteredMultiplayerCompatibilityMod> clientById = ToModMap(clientMods);

            foreach (KeyValuePair<string, ShelteredMultiplayerCompatibilityMod> pair in hostById)
            {
                ShelteredMultiplayerCompatibilityMod client;
                if (!clientById.TryGetValue(pair.Key, out client))
                {
                    mismatches.Add("Client is missing gameplay mod '" + pair.Key + "'.");
                    continue;
                }

                ShelteredMultiplayerCompatibilityMod host = pair.Value;
                CompareField(mismatches, "Mod '" + pair.Key + "' version", host.Version, client.Version);
                CompareField(mismatches, "Mod '" + pair.Key + "' requiredModApiVersion",
                    host.RequiredModApiVersion, client.RequiredModApiVersion);
                CompareField(mismatches, "Mod '" + pair.Key + "' requiredShelteredApiVersion",
                    host.RequiredShelteredApiVersion, client.RequiredShelteredApiVersion);
            }

            foreach (KeyValuePair<string, ShelteredMultiplayerCompatibilityMod> pair in clientById)
            {
                if (!hostById.ContainsKey(pair.Key))
                    mismatches.Add("Client has extra gameplay mod '" + pair.Key + "'.");
            }
        }

        private static Dictionary<string, ShelteredMultiplayerCompatibilityMod> ToModMap(
            List<ShelteredMultiplayerCompatibilityMod> mods)
        {
            Dictionary<string, ShelteredMultiplayerCompatibilityMod> map =
                new Dictionary<string, ShelteredMultiplayerCompatibilityMod>(StringComparer.OrdinalIgnoreCase);
            if (mods == null)
                return map;

            for (int i = 0; i < mods.Count; i++)
            {
                ShelteredMultiplayerCompatibilityMod mod = mods[i];
                if (mod != null && Normalize(mod.ModId).Length > 0 && !mod.NonGameplayAffecting)
                    map[Normalize(mod.ModId)] = mod;
            }

            return map;
        }

        private static void CompareStringList(List<string> mismatches, string label, List<string> host, List<string> client)
        {
            Dictionary<string, bool> hostMap = ToStringMap(host);
            Dictionary<string, bool> clientMap = ToStringMap(client);

            foreach (string value in hostMap.Keys)
            {
                if (!clientMap.ContainsKey(value))
                    mismatches.Add("Client is missing " + label + " '" + value + "'.");
            }

            foreach (string value in clientMap.Keys)
            {
                if (!hostMap.ContainsKey(value))
                    mismatches.Add("Client has extra " + label + " '" + value + "'.");
            }
        }

        private static Dictionary<string, bool> ToStringMap(List<string> values)
        {
            Dictionary<string, bool> map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
                return map;

            for (int i = 0; i < values.Count; i++)
            {
                string value = Normalize(values[i]);
                if (value.Length > 0)
                    map[value] = true;
            }

            return map;
        }

        private static void CompareField(List<string> mismatches, string label, string host, string client)
        {
            string hostValue = Normalize(host);
            string clientValue = Normalize(client);
            if (string.Equals(hostValue, clientValue, StringComparison.Ordinal))
                return;

            mismatches.Add(label + " mismatch: host='" + hostValue + "', client='" + clientValue + "'.");
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
