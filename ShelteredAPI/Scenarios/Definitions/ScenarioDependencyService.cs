using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Saves.Paging;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Domain.Compatibility;
namespace ShelteredAPI.Scenarios.Definitions{
    internal sealed class ScenarioDependencyService : IScenarioDependencyVerifier, IScenarioDefinitionDependencyReader
    {
        private readonly IScenarioDefinitionReader _definitionReader;

        public ScenarioDependencyService(IScenarioDefinitionReader definitionReader)
        {
            _definitionReader = definitionReader;
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
                ScenarioDefinition definition;
                string scenarioFilePath;
                string errorMessage;
                if (!_definitionReader.TryLoadUnchecked(scenarioId, out definition, out scenarioFilePath, out errorMessage) || definition == null)
                {
                    if (!string.IsNullOrEmpty(errorMessage) && !errorMessage.StartsWith("Scenario is not indexed"))
                        MMLog.WriteWarning("[ScenarioDependencyService] Failed to load dependency manifest for '" + scenarioId + "': " + errorMessage);
                    return new ScenarioModDependency[0];
                }

                return ScenarioDependencyManifest.Merge(
                    ScenarioDependencyManifest.FromDependencyStrings(definition.Dependencies),
                    FromRequiredModDependencies(definition.ModDependencies));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioDependencyService] Failed to load dependency manifest for '" + scenarioId + "': " + ex.Message);
                return new ScenarioModDependency[0];
            }
        }

        private static ScenarioModDependency[] FromRequiredModDependencies(IList<ScenarioModDependencyDefinition> dependencies)
        {
            if (dependencies == null || dependencies.Count == 0)
                return new ScenarioModDependency[0];

            List<ScenarioModDependency> required = new List<ScenarioModDependency>();
            for (int i = 0; i < dependencies.Count; i++)
            {
                ScenarioModDependencyDefinition dependency = dependencies[i];
                if (dependency == null
                    || dependency.Kind != ScenarioModDependencyKind.Required
                    || string.IsNullOrEmpty(dependency.ModId))
                {
                    continue;
                }

                required.Add(new ScenarioModDependency
                {
                    modId = dependency.ModId,
                    version = dependency.Version,
                    warnings = new string[0]
                });
            }

            return ScenarioDependencyManifest.CloneRequiredMods(required.ToArray());
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
