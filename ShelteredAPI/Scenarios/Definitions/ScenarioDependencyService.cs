using System;
using ModAPI.Core;
using ShelteredAPI.Saves.Paging;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioDependencyService
    {
        private readonly IScenarioDefinitionSerializer _definitionSerializer;
        private readonly IScenarioDefinitionCatalog _definitionCatalog;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;

        public ScenarioDependencyService(
            IScenarioDefinitionSerializer definitionSerializer,
            IScenarioDefinitionCatalog definitionCatalog,
            ScenarioAuthoringDraftRepository draftRepository)
        {
            _definitionSerializer = definitionSerializer;
            _definitionCatalog = definitionCatalog;
            _draftRepository = draftRepository;
        }

        public SlotManifest CreateDependencyManifest(CustomScenarioInfo info)
        {
            if (info == null)
                return ToSlotManifest(ScenarioDependencyManifest.Create("Custom Scenario", new ScenarioModDependency[0]));

            ScenarioModDependency[] required = ScenarioDependencyManifest.Merge(
                info.RequiredMods,
                LoadDefinitionDependencies(info.Id));

            return ToSlotManifest(ScenarioDependencyManifest.Create(info.DisplayName, required));
        }

        public ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info)
        {
            return MapVerificationState(SaveVerification.VerifyRequired(CreateDependencyManifest(info)));
        }

        public ScenarioModDependency[] LoadDefinitionDependencies(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return new ScenarioModDependency[0];

            try
            {
                ScenarioInfo info;
                if (!TryGetDefinitionInfo(scenarioId, out info) || info == null)
                    return new ScenarioModDependency[0];

                ScenarioDefinition definition = _definitionSerializer.Load(info.FilePath);
                return ScenarioDependencyManifest.FromDependencyStrings(definition.Dependencies);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioDependencyService] Failed to load dependency manifest for '" + scenarioId + "': " + ex.Message);
                return new ScenarioModDependency[0];
            }
        }

        private static SlotManifest ToSlotManifest(ScenarioDependencyManifestData manifest)
        {
            if (manifest == null)
                manifest = ScenarioDependencyManifest.Create("Custom Scenario", new ScenarioModDependency[0]);

            return new SlotManifest
            {
                family_name = manifest.name ?? string.Empty,
                lastModified = manifest.lastModified ?? DateTime.UtcNow.ToString("o"),
                lastLoadedMods = ToLoadedModInfo(manifest.requiredMods)
            };
        }

        private static LoadedModInfo[] ToLoadedModInfo(ScenarioModDependency[] dependencies)
        {
            if (dependencies == null || dependencies.Length == 0)
                return new LoadedModInfo[0];

            LoadedModInfo[] result = new LoadedModInfo[dependencies.Length];
            for (int i = 0; i < dependencies.Length; i++)
            {
                ScenarioModDependency dependency = dependencies[i];
                result[i] = new LoadedModInfo
                {
                    modId = dependency != null ? dependency.modId : null,
                    version = dependency != null ? dependency.version : null,
                    warnings = dependency != null && dependency.warnings != null ? (string[])dependency.warnings.Clone() : new string[0]
                };
            }

            return result;
        }

        private bool TryGetDefinitionInfo(string scenarioId, out ScenarioInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            if (_definitionCatalog.TryGet(scenarioId, out info) && info != null)
                return true;

            return _draftRepository.TryGet(scenarioId, out info) && info != null;
        }

        private static ScenarioDependencyVerificationState MapVerificationState(SaveVerification.VerificationState state)
        {
            switch (state)
            {
                case SaveVerification.VerificationState.Match:
                    return ScenarioDependencyVerificationState.Match;
                case SaveVerification.VerificationState.VersionMismatch:
                    return ScenarioDependencyVerificationState.VersionMismatch;
                case SaveVerification.VerificationState.Warning:
                    return ScenarioDependencyVerificationState.Warning;
                case SaveVerification.VerificationState.Missing:
                    return ScenarioDependencyVerificationState.Missing;
                default:
                    return ScenarioDependencyVerificationState.Unknown;
            }
        }
    }
}
