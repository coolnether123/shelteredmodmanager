using System;
using System.Globalization;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioPublishExportService
    {
        private const string ExportRootFolder = "ScenarioAuthoringExports";

        private readonly IScenarioEditorService _editorService;
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly ScenarioPackagePlanner _planner;
        private readonly ScenarioPackageContents _packageContents;
        private readonly ScenarioPackageInstaller _installer;
        private readonly ScenarioAuthorTestChecklistService _testChecklistService;
        private readonly IScenarioEditorSessionStore _sessionStore;
        private readonly string _exportsRoot;
        private readonly object _sync = new object();
        private ScenarioPublishExportResult _lastResult;
        private ScenarioPackageOperationResult _lastInstallResult;

        public ScenarioPublishExportService(
            IScenarioEditorService editorService,
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionValidator validator,
            ScenarioPackageInstaller installer,
            ScenarioAuthorTestChecklistService testChecklistService,
            IScenarioEditorSessionStore sessionStore,
            Action<ScenarioPackageStep, string> beforePackageStep = null,
            string exportsRoot = null)
        {
            if (installer == null) throw new ArgumentNullException("installer");
            _editorService = editorService;
            _serializer = serializer;
            _validator = validator;
            _testChecklistService = testChecklistService;
            _sessionStore = sessionStore;
            _exportsRoot = exportsRoot;
            _planner = new ScenarioPackagePlanner(serializer, testChecklistService);
            _packageContents = new ScenarioPackageContents(serializer, validator, beforePackageStep);
            _installer = installer;
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

        public ScenarioPackageOperationResult LastInstallResult { get { lock (_sync) { return _lastInstallResult; } } }

        public ScenarioPublishExportResult ExportActiveDraft(ScenarioAuthoringState state)
        {
            ScenarioDefinition definition = GetActiveDefinition();
            if (definition == null)
                return Remember(ScenarioPublishExportResult.Failed("No active scenario definition is available."));

            ScenarioValidationResult validation = Validate(definition, _sessionStore.CurrentFilePath);
            int errorCount = CountErrors(validation);
            if (validation == null)
                return Remember(ScenarioPublishExportResult.Failed("Validation could not run; export was not created."));
            if (errorCount > 0)
                return Remember(ScenarioPublishExportResult.BlockedResult(errorCount, FirstIssueMessage(validation)));

            try
            {
                string exportRoot = ResolveExportRoot(definition);
                string exportFilePath = Path.Combine(exportRoot, ScenarioEditorDefinitionSerializer.DefaultFileName);
                DateTime? replacedTimestampUtc = null;
                ScenarioValidationResult exportedValidation = _packageContents.Replace(exportRoot, definition.Id, delegate
                {
                    if (File.Exists(exportFilePath))
                        replacedTimestampUtc = File.GetLastWriteTimeUtc(exportFilePath);
                    ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(_sessionStore.CurrentFilePath);
                    return _planner.Build(definition, _sessionStore.CurrentFilePath, exportRoot, preferences.IncludeReadme,
                        validation, _editorService.CurrentSession).Entries;
                });
                return Remember(ScenarioPublishExportResult.Succeeded(exportFilePath, exportRoot,
                    CountWarnings(exportedValidation), replacedTimestampUtc));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[Scenario package export] " + ex.Message);
                return Remember(ScenarioPublishExportResult.Failed("Could not complete the scenario export. Check that the package files are available and try again."));
            }
        }

        public ScenarioPackagePlan PreviewActiveDraft(ScenarioAuthoringState state)
        {
            ScenarioDefinition definition = GetActiveDefinition();
            if (definition == null)
                return null;
            string scenarioPath = _sessionStore.CurrentFilePath;
            ScenarioValidationResult validation = Validate(definition, scenarioPath);
            ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(scenarioPath);
            ScenarioPackagePlan plan = _planner.Build(
                definition,
                scenarioPath,
                ResolveExportRoot(definition),
                preferences.IncludeReadme,
                validation,
                _editorService != null ? _editorService.CurrentSession : null);
            ScenarioPackagePlanner.FindOrphans(plan);
            return plan;
        }

        public ScenarioPackageOperationResult InstallLastExport(bool overwriteConfirmed)
        {
            ScenarioPublishExportResult last = LastResult;
            if (last == null || !last.Success || string.IsNullOrEmpty(last.ArtifactRootPath))
                return RememberInstall(new ScenarioPackageOperationResult { Message = "Create a validated export before installing it." });
            ScenarioPackageOperationResult result = _installer.Install(last.ArtifactRootPath, overwriteConfirmed);
            if (result != null && result.Success && _testChecklistService != null)
                _testChecklistService.MarkExportReinstalled(_editorService != null ? _editorService.CurrentSession : null);
            return RememberInstall(result);
        }

        public ScenarioPackageOperationResult UninstallLastExport()
        {
            ScenarioPublishExportResult export = LastResult;
            ScenarioPackageOperationResult install = LastInstallResult;
            if (export == null || !export.Success || string.IsNullOrEmpty(export.ArtifactPath)
                || install == null || string.IsNullOrEmpty(install.InstallPath))
            {
                return RememberInstall(new ScenarioPackageOperationResult { Message = "Install a validated export before uninstalling it." });
            }

            ScenarioDefinition exported;
            try { exported = _serializer.Load(export.ArtifactPath); }
            catch (Exception ex) { return RememberInstall(new ScenarioPackageOperationResult { Message = "Could not verify the exported package: " + ex.Message }); }
            ScenarioPackageOperationResult result = _installer.Uninstall(exported != null ? exported.Id : null, install.InstallPath);
            return RememberInstall(result);
        }

        private ScenarioPackageOperationResult RememberInstall(ScenarioPackageOperationResult result) { lock (_sync) { _lastInstallResult = result; return result; } }

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

        // Reports whether a validated export folder already exists for a draft identity,
        // reusing the same export-path convention as ExportActiveDraft so the scenario
        // book can show the last export without an active editor session.
        internal static bool TryGetExistingExportInfo(string scenarioId, string displayName, out string exportRoot, out DateTime lastWriteUtc)
        {
            exportRoot = null;
            lastWriteUtc = DateTime.MinValue;
            try
            {
                string scenarioFolder = ScenarioPackageContents.SafeFolderName(!string.IsNullOrEmpty(scenarioId) ? scenarioId : displayName);
                exportRoot = Path.Combine(GetExportsRoot(), scenarioFolder);
                string exportFilePath = Path.Combine(exportRoot, ScenarioEditorDefinitionSerializer.DefaultFileName);
                if (File.Exists(exportFilePath))
                {
                    lastWriteUtc = File.GetLastWriteTimeUtc(exportFilePath);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private string ResolveExportRoot(ScenarioDefinition definition)
        {
            string scenarioFolder = ScenarioPackageContents.SafeFolderName(!string.IsNullOrEmpty(definition.Id) ? definition.Id : definition.DisplayName);
            return Path.Combine(_exportsRoot ?? GetExportsRoot(), scenarioFolder);
        }

        private static string GetExportsRoot()
        {
            return Path.Combine(ModApiPaths.UserRoot, ExportRootFolder);
        }

        internal static void RecoverExports()
        {
            try { ShelteredScenarioPackages.RecoverRoot(GetExportsRoot()); }
            catch (IOException ex) { MMLog.WriteWarning("[Scenario package export recovery] " + ex.Message); }
            catch (UnauthorizedAccessException ex) { MMLog.WriteWarning("[Scenario package export recovery] " + ex.Message); }
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
