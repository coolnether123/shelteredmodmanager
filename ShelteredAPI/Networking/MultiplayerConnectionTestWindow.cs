using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionTestWindow : MonoBehaviour
    {
        private const int WindowId = 774421;
        private const float WindowMinWidth = 620f;
        private const float WindowMinHeight = 640f;

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

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Sheltered Multiplayer");
            ClampWindowToScreen();
        }

        private void DrawWindow(int id)
        {
            MultiplayerConnectionTestService service = _controller.Service;
            MultiplayerConnectionPanelViewModel model = _presenter.Build(service);

            _renderer.Draw(model, _panelState, GetScrollHeight(), delegate { UnityEngine.Object.Destroy(this); });

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private float GetScrollHeight()
        {
            float available = _windowRect.height - 180f;
            if (available < 320f)
                return 320f;
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
