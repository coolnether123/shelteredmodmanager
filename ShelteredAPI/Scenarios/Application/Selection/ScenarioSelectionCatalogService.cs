using System;
using System.Collections.Generic;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSelectionCatalogService : IScenarioSelectionCatalogService
    {
        private readonly ICustomScenarioRegistry _customScenarios;
        private readonly IScenarioDefinitionCatalogService _definitions;
        private readonly IScenarioDependencyVerifier _dependencies;
        private readonly IScenarioSaveLibrary _saveLibrary;
        private readonly IScenarioDefinitionSerializer _definitionSerializer;

        public ScenarioSelectionCatalogService(
            ICustomScenarioRegistry customScenarios,
            IScenarioDefinitionCatalogService definitions,
            IScenarioDependencyVerifier dependencies,
            IScenarioSaveLibrary saveLibrary,
            IScenarioDefinitionSerializer definitionSerializer)
        {
            _customScenarios = customScenarios;
            _definitions = definitions;
            _dependencies = dependencies;
            _saveLibrary = saveLibrary;
            _definitionSerializer = definitionSerializer;
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
            AddDraftEntries(entries);
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

                if (candidate.Source == ScenarioCatalogSource.Draft)
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

                SlotManifest manifest = _dependencies.CreateDependencyManifest(scenario);
                ScenarioDependencyVerificationState dependencyState = _dependencies.VerifyDependencies(scenario);
                ScenarioBaseGameMode baseGameMode = ResolveBaseGameMode(scenario);
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

        // Drafts live in a dedicated storage scenario id (ScenarioAuthoringDrafts)
        // so their saves are physically separated from published modded scenarios.
        // Surfacing them as ScenarioCatalogSource.Draft keeps the browser's
        // Modded tab to published mods only.
        private void AddDraftEntries(List<ScenarioCatalogEntry> entries)
        {
            ScenarioAuthoringDraftRepository repo;
            try { repo = ScenarioAuthoringDraftRepository.Instance; }
            catch { return; }

            if (repo == null)
                return;

            ScenarioInfo[] drafts;
            try { drafts = repo.ListAll(); }
            catch (System.Exception ex)
            {
                ModAPI.Core.MMLog.WriteWarning("[ScenarioSelectionCatalogService] Draft enumeration failed: " + ex.Message);
                return;
            }

            int order = 1000;
            for (int i = 0; i < drafts.Length; i++)
            {
                ScenarioInfo draft = drafts[i];
                if (draft == null || string.IsNullOrEmpty(draft.Id))
                    continue;

                ScenarioBaseGameMode baseGameMode = ResolveDraftBaseGameMode(draft);
                entries.Add(new ScenarioCatalogEntry
                {
                    ScenarioId = draft.Id,
                    StorageScenarioId = ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    Source = ScenarioCatalogSource.Draft,
                    LaunchMode = ScenarioLaunchMode.AuthoringDraft,
                    BaseGameMode = baseGameMode,
                    DefaultSaveType = ScenarioSelectionIds.GetDefaultSaveType(baseGameMode),
                    DisplayName = string.IsNullOrEmpty(draft.DisplayName) ? draft.Id : draft.DisplayName,
                    Description = "Authoring draft. Open the editor to continue working on this scenario.",
                    Version = draft.Version,
                    OwnerModId = draft.OwnerModId,
                    Order = order + i,
                    SaveCount = _saveLibrary.CountSaves(ScenarioAuthoringDraftRepository.DraftStorageScenarioId),
                    CanStart = true,
                    DependencyState = ScenarioDependencyVerificationState.Match
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

        private ScenarioBaseGameMode ResolveDraftBaseGameMode(ScenarioInfo draft)
        {
            if (draft == null || string.IsNullOrEmpty(draft.FilePath))
                return ScenarioBaseGameMode.Survival;

            try
            {
                ScenarioDefinition definition = _definitionSerializer.Load(draft.FilePath);
                if (definition != null && Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode))
                    return definition.BaseGameMode;
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
