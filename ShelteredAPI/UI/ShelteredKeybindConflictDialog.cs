using System;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Widgets;
using UnityEngine;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Sheltered book-style confirmation dialog used by the controls UI.
    /// </summary>
    internal sealed class ShelteredKeybindConflictDialog : MonoBehaviour
    {
        private static GameObject _instance;
        private static int _lastClosedFrame = -1;

        private const int WindowWidth = 760;
        private const int WindowHeight = 430;
        private const int OverlayDepth = 60000;

        private Action _onConfirm;
        private Action _onCancel;
        private bool _closed;
        private bool _disposed;
        private IThemePalette _palette;
        private ProceduralTextureLibrary _textures;
        private UIPrimitiveFactory _ui;

        internal static bool ShouldBlockPanelInput()
        {
            return _instance != null || Time.frameCount == _lastClosedFrame;
        }

        /// <summary>
        /// Displays a modal confirmation dialog and routes the user's choice to the provided callbacks.
        /// </summary>
        public static void Show(string title, string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
        {
            if (_instance != null)
                Destroy(_instance);

            UIFontCache.RefreshIfMissing();
            UIPanel panel = UIUtil.EnsureOverlayPanel("ShelteredAPI_KeybindConfirmDialog", OverlayDepth);
            if (panel == null)
            {
                MMLog.WriteWarning("[ShelteredKeybindConflictDialog] Failed to create overlay panel.");
                if (onCancel != null) onCancel();
                return;
            }

            GameObject root = new GameObject("ShelteredKeybindConfirmDialog");
            root.transform.SetParent(panel.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            root.layer = panel.gameObject.layer;
            _instance = root;

            ShelteredKeybindConflictDialog dialog = root.AddComponent<ShelteredKeybindConflictDialog>();
            dialog._onConfirm = onConfirm;
            dialog._onCancel = onCancel;
            dialog.BuildUI(title, message, confirmLabel, cancelLabel);
        }

        private void BuildUI(string title, string message, string confirmLabel, string cancelLabel)
        {
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();
            _palette = new FieldManualPalette();
            _textures = new ProceduralTextureLibrary(_palette);
            _ui = new UIPrimitiveFactory(fonts.Bitmap, fonts.TTF, OverlayDepth);

            UITexture blocker = _ui.CreateQuad(gameObject, "Blocker", _textures.White, Vector3.zero,
                4000, 4000, new Color(0f, 0f, 0f, 0.58f), _ui.NextDepth());
            _ui.AddClickCollider(blocker.gameObject, 4000, 4000, null);

            _ui.CreateQuad(gameObject, "DialogShadow", _textures.White, new Vector3(8f, -8f, 0f),
                WindowWidth + 28, WindowHeight + 28, _palette.PaperShadow, _ui.NextDepth());
            _ui.CreateQuad(gameObject, "DialogFrame", _textures.Gunmetal(WindowWidth + 22, WindowHeight + 22),
                Vector3.zero, WindowWidth + 22, WindowHeight + 22, Color.white, _ui.NextDepth());
            _ui.CreateQuad(gameObject, "DialogPaper", _textures.Paper(WindowWidth, WindowHeight),
                Vector3.zero, WindowWidth, WindowHeight, Color.white, _ui.NextDepth());

            _ui.CreateQuad(gameObject, "TitleTape", _textures.MaskingTape(420, 46),
                new Vector3(0f, WindowHeight / 2f - 54f, 0f), 420, 46, Color.white, _ui.NextDepth());

            UILabel titleLabel = _ui.CreateLabel(gameObject, "Title",
                string.IsNullOrEmpty(title) ? "CONFIRM" : title,
                new Vector3(0f, WindowHeight / 2f - 54f, 0f),
                26, _palette.Ink, WindowWidth - 96, 42,
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            titleLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            _ui.CreateQuad(gameObject, "Rule", _textures.White,
                new Vector3(0f, WindowHeight / 2f - 91f, 0f),
                WindowWidth - 112, 2,
                new Color(_palette.PaperGrain.r, _palette.PaperGrain.g, _palette.PaperGrain.b, 0.58f),
                _ui.NextDepth());

            UILabel messageLabel = _ui.CreateLabel(gameObject, "Message", message ?? string.Empty,
                new Vector3(-WindowWidth / 2f + 64f, 104f, 0f),
                18, _palette.Ink, WindowWidth - 128, 240,
                NGUIText.Alignment.Left, UIWidget.Pivot.TopLeft, _ui.NextDepth());
            messageLabel.multiLine = true;
            messageLabel.spacingY = 3;
            messageLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            BookButtonWidget buttons = new BookButtonWidget(_palette, _textures, _ui);
            buttons.Build(gameObject, "ConfirmButton", string.IsNullOrEmpty(confirmLabel) ? "YES" : confirmLabel,
                new Vector3(-125f, -WindowHeight / 2f + 58f, 0f), 190, 54, 18, Confirm);
            buttons.Build(gameObject, "CancelButton", string.IsNullOrEmpty(cancelLabel) ? "NO" : cancelLabel,
                new Vector3(125f, -WindowHeight / 2f + 58f, 0f), 190, 54, 18, Cancel);
        }

        private void Update()
        {
            if (_closed) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                Confirm();
        }

        private void Confirm()
        {
            if (_closed) return;
            _closed = true;
            Action handler = _onConfirm;
            Close();
            if (handler != null) handler();
        }

        private void Cancel()
        {
            if (_closed) return;
            _closed = true;
            Action handler = _onCancel;
            Close();
            if (handler != null) handler();
        }

        private void Close()
        {
            if (_instance == gameObject)
                _instance = null;
            _lastClosedFrame = Time.frameCount;
            DisposeResources();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == gameObject)
            {
                _instance = null;
                _lastClosedFrame = Time.frameCount;
            }

            DisposeResources();
        }

        private void DisposeResources()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_textures != null)
            {
                _textures.Dispose();
                _textures = null;
            }
        }
    }
}
