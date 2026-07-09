using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Assets;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioPackageUxVerification
    {
        public static void Verify(string root, ScenarioValidationResult result)
        {
            string draftRoot = Path.Combine(root, "PackageUxDraft");
            string exportRoot = Path.Combine(root, "PackageUxExport");
            string installRoot = Path.Combine(root, "PackageUxInstalled");
            Directory.CreateDirectory(Path.Combine(draftRoot, "Assets"));
            File.WriteAllBytes(Path.Combine(draftRoot, "Assets\\icon.png"), new byte[] { 1, 2, 3, 4, 5 });

            ScenarioDefinition definition = new ScenarioDefinition
            {
                Id = "verify.packageux",
                DisplayName = "Package UX Verification",
                Description = "A package contract scenario.",
                Author = "Verifier",
                Version = "1.2.3",
                Credits = "Contract suite"
            };
            definition.AssetReferences.CustomIcons.Add(new IconRef { Id = "icon", RelativePath = "Assets\\icon.png" });
            definition.ModDependencies.Add(new ScenarioModDependencyDefinition { ModId = "Example.Required", Version = "2.0", Kind = ScenarioModDependencyKind.Required });

            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            ScenarioDefinitionSerializerAdapter adapter = new ScenarioDefinitionSerializerAdapter(serializer);
            string draftScenario = Path.Combine(draftRoot, ScenarioDefinitionSerializer.DefaultFileName);
            serializer.Save(definition, draftScenario);
            ScenarioValidationResult validation = new ScenarioValidationResult();
            validation.AddWarning("Unsupported verification feature remains author-visible.");

            ScenarioPackageAuthoringPreferences preferences = new ScenarioPackageAuthoringPreferences();
            string accepted = ScenarioPackageAuthoringPreferences.BuildFingerprint(validation.Issues, 0);
            preferences.Accept(accepted, "Intentional compatibility tradeoff.");
            preferences.Save(draftScenario);
            ScenarioPackageAuthoringPreferences loadedPreferences = ScenarioPackageAuthoringPreferences.Load(draftScenario);
            Assert(loadedPreferences.Find(accepted) != null, "Accepted-warning note did not persist.", result);
            ScenarioValidationResult changedValidation = new ScenarioValidationResult();
            changedValidation.AddWarning("Unsupported verification feature occurs at a new location.");
            Assert(loadedPreferences.Find(ScenarioPackageAuthoringPreferences.BuildFingerprint(changedValidation.Issues, 0)) == null,
                "A new warning occurrence inherited an old acceptance.", result);

            ScenarioPackagePlanner planner = new ScenarioPackagePlanner(adapter);
            ScenarioPackagePlan preview = planner.Build(definition, draftScenario, exportRoot, true, validation);
            preview.Write();
            AssertPackageMatchesPlan(preview, result);
            string readme = File.ReadAllText(Path.Combine(exportRoot, ScenarioPackagePlanner.ReadmeFileName));
            Assert(readme.IndexOf("DESCRIPTION", StringComparison.Ordinal) >= 0
                && readme.IndexOf("INSTALLATION", StringComparison.Ordinal) >= 0
                && readme.IndexOf("REQUIRED MODS", StringComparison.Ordinal) >= 0
                && readme.IndexOf("KNOWN LIMITATIONS", StringComparison.Ordinal) >= 0,
                "README.txt is missing a required section.", result);

            VerificationCatalog catalog = new VerificationCatalog();
            ScenarioPackageInstaller installer = new ScenarioPackageInstaller(adapter, catalog);
            ScenarioPackageInstallResult installed = installer.Install(exportRoot, installRoot, false);
            Assert(installed.Success && File.Exists(Path.Combine(Path.Combine(installRoot, definition.Id), ScenarioDefinitionSerializer.DefaultFileName)),
                "Local install did not round-trip into the scenario directory.", result);
            definition.DisplayName = "Different Package With Same Id";
            serializer.Save(definition, Path.Combine(Path.Combine(installRoot, definition.Id), ScenarioDefinitionSerializer.DefaultFileName));
            ScenarioPackageInstallResult guarded = installer.Install(exportRoot, installRoot, false);
            Assert(guarded.ConfirmationRequired && !guarded.Success, "Same-ID replacement was not confirmation guarded.", result);
        }

        private static void AssertPackageMatchesPlan(ScenarioPackagePlan plan, ScenarioValidationResult result)
        {
            string[] files = Directory.GetFiles(plan.PackageRoot, "*", SearchOption.AllDirectories);
            Assert(files.Length == plan.Entries.Count, "Dry-run preview file count differs from actual export.", result);
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                string path = ScenarioPackagePlan.ResolveContainedPath(plan.PackageRoot, plan.Entries[i].RelativePath);
                Assert(File.Exists(path) && new FileInfo(path).Length == plan.Entries[i].Size,
                    "Dry-run preview differs from actual export for " + plan.Entries[i].RelativePath + ".", result);
            }
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition) result.AddError("Package UX contract: " + message);
        }

        private sealed class VerificationCatalog : IScenarioDefinitionCatalogService
        {
            private int _revision;
            public int CatalogRevision { get { return _revision; } }
            public void RefreshDefinitionCatalog() { _revision++; }
            public ScenarioInfo[] ListDefinitions() { return new ScenarioInfo[0]; }
            public ScenarioValidationResult ValidateDefinition(string scenarioId) { return new ScenarioValidationResult(); }
            public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
            {
                definition = null; scenarioFilePath = null; validation = new ScenarioValidationResult(); return false;
            }
        }
    }
}
