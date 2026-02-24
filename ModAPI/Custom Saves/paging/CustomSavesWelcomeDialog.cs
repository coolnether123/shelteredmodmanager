using System;
using UnityEngine;
using ModAPI.Core;
using ModAPI.UI;

namespace ModAPI.Hooks.Paging
{
    /// <summary>
    /// Readable first-time onboarding dialog for custom save paging.
    /// Replaces the tiny vanilla MessageBox rendering on some resolutions.
    /// </summary>
    internal class CustomSavesWelcomeDialog : MonoBehaviour
    {
        private static GameObject _instance;
        private static Texture2D _whiteTexture;

        private const int WINDOW_WIDTH = 860;
        private const int WINDOW_HEIGHT = 520;

        private static readonly Color ColorHeader = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color ColorText = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color ColorPanel = new Color(0.15f, 0.12f, 0.1f, 0.98f);
        private static readonly Color ColorBorder = new Color(0.5f, 0.4f, 0.3f, 1f);
        private static readonly Color ColorButton = new Color(113f / 255f, 82f / 255f, 62f / 255f, 1f);

        public static void Show()
        {
            if (_instance != null)
                Destroy(_instance);

            var panel = UIUtil.EnsureOverlayPanel("ModAPI_CustomSavesWelcomeDialog", 10002);
            if (panel == null)
            {
                MMLog.WriteError("[CustomSavesWelcomeDialog] Failed to create overlay panel.");
                return;
            }

            EnsureWhiteTexture();

            var root = new GameObject("CustomSavesWelcomeDialog");
            root.transform.SetParent(panel.transform, false);
            root.layer = panel.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            _instance = root;

            UIFont uiFont = null;
            Font ttfFont = null;
            var sample = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (sample != null)
            {
                uiFont = sample.bitmapFont;
                ttfFont = sample.trueTypeFont;
            }
            if (uiFont == null && ttfFont == null)
                ttfFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var dlg = root.AddComponent<CustomSavesWelcomeDialog>();
            dlg.BuildUI(root.transform, uiFont, ttfFont);
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

        private void BuildUI(Transform root, UIFont uiFont, Font ttfFont)
        {
            CreateTexturedBox(root, "Overlay", Vector3.zero, 3000, 3000, new Color(0f, 0f, 0f, 0.7f), 0, true);
            CreateTexturedBox(root, "PanelBorder", Vector3.zero, WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, ColorBorder, 9, false);
            CreateTexturedBox(root, "Panel", Vector3.zero, WINDOW_WIDTH, WINDOW_HEIGHT, ColorPanel, 10, false);

            var title = CreateLabel(root, "Title", "WELCOME TO CUSTOM SAVES",
                new Vector3(0, WINDOW_HEIGHT / 2 - 60, 0), 34, ColorHeader, uiFont, ttfFont, 20);
            title.alignment = NGUIText.Alignment.Center;

            string bodyText =
                "Pages 2+ contain unlimited custom save slots.\n" +
                "Use the arrows or keyboard to navigate pages.\n\n" +
                "Custom saves stay in their slot numbers unless you reorganize them.\n" +
                "If gaps are detected after deleting saves, you will be asked at startup\n" +
                "whether to compact slot numbering.\n\n" +
                "Slots 1-3 are vanilla saves.";

            var body = CreateLabel(root, "Body", bodyText,
                new Vector3(0, 40, 0), 24, ColorText, uiFont, ttfFont, 20);
            body.alignment = NGUIText.Alignment.Center;
            body.width = WINDOW_WIDTH - 120;
            body.overflowMethod = UILabel.Overflow.ResizeHeight;
            body.spacingY = 6;

            int buttonY = -WINDOW_HEIGHT / 2 + 68;
            var ok = CreateButton(root, "OkayBtn", "OKAY", new Vector3(0, buttonY, 0),
                26, Color.white, uiFont, ttfFont, 240, 58, Close);
            var okTex = ok.GetComponent<UITexture>();
            if (okTex != null)
                okTex.color = ColorButton;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                Close();
        }

        private GameObject CreateTexturedBox(Transform parent, string name, Vector3 pos, int w, int h, Color color, int depth, bool addCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;

            var tex = go.AddComponent<UITexture>();
            tex.mainTexture = _whiteTexture;
            tex.width = w;
            tex.height = h;
            tex.depth = depth;
            tex.color = color;

            if (addCollider)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(w, h, 1);
            }

            return go;
        }

        private UILabel CreateLabel(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;

            var label = go.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = depth;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.bitmapFont = uiFont;
            label.trueTypeFont = ttfFont;
            return label;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int w, int h, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;

            var bg = go.AddComponent<UITexture>();
            bg.mainTexture = _whiteTexture;
            bg.width = w;
            bg.height = h;
            bg.depth = 100;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.layer = go.layer;

            var label = labelGo.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = 101;
            label.alignment = NGUIText.Alignment.Center;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.bitmapFont = uiFont;
            label.trueTypeFont = ttfFont;

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(w, h, 1);

            var btn = go.AddComponent<UIButton>();
            btn.tweenTarget = go;
            if (onClick != null)
                EventDelegate.Set(btn.onClick, () => onClick());

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
