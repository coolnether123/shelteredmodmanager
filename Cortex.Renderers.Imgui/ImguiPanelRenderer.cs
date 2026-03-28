using System;
using System.Collections.Generic;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiPanelRenderer : IPanelRenderer, IDisposable
    {
        private const float HeaderHeight = 54f;
        private const float ActionStripHeight = 30f;
        private const float Padding = 10f;
        private const float SectionSpacing = 6f;
        private const float ExpandedSectionTopSpacing = 4f;
        private const float CardPadding = 8f;
        private const float DividerHeight = 1f;
        private const float LabelWidth = 96f;
        private const float SectionHeaderHeight = 26f;
        private const float HeaderActionButtonHeight = 24f;
        private const float HeaderActionButtonSpacing = 6f;
        private const float CardActionButtonHeight = 24f;
        private const float CardActionButtonSpacing = 4f;
        private const float MinContentWidth = 120f;
        private const float MinContentHeight = 40f;

        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;
        private readonly PanelThemeResources _themeResources = new PanelThemeResources();

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
            var rootLayout = BuildRootLayout(unityRect, document.HeaderActions);
            var contentWidth = Mathf.Max(MinContentWidth, rootLayout.ContentViewport.width - 16f);
            var contentLayout = BuildContentLayout(document, contentWidth);
            var contentRect = new Rect(0f, 0f, contentWidth, contentLayout.TotalHeight);

            GUI.BeginGroup(unityRect);
            try
            {
                DrawChrome(rootLayout, document.HeaderActions != null && document.HeaderActions.Length > 0);
                DrawHeader(document, rootLayout.HeaderRect, ref result);
                DrawHeaderActions(document.HeaderActions, rootLayout.HeaderActionsRect, ref result);

                var scrollPosition = GUI.BeginScrollView(
                    rootLayout.ContentViewport,
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

        private static RootLayout BuildRootLayout(Rect panelRect, PanelAction[] headerActions)
        {
            var hasHeaderActions = headerActions != null && headerActions.Length > 0;
            var headerRect = new Rect(0f, 0f, panelRect.width, HeaderHeight);
            var actionsRect = hasHeaderActions
                ? new Rect(Padding, HeaderHeight + 6f, panelRect.width - (Padding * 2f), ActionStripHeight)
                : new Rect(Padding, HeaderHeight + 6f, panelRect.width - (Padding * 2f), 0f);
            var contentTop = hasHeaderActions ? actionsRect.yMax + 8f : headerRect.yMax + 10f;
            var layout = new RootLayout();
            layout.PanelRect = new Rect(0f, 0f, panelRect.width, panelRect.height);
            layout.HeaderRect = headerRect;
            layout.HeaderActionsRect = actionsRect;
            layout.ContentViewport = new Rect(
                Padding,
                contentTop,
                panelRect.width - (Padding * 2f),
                Mathf.Max(24f, panelRect.height - contentTop - Padding));
            return layout;
        }

        private void DrawChrome(RootLayout rootLayout, bool hasHeaderActions)
        {
            GUI.DrawTexture(rootLayout.PanelRect, _themeResources.BackgroundFill);
            GUI.DrawTexture(rootLayout.HeaderRect, _themeResources.HeaderFill);
            GUI.DrawTexture(new Rect(0f, rootLayout.HeaderRect.yMax, rootLayout.PanelRect.width, DividerHeight), _themeResources.DividerFill);
            if (hasHeaderActions)
            {
                GUI.DrawTexture(new Rect(Padding, rootLayout.HeaderActionsRect.yMax + 8f, rootLayout.PanelRect.width - (Padding * 2f), DividerHeight), _themeResources.DividerFill);
            }

            ImguiStyleUtil.DrawBorder(rootLayout.PanelRect, _themeResources.BorderFill, 1f);
        }

        private void DrawHeader(PanelDocument document, Rect headerRect, ref PanelRenderResult result)
        {
            var titleRect = new Rect(Padding, 8f, headerRect.width - 62f, 24f);
            var subtitleRect = new Rect(Padding, 31f, headerRect.width - 62f, 18f);
            GUI.Label(titleRect, document.Title ?? string.Empty, _themeResources.TitleStyle);
            GUI.Label(subtitleRect, document.Subtitle ?? string.Empty, _themeResources.SubtitleStyle);
            if (!document.ShowCloseButton)
            {
                return;
            }

            var closeRect = new Rect(headerRect.width - 42f, 12f, 28f, 26f);
            if (GUI.Button(closeRect, "X", _themeResources.CloseButtonStyle))
            {
                result.ActivatedId = PanelCommandIds.Close;
            }
        }

        private void DrawHeaderActions(PanelAction[] actions, Rect rect, ref PanelRenderResult result)
        {
            if (actions == null || actions.Length == 0 || rect.height <= 0f)
            {
                return;
            }

            var buttonWidth = Mathf.Max(92f, Mathf.Min(132f, (rect.width - 18f) / Math.Max(1, actions.Length)));
            var x = rect.x;
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                var buttonRect = new Rect(x, rect.y, buttonWidth, HeaderActionButtonHeight);
                DrawActionButton(buttonRect, action, ref result);
                x += buttonWidth + HeaderActionButtonSpacing;
            }
        }

        private PanelContentLayout BuildContentLayout(PanelDocument document, float width)
        {
            var layout = new PanelContentLayout();
            var y = 0f;
            var sections = document.Sections ?? new PanelSection[0];
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var sectionLayout = new PanelSectionLayout();
                sectionLayout.Section = section;
                sectionLayout.HeaderRect = new Rect(0f, y, width, SectionHeaderHeight);
                layout.Sections.Add(sectionLayout);

                y += SectionHeaderHeight;
                if (!section.Expanded)
                {
                    y += SectionSpacing;
                    continue;
                }

                y += ExpandedSectionTopSpacing;
                var elements = section.Elements ?? new PanelElement[0];
                for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
                {
                    var element = elements[elementIndex];
                    if (element == null)
                    {
                        continue;
                    }

                    var elementLayout = BuildElementLayout(width, y, element);
                    y = elementLayout.NextY;
                    sectionLayout.ElementLayouts.Add(elementLayout);
                }

                y += SectionSpacing;
            }

            layout.TotalHeight = Mathf.Max(MinContentHeight, y);
            return layout;
        }

        private PanelElementLayout BuildElementLayout(float width, float startY, PanelElement element)
        {
            var rect = new Rect(0f, startY, width, 0f);
            switch (element.Kind)
            {
                case PanelElementKind.Metadata:
                    var metadata = element as PanelMetadataElement;
                    rect.height = MeasureMetadataHeight(width, metadata);
                    return new PanelElementLayout(element, rect, startY + rect.height + 3f + (metadata != null && metadata.DrawDivider ? DividerHeight + 2f : 0f));
                case PanelElementKind.Text:
                    rect.height = MeasureTextElementHeight(width, element as PanelTextElement);
                    return new PanelElementLayout(element, rect, startY + rect.height);
                case PanelElementKind.Action:
                    rect.height = MeasureActionElementHeight(width, element as PanelActionElement);
                    return new PanelElementLayout(element, rect, startY + rect.height + 4f);
                case PanelElementKind.Card:
                    rect.height = MeasureCardHeight(width, element as PanelCardElement);
                    return new PanelElementLayout(element, rect, startY + rect.height + 4f);
                case PanelElementKind.Spacer:
                    var spacer = element as PanelSpacerElement;
                    rect.height = spacer != null ? Mathf.Max(0f, spacer.Height) : 0f;
                    return new PanelElementLayout(element, rect, startY + rect.height);
                default:
                    return new PanelElementLayout(element, rect, startY);
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
            if (GUI.Button(layout.HeaderRect, title, _themeResources.SectionButtonStyle))
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
                    DrawMetadataElement(layout.Bounds, layout.Element as PanelMetadataElement);
                    break;
                case PanelElementKind.Text:
                    DrawTextElement(layout.Bounds, layout.Element as PanelTextElement);
                    break;
                case PanelElementKind.Action:
                    DrawActionElement(layout.Bounds, layout.Element as PanelActionElement, ref result);
                    break;
                case PanelElementKind.Card:
                    DrawCardElement(layout.Bounds, layout.Element as PanelCardElement, ref result);
                    break;
            }
        }

        private void DrawMetadataElement(Rect rect, PanelMetadataElement element)
        {
            if (element == null)
            {
                return;
            }

            var rowHeight = MeasureMetadataHeight(rect.width, element);
            var keyRect = new Rect(rect.x, rect.y, LabelWidth, rowHeight);
            var valueRect = new Rect(rect.x + LabelWidth, rect.y, Mathf.Max(40f, rect.width - LabelWidth), rowHeight);
            GUI.Label(keyRect, (element.Label ?? string.Empty) + ":", _themeResources.KeyStyle);
            GUI.Label(valueRect, !string.IsNullOrEmpty(element.Value) ? element.Value : "Unknown", _themeResources.ValueStyle);
            if (element.DrawDivider)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y + rowHeight + 3f, rect.width, DividerHeight), _themeResources.DividerFill);
            }
        }

        private void DrawTextElement(Rect rect, PanelTextElement element)
        {
            if (element == null)
            {
                return;
            }

            var y = rect.y;
            if (!string.IsNullOrEmpty(element.Label))
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 18f), element.Label, _themeResources.SectionLabelStyle);
                y += 20f;
            }

            var style = element.Monospace ? _themeResources.MonoStyle : _themeResources.DetailStyle;
            var textHeight = Mathf.Max(18f, style.CalcHeight(new GUIContent(element.Value ?? string.Empty), rect.width));
            GUI.Label(new Rect(rect.x, y, rect.width, textHeight), element.Value ?? string.Empty, style);
        }

        private void DrawActionElement(Rect rect, PanelActionElement element, ref PanelRenderResult result)
        {
            if (element == null || element.Action == null)
            {
                return;
            }

            GUI.Box(rect, GUIContent.none, _themeResources.CardStyle);
            var buttonRect = new Rect(rect.x + CardPadding, rect.y + CardPadding, rect.width - (CardPadding * 2f), CardActionButtonHeight);
            DrawActionButton(buttonRect, element.Action, ref result);
            if (!string.IsNullOrEmpty(element.Hint))
            {
                var hintRect = new Rect(buttonRect.x, buttonRect.yMax + CardActionButtonSpacing, buttonRect.width, Mathf.Max(18f, _themeResources.DetailStyle.CalcHeight(new GUIContent(element.Hint), buttonRect.width)));
                GUI.Label(hintRect, element.Hint, _themeResources.DetailStyle);
            }
        }

        private void DrawCardElement(Rect rect, PanelCardElement element, ref PanelRenderResult result)
        {
            if (element == null)
            {
                return;
            }

            GUI.Box(rect, GUIContent.none, _themeResources.CardStyle);
            var content = BuildCardContentLayout(rect, element);
            if (!string.IsNullOrEmpty(element.Title))
            {
                GUI.Label(content.TitleRect, element.Title, _themeResources.SectionLabelStyle);
            }

            for (var i = 0; i < content.RowRects.Count; i++)
            {
                DrawMetadataElement(content.RowRects[i], content.Rows[i]);
            }

            if (content.HasBody)
            {
                GUI.Label(content.BodyRect, element.Body ?? string.Empty, _themeResources.DetailStyle);
            }

            for (var i = 0; i < content.ActionRects.Count; i++)
            {
                DrawActionButton(content.ActionRects[i], content.Actions[i], ref result);
            }
        }

        private CardContentLayout BuildCardContentLayout(Rect cardRect, PanelCardElement element)
        {
            var layout = new CardContentLayout();
            var innerWidth = Mathf.Max(40f, cardRect.width - (CardPadding * 2f));
            var x = cardRect.x + CardPadding;
            var y = cardRect.y + CardPadding;

            if (!string.IsNullOrEmpty(element.Title))
            {
                layout.TitleRect = new Rect(x, y, innerWidth, 18f);
                y += 20f;
            }

            var rows = element.Rows ?? new PanelMetadataElement[0];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }

                var rowHeight = MeasureMetadataHeight(innerWidth, row);
                layout.Rows.Add(row);
                layout.RowRects.Add(new Rect(x, y, innerWidth, rowHeight));
                y += rowHeight + 3f + (row.DrawDivider ? DividerHeight + 2f : 0f);
            }

            if (!string.IsNullOrEmpty(element.Body))
            {
                var bodyHeight = Mathf.Max(18f, _themeResources.DetailStyle.CalcHeight(new GUIContent(element.Body), innerWidth));
                layout.HasBody = true;
                layout.BodyRect = new Rect(x, y, innerWidth, bodyHeight);
                y += bodyHeight + CardActionButtonSpacing;
            }

            var actions = element.Actions ?? new PanelAction[0];
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                layout.Actions.Add(action);
                layout.ActionRects.Add(new Rect(x, y, Mathf.Min(180f, innerWidth), CardActionButtonHeight));
                y += CardActionButtonHeight + CardActionButtonSpacing;
            }

            return layout;
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

        private sealed class RootLayout
        {
            public Rect PanelRect;
            public Rect HeaderRect;
            public Rect HeaderActionsRect;
            public Rect ContentViewport;
        }

        private sealed class PanelContentLayout
        {
            public List<PanelSectionLayout> Sections = new List<PanelSectionLayout>();
            public float TotalHeight;
        }

        private sealed class PanelSectionLayout
        {
            public PanelSection Section;
            public Rect HeaderRect;
            public List<PanelElementLayout> ElementLayouts = new List<PanelElementLayout>();
        }

        private sealed class PanelElementLayout
        {
            public readonly PanelElement Element;
            public readonly Rect Bounds;
            public readonly float NextY;

            public PanelElementLayout(PanelElement element, Rect bounds, float nextY)
            {
                Element = element;
                Bounds = bounds;
                NextY = nextY;
            }
        }

        private sealed class CardContentLayout
        {
            public Rect TitleRect;
            public bool HasBody;
            public Rect BodyRect;
            public List<PanelMetadataElement> Rows = new List<PanelMetadataElement>();
            public List<Rect> RowRects = new List<Rect>();
            public List<PanelAction> Actions = new List<PanelAction>();
            public List<Rect> ActionRects = new List<Rect>();
        }
    }
}
