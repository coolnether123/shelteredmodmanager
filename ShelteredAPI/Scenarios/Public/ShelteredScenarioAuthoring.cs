using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Diagnostics;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Registration;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredAPI.Scenarios.Domain.Validation;
namespace ShelteredAPI.Scenarios.Public{
    /// <summary>
    /// Stable XML scenario authoring facade for loading, saving, and validating Sheltered scenario definitions.
    /// </summary>
    public static class ShelteredScenarioAuthoring
    {
        public const string DefaultFileName = ScenarioDefinitionSerializer.DefaultFileName;
        /// <summary>Default display title for a newly authored or metadata-incomplete scenario.</summary>
        public const string DefaultTitle = ScenarioMetadataDefaults.DefaultTitle;
        /// <summary>Default author marker for a newly authored or metadata-incomplete scenario.</summary>
        public const string DefaultAuthor = ScenarioMetadataDefaults.DefaultAuthor;
        /// <summary>Default semantic version for a newly authored or metadata-incomplete scenario.</summary>
        public const string DefaultVersion = ScenarioMetadataDefaults.DefaultVersion;
        /// <summary>Terrain identifier for a generated blend-area patch.</summary>
        public const string GeneratedBlendTerrainId = ScenarioMapTerrainModes.GeneratedBlend;

        public static ScenarioDefinition CreateDefinition()
        {
            return new ScenarioDefinition();
        }

        public static ScenarioDefinition CreateDefinition(ScenarioBaseGameMode baseGameMode)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.BaseGameMode = baseGameMode;
            return definition;
        }

        /// <summary>Returns a patch- or minor-incremented semantic version using canonical scenario defaults.</summary>
        public static string BumpVersion(string version, bool minor)
        {
            return ScenarioMetadataDefaults.BumpVersion(version, minor);
        }

        /// <summary>Returns the supported vanilla map icon identifiers.</summary>
        public static string[] GetKnownMapIconIds()
        {
            return ScenarioMapIconCatalog.GetKnownIconIds();
        }

        /// <summary>Returns whether an icon id is empty or names a supported vanilla map icon.</summary>
        public static bool IsKnownMapIconId(string iconId)
        {
            return ScenarioMapIconCatalog.IsKnownIconId(iconId);
        }

        /// <summary>Resolves the canonical actor reference for a future-survivor definition.</summary>
        public static ScenarioActorRef ResolveFutureSurvivorActorReference(FutureSurvivorDefinition survivor)
        {
            return ScenarioFutureSurvivorActorReference.Resolve(survivor);
        }

        public static ScenarioDefinition LoadDefinition(string filePath)
        {
            return new ScenarioDefinitionSerializer().Load(filePath);
        }

        public static bool TryLoadDefinitionWithRecovery(
            string filePath,
            out ScenarioDefinition definition,
            out string recoveryMessage,
            out bool recovered)
        {
            return new ScenarioDefinitionSerializer().TryLoadWithRecovery(
                filePath,
                out definition,
                out recoveryMessage,
                out recovered);
        }

        public static ScenarioInfo LoadDefinitionInfo(string filePath, string ownerModId)
        {
            return new ScenarioDefinitionSerializer().LoadInfo(filePath, ownerModId);
        }

        public static ScenarioDefinition FromXml(string xml)
        {
            return new ScenarioDefinitionSerializer().FromXml(xml);
        }

        public static void SaveDefinition(ScenarioDefinition definition, string filePath)
        {
            new ScenarioDefinitionSerializer().Save(definition, filePath);
        }

        public static string ToXml(ScenarioDefinition definition)
        {
            return new ScenarioDefinitionSerializer().ToXml(definition);
        }

        public static ScenarioValidationResult ValidateDefinition(ScenarioDefinition definition, string scenarioFilePath)
        {
            return new ScenarioValidatorImpl().Validate(definition, scenarioFilePath);
        }

        /// <summary>Builds a reusable, case-insensitive reference index for one scenario definition.</summary>
        public static ScenarioDefinitionReferenceIndex IndexDefinition(ScenarioDefinition definition)
        {
            return new ScenarioDefinitionReferenceIndex(new ScenarioDefinitionIndex(definition));
        }

        /// <summary>Builds a reusable, case-insensitive reference index for trigger authoring.</summary>
        public static ScenarioDefinitionReferenceIndex IndexDefinition(TriggersAndEventsDefinition triggersAndEvents)
        {
            return new ScenarioDefinitionReferenceIndex(new ScenarioDefinitionIndex(triggersAndEvents));
        }

        /// <summary>Runs the canonical story-flow validation analysis used by runtime validation.</summary>
        public static ScenarioStoryFlowIssue[] AnalyzeStoryFlow(ScenarioDefinition definition)
        {
            return new ScenarioStoryFlowValidationAnalyzer().Analyze(definition);
        }

        /// <summary>Returns whether the definition has at least one starting survivor.</summary>
        public static bool HasStartingSurvivor(ScenarioDefinition definition)
        {
            return ScenarioPlayStartReadiness.HasStartingSurvivor(definition);
        }

        /// <summary>Returns whether the definition can enter play, with the canonical disabled reason.</summary>
        public static bool CanStartPlay(ScenarioDefinition definition, out string reason)
        {
            return new ScenarioPlayStartReadiness().CanStartPlay(definition, out reason);
        }

        /// <summary>Returns the canonical map encounter projection capabilities.</summary>
        public static ScenarioMapProjectionField[] GetMapEncounterProjectionFields()
        {
            if (!ScenarioMapProjectionFieldCatalog.IsSynchronized())
                throw new System.InvalidOperationException("Map encounter projection descriptors are out of sync with runtime behavior.");
            return ScenarioMapProjectionFieldCatalog.GetEncounterFields();
        }

        public const string EmptyStartingCastWarning = ScenarioPlayStartReadiness.EmptyCastWarning;
        public const string EmptyStartingCastDisabledReason = ScenarioPlayStartReadiness.EmptyCastDisabledReason;
        public const string UnsavedDraftPlayDisabledReason = ScenarioPlayStartReadiness.UnsavedDraftDisabledReason;
        public const string ValidationUnavailablePlayDisabledReason = ScenarioPlayStartReadiness.ValidationUnavailableDisabledReason;

        public static ScenarioValidationResult ValidateXmlDefinition(string scenarioId)
        {
            return ShelteredCustomScenarioService.Instance.ValidateDefinition(scenarioId);
        }

        /// <summary>
        /// Projects a serialized trigger onto the same scheduled-action model used
        /// by the live runtime and validation pipeline.
        /// </summary>
        public static bool TryCompileTrigger(
            TriggerDef trigger,
            int index,
            out ScenarioScheduledActionDefinition action,
            out string reason)
        {
            return ScenarioTriggerDefinitionCompiler.TryCreateAction(
                trigger,
                index,
                out action,
                out reason);
        }

        /// <summary>Returns whether a serialized trigger requires an explicit runtime fire call.</summary>
        public static bool IsManualTrigger(TriggerDef trigger)
        {
            return ScenarioTriggerDefinitionCompiler.IsManual(trigger);
        }

        public static bool TryLoadXmlDefinition(
            string scenarioId,
            out ScenarioDefinition definition,
            out string scenarioFilePath,
            out ScenarioValidationResult validation)
        {
            return ShelteredCustomScenarioService.Instance.TryLoadDefinition(
                scenarioId,
                out definition,
                out scenarioFilePath,
                out validation);
        }

        /// <summary>
        /// Assigns stable actor references to cast entries that do not already have one.
        /// Existing references are preserved.
        /// </summary>
        public static int AssignMissingActorReferences(ScenarioDefinition definition)
        {
            return new ScenarioActorResolver().AssignMissingCastActorRefs(definition);
        }

        /// <summary>Ensures that one starting-family definition has a stable actor reference.</summary>
        public static ScenarioActorRef EnsureStartingMemberActorReference(
            ScenarioDefinition definition,
            FamilyMemberConfig member,
            int memberIndex)
        {
            return new ScenarioActorResolver().EnsureStartingMemberRef(definition, member, memberIndex);
        }

        /// <summary>Ensures that one future-survivor definition has a stable actor reference.</summary>
        public static ScenarioActorRef EnsureFutureSurvivorActorReference(
            ScenarioDefinition definition,
            FutureSurvivorDefinition survivor,
            int survivorIndex)
        {
            return new ScenarioActorResolver().EnsureFutureSurvivorRef(definition, survivor, survivorIndex);
        }

        /// <summary>Creates an actor reference for a family member currently present in the shelter.</summary>
        public static ScenarioActorRef CreateLiveFamilyMemberActorReference(FamilyMember member)
        {
            return new ScenarioActorResolver().CreateLiveFamilyMemberRef(member);
        }

    }

    /// <summary>
    /// Read-only reference query session over one scenario definition. The index is built once so
    /// editor and mod tooling can make repeated reference checks without duplicating traversal rules.
    /// </summary>
    public sealed class ScenarioDefinitionReferenceIndex
    {
        private readonly ScenarioDefinitionIndex _index;

        internal ScenarioDefinitionReferenceIndex(ScenarioDefinitionIndex index)
        {
            _index = index;
        }

        public bool HasGate(string id) { return _index.HasGate(id); }
        public bool HasTrigger(string id) { return _index.HasTrigger(id); }
        public bool HasQuest(string id) { return _index.HasQuest(id); }
        public bool HasCondition(string id) { return _index.HasCondition(id); }
        public bool HasExpansion(string id) { return _index.HasExpansion(id); }
        public bool HasObject(string id) { return _index.HasObject(id); }
        public bool HasFutureSurvivor(string id) { return _index.HasFutureSurvivor(id); }
        public bool HasFamilySurvivor(string id) { return _index.HasFamilySurvivor(id); }
    }
}
