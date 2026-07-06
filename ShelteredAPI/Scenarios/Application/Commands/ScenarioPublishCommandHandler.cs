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
                || string.Equals(actionId, ScenarioPublishActionIds.CopyLastExportPath, StringComparison.Ordinal);
            message = null;
            if (!handled)
                return false;

            if (string.Equals(actionId, ScenarioPublishActionIds.OpenLastExportFolder, StringComparison.Ordinal))
                return OpenLastExportFolder(out message);

            if (string.Equals(actionId, ScenarioPublishActionIds.CopyLastExportPath, StringComparison.Ordinal))
                return CopyLastExportPath(out message);

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
