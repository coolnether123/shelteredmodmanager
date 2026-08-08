using System;
using System.Diagnostics;
using System.IO;
using ShelteredScenarioEditor.Application.Authoring;
using UnityEngine;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class ScenarioPublishAutomationIds
    {
        public const string Export = "publish.export";
        public const string OpenLastExportFolder = "publish.export.open_folder";
        public const string CopyLastExportPath = "publish.export.copy_path";
        public const string InstallLastExport = "publish.export.install";
        public const string ConfirmInstallOverwrite = "publish.export.install_confirm";
        public const string UninstallLastExport = "publish.export.uninstall";
        public const string ToggleReadme = "publish.readme.toggle";
        public const string AcceptWarningPrefix = "publish.warning.accept.";
        public const string UnacceptWarningPrefix = "publish.warning.unaccept.";
    }

    internal enum ScenarioPublishCommandKind
    {
        Export,
        OpenLastExportFolder,
        CopyLastExportPath,
        InstallLastExport,
        UninstallLastExport,
        ToggleReadme,
        AcceptWarning,
        UnacceptWarning
    }

    internal sealed class ScenarioPublishCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private ScenarioPublishCommand(
            ScenarioPublishCommandKind kind,
            bool confirmOverwrite,
            string fingerprint,
            string note,
            string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            ConfirmOverwrite = confirmOverwrite;
            Fingerprint = fingerprint;
            Note = note;
        }

        public ScenarioPublishCommandKind Kind { get; private set; }
        public bool ConfirmOverwrite { get; private set; }
        public string Fingerprint { get; private set; }
        public string Note { get; private set; }

        public static ScenarioPublishCommand Export()
        {
            return Simple(ScenarioPublishCommandKind.Export, ScenarioPublishAutomationIds.Export);
        }

        public static ScenarioPublishCommand OpenLastExportFolder()
        {
            return Simple(ScenarioPublishCommandKind.OpenLastExportFolder, ScenarioPublishAutomationIds.OpenLastExportFolder);
        }

        public static ScenarioPublishCommand CopyLastExportPath()
        {
            return Simple(ScenarioPublishCommandKind.CopyLastExportPath, ScenarioPublishAutomationIds.CopyLastExportPath);
        }

        public static ScenarioPublishCommand InstallLastExport(bool confirmOverwrite)
        {
            return new ScenarioPublishCommand(
                ScenarioPublishCommandKind.InstallLastExport,
                confirmOverwrite,
                null,
                null,
                confirmOverwrite ? ScenarioPublishAutomationIds.ConfirmInstallOverwrite : ScenarioPublishAutomationIds.InstallLastExport);
        }

        public static ScenarioPublishCommand UninstallLastExport()
        {
            return Simple(ScenarioPublishCommandKind.UninstallLastExport, ScenarioPublishAutomationIds.UninstallLastExport);
        }

        public static ScenarioPublishCommand ToggleReadme()
        {
            return Simple(ScenarioPublishCommandKind.ToggleReadme, ScenarioPublishAutomationIds.ToggleReadme);
        }

        public static ScenarioPublishCommand AcceptWarning(string fingerprint, string note)
        {
            return new ScenarioPublishCommand(
                ScenarioPublishCommandKind.AcceptWarning,
                false,
                fingerprint,
                note,
                ScenarioPublishAutomationIds.AcceptWarningPrefix + (fingerprint ?? string.Empty));
        }

        public static ScenarioPublishCommand UnacceptWarning(string fingerprint)
        {
            return new ScenarioPublishCommand(
                ScenarioPublishCommandKind.UnacceptWarning,
                false,
                fingerprint,
                null,
                ScenarioPublishAutomationIds.UnacceptWarningPrefix + (fingerprint ?? string.Empty));
        }

        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            return Kind == ScenarioPublishCommandKind.AcceptWarning
                ? AcceptWarning(Fingerprint, value)
                : this;
        }

        private static ScenarioPublishCommand Simple(ScenarioPublishCommandKind kind, string automationId)
        {
            return new ScenarioPublishCommand(kind, false, null, null, automationId);
        }
    }

    internal sealed class ScenarioPublishCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioPublishExportService _exportService;
        private readonly IScenarioEditorSessionStore _sessionStore;

        public ScenarioPublishCommandHandler(
            ScenarioPublishExportService exportService,
            IScenarioEditorSessionStore sessionStore)
        {
            _exportService = exportService;
            _sessionStore = sessionStore;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is ScenarioPublishCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            ScenarioPublishCommand publishCommand = command as ScenarioPublishCommand;
            string message;
            bool changed;
            switch (publishCommand.Kind)
            {
                case ScenarioPublishCommandKind.OpenLastExportFolder:
                    changed = OpenLastExportFolder(out message);
                    break;
                case ScenarioPublishCommandKind.CopyLastExportPath:
                    changed = CopyLastExportPath(out message);
                    break;
                case ScenarioPublishCommandKind.InstallLastExport:
                    ScenarioPackageOperationResult install = _exportService != null
                        ? _exportService.InstallLastExport(publishCommand.ConfirmOverwrite)
                        : null;
                    message = install != null ? install.Message : "Install service is unavailable.";
                    changed = install != null && install.Success;
                    break;
                case ScenarioPublishCommandKind.UninstallLastExport:
                    ScenarioPackageOperationResult uninstall = _exportService != null
                        ? _exportService.UninstallLastExport()
                        : null;
                    message = uninstall != null ? uninstall.Message : "Uninstall service is unavailable.";
                    changed = uninstall != null && uninstall.Success;
                    break;
                case ScenarioPublishCommandKind.ToggleReadme:
                    changed = ToggleReadme(out message);
                    break;
                case ScenarioPublishCommandKind.AcceptWarning:
                    changed = AcceptWarning(publishCommand.Fingerprint, publishCommand.Note, out message);
                    break;
                case ScenarioPublishCommandKind.UnacceptWarning:
                    changed = UnacceptWarning(publishCommand.Fingerprint, out message);
                    break;
                case ScenarioPublishCommandKind.Export:
                    ScenarioPublishExportResult result = _exportService != null ? _exportService.ExportActiveDraft(state) : null;
                    message = result != null ? result.Message : "Export service is unavailable.";
                    changed = true;
                    break;
                default:
                    changed = false;
                    message = "Publish command is not available.";
                    break;
            }

            return Result(changed, message);
        }

        private bool ToggleReadme(out string message)
        {
            if (_sessionStore == null)
            {
                message = "Editor session is unavailable.";
                return false;
            }

            ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(_sessionStore.CurrentFilePath);
            preferences.IncludeReadme = !preferences.IncludeReadme;
            preferences.Save(_sessionStore.CurrentFilePath);
            message = preferences.IncludeReadme ? "README.txt will be included." : "README.txt will not be included.";
            return true;
        }

        private bool AcceptWarning(string fingerprint, string note, out string message)
        {
            if (string.IsNullOrEmpty(fingerprint))
            {
                message = "Warning acceptance target is invalid.";
                return false;
            }
            if (string.IsNullOrEmpty(note))
            {
                message = "Add a short acceptance note before accepting this warning.";
                return false;
            }
            if (_sessionStore == null)
            {
                message = "Editor session is unavailable.";
                return false;
            }

            ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(_sessionStore.CurrentFilePath);
            preferences.Accept(fingerprint, note);
            preferences.Save(_sessionStore.CurrentFilePath);
            message = "Warning accepted with author note.";
            return true;
        }

        private bool UnacceptWarning(string fingerprint, out string message)
        {
            if (string.IsNullOrEmpty(fingerprint) || _sessionStore == null)
            {
                message = "Warning acceptance target is invalid.";
                return false;
            }

            ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(_sessionStore.CurrentFilePath);
            preferences.Remove(fingerprint);
            preferences.Save(_sessionStore.CurrentFilePath);
            message = "Warning acceptance removed.";
            return true;
        }

        private bool OpenLastExportFolder(out string message)
        {
            string path = GetLastExportRoot();
            if (string.IsNullOrEmpty(path))
            {
                message = "No export folder is available yet.";
                return false;
            }

            if (!Directory.Exists(path))
            {
                message = "Export folder no longer exists: " + path;
                return false;
            }

            try
            {
                Process.Start("explorer.exe", "\"" + path + "\"");
                message = "Opened export folder.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Could not open export folder: " + ex.Message;
                return false;
            }
        }

        private bool CopyLastExportPath(out string message)
        {
            string path = GetLastExportRoot();
            if (string.IsNullOrEmpty(path))
            {
                message = "No export path is available yet.";
                return false;
            }

            try
            {
                GUIUtility.systemCopyBuffer = path;
                message = "Copied export path.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Could not copy export path: " + ex.Message;
                return false;
            }
        }

        private string GetLastExportRoot()
        {
            ScenarioPublishExportResult result = _exportService != null ? _exportService.LastResult : null;
            return result != null && !string.IsNullOrEmpty(result.ArtifactRootPath)
                ? result.ArtifactRootPath
                : null;
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult
            {
                Handled = true,
                Changed = changed,
                Message = message
            };
        }
    }
}
