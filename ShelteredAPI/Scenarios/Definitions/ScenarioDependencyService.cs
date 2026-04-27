using System;
using ModAPI.Core;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
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
                return ScenarioDependencyManifest.Create("Custom Scenario", new LoadedModInfo[0]);

            LoadedModInfo[] required = ScenarioDependencyManifest.Merge(
                info.RequiredMods,
                LoadDefinitionDependencies(info.Id));

            return ScenarioDependencyManifest.Create(info.DisplayName, required);
        }

        public ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info)
        {
            return MapVerificationState(SaveVerification.VerifyRequired(CreateDependencyManifest(info)));
        }

        public LoadedModInfo[] LoadDefinitionDependencies(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return new LoadedModInfo[0];

            try
            {
                ScenarioInfo info;
                if (!TryGetDefinitionInfo(scenarioId, out info) || info == null)
                    return new LoadedModInfo[0];

                ScenarioDefinition definition = _definitionSerializer.Load(info.FilePath);
                return ScenarioDependencyManifest.FromDependencyStrings(definition.Dependencies);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioService] Failed to load dependency manifest for '" + scenarioId + "': " + ex.Message);
                return new LoadedModInfo[0];
            }
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
