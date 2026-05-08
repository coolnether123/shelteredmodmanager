using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionTestWindow : MonoBehaviour
    {
        private const int WindowId = 774421;
        private const float WindowMinWidth = 620f;
        private const float WindowMinHeight = 640f;
        private const float WindowHeaderHeight = 26f;
        private const float WindowPadding = 8f;
        private const float CloseButtonSize = 22f;

        private MultiplayerMenuController _controller;
        private readonly MultiplayerConnectionPanelState _panelState = new MultiplayerConnectionPanelState();
        private readonly MultiplayerConnectionPanelPresenter _presenter = new MultiplayerConnectionPanelPresenter();
        private readonly MultiplayerConnectionPanelRenderer _renderer = new MultiplayerConnectionPanelRenderer();
        private Rect _windowRect = new Rect(80f, 80f, 680f, 720f);

        public void Initialize(MultiplayerMenuController controller)
        {
            _controller = controller;
        }

        private void OnGUI()
        {
            if (_controller == null || _controller.Service == null)
                return;

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "Sheltered Multiplayer");
            ClampWindowToScreen();
        }

        private void DrawWindow(int id)
        {
            if (GUI.Button(new Rect(_windowRect.width - CloseButtonSize - 4f, 3f, CloseButtonSize, 20f), "X"))
            {
                UnityEngine.Object.Destroy(this);
                return;
            }

            MultiplayerConnectionTestService service = _controller.Service;
            MultiplayerConnectionPanelViewModel model = _presenter.Build(service, _panelState);

            Rect contentRect = new Rect(
                WindowPadding,
                WindowHeaderHeight,
                _windowRect.width - (WindowPadding * 2f),
                _windowRect.height - WindowHeaderHeight - WindowPadding);

            GUILayout.BeginArea(contentRect);
            _renderer.Draw(model, _panelState, GetScrollHeight(), delegate { UnityEngine.Object.Destroy(this); });
            GUILayout.EndArea();

            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - CloseButtonSize - 8f, WindowHeaderHeight));
        }

        private float GetScrollHeight()
        {
            float available = _windowRect.height - 245f;
            if (available < 180f)
                return 180f;
            return available;
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
