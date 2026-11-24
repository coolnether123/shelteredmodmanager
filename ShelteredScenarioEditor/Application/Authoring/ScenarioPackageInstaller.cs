using ShelteredScenarioEditor.Application.Runtime;
using System;
using System.IO;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;

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
        private readonly ScenarioPackageContents _packageContents;

        public ScenarioPackageInstaller(IScenarioDefinitionSerializer serializer, IScenarioDefinitionCatalogService catalog)
            : this(serializer, catalog, ModApiPaths.ScenarioPackagesRoot)
        {
        }

        internal ScenarioPackageInstaller(
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionCatalogService catalog,
            string installScenariosRoot,
            Action<ScenarioPackageStep, string> beforePackageStep = null)
        {
            if (serializer == null) throw new ArgumentNullException("serializer");
            if (string.IsNullOrEmpty(installScenariosRoot)) throw new ArgumentException("A scenario package root is required.", "installScenariosRoot");
            _serializer = serializer;
            _catalog = catalog;
            _installScenariosRoot = Path.GetFullPath(installScenariosRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _packageContents = new ScenarioPackageContents(serializer, new ScenarioEditorDefinitionValidator(), beforePackageStep);
        }

        public ScenarioPackageOperationResult Install(string exportRoot, bool overwriteConfirmed)
        {
            try
            {
                ScenarioPackageOperationResult result = ShelteredScenarioPackages.RunLocked(
                    delegate { return InstallPackage(exportRoot, overwriteConfirmed); });
                if (result.Success && _catalog != null)
                {
                    try { _catalog.RefreshDefinitionCatalog(); }
                    catch (Exception ex) { MMLog.WriteWarning("[Scenario package catalog refresh] " + ex.Message); }
                }
                return result;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[Scenario package install] " + ex.Message);
                return Failed("Could not complete the scenario install. Check that the package files are available and try again.");
            }
        }

        private ScenarioPackageOperationResult InstallPackage(string exportRoot, bool overwriteConfirmed)
        {
            string sourceScenario = !string.IsNullOrEmpty(exportRoot) ? Path.Combine(exportRoot, ScenarioEditorDefinitionSerializer.DefaultFileName) : null;
            if (string.IsNullOrEmpty(sourceScenario) || !File.Exists(sourceScenario)) return Failed("Export scenario.xml was not found.");
            ScenarioDefinition definition = _serializer.Load(sourceScenario);
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return Failed("The export has no scenario identity.");
            string safeId = ScenarioPackageContents.SafeFolderName(definition.Id);
            string destination = Path.Combine(_installScenariosRoot, safeId);
            ShelteredScenarioPackages.Recover(destination);
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

            _packageContents.Replace(destination, definition.Id, delegate { return ScenarioPackageContents.ReadDirectory(exportRoot); });
            return new ScenarioPackageOperationResult
            {
                Success = true,
                InstallPath = destination,
                Message = "Installed - it will appear in the scenario book as " + (definition.DisplayName ?? definition.Id) + ". Reopen or refresh the scenario book; restart the game if its catalog was already cached."
            };
        }

        public ScenarioPackageOperationResult Uninstall(string expectedScenarioId, string installPath)
        {
            return ShelteredScenarioPackages.RunLocked(delegate { return UninstallPackage(expectedScenarioId, installPath); });
        }

        private ScenarioPackageOperationResult UninstallPackage(string expectedScenarioId, string installPath)
        {
            if (string.IsNullOrEmpty(expectedScenarioId) || string.IsNullOrEmpty(installPath))
                return Failed("Choose an installed scenario package to uninstall.", installPath);

            string normalizedInstallPath;
            try { normalizedInstallPath = Path.GetFullPath(installPath); }
            catch (Exception ex) { return Failed("Uninstall refused an invalid package path: " + ex.Message, installPath); }

            if (!string.Equals(Path.GetDirectoryName(normalizedInstallPath), _installScenariosRoot, StringComparison.OrdinalIgnoreCase)
                || ShelteredScenarioPackages.IsWorkDirectory(normalizedInstallPath))
            {
                return Failed("Uninstall refused a package outside the user scenario package root.", normalizedInstallPath);
            }

            try { ShelteredScenarioPackages.Recover(normalizedInstallPath); }
            catch (IOException) { return Failed("Uninstall could not finish recovering the installed package. Try again after closing files in use.", installPath); }
            catch (UnauthorizedAccessException) { return Failed("Uninstall could not access the installed package.", installPath); }
            if (!Directory.Exists(normalizedInstallPath))
                return Failed("The installed package was not found.", installPath);

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

        private static bool FilesEqual(string left, string right)
        {
            byte[] a = File.ReadAllBytes(left); byte[] b = File.ReadAllBytes(right);
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static ScenarioPackageOperationResult Failed(string message) { return Failed(message, null); }
        private static ScenarioPackageOperationResult Failed(string message, string path)
        {
            return new ScenarioPackageOperationResult { Message = message, InstallPath = path };
        }

    }
}
