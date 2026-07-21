using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Bunker{
    internal interface IScenarioDraftMutationService
    {
        bool HasActiveDraft { get; }
        bool CanMutateActiveDraft(out string message);
        BunkerEditsDefinition EnsureBunkerEdits();
        bool TryEnsureBunkerEdits(out BunkerEditsDefinition bunkerEdits);
        void MarkDirty(ScenarioDirtySection section, ScenarioEditCategory category);
        void UpsertPlacement(ObjectPlacement placement);
        bool TryUpsertPlacement(ObjectPlacement placement);
        bool TryFindSinglePlacement(Predicate<ObjectPlacement> predicate, out ObjectPlacement placement);
        bool TryRemovePlacement(Predicate<ObjectPlacement> predicate);
        void UpsertRoomEdit(int gridX, int gridY, Action<RoomEdit> applyUpdate);
        bool TryUpsertRoomEdit(int gridX, int gridY, Action<RoomEdit> applyUpdate);
        bool TryRemoveRoomEdit(int gridX, int gridY, Func<RoomEdit, bool> shouldRemove);
    }

    internal sealed class ScenarioDraftMutationService : IScenarioDraftMutationService
    {
        private readonly IScenarioEditorSessionStore _sessionStore;

        public ScenarioDraftMutationService(IScenarioEditorSessionStore sessionStore)
        {
            _sessionStore = sessionStore;
        }

        public bool HasActiveDraft
        {
            get
            {
                ScenarioEditorSession session = _sessionStore.Current;
                return session != null && session.WorkingDefinition != null;
            }
        }

        public bool CanMutateActiveDraft(out string message)
        {
            message = null;
            ScenarioEditorSession session = _sessionStore.Current;
            if (session == null)
            {
                message = "No active scenario draft is available.";
                return false;
            }

            if (session.WorkingDefinition == null)
            {
                message = "The active scenario draft has no working definition.";
                return false;
            }

            return true;
        }

        public BunkerEditsDefinition EnsureBunkerEdits()
        {
            return ScenarioBunkerDraftService.EnsureBunkerEdits(RequireSession());
        }

        public bool TryEnsureBunkerEdits(out BunkerEditsDefinition bunkerEdits)
        {
            bunkerEdits = null;
            string ignored;
            if (!CanMutateActiveDraft(out ignored))
            {
                LogMutationFailure(ignored);
                return false;
            }

            ScenarioEditorSession session = _sessionStore.Current;
            try
            {
                bunkerEdits = ScenarioBunkerDraftService.EnsureBunkerEdits(session);
                return true;
            }
            catch (Exception ex)
            {
                LogMutationFailure("Could not prepare bunker edits: " + ex.Message);
                return false;
            }
        }

        public void MarkDirty(ScenarioDirtySection section, ScenarioEditCategory category)
        {
            ScenarioEditorSession session = _sessionStore.Current;
            if (session == null)
            {
                LogMutationFailure("No active scenario draft is available while marking " + section + " dirty.");
                return;
            }

            session.MarkDraftChanged(section, category);
        }

        public void UpsertPlacement(ObjectPlacement placement)
        {
            TryUpsertPlacement(placement);
        }

        public bool TryUpsertPlacement(ObjectPlacement placement)
        {
            if (placement == null)
            {
                LogMutationFailure("Object placement mutation was ignored because the placement was null.");
                return false;
            }

            string ignored;
            if (!CanMutateActiveDraft(out ignored))
            {
                LogMutationFailure(ignored);
                return false;
            }

            ScenarioEditorSession session = _sessionStore.Current;
            try
            {
                ScenarioBunkerDraftService.UpsertPlacement(session, placement);
                return true;
            }
            catch (Exception ex)
            {
                LogMutationFailure("Object placement mutation failed: " + ex.Message);
                return false;
            }
        }

        public bool TryFindSinglePlacement(Predicate<ObjectPlacement> predicate, out ObjectPlacement placement)
        {
            placement = null;
            if (predicate == null)
            {
                LogMutationFailure("Object placement lookup was ignored because the predicate was null.");
                return false;
            }

            string ignored;
            if (!CanMutateActiveDraft(out ignored))
            {
                LogMutationFailure(ignored);
                return false;
            }

            ScenarioEditorSession session = _sessionStore.Current;
            BunkerEditsDefinition bunkerEdits = session != null && session.WorkingDefinition != null
                ? session.WorkingDefinition.BunkerEdits
                : null;
            if (bunkerEdits == null || bunkerEdits.ObjectPlacements == null)
                return false;

            for (int i = 0; i < bunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement candidate = bunkerEdits.ObjectPlacements[i];
                if (candidate == null || !predicate(candidate))
                    continue;

                if (placement != null)
                {
                    placement = null;
                    return false;
                }

                placement = candidate;
            }

            return placement != null;
        }

        public bool TryRemovePlacement(Predicate<ObjectPlacement> predicate)
        {
            if (predicate == null)
            {
                LogMutationFailure("Object placement removal was ignored because the predicate was null.");
                return false;
            }

            string ignored;
            if (!CanMutateActiveDraft(out ignored))
            {
                LogMutationFailure(ignored);
                return false;
            }

            ScenarioEditorSession session = _sessionStore.Current;
            try
            {
                return ScenarioBunkerDraftService.RemovePlacement(session, predicate);
            }
            catch (Exception ex)
            {
                LogMutationFailure("Object placement removal failed: " + ex.Message);
                return false;
            }
        }

        public void UpsertRoomEdit(int gridX, int gridY, Action<RoomEdit> applyUpdate)
        {
            TryUpsertRoomEdit(gridX, gridY, applyUpdate);
        }

        public bool TryUpsertRoomEdit(int gridX, int gridY, Action<RoomEdit> applyUpdate)
        {
            if (applyUpdate == null)
            {
                LogMutationFailure("Room edit mutation was ignored because the update action was null.");
                return false;
            }

            string ignored;
            if (!CanMutateActiveDraft(out ignored))
            {
                LogMutationFailure(ignored);
                return false;
            }

            ScenarioEditorSession session = _sessionStore.Current;
            try
            {
                ScenarioBunkerDraftService.UpsertRoomEdit(session, gridX, gridY, applyUpdate);
                return true;
            }
            catch (Exception ex)
            {
                LogMutationFailure("Room edit mutation failed at " + gridX + "," + gridY + ": " + ex.Message);
                return false;
            }
        }

        public bool TryRemoveRoomEdit(int gridX, int gridY, Func<RoomEdit, bool> shouldRemove)
        {
            string ignored;
            if (!CanMutateActiveDraft(out ignored))
            {
                LogMutationFailure(ignored);
                return false;
            }

            ScenarioEditorSession session = _sessionStore.Current;
            try
            {
                return ScenarioBunkerDraftService.RemoveRoomEdit(session, gridX, gridY, shouldRemove);
            }
            catch (Exception ex)
            {
                LogMutationFailure("Room edit removal failed at " + gridX + "," + gridY + ": " + ex.Message);
                return false;
            }
        }

        private ScenarioEditorSession RequireSession()
        {
            ScenarioEditorSession session = _sessionStore.Current;
            if (session == null)
                throw new InvalidOperationException("No scenario editor session is active.");
            return session;
        }

        private static void LogMutationFailure(string message)
        {
            if (!string.IsNullOrEmpty(message))
                MMLog.WriteWarning("[ScenarioDraftMutation] " + message);
        }
    }
}
