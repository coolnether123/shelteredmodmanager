using System.Collections.Generic;
using ShelteredAPI.Networking.Compatibility;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredCompatibilityHashTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("CompatibilityHash_IsStableAcrossInputOrder", HashIsStableAcrossInputOrder));
            tests.Add(new TestCase("CompatibilityHash_IgnoresExplicitUiOnlyMods", HashIgnoresExplicitUiOnlyMods));
            tests.Add(new TestCase("CompatibilityValidator_ReportsReadableModMismatch", ValidatorReportsReadableModMismatch));
            tests.Add(new TestCase("CompatibilityValidator_DoesNotBlockSinglePlayer", ValidatorDoesNotBlockSinglePlayer));
        }

        private static void HashIsStableAcrossInputOrder()
        {
            ShelteredMultiplayerCompatibilityHasher hasher = new ShelteredMultiplayerCompatibilityHasher();
            ShelteredMultiplayerCompatibilityInput first = CreateBaseInput();
            first.EnabledMods.Add(CreateMod("b.mod", "1.0", false));
            first.EnabledMods.Add(CreateMod("a.mod", "1.0", false));
            first.CustomContentIds.Add("z.content");
            first.CustomContentIds.Add("a.content");

            ShelteredMultiplayerCompatibilityInput second = CreateBaseInput();
            second.EnabledMods.Add(CreateMod("a.mod", "1.0", false));
            second.EnabledMods.Add(CreateMod("b.mod", "1.0", false));
            second.CustomContentIds.Add("a.content");
            second.CustomContentIds.Add("z.content");

            TestAssert.Equal(hasher.ComputeHash(first).Hash, hasher.ComputeHash(second).Hash,
                "Compatibility hash should sort mods and content ids before hashing.");
        }

        private static void HashIgnoresExplicitUiOnlyMods()
        {
            ShelteredMultiplayerCompatibilityHasher hasher = new ShelteredMultiplayerCompatibilityHasher();
            ShelteredMultiplayerCompatibilityInput host = CreateBaseInput();
            ShelteredMultiplayerCompatibilityInput client = CreateBaseInput();
            client.EnabledMods.Add(CreateMod("ui.pretty-panel", "9.0", true));

            TestAssert.Equal(hasher.ComputeHash(host).Hash, hasher.ComputeHash(client).Hash,
                "Explicit non-gameplay-affecting mods should not alter the compatibility hash.");
        }

        private static void ValidatorReportsReadableModMismatch()
        {
            ShelteredMultiplayerCompatibilityValidator validator = new ShelteredMultiplayerCompatibilityValidator();
            ShelteredMultiplayerCompatibilityInput host = CreateBaseInput();
            ShelteredMultiplayerCompatibilityInput client = CreateBaseInput();
            host.EnabledMods.Add(CreateMod("gameplay.balance", "1.0", false));
            client.EnabledMods.Add(CreateMod("gameplay.balance", "2.0", false));

            ShelteredMultiplayerCompatibilityValidationResult result = validator.Validate(host, client, true);

            TestAssert.False(result.Compatible, "Different gameplay mod versions should be incompatible.");
            TestAssert.True(result.Message.IndexOf("gameplay.balance") >= 0,
                "Mismatch message should name the incompatible mod.");
        }

        private static void ValidatorDoesNotBlockSinglePlayer()
        {
            ShelteredMultiplayerCompatibilityValidator validator = new ShelteredMultiplayerCompatibilityValidator();
            ShelteredMultiplayerCompatibilityInput host = CreateBaseInput();
            ShelteredMultiplayerCompatibilityInput client = CreateBaseInput();
            client.ProtocolVersion = "different";

            ShelteredMultiplayerCompatibilityValidationResult result = validator.Validate(host, client, false);

            TestAssert.True(result.Compatible, "Compatibility validation should not block single-player.");
            TestAssert.Equal(0, result.Mismatches.Length, "Single-player validation should not emit mismatch failures.");
        }

        private static ShelteredMultiplayerCompatibilityInput CreateBaseInput()
        {
            ShelteredMultiplayerCompatibilityInput input = new ShelteredMultiplayerCompatibilityInput();
            input.SmmVersion = "1.4.0";
            input.ModApiVersion = "1.3.0";
            input.ShelteredApiVersion = "1.3.0";
            input.ModApiNetworkingVersion = "1.4.0";
            input.ProtocolVersion = "1";
            input.ScenarioId = "scenario.default";
            input.ScenarioStorageId = "storage.default";
            return input;
        }

        private static ShelteredMultiplayerCompatibilityMod CreateMod(string id, string version, bool nonGameplay)
        {
            ShelteredMultiplayerCompatibilityMod mod = new ShelteredMultiplayerCompatibilityMod();
            mod.ModId = id;
            mod.Version = version;
            mod.RequiredModApiVersion = "1.3.0";
            mod.RequiredShelteredApiVersion = "1.3.0";
            mod.NonGameplayAffecting = nonGameplay;
            return mod;
        }
    }
}
