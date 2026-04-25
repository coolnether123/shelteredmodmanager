using System;
using System.Text;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringNguiRenderModule : IScenarioAuthoringRenderModule
    {
        private const string OverlayName = "ShelteredAPI_ScenarioAuthoringOverlay";
        private readonly Color _shellColor = new Color(0.11f, 0.10f, 0.08f, 0.96f);
        private readonly Color _inspectorColor = new Color(0.09f, 0.10f, 0.12f, 0.96f);
        private readonly Color _hoverColor = new Color(0.12f, 0.09f, 0.06f, 0.96f);
        private readonly Color _borderColor = new Color(0.70f, 0.60f, 0.48f, 0.95f);
        private readonly Color _titleColor = new Color(0.96f, 0.87f, 0.72f, 1f);
        private readonly Color _subtitleColor = new Color(0.82f, 0.74f, 0.62f, 1f);
        private readonly Color _bodyColor = new Color(0.92f, 0.91f, 0.88f, 1f);

        private GameObject _root;
        private UIPanel _panel;
        private WindowView _shellWindow;
        private WindowView _inspectorWindow;
        private WindowView _hoverWindow;

        public string ModuleId
        {
            get { return "ShelteredAPI.NGUI"; }
        }

        public int Priority
        {
            get { return 0; }
        }

        public bool CanRender()
        {
            return UIRoot.list != null && UIRoot.list.Count > 0;
        }

        public void Render(ScenarioAuthoringPresentationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.State == null || !snapshot.State.IsActive)
            {
                Hide();
                return;
            }

            if (!EnsureUi())
                return;

            ApplyWindow(
                ref _shellWindow,
                "ShellWindow",
                snapshot.ShellDocument,
                snapshot.State != null && snapshot.State.ShellVisible,
                new Vector3(-520f, 240f, 0f),
                380,
                0,
                _shellColor);

            ApplyWindow(
                ref _inspectorWindow,
                "InspectorWindow",
                snapshot.InspectorDocument,
                snapshot.State != null && snapshot.State.SelectedTarget != null,
                new Vector3(500f, 220f, 0f),
                360,
                100,
                _inspectorColor);

            ApplyWindow(
                ref _hoverWindow,
                "HoverTooltipWindow",
                snapshot.HoverDocument,
                snapshot.State != null && snapshot.State.SelectionModeActive && snapshot.State.HoveredTarget != null,
                ResolveHoverPosition(),
                300,
                200,
                _hoverColor);
        }

        public void Hide()
        {
            SetWindowVisible(_shellWindow, false);
            SetWindowVisible(_inspectorWindow, false);
            SetWindowVisible(_hoverWindow, false);
        }

        private bool EnsureUi()
        {
            UIFontCache.RefreshIfMissing();
            _panel = UIUtil.EnsureOverlayPanel(OverlayName, 65000);
            if (_panel == null)
                return false;

            _root = _panel.gameObject;
            UIFontCache.SeedFromGameObject(_root, OverlayName);
            return true;
        }

        private void ApplyWindow(
            ref WindowView window,
            string name,
            ScenarioAuthoringInspectorDocument document,
            bool visible,
            Vector3 localPosition,
            int width,
            int depthOffset,
            Color fillColor)
        {
            if (!visible || document == null)
            {
                SetWindowVisible(window, false);
                return;
            }

            if (window == null)
                window = CreateWindow(name, width, depthOffset, fillColor);
            if (window == null || window.Root == null)
                return;

            string actions = ComposeActions(document);
            string body = ComposeBody(document);
            int height = EstimateHeight(width, body, actions);

            ConfigureWindow(window, document, actions, body, localPosition, width, height);
            window.Root.SetActive(true);
        }

        private WindowView CreateWindow(string name, int width, int depthOffset, Color fillColor)
        {
            if (_root == null)
                return null;

            UIFontCache.FontResult fonts = UIFontCache.GetFonts();
            int baseDepth = _panel != null ? _panel.depth + 1 + depthOffset : 65001 + depthOffset;
            GameObject root = new GameObject(name);
            root.transform.SetParent(_root.transform, false);
            root.layer = _root.layer;

            GameObject borderObject = new GameObject("Border");
            borderObject.transform.SetParent(root.transform, false);
            borderObject.layer = root.layer;
            UITexture border = borderObject.AddComponent<UITexture>();
            border.mainTexture = UIUtil.WhiteTexture;
            border.color = _borderColor;
            border.depth = baseDepth;

            GameObject backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(root.transform, false);
            backgroundObject.layer = root.layer;
            UITexture background = backgroundObject.AddComponent<UITexture>();
            background.mainTexture = UIUtil.WhiteTexture;
            background.color = fillColor;
            background.depth = baseDepth + 1;

            UILabel title = CreateLabel(root.transform, "Title", fonts, 24, _titleColor, baseDepth + 2, width - 24);
            UILabel subtitle = CreateLabel(root.transform, "Subtitle", fonts, 16, _subtitleColor, baseDepth + 3, width - 24);
            UILabel actions = CreateLabel(root.transform, "Actions", fonts, 14, _subtitleColor, baseDepth + 3, width - 24);
            UILabel body = CreateLabel(root.transform, "Body", fonts, 15, _bodyColor, baseDepth + 4, width - 24);

            body.overflowMethod = UILabel.Overflow.ResizeHeight;
            actions.overflowMethod = UILabel.Overflow.ResizeHeight;
            subtitle.overflowMethod = UILabel.Overflow.ResizeHeight;

            return new WindowView
            {
                Root = root,
                Border = border,
                Background = background,
                Title = title,
                Subtitle = subtitle,
                Actions = actions,
                Body = body
            };
        }

        private void ConfigureWindow(WindowView window, ScenarioAuthoringInspectorDocument document, string actions, string body, Vector3 localPosition, int width, int height)
        {
            int innerWidth = width - 10;
            window.Root.transform.localPosition = localPosition;

            window.Border.width = width;
            window.Border.height = height;
            window.Border.pivot = UIWidget.Pivot.TopLeft;
            window.Border.transform.localPosition = Vector3.zero;

            window.Background.width = width - 4;
            window.Background.height = height - 4;
            window.Background.pivot = UIWidget.Pivot.TopLeft;
            window.Background.transform.localPosition = new Vector3(2f, -2f, 0f);

            ConfigureLabel(window.Title, document.Title ?? string.Empty, new Vector3(12f, -12f, 0f), innerWidth - 14, 28);
            ConfigureLabel(window.Subtitle, document.Subtitle ?? string.Empty, new Vector3(12f, -42f, 0f), innerWidth - 14, 18);
            ConfigureLabel(window.Actions, actions, new Vector3(12f, -68f, 0f), innerWidth - 14, 18);
            ConfigureLabel(window.Body, body, new Vector3(12f, -96f, 0f), innerWidth - 14, Math.Max(90, height - 112));
        }

        private static UILabel CreateLabel(Transform parent, string name, UIFontCache.FontResult fonts, int fontSize, Color color, int depth, int width)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            UILabel label = go.AddComponent<UILabel>();
            if (fonts.Bitmap != null)
                label.bitmapFont = fonts.Bitmap;
            else
                label.trueTypeFont = fonts.TTF;

            label.fontSize = fontSize;
            label.color = color;
            label.depth = depth;
            label.pivot = UIWidget.Pivot.TopLeft;
            label.alignment = NGUIText.Alignment.Left;
            label.overflowMethod = UILabel.Overflow.ResizeHeight;
            label.width = width;
            return label;
        }

        private static void ConfigureLabel(UILabel label, string text, Vector3 localPosition, int width, int height)
        {
            if (label == null)
                return;

            label.transform.localPosition = localPosition;
            label.width = width;
            label.height = height;
            label.text = text ?? string.Empty;
        }

        private static void SetWindowVisible(WindowView window, bool visible)
        {
            if (window != null && window.Root != null)
                window.Root.SetActive(visible);
        }

        private static string ComposeActions(ScenarioAuthoringInspectorDocument document)
        {
            if (document == null || document.HeaderActions == null || document.HeaderActions.Length == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < document.HeaderActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = document.HeaderActions[i];
                if (action == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append("  ");
                builder.Append(action.Enabled ? "[" : "(")
                    .Append(action.Label ?? string.Empty)
                    .Append(action.Enabled ? "]" : ")");
            }

            return builder.ToString();
        }

        private static string ComposeBody(ScenarioAuthoringInspectorDocument document)
        {
            if (document == null || document.Sections == null || document.Sections.Length == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < document.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = document.Sections[i];
                if (section == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append("\n");
                if (!string.IsNullOrEmpty(section.Title))
                    builder.Append(section.Title).Append("\n");

                ScenarioAuthoringInspectorItem[] items = section.Items;
                for (int j = 0; items != null && j < items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = items[j];
                    if (item == null)
                        continue;

                    switch (item.Kind)
                    {
                        case ScenarioAuthoringInspectorItemKind.Property:
                            builder.Append(" - ").Append(item.Label ?? string.Empty).Append(": ").Append(item.Value ?? string.Empty).Append("\n");
                            break;
                        case ScenarioAuthoringInspectorItemKind.Action:
                            if (item.Action != null)
                                builder.Append(" - Action: ").Append(item.Action.Label ?? string.Empty).Append("\n");
                            break;
                        default:
                            builder.Append(" - ").Append(item.Value ?? string.Empty).Append("\n");
                            break;
                    }
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static int EstimateHeight(int width, string body, string actions)
        {
            int lineCount = 0;
            lineCount += CountWrappedLines(actions, width, 14);
            lineCount += CountWrappedLines(body, width, 15);
            return Mathf.Clamp(120 + (lineCount * 18), 130, 460);
        }

        private static int CountWrappedLines(string text, int width, int fontSize)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int maxChars = Mathf.Max(24, (int)((width - 28) / Mathf.Max(6f, fontSize * 0.5f)));
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                count += Mathf.Max(1, (int)Math.Ceiling((double)line.Length / maxChars));
            }

            return count;
        }

        private static Vector3 ResolveHoverPosition()
        {
            UIRoot root = UIRoot.list != null && UIRoot.list.Count > 0 ? UIRoot.list[0] : null;
            if (root == null)
                return new Vector3(220f, 120f, 0f);

            float ratio = (float)root.activeHeight / Math.Max(1, Screen.height);
            float x = (UnityEngine.Input.mousePosition.x - Screen.width * 0.5f) * ratio;
            float y = (UnityEngine.Input.mousePosition.y - Screen.height * 0.5f) * ratio;
            x = Mathf.Clamp(x + 26f, -520f, 500f);
            y = Mathf.Clamp(y + 40f, -240f, 260f);
            return new Vector3(x, y, 0f);
        }

        private sealed class WindowView
        {
            public GameObject Root;
            public UITexture Border;
            public UITexture Background;
            public UILabel Title;
            public UILabel Subtitle;
            public UILabel Actions;
            public UILabel Body;
        }
    }
}
