using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioPublishExportService
    {
        private const string ExportRootFolder = "ScenarioAuthoringExports";

        private readonly IScenarioEditorService _editorService;
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly ScenarioPackagePlanner _planner;
        private readonly ScenarioPackageInstaller _installer;
        private readonly object _sync = new object();
        private ScenarioPublishExportResult _lastResult;
        private ScenarioPackageInstallResult _lastInstallResult;

        public ScenarioPublishExportService(
            IScenarioEditorService editorService,
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionValidator validator,
            IScenarioDefinitionCatalogService catalog)
        {
            _editorService = editorService;
            _serializer = serializer;
            _validator = validator;
            _planner = new ScenarioPackagePlanner(serializer);
            _installer = new ScenarioPackageInstaller(serializer, catalog);
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

        public ScenarioPackageInstallResult LastInstallResult { get { lock (_sync) { return _lastInstallResult; } } }

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
                ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(state != null ? state.ActiveScenarioFilePath : null);
                ScenarioPackagePlan plan = _planner.Build(
                    definition,
                    state != null ? state.ActiveScenarioFilePath : null,
                    exportRoot,
                    preferences.IncludeReadme,
                    validation);
                plan.Write();
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

        public ScenarioPackagePlan PreviewActiveDraft(ScenarioAuthoringState state)
        {
            ScenarioDefinition definition = GetActiveDefinition();
            if (definition == null)
                return null;
            string scenarioPath = state != null ? state.ActiveScenarioFilePath : null;
            ScenarioValidationResult validation = Validate(definition, scenarioPath);
            ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(scenarioPath);
            return _planner.Build(definition, scenarioPath, ResolveExportRoot(definition), preferences.IncludeReadme, validation);
        }

        public ScenarioPackageInstallResult InstallLastExport(bool overwriteConfirmed)
        {
            ScenarioPublishExportResult last = LastResult;
            if (last == null || !last.Success || string.IsNullOrEmpty(last.ArtifactRootPath))
                return RememberInstall(new ScenarioPackageInstallResult { Message = "Create a validated export before installing it." });
            return RememberInstall(_installer.Install(last.ArtifactRootPath, null, overwriteConfirmed));
        }

        private ScenarioPackageInstallResult RememberInstall(ScenarioPackageInstallResult result) { lock (_sync) { _lastInstallResult = result; return result; } }

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

        private static string ResolveExportModRoot()
        {
            return ScenarioPackageModRootResolver.ResolveLoadedOwnerRoot(typeof(ScenarioPublishExportService).Assembly);
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
            string message = "Package ready. Export contents were created and validated; install locally or share the folder.";
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
