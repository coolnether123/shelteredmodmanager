using System.Globalization;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class ScenarioDraftHistoryAutomationIds
    {
        public const string Show = "editor.history.show";
        public const string SaveVersion = "editor.history.save_version";
        public const string Close = "editor.history.close";
        public const string ConfirmRestore = "editor.history.confirm_restore";
        public const string ConfirmDelete = "editor.history.confirm_delete";
        public const string CancelRestore = "editor.history.cancel_restore";
        public const string CancelDelete = "editor.history.cancel_delete";
        public const string SelectRestorePrefix = "editor.history.restore.";
        public const string SelectDeletePrefix = "editor.history.delete.";
    }

    internal enum ScenarioDraftHistoryCommandKind
    {
        Show,
        SaveVersion,
        Close,
        SelectRestore,
        SelectDelete,
        Cancel,
        ConfirmRestore,
        ConfirmDelete
    }

    internal sealed class ScenarioDraftHistoryCommand : ScenarioAuthoringCommand
    {
        private ScenarioDraftHistoryCommand(ScenarioDraftHistoryCommandKind kind, int snapshotIndex, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            SnapshotIndex = snapshotIndex;
        }

        public ScenarioDraftHistoryCommandKind Kind { get; private set; }
        public int SnapshotIndex { get; private set; }

        public static ScenarioDraftHistoryCommand Show()
        {
            return Simple(ScenarioDraftHistoryCommandKind.Show, ScenarioDraftHistoryAutomationIds.Show);
        }

        public static ScenarioDraftHistoryCommand SaveVersion()
        {
            return Simple(ScenarioDraftHistoryCommandKind.SaveVersion, ScenarioDraftHistoryAutomationIds.SaveVersion);
        }

        public static ScenarioDraftHistoryCommand Close()
        {
            return Simple(ScenarioDraftHistoryCommandKind.Close, ScenarioDraftHistoryAutomationIds.Close);
        }

        public static ScenarioDraftHistoryCommand SelectRestore(int snapshotIndex)
        {
            return Indexed(ScenarioDraftHistoryCommandKind.SelectRestore, snapshotIndex, ScenarioDraftHistoryAutomationIds.SelectRestorePrefix);
        }

        public static ScenarioDraftHistoryCommand SelectDelete(int snapshotIndex)
        {
            return Indexed(ScenarioDraftHistoryCommandKind.SelectDelete, snapshotIndex, ScenarioDraftHistoryAutomationIds.SelectDeletePrefix);
        }

        public static ScenarioDraftHistoryCommand CancelRestore()
        {
            return Simple(ScenarioDraftHistoryCommandKind.Cancel, ScenarioDraftHistoryAutomationIds.CancelRestore);
        }

        public static ScenarioDraftHistoryCommand CancelDelete()
        {
            return Simple(ScenarioDraftHistoryCommandKind.Cancel, ScenarioDraftHistoryAutomationIds.CancelDelete);
        }

        public static ScenarioDraftHistoryCommand ConfirmRestore()
        {
            return Simple(ScenarioDraftHistoryCommandKind.ConfirmRestore, ScenarioDraftHistoryAutomationIds.ConfirmRestore);
        }

        public static ScenarioDraftHistoryCommand ConfirmDelete()
        {
            return Simple(ScenarioDraftHistoryCommandKind.ConfirmDelete, ScenarioDraftHistoryAutomationIds.ConfirmDelete);
        }

        private static ScenarioDraftHistoryCommand Simple(ScenarioDraftHistoryCommandKind kind, string automationId)
        {
            return new ScenarioDraftHistoryCommand(kind, -1, automationId);
        }

        private static ScenarioDraftHistoryCommand Indexed(ScenarioDraftHistoryCommandKind kind, int snapshotIndex, string prefix)
        {
            return new ScenarioDraftHistoryCommand(kind, snapshotIndex, prefix + snapshotIndex.ToString(CultureInfo.InvariantCulture));
        }
    }

    internal sealed class ScenarioDraftHistoryCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioDraftSnapshotService _snapshots;

        internal ScenarioDraftHistoryCommandHandler(ScenarioDraftSnapshotService snapshots)
        {
            _snapshots = snapshots;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is ScenarioDraftHistoryCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            ScenarioDraftHistoryCommand historyCommand = command as ScenarioDraftHistoryCommand;
            switch (historyCommand.Kind)
            {
                case ScenarioDraftHistoryCommandKind.Show:
                    ResetCandidates(state);
                    if (state != null)
                    {
                        state.HistoryWindowOpen = true;
                        state.WindowMenuOpen = false;
                        state.GlobalSearchOpen = false;
                    }
                    return Result(true, BuildHistorySummary());
                case ScenarioDraftHistoryCommandKind.Close:
                    if (state != null)
                        state.HistoryWindowOpen = false;
                    ResetCandidates(state);
                    return Result(true, "History closed.");
                case ScenarioDraftHistoryCommandKind.SaveVersion:
                    return SaveVersion();
                case ScenarioDraftHistoryCommandKind.SelectRestore:
                    return SelectCandidate(state, historyCommand.SnapshotIndex, true);
                case ScenarioDraftHistoryCommandKind.SelectDelete:
                    return SelectCandidate(state, historyCommand.SnapshotIndex, false);
                case ScenarioDraftHistoryCommandKind.Cancel:
                    ResetCandidates(state);
                    return Result(true, "History action canceled.");
                case ScenarioDraftHistoryCommandKind.ConfirmRestore:
                    return RestoreCandidate(state);
                case ScenarioDraftHistoryCommandKind.ConfirmDelete:
                    return DeleteCandidate(state);
                default:
                    return Result(false, "History command is not available.");
            }
        }

        private ScenarioCommandDispatchResult SaveVersion()
        {
            ScenarioDraftSnapshotInfo saved;
            string error = null;
            if (_snapshots == null || !_snapshots.SaveVersion(out saved, out error))
                return Result(false, "Could not save a version: " + (error ?? "snapshot service is unavailable."));
            return Result(true, "Saved version: " + saved.Name + ".");
        }

        private ScenarioCommandDispatchResult SelectCandidate(ScenarioAuthoringState state, int index, bool restore)
        {
            if (!HasSnapshot(index))
                return Result(true, "That saved snapshot is no longer available.");
            if (state != null)
            {
                state.HistoryRestoreCandidateIndex = restore ? index : -1;
                state.HistoryDeleteCandidateIndex = restore ? -1 : index;
            }
            return Result(true, restore
                ? "Confirm restore of the selected saved draft."
                : "Confirm deletion of the selected saved draft.");
        }

        private ScenarioCommandDispatchResult RestoreCandidate(ScenarioAuthoringState state)
        {
            ScenarioDraftSnapshotInfo candidate = SnapshotAt(state != null ? state.HistoryRestoreCandidateIndex : -1);
            string error = null;
            bool restored = _snapshots != null && _snapshots.Restore(candidate, out error);
            ResetCandidates(state);
            return Result(restored, restored
                ? "Saved version restored into the current draft. Save when you are ready."
                : "Restore failed: " + (error ?? "saved snapshot is unavailable."));
        }

        private ScenarioCommandDispatchResult DeleteCandidate(ScenarioAuthoringState state)
        {
            ScenarioDraftSnapshotInfo candidate = SnapshotAt(state != null ? state.HistoryDeleteCandidateIndex : -1);
            string error = null;
            bool deleted = _snapshots != null && _snapshots.Delete(candidate, out error);
            ResetCandidates(state);
            return Result(deleted, deleted
                ? "Saved history entry deleted."
                : "Could not delete history entry: " + (error ?? "saved snapshot is unavailable."));
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

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult
            {
                Handled = true,
                Changed = changed,
                Message = message
            };
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
