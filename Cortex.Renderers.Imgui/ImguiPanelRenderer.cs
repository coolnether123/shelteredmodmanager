using System;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi.Panels;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiPanelRenderer : IPanelRenderer, IDisposable
    {
        private const float CardPadding = 8f;
        private const float DividerHeight = 1f;
        private const float LabelWidth = 96f;
        private const float CardActionButtonHeight = 24f;
        private const float CardActionButtonSpacing = 4f;

        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;
        private readonly PanelThemeResources _themeResources = new PanelThemeResources();
        private readonly IPanelLayoutMeasurer _layoutMeasurer;

        public ImguiPanelRenderer()
            : this(new ImguiModuleResources(), true)
        {
        }

        internal ImguiPanelRenderer(ImguiModuleResources moduleResources)
            : this(moduleResources, false)
        {
        }

        private ImguiPanelRenderer(ImguiModuleResources moduleResources, bool ownsModuleResources)
        {
            _moduleResources = moduleResources ?? new ImguiModuleResources();
            _ownsModuleResources = ownsModuleResources;
            _layoutMeasurer = new ImguiPanelLayoutMeasurer(this);
        }

        public void Dispose()
        {
            if (_ownsModuleResources)
            {
                _moduleResources.Dispose();
            }
        }

        public PanelRenderResult Draw(RenderRect rect, PanelDocument document, RenderPoint scroll, PanelThemePalette theme)
        {
            EnsureStyles(ImguiPanelThemeFactory.Create(theme));
            var result = new PanelRenderResult();
            result.Scroll = scroll;
            if (document == null)
            {
                return result;
            }

            var unityRect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            var rootLayout = PanelLayoutPlanner.BuildRootLayout(new RenderRect(0f, 0f, unityRect.width, unityRect.height), document.HeaderActions);
            var contentWidth = Mathf.Max(PanelLayoutPlanner.MinContentWidth, rootLayout.ContentViewport.Width - 16f);
            var contentLayout = PanelLayoutPlanner.BuildContentLayout(document, contentWidth, _layoutMeasurer);
            var contentRect = new Rect(0f, 0f, contentWidth, contentLayout.TotalHeight);

            GUI.BeginGroup(unityRect);
            try
            {
                DrawChrome(rootLayout, document.HeaderActions != null && document.HeaderActions.Length > 0);
                DrawHeader(document, rootLayout, ref result);
                DrawHeaderActions(document.HeaderActions, rootLayout, ref result);

                var scrollPosition = GUI.BeginScrollView(
                    ToRect(rootLayout.ContentViewport),
                    new Vector2(result.Scroll.X, result.Scroll.Y),
                    contentRect,
                    false,
                    true);
                try
                {
                    result.Scroll = new RenderPoint(scrollPosition.x, scrollPosition.y);
                    DrawSections(contentLayout, ref result);
                }
                finally
                {
                    GUI.EndScrollView();
                }
            }
            finally
            {
                GUI.EndGroup();
            }

            return result;
        }

        private void EnsureStyles(ImguiPanelTheme theme)
        {
            var effectiveTheme = theme ?? new ImguiPanelTheme();
            var themeKey = ImguiThemeUtility.BuildKey(
                effectiveTheme.BackgroundColor,
                effectiveTheme.HeaderColor,
                effectiveTheme.BorderColor,
                effectiveTheme.DividerColor,
                effectiveTheme.ActionFillColor,
                effectiveTheme.ActionActiveFillColor,
                effectiveTheme.CardFillColor,
                effectiveTheme.TextColor,
                effectiveTheme.MutedTextColor,
                effectiveTheme.AccentColor,
                effectiveTheme.WarningColor);
            if (string.Equals(_themeResources.ThemeKey, themeKey, StringComparison.Ordinal))
            {
                return;
            }

            _themeResources.ThemeKey = themeKey;
            var textureCache = _moduleResources.TextureCache;
            _themeResources.BackgroundFill = textureCache.GetFill(effectiveTheme.BackgroundColor);
            _themeResources.HeaderFill = textureCache.GetFill(effectiveTheme.HeaderColor);
            _themeResources.BorderFill = textureCache.GetFill(effectiveTheme.BorderColor);
            _themeResources.DividerFill = textureCache.GetFill(effectiveTheme.DividerColor);
            _themeResources.CardFill = textureCache.GetBordered(4, 4, effectiveTheme.CardFillColor, effectiveTheme.BorderColor);
            _themeResources.ActionFill = textureCache.GetBordered(4, 4, effectiveTheme.ActionFillColor, effectiveTheme.BorderColor);
            _themeResources.ActionActiveFill = textureCache.GetBordered(4, 4, effectiveTheme.ActionActiveFillColor, effectiveTheme.AccentColor);

            _themeResources.TitleStyle = new GUIStyle(GUI.skin.label);
            _themeResources.TitleStyle.fontStyle = FontStyle.Bold;
            _themeResources.TitleStyle.fontSize = Mathf.Max(15, _themeResources.TitleStyle.fontSize + 1);
            _themeResources.TitleStyle.wordWrap = false;
            _themeResources.TitleStyle.clipping = TextClipping.Clip;
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.TitleStyle, effectiveTheme.TextColor);

            _themeResources.SubtitleStyle = new GUIStyle(GUI.skin.label);
            _themeResources.SubtitleStyle.wordWrap = false;
            _themeResources.SubtitleStyle.clipping = TextClipping.Clip;
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.SubtitleStyle, effectiveTheme.MutedTextColor);

            _themeResources.SectionButtonStyle = CreateButtonStyle(effectiveTheme.TextColor, _themeResources.ActionFill, _themeResources.ActionActiveFill, TextAnchor.MiddleLeft, true);
            _themeResources.SectionButtonStyle.padding = new RectOffset(10, 10, 4, 4);

            _themeResources.ActionButtonStyle = CreateButtonStyle(effectiveTheme.TextColor, _themeResources.ActionFill, _themeResources.ActionActiveFill, TextAnchor.MiddleCenter, true);
            _themeResources.ActionButtonStyle.padding = new RectOffset(8, 8, 4, 4);

            _themeResources.EmphasizedActionButtonStyle = CreateButtonStyle(effectiveTheme.TextColor, _themeResources.ActionActiveFill, _themeResources.ActionActiveFill, TextAnchor.MiddleCenter, true);
            _themeResources.EmphasizedActionButtonStyle.padding = new RectOffset(8, 8, 4, 4);

            _themeResources.CloseButtonStyle = new GUIStyle(_themeResources.ActionButtonStyle);
            _themeResources.CloseButtonStyle.fontSize = Mathf.Max(14, _themeResources.CloseButtonStyle.fontSize + 1);
            _themeResources.CloseButtonStyle.padding = new RectOffset(0, 0, 0, 2);

            _themeResources.KeyStyle = new GUIStyle(GUI.skin.label);
            _themeResources.KeyStyle.fontStyle = FontStyle.Bold;
            _themeResources.KeyStyle.wordWrap = false;
            _themeResources.KeyStyle.clipping = TextClipping.Clip;
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.KeyStyle, effectiveTheme.MutedTextColor);

            _themeResources.ValueStyle = new GUIStyle(GUI.skin.label);
            _themeResources.ValueStyle.wordWrap = true;
            _themeResources.ValueStyle.clipping = TextClipping.Clip;
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.ValueStyle, effectiveTheme.TextColor);

            _themeResources.DetailStyle = new GUIStyle(GUI.skin.label);
            _themeResources.DetailStyle.wordWrap = true;
            _themeResources.DetailStyle.clipping = TextClipping.Clip;
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.DetailStyle, effectiveTheme.TextColor);

            _themeResources.MonoStyle = new GUIStyle(GUI.skin.label);
            _themeResources.MonoStyle.wordWrap = true;
            _themeResources.MonoStyle.clipping = TextClipping.Clip;
            _themeResources.MonoStyle.font = GUI.skin != null ? GUI.skin.font : null;
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.MonoStyle, effectiveTheme.TextColor);

            _themeResources.SectionLabelStyle = new GUIStyle(GUI.skin.label);
            _themeResources.SectionLabelStyle.fontStyle = FontStyle.Bold;
            _themeResources.SectionLabelStyle.wordWrap = false;
            _themeResources.SectionLabelStyle.clipping = TextClipping.Clip;
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.SectionLabelStyle, effectiveTheme.WarningColor);

            _themeResources.CardStyle = new GUIStyle(GUI.skin.box);
            _themeResources.CardStyle.padding = new RectOffset((int)CardPadding, (int)CardPadding, (int)CardPadding, (int)CardPadding);
            _themeResources.CardStyle.margin = new RectOffset(0, 0, 0, 0);
            _themeResources.CardStyle.border = new RectOffset(1, 1, 1, 1);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_themeResources.CardStyle, _themeResources.CardFill);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.CardStyle, effectiveTheme.TextColor);
        }

        private GUIStyle CreateButtonStyle(Color textColor, Texture2D normalTexture, Texture2D activeTexture, TextAnchor alignment, bool bold)
        {
            var style = new GUIStyle(GUI.skin.button);
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.alignment = alignment;
            ImguiStyleUtil.ApplyBackgroundToAllStates(style, normalTexture);
            style.hover.background = activeTexture;
            style.active.background = activeTexture;
            style.focused.background = activeTexture;
            style.onNormal.background = activeTexture;
            style.onHover.background = activeTexture;
            style.onActive.background = activeTexture;
            style.onFocused.background = activeTexture;
            ImguiStyleUtil.ApplyTextColorToAllStates(style, textColor);
            return style;
        }

        private void DrawChrome(PanelRootLayout rootLayout, bool hasHeaderActions)
        {
            GUI.DrawTexture(ToRect(rootLayout.PanelRect), _themeResources.BackgroundFill);
            GUI.DrawTexture(ToRect(rootLayout.HeaderRect), _themeResources.HeaderFill);
            GUI.DrawTexture(new Rect(0f, rootLayout.HeaderRect.Y + rootLayout.HeaderRect.Height, rootLayout.PanelRect.Width, DividerHeight), _themeResources.DividerFill);
            if (hasHeaderActions)
            {
                GUI.DrawTexture(
                    new Rect(
                        PanelLayoutPlanner.Padding,
                        rootLayout.HeaderActionsRect.Y + rootLayout.HeaderActionsRect.Height + 8f,
                        rootLayout.PanelRect.Width - (PanelLayoutPlanner.Padding * 2f),
                        DividerHeight),
                    _themeResources.DividerFill);
            }

            ImguiStyleUtil.DrawBorder(ToRect(rootLayout.PanelRect), _themeResources.BorderFill, 1f);
        }

        private void DrawHeader(PanelDocument document, PanelRootLayout rootLayout, ref PanelRenderResult result)
        {
            GUI.Label(ToRect(rootLayout.TitleRect), document.Title ?? string.Empty, _themeResources.TitleStyle);
            GUI.Label(ToRect(rootLayout.SubtitleRect), document.Subtitle ?? string.Empty, _themeResources.SubtitleStyle);
            if (!document.ShowCloseButton)
            {
                return;
            }

            if (GUI.Button(ToRect(rootLayout.CloseButtonRect), "X", _themeResources.CloseButtonStyle))
            {
                result.ActivatedId = PanelCommandIds.Close;
            }
        }

        private void DrawHeaderActions(PanelAction[] actions, PanelRootLayout rootLayout, ref PanelRenderResult result)
        {
            if (actions == null || actions.Length == 0 || rootLayout == null || rootLayout.HeaderActionsRect.Height <= 0f)
            {
                return;
            }

            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null || i >= rootLayout.HeaderActionButtonRects.Count)
                {
                    continue;
                }

                DrawActionButton(ToRect(rootLayout.HeaderActionButtonRects[i]), action, ref result);
            }
        }

        private void DrawSections(PanelContentLayout layout, ref PanelRenderResult result)
        {
            if (layout == null || layout.Sections == null)
            {
                return;
            }

            for (var i = 0; i < layout.Sections.Count; i++)
            {
                var sectionLayout = layout.Sections[i];
                if (sectionLayout == null || sectionLayout.Section == null)
                {
                    continue;
                }

                DrawSectionHeader(sectionLayout, ref result);
                if (!sectionLayout.Section.Expanded)
                {
                    continue;
                }

                for (var elementIndex = 0; elementIndex < sectionLayout.ElementLayouts.Count; elementIndex++)
                {
                    DrawElement(sectionLayout.ElementLayouts[elementIndex], ref result);
                }
            }
        }

        private void DrawSectionHeader(PanelSectionLayout layout, ref PanelRenderResult result)
        {
            var section = layout.Section;
            var title = (section.Expanded ? "[-] " : "[+] ") + (section.Title ?? string.Empty);
            if (GUI.Button(ToRect(layout.HeaderRect), title, _themeResources.SectionButtonStyle))
            {
                result.ActivatedId = PanelCommandIds.CreateSectionToggle(section.Id);
            }
        }

        private void DrawElement(PanelElementLayout layout, ref PanelRenderResult result)
        {
            if (layout == null || layout.Element == null)
            {
                return;
            }

            switch (layout.Element.Kind)
            {
                case PanelElementKind.Metadata:
                    DrawMetadataElement(layout.MetadataLayout, layout.Element as PanelMetadataElement);
                    break;
                case PanelElementKind.Text:
                    DrawTextElement(layout.TextLayout, layout.Element as PanelTextElement);
                    break;
                case PanelElementKind.Action:
                    DrawActionElement(ToRect(layout.Bounds), layout.ActionLayout, layout.Element as PanelActionElement, ref result);
                    break;
                case PanelElementKind.Card:
                    DrawCardElement(ToRect(layout.Bounds), layout.CardLayout, layout.Element as PanelCardElement, ref result);
                    break;
            }
        }

        private void DrawMetadataElement(PanelMetadataContentLayout layout, PanelMetadataElement element)
        {
            if (element == null || layout == null)
            {
                return;
            }

            GUI.Label(ToRect(layout.KeyRect), (element.Label ?? string.Empty) + ":", _themeResources.KeyStyle);
            GUI.Label(ToRect(layout.ValueRect), !string.IsNullOrEmpty(element.Value) ? element.Value : "Unknown", _themeResources.ValueStyle);
            if (element.DrawDivider)
            {
                GUI.DrawTexture(ToRect(layout.DividerRect), _themeResources.DividerFill);
            }
        }

        private void DrawTextElement(PanelTextContentLayout layout, PanelTextElement element)
        {
            if (element == null || layout == null)
            {
                return;
            }

            if (layout.HasLabel)
            {
                GUI.Label(ToRect(layout.LabelRect), element.Label, _themeResources.SectionLabelStyle);
            }

            var style = element.Monospace ? _themeResources.MonoStyle : _themeResources.DetailStyle;
            GUI.Label(ToRect(layout.ValueRect), element.Value ?? string.Empty, style);
        }

        private void DrawActionElement(Rect rect, PanelActionElementContentLayout layout, PanelActionElement element, ref PanelRenderResult result)
        {
            if (element == null || element.Action == null || layout == null)
            {
                return;
            }

            GUI.Box(rect, GUIContent.none, _themeResources.CardStyle);
            DrawActionButton(ToRect(layout.ButtonRect), element.Action, ref result);
            if (layout.HasHint)
            {
                GUI.Label(ToRect(layout.HintRect), element.Hint, _themeResources.DetailStyle);
            }
        }

        private void DrawCardElement(Rect rect, PanelCardContentLayout content, PanelCardElement element, ref PanelRenderResult result)
        {
            if (element == null || content == null)
            {
                return;
            }

            GUI.Box(rect, GUIContent.none, _themeResources.CardStyle);
            if (!string.IsNullOrEmpty(element.Title))
            {
                GUI.Label(ToRect(content.TitleRect), element.Title, _themeResources.SectionLabelStyle);
            }

            for (var i = 0; i < content.RowLayouts.Count; i++)
            {
                DrawMetadataElement(content.RowLayouts[i], content.Rows[i]);
            }

            if (content.HasBody)
            {
                GUI.Label(ToRect(content.BodyRect), element.Body ?? string.Empty, _themeResources.DetailStyle);
            }

            for (var i = 0; i < content.ActionRects.Count; i++)
            {
                DrawActionButton(ToRect(content.ActionRects[i]), content.Actions[i], ref result);
            }
        }

        private void DrawActionButton(Rect rect, PanelAction action, ref PanelRenderResult result)
        {
            if (action == null)
            {
                return;
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = action.Enabled;
            var style = action.Emphasized ? _themeResources.EmphasizedActionButtonStyle : _themeResources.ActionButtonStyle;
            if (GUI.Button(rect, action.Label ?? string.Empty, style) && action.Enabled)
            {
                result.ActivatedId = action.Id ?? string.Empty;
            }

            GUI.enabled = previousEnabled;
        }

        private float MeasureMetadataHeight(float width, PanelMetadataElement element)
        {
            if (element == null)
            {
                return 18f;
            }

            var valueWidth = Mathf.Max(40f, width - LabelWidth);
            var keyHeight = Mathf.Max(18f, _themeResources.KeyStyle.CalcHeight(new GUIContent(element.Label ?? string.Empty), LabelWidth));
            var valueHeight = Mathf.Max(18f, _themeResources.ValueStyle.CalcHeight(new GUIContent(element.Value ?? string.Empty), valueWidth));
            return Mathf.Max(keyHeight, valueHeight);
        }

        private float MeasureTextElementHeight(float width, PanelTextElement element)
        {
            if (element == null)
            {
                return 18f;
            }

            var height = 0f;
            if (!string.IsNullOrEmpty(element.Label))
            {
                height += 20f;
            }

            var style = element.Monospace ? _themeResources.MonoStyle : _themeResources.DetailStyle;
            height += Mathf.Max(18f, style.CalcHeight(new GUIContent(element.Value ?? string.Empty), width)) + 6f;
            return height;
        }

        private float MeasureActionElementHeight(float width, PanelActionElement element)
        {
            if (element == null)
            {
                return 34f;
            }

            var height = CardPadding + CardActionButtonHeight + CardPadding;
            if (!string.IsNullOrEmpty(element.Hint))
            {
                height += Mathf.Max(18f, _themeResources.DetailStyle.CalcHeight(new GUIContent(element.Hint), Mathf.Max(40f, width - (CardPadding * 2f)))) + CardActionButtonSpacing;
            }

            return height;
        }

        private float MeasureCardHeight(float width, PanelCardElement element)
        {
            if (element == null)
            {
                return 34f;
            }

            var innerWidth = Mathf.Max(40f, width - (CardPadding * 2f));
            var height = CardPadding;
            if (!string.IsNullOrEmpty(element.Title))
            {
                height += 20f;
            }

            var rows = element.Rows ?? new PanelMetadataElement[0];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }

                height += MeasureMetadataHeight(innerWidth, row) + 3f + (row.DrawDivider ? DividerHeight + 2f : 0f);
            }

            if (!string.IsNullOrEmpty(element.Body))
            {
                height += Mathf.Max(18f, _themeResources.DetailStyle.CalcHeight(new GUIContent(element.Body), innerWidth)) + CardActionButtonSpacing;
            }

            var actions = element.Actions ?? new PanelAction[0];
            for (var i = 0; i < actions.Length; i++)
            {
                if (actions[i] != null)
                {
                    height += CardActionButtonHeight + CardActionButtonSpacing;
                }
            }

            height += CardPadding;
            return height;
        }

        private sealed class PanelThemeResources
        {
            public string ThemeKey = string.Empty;
            public Texture2D BackgroundFill;
            public Texture2D HeaderFill;
            public Texture2D BorderFill;
            public Texture2D DividerFill;
            public Texture2D CardFill;
            public Texture2D ActionFill;
            public Texture2D ActionActiveFill;
            public GUIStyle TitleStyle;
            public GUIStyle SubtitleStyle;
            public GUIStyle SectionButtonStyle;
            public GUIStyle ActionButtonStyle;
            public GUIStyle EmphasizedActionButtonStyle;
            public GUIStyle CloseButtonStyle;
            public GUIStyle KeyStyle;
            public GUIStyle ValueStyle;
            public GUIStyle DetailStyle;
            public GUIStyle MonoStyle;
            public GUIStyle SectionLabelStyle;
            public GUIStyle CardStyle;
        }

        private static Rect ToRect(RenderRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private sealed class ImguiPanelLayoutMeasurer : IPanelLayoutMeasurer
        {
            private readonly ImguiPanelRenderer _owner;

            public ImguiPanelLayoutMeasurer(ImguiPanelRenderer owner)
            {
                _owner = owner;
            }

            public float MeasureMetadataHeight(float width, PanelMetadataElement element)
            {
                return _owner.MeasureMetadataHeight(width, element);
            }

            public float MeasureTextHeight(float width, PanelTextElement element)
            {
                return _owner.MeasureTextElementHeight(width, element);
            }

            public float MeasureActionHeight(float width, PanelActionElement element)
            {
                return _owner.MeasureActionElementHeight(width, element);
            }

            public float MeasureCardHeight(float width, PanelCardElement element)
            {
                return _owner.MeasureCardHeight(width, element);
            }
        }
    }
}
