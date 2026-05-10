using System;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionTestWindow : MonoBehaviour
    {
        private const int WindowId = 774421;
        private const float WindowMinWidth = 720f;
        private const float WindowMinHeight = 660f;
        private const float WindowHeaderHeight = 26f;
        private const float WindowPadding = 8f;
        private const float CloseButtonSize = 22f;
        private const int ModelRefreshMilliseconds = 250;

        private MultiplayerMenuController _controller;
        private readonly MultiplayerConnectionPanelState _panelState = new MultiplayerConnectionPanelState();
        private readonly MultiplayerConnectionPanelPresenter _presenter = new MultiplayerConnectionPanelPresenter();
        private readonly MultiplayerConnectionPanelRenderer _renderer = new MultiplayerConnectionPanelRenderer();
        private Rect _windowRect = new Rect(80f, 80f, 760f, 740f);
        private MultiplayerConnectionPanelViewModel _cachedModel;
        private MultiplayerConnectionTestService _cachedModelService;
        private int _cachedModelUiRevision = int.MinValue;
        private int _nextModelRefreshTick = int.MinValue;

        public void Initialize(MultiplayerMenuController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            if (_controller != null)
            {
                bool actionRan = ProcessPendingPanelActions(_controller.Service);
                RefreshModelIfNeeded(_controller.Service, actionRan);
            }
        }

        private void OnGUI()
        {
            if (_controller == null || _controller.Service == null)
                return;

            if (_cachedModel == null || IsModelRefreshEvent())
                RefreshModelIfNeeded(_controller.Service, _cachedModel == null);
            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "Sheltered Multiplayer");
            ClampWindowToScreen();
        }

        private void DrawWindow(int id)
        {
            if (GUI.Button(new Rect(_windowRect.width - CloseButtonSize - 4f, 3f, CloseButtonSize, 20f), "X"))
            {
                _panelState.PendingCloseRequested = true;
                return;
            }

            MultiplayerConnectionPanelViewModel model = _cachedModel;
            if (model == null)
                model = RefreshModelIfNeeded(_controller.Service, true);

            Rect contentRect = new Rect(
                WindowPadding,
                WindowHeaderHeight,
                _windowRect.width - (WindowPadding * 2f),
                _windowRect.height - WindowHeaderHeight - WindowPadding);

            DrawContentBackground(contentRect);
            Exception renderException = null;
            try
            {
                GUILayout.BeginArea(contentRect);
                try
                {
                    _renderer.Draw(model, _panelState, GetScrollHeight(), delegate { _panelState.PendingCloseRequested = true; });
                    _panelState.LastRenderErrorText = string.Empty;
                }
                catch (Exception ex)
                {
                    renderException = ex;
                    _panelState.LastRenderErrorText = ex.Message;
                    Debug.LogException(ex);
                }
                finally
                {
                    GUILayout.EndArea();
                }
            }
            catch (Exception ex)
            {
                renderException = ex;
                _panelState.LastRenderErrorText = ex.Message;
                Debug.LogException(ex);
            }

            if (renderException != null)
                DrawRenderFallback(contentRect, renderException);

            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - CloseButtonSize - 8f, WindowHeaderHeight));
        }

        private MultiplayerConnectionPanelViewModel RefreshModelIfNeeded(
            MultiplayerConnectionTestService service,
            bool force)
        {
            if (service == null)
            {
                _cachedModel = null;
                _cachedModelService = null;
                return null;
            }

            int now = Environment.TickCount;
            bool serviceChanged = !object.ReferenceEquals(_cachedModelService, service);
            bool uiChanged = _panelState.UiRevision != _cachedModelUiRevision;
            bool due = unchecked(now - _nextModelRefreshTick) >= 0;
            if (force || _cachedModel == null || serviceChanged || uiChanged || due)
            {
                _cachedModel = _presenter.Build(service, _panelState);
                _cachedModelService = service;
                _cachedModelUiRevision = _panelState.UiRevision;
                _nextModelRefreshTick = unchecked(now + ModelRefreshMilliseconds);
            }

            return _cachedModel;
        }

        private bool ProcessPendingPanelActions(MultiplayerConnectionTestService service)
        {
            bool changed = false;
            if (_panelState.PendingCloseRequested)
            {
                _panelState.PendingCloseRequested = false;
                UnityEngine.Object.Destroy(this);
                changed = true;
            }

            MultiplayerConnectionWizardActionKind pending = _panelState.PendingActionKind;
            if (pending == MultiplayerConnectionWizardActionKind.None || service == null)
                return changed;

            _panelState.PendingActionKind = MultiplayerConnectionWizardActionKind.None;
            MultiplayerConnectionPanelViewModel model = _presenter.Build(service, _panelState);
            MultiplayerConnectionWizardAction action = ResolvePendingAction(model, pending);
            if (MultiplayerConnectionWizardActionInvoker.Invoke(action, model, _panelState))
                changed = true;

            return changed;
        }

        private static MultiplayerConnectionWizardAction ResolvePendingAction(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionWizardActionKind kind)
        {
            if (model == null)
                return null;

            if (model.Wizard != null)
            {
                if (model.Wizard.PrimaryAction != null && model.Wizard.PrimaryAction.Kind == kind)
                    return model.Wizard.PrimaryAction;

                if (model.Wizard.SecondaryActions != null)
                {
                    for (int i = 0; i < model.Wizard.SecondaryActions.Length; i++)
                    {
                        MultiplayerConnectionWizardAction action = model.Wizard.SecondaryActions[i];
                        if (action != null && action.Kind == kind)
                            return action;
                    }
                }
            }

            switch (kind)
            {
                case MultiplayerConnectionWizardActionKind.Host:
                    return MultiplayerConnectionWizardAction.FromActionState(kind, model.HostAction);
                case MultiplayerConnectionWizardActionKind.Join:
                    return MultiplayerConnectionWizardAction.FromActionState(kind, model.JoinAction);
                case MultiplayerConnectionWizardActionKind.Stop:
                    return MultiplayerConnectionWizardAction.FromActionState(kind, model.StopAction);
                case MultiplayerConnectionWizardActionKind.DiscoverLan:
                    return MultiplayerConnectionWizardAction.FromActionState(kind, model.DiscoveryAction);
                case MultiplayerConnectionWizardActionKind.BeginSetup:
                    return MultiplayerConnectionWizardAction.FromActionState(kind, model.BeginSetupAction);
                case MultiplayerConnectionWizardActionKind.ReleaseSetup:
                    return MultiplayerConnectionWizardAction.FromActionState(kind, model.ReleaseSetupAction);
                case MultiplayerConnectionWizardActionKind.SendTestMessage:
                    return MultiplayerConnectionWizardAction.FromActionState(kind, model.SendTestMessageAction);
                default:
                    return null;
            }
        }

        private static bool IsModelRefreshEvent()
        {
            Event current = Event.current;
            return current == null || current.type == EventType.Layout;
        }

        private float GetScrollHeight()
        {
            float available = _windowRect.height - 390f;
            if (available < 160f)
                return 160f;
            return available;
        }

        private static void DrawContentBackground(Rect contentRect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.96f, 0.96f, 0.96f, 0.96f);
            GUI.DrawTexture(contentRect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawRenderFallback(Rect contentRect, Exception exception)
        {
            Rect boxRect = new Rect(contentRect.x + 12f, contentRect.y + 12f, contentRect.width - 24f, 104f);
            GUI.Box(boxRect, "Multiplayer panel render fallback");
            string message = exception != null ? exception.Message : "Unknown render error.";
            GUI.Label(new Rect(boxRect.x + 12f, boxRect.y + 28f, boxRect.width - 24f, 44f),
                "The multiplayer panel hit a render exception. Controls are hidden for this frame to keep Unity IMGUI state balanced.");
            GUI.Label(new Rect(boxRect.x + 12f, boxRect.y + 72f, boxRect.width - 24f, 22f), message);
        }

        private void ClampWindowToScreen()
        {
            if (_windowRect.width < WindowMinWidth)
                _windowRect.width = WindowMinWidth;
            if (_windowRect.height < WindowMinHeight)
                _windowRect.height = WindowMinHeight;

            if (_windowRect.width > Screen.width)
                _windowRect.width = Screen.width;
            if (_windowRect.height > Screen.height)
                _windowRect.height = Screen.height;

            if (_windowRect.x < 0f)
                _windowRect.x = 0f;
            if (_windowRect.y < 0f)
                _windowRect.y = 0f;
            if (_windowRect.xMax > Screen.width)
                _windowRect.x = Screen.width - _windowRect.width;
            if (_windowRect.yMax > Screen.height)
                _windowRect.y = Screen.height - _windowRect.height;
        }
    }
}
