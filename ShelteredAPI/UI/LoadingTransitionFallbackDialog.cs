using ShelteredAPI.Core;
using UnityEngine;

namespace ShelteredAPI.UI
{
    internal static class LoadingTransitionFallbackDialog
    {
        private const int WindowId = 918327;
        private const int Width = 720;
        private const int Height = 260;

        private static LoadingTransitionRecoveryNotice _notice;
        private static bool _closeRequested;

        public static bool Draw(LoadingTransitionRecoveryNotice notice)
        {
            if (notice == null)
                return false;

            _notice = notice;
            _closeRequested = false;

            Rect rect = new Rect((Screen.width - Width) / 2, (Screen.height - Height) / 2, Width, Height);
            GUI.ModalWindow(WindowId, rect, DrawWindow, notice.Title);

            _notice = null;
            return _closeRequested;
        }

        private static void DrawWindow(int id)
        {
            if (_notice == null)
                return;

            GUI.Label(new Rect(24, 42, 672, 150), _notice.Message);
            if (GUI.Button(new Rect(285, 205, 150, 36), "OK"))
                _closeRequested = true;
        }
    }
}
