using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioPublishExportService
    {
        private const string ExportOwnerModId = "ShelteredAPI";
        private const string ExportRootFolder = "ScenarioAuthoringExports";

        private readonly IScenarioEditorService _editorService;
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly object _sync = new object();
        private ScenarioPublishExportResult _lastResult;

        public ScenarioPublishExportService(
            IScenarioEditorService editorService,
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionValidator validator,
            IScenarioDefinitionCatalogService catalog)
        {
            _editorService = editorService;
            _serializer = serializer;
            _validator = validator;
        }

        public ScenarioPublishExportResult LastResult
        {
            get
            {
                lock (_sync)
                {
                    return _lastResult != null ? _lastResult.Copy() : null;
                }
            }
        }

        public ScenarioPublishExportResult ExportActiveDraft(ScenarioAuthoringState state)
        {
            ScenarioDefinition definition = GetActiveDefinition();
            if (definition == null)
                return Remember(ScenarioPublishExportResult.Failed("No active scenario definition is available."));

            ScenarioValidationResult validation = Validate(definition, state != null ? state.ActiveScenarioFilePath : null);
            int errorCount = CountErrors(validation);
            if (validation == null)
                return Remember(ScenarioPublishExportResult.Failed("Validation could not run; export was not created."));
            if (errorCount > 0)
                return Remember(ScenarioPublishExportResult.BlockedResult(errorCount, FirstIssueMessage(validation)));

            string exportFilePath;
            string exportRoot;
            DateTime? replacedTimestampUtc = null;
            try
            {
                exportRoot = ResolveExportRoot(definition);
                exportFilePath = Path.Combine(exportRoot, ScenarioDefinitionSerializer.DefaultFileName);
                if (File.Exists(exportFilePath))
                    replacedTimestampUtc = File.GetLastWriteTimeUtc(exportFilePath);
                _serializer.Save(definition, exportFilePath);
                CopyReferencedAssets(definition, state != null ? state.ActiveScenarioFilePath : null, exportRoot);
            }
            catch (Exception ex)
            {
                return Remember(ScenarioPublishExportResult.Failed("Export failed: " + ex.Message));
            }

            ScenarioDefinition exported;
            try
            {
                exported = _serializer.Load(exportFilePath);
            }
            catch (Exception ex)
            {
                return Remember(ScenarioPublishExportResult.Failed("Post-export load failed: " + ex.Message, exportFilePath));
            }

            ScenarioValidationResult exportedValidation = Validate(exported, exportFilePath);
            int exportedErrors = CountErrors(exportedValidation);
            if (exportedValidation == null || exportedErrors > 0)
            {
                return Remember(ScenarioPublishExportResult.Failed(
                    "Post-export validation failed: " + FirstIssueMessage(exportedValidation),
                    exportFilePath));
            }

            return Remember(ScenarioPublishExportResult.Succeeded(exportFilePath, exportRoot, CountWarnings(exportedValidation), replacedTimestampUtc));
        }

        private ScenarioPublishExportResult Remember(ScenarioPublishExportResult result)
        {
            lock (_sync)
            {
                _lastResult = result != null ? result.Copy() : null;
                return result;
            }
        }

        private ScenarioDefinition GetActiveDefinition()
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            return session != null ? session.WorkingDefinition : null;
        }

        private ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            try
            {
                return _validator != null ? _validator.Validate(definition, scenarioFilePath) : null;
            }
            catch (Exception ex)
            {
                ScenarioValidationResult failed = new ScenarioValidationResult();
                failed.AddError("Validation threw an exception: " + ex.Message);
                return failed;
            }
        }

        private static string ResolveExportRoot(ScenarioDefinition definition)
        {
            string modRoot = ResolveExportModRoot();
            string scenarioFolder = BuildSafeFolderName(!string.IsNullOrEmpty(definition.Id) ? definition.Id : definition.DisplayName);
            return Path.Combine(Path.Combine(modRoot, ExportRootFolder), scenarioFolder);
        }

        private static void CopyReferencedAssets(ScenarioDefinition definition, string sourceScenarioFilePath, string exportRoot)
        {
            if (definition == null || definition.AssetReferences == null || string.IsNullOrEmpty(exportRoot))
                return;

            List<string> relativePaths = new List<string>();
            AddAssetReferencePaths(definition, relativePaths);
            if (relativePaths.Count == 0)
                return;

            string draftRoot = !string.IsNullOrEmpty(sourceScenarioFilePath) ? Path.GetDirectoryName(sourceScenarioFilePath) : null;
            string assetsRoot = ScenarioAuthoringStoragePaths.GetAssetsRootPath();
            for (int i = 0; i < relativePaths.Count; i++)
                CopyAssetIfFound(relativePaths[i], draftRoot, assetsRoot, exportRoot);
        }

        private static void AddAssetReferencePaths(ScenarioDefinition definition, List<string> paths)
        {
            if (definition.AssetReferences.CustomSprites != null)
            {
                for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
                    AddRelativePath(paths, definition.AssetReferences.CustomSprites[i] != null ? definition.AssetReferences.CustomSprites[i].RelativePath : null);
            }

            if (definition.AssetReferences.CustomIcons != null)
            {
                for (int i = 0; i < definition.AssetReferences.CustomIcons.Count; i++)
                    AddRelativePath(paths, definition.AssetReferences.CustomIcons[i] != null ? definition.AssetReferences.CustomIcons[i].RelativePath : null);
            }

            if (definition.AssetReferences.SpriteSwaps != null)
            {
                for (int i = 0; i < definition.AssetReferences.SpriteSwaps.Count; i++)
                    AddRelativePath(paths, definition.AssetReferences.SpriteSwaps[i] != null ? definition.AssetReferences.SpriteSwaps[i].RelativePath : null);
            }

            if (definition.AssetReferences.SceneSpritePlacements != null)
            {
                for (int i = 0; i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
                    AddRelativePath(paths, definition.AssetReferences.SceneSpritePlacements[i] != null ? definition.AssetReferences.SceneSpritePlacements[i].RelativePath : null);
            }

            if (definition.AssetReferences.SpritePatches != null)
            {
                for (int i = 0; i < definition.AssetReferences.SpritePatches.Count; i++)
                    AddRelativePath(paths, definition.AssetReferences.SpritePatches[i] != null ? definition.AssetReferences.SpritePatches[i].BaseRelativePath : null);
            }
        }

        private static void AddRelativePath(List<string> paths, string relativePath)
        {
            if (paths == null || string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
                return;

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], relativePath, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            paths.Add(relativePath);
        }

        private static void CopyAssetIfFound(string relativePath, string draftRoot, string assetsRoot, string exportRoot)
        {
            string source = ResolveSourceAsset(relativePath, draftRoot, assetsRoot);
            if (string.IsNullOrEmpty(source) || !File.Exists(source))
                return;

            string destination = ResolveExportAssetPath(exportRoot, relativePath);
            if (string.IsNullOrEmpty(destination))
                return;

            string directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.Copy(source, destination, true);
        }

        private static string ResolveSourceAsset(string relativePath, string draftRoot, string assetsRoot)
        {
            string source = ResolveContainedPath(draftRoot, relativePath);
            if (!string.IsNullOrEmpty(source) && File.Exists(source))
                return source;
            source = ResolveContainedPath(assetsRoot, relativePath);
            return !string.IsNullOrEmpty(source) && File.Exists(source) ? source : null;
        }

        private static string ResolveExportAssetPath(string exportRoot, string relativePath)
        {
            return ResolveContainedPath(exportRoot, relativePath);
        }

        private static string ResolveContainedPath(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
                return null;

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath;

            string rootedPrefix = fullRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootedPrefix, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        private static string ResolveExportModRoot()
        {
            ModEntry preferred = ModRegistry.GetMod(ExportOwnerModId);
            if (preferred != null && !string.IsNullOrEmpty(preferred.RootPath))
                return preferred.RootPath;

            List<ModEntry> loaded = ModRegistry.GetLoadedMods();
            for (int i = 0; loaded != null && i < loaded.Count; i++)
            {
                if (loaded[i] != null && !string.IsNullOrEmpty(loaded[i].RootPath))
                    return loaded[i].RootPath;
            }

            string gameRoot;
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                gameRoot = !string.IsNullOrEmpty(location)
                    ? Path.GetFullPath(Path.Combine(Path.Combine(Path.GetDirectoryName(location), ".."), ".."))
                    : Directory.GetCurrentDirectory();
            }
            catch
            {
                gameRoot = Directory.GetCurrentDirectory();
            }

            return Path.Combine(Path.Combine(gameRoot, "mods"), "ModAPI");
        }

        private static string BuildSafeFolderName(string value)
        {
            string raw = string.IsNullOrEmpty(value) ? "scenario" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            List<char> chars = new List<char>();
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }

                chars.Add(bad || char.IsWhiteSpace(c) ? '_' : c);
            }

            string safe = new string(chars.ToArray()).Trim('_', '.');
            return string.IsNullOrEmpty(safe) ? "scenario" : safe;
        }

        private static int CountErrors(ScenarioValidationResult validation)
        {
            return CountIssues(validation, ScenarioIssueSeverity.Error);
        }

        private static int CountWarnings(ScenarioValidationResult validation)
        {
            return CountIssues(validation, ScenarioIssueSeverity.Warning);
        }

        private static int CountIssues(ScenarioValidationResult validation, ScenarioIssueSeverity severity)
        {
            int count = 0;
            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : null;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                if (issues[i] != null && issues[i].Severity == severity)
                    count++;
            }

            return count;
        }

        private static string FirstIssueMessage(ScenarioValidationResult validation)
        {
            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : null;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                if (issues[i] != null && !string.IsNullOrEmpty(issues[i].Message))
                    return issues[i].Message;
            }

            return "Unknown validation issue.";
        }
    }

    internal sealed class ScenarioPublishExportResult
    {
        public bool Success { get; private set; }
        public bool Blocked { get; private set; }
        public string Message { get; private set; }
        public string ArtifactPath { get; private set; }
        public string ArtifactRootPath { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public DateTime TimestampUtc { get; private set; }
        public DateTime? ReplacedTimestampUtc { get; private set; }

        public ScenarioPublishExportResult Copy()
        {
            return new ScenarioPublishExportResult
            {
                Success = Success,
                Blocked = Blocked,
                Message = Message,
                ArtifactPath = ArtifactPath,
                ArtifactRootPath = ArtifactRootPath,
                ErrorCount = ErrorCount,
                WarningCount = WarningCount,
                TimestampUtc = TimestampUtc,
                ReplacedTimestampUtc = ReplacedTimestampUtc
            };
        }

        public string FormatTimestamp()
        {
            return TimestampUtc == DateTime.MinValue
                ? "<none>"
                : TimestampUtc.ToString("u", CultureInfo.InvariantCulture);
        }

        public static ScenarioPublishExportResult Succeeded(string artifactPath, string artifactRootPath, int warningCount, DateTime? replacedTimestampUtc)
        {
            string message = "Export package created and validated. To install or share it, copy this folder into any mod's Scenarios directory.";
            if (replacedTimestampUtc.HasValue)
            {
                message += " Replaced previous export from "
                    + replacedTimestampUtc.Value.ToString("u", CultureInfo.InvariantCulture)
                    + ".";
            }

            return new ScenarioPublishExportResult
            {
                Success = true,
                Message = message,
                ArtifactPath = artifactPath,
                ArtifactRootPath = artifactRootPath,
                WarningCount = warningCount,
                TimestampUtc = DateTime.UtcNow,
                ReplacedTimestampUtc = replacedTimestampUtc
            };
        }

        public static ScenarioPublishExportResult BlockedResult(int errorCount, string reason)
        {
            return new ScenarioPublishExportResult
            {
                Blocked = true,
                Message = "Export blocked by validation errors: " + (reason ?? "Unknown validation issue."),
                ErrorCount = errorCount,
                TimestampUtc = DateTime.UtcNow
            };
        }

        public static ScenarioPublishExportResult Failed(string message)
        {
            return Failed(message, null);
        }

        public static ScenarioPublishExportResult Failed(string message, string artifactPath)
        {
            return new ScenarioPublishExportResult
            {
                Message = string.IsNullOrEmpty(message) ? "Export failed." : message,
                ArtifactPath = artifactPath,
                TimestampUtc = DateTime.UtcNow
            };
        }
    }
}
