using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Developer-only smoke harness for Sprint 1. It is not wired into startup because
    /// bad scenario XML should never block normal game boot; call this from a debug mod
    /// or immediate window when validating a handmade scenario.xml.
    /// </summary>
    public static class ScenarioPipelineSmokeTest
    {
        public static ScenarioValidationResult Run(string modsRootOrSingleScenarioFile)
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            if (string.IsNullOrEmpty(modsRootOrSingleScenarioFile))
            {
                result.AddError("Smoke test path is required.");
                return result;
            }

            try
            {
                if (File.Exists(modsRootOrSingleScenarioFile))
                    return RunSingleFile(modsRootOrSingleScenarioFile);

                if (Directory.Exists(modsRootOrSingleScenarioFile))
                    return RunCatalog(modsRootOrSingleScenarioFile);

                result.AddError("Smoke test path does not exist: " + modsRootOrSingleScenarioFile);
                return result;
            }
            catch (Exception ex)
            {
                result.AddError("Smoke test failed: " + ex.Message);
                return result;
            }
        }

        private static ScenarioValidationResult RunSingleFile(string scenarioFile)
        {
            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            ScenarioDefinition definition = serializer.Load(scenarioFile);
            ScenarioValidationResult validation = new ScenarioValidator(new NoDependencyWarnings()).Validate(definition, scenarioFile);
            LogDefinition(definition, validation);

            string xml = serializer.ToXml(definition);
            ScenarioDefinition roundTrip = serializer.FromXml(xml);
            if (!ScenarioDefinitionComparer.AreEquivalent(definition, roundTrip))
                validation.AddError("Round-trip serialize/deserialize changed the definition.");

            LogValidation(validation);
            return validation;
        }

        private static ScenarioValidationResult RunCatalog(string modsRoot)
        {
            ScenarioCatalog catalog = new ScenarioCatalog(new DirectoryScenarioModFolderSource(modsRoot), new ScenarioDefinitionSerializer());
            catalog.Refresh();
            ScenarioInfo[] scenarios = catalog.ListAll();
            MMLog.WriteInfo("[ScenarioPipelineSmokeTest] Catalog found " + scenarios.Length + " scenario(s).");

            ScenarioValidationResult combined = new ScenarioValidationResult();
            ScenarioLoader loader = new ScenarioLoader(
                catalog,
                new ScenarioDefinitionSerializer(),
                new ScenarioValidator(new NoDependencyWarnings()));

            for (int i = 0; i < scenarios.Length; i++)
            {
                ScenarioValidationResult validation = loader.Validate(scenarios[i].Id);
                CopyIssues(validation, combined);
                MMLog.WriteInfo("[ScenarioPipelineSmokeTest] " + scenarios[i].Id + " valid=" + validation.IsValid);
            }

            return combined;
        }

        private static void LogDefinition(ScenarioDefinition definition, ScenarioValidationResult validation)
        {
            if (definition == null)
                return;

            MMLog.WriteInfo("[ScenarioPipelineSmokeTest] Loaded scenario '" + definition.Id + "' "
                + "name='" + definition.DisplayName + "' author='" + definition.Author + "' version='" + definition.Version + "'.");
            MMLog.WriteInfo("[ScenarioPipelineSmokeTest] BaseMode=" + definition.BaseGameMode
                + ", familyMembers=" + definition.FamilySetup.Members.Count
                + ", inventoryItems=" + definition.StartingInventory.Items.Count
                + ", roomEdits=" + definition.BunkerEdits.RoomChanges.Count + ".");
        }

        private static void LogValidation(ScenarioValidationResult validation)
        {
            if (validation == null)
                return;

            MMLog.WriteInfo("[ScenarioPipelineSmokeTest] Validation valid=" + validation.IsValid
                + ", issues=" + validation.Issues.Length + ".");
            for (int i = 0; i < validation.Issues.Length; i++)
            {
                ScenarioValidationIssue issue = validation.Issues[i];
                if (issue != null)
                    MMLog.WriteInfo("[ScenarioPipelineSmokeTest] " + issue.Severity + ": " + issue.Message);
            }
        }

        private static void CopyIssues(ScenarioValidationResult source, ScenarioValidationResult target)
        {
            if (source == null || target == null)
                return;

            ScenarioValidationIssue[] issues = source.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] == null)
                    continue;
                if (issues[i].Severity == ScenarioIssueSeverity.Error)
                    target.AddError(issues[i].Message);
                else
                    target.AddWarning(issues[i].Message);
            }
        }

        private sealed class NoDependencyWarnings : IScenarioDependencyResolver
        {
            public bool IsLoaded(string modId)
            {
                return true;
            }
        }

        private sealed class DirectoryScenarioModFolderSource : IScenarioModFolderSource
        {
            private readonly string _modsRoot;

            public DirectoryScenarioModFolderSource(string modsRoot)
            {
                _modsRoot = modsRoot;
            }

            public ScenarioModFolder[] GetLoadedModFolders()
            {
                List<ScenarioModFolder> folders = new List<ScenarioModFolder>();
                string[] directories = Directory.GetDirectories(_modsRoot);
                for (int i = 0; i < directories.Length; i++)
                    folders.Add(new ScenarioModFolder(Path.GetFileName(directories[i]), directories[i]));
                return folders.ToArray();
            }
        }
    }

    internal static class ScenarioDefinitionComparer
    {
        public static bool AreEquivalent(ScenarioDefinition left, ScenarioDefinition right)
        {
            if (left == null || right == null)
                return left == right;

            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            return string.Equals(serializer.ToXml(left), serializer.ToXml(right), StringComparison.Ordinal);
        }
    }
}
