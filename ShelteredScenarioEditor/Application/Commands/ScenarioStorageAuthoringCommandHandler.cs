using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;

namespace ShelteredScenarioEditor.Application.Commands{
    internal sealed class StorageAuthoringCommand : ScenarioAuthoringCommand
    {
        public StorageAuthoringCommand()
            : base(ScenarioAuthoringActionIds.ActionInventoryStorageOpen, ScenarioAuthoringCommandPolicy.World)
        {
        }
    }

    internal sealed class ScenarioStorageAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioStorageAuthoringRuntimeService _runtimeService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioVanillaInteractionRuntimeService _vanillaInteraction;

        public ScenarioStorageAuthoringCommandHandler(
            ScenarioStorageAuthoringRuntimeService runtimeService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioVanillaInteractionRuntimeService vanillaInteraction)
        {
            _runtimeService = runtimeService;
            _layoutService = layoutService;
            _vanillaInteraction = vanillaInteraction;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is StorageAuthoringCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            string message = null;
            bool changed = state != null && OpenStorageAuthoring(state, out message);
            return new ScenarioCommandDispatchResult
            {
                Handled = true,
                Changed = changed,
                Message = message
            };
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
            if (!ShelteredScenarioRuntime.IsWorldReady(out blockingReason))
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
            if (_vanillaInteraction != null)
                _vanillaInteraction.BeginPanelSession(state, ScenarioVanillaInteractionRuntimeService.KindStorage, "Changes sync to your scenario. Storage live-truth is captured into the stockpile draft.");
            message = state.StatusMessage;
            return true;
        }

    }
}
