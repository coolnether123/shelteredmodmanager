using System;
using ModAPI.Core;
using ShelteredAPI.UI;
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

            GameObject root = NguiModalPrimitives.CreateRoot(panel, "VanillaMirrorConflictDialog");
            _instance = root;

            UIFontCache.SeedFromGameObject(panel.gameObject, "VanillaMirrorConflictDialog");
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();

            VanillaMirrorConflictDialog dialog = root.AddComponent<VanillaMirrorConflictDialog>();
            dialog._onLoadVanilla = onLoadVanilla;
            dialog._onLoadEdited = onLoadEdited;
            dialog._onCancel = onCancel;
            dialog.BuildUI(root.transform, message, fonts.Bitmap, fonts.TTF);
        }

        private void BuildUI(Transform root, string message, UIFont uiFont, Font ttfFont)
        {
            NguiModalPrimitives.CreateBox(root, "DarkOverlay", Vector3.zero, 3000, 3000,
                new Color(0f, 0f, 0f, 0.72f), 0, true);

            NguiModalPrimitives.CreateBox(root, "WindowBorder", Vector3.zero,
                WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);
            NguiModalPrimitives.CreateBox(root, "WindowBackground", Vector3.zero,
                WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);

            UILabel title = NguiModalPrimitives.CreateLabel(root, "Title", "VANILLA SAVE STATE",
                new Vector3(0, WINDOW_HEIGHT / 2 - 58, 0), 28, ColorHeader, uiFont, ttfFont, 100);
            title.alignment = NGUIText.Alignment.Center;

            UILabel body = NguiModalPrimitives.CreateLabel(root, "Body", message,
                new Vector3(0, 40, 0), 21, ColorText, uiFont, ttfFont, 100);
            body.alignment = NGUIText.Alignment.Center;
            body.width = WINDOW_WIDTH - 110;
            body.overflowMethod = UILabel.Overflow.ResizeHeight;

            UILabel hint = NguiModalPrimitives.CreateLabel(root, "Hint",
                "Cancel leaves both files unchanged.",
                new Vector3(0, -48, 0), 16, ColorSubtext, uiFont, ttfFont, 100);
            hint.alignment = NGUIText.Alignment.Center;

            int buttonY = -WINDOW_HEIGHT / 2 + 58;
            NguiModalPrimitives.CreateButton(root, "LoadVanillaBtn", "Load Vanilla State",
                new Vector3(-245, buttonY, 0), 220, 48, 17, uiFont, ttfFont,
                _onLoadVanilla != null ? ColorButton : ColorDisabledButton,
                _onLoadVanilla != null ? Color.white : ColorSubtext,
                _onLoadVanilla != null ? (Action)LoadVanilla : null);
            NguiModalPrimitives.CreateButton(root, "LoadEditedBtn", "Load Edited XML State",
                new Vector3(0, buttonY, 0), 235, 48, 17, uiFont, ttfFont,
                _onLoadEdited != null ? ColorButton : ColorDisabledButton,
                _onLoadEdited != null ? Color.white : ColorSubtext,
                _onLoadEdited != null ? (Action)LoadEdited : null);
            NguiModalPrimitives.CreateButton(root, "CancelBtn", "Cancel",
                new Vector3(245, buttonY, 0), 150, 48, 17, uiFont, ttfFont, ColorButton, Color.white, Cancel);
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
