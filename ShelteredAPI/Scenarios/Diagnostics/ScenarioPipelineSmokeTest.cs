using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;

using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Diagnostics{
    /// <summary>
    /// Developer-only smoke harness for Sprint 1. It is not wired into startup because
    /// bad scenario XML should never block normal game boot; call this from a debug mod
    /// or immediate window when validating a handmade scenario.xml.
    /// </summary>
    internal static class ScenarioPipelineSmokeTest
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

        internal static ScenarioValidationResult RunMetadataContract()
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            string path = Path.Combine(Path.GetTempPath(), "sheltered-metadata-contract-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                ScenarioDefinition definition = new ScenarioDefinition();
                definition.Id = "com.example.metadata.contract";
                definition.DisplayName = "Metadata Contract";
                definition.Description = "Verifies packaging metadata survives a saved draft reload.";
                definition.Author = "Contract Author";
                definition.Version = "2.3.4";
                definition.Credits = "Test contributors";
                definition.Tags.Add("story");
                definition.Tags.Add("contract");

                ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
                serializer.Save(definition, path);
                ScenarioDefinition reloaded = serializer.Load(path);
                if (reloaded == null
                    || reloaded.Id != definition.Id
                    || reloaded.DisplayName != definition.DisplayName
                    || reloaded.Description != definition.Description
                    || reloaded.Author != definition.Author
                    || reloaded.Version != definition.Version
                    || reloaded.Credits != definition.Credits
                    || reloaded.Tags.Count != 2
                    || reloaded.Tags[0] != "story"
                    || reloaded.Tags[1] != "contract")
                {
                    result.AddError("Metadata save/reload contract failed.");
                }

                ScenarioDefinition placeholders = new ScenarioDefinition();
                placeholders.Id = "com.example.placeholder.contract";
                placeholders.DisplayName = ScenarioMetadataDefaults.DefaultTitle;
                placeholders.Author = ScenarioMetadataDefaults.DefaultAuthor;
                placeholders.Version = ScenarioMetadataDefaults.DefaultVersion;
                ScenarioValidationResult validation = new ScenarioValidator(new NoDependencyWarnings()).Validate(placeholders, path);
                if (!HasWarning(validation, "placeholder title")
                    || !HasWarning(validation, "author as 'unknown'")
                    || !HasWarning(validation, "no description")
                    || !HasWarning(validation, "default version 0.1.0"))
                {
                    result.AddError("Metadata placeholder validation contract failed.");
                }
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }

            return result;
        }

        private static bool HasWarning(ScenarioValidationResult validation, string text)
        {
            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : null;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                if (issues[i] != null
                    && issues[i].Severity == ScenarioIssueSeverity.Warning
                    && issues[i].Message != null
                    && issues[i].Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
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

        private sealed class NoDependencyWarnings : IScenarioDependencyVersionResolver
        {
            public bool IsLoaded(string modId)
            {
                return true;
            }

            public string GetLoadedVersion(string modId)
            {
                return null;
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
