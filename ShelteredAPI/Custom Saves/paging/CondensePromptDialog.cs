using System;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.UI;

namespace ModAPI.Hooks.Paging
{
    /// <summary>
    /// Dialog shown at startup when save slot gaps are detected.
    /// Asks user if they want to auto-condense saves.
    /// </summary>
    internal class CondensePromptDialog : MonoBehaviour
    {
        private static GameObject _instance;
        private static Texture2D _whiteTexture;
        
        private const int WINDOW_WIDTH = 620;
        private const int WINDOW_HEIGHT = 400;
        
        private static readonly Color COLOR_HEADER = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color COLOR_TEXT = Color.white;
        private static readonly Color COLOR_SUBTEXT = new Color(0.7f, 0.7f, 0.7f);
        
        private bool _rememberChoice = false;
        private UISprite _checkboxSprite;

        public static void Show()
        {
            if (_instance != null) Destroy(_instance);

            var panel = UIUtil.EnsureOverlayPanel("ModAPI_CondensePromptDialog", 10001);
            if (panel == null) 
            {
                MMLog.WriteError("[CondensePromptDialog] Failed to create overlay panel!");
                return;
            }
            
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(2, 2);
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        _whiteTexture.SetPixel(x, y, Color.white);
                _whiteTexture.Apply();
            }

            var root = new GameObject("CondensePromptDialog");
            root.transform.SetParent(panel.transform, false);
            root.layer = panel.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            
            _instance = root;

            UIFont uiFont = null;
            Font ttfFont = null;
            var sampleLabel = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (sampleLabel != null)
            {
                uiFont = sampleLabel.bitmapFont;
                ttfFont = sampleLabel.trueTypeFont;
            }
            if (uiFont == null && ttfFont == null)
                ttfFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var dialog = root.AddComponent<CondensePromptDialog>();
            dialog.BuildUI(root.transform, uiFont, ttfFont);
        }

        private void BuildUI(Transform root, UIFont uiFont, Font ttfFont)
        {
            // Dark overlay
            CreateTexturedBox(root, "DarkOverlay", Vector3.zero, 3000, 3000, 
                new Color(0f, 0f, 0f, 0.7f), 0, true);
            
            // Window background
            CreateTexturedBox(root, "WindowBackground", Vector3.zero, 
                WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);
            CreateTexturedBox(root, "WindowBorder", Vector3.zero, 
                WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);

            // Title
            var titleLabel = CreateLabel(root, "Title", "ORGANIZE SAVE SLOTS?",
                new Vector3(0, WINDOW_HEIGHT/2 - 50, 0), 28, COLOR_HEADER, uiFont, ttfFont, 100);
            titleLabel.alignment = NGUIText.Alignment.Center;
            
            // Description
            var descLabel = CreateLabel(root, "Description", 
                "Gaps were detected in your save slot numbers.\n\n" +
                "Would you like to automatically reorganize\n" +
                "your saves to fill these gaps?\n\n" +
                "This will renumber slot positions but won't\n" +
                "delete or modify your actual save data.",
                new Vector3(0, 15, 0), 20, COLOR_TEXT, uiFont, ttfFont, 100);
            descLabel.alignment = NGUIText.Alignment.Center;
            
            // Remember choice checkbox
            int checkboxY = -95;
            var checkboxContainer = new GameObject("CheckboxContainer");
            checkboxContainer.transform.SetParent(root, false);
            checkboxContainer.layer = root.gameObject.layer;
            checkboxContainer.transform.localPosition = new Vector3(0, checkboxY, 0);
            
            // Checkbox background
            var checkboxBg = CreateTexturedBox(checkboxContainer.transform, "CheckboxBg", 
                new Vector3(-100, 0, 0), 20, 20, new Color(0.3f, 0.25f, 0.2f, 1f), 100, false);
            
            // Checkbox checkmark (initially hidden)
            var checkmark = CreateLabel(checkboxContainer.transform, "Checkmark", "✓",
                new Vector3(-100, 0, 0), 18, new Color(0.3f, 0.9f, 0.3f), uiFont, ttfFont, 101);
            checkmark.alignment = NGUIText.Alignment.Center;
            checkmark.gameObject.SetActive(false);
            
            // Checkbox label
            var checkboxLabel = CreateLabel(checkboxContainer.transform, "CheckboxLabel", 
                "Remember my choice",
                new Vector3(10, 0, 0), 16, COLOR_SUBTEXT, uiFont, ttfFont, 100);
            checkboxLabel.alignment = NGUIText.Alignment.Left;
            
            // Add click handler to checkbox area
            var checkboxCol = checkboxBg.AddComponent<BoxCollider>();
            checkboxCol.size = new Vector3(200, 30, 1);
            checkboxCol.center = new Vector3(60, 0, 0);
            
            var checkboxBtn = checkboxBg.AddComponent<UIButton>();
            checkboxBtn.tweenTarget = checkboxBg;
            EventDelegate.Set(checkboxBtn.onClick, () => {
                _rememberChoice = !_rememberChoice;
                checkmark.gameObject.SetActive(_rememberChoice);
            });
            
            // Add hover tooltip
            UIHelper.AddTooltip(checkboxBg, root, "(Can be changed later in Manager.exe settings)", uiFont, ttfFont);
            
            // Button row
            int buttonY = -WINDOW_HEIGHT/2 + 60;
            Color btnColor = new Color(113f/255f, 82f/255f, 62f/255f);
            
            // YES button
            var yesBtn = CreateButton(root, "YesBtn", "YES, ORGANIZE",
                new Vector3(-100, buttonY, 0), 18, Color.white, uiFont, ttfFont, 160, 45,
                () => {
                    SaveCondenseManager.OnUserChoice(true, _rememberChoice);
                    Close();
                });
            var yesTex = yesBtn.GetComponent<UITexture>();
            if (yesTex != null) yesTex.color = btnColor;
            
            // NO button
            var noBtn = CreateButton(root, "NoBtn", "NO, KEEP AS-IS",
                new Vector3(100, buttonY, 0), 18, Color.white, uiFont, ttfFont, 160, 45,
                () => {
                    SaveCondenseManager.OnUserChoice(false, _rememberChoice);
                    Close();
                });
            var noTex = noBtn.GetComponent<UITexture>();
            if (noTex != null) noTex.color = btnColor;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SaveCondenseManager.OnUserChoice(false, false);
                Close();
            }
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
            bg.color = new Color(0.44f, 0.32f, 0.24f, 1f);
            
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.layer = go.layer;
            
            var label = labelGo.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = 101;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.alignment = NGUIText.Alignment.Center;
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
