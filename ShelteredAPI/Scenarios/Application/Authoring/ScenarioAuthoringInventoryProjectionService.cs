using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringInventoryProjectionService
    {
        private const float LiveTruthPollSeconds = 1f;
        private readonly InventoryApplyService _inventoryApplyService;
        private float _lastLiveTruthPollRealtime;

        public ScenarioAuthoringInventoryProjectionService(InventoryApplyService inventoryApplyService)
        {
            _inventoryApplyService = inventoryApplyService;
        }

        public void ResetForCurrentWorld(ScenarioEditorSession session)
        {
            if (_inventoryApplyService == null || session == null)
                return;

            _inventoryApplyService.ResetAuthoringProjection(session.WorkingDefinition);
        }

        public bool TryProject(ScenarioEditorSession session, string reason, out string message)
        {
            message = null;
            if (_inventoryApplyService == null || session == null || session.WorkingDefinition == null)
                return false;

            if (!CanProjectInCurrentWorld(out message))
                return false;

            ScenarioApplyResult applyResult = new ScenarioApplyResult();
            InventoryProjectionResult projection = _inventoryApplyService.ProjectAuthoringStartingInventory(session.WorkingDefinition, applyResult);
            if (projection == null)
                return false;

            message = projection.Changed
                ? "Shelter storage synced (+" + projection.Added.ToString() + "/-" + projection.Removed.ToString() + ")."
                : "Shelter storage already matches starting items.";

            if (applyResult.Messages != null && applyResult.Messages.Length > 0)
                message = message + " " + string.Join(" ", applyResult.Messages);

            MMLog.WriteInfo("[ScenarioAuthoringInventoryProjection] " + message
                + " reason=" + (reason ?? "unspecified")
                + ", scenario=" + (session.WorkingDefinition != null ? session.WorkingDefinition.Id : "<none>") + ".");
            return true;
        }

        public void UpdateLiveTruth(ScenarioEditorSession session)
        {
            if (_inventoryApplyService == null || session == null || session.WorkingDefinition == null)
                return;

            float now = 0f;
            try { now = Time.realtimeSinceStartup; }
            catch { now = _lastLiveTruthPollRealtime + LiveTruthPollSeconds; }
            if (_lastLiveTruthPollRealtime > 0f && now - _lastLiveTruthPollRealtime < LiveTruthPollSeconds)
                return;

            _lastLiveTruthPollRealtime = now;
            string message;
            TryReconcileLiveTruth(session, "live truth poll", out message);
        }

        public bool TryReconcileLiveTruth(ScenarioEditorSession session, string reason, out string message)
        {
            message = null;
            if (_inventoryApplyService == null || session == null || session.WorkingDefinition == null)
                return false;

            if (!CanProjectInCurrentWorld(out message))
                return false;

            ScenarioApplyResult applyResult = new ScenarioApplyResult();
            InventoryProjectionResult projection = _inventoryApplyService.ReconcileAuthoringLiveTruth(session.WorkingDefinition, applyResult);
            if (projection == null)
                return false;

            if (projection.DraftUpdated)
            {
                session.MarkDraftChanged(ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
                message = "Shelter storage adopted as scenario starting inventory (+" + projection.Added.ToString() + "/-" + projection.Removed.ToString() + ").";
            }
            else if (projection.Changed)
            {
                message = "Shelter storage synced (+" + projection.Added.ToString() + "/-" + projection.Removed.ToString() + ").";
            }
            else
            {
                message = "Shelter storage already matches starting items.";
            }

            if (applyResult.Messages != null && applyResult.Messages.Length > 0)
                message = message + " " + string.Join(" ", applyResult.Messages);

            if (projection.Changed)
            {
                MMLog.WriteInfo("[ScenarioAuthoringInventoryProjection] " + message
                    + " reason=" + (reason ?? "unspecified")
                    + ", scenario=" + (session.WorkingDefinition != null ? session.WorkingDefinition.Id : "<none>") + ".");
            }

            return true;
        }

        public void Clear()
        {
            _lastLiveTruthPollRealtime = 0f;
            if (_inventoryApplyService != null)
                _inventoryApplyService.ClearAuthoringProjection();
        }

        private static bool CanProjectInCurrentWorld(out string reason)
        {
            reason = null;
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
            {
                reason = "Authoring inventory projection skipped because no draft editor is active.";
                return false;
            }

            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
            {
                reason = "Authoring inventory projection skipped because playtest owns the live apply pipeline.";
                return false;
            }

            if (!ScenarioWorldReady.IsShelterSceneActive())
            {
                reason = "Authoring inventory projection skipped because the shelter scene is not active.";
                return false;
            }

            if (InventoryManager.Instance == null)
            {
                reason = "Authoring inventory projection skipped because InventoryManager is not ready.";
                return false;
            }

            return true;
        }
    }
}
