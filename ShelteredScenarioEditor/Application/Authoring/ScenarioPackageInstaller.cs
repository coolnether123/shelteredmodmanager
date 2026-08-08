using ShelteredScenarioEditor.Application.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed class ScenarioPackageOperationResult
    {
        public bool Success { get; set; }
        public bool ConfirmationRequired { get; set; }
        public string InstallPath { get; set; }
        public string Message { get; set; }
    }

    internal sealed class ScenarioPackageInstaller
    {
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionCatalogService _catalog;
        private readonly string _installScenariosRoot;

        public ScenarioPackageInstaller(IScenarioDefinitionSerializer serializer, IScenarioDefinitionCatalogService catalog)
            : this(serializer, catalog, ModApiPaths.ScenarioPackagesRoot)
        {
        }

        internal ScenarioPackageInstaller(
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionCatalogService catalog,
            string installScenariosRoot)
        {
            if (serializer == null) throw new ArgumentNullException("serializer");
            if (string.IsNullOrEmpty(installScenariosRoot)) throw new ArgumentException("A scenario package root is required.", "installScenariosRoot");
            _serializer = serializer;
            _catalog = catalog;
            _installScenariosRoot = Path.GetFullPath(installScenariosRoot);
        }

        public ScenarioPackageOperationResult Install(string exportRoot, bool overwriteConfirmed)
        {
            string sourceScenario = !string.IsNullOrEmpty(exportRoot) ? Path.Combine(exportRoot, ScenarioEditorDefinitionSerializer.DefaultFileName) : null;
            if (string.IsNullOrEmpty(sourceScenario) || !File.Exists(sourceScenario)) return Failed("Export scenario.xml was not found.");
            ScenarioDefinition definition = _serializer.Load(sourceScenario);
            string safeId = BuildSafeFolderName(definition.Id);
            string destination = Path.Combine(_installScenariosRoot, safeId);
            string existingScenario = Path.Combine(destination, ScenarioEditorDefinitionSerializer.DefaultFileName);
            if (File.Exists(existingScenario) && !FilesEqual(sourceScenario, existingScenario) && !overwriteConfirmed)
            {
                return new ScenarioPackageOperationResult
                {
                    ConfirmationRequired = true,
                    InstallPath = destination,
                    Message = "A different installed scenario package already uses ID '" + definition.Id + "'. Confirm replacement to continue."
                };
            }

            CopyDirectory(exportRoot, destination);
            if (_catalog != null) _catalog.RefreshDefinitionCatalog();
            return new ScenarioPackageOperationResult
            {
                Success = true,
                InstallPath = destination,
                Message = "Installed - it will appear in the scenario book as " + (definition.DisplayName ?? definition.Id) + ". Reopen or refresh the scenario book; restart the game if its catalog was already cached."
            };
        }

        public ScenarioPackageOperationResult Uninstall(string expectedScenarioId, string installPath)
        {
            if (string.IsNullOrEmpty(expectedScenarioId) || string.IsNullOrEmpty(installPath))
                return Failed("Choose an installed scenario package to uninstall.", installPath);

            string normalizedInstallPath;
            try { normalizedInstallPath = Path.GetFullPath(installPath); }
            catch (Exception ex) { return Failed("Uninstall refused an invalid package path: " + ex.Message, installPath); }

            if (!string.Equals(Path.GetDirectoryName(normalizedInstallPath), _installScenariosRoot, StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(normalizedInstallPath))
            {
                return Failed("Uninstall refused a package outside the user scenario package root.", normalizedInstallPath);
            }

            string installedScenarioPath = Path.Combine(normalizedInstallPath, ScenarioEditorDefinitionSerializer.DefaultFileName);
            ScenarioDefinition installedDefinition;
            try { installedDefinition = _serializer.Load(installedScenarioPath); }
            catch (Exception ex) { return Failed("Could not verify the installed package: " + ex.Message, normalizedInstallPath); }

            if (installedDefinition == null
                || !string.Equals(installedDefinition.Id, expectedScenarioId, StringComparison.OrdinalIgnoreCase))
            {
                return Failed("Uninstall refused because the installed package identity did not match the expected scenario.", normalizedInstallPath);
            }

            try
            {
                ShelteredScenarioEditor.Infrastructure.Persistence.ScenarioEditorDefinitionMetadataCache.InvalidateUnder(normalizedInstallPath);
                Directory.Delete(normalizedInstallPath, true);
                if (_catalog != null) _catalog.RefreshDefinitionCatalog();
                return new ScenarioPackageOperationResult
                {
                    Success = true,
                    InstallPath = normalizedInstallPath,
                    Message = "Uninstalled " + (installedDefinition.DisplayName ?? installedDefinition.Id)
                        + ". Saved runs and authoring drafts were retained."
                };
            }
            catch (UnauthorizedAccessException)
            {
                return Failed("Uninstall failed because the installed package folder is read-only.", normalizedInstallPath);
            }
            catch (Exception ex)
            {
                return Failed("Uninstall failed while deleting the installed package: " + ex.Message, normalizedInstallPath);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);
            string sourceRoot = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
            string destinationRoot = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
            string[] existing = Directory.GetFiles(destinationRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < existing.Length; i++)
            {
                string relative = existing[i].Substring(destinationRoot.Length + 1);
                string correspondingSource = ScenarioPackagePlan.ResolveContainedPath(sourceRoot, relative);
                if (string.IsNullOrEmpty(correspondingSource) || !File.Exists(correspondingSource)) File.Delete(existing[i]);
            }
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(sourceRoot.Length + 1);
                string target = ScenarioPackagePlan.ResolveContainedPath(destinationRoot, relative);
                string directory = Path.GetDirectoryName(target);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.Copy(files[i], target, true);
            }
        }

        private static bool FilesEqual(string left, string right)
        {
            byte[] a = File.ReadAllBytes(left); byte[] b = File.ReadAllBytes(right);
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static string BuildSafeFolderName(string value)
        {
            string raw = string.IsNullOrEmpty(value) ? "scenario" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars(); List<char> result = new List<char>();
            for (int i = 0; i < raw.Length; i++) result.Add(IsInvalid(raw[i], invalid) || char.IsWhiteSpace(raw[i]) ? '_' : raw[i]);
            string safe = new string(result.ToArray()).Trim('_', '.'); return string.IsNullOrEmpty(safe) ? "scenario" : safe;
        }

        private static bool IsInvalid(char value, char[] invalid) { for (int i = 0; i < invalid.Length; i++) if (invalid[i] == value) return true; return false; }
        private static ScenarioPackageOperationResult Failed(string message) { return Failed(message, null); }
        private static ScenarioPackageOperationResult Failed(string message, string path)
        {
            return new ScenarioPackageOperationResult { Message = message, InstallPath = path };
        }

    }
}
