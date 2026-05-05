using ModAPI.Core;
using ShelteredAPI.Core;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.UI
{
    internal sealed class LoadingTransitionRecoveryDialog : MonoBehaviour
    {
        private const int WindowWidth = 760;
        private const int WindowHeight = 500;
        private const int OverlayDepth = 60000;

        private static readonly Color HeaderColor = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color BackgroundColor = new Color(0.12f, 0.1f, 0.085f, 0.98f);
        private static readonly Color BorderColor = new Color(0.55f, 0.42f, 0.28f, 1f);

        private static GameObject _instance;

        public static void Show(string title, string message)
        {
            if (_instance != null)
                Destroy(_instance);

            UIFontCache.RefreshIfMissing();
            UIPanel panel = UIUtil.EnsureOverlayPanel("ShelteredAPI_LoadingRecoveryDialog", OverlayDepth);
            if (panel == null)
            {
                MMLog.WriteWarning("[LoadingTransitionRecoveryDialog] Failed to create overlay panel.");
                return;
            }

            GameObject root = NguiModalPrimitives.CreateRoot(panel, "LoadingTransitionRecoveryDialog");
            if (root == null)
                return;

            _instance = root;

            LoadingTransitionRecoveryDialog dialog = root.AddComponent<LoadingTransitionRecoveryDialog>();
            dialog.BuildUI(title, message);
        }

        private void BuildUI(string title, string message)
        {
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();

            NguiModalPrimitives.CreateBox(transform, "Blocker", Vector3.zero, 4000, 4000, new Color(0f, 0f, 0f, 0.68f), 0, true);
            NguiModalPrimitives.CreateBox(transform, "WindowBorder", Vector3.zero, WindowWidth + 4, WindowHeight + 4, BorderColor, 9, false);
            NguiModalPrimitives.CreateBox(transform, "WindowBackground", Vector3.zero, WindowWidth, WindowHeight, BackgroundColor, 10, false);

            UILabel titleLabel = NguiModalPrimitives.CreateLabel(
                transform,
                "Title",
                string.IsNullOrEmpty(title) ? LoadingTransitionRecoveryConstants.DialogTitle : title,
                new Vector3(0f, WindowHeight / 2f - 42f, 0f),
                24,
                HeaderColor,
                fonts.Bitmap,
                fonts.TTF,
                100);
            titleLabel.alignment = NGUIText.Alignment.Center;
            titleLabel.width = WindowWidth - 60;
            titleLabel.multiLine = false;
            titleLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel messageLabel = NguiModalPrimitives.CreateLabel(
                transform,
                "Message",
                message ?? string.Empty,
                new Vector3(-WindowWidth / 2f + 45f, WindowHeight / 2f - 120f, 0f),
                16,
                TextColor,
                fonts.Bitmap,
                fonts.TTF,
                100);
            messageLabel.alignment = NGUIText.Alignment.Left;
            messageLabel.pivot = UIWidget.Pivot.TopLeft;
            messageLabel.width = WindowWidth - 90;
            messageLabel.height = WindowHeight - 150;
            messageLabel.multiLine = true;
            messageLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            NguiModalPrimitives.CreateButton(
                transform,
                "OkButton",
                "OK",
                new Vector3(0f, -WindowHeight / 2f + 48f, 0f),
                160,
                42,
                fonts.Bitmap,
                fonts.TTF,
                new Color(0.42f, 0.31f, 0.22f, 1f),
                TextColor,
                Close);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Return) ||
                UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Close();
            }
        }

        private void Close()
        {
            if (_instance == gameObject)
                _instance = null;
            Destroy(gameObject);
        }
    }
}
