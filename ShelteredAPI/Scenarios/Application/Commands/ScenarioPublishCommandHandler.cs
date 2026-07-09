using System;
using System.Diagnostics;
using System.IO;
using ShelteredAPI.Scenarios.Application.Authoring;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal static class ScenarioPublishActionIds
    {
        public const string OpenLastExportFolder = "publish.export.open_folder";
        public const string CopyLastExportPath = "publish.export.copy_path";
        public const string InstallLastExport = "publish.export.install";
        public const string ConfirmInstallOverwrite = "publish.export.install_confirm";
        public const string ToggleReadme = "publish.readme.toggle";
        public const string AcceptWarningPrefix = "publish.warning.accept.";
        public const string UnacceptWarningPrefix = "publish.warning.unaccept.";
    }

    internal sealed class ScenarioPublishCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioPublishExportService _exportService;

        public ScenarioPublishCommandHandler(ScenarioPublishExportService exportService)
        {
            _exportService = exportService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = string.Equals(actionId, ScenarioAuthoringActionIds.ActionPublishExport, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioPublishActionIds.OpenLastExportFolder, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioPublishActionIds.CopyLastExportPath, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioPublishActionIds.InstallLastExport, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioPublishActionIds.ConfirmInstallOverwrite, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioPublishActionIds.ToggleReadme, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(actionId) && (actionId.StartsWith(ScenarioPublishActionIds.AcceptWarningPrefix, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioPublishActionIds.UnacceptWarningPrefix, StringComparison.Ordinal)));
            message = null;
            if (!handled)
                return false;

            if (string.Equals(actionId, ScenarioPublishActionIds.OpenLastExportFolder, StringComparison.Ordinal))
                return OpenLastExportFolder(out message);

            if (string.Equals(actionId, ScenarioPublishActionIds.CopyLastExportPath, StringComparison.Ordinal))
                return CopyLastExportPath(out message);

            if (string.Equals(actionId, ScenarioPublishActionIds.InstallLastExport, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioPublishActionIds.ConfirmInstallOverwrite, StringComparison.Ordinal))
            {
                ScenarioPackageInstallResult install = _exportService.InstallLastExport(string.Equals(actionId, ScenarioPublishActionIds.ConfirmInstallOverwrite, StringComparison.Ordinal));
                message = install != null ? install.Message : "Install service is unavailable.";
                return install != null && install.Success;
            }

            if (string.Equals(actionId, ScenarioPublishActionIds.ToggleReadme, StringComparison.Ordinal))
            {
                ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(state != null ? state.ActiveScenarioFilePath : null);
                preferences.IncludeReadme = !preferences.IncludeReadme;
                preferences.Save(state != null ? state.ActiveScenarioFilePath : null);
                message = preferences.IncludeReadme ? "README.txt will be included." : "README.txt will not be included.";
                return true;
            }

            if (actionId.StartsWith(ScenarioPublishActionIds.UnacceptWarningPrefix, StringComparison.Ordinal))
            {
                ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(state != null ? state.ActiveScenarioFilePath : null);
                preferences.Remove(actionId.Substring(ScenarioPublishActionIds.UnacceptWarningPrefix.Length));
                preferences.Save(state != null ? state.ActiveScenarioFilePath : null);
                message = "Warning acceptance removed.";
                return true;
            }

            if (actionId.StartsWith(ScenarioPublishActionIds.AcceptWarningPrefix, StringComparison.Ordinal))
            {
                string payload = actionId.Substring(ScenarioPublishActionIds.AcceptWarningPrefix.Length);
                if (payload.Length < 17 || payload[16] != '.') { message = "Warning acceptance action is invalid."; return false; }
                string note = ScenarioAuthoringActionCodec.DecodeToken(payload.Substring(17));
                if (string.IsNullOrEmpty(note)) { message = "Add a short acceptance note before accepting this warning."; return false; }
                ScenarioPackageAuthoringPreferences preferences = ScenarioPackageAuthoringPreferences.Load(state != null ? state.ActiveScenarioFilePath : null);
                preferences.Accept(payload.Substring(0, 16), note);
                preferences.Save(state != null ? state.ActiveScenarioFilePath : null);
                message = "Warning accepted with author note.";
                return true;
            }

            ScenarioPublishExportResult result = _exportService != null ? _exportService.ExportActiveDraft(state) : null;
            message = result != null ? result.Message : "Export service is unavailable.";
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
            if (result == null || string.IsNullOrEmpty(result.ArtifactRootPath))
                return null;
            return result.ArtifactRootPath;
        }
    }
}
