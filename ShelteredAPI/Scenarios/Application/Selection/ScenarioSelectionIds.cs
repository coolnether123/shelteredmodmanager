using System;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Selection{
    internal static class ScenarioSelectionIds
    {
        public const string StandardStorageScenarioId = "Standard";
        public const string VanillaStandardScenarioId = "Vanilla.Standard";
        public const string VanillaSurroundedScenarioId = "Vanilla.Surrounded";
        public const string VanillaStasisScenarioId = "Vanilla.Stasis";

        public static bool IsReservedStorageId(string scenarioId)
        {
            return ScenarioSaveIdGuards.IsReservedStorageId(scenarioId);
        }

        public static bool IsStandardScenario(string scenarioId)
        {
            return string.Equals(scenarioId, ScenarioSaveIdGuards.StandardStorageScenarioId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scenarioId, ScenarioSaveIdGuards.VanillaStandardScenarioId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVanillaScenario(string scenarioId)
        {
            return IsStandardScenario(scenarioId)
                || string.Equals(scenarioId, VanillaSurroundedScenarioId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scenarioId, VanillaStasisScenarioId, StringComparison.OrdinalIgnoreCase);
        }

        public static string ToStorageScenarioId(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return StandardStorageScenarioId;

            return IsStandardScenario(scenarioId) ? StandardStorageScenarioId : scenarioId;
        }

        public static string ToCatalogScenarioId(string scenarioId)
        {
            if (IsStandardScenario(scenarioId))
                return VanillaStandardScenarioId;

            return string.IsNullOrEmpty(scenarioId) ? VanillaStandardScenarioId : scenarioId;
        }

        public static SaveManager.SaveType GetDefaultSaveType(string scenarioId)
        {
            if (string.Equals(scenarioId, VanillaSurroundedScenarioId, StringComparison.OrdinalIgnoreCase))
                return SaveManager.SaveType.SlotSurrounded;

            if (string.Equals(scenarioId, VanillaStasisScenarioId, StringComparison.OrdinalIgnoreCase))
                return SaveManager.SaveType.SlotStasis;

            return SaveManager.SaveType.Slot1;
        }

        public static SaveManager.SaveType GetDefaultSaveType(ScenarioBaseGameMode baseGameMode)
        {
            if (baseGameMode == ScenarioBaseGameMode.Surrounded)
                return SaveManager.SaveType.SlotSurrounded;

            if (baseGameMode == ScenarioBaseGameMode.Stasis)
                return SaveManager.SaveType.SlotStasis;

            return SaveManager.SaveType.Slot1;
        }

        public static SaveManager.SaveType GetCustomScenarioTransportSaveType()
        {
            return SaveManager.SaveType.Slot1;
        }

        public static ScenarioBaseGameMode GetBaseGameMode(string scenarioId)
        {
            if (string.Equals(scenarioId, VanillaSurroundedScenarioId, StringComparison.OrdinalIgnoreCase))
                return ScenarioBaseGameMode.Surrounded;

            if (string.Equals(scenarioId, VanillaStasisScenarioId, StringComparison.OrdinalIgnoreCase))
                return ScenarioBaseGameMode.Stasis;

            return ScenarioBaseGameMode.Survival;
        }

        public static void RegisterVanillaDescriptors()
        {
            RegisterDescriptor(StandardStorageScenarioId, "Survival", "Standard Sheltered survival game.", "1.0");
            RegisterDescriptor(VanillaStandardScenarioId, "Survival", "Standard Sheltered survival game.", "1.0");
            RegisterDescriptor(VanillaSurroundedScenarioId, "Surrounded", "Vanilla Surrounded scenario.", "1.0");
            RegisterDescriptor(VanillaStasisScenarioId, "Stasis", "Vanilla Stasis scenario.", "1.0");
        }

        private static void RegisterDescriptor(string id, string displayName, string description, string version)
        {
            ScenarioRegistry.RegisterScenario(new ScenarioDescriptor
            {
                id = id,
                displayName = displayName,
                description = description,
                version = version
            });
        }
    }
}
