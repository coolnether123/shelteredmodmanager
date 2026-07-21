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

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryShow, StringComparison.Ordinal))
            {
                ResetCandidates(state);
                if (state != null)
                    state.HistoryWindowOpen = true;
                message = BuildHistorySummary();
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryClose, StringComparison.Ordinal))
            {
                if (state != null)
                    state.HistoryWindowOpen = false;
                ResetCandidates(state);
                message = "History closed.";
                return true;
            }

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

            int index;
            if (TryGetSnapshotIndex(actionId, ScenarioAuthoringActionIds.ActionHistoryRestorePrefix, out index))
            {
                if (!HasSnapshot(index))
                {
                    message = "That saved snapshot is no longer available.";
                    return true;
                }
                if (state != null)
                {
                    state.HistoryRestoreCandidateIndex = index;
                    state.HistoryDeleteCandidateIndex = -1;
                }
                message = "Confirm restore of the selected saved draft.";
                return true;
            }

            if (TryGetSnapshotIndex(actionId, ScenarioAuthoringActionIds.ActionHistoryDeletePrefix, out index))
            {
                if (!HasSnapshot(index))
                {
                    message = "That saved snapshot is no longer available.";
                    return true;
                }
                if (state != null)
                {
                    state.HistoryDeleteCandidateIndex = index;
                    state.HistoryRestoreCandidateIndex = -1;
                }
                message = "Confirm deletion of the selected saved draft.";
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryCancelRestore, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryCancelDelete, StringComparison.Ordinal))
            {
                ResetCandidates(state);
                message = "History action canceled.";
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryConfirmRestore, StringComparison.Ordinal))
            {
                ScenarioDraftSnapshotInfo candidate = SnapshotAt(state != null ? state.HistoryRestoreCandidateIndex : -1);
                string error = null;
                bool restored = _snapshots != null && _snapshots.Restore(candidate, out error);
                ResetCandidates(state);
                message = restored ? "Saved version restored into the current draft. Save when you are ready." : "Restore failed: " + (error ?? "saved snapshot is unavailable.");
                return restored;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryConfirmDelete, StringComparison.Ordinal))
            {
                ScenarioDraftSnapshotInfo candidate = SnapshotAt(state != null ? state.HistoryDeleteCandidateIndex : -1);
                string error = null;
                bool deleted = _snapshots != null && _snapshots.Delete(candidate, out error);
                ResetCandidates(state);
                message = deleted ? "Saved history entry deleted." : "Could not delete history entry: " + (error ?? "saved snapshot is unavailable.");
                return deleted;
            }

            message = "History action is not available.";
            return true;
        }

        private string BuildHistorySummary()
        {
            ScenarioDraftSnapshotInfo[] snapshots = _snapshots != null ? _snapshots.ListSnapshots() : new ScenarioDraftSnapshotInfo[0];
            string manualSave = _snapshots != null ? _snapshots.GetLastManualSaveText() : "unavailable";
            return "History: last manual save " + manualSave + "; " + snapshots.Length + " saved snapshot" + (snapshots.Length == 1 ? "." : "s.");
        }

        private bool HasSnapshot(int index)
        {
            return SnapshotAt(index) != null;
        }

        private ScenarioDraftSnapshotInfo SnapshotAt(int index)
        {
            ScenarioDraftSnapshotInfo[] snapshots = _snapshots != null ? _snapshots.ListSnapshots() : null;
            return snapshots != null && index >= 0 && index < snapshots.Length ? snapshots[index] : null;
        }

        private static bool TryGetSnapshotIndex(string actionId, string prefix, out int index)
        {
            index = -1;
            return !string.IsNullOrEmpty(actionId)
                && actionId.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(actionId.Substring(prefix.Length), out index)
                && index >= 0;
        }

        private static void ResetCandidates(ScenarioAuthoringState state)
        {
            if (state == null)
                return;
            state.HistoryRestoreCandidateIndex = -1;
            state.HistoryDeleteCandidateIndex = -1;
        }
    }
}
