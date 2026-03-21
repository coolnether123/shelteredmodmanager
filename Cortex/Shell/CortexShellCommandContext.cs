using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Host.Unity.Runtime;
using Cortex.Modules.Editor;

namespace Cortex
{
    internal sealed class CortexShellCommandContext
    {
        private readonly Func<UnityWorkbenchRuntime> _workbenchRuntimeAccessor;
        private readonly Func<IDocumentService> _documentServiceAccessor;
        private readonly Func<bool> _visibleAccessor;
        private readonly Action<bool> _setVisible;
        private readonly Action _persistWorkbenchSession;
        private readonly Action _persistWindowSettings;
        private readonly Action _fitMainWindowToScreen;
        private readonly Action<string> _activateContainer;
        private readonly Func<string, WorkbenchHostLocation> _resolveHostLocation;
        private readonly Action<string> _hideContainer;
        private readonly Action _openSettingsWindow;
        private readonly Action _openOnboarding;
        private readonly Action _openFind;
        private readonly Action<int> _executeSearchOrAdvance;
        private readonly Action _closeFind;
        private readonly Action<EditorCommandTarget> _requestDefinition;

        public CortexShellCommandContext(
            CortexShellState state,
            Func<UnityWorkbenchRuntime> workbenchRuntimeAccessor,
            Func<IDocumentService> documentServiceAccessor,
            Func<bool> visibleAccessor,
            Action<bool> setVisible,
            Action persistWorkbenchSession,
            Action persistWindowSettings,
            Action fitMainWindowToScreen,
            Action<string> activateContainer,
            Func<string, WorkbenchHostLocation> resolveHostLocation,
            Action<string> hideContainer,
            Action openSettingsWindow,
            Action openOnboarding,
            Action openFind,
            Action<int> executeSearchOrAdvance,
            Action closeFind,
            Action<EditorCommandTarget> requestDefinition)
        {
            State = state;
            _workbenchRuntimeAccessor = workbenchRuntimeAccessor;
            _documentServiceAccessor = documentServiceAccessor;
            _visibleAccessor = visibleAccessor;
            _setVisible = setVisible;
            _persistWorkbenchSession = persistWorkbenchSession;
            _persistWindowSettings = persistWindowSettings;
            _fitMainWindowToScreen = fitMainWindowToScreen;
            _activateContainer = activateContainer;
            _resolveHostLocation = resolveHostLocation;
            _hideContainer = hideContainer;
            _openSettingsWindow = openSettingsWindow;
            _openOnboarding = openOnboarding;
            _openFind = openFind;
            _executeSearchOrAdvance = executeSearchOrAdvance;
            _closeFind = closeFind;
            _requestDefinition = requestDefinition;
        }

        public CortexShellState State { get; private set; }

        public UnityWorkbenchRuntime WorkbenchRuntime
        {
            get { return _workbenchRuntimeAccessor != null ? _workbenchRuntimeAccessor() : null; }
        }

        public IDocumentService DocumentService
        {
            get { return _documentServiceAccessor != null ? _documentServiceAccessor() : null; }
        }

        public bool Visible
        {
            get { return _visibleAccessor != null && _visibleAccessor(); }
            set
            {
                if (_setVisible != null)
                {
                    _setVisible(value);
                }
            }
        }

        public void PersistWorkbenchSession()
        {
            if (_persistWorkbenchSession != null)
            {
                _persistWorkbenchSession();
            }
        }

        public void PersistWindowSettings()
        {
            if (_persistWindowSettings != null)
            {
                _persistWindowSettings();
            }
        }

        public void FitMainWindowToScreen()
        {
            if (_fitMainWindowToScreen != null)
            {
                _fitMainWindowToScreen();
            }
        }

        public void ActivateContainer(string containerId)
        {
            if (_activateContainer != null)
            {
                _activateContainer(containerId);
            }
        }

        public WorkbenchHostLocation ResolveHostLocation(string containerId)
        {
            return _resolveHostLocation != null ? _resolveHostLocation(containerId) : WorkbenchHostLocation.PrimarySideHost;
        }

        public void HideContainer(string containerId)
        {
            if (_hideContainer != null)
            {
                _hideContainer(containerId);
            }
        }

        public void OpenSettingsWindow()
        {
            if (_openSettingsWindow != null)
            {
                _openSettingsWindow();
            }
        }

        public void OpenFind()
        {
            if (_openFind != null)
            {
                _openFind();
            }
        }

        public void OpenOnboarding()
        {
            if (_openOnboarding != null)
            {
                _openOnboarding();
            }
        }

        public void ExecuteSearchOrAdvance(int step)
        {
            if (_executeSearchOrAdvance != null)
            {
                _executeSearchOrAdvance(step);
            }
        }

        public void CloseFind()
        {
            if (_closeFind != null)
            {
                _closeFind();
            }
        }

        public void RequestDefinition(EditorCommandTarget target)
        {
            if (_requestDefinition != null)
            {
                _requestDefinition(target);
            }
        }
    }
}
