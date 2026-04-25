using System;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Temporary confirmation dialog shown after switching to controller mode.
    /// If the countdown expires, the previous input mode is restored automatically.
    /// </summary>
    public sealed class ShelteredControllerModePromptDialog : MonoBehaviour
    {
        private const float DefaultTimeoutSeconds = 10f;
        private const int WindowWidth = 720;
        private const int WindowHeight = 380;
        private const int OverlayDepth = 60200;

        private static GameObject _instance;

        private static readonly Color HeaderColor = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color AccentColor = new Color(0.82f, 0.72f, 0.5f, 1f);
        private static readonly Color BackgroundColor = new Color(0.15f, 0.12f, 0.1f, 0.98f);
        private static readonly Color BorderColor = new Color(0.5f, 0.4f, 0.3f, 1f);

        private PlatformInput.InputType _revertMode;
        private float _remainingSeconds;
        private UILabel _countdownLabel;
        private bool _closed;

        /// <summary>
        /// Displays the countdown prompt. Returns false when the dialog could not be created.
        /// </summary>
        public static bool Show(PlatformInput.InputType revertMode)
        {
            return Show(revertMode, DefaultTimeoutSeconds);
        }

        /// <summary>
        /// Displays the countdown prompt. Returns false when the dialog could not be created.
        /// </summary>
        public static bool Show(PlatformInput.InputType revertMode, float timeoutSeconds)
        {
            if (_instance != null)
                Destroy(_instance);

            UIFontCache.RefreshIfMissing();
            UIPanel panel = UIUtil.EnsureOverlayPanel("ShelteredAPI_ControllerModePromptDialog", OverlayDepth);
            if (panel == null)
            {
                MMLog.WriteWarning("[ShelteredControllerModePromptDialog] Failed to create overlay panel.");
                return false;
            }

            GameObject root = new GameObject("ShelteredControllerModePromptDialog");
            root.transform.SetParent(panel.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            root.layer = panel.gameObject.layer;
            _instance = root;

            ShelteredControllerModePromptDialog dialog = root.AddComponent<ShelteredControllerModePromptDialog>();
            dialog.Initialize(revertMode, timeoutSeconds);
            dialog.BuildUI();
            MMLog.WriteInfo("[ShelteredControllerModePromptDialog] Showing controller confirmation prompt. Revert mode=" + revertMode + ".");
            return true;
        }

        /// <summary>
        /// Closes the active prompt without changing the current input mode.
        /// </summary>
        public static void Dismiss()
        {
            ShelteredControllerModePromptDialog dialog = GetActiveDialog();
            if (dialog != null)
                dialog.Close();
        }

        /// <summary>
        /// Reverts the active prompt immediately when one is present.
        /// </summary>
        public static void RevertActive()
        {
            ShelteredControllerModePromptDialog dialog = GetActiveDialog();
            if (dialog != null)
                dialog.RevertToPreviousMode();
        }

        private static ShelteredControllerModePromptDialog GetActiveDialog()
        {
            if (_instance == null)
                return null;

            return _instance.GetComponent<ShelteredControllerModePromptDialog>();
        }

        private void Initialize(PlatformInput.InputType revertMode, float timeoutSeconds)
        {
            _revertMode = revertMode;
            _remainingSeconds = Mathf.Max(1f, timeoutSeconds);
        }

        private void BuildUI()
        {
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();

            CreateBox(transform, "Blocker", Vector3.zero, 4000, 4000, new Color(0f, 0f, 0f, 0.72f), 0, true);
            CreateBox(transform, "WindowBackground", Vector3.zero, WindowWidth, WindowHeight, BackgroundColor, 10, false);
            CreateBox(transform, "WindowBorder", Vector3.zero, WindowWidth + 4, WindowHeight + 4, BorderColor, 9, false);

            UILabel titleLabel = CreateLabel(
                transform,
                "Title",
                "KEEP CONTROLLER MODE?",
                new Vector3(0f, WindowHeight / 2f - 42f, 0f),
                24,
                HeaderColor,
                fonts.Bitmap,
                fonts.TTF,
                100);
            titleLabel.alignment = NGUIText.Alignment.Center;
            titleLabel.width = WindowWidth - 60;
            titleLabel.multiLine = false;
            titleLabel.overflowMethod = UILabel.Overflow.ClampContent;

            UILabel messageLabel = CreateLabel(
                transform,
                "Message",
                "Controller mode has been enabled temporarily so you can verify the controller works.\n\nPress A / Enter to keep it.\nPress B / Escape to revert now.",
                new Vector3(0f, 36f, 0f),
                18,
                TextColor,
                fonts.Bitmap,
                fonts.TTF,
                100);
            messageLabel.alignment = NGUIText.Alignment.Center;
            messageLabel.width = WindowWidth - 90;
            messageLabel.height = 180;
            messageLabel.multiLine = true;
            messageLabel.overflowMethod = UILabel.Overflow.ResizeHeight;

            _countdownLabel = CreateLabel(
                transform,
                "Countdown",
                string.Empty,
                new Vector3(0f, -52f, 0f),
                20,
                AccentColor,
                fonts.Bitmap,
                fonts.TTF,
                100);
            _countdownLabel.alignment = NGUIText.Alignment.Center;
            _countdownLabel.width = WindowWidth - 80;
            _countdownLabel.multiLine = false;
            _countdownLabel.overflowMethod = UILabel.Overflow.ClampContent;
            RefreshCountdownLabel();

            CreateButton(
                transform,
                "ConfirmButton",
                "KEEP",
                new Vector3(-120f, -WindowHeight / 2f + 58f, 0f),
                160,
                44,
                fonts.Bitmap,
                fonts.TTF,
                Confirm);
            CreateButton(
                transform,
                "CancelButton",
                "REVERT",
                new Vector3(120f, -WindowHeight / 2f + 58f, 0f),
                160,
                44,
                fonts.Bitmap,
                fonts.TTF,
                RevertToPreviousMode);
        }

        private void Update()
        {
            if (_closed)
                return;

            if (PlatformInput.InputMethod != PlatformInput.InputType.Gamepad)
            {
                Close();
                return;
            }

            if (IsConfirmPressed())
            {
                Confirm();
                return;
            }

            if (IsCancelPressed())
            {
                RevertToPreviousMode();
                return;
            }

            _remainingSeconds -= RealTime.deltaTime;
            if (_remainingSeconds <= 0f)
            {
                RevertToPreviousMode();
                return;
            }

            RefreshCountdownLabel();
        }

        private bool IsConfirmPressed()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Return)
                || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)
                || PlatformInput.GetButtonUp(PlatformInput.MenuInputButton.UIselect)
                || PlatformInput.GetButtonUp(PlatformInput.MenuInputButton.UIstart);
        }

        private bool IsCancelPressed()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape)
                || PlatformInput.GetButtonUp(PlatformInput.MenuInputButton.UIcancel);
        }

        private void RefreshCountdownLabel()
        {
            if (_countdownLabel == null)
                return;

            int seconds = Mathf.Max(0, Mathf.CeilToInt(_remainingSeconds));
            _countdownLabel.text = "Reverting to keyboard and mouse in " + seconds + " second" + (seconds == 1 ? string.Empty : "s") + ".";
        }

        private void Confirm()
        {
            if (_closed)
                return;

            MMLog.WriteInfo("[ShelteredControllerModePromptDialog] Controller mode confirmed by user.");
            Close();
        }

        private void RevertToPreviousMode()
        {
            if (_closed)
                return;

            MMLog.WriteInfo("[ShelteredControllerModePromptDialog] Reverting controller mode back to " + _revertMode + ".");
            PlatformInput.SetInputMethod(_revertMode);
            Close();
        }

        private void Close()
        {
            if (_closed)
                return;

            _closed = true;
            if (_instance == gameObject)
                _instance = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == gameObject)
                _instance = null;
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
                if (onClick != null)
                    onClick();
            };

            return button;
        }
    }
}
