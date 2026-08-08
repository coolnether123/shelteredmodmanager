using System;
using System.Collections.Generic;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Selection{
    internal sealed class ScenarioSelectionCatalogService : IScenarioSelectionCatalogService
    {
        private readonly ICustomScenarioRegistry _customScenarios;
        private readonly IScenarioDefinitionCatalogService _definitions;
        private readonly IScenarioDependencyVerifier _dependencies;
        private readonly IScenarioSaveLibrary _saveLibrary;

        public ScenarioSelectionCatalogService(
            ICustomScenarioRegistry customScenarios,
            IScenarioDefinitionCatalogService definitions,
            IScenarioDependencyVerifier dependencies,
            IScenarioSaveLibrary saveLibrary)
        {
            _customScenarios = customScenarios;
            _definitions = definitions;
            _dependencies = dependencies;
            _saveLibrary = saveLibrary;
        }

        public void Refresh()
        {
            ScenarioSelectionIds.RegisterVanillaDescriptors();
            _definitions.RefreshDefinitionCatalog();
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

                if (string.Equals(candidate.ScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }

            ScenarioCatalogEntry storageMatch = null;
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry candidate = all[i];
                if (candidate == null)
                    continue;

                if (!string.Equals(candidate.StorageScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (storageMatch != null)
                    return false;

                storageMatch = candidate;
            }

            entry = storageMatch;
            return entry != null;
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
                ScenarioSelectionIds.VanillaSurroundedStorageScenarioId,
                ScenarioLaunchMode.Surrounded,
                "Surrounded",
                "Vanilla Surrounded scenario.",
                1));

            entries.Add(CreateVanillaEntry(
                ScenarioSelectionIds.VanillaStasisScenarioId,
                ScenarioSelectionIds.VanillaStasisStorageScenarioId,
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
            SaveEntry[] saves = _saveLibrary.ListSaves(storageScenarioId);
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
                SaveCount = saves != null ? saves.Length : 0,
                LastPlayedUtc = ScenarioLibraryMetadata.ReadLastPlayedUtc(saves),
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
                SlotManifest manifest = _dependencies.CreateDependencyManifest(scenario);
                ScenarioDependencyVerificationState dependencyState = _dependencies.VerifyDependencies(scenario);
                ScenarioBaseGameMode baseGameMode = ResolveBaseGameMode(scenario);
                string author = null;
                ScenarioDefinition loadedDefinition;
                string loadedScenarioPath;
                ScenarioValidationResult ignoredValidation;
                if (_definitions.TryLoadDefinition(scenario.Id, out loadedDefinition, out loadedScenarioPath, out ignoredValidation)
                    && loadedDefinition != null)
                {
                    author = loadedDefinition.Author;
                }
                SaveEntry[] saves = _saveLibrary.ListSaves(scenario.Id);
                entries.Add(new ScenarioCatalogEntry
                {
                    ScenarioId = scenario.Id,
                    StorageScenarioId = _saveLibrary.ToStorageScenarioId(scenario.Id),
                    Source = ScenarioCatalogSource.Modded,
                    LaunchMode = ScenarioLaunchMode.CustomDefinition,
                    BaseGameMode = baseGameMode,
                    DefaultSaveType = ScenarioSelectionIds.GetDefaultSaveType(baseGameMode),
                    DisplayName = scenario.DisplayName,
                    Description = scenario.Description,
                    Version = scenario.Version,
                    Author = author,
                    OwnerModId = scenario.OwnerModId,
                    Order = scenario.Order,
                    SaveCount = saves != null ? saves.Length : 0,
                    InstalledUtc = ScenarioLibraryMetadata.ReadInstalledUtc(loadedScenarioPath),
                    CreatedUtc = ScenarioLibraryMetadata.ReadScenarioCreatedUtc(loadedScenarioPath),
                    LastPlayedUtc = ScenarioLibraryMetadata.ReadLastPlayedUtc(saves),
                    CanStart = dependencyState == ScenarioDependencyVerificationState.Match,
                    DependencyState = dependencyState,
                    DependencyManifest = manifest,
                    CustomScenario = scenario
                });
            }
        }

        private ScenarioBaseGameMode ResolveBaseGameMode(CustomScenarioInfo scenario)
        {
            if (scenario == null || string.IsNullOrEmpty(scenario.Id))
                return ScenarioBaseGameMode.Survival;

            ScenarioDefinition definition;
            string scenarioFilePath;
            ScenarioValidationResult validation;
            try
            {
                if (_definitions.TryLoadDefinition(scenario.Id, out definition, out scenarioFilePath, out validation)
                    && definition != null
                    && Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode))
                {
                    return definition.BaseGameMode;
                }
            }
            catch
            {
            }

            return ScenarioBaseGameMode.Survival;
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
