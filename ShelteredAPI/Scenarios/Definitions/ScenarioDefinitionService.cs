using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Registration;
using ShelteredAPI.UI.FieldManual.Textures;
namespace ShelteredAPI.Scenarios.Definitions{
    internal sealed class ScenarioDefinitionService : IScenarioDefinitionFactory
    {
        private readonly IScenarioRegistrationStore _store;
        private readonly IScenarioStateManager _stateManager;
        private readonly IScenarioDefinitionReader _definitionReader;

        public ScenarioDefinitionService(
            IScenarioRegistrationStore store,
            IScenarioStateManager stateManager,
            IScenarioDefinitionReader definitionReader)
        {
            _store = store;
            _stateManager = stateManager;
            _definitionReader = definitionReader;
        }

        public bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage)
        {
            ScenarioDef scenarioDef;
            bool result = TryCreateScenarioDef(scenarioId, context, out scenarioDef, out errorMessage);
            definition = scenarioDef;
            return result;
        }

        public bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage)
        {
            definition = null;
            errorMessage = null;

            ScenarioRecord record;
            if (!_store.TryGet(scenarioId, out record))
            {
                errorMessage = "Custom scenario is not registered: " + scenarioId;
                return false;
            }

            CustomScenarioRegistration registration = record.Registration;
            if (registration.Definition != null)
            {
                definition = registration.Definition as ScenarioDef;
                if (definition == null)
                {
                    errorMessage = "Registered definition for '" + record.Info.Id + "' is not a Sheltered ScenarioDef.";
                    return false;
                }

                return true;
            }

            if (registration.DefinitionFactory == null)
            {
                errorMessage = "Custom scenario has no ScenarioDef or definition factory: " + record.Info.Id;
                return false;
            }

            try
            {
                CustomScenarioBuildContext buildContext = PrepareBuildContext(record, context);
                object built = registration.DefinitionFactory(buildContext);
                definition = built as ScenarioDef;
                if (definition == null)
                {
                    errorMessage = "Definition factory for '" + record.Info.Id + "' did not return a Sheltered ScenarioDef.";
                    return false;
                }

                return true;
            }
            catch (InvalidOperationException ex)
            {
                errorMessage = "Definition factory for '" + record.Info.Id + "' failed: " + ex.Message;
                MMLog.WriteError("[ScenarioDefinitionService] " + errorMessage);
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Definition factory for '" + record.Info.Id + "' failed: " + ex.Message;
                MMLog.WriteError("[ScenarioDefinitionService] " + errorMessage);
                return false;
            }
        }

        public ScenarioDef BuildScenarioDefFromDefinition(string scenarioId)
        {
            ScenarioDefinition definition;
            string scenarioFilePath;
            ScenarioValidationResult validation;
            if (!_definitionReader.TryLoad(scenarioId, out definition, out scenarioFilePath, out validation))
                throw new InvalidOperationException("Scenario XML failed validation: " + FormatValidationIssues(validation));

            return BuildPlayableScenarioDef(definition);
        }

        // Builds the zero-stage carrier used by completion-only diagnostics and
        // scenarios without a playable story flow.
        internal static ScenarioDef BuildScenarioDef(ScenarioDefinition definition)
        {
            return BuildScenarioDefCore(definition, false);
        }

        // Installed scenarios and authoring playtests need the authored vanilla
        // ScenarioFlow projected into ScenarioStage objects. QuestManager then
        // owns the intercom visitor, choices, dialogue, and branch transitions,
        // while the exact-clock scheduler remains responsible for neutral actions.
        internal static ScenarioDef BuildPlayableScenarioDef(ScenarioDefinition definition)
        {
            return BuildScenarioDefCore(definition, true);
        }

        private static ScenarioDef BuildScenarioDefCore(ScenarioDefinition definition, bool includeStoryFlow)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            ShelteredScenarioDefBuilder builder = new ShelteredScenarioDefBuilder()
                .SetId(definition.Id)
                .SetNameKey(!string.IsNullOrEmpty(definition.DisplayName) ? definition.DisplayName : definition.Id)
                .SetDescriptionKey(definition.Description ?? string.Empty)
                .ApplySelectionRules(definition.SelectionRules, definition.BaseGameMode);

            ScenarioCharacterRuntimeNameRegistry.Register(definition);

            for (int i = 0; definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
                builder.AddScenarioCharacter(definition.ScenarioCharacters[i]);

            for (int i = 0; includeStoryFlow
                && definition.ScenarioFlow != null
                && definition.ScenarioFlow.Stages != null
                && i < definition.ScenarioFlow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[i];
                if (stage != null && stage.IntercomStages != null && stage.IntercomStages.Count > 0)
                    builder.AddFlowStage(stage);
            }

            return builder.Build();
        }

        private CustomScenarioBuildContext PrepareBuildContext(ScenarioRecord record, CustomScenarioBuildContext context)
        {
            CustomScenarioBuildContext result = context ?? new CustomScenarioBuildContext();
            if (string.IsNullOrEmpty(result.ScenarioId))
                result.ScenarioId = record.Info.Id;
            if (string.IsNullOrEmpty(result.OwnerModId))
                result.OwnerModId = record.Info.OwnerModId;
            if (result.State == null)
                result.State = _stateManager.GetCustomScenarioState();
            if (result.UserData == null)
                result.UserData = record.Registration.UserData;
            return result;
        }

        private static string FormatValidationIssues(ScenarioValidationResult validation)
        {
            if (validation == null || validation.Issues.Length == 0)
                return "no details were provided.";

            List<string> parts = new List<string>();
            ScenarioValidationIssue[] issues = validation.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null)
                    parts.Add(issues[i].Severity + ": " + issues[i].Message);
            }

            return string.Join("; ", parts.ToArray());
        }
    }
}
