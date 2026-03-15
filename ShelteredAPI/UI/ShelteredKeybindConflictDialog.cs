using System;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.UI
{
    public sealed class ShelteredKeybindConflictDialog : MonoBehaviour
    {
        private static GameObject _instance;

        private const int WindowWidth = 680;
        private const int WindowHeight = 360;
        private const int OverlayDepth = 60000;

        private static readonly Color HeaderColor = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color BackgroundColor = new Color(0.15f, 0.12f, 0.1f, 0.98f);
        private static readonly Color BorderColor = new Color(0.5f, 0.4f, 0.3f, 1f);

        private Action _onConfirm;
        private Action _onCancel;
        private bool _closed;

        public static void Show(string title, string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
        {
            if (_instance != null)
                Destroy(_instance);

            UIFontCache.RefreshIfMissing();
            UIPanel panel = UIUtil.EnsureOverlayPanel("ShelteredAPI_KeybindConflictDialog", OverlayDepth);
            if (panel == null)
            {
                MMLog.WriteWarning("[ShelteredKeybindConflictDialog] Failed to create overlay panel.");
                if (onCancel != null) onCancel();
                return;
            }

            GameObject root = new GameObject("ShelteredKeybindConflictDialog");
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

            CreateBox(transform, "Blocker", Vector3.zero, 4000, 4000, new Color(0f, 0f, 0f, 0.72f), 0, true);
            CreateBox(transform, "WindowBackground", Vector3.zero, WindowWidth, WindowHeight, BackgroundColor, 10, false);
            CreateBox(transform, "WindowBorder", Vector3.zero, WindowWidth + 4, WindowHeight + 4, BorderColor, 9, false);

            UILabel titleLabel = CreateLabel(transform, "Title", string.IsNullOrEmpty(title) ? "CONFIRM" : title,
                new Vector3(0f, WindowHeight / 2f - 42f, 0f), 24, HeaderColor, fonts.Bitmap, fonts.TTF, 100);
            titleLabel.alignment = NGUIText.Alignment.Center;
            titleLabel.width = WindowWidth - 60;
            titleLabel.multiLine = false;
            titleLabel.overflowMethod = UILabel.Overflow.ClampContent;

            UILabel messageLabel = CreateLabel(transform, "Message", message ?? string.Empty,
                new Vector3(0f, 24f, 0f), 18, TextColor, fonts.Bitmap, fonts.TTF, 100);
            messageLabel.alignment = NGUIText.Alignment.Center;
            messageLabel.width = WindowWidth - 80;
            messageLabel.height = 180;
            messageLabel.multiLine = true;
            messageLabel.overflowMethod = UILabel.Overflow.ResizeHeight;

            CreateButton(transform, "ConfirmButton", string.IsNullOrEmpty(confirmLabel) ? "YES" : confirmLabel,
                new Vector3(-110f, -WindowHeight / 2f + 54f, 0f), 150, 42, fonts.Bitmap, fonts.TTF, Confirm);
            CreateButton(transform, "CancelButton", string.IsNullOrEmpty(cancelLabel) ? "NO" : cancelLabel,
                new Vector3(110f, -WindowHeight / 2f + 54f, 0f), 150, 42, fonts.Bitmap, fonts.TTF, Cancel);
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
            Destroy(gameObject);
        }

        private GameObject CreateBox(Transform parent, string name, Vector3 position, int width, int height, Color color, int depth, bool addCollider)
        {
            GameObject box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.layer = parent.gameObject.layer;

            UITexture texture = box.AddComponent<UITexture>();
            texture.mainTexture = UIUtil.WhiteTexture;
            texture.width = width;
            texture.height = height;
            texture.depth = depth;
            texture.color = color;

            if (addCollider)
            {
                BoxCollider collider = box.AddComponent<BoxCollider>();
                collider.size = new Vector3(width, height, 1f);
            }

            return box;
        }

        private UILabel CreateLabel(Transform parent, string name, string text, Vector3 position, int fontSize, Color color, UIFont bitmapFont, Font trueTypeFont, int depth)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.layer = parent.gameObject.layer;

            UILabel label = go.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = depth;
            label.bitmapFont = bitmapFont;
            label.trueTypeFont = trueTypeFont;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            return label;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector3 position, int width, int height, UIFont bitmapFont, Font trueTypeFont, Action onClick)
        {
            GameObject button = new GameObject(name);
            button.transform.SetParent(parent, false);
            button.transform.localPosition = position;
            button.layer = parent.gameObject.layer;

            UITexture background = button.AddComponent<UITexture>();
            background.mainTexture = UIUtil.WhiteTexture;
            background.width = width;
            background.height = height;
            background.depth = 100;
            background.color = new Color(0.44f, 0.32f, 0.24f, 1f);

            UILabel label = CreateLabel(button.transform, "Label", text, Vector3.zero, 16, TextColor, bitmapFont, trueTypeFont, 101);
            label.alignment = NGUIText.Alignment.Center;
            label.width = width - 20;
            label.height = height - 8;
            label.multiLine = false;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;

            BoxCollider collider = button.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 1f);

            UIEventListener listener = UIEventListener.Get(button);
            listener.onClick = _ =>
            {
                if (onClick != null) onClick();
            };

            return button;
        }
    }
}
