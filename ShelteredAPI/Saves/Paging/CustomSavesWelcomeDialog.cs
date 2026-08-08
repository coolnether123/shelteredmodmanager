using System;
using UnityEngine;
using ModAPI.Core;
using ShelteredAPI.UI;
using ShelteredAPI.UI.Compatibility;

namespace ShelteredAPI.Saves.Paging
{
    /// <summary>
    /// Readable first-time onboarding dialog for custom save paging.
    /// Replaces the tiny vanilla MessageBox rendering on some resolutions.
    /// </summary>
    internal class CustomSavesWelcomeDialog : MonoBehaviour
    {
        private static GameObject _instance;

        private const int WINDOW_WIDTH = 920;
        private const int WINDOW_HEIGHT = 440;
        private const int CONTENT_WIDTH = WINDOW_WIDTH - 96;
        private const int TITLE_HEIGHT = 48;
        private const int BODY_HEIGHT = 220;
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

            var root = NguiModalPrimitives.CreateRoot(panel, "CustomSavesWelcomeDialog");
            _instance = root;

            UIFontCache.SeedFromGameObject(panel.gameObject, "CustomSavesWelcomeDialog");
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();

            var dlg = root.AddComponent<CustomSavesWelcomeDialog>();
            dlg.BuildUI(root.transform, fonts.Bitmap, fonts.TTF);
        }

        private void BuildUI(Transform root, UIFont uiFont, Font ttfFont)
        {
            NguiModalPrimitives.CreateBox(root, "Overlay", Vector3.zero, 3000, 3000, new Color(0f, 0f, 0f, 0.7f), 0, true);
            NguiModalPrimitives.CreateBox(root, "PanelBorder", Vector3.zero, WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, ColorBorder, 9, false);
            NguiModalPrimitives.CreateBox(root, "Panel", Vector3.zero, WINDOW_WIDTH, WINDOW_HEIGHT, ColorPanel, 10, false);

            var title = NguiModalPrimitives.CreateLabel(root, "Title", "WELCOME TO CUSTOM SAVES",
                new Vector3(0, WINDOW_HEIGHT / 2 - 58, 0), 32, ColorHeader, uiFont, ttfFont, 20);
            ConfigureTextBox(title, CONTENT_WIDTH, TITLE_HEIGHT, NGUIText.Alignment.Center, false);
            title.overflowMethod = UILabel.Overflow.ClampContent;

            string bodyText =
                "Pages 2+ contain unlimited custom save slots.\n" +
                "Use arrows or keyboard to navigate pages.\n\n" +
                "Custom saves keep their slot numbers unless reorganized.\n" +
                "If deleting saves leaves gaps, startup asks whether to\n" +
                "compact slot numbering.\n\n" +
                "Slots 1-3 are still vanilla slots.";

            var body = NguiModalPrimitives.CreateLabel(root, "Body", bodyText,
                new Vector3(0, 18, 0), 22, ColorText, uiFont, ttfFont, 20);
            ConfigureTextBox(body, CONTENT_WIDTH, BODY_HEIGHT, NGUIText.Alignment.Center, true);
            body.overflowMethod = UILabel.Overflow.ShrinkContent;
            body.spacingY = 4;

            int buttonY = -WINDOW_HEIGHT / 2 + 58;
            NguiModalPrimitives.CreateButton(root, "OkayBtn", "OKAY", new Vector3(0, buttonY, 0),
                240, 58, 26, uiFont, ttfFont, ColorButton, Color.white, Close);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.Return))
                Close();
        }

        private static void ConfigureTextBox(UILabel label, int width, int height, NGUIText.Alignment alignment, bool multiLine)
        {
            if (label == null)
                return;

            label.pivot = UIWidget.Pivot.Center;
            label.alignment = alignment;
            label.width = width;
            label.height = height;
            label.multiLine = multiLine;
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
