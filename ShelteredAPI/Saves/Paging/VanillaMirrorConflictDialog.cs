using System;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Saves.Paging
{
    internal sealed class VanillaMirrorConflictDialog : MonoBehaviour
    {
        private const int WINDOW_WIDTH = 760;
        private const int WINDOW_HEIGHT = 360;

        private static readonly Color ColorHeader = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color ColorText = Color.white;
        private static readonly Color ColorSubtext = new Color(0.72f, 0.72f, 0.72f);
        private static readonly Color ColorButton = new Color(0.44f, 0.32f, 0.24f, 1f);
        private static readonly Color ColorDisabledButton = new Color(0.18f, 0.15f, 0.13f, 1f);

        private static GameObject _instance;
        private static Texture2D _whiteTexture;

        private Action _onLoadVanilla;
        private Action _onLoadEdited;
        private Action _onCancel;

        public static void Show(Action onLoadVanilla, Action onLoadEdited, Action onCancel)
        {
            Show(
                "This SMM XML save differs from the vanilla save file. Which state should be loaded?",
                onLoadVanilla,
                onLoadEdited,
                onCancel);
        }

        public static void ShowMissingVanilla(Action onLoadEdited, Action onCancel)
        {
            Show(
                "The vanilla save file is missing, but an SMM XML mirror exists. Which state should be loaded?",
                null,
                onLoadEdited,
                onCancel);
        }

        private static void Show(string message, Action onLoadVanilla, Action onLoadEdited, Action onCancel)
        {
            if (_instance != null)
                Destroy(_instance);

            var panel = UIUtil.EnsureOverlayPanel("ModAPI_VanillaMirrorConflictDialog", 10003);
            if (panel == null)
            {
                MMLog.WriteError("[VanillaMirrorConflictDialog] Failed to create overlay panel.");
                if (onCancel != null)
                    onCancel();
                return;
            }

            EnsureWhiteTexture();

            GameObject root = new GameObject("VanillaMirrorConflictDialog");
            root.transform.SetParent(panel.transform, false);
            root.layer = panel.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            _instance = root;

            UIFont uiFont = null;
            Font ttfFont = null;
            UILabel sampleLabel = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (sampleLabel != null)
            {
                uiFont = sampleLabel.bitmapFont;
                ttfFont = sampleLabel.trueTypeFont;
            }
            if (uiFont == null && ttfFont == null)
                ttfFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            VanillaMirrorConflictDialog dialog = root.AddComponent<VanillaMirrorConflictDialog>();
            dialog._onLoadVanilla = onLoadVanilla;
            dialog._onLoadEdited = onLoadEdited;
            dialog._onCancel = onCancel;
            dialog.BuildUI(root.transform, message, uiFont, ttfFont);
        }

        private void BuildUI(Transform root, string message, UIFont uiFont, Font ttfFont)
        {
            CreateTexturedBox(root, "DarkOverlay", Vector3.zero, 3000, 3000,
                new Color(0f, 0f, 0f, 0.72f), 0, true);

            CreateTexturedBox(root, "WindowBorder", Vector3.zero,
                WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);
            CreateTexturedBox(root, "WindowBackground", Vector3.zero,
                WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);

            UILabel title = CreateLabel(root, "Title", "VANILLA SAVE STATE",
                new Vector3(0, WINDOW_HEIGHT / 2 - 58, 0), 28, ColorHeader, uiFont, ttfFont, 100);
            title.alignment = NGUIText.Alignment.Center;

            UILabel body = CreateLabel(root, "Body", message,
                new Vector3(0, 40, 0), 21, ColorText, uiFont, ttfFont, 100);
            body.alignment = NGUIText.Alignment.Center;
            body.width = WINDOW_WIDTH - 110;
            body.overflowMethod = UILabel.Overflow.ResizeHeight;

            UILabel hint = CreateLabel(root, "Hint",
                "Cancel leaves both files unchanged.",
                new Vector3(0, -48, 0), 16, ColorSubtext, uiFont, ttfFont, 100);
            hint.alignment = NGUIText.Alignment.Center;

            int buttonY = -WINDOW_HEIGHT / 2 + 58;
            CreateButton(root, "LoadVanillaBtn", "Load Vanilla State",
                new Vector3(-245, buttonY, 0), 17, Color.white, uiFont, ttfFont, 220, 48,
                _onLoadVanilla != null ? (Action)LoadVanilla : null);
            CreateButton(root, "LoadEditedBtn", "Load Edited XML State",
                new Vector3(0, buttonY, 0), 17, Color.white, uiFont, ttfFont, 235, 48,
                _onLoadEdited != null ? (Action)LoadEdited : null);
            CreateButton(root, "CancelBtn", "Cancel",
                new Vector3(245, buttonY, 0), 17, Color.white, uiFont, ttfFont, 150, 48, Cancel);
        }

        private void LoadVanilla()
        {
            if (_onLoadVanilla == null)
                return;

            Action action = _onLoadVanilla;
            Close();
            action();
        }

        private void LoadEdited()
        {
            if (_onLoadEdited == null)
                return;

            Action action = _onLoadEdited;
            Close();
            action();
        }

        private void Cancel()
        {
            Action action = _onCancel;
            Close();
            if (action != null)
                action();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                Cancel();
        }

        private static void EnsureWhiteTexture()
        {
            if (_whiteTexture != null)
                return;

            _whiteTexture = new Texture2D(2, 2);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    _whiteTexture.SetPixel(x, y, Color.white);
            _whiteTexture.Apply();
        }

        private GameObject CreateTexturedBox(Transform parent, string name, Vector3 pos, int width, int height, Color color, int depth, bool addCollider)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;

            UITexture texture = go.AddComponent<UITexture>();
            texture.mainTexture = _whiteTexture;
            texture.width = width;
            texture.height = height;
            texture.depth = depth;
            texture.color = color;

            if (addCollider)
            {
                BoxCollider collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(width, height, 1);
            }

            return go;
        }

        private UILabel CreateLabel(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int depth)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;

            UILabel label = go.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = depth;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.bitmapFont = uiFont;
            label.trueTypeFont = ttfFont;
            return label;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int width, int height, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;

            UITexture background = go.AddComponent<UITexture>();
            background.mainTexture = _whiteTexture;
            background.width = width;
            background.height = height;
            background.depth = 100;
            background.color = onClick != null ? ColorButton : ColorDisabledButton;

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.layer = go.layer;

            UILabel label = labelGo.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = onClick != null ? color : ColorSubtext;
            label.depth = 101;
            label.alignment = NGUIText.Alignment.Center;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            label.width = width - 10;
            label.height = height;
            label.bitmapFont = uiFont;
            label.trueTypeFont = ttfFont;

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 1);

            UIButton button = go.AddComponent<UIButton>();
            button.tweenTarget = go;
            if (onClick != null)
                EventDelegate.Set(button.onClick, () => onClick());
            else
                button.isEnabled = false;

            return go;
        }

        private void Close()
        {
            if (_instance != null)
            {
                Destroy(_instance);
                _instance = null;
            }
        }
    }
}
