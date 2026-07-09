using System;
using ShelteredAPI.Scenarios.Application.Authoring;

namespace ShelteredAPI.Scenarios.Application.Commands
{
    internal sealed class ScenarioDraftHistoryCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioDraftSnapshotService _snapshots;

        internal ScenarioDraftHistoryCommandHandler(ScenarioDraftSnapshotService snapshots)
        {
            _snapshots = snapshots;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null && actionId.StartsWith("editor.history.", StringComparison.Ordinal);
            message = null;
            if (!handled) return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistorySaveVersion, StringComparison.Ordinal))
            {
                ScenarioDraftSnapshotInfo saved;
                string error;
                string name = "Version " + DateTime.Now.ToString("g");
                if (!_snapshots.SaveNamedVersion(name, out saved, out error))
                {
                    message = "Could not save a version: " + error;
                    return true;
                }
                message = "Saved version: " + saved.Name + ".";
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryShow, StringComparison.Ordinal))
            {
                ScenarioDraftSnapshotInfo[] snapshots = _snapshots.ListSnapshots();
                message = "History: last manual save " + _snapshots.GetLastManualSaveText() + "; " + snapshots.Length + " saved snapshot" + (snapshots.Length == 1 ? "." : "s.");
                return true;
            }

            message = "History action is not available.";
            return true;
        }
    }
}
