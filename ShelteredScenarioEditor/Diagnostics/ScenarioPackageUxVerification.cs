using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Assets;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Public;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredScenarioEditor.Diagnostics
{
    internal static class ScenarioPackageUxVerification
    {
        public static string[] Run(string root)
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            Verify(root, result);
            return result.Issues.Select(issue => issue.Message).ToArray();
        }

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
            definition.AssetReferences.SceneSpritePlacements.Add(new SceneSpritePlacement { Id = "sprite", RelativePath = "Assets\\icon.png" });
            definition.ModDependencies.Add(new ScenarioModDependencyDefinition { ModId = "Example.Optional", Version = "2.0", Kind = ScenarioModDependencyKind.Optional });

            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            string draftScenario = Path.Combine(draftRoot, ScenarioEditorDefinitionSerializer.DefaultFileName);
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

            ScenarioPackagePlanner planner = new ScenarioPackagePlanner(serializer);
            ScenarioPackagePlan preview = planner.Build(definition, draftScenario, exportRoot, true, validation, null);
            ScenarioEditorDefinitionValidator validator = new ScenarioEditorDefinitionValidator();
            new ScenarioPackageContents(serializer, validator).Replace(exportRoot, definition.Id, delegate { return preview.Entries; });
            AssertPackageMatchesPlan(preview, result);
            string readme = File.ReadAllText(Path.Combine(exportRoot, ScenarioPackagePlanner.ReadmeFileName));
            Assert(readme.IndexOf("DESCRIPTION", StringComparison.Ordinal) >= 0
                && readme.IndexOf("GOAL", StringComparison.Ordinal) >= 0
                && readme.IndexOf("INSTALLATION", StringComparison.Ordinal) >= 0
                && readme.IndexOf("REQUIRED MODS", StringComparison.Ordinal) >= 0
                && readme.IndexOf("KNOWN LIMITATIONS", StringComparison.Ordinal) >= 0,
                "README.txt is missing a required section.", result);

            VerificationCatalog catalog = new VerificationCatalog();
            ScenarioPackageInstaller installer = new ScenarioPackageInstaller(serializer, catalog, installRoot);
            ScenarioPackageOperationResult installed = installer.Install(exportRoot, false);
            Assert(installed.Success && File.Exists(Path.Combine(Path.Combine(installRoot, definition.Id), ScenarioEditorDefinitionSerializer.DefaultFileName)),
                "Local install did not round-trip into the scenario directory.", result);
            definition.DisplayName = "Different Package With Same Id";
            serializer.Save(definition, Path.Combine(Path.Combine(installRoot, definition.Id), ScenarioEditorDefinitionSerializer.DefaultFileName));
            ScenarioPackageOperationResult guarded = installer.Install(exportRoot, false);
            Assert(guarded.ConfirmationRequired && !guarded.Success, "Same-ID replacement was not confirmation guarded.", result);
            serializer.Save(serializer.Load(Path.Combine(exportRoot, ScenarioEditorDefinitionSerializer.DefaultFileName)),
                Path.Combine(Path.Combine(installRoot, definition.Id), ScenarioEditorDefinitionSerializer.DefaultFileName));
            ScenarioPackageOperationResult uninstalled = installer.Uninstall(definition.Id, installed.InstallPath);
            Assert(uninstalled.Success && !Directory.Exists(installed.InstallPath) && catalog.CatalogRevision == 2,
                "Identity-checked uninstall did not remove the installed package and refresh the catalog.", result);

            string exports = Path.Combine(root, "Exports");
            string exportedPackage = Path.Combine(exports, definition.Id);
            string installedPackage = Path.Combine(installRoot, definition.Id);
            ScenarioEditorSessionStore store = new ScenarioEditorSessionStore();
            store.Set(new ScenarioEditorSession { WorkingDefinition = definition }, draftScenario);
            ScenarioEditorController editor = new ScenarioEditorController(store, serializer, validator, null, null, null, null, null, null, null);
            Func<Action<ScenarioPackageStep, string>, bool> export = delegate(Action<ScenarioPackageStep, string> fault)
            {
                ScenarioPublishExportService service = new ScenarioPublishExportService(editor, serializer, validator,
                    installer, null, store, fault, exports);
                return service.ExportActiveDraft(null).Success;
            };
            Assert(export(null), "Could not create the new export fixture.", result);
            Dictionary<string, string> newBytes = Snapshot(exportedPackage);
            VerifyReplacement(exportedPackage, newBytes, export, result);
            Directory.Delete(exportedPackage, true);
            Restore(exportedPackage, newBytes);

            Func<Action<ScenarioPackageStep, string>, bool> install = delegate(Action<ScenarioPackageStep, string> fault)
            {
                return new ScenarioPackageInstaller(serializer, null, installRoot, fault).Install(exportedPackage, true).Success;
            };
            VerifyReplacement(installedPackage, newBytes, install, result);

            string sourceAsset = Path.Combine(exportedPackage, @"Assets\icon.png");
            Dictionary<string, string> beforeLock = Snapshot(installedPackage);
            using (FileStream locked = new FileStream(sourceAsset, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert(!install(null) && Matches(installedPackage, beforeLock), "A locked source file changed the installed package.", result);
            Assert(install(null) && Matches(installedPackage, newBytes), "A retry after unlocking the source failed.", result);

            File.WriteAllText(Path.Combine(exportedPackage, ScenarioPackagePlanner.ManifestFileName), "<ScenarioPackage/>");
            Assert(!install(null) && Matches(installedPackage, newBytes), "An invalid manifest replaced the installed package.", result);
            Restore(exportedPackage, newBytes);
            File.Delete(sourceAsset);
            Assert(!install(null) && Matches(installedPackage, newBytes), "A missing asset replaced the installed package.", result);

            VerifyCatalogRecovery(installRoot, installedPackage, newBytes, result);
        }

        private static void VerifyReplacement(string destination, Dictionary<string, string> newBytes,
            Func<Action<ScenarioPackageStep, string>, bool> replace, ScenarioValidationResult result)
        {
            Dictionary<string, string> oldBytes = new Dictionary<string, string>(newBytes, StringComparer.OrdinalIgnoreCase);
            oldBytes["Assets\\icon.png"] = Convert.ToBase64String(new byte[] { 7, 6, 5 });
            oldBytes["old-only.txt"] = Convert.ToBase64String(new byte[] { 42 });

            foreach (ScenarioPackageStep step in Enum.GetValues(typeof(ScenarioPackageStep)))
            {
                Restore(destination, oldBytes);
                bool reached = false;
                bool committed = replace(delegate(ScenarioPackageStep current, string path)
                {
                    if (current != step) return;
                    reached = true;
                    if (step == ScenarioPackageStep.Write)
                        File.WriteAllText(Path.Combine(path, "incomplete.txt"), "partially written");
                    if (step == ScenarioPackageStep.Cleanup)
                        File.Delete(Path.Combine(path, "old-only.txt"));
                    throw new IOException("Injected failure at " + step);
                });
                bool expectedCommit = step == ScenarioPackageStep.Cleanup;
                Assert(reached && committed == expectedCommit && Matches(destination, expectedCommit ? newBytes : oldBytes),
                    Path.GetFileName(Path.GetDirectoryName(destination)) + " changed the wrong bytes after failure at " + step + ".", result);
                ShelteredScenarioPackages.RecoverRoot(Path.GetDirectoryName(destination));
                Assert(!Directory.Exists(TransactionFile(destination, "stage"))
                    && !Directory.Exists(TransactionFile(destination, "backup"))
                    && !File.Exists(TransactionFile(destination, "txn"))
                    && Matches(destination, expectedCommit ? newBytes : oldBytes), "Recovery changed the wrong bytes after " + step + ".", result);
            }

            Restore(destination, oldBytes);
            bool corrupted = replace(delegate(ScenarioPackageStep current, string stage)
            {
                if (current == ScenarioPackageStep.Validate)
                    File.WriteAllBytes(Path.Combine(stage, @"Assets\icon.png"), new byte[] { 8, 8, 8, 8, 8 });
            });
            Assert(!corrupted && Matches(destination, oldBytes), "A same-size staged file change passed its hash check.", result);
            Assert(replace(null) && Matches(destination, newBytes), "Normal replacement did not publish the complete new package.", result);
        }

        private static void VerifyCatalogRecovery(string root, string destination, Dictionary<string, string> bytes, ScenarioValidationResult result)
        {
            string stage = TransactionFile(destination, "stage");
            string backup = TransactionFile(destination, "backup");
            File.WriteAllText(TransactionFile(destination, "txn"), "");
            Directory.Move(destination, backup);
            Restore(stage, bytes);
            File.WriteAllText(Path.Combine(stage, "incomplete.txt"), "staging must be discarded");
            // Use the production catalog with a fixture root without opening an editor-facing API for its constructor.
            Type catalogType = typeof(ShelteredScenarioPackages).Assembly.GetType("ShelteredAPI.Scenarios.Definitions.ScenarioCatalog", true);
            object catalog = Activator.CreateInstance(catalogType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { null, null, root }, null);
            MethodInfo refresh = catalogType.GetMethod("Refresh");
            MethodInfo list = catalogType.GetMethod("ListAll");
            refresh.Invoke(catalog, null);
            ScenarioInfo[] scenarios = (ScenarioInfo[])list.Invoke(catalog, null);
            Assert(scenarios.Length == 1 && Matches(destination, bytes) && !Directory.Exists(stage) && !Directory.Exists(backup),
                "Catalog startup did not restore the last installed package before discovering it.", result);

            File.WriteAllText(TransactionFile(destination, "txn"), "");
            Restore(backup, bytes);
            string backupDefinitionPath = Path.Combine(backup, ScenarioEditorDefinitionSerializer.DefaultFileName);
            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            ScenarioDefinition backupDefinition = serializer.Load(backupDefinitionPath);
            backupDefinition.Id = "backup.only";
            serializer.Save(backupDefinition, backupDefinitionPath);
            using (FileStream locked = new FileStream(Path.Combine(backup, @"Assets\icon.png"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                refresh.Invoke(catalog, null);
                Assert(((ScenarioInfo[])list.Invoke(catalog, null)).Length == 1 && Matches(destination, bytes) && Directory.Exists(backup),
                    "The catalog did not preserve the committed package while backup cleanup was locked.", result);
            }
            refresh.Invoke(catalog, null);
            Assert(!Directory.Exists(backup) && Matches(destination, bytes), "Catalog retry did not remove the unlocked backup.", result);
        }

        private static string TransactionFile(string destination, string suffix)
        {
            return Path.Combine(Path.GetDirectoryName(destination), "." + Path.GetFileName(destination) + ".smm-package-" + suffix);
        }

        private static Dictionary<string, string> Snapshot(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
                path => path.Substring(root.Length + 1), path => Convert.ToBase64String(File.ReadAllBytes(path)), StringComparer.OrdinalIgnoreCase);
        }

        private static bool Matches(string root, Dictionary<string, string> expected)
        {
            if (!Directory.Exists(root)) return false;
            Dictionary<string, string> actual = Snapshot(root);
            return actual.Count == expected.Count && expected.All(file => actual.ContainsKey(file.Key) && actual[file.Key] == file.Value);
        }

        private static void Restore(string root, Dictionary<string, string> files)
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            foreach (KeyValuePair<string, string> file in files)
            {
                string path = Path.Combine(root, file.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, Convert.FromBase64String(file.Value));
            }
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
