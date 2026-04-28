using System;
using System.Collections.Generic;
using ModAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSelectionCatalogService : IScenarioSelectionCatalogService
    {
        private readonly IShelteredCustomScenarioService _customScenarios;
        private readonly IScenarioSaveLibrary _saveLibrary;

        public ScenarioSelectionCatalogService(
            IShelteredCustomScenarioService customScenarios,
            IScenarioSaveLibrary saveLibrary)
        {
            _customScenarios = customScenarios;
            _saveLibrary = saveLibrary;
        }

        public void Refresh()
        {
            ScenarioSelectionIds.RegisterVanillaDescriptors();
            _customScenarios.RefreshDefinitionCatalog();
        }

        public ScenarioCatalogEntry[] ListAll()
        {
            Refresh();

            List<ScenarioCatalogEntry> entries = new List<ScenarioCatalogEntry>();
            AddVanillaEntries(entries);
            AddModdedEntries(entries);
            entries.Sort(CompareEntries);
            return entries.ToArray();
        }

        public ScenarioCatalogEntry[] ListBySource(ScenarioCatalogSource source)
        {
            ScenarioCatalogEntry[] all = ListAll();
            List<ScenarioCatalogEntry> filtered = new List<ScenarioCatalogEntry>();
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry entry = all[i];
                if (entry != null && entry.Source == source)
                    filtered.Add(entry);
            }

            return filtered.ToArray();
        }

        public bool TryGet(string scenarioId, out ScenarioCatalogEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            ScenarioCatalogEntry[] all = ListAll();
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry candidate = all[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.ScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.StorageScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        private void AddVanillaEntries(List<ScenarioCatalogEntry> entries)
        {
            entries.Add(CreateVanillaEntry(
                ScenarioSelectionIds.VanillaStandardScenarioId,
                ScenarioSelectionIds.StandardStorageScenarioId,
                ScenarioLaunchMode.Survival,
                "Survival",
                "Standard Sheltered survival game.",
                0));

            entries.Add(CreateVanillaEntry(
                ScenarioSelectionIds.VanillaSurroundedScenarioId,
                ScenarioSelectionIds.VanillaSurroundedScenarioId,
                ScenarioLaunchMode.Surrounded,
                "Surrounded",
                "Vanilla Surrounded scenario.",
                1));

            entries.Add(CreateVanillaEntry(
                ScenarioSelectionIds.VanillaStasisScenarioId,
                ScenarioSelectionIds.VanillaStasisScenarioId,
                ScenarioLaunchMode.Stasis,
                "Stasis",
                "Vanilla Stasis scenario.",
                2));
        }

        private ScenarioCatalogEntry CreateVanillaEntry(
            string scenarioId,
            string storageScenarioId,
            ScenarioLaunchMode launchMode,
            string displayName,
            string description,
            int order)
        {
            return new ScenarioCatalogEntry
            {
                ScenarioId = scenarioId,
                StorageScenarioId = storageScenarioId,
                Source = ScenarioCatalogSource.Vanilla,
                LaunchMode = launchMode,
                BaseGameMode = ScenarioSelectionIds.GetBaseGameMode(scenarioId),
                DefaultSaveType = ScenarioSelectionIds.GetDefaultSaveType(scenarioId),
                DisplayName = displayName,
                Description = description,
                Version = "1.0",
                Order = order,
                SaveCount = _saveLibrary.CountSaves(storageScenarioId),
                CanStart = true,
                DependencyState = ScenarioDependencyVerificationState.Match
            };
        }

        private void AddModdedEntries(List<ScenarioCatalogEntry> entries)
        {
            CustomScenarioInfo[] scenarios = _customScenarios.List();
            for (int i = 0; i < scenarios.Length; i++)
            {
                CustomScenarioInfo scenario = scenarios[i];
                if (scenario == null || string.IsNullOrEmpty(scenario.Id))
                    continue;

                SlotManifest manifest = _customScenarios.CreateDependencyManifest(scenario);
                ScenarioDependencyVerificationState dependencyState = _customScenarios.VerifyDependencies(scenario);
                entries.Add(new ScenarioCatalogEntry
                {
                    ScenarioId = scenario.Id,
                    StorageScenarioId = _saveLibrary.ToStorageScenarioId(scenario.Id),
                    Source = ScenarioCatalogSource.Modded,
                    LaunchMode = ScenarioLaunchMode.CustomDefinition,
                    BaseGameMode = ScenarioBaseGameMode.Survival,
                    DefaultSaveType = SaveManager.SaveType.Slot1,
                    DisplayName = scenario.DisplayName,
                    Description = scenario.Description,
                    Version = scenario.Version,
                    OwnerModId = scenario.OwnerModId,
                    Order = scenario.Order,
                    SaveCount = _saveLibrary.CountSaves(scenario.Id),
                    CanStart = dependencyState == ScenarioDependencyVerificationState.Match,
                    DependencyState = dependencyState,
                    DependencyManifest = manifest,
                    CustomScenario = scenario
                });
            }
        }

        private static int CompareEntries(ScenarioCatalogEntry left, ScenarioCatalogEntry right)
        {
            if (object.ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int source = left.Source.CompareTo(right.Source);
            if (source != 0) return source;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0) return order;

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0) return name;

            return string.Compare(left.ScenarioId, right.ScenarioId, StringComparison.OrdinalIgnoreCase);
        }

    }
}
