using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    internal sealed class ScenarioPackageInstallResult
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

        public ScenarioPackageInstaller(IScenarioDefinitionSerializer serializer, IScenarioDefinitionCatalogService catalog)
        {
            _serializer = serializer;
            _catalog = catalog;
        }

        public ScenarioPackageInstallResult Install(string exportRoot, string installScenariosRoot, bool overwriteConfirmed)
        {
            string sourceScenario = !string.IsNullOrEmpty(exportRoot) ? Path.Combine(exportRoot, ScenarioDefinitionSerializer.DefaultFileName) : null;
            if (string.IsNullOrEmpty(sourceScenario) || !File.Exists(sourceScenario)) return Failed("Export scenario.xml was not found.");
            ScenarioDefinition definition = _serializer.Load(sourceScenario);
            string safeId = BuildSafeFolderName(definition.Id);
            string root = !string.IsNullOrEmpty(installScenariosRoot) ? installScenariosRoot : ResolveDefaultScenariosRoot();
            string destination = Path.Combine(root, safeId);
            string existingScenario = Path.Combine(destination, ScenarioDefinitionSerializer.DefaultFileName);
            if (File.Exists(existingScenario) && !FilesEqual(sourceScenario, existingScenario) && !overwriteConfirmed)
            {
                return new ScenarioPackageInstallResult
                {
                    ConfirmationRequired = true,
                    InstallPath = destination,
                    Message = "A different installed scenario package already uses ID '" + definition.Id + "'. Confirm replacement to continue."
                };
            }

            CopyDirectory(exportRoot, destination);
            if (_catalog != null) _catalog.RefreshDefinitionCatalog();
            return new ScenarioPackageInstallResult
            {
                Success = true,
                InstallPath = destination,
                Message = "Installed - it will appear in the scenario book as " + (definition.DisplayName ?? definition.Id) + ". Reopen or refresh the scenario book; restart the game if its catalog was already cached."
            };
        }

        internal static string ResolveDefaultScenariosRoot()
        {
            ModEntry owner = ModRegistry.GetMod("ShelteredAPI");
            if (owner != null && !string.IsNullOrEmpty(owner.RootPath)) return Path.Combine(owner.RootPath, "Scenarios");
            List<ModEntry> loaded = ModRegistry.GetLoadedMods();
            for (int i = 0; loaded != null && i < loaded.Count; i++) if (loaded[i] != null && !string.IsNullOrEmpty(loaded[i].RootPath)) return Path.Combine(loaded[i].RootPath, "Scenarios");
            throw new InvalidOperationException("No loaded mod root is available for local installation.");
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
        private static ScenarioPackageInstallResult Failed(string message) { return new ScenarioPackageInstallResult { Message = message }; }

    }
}
