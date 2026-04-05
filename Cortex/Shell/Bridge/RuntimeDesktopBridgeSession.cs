using System;
using Cortex.Bridge;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services.Navigation;
using Cortex.Shell;

namespace Cortex.Shell.Bridge
{
    internal sealed class RuntimeDesktopBridgeSession
    {
        private readonly CortexShellState _shellState;
        private readonly RuntimeDesktopBridgeSettingsFeature _settingsFeature;
        private readonly RuntimeDesktopBridgeWorkspaceFeature _workspaceFeature;
        private readonly RuntimeDesktopBridgeWorkbenchFeature _workbenchFeature;
        private readonly RuntimeDesktopBridgeOverlayFeature _overlayFeature;
        private readonly RuntimeDesktopBridgeSnapshotBuilder _snapshotBuilder;
        private string _statusMessage = string.Empty;
        private string _cachedStatusMessage = string.Empty;
        private long _revision;

        public RuntimeDesktopBridgeSession(
            CortexShellState shellState,
            CortexShellViewState viewState,
            Func<IWorkbenchRuntime> runtimeAccessor,
            Func<ICortexSettingsStore> settingsStoreAccessor,
            Func<IProjectCatalog> projectCatalogAccessor,
            Func<ISourceLookupIndex> sourceLookupIndexAccessor,
            Func<ITextSearchService> textSearchServiceAccessor,
            Func<ICortexNavigationService> navigationServiceAccessor)
        {
            _shellState = shellState ?? new CortexShellState();
            _settingsFeature = new RuntimeDesktopBridgeSettingsFeature(_shellState, settingsStoreAccessor);
            _workspaceFeature = new RuntimeDesktopBridgeWorkspaceFeature(_shellState, projectCatalogAccessor, () => _settingsFeature.CurrentSettings);
            _workbenchFeature = new RuntimeDesktopBridgeWorkbenchFeature(
                _shellState,
                projectCatalogAccessor,
                sourceLookupIndexAccessor,
                textSearchServiceAccessor,
                navigationServiceAccessor);
            _overlayFeature = new RuntimeDesktopBridgeOverlayFeature(_shellState, viewState, runtimeAccessor);
            _snapshotBuilder = new RuntimeDesktopBridgeSnapshotBuilder(_settingsFeature, _workspaceFeature, _workbenchFeature, _overlayFeature);
        }

        public long Revision
        {
            get { return _revision; }
        }

        public void Initialize()
        {
            _settingsFeature.Initialize();
            _workspaceFeature.Initialize();
            _workbenchFeature.Initialize();
            _overlayFeature.Initialize();
            _statusMessage = ResolveRuntimeStatusMessage("Legacy runtime bridge ready.");
            CacheRuntimeMirror();
            Touch();
        }

        public bool SynchronizeFromRuntime()
        {
            var changed = false;
            changed |= _settingsFeature.SynchronizeFromRuntime();
            changed |= _workspaceFeature.SynchronizeFromRuntime();
            changed |= _workbenchFeature.SynchronizeFromRuntime();

            var currentStatus = ResolveRuntimeStatusMessage(_statusMessage);
            if (!string.Equals(_cachedStatusMessage, currentStatus, StringComparison.Ordinal))
            {
                _statusMessage = currentStatus;
                changed = true;
            }

            if (changed)
            {
                CacheRuntimeMirror();
                Touch();
            }

            return changed;
        }

        public BridgeOperationResultMessage ApplyIntent(BridgeIntentMessage intent)
        {
            var result = new BridgeOperationResultMessage
            {
                RequestId = intent != null ? intent.RequestId ?? string.Empty : string.Empty,
                IntentType = intent != null ? intent.IntentType : BridgeIntentType.SaveSettings,
                Status = BridgeOperationStatus.Completed,
                StatusMessage = string.Empty
            };
            if (intent == null)
            {
                result.Status = BridgeOperationStatus.Rejected;
                result.StatusMessage = "Intent payload was missing.";
                return result;
            }

            string statusMessage;
            var handled = _settingsFeature.TryApplyIntent(intent, out statusMessage);
            if (handled)
            {
                _workspaceFeature.RefreshFromSettings();
                _workbenchFeature.SynchronizeFromRuntime();
            }
            else
            {
                handled = _workspaceFeature.TryApplyIntent(intent, out statusMessage);
                if (!handled)
                {
                    handled = _workbenchFeature.TryApplyIntent(intent, out statusMessage);
                }
            }

            if (!handled)
            {
                result.Status = BridgeOperationStatus.Rejected;
                result.StatusMessage = "Unsupported bridge intent.";
                return result;
            }

            _statusMessage = ResolveRuntimeStatusMessage(statusMessage);
            result.StatusMessage = _statusMessage;
            CacheRuntimeMirror();
            Touch();
            return result;
        }

        public bool SynchronizeOverlayPresentation(OverlayPresentationSnapshot overlaySnapshot)
        {
            return _overlayFeature.SynchronizeFromRuntime(overlaySnapshot);
        }

        public long OverlayRevision
        {
            get { return _overlayFeature.Revision; }
        }

        public OverlayPresentationSnapshot BuildOverlaySnapshot()
        {
            return _overlayFeature.BuildSnapshot();
        }

        public bool ApplyOverlayInputIntent(OverlayInputIntentMessage intent, out string statusMessage)
        {
            return _overlayFeature.TryApplyInputIntent(intent, out statusMessage);
        }

        public bool ApplyOverlayWindowState(OverlayWindowStateChangedMessage message, out string statusMessage)
        {
            return _overlayFeature.TryApplyWindowState(message, out statusMessage);
        }

        public bool ApplyOverlayLifecycle(OverlayHostLifecycleMessage message, out string statusMessage)
        {
            return _overlayFeature.TryApplyLifecycle(message, out statusMessage);
        }

        public WorkbenchBridgeSnapshot BuildSnapshot()
        {
            return _snapshotBuilder.Build(_statusMessage);
        }

        private void CacheRuntimeMirror()
        {
            _cachedStatusMessage = ResolveRuntimeStatusMessage(_statusMessage);
        }

        private string ResolveRuntimeStatusMessage(string fallback)
        {
            return !string.IsNullOrEmpty(_shellState.StatusMessage)
                ? _shellState.StatusMessage
                : fallback ?? string.Empty;
        }

        private void Touch()
        {
            _revision++;
        }
    }
}
