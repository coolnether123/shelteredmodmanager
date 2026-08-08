using System;
using UnityEngine;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.UI;
using ShelteredAPI.UI.Compatibility;

namespace ShelteredAPI.Saves.Paging
{
    /// <summary>
    /// Dialog shown at startup when save slot gaps are detected.
    /// Asks user if they want to auto-condense saves.
    /// </summary>
    internal class CondensePromptDialog : MonoBehaviour
    {
        private static GameObject _instance;
        
        private const int WINDOW_WIDTH = 620;
        private const int WINDOW_HEIGHT = 400;
        
        private static readonly Color COLOR_HEADER = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color COLOR_TEXT = Color.white;
        private static readonly Color COLOR_SUBTEXT = new Color(0.7f, 0.7f, 0.7f);
        
        private bool _rememberChoice = false;

        public static void Show()
        {
            if (_instance != null) Destroy(_instance);

            var panel = UIUtil.EnsureOverlayPanel("ModAPI_CondensePromptDialog", 10001);
            if (panel == null) 
            {
                MMLog.WriteError("[CondensePromptDialog] Failed to create overlay panel!");
                return;
            }
            
            var root = NguiModalPrimitives.CreateRoot(panel, "CondensePromptDialog");
            
            _instance = root;

            UIFontCache.SeedFromGameObject(panel.gameObject, "CondensePromptDialog");
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();

            var dialog = root.AddComponent<CondensePromptDialog>();
            dialog.BuildUI(root.transform, fonts.Bitmap, fonts.TTF);
        }

        private void BuildUI(Transform root, UIFont uiFont, Font ttfFont)
        {
            // Dark overlay
            NguiModalPrimitives.CreateBox(root, "DarkOverlay", Vector3.zero, 3000, 3000,
                new Color(0f, 0f, 0f, 0.7f), 0, true);
            
            // Window background
            NguiModalPrimitives.CreateBox(root, "WindowBackground", Vector3.zero,
                WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);
            NguiModalPrimitives.CreateBox(root, "WindowBorder", Vector3.zero,
                WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);

            // Title
            var titleLabel = NguiModalPrimitives.CreateLabel(root, "Title", "ORGANIZE SAVE SLOTS?",
                new Vector3(0, WINDOW_HEIGHT/2 - 50, 0), 28, COLOR_HEADER, uiFont, ttfFont, 100);
            titleLabel.alignment = NGUIText.Alignment.Center;
            
            // Description
            var descLabel = NguiModalPrimitives.CreateLabel(root, "Description",
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
            var checkboxBg = NguiModalPrimitives.CreateBox(checkboxContainer.transform, "CheckboxBg",
                new Vector3(-100, 0, 0), 20, 20, new Color(0.3f, 0.25f, 0.2f, 1f), 100, false);
            
            // Checkbox checkmark (initially hidden)
            var checkmark = NguiModalPrimitives.CreateLabel(checkboxContainer.transform, "Checkmark", "✓",
                new Vector3(-100, 0, 0), 18, new Color(0.3f, 0.9f, 0.3f), uiFont, ttfFont, 101);
            checkmark.alignment = NGUIText.Alignment.Center;
            checkmark.gameObject.SetActive(false);
            
            // Checkbox label
            var checkboxLabel = NguiModalPrimitives.CreateLabel(checkboxContainer.transform, "CheckboxLabel",
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
            NguiModalPrimitives.CreateButton(root, "YesBtn", "YES, ORGANIZE",
                new Vector3(-100, buttonY, 0), 160, 45, 18, uiFont, ttfFont, btnColor, Color.white,
                () => {
                    SaveCondenseManager.OnUserChoice(true, _rememberChoice);
                    Close();
                });
            // NO button
            NguiModalPrimitives.CreateButton(root, "NoBtn", "NO, KEEP AS-IS",
                new Vector3(100, buttonY, 0), 160, 45, 18, uiFont, ttfFont, btnColor, Color.white,
                () => {
                    SaveCondenseManager.OnUserChoice(false, _rememberChoice);
                    Close();
                });
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                SaveCondenseManager.OnUserChoice(false, false);
                Close();
            }
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
