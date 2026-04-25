using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Saves;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Executable verification harness for the scenario framework. This follows the
    /// existing smoke-test style and avoids a test framework so it can run under the
    /// .NET Framework 3.5 game runtime.
    /// </summary>
    public static class ScenarioFrameworkVerification
    {
        public static ScenarioValidationResult Run()
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            string root = Path.Combine(Path.GetTempPath(), "SMMScenarioFrameworkVerification_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(root);
                VerifyRoundTripAndCatalog(root, result);
                VerifyDependencies(result);
                VerifyAssetEscapes(root, result);
            }
            catch (Exception ex)
            {
                result.AddError("Scenario framework verification failed: " + ex.Message);
            }
            finally
            {
                TryDelete(root);
            }

            return result;
        }

        private static void VerifyRoundTripAndCatalog(string root, ScenarioValidationResult result)
        {
            string scenarioFile = CreateScenarioPack(root, "PackOne", "Scenario.PackOne", "Assets\\icon.png");
            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            ScenarioDefinition loaded = serializer.Load(scenarioFile);
            string xml = serializer.ToXml(loaded);
            ScenarioDefinition roundTrip = serializer.FromXml(xml);

            Assert(ScenarioDefinitionComparer.AreEquivalent(loaded, roundTrip), "Scenario XML round-trip changed the definition.", result);
            Assert(loaded.Dependencies.Count == 2, "Scenario dependency declarations were not parsed.", result);
            Assert(loaded.FamilySetup.Members.Count == 1, "Family member definition was not parsed.", result);
            Assert(loaded.FamilySetup.Members[0].Stats.Count == 1, "Family stat override was not parsed.", result);
            Assert(loaded.FamilySetup.Members[0].Traits.Count == 1, "Family trait override was not parsed.", result);
            Assert(loaded.FamilySetup.Members[0].Skills.Count == 1, "Family skill override was not parsed.", result);
            Assert(loaded.BunkerEdits.ObjectPlacements.Count == 1, "Object placement was not parsed.", result);
            Assert(loaded.TriggersAndEvents.Triggers.Count == 1, "Trigger definition was not parsed.", result);
            Assert(loaded.WinLossConditions.WinConditions.Count == 1, "Win condition was not parsed.", result);

            ScenarioCatalog catalog = new ScenarioCatalog(new VerificationFolderSource(root), serializer);
            catalog.Refresh();
            ScenarioInfo[] scenarios = catalog.ListAll();
            Assert(scenarios.Length == 1, "Scenario catalog did not discover exactly one scenario.xml pack.", result);
            Assert(scenarios.Length == 0 || string.Equals(scenarios[0].Id, "Scenario.PackOne", StringComparison.OrdinalIgnoreCase),
                "Scenario catalog discovered the wrong scenario id.", result);
        }

        private static void VerifyDependencies(ScenarioValidationResult result)
        {
            LoadedModInfo parsed = ScenarioDependencyManifest.ParseDependency("Required.Mod@1.2.0");
            Assert(parsed != null && parsed.modId == "Required.Mod" && parsed.version == "1.2.0",
                "Dependency parser did not split mod id and version.", result);

            ScenarioDefinition definition = CreateDefinition("Scenario.Dependency");
            definition.Dependencies.Add("Required.Mod@1.2.0");

            ScenarioValidationResult matched = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.2.0")).Validate(definition, null);
            Assert(matched.IsValid, "Matched required dependency was reported invalid.", result);

            ScenarioValidationResult missing = new ScenarioValidator(new VerificationDependencyResolver(null, null)).Validate(definition, null);
            Assert(ContainsIssue(missing, "not loaded"), "Missing required dependency was not reported.", result);

            ScenarioValidationResult mismatched = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "2.0.0")).Validate(definition, null);
            Assert(ContainsIssue(mismatched, "version mismatch"), "Version-mismatched dependency was not reported.", result);
        }

        private static void VerifyAssetEscapes(string root, ScenarioValidationResult result)
        {
            string pack1File = CreateScenarioPack(root, "Pack1", "Scenario.AssetEscape", "Assets\\icon.png");
            string pack2 = Path.Combine(root, "Pack2");
            Directory.CreateDirectory(pack2);
            File.WriteAllBytes(Path.Combine(pack2, "file.png"), new byte[] { 1, 2, 3, 4 });

            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            ScenarioDefinition definition = serializer.Load(pack1File);
            definition.AssetReferences.CustomIcons.Clear();
            definition.AssetReferences.CustomIcons.Add(new IconRef { Id = "escaped", RelativePath = "..\\Pack2\\file.png" });

            ScenarioValidationResult validation = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.2.0")).Validate(definition, pack1File);
            Assert(ContainsIssue(validation, "escapes the scenario pack folder"), "Sibling-prefix asset escape was not blocked.", result);
        }

        private static string CreateScenarioPack(string root, string packName, string scenarioId, string assetPath)
        {
            string packRoot = Path.Combine(Path.Combine(root, packName), "Scenarios\\Main");
            string assetFullPath = Path.Combine(packRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(assetFullPath));
            File.WriteAllBytes(assetFullPath, new byte[] { 137, 80, 78, 71 });

            ScenarioDefinition definition = CreateDefinition(scenarioId);
            definition.Dependencies.Add("Required.Mod@1.2.0");
            definition.Dependencies.Add("Optional.Mod");
            definition.AssetReferences.CustomIcons.Add(new IconRef { Id = "main", RelativePath = assetPath });

            string scenarioFile = Path.Combine(packRoot, ScenarioDefinitionSerializer.DefaultFileName);
            new ScenarioDefinitionSerializer().Save(definition, scenarioFile);
            return scenarioFile;
        }

        private static ScenarioDefinition CreateDefinition(string scenarioId)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.Id = scenarioId;
            definition.DisplayName = "Verification Scenario";
            definition.Description = "Used by the scenario framework verification harness.";
            definition.Author = "SMM";
            definition.Version = "1.0.0";

            FamilyMemberConfig member = new FamilyMemberConfig();
            member.Name = "Alex";
            member.Gender = ScenarioGender.Female;
            member.Stats.Add(new StatOverride { StatId = "Strength", Value = 7 });
            member.Traits.Add("Strength:Courageous");
            member.Skills.Add(new SkillOverride { SkillId = "Crafting", Level = 2 });
            definition.FamilySetup.Members.Add(member);

            definition.StartingInventory.OverrideRandomStart = true;
            definition.StartingInventory.Items.Add(new ItemEntry { ItemId = "Water", Quantity = 2 });

            ObjectPlacement placement = new ObjectPlacement();
            placement.DefinitionReference = "Generator";
            placement.Position.X = 1f;
            placement.Position.Y = -2f;
            placement.CustomProperties.Add(new ScenarioProperty { Key = "level", Value = "1" });
            definition.BunkerEdits.ObjectPlacements.Add(placement);

            TriggerDef trigger = new TriggerDef();
            trigger.Id = "day-3";
            trigger.Type = "day";
            trigger.Properties.Add(new ScenarioProperty { Key = "day", Value = "3" });
            definition.TriggersAndEvents.Triggers.Add(trigger);

            ConditionDef condition = new ConditionDef();
            condition.Id = "survive-7-days";
            condition.Type = "surviveDays";
            condition.Properties.Add(new ScenarioProperty { Key = "days", Value = "7" });
            definition.WinLossConditions.WinConditions.Add(condition);
            return definition;
        }

        private static bool ContainsIssue(ScenarioValidationResult result, string text)
        {
            if (result == null || text == null)
                return false;

            ScenarioValidationIssue[] issues = result.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null && issues[i].Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private sealed class VerificationDependencyResolver : IScenarioDependencyVersionResolver
        {
            private readonly string _loadedModId;
            private readonly string _loadedVersion;

            public VerificationDependencyResolver(string loadedModId, string loadedVersion)
            {
                _loadedModId = loadedModId;
                _loadedVersion = loadedVersion;
            }

            public bool IsLoaded(string modId)
            {
                return !string.IsNullOrEmpty(modId)
                    && !string.IsNullOrEmpty(_loadedModId)
                    && string.Equals(modId, _loadedModId, StringComparison.OrdinalIgnoreCase);
            }

            public string GetLoadedVersion(string modId)
            {
                return IsLoaded(modId) ? _loadedVersion : null;
            }
        }

        private sealed class VerificationFolderSource : IScenarioModFolderSource
        {
            private readonly string _root;

            public VerificationFolderSource(string root)
            {
                _root = root;
            }

            public ScenarioModFolder[] GetLoadedModFolders()
            {
                List<ScenarioModFolder> folders = new List<ScenarioModFolder>();
                string[] directories = Directory.GetDirectories(_root);
                for (int i = 0; i < directories.Length; i++)
                    folders.Add(new ScenarioModFolder(Path.GetFileName(directories[i]), directories[i]));
                return folders.ToArray();
            }
        }
    }
}
