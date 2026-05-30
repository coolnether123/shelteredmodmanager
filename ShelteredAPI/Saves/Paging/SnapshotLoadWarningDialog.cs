using System;
using ModAPI.Core;
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
        private static Texture2D _whiteTexture;
        private bool _dontWarnAgain;
        private Action _onConfirm;
        private Action _onCancel;

        public static bool ShouldShow(int futureSnapshotCount)
        {
            return futureSnapshotCount > 0 && ModPrefs.GetInt(HideWarningPrefKey, 0) == 0;
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

            EnsureWhiteTexture();

            GameObject root = new GameObject("SnapshotLoadWarningDialog");
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

            SnapshotLoadWarningDialog dialog = root.AddComponent<SnapshotLoadWarningDialog>();
            dialog._onConfirm = onConfirm;
            dialog._onCancel = onCancel;
            dialog.BuildUI(root.transform, entry, futureSnapshotCount, uiFont, ttfFont);
        }

        private void BuildUI(Transform root, SaveEntry entry, int futureSnapshotCount, UIFont uiFont, Font ttfFont)
        {
            CreateTexturedBox(root, "DarkOverlay", Vector3.zero, 3000, 3000,
                new Color(0f, 0f, 0f, 0.72f), 0, true);

            CreateTexturedBox(root, "WindowBorder", Vector3.zero,
                WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);
            CreateTexturedBox(root, "WindowBackground", Vector3.zero,
                WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);

            UILabel title = CreateLabel(root, "Title", "LOAD BACKUP SNAPSHOT?",
                new Vector3(0, WINDOW_HEIGHT / 2 - 55, 0), 28, ColorHeader, uiFont, ttfFont, 100);
            title.alignment = NGUIText.Alignment.Center;

            string family = entry != null && entry.saveInfo != null && !string.IsNullOrEmpty(entry.saveInfo.familyName)
                ? entry.saveInfo.familyName
                : "this save";
            string plural = futureSnapshotCount == 1 ? "snapshot" : "snapshots";
            string message =
                "Loading this snapshot rolls back " + family + ".\n\n" +
                "The next save made from it will delete " + futureSnapshotCount + " newer backup " + plural + "\n" +
                "for this save, then continue the backup timeline from here.";

            UILabel body = CreateLabel(root, "Body", message,
                new Vector3(0, 45, 0), 20, ColorText, uiFont, ttfFont, 100);
            body.alignment = NGUIText.Alignment.Center;
            body.width = WINDOW_WIDTH - 80;
            body.overflowMethod = UILabel.Overflow.ResizeHeight;

            GameObject checkbox = BuildCheckbox(root, uiFont, ttfFont);
            checkbox.transform.localPosition = new Vector3(0, -95, 0);

            int buttonY = -WINDOW_HEIGHT / 2 + 62;
            CreateButton(root, "LoadBtn", "LOAD SNAPSHOT",
                new Vector3(-115, buttonY, 0), 18, Color.white, uiFont, ttfFont, 180, 46, Confirm);
            CreateButton(root, "CancelBtn", "CANCEL",
                new Vector3(115, buttonY, 0), 18, Color.white, uiFont, ttfFont, 150, 46, Cancel);
        }

        private GameObject BuildCheckbox(Transform root, UIFont uiFont, Font ttfFont)
        {
            GameObject container = new GameObject("CheckboxContainer");
            container.transform.SetParent(root, false);
            container.layer = root.gameObject.layer;

            GameObject box = CreateTexturedBox(container.transform, "CheckboxBg",
                new Vector3(-145, 0, 0), 22, 22, new Color(0.3f, 0.25f, 0.2f, 1f), 100, false);

            UILabel mark = CreateLabel(container.transform, "Checkmark", "X",
                new Vector3(-145, 0, 0), 18, new Color(0.35f, 0.95f, 0.35f), uiFont, ttfFont, 101);
            mark.alignment = NGUIText.Alignment.Center;
            mark.gameObject.SetActive(false);

            UILabel label = CreateLabel(container.transform, "CheckboxLabel",
                "I understand - don't warn me again",
                new Vector3(-115, 0, 0), 16, ColorSubtext, uiFont, ttfFont, 100);
            label.alignment = NGUIText.Alignment.Left;

            BoxCollider collider = box.AddComponent<BoxCollider>();
            collider.size = new Vector3(330, 34, 1);
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
            background.color = new Color(0.44f, 0.32f, 0.24f, 1f);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.layer = go.layer;

            UILabel label = labelGo.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
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
            EventDelegate.Set(button.onClick, () => onClick());

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
