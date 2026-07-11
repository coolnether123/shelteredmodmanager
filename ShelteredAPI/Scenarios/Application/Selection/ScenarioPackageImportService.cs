using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Selection
{
    internal sealed class ScenarioPackageImportCandidate
    {
        public string PackageRoot;
        public string ScenarioFilePath;
        public string ScenarioId;
        public string DisplayName;
        public string Author;
        public string Version;
        public ScenarioBaseGameMode BaseGameMode;
        public bool CanInstall;
        public bool IsAlreadyInstalled;
        public string InstallPath;
        public string FailureReason;
    }

    internal sealed class ScenarioPackageImportScanResult
    {
        public ScenarioPackageImportCandidate[] Candidates = new ScenarioPackageImportCandidate[0];
        public string Error;
    }

    internal sealed class ScenarioPackageImportResult
    {
        public bool Success;
        public string Message;
        public string InstallPath;
    }

    internal sealed class ScenarioPackageImportService
    {
        internal const string StagingFolderName = "ScenarioDownloads";

        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly IScenarioDefinitionCatalogService _catalog;

        public ScenarioPackageImportService(
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionValidator validator,
            IScenarioDefinitionCatalogService catalog)
        {
            if (serializer == null) throw new ArgumentNullException("serializer");
            if (validator == null) throw new ArgumentNullException("validator");
            if (catalog == null) throw new ArgumentNullException("catalog");

            _serializer = serializer;
            _validator = validator;
            _catalog = catalog;
        }

        public string StagingRoot
        {
            get { return Path.GetFullPath(Path.Combine(ModApiPaths.UserRoot, StagingFolderName)); }
        }

        public string InstalledScenariosRoot
        {
            get { return ScenarioPackageModRootResolver.ResolveScenariosRoot(typeof(ScenarioPackageImportService).Assembly); }
        }

        public ScenarioPackageImportScanResult Scan()
        {
            ScenarioPackageImportScanResult result = new ScenarioPackageImportScanResult();
            try
            {
                Directory.CreateDirectory(StagingRoot);
                List<string> roots = DiscoverPackageRoots();
                List<ScenarioPackageImportCandidate> candidates = new List<ScenarioPackageImportCandidate>();
                for (int i = 0; i < roots.Count; i++)
                    candidates.Add(ReadCandidate(roots[i]));

                candidates.Sort(CompareCandidates);
                result.Candidates = candidates.ToArray();
            }
            catch (Exception ex)
            {
                result.Error = "Could not scan downloaded scenarios: " + PlayerMessage(ex);
            }

            return result;
        }

        public ScenarioPackageImportResult Install(ScenarioPackageImportCandidate candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.PackageRoot))
                return Failed("Choose a scenario package to install.");

            ScenarioPackageImportCandidate current;
            try
            {
                current = ReadCandidate(candidate.PackageRoot);
            }
            catch (Exception ex)
            {
                return Failed("Could not read this package: " + PlayerMessage(ex));
            }

            if (!current.CanInstall)
                return Failed(current.FailureReason ?? "This package cannot be installed.", current.InstallPath);

            string temporary = current.InstallPath + ".installing-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(InstalledScenariosRoot);
                if (Directory.Exists(current.InstallPath) || File.Exists(current.InstallPath))
                    return Failed("This scenario is already installed at " + current.InstallPath + ".", current.InstallPath);

                CopyDirectory(current.PackageRoot, temporary);
                Directory.Move(temporary, current.InstallPath);
                ScenarioDefinitionMetadataCache.InvalidateUnder(current.InstallPath);
                _catalog.RefreshDefinitionCatalog();
                return new ScenarioPackageImportResult
                {
                    Success = true,
                    InstallPath = current.InstallPath,
                    Message = "Installed " + DisplayName(current) + " - find it under " + SectionName(current.BaseGameMode) + "."
                };
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteTemporary(temporary);
                return Failed("Install failed because the scenario folder is read-only. Check its permissions and try again.");
            }
            catch (IOException ex)
            {
                TryDeleteTemporary(temporary);
                if (Directory.Exists(current.InstallPath) || File.Exists(current.InstallPath))
                    return Failed("This scenario is already installed at " + current.InstallPath + ".", current.InstallPath);
                return Failed("Install failed while copying files: " + PlayerMessage(ex));
            }
            catch (Exception ex)
            {
                TryDeleteTemporary(temporary);
                return Failed("Install failed: " + PlayerMessage(ex));
            }
        }

        public bool OpenFolder(string path, out string message)
        {
            message = null;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    message = "No folder is available yet.";
                    return false;
                }

                Directory.CreateDirectory(path);
                Process.Start(path);
                message = "Opened " + path + ".";
                return true;
            }
            catch (Exception ex)
            {
                message = "Could not open the folder: " + PlayerMessage(ex);
                return false;
            }
        }

        private List<string> DiscoverPackageRoots()
        {
            List<string> roots = new List<string>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            AddScenarioFilesUnder(StagingRoot, roots, seen, SearchOption.AllDirectories);
            string[] stagedFolders;
            try { stagedFolders = Directory.GetDirectories(StagingRoot, "*", SearchOption.TopDirectoryOnly); }
            catch { stagedFolders = new string[0]; }
            for (int i = 0; i < stagedFolders.Length; i++)
            {
                string[] contained;
                try { contained = Directory.GetFiles(stagedFolders[i], ScenarioDefinitionSerializer.DefaultFileName, SearchOption.AllDirectories); }
                catch { contained = new string[0]; }
                if (contained.Length == 0)
                {
                    string emptyPackage = Normalize(stagedFolders[i]);
                    if (!seen.ContainsKey(emptyPackage))
                    {
                        seen[emptyPackage] = true;
                        roots.Add(emptyPackage);
                    }
                }
            }

            string ownerRoot = ScenarioPackageModRootResolver.ResolveLoadedOwnerRoot(typeof(ScenarioPackageImportService).Assembly);
            AddScenarioFilesUnder(Path.Combine(ownerRoot, "ScenarioAuthoringExports"), roots, seen, SearchOption.AllDirectories);

            string[] modFolders = Directory.Exists(ModApiPaths.ModsRoot)
                ? Directory.GetDirectories(ModApiPaths.ModsRoot, "*", SearchOption.TopDirectoryOnly)
                : new string[0];
            string installedRoot = Normalize(InstalledScenariosRoot);
            for (int i = 0; i < modFolders.Length; i++)
            {
                string folder = Normalize(modFolders[i]);
                if (IsWithin(folder, installedRoot))
                    continue;
                AddScenarioFilesUnder(Path.Combine(folder, "ScenarioAuthoringExports"), roots, seen, SearchOption.AllDirectories);
                AddScenarioFile(Path.Combine(folder, ScenarioDefinitionSerializer.DefaultFileName), roots, seen);
            }

            return roots;
        }

        private ScenarioPackageImportCandidate ReadCandidate(string packageRoot)
        {
            ScenarioPackageImportCandidate candidate = new ScenarioPackageImportCandidate();
            candidate.PackageRoot = Path.GetFullPath(packageRoot);
            candidate.ScenarioFilePath = Path.Combine(candidate.PackageRoot, ScenarioDefinitionSerializer.DefaultFileName);
            if (!File.Exists(candidate.ScenarioFilePath))
            {
                candidate.FailureReason = "This folder does not contain scenario.xml.";
                return candidate;
            }

            ScenarioDefinitionMetadata metadata;
            try
            {
                if (!ScenarioDefinitionMetadataCache.TryLoad(_serializer, candidate.ScenarioFilePath, null, out metadata)
                    || metadata == null || metadata.Info == null)
                {
                    candidate.FailureReason = "scenario.xml could not be read. The package may be damaged.";
                    return candidate;
                }

                candidate.ScenarioId = metadata.Info.Id;
                candidate.DisplayName = metadata.Info.DisplayName;
                candidate.Author = metadata.Info.Author;
                candidate.Version = metadata.Info.Version;
                candidate.BaseGameMode = metadata.BaseGameMode;
            }
            catch (Exception ex)
            {
                candidate.FailureReason = "scenario.xml could not be read: " + PlayerMessage(ex);
                return candidate;
            }

            if (string.IsNullOrEmpty(candidate.ScenarioId))
            {
                candidate.FailureReason = "This package has no scenario identity.";
                return candidate;
            }

            string safeId = BuildSafeFolderName(candidate.ScenarioId);
            if (!string.Equals(safeId, candidate.ScenarioId.Trim(), StringComparison.Ordinal))
            {
                candidate.FailureReason = "This package has an unsafe scenario identity and cannot be installed.";
                return candidate;
            }

            candidate.InstallPath = Path.Combine(InstalledScenariosRoot, safeId);
            string existingInstallPath;
            if (TryFindInstalledScenario(candidate.ScenarioId, out existingInstallPath))
            {
                candidate.IsAlreadyInstalled = true;
                candidate.InstallPath = existingInstallPath;
                candidate.FailureReason = "Already installed at " + candidate.InstallPath + ".";
                return candidate;
            }

            try
            {
                ScenarioDefinition definition = _serializer.Load(candidate.ScenarioFilePath);
                ScenarioValidationResult validation = _validator.Validate(definition, candidate.ScenarioFilePath);
                string validationError = FirstValidationError(validation);
                if (!string.IsNullOrEmpty(validationError))
                {
                    candidate.FailureReason = "Cannot install: " + validationError;
                    return candidate;
                }
            }
            catch (Exception ex)
            {
                candidate.FailureReason = "Validation could not read this package: " + PlayerMessage(ex);
                return candidate;
            }

            candidate.CanInstall = true;
            return candidate;
        }

        private static void AddScenarioFilesUnder(string root, List<string> roots, Dictionary<string, bool> seen, SearchOption option)
        {
            if (!Directory.Exists(root))
                return;

            string[] files;
            try { files = Directory.GetFiles(root, ScenarioDefinitionSerializer.DefaultFileName, option); }
            catch { return; }
            for (int i = 0; i < files.Length; i++)
                AddScenarioFile(files[i], roots, seen);
        }

        private static void AddScenarioFile(string scenarioFile, List<string> roots, Dictionary<string, bool> seen)
        {
            if (!File.Exists(scenarioFile))
                return;
            string root = Normalize(Path.GetDirectoryName(scenarioFile));
            if (string.IsNullOrEmpty(root) || seen.ContainsKey(root))
                return;
            seen[root] = true;
            roots.Add(root);
        }

        private static void CopyDirectory(string source, string destination)
        {
            string sourceRoot = Normalize(source);
            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(sourceRoot.Length + 1);
                string target = ScenarioPackagePlan.ResolveContainedPath(destination, relative);
                if (string.IsNullOrEmpty(target))
                    throw new IOException("The package contains an unsafe file path.");
                string directory = Path.GetDirectoryName(target);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.Copy(files[i], target, false);
            }
        }

        private static string FirstValidationError(ScenarioValidationResult validation)
        {
            if (validation == null)
                return "validation did not run.";
            ScenarioValidationIssue[] issues = validation.Issues;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Error)
                    return string.IsNullOrEmpty(issues[i].Message) ? "the package has validation errors." : issues[i].Message;
            }
            return null;
        }

        private bool TryFindInstalledScenario(string scenarioId, out string installPath)
        {
            installPath = null;
            try
            {
                ScenarioInfo[] definitions = _catalog.ListDefinitions();
                string installedRoot = Normalize(InstalledScenariosRoot);
                for (int i = 0; definitions != null && i < definitions.Length; i++)
                {
                    ScenarioInfo definition = definitions[i];
                    if (definition == null
                        || !string.Equals(definition.Id, scenarioId, StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrEmpty(definition.FilePath))
                    {
                        continue;
                    }

                    string definitionFolder = Normalize(Path.GetDirectoryName(definition.FilePath));
                    if (IsWithin(definitionFolder, installedRoot))
                    {
                        installPath = definitionFolder;
                        return true;
                    }
                }
            }
            catch
            {
            }

            string expected = Path.Combine(InstalledScenariosRoot, BuildSafeFolderName(scenarioId));
            if (Directory.Exists(expected) || File.Exists(expected))
            {
                installPath = expected;
                return true;
            }
            return false;
        }

        private static string SectionName(ScenarioBaseGameMode mode)
        {
            if (mode == ScenarioBaseGameMode.Surrounded) return "Surrounded";
            if (mode == ScenarioBaseGameMode.Stasis) return "Stasis";
            return "Custom Scenarios";
        }

        private static string DisplayName(ScenarioPackageImportCandidate candidate)
        {
            return candidate != null && !string.IsNullOrEmpty(candidate.DisplayName) ? candidate.DisplayName : "the scenario";
        }

        private static int CompareCandidates(ScenarioPackageImportCandidate left, ScenarioPackageImportCandidate right)
        {
            return string.Compare(DisplayName(left), DisplayName(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSafeFolderName(string value)
        {
            string raw = string.IsNullOrEmpty(value) ? "scenario" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            List<char> result = new List<char>();
            for (int i = 0; i < raw.Length; i++)
            {
                bool bad = char.IsWhiteSpace(raw[i]);
                for (int j = 0; !bad && j < invalid.Length; j++) bad = raw[i] == invalid[j];
                result.Add(bad ? '_' : raw[i]);
            }
            string safe = new string(result.ToArray()).Trim('_', '.');
            return string.IsNullOrEmpty(safe) ? "scenario" : safe;
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? null : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsWithin(string path, string parent)
        {
            return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(parent)
                && (string.Equals(path, parent, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        private static string PlayerMessage(Exception ex)
        {
            return ex == null || string.IsNullOrEmpty(ex.Message) ? "unknown file error" : ex.Message;
        }

        private static ScenarioPackageImportResult Failed(string message)
        {
            return Failed(message, null);
        }

        private static ScenarioPackageImportResult Failed(string message, string path)
        {
            return new ScenarioPackageImportResult { Message = message, InstallPath = path };
        }

        private static void TryDeleteTemporary(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }
    }
}
