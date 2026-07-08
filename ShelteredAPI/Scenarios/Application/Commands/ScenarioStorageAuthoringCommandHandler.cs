using System;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal sealed class ScenarioStorageAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioStorageAuthoringRuntimeService _runtimeService;
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public ScenarioStorageAuthoringCommandHandler(
            ScenarioStorageAuthoringRuntimeService runtimeService,
            ScenarioAuthoringLayoutService layoutService)
        {
            _runtimeService = runtimeService;
            _layoutService = layoutService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (!IsStorageAction(actionId))
                return false;

            handled = true;
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryStorageOpen, StringComparison.Ordinal))
                return OpenStorageAuthoring(state, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryStorageClose, StringComparison.Ordinal))
                return CloseStorageAuthoring(state, out message);

            handled = false;
            return false;
        }

        private bool OpenStorageAuthoring(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
            {
                message = "Stop playtest before opening shelter storage authoring.";
                return false;
            }

            string blockingReason;
            if (!ScenarioWorldReady.Evaluate(out blockingReason))
            {
                message = blockingReason;
                return false;
            }

            if (_runtimeService == null || !_runtimeService.OpenVanillaStorage())
            {
                message = "The vanilla shelter storage panel is not available yet.";
                return false;
            }

            if (_layoutService != null)
            {
                _layoutService.SelectStage(state, ScenarioStageKind.InventoryStorage);
                _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.Stockpile, true);
            }
            state.ActiveStage = ScenarioStageKind.InventoryStorage;
            state.ActiveShellTab = ScenarioAuthoringShellTab.Stockpile;
            state.StorageAuthoringPreviousShellVisible = state.ShellVisible;
            state.StorageAuthoringActive = true;
            state.ShellVisible = false;
            state.StatusMessage = "Shelter storage authoring active. Use the real storage window; changes sync into the draft.";
            ScenarioVanillaInteractionRuntimeService vanillaInteraction = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
            if (vanillaInteraction != null)
                vanillaInteraction.BeginPanelSession(state, ScenarioVanillaInteractionRuntimeService.KindStorage, "Changes sync to your scenario. Storage live-truth is captured into the stockpile draft.");
            message = state.StatusMessage;
            return true;
        }

        private bool CloseStorageAuthoring(ScenarioAuthoringState state, out string message)
        {
            if (_runtimeService != null)
                _runtimeService.CloseVanillaStorage();

            ScenarioVanillaInteractionRuntimeService vanillaInteraction = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
            if (vanillaInteraction != null && state.VanillaInteractionActive)
                vanillaInteraction.ReturnToEditor(state);
            else
                ScenarioStorageAuthoringRuntimeService.RestoreSuppliesWorkspace(state);
            if (_layoutService != null)
                _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.Stockpile, true);

            message = "Shelter storage closed. Supplies workspace active.";
            state.StatusMessage = message;
            return true;
        }

        private static bool IsStorageAction(string actionId)
        {
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryStorageOpen, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryStorageClose, StringComparison.Ordinal);
        }
    }
}
