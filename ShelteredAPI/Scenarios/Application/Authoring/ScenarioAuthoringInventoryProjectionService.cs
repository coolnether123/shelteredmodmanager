using ModAPI.Core;

using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringInventoryProjectionService
    {
        private readonly InventoryApplyService _inventoryApplyService;

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

        public void AdoptCurrentDraftAsProjected(ScenarioEditorSession session, string reason)
        {
            if (_inventoryApplyService == null || session == null)
                return;

            _inventoryApplyService.AdoptAuthoringProjection(session.WorkingDefinition);
            MMLog.WriteInfo("[ScenarioAuthoringInventoryProjection] Adopted current draft inventory as projected. reason="
                + (reason ?? "unspecified") + ".");
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

        public void Clear()
        {
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
