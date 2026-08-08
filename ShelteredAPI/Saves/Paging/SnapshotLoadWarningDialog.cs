using System;
using ModAPI.Core;
using ShelteredAPI.UI;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Saves.Paging
{
    internal class SnapshotLoadWarningDialog : MonoBehaviour
    {
        private const string HideWarningPrefKey = "ModAPI_HideSnapshotBranchWarning";
        private const int WINDOW_WIDTH = 720;
        private const int WINDOW_HEIGHT = 430;

        private static readonly Color ColorHeader = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color ColorText = Color.white;
        private static readonly Color ColorSubtext = new Color(0.72f, 0.72f, 0.72f);

        private static GameObject _instance;
        private bool _dontWarnAgain;
        private Action _onConfirm;
        private Action _onCancel;

        public static bool ShouldShow(int futureSnapshotCount)
        {
            return ModPrefs.GetInt(HideWarningPrefKey, 0) == 0;
        }

        public static void Show(SaveEntry entry, int futureSnapshotCount, Action onConfirm, Action onCancel)
        {
            if (_instance != null)
                Destroy(_instance);

            var panel = UIUtil.EnsureOverlayPanel("ModAPI_SnapshotLoadWarningDialog", 10002);
            if (panel == null)
            {
                MMLog.WriteError("[SnapshotLoadWarningDialog] Failed to create overlay panel.");
                onCancel?.Invoke();
                return;
            }

            GameObject root = NguiModalPrimitives.CreateRoot(panel, "SnapshotLoadWarningDialog");
            _instance = root;

            UIFontCache.SeedFromGameObject(panel.gameObject, "SnapshotLoadWarningDialog");
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();

            SnapshotLoadWarningDialog dialog = root.AddComponent<SnapshotLoadWarningDialog>();
            dialog._onConfirm = onConfirm;
            dialog._onCancel = onCancel;
            dialog.BuildUI(root.transform, entry, futureSnapshotCount, fonts.Bitmap, fonts.TTF);
        }

        private void BuildUI(Transform root, SaveEntry entry, int futureSnapshotCount, UIFont uiFont, Font ttfFont)
        {
            NguiModalPrimitives.CreateBox(root, "DarkOverlay", Vector3.zero, 3000, 3000,
                new Color(0f, 0f, 0f, 0.72f), 0, true);

            NguiModalPrimitives.CreateBox(root, "WindowBorder", Vector3.zero,
                WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);
            NguiModalPrimitives.CreateBox(root, "WindowBackground", Vector3.zero,
                WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);

            UILabel title = NguiModalPrimitives.CreateLabel(root, "Title", "LOAD BACKUP SNAPSHOT?",
                new Vector3(0, WINDOW_HEIGHT / 2 - 55, 0), 28, ColorHeader, uiFont, ttfFont, 100);
            title.alignment = NGUIText.Alignment.Center;

            string family = entry != null && entry.saveInfo != null && !string.IsNullOrEmpty(entry.saveInfo.familyName)
                ? entry.saveInfo.familyName
                : "this save";
            string message =
                "Loading this snapshot rolls back " + family + ".\n\n" +
                "Current progress is preserved first as a safety snapshot.\n" +
                "Newer snapshots remain available in this archive.";

            UILabel body = NguiModalPrimitives.CreateLabel(root, "Body", message,
                new Vector3(0, 50, 0), 19, ColorText, uiFont, ttfFont, 100);
            body.alignment = NGUIText.Alignment.Center;
            body.pivot = UIWidget.Pivot.Center;
            body.width = WINDOW_WIDTH - 70;
            body.height = 120;
            body.overflowMethod = UILabel.Overflow.ShrinkContent;

            GameObject checkbox = BuildCheckbox(root, uiFont, ttfFont);
            checkbox.transform.localPosition = new Vector3(0, -82, 0);

            int buttonY = -WINDOW_HEIGHT / 2 + 62;
            NguiModalPrimitives.CreateButton(root, "LoadBtn", "LOAD SNAPSHOT",
                new Vector3(-115, buttonY, 0), 180, 46, 18, uiFont, ttfFont, new Color(0.44f, 0.32f, 0.24f, 1f), Color.white, Confirm);
            NguiModalPrimitives.CreateButton(root, "CancelBtn", "CANCEL",
                new Vector3(115, buttonY, 0), 150, 46, 18, uiFont, ttfFont, new Color(0.44f, 0.32f, 0.24f, 1f), Color.white, Cancel);
        }

        private GameObject BuildCheckbox(Transform root, UIFont uiFont, Font ttfFont)
        {
            GameObject container = new GameObject("CheckboxContainer");
            container.transform.SetParent(root, false);
            container.layer = root.gameObject.layer;

            GameObject box = NguiModalPrimitives.CreateBox(container.transform, "CheckboxBg",
                new Vector3(-150, 0, 0), 22, 22, new Color(0.3f, 0.25f, 0.2f, 1f), 100, false);

            UILabel mark = NguiModalPrimitives.CreateLabel(container.transform, "Checkmark", "X",
                new Vector3(-150, 0, 0), 18, new Color(0.35f, 0.95f, 0.35f), uiFont, ttfFont, 101);
            mark.alignment = NGUIText.Alignment.Center;
            mark.gameObject.SetActive(false);

            UILabel label = NguiModalPrimitives.CreateLabel(container.transform, "CheckboxLabel",
                "Don't show this warning again",
                new Vector3(10, 0, 0), 16, ColorSubtext, uiFont, ttfFont, 100);
            label.alignment = NGUIText.Alignment.Center;
            label.pivot = UIWidget.Pivot.Center;
            label.width = 300;
            label.height = 32;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;

            BoxCollider collider = box.AddComponent<BoxCollider>();
            collider.size = new Vector3(340, 36, 1);
            collider.center = new Vector3(150, 0, 0);

            UIButton button = box.AddComponent<UIButton>();
            button.tweenTarget = box;
            EventDelegate.Set(button.onClick, () =>
            {
                _dontWarnAgain = !_dontWarnAgain;
                mark.gameObject.SetActive(_dontWarnAgain);
            });

            return container;
        }

        private void Confirm()
        {
            if (_dontWarnAgain)
            {
                ModPrefs.SetInt(HideWarningPrefKey, 1);
                ModPrefs.Save();
            }

            Action confirm = _onConfirm;
            Close();
            if (confirm != null)
                confirm();
        }

        private void Cancel()
        {
            Action cancel = _onCancel;
            Close();
            if (cancel != null)
                cancel();
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
