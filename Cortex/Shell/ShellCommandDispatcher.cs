using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services.Editor.Context;

namespace Cortex.Shell
{
    internal sealed class ShellCommandDispatcher
    {
        private readonly CortexShellState _state;
        private readonly CortexShellCommandRouter _commandRouter;
        private readonly Func<IWorkbenchRuntime> _runtimeProvider;
        private readonly Func<IDocumentService> _documentServiceProvider;
        private readonly Func<bool> _isVisibleProvider;
        private readonly Action<bool> _setVisibilityAction;
        private readonly Action _persistSessionAction;
        private readonly Action _persistWindowSettingsAction;
        private readonly Action _fitMainWindowToScreenAction;
        private readonly Action<string> _activateContainerAction;
        private readonly Func<string, WorkbenchHostLocation> _resolveHostLocationFunc;
        private readonly Action<string> _hideContainerAction;
        private readonly Action _openSettingsWindowAction;
        private readonly Action<bool> _openOnboardingAction;
        private readonly Action _openFindAction;
        private readonly Action<int> _executeSearchAction;
        private readonly Action _closeFindAction;
        private readonly EditorSymbolInteractionService _symbolInteractionService;

        private CortexShellCommandContext _commandContext;

        public ShellCommandDispatcher(
            CortexShellState state,
            CortexShellCommandRouter commandRouter,
            Func<IWorkbenchRuntime> runtimeProvider,
            Func<IDocumentService> documentServiceProvider,
            Func<bool> isVisibleProvider,
            Action<bool> setVisibilityAction,
            Action persistSessionAction,
            Action persistWindowSettingsAction,
            Action fitMainWindowToScreenAction,
            Action<string> activateContainerAction,
            Func<string, WorkbenchHostLocation> resolveHostLocationFunc,
            Action<string> hideContainerAction,
            Action openSettingsWindowAction,
            Action<bool> openOnboardingAction,
            Action openFindAction,
            Action<int> executeSearchAction,
            Action closeFindAction,
            EditorSymbolInteractionService symbolInteractionService)
        {
            _state = state;
            _commandRouter = commandRouter;
            _runtimeProvider = runtimeProvider;
            _documentServiceProvider = documentServiceProvider;
            _isVisibleProvider = isVisibleProvider;
            _setVisibilityAction = setVisibilityAction;
            _persistSessionAction = persistSessionAction;
            _persistWindowSettingsAction = persistWindowSettingsAction;
            _fitMainWindowToScreenAction = fitMainWindowToScreenAction;
            _activateContainerAction = activateContainerAction;
            _resolveHostLocationFunc = resolveHostLocationFunc;
            _hideContainerAction = hideContainerAction;
            _openSettingsWindowAction = openSettingsWindowAction;
            _openOnboardingAction = openOnboardingAction;
            _openFindAction = openFindAction;
            _executeSearchAction = executeSearchAction;
            _closeFindAction = closeFindAction;
            _symbolInteractionService = symbolInteractionService;
        }

        public void RegisterCommandHandlers()
        {
            _commandRouter.RegisterCommandHandlers(GetCommandContext());
        }

        public bool ExecuteCommand(string commandId, object parameter)
        {
            var runtime = _runtimeProvider();
            if (runtime == null || runtime.CommandRegistry == null) return false;
            return runtime.CommandRegistry.Execute(commandId, BuildCommandContext(parameter));
        }

        private CommandExecutionContext BuildCommandContext(object parameter)
        {
            var runtime = _runtimeProvider();
            return new CommandExecutionContext
            {
                ActiveContainerId = _state.Workbench.FocusedContainerId,
                ActiveDocumentId = _state.Documents.ActiveDocumentPath,
                FocusedRegionId = runtime?.FocusState.FocusedRegionId ?? string.Empty,
                Parameter = parameter
            };
        }

        private CortexShellCommandContext GetCommandContext()
        {
            if (_commandContext == null)
            {
                _commandContext = new CortexShellCommandContext(
                    _state,
                    _runtimeProvider,
                    _documentServiceProvider,
                    _isVisibleProvider,
                    _setVisibilityAction,
                    _persistSessionAction,
                    _persistWindowSettingsAction,
                    _fitMainWindowToScreenAction,
                    _activateContainerAction,
                    _resolveHostLocationFunc,
                    _hideContainerAction,
                    _openSettingsWindowAction,
                    () => _openOnboardingAction(true),
                    _openFindAction,
                    _executeSearchAction,
                    _closeFindAction,
                    (target) => _symbolInteractionService.RequestDefinition(_state, target));
            }
            return _commandContext;
        }
    }
}
