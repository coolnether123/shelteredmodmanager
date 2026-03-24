using System;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiPanelRenderer
    {
        private const float HeaderHeight = 54f;
        private const float ActionStripHeight = 30f;
        private const float Padding = 10f;
        private const float SectionSpacing = 6f;
        private const float RowSpacing = 4f;
        private const float CardPadding = 8f;
        private const float DividerHeight = 1f;
        private const float LabelWidth = 96f;

        private string _themeKey = string.Empty;
        private Texture2D _backgroundFill;
        private Texture2D _headerFill;
        private Texture2D _borderFill;
        private Texture2D _dividerFill;
        private Texture2D _cardFill;
        private Texture2D _actionFill;
        private Texture2D _actionActiveFill;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _sectionButtonStyle;
        private GUIStyle _actionButtonStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _detailStyle;
        private GUIStyle _monoStyle;
        private GUIStyle _sectionLabelStyle;
        private GUIStyle _cardStyle;

        public PanelRenderResult Draw(Rect rect, PanelDocument document, Vector2 scroll, ImguiPanelTheme theme)
        {
            EnsureStyles(theme);
            var result = new PanelRenderResult();
            result.Scroll = scroll;
            if (document == null)
            {
                return result;
            }

            var hasHeaderActions = document.HeaderActions != null && document.HeaderActions.Length > 0;
            var headerRect = new Rect(0f, 0f, rect.width, HeaderHeight);
            var actionsRect = hasHeaderActions
                ? new Rect(Padding, HeaderHeight + 6f, rect.width - (Padding * 2f), ActionStripHeight)
                : new Rect(Padding, HeaderHeight + 6f, rect.width - (Padding * 2f), 0f);
            var contentTop = hasHeaderActions ? actionsRect.yMax + 8f : headerRect.yMax + 10f;
            var contentViewport = new Rect(
                Padding,
                contentTop,
                rect.width - (Padding * 2f),
                Mathf.Max(24f, rect.height - contentTop - Padding));

            GUI.BeginGroup(rect);
            DrawChrome(new Rect(0f, 0f, rect.width, rect.height), headerRect, actionsRect, hasHeaderActions);
            DrawHeader(document, headerRect, ref result);
            if (hasHeaderActions)
            {
                DrawHeaderActions(document, actionsRect, ref result);
            }

            var contentWidth = Mathf.Max(120f, contentViewport.width - 16f);
            var contentHeight = MeasureDocument(document, contentWidth);
            var contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            result.Scroll = GUI.BeginScrollView(contentViewport, result.Scroll, contentRect, false, true);
            DrawSections(document, contentRect.width, ref result);
            GUI.EndScrollView();
            GUI.EndGroup();
            return result;
        }

        private void DrawChrome(Rect panelRect, Rect headerRect, Rect actionsRect, bool hasHeaderActions)
        {
            GUI.DrawTexture(panelRect, _backgroundFill);
            GUI.DrawTexture(headerRect, _headerFill);
            GUI.DrawTexture(new Rect(0f, headerRect.yMax, panelRect.width, DividerHeight), _dividerFill);
            if (hasHeaderActions)
            {
                GUI.DrawTexture(new Rect(Padding, actionsRect.yMax + 8f, panelRect.width - (Padding * 2f), DividerHeight), _dividerFill);
            }
            DrawBorder(panelRect);
        }

        private void DrawHeader(PanelDocument document, Rect rect, ref PanelRenderResult result)
        {
            var titleRect = new Rect(Padding, 8f, rect.width - 62f, 24f);
            var subtitleRect = new Rect(Padding, 31f, rect.width - 62f, 18f);
            GUI.Label(titleRect, document.Title ?? string.Empty, _titleStyle);
            GUI.Label(subtitleRect, document.Subtitle ?? string.Empty, _subtitleStyle);
            if (document.ShowCloseButton)
            {
                var closeRect = new Rect(rect.width - 42f, 12f, 28f, 26f);
                if (GUI.Button(closeRect, "X", _closeButtonStyle))
                {
                    result.ActivatedId = "close";
                }
            }
        }

        private void DrawHeaderActions(PanelDocument document, Rect rect, ref PanelRenderResult result)
        {
            var actions = document.HeaderActions ?? new PanelAction[0];
            if (actions.Length == 0)
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

                var buttonRect = new Rect(x, rect.y, buttonWidth, 24f);
                var previousEnabled = GUI.enabled;
                GUI.enabled = action.Enabled;
                if (GUI.Button(buttonRect, action.Label ?? string.Empty, _actionButtonStyle) && action.Enabled)
                {
                    result.ActivatedId = action.Id ?? string.Empty;
                }
                GUI.enabled = previousEnabled;
                x += buttonWidth + 6f;
            }
        }

        private void DrawSections(PanelDocument document, float width, ref PanelRenderResult result)
        {
            var y = 0f;
            var sections = document.Sections ?? new PanelSection[0];
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var sectionButtonRect = new Rect(0f, y, width, 26f);
                if (GUI.Button(sectionButtonRect, (section.Expanded ? "[-] " : "[+] ") + (section.Title ?? string.Empty), _sectionButtonStyle))
                {
                    result.ActivatedId = "section:" + (section.Id ?? string.Empty);
                }

                y += 26f;
                if (!section.Expanded)
                {
                    y += SectionSpacing;
                    continue;
                }

                y += 4f;
                var elements = section.Elements ?? new PanelElement[0];
                for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
                {
                    var element = elements[elementIndex];
                    if (element == null)
                    {
                        continue;
                    }

                    switch (element.Kind)
                    {
                        case PanelElementKind.Metadata:
                            y = DrawMetadataElement(new Rect(0f, y, width, 0f), element as PanelMetadataElement);
                            break;
                        case PanelElementKind.Text:
                            y = DrawTextElement(new Rect(0f, y, width, 0f), element as PanelTextElement);
                            break;
                        case PanelElementKind.Action:
                            y = DrawActionElement(new Rect(0f, y, width, 0f), element as PanelActionElement, ref result);
                            break;
                        case PanelElementKind.Card:
                            y = DrawCardElement(new Rect(0f, y, width, 0f), element as PanelCardElement, ref result);
                            break;
                        case PanelElementKind.Spacer:
                            var spacer = element as PanelSpacerElement;
                            y += spacer != null ? Mathf.Max(0f, spacer.Height) : 0f;
                            break;
                    }
                }

                y += SectionSpacing;
            }
        }

        private float DrawMetadataElement(Rect rect, PanelMetadataElement element)
        {
            if (element == null)
            {
                return rect.y;
            }

            var rowHeight = MeasureMetadataHeight(rect.width, element);
            var keyRect = new Rect(rect.x, rect.y, LabelWidth, rowHeight);
            var valueRect = new Rect(rect.x + LabelWidth, rect.y, Mathf.Max(40f, rect.width - LabelWidth), rowHeight);
            GUI.Label(keyRect, (element.Label ?? string.Empty) + ":", _keyStyle);
            GUI.Label(valueRect, !string.IsNullOrEmpty(element.Value) ? element.Value : "Unknown", _valueStyle);
            var nextY = rect.y + rowHeight + 3f;
            if (element.DrawDivider)
            {
                GUI.DrawTexture(new Rect(rect.x, nextY, rect.width, DividerHeight), _dividerFill);
                nextY += DividerHeight + 2f;
            }

            return nextY;
        }

        private float DrawTextElement(Rect rect, PanelTextElement element)
        {
            if (element == null)
            {
                return rect.y;
            }

            var y = rect.y;
            if (!string.IsNullOrEmpty(element.Label))
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 18f), element.Label, _sectionLabelStyle);
                y += 20f;
            }

            var style = element.Monospace ? _monoStyle : _detailStyle;
            var textHeight = Mathf.Max(18f, style.CalcHeight(new GUIContent(element.Value ?? string.Empty), rect.width));
            GUI.Label(new Rect(rect.x, y, rect.width, textHeight), element.Value ?? string.Empty, style);
            return y + textHeight + 6f;
        }

        private float DrawActionElement(Rect rect, PanelActionElement element, ref PanelRenderResult result)
        {
            if (element == null || element.Action == null)
            {
                return rect.y;
            }

            var height = MeasureActionElementHeight(rect.width, element);
            var containerRect = new Rect(rect.x, rect.y, rect.width, height);
            GUI.Box(containerRect, GUIContent.none, _cardStyle);
            var buttonRect = new Rect(containerRect.x + CardPadding, containerRect.y + CardPadding, containerRect.width - (CardPadding * 2f), 24f);
            var previousEnabled = GUI.enabled;
            GUI.enabled = element.Action.Enabled;
            if (GUI.Button(buttonRect, element.Action.Label ?? string.Empty, _actionButtonStyle) && element.Action.Enabled)
            {
                result.ActivatedId = element.Action.Id ?? string.Empty;
            }
            GUI.enabled = previousEnabled;
            if (!string.IsNullOrEmpty(element.Hint))
            {
                var hintRect = new Rect(buttonRect.x, buttonRect.yMax + 4f, buttonRect.width, Mathf.Max(18f, _detailStyle.CalcHeight(new GUIContent(element.Hint), buttonRect.width)));
                GUI.Label(hintRect, element.Hint, _detailStyle);
            }

            return containerRect.yMax + 4f;
        }

        private float DrawCardElement(Rect rect, PanelCardElement element, ref PanelRenderResult result)
        {
            if (element == null)
            {
                return rect.y;
            }

            var height = MeasureCardHeight(rect.width, element);
            var cardRect = new Rect(rect.x, rect.y, rect.width, height);
            GUI.Box(cardRect, GUIContent.none, _cardStyle);
            var innerWidth = cardRect.width - (CardPadding * 2f);
            var y = cardRect.y + CardPadding;
            if (!string.IsNullOrEmpty(element.Title))
            {
                GUI.Label(new Rect(cardRect.x + CardPadding, y, innerWidth, 18f), element.Title, _sectionLabelStyle);
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

                var rowRect = new Rect(cardRect.x + CardPadding, y, innerWidth, 0f);
                y = DrawMetadataElement(rowRect, row);
            }

            if (!string.IsNullOrEmpty(element.Body))
            {
                var bodyHeight = Mathf.Max(18f, _detailStyle.CalcHeight(new GUIContent(element.Body), innerWidth));
                GUI.Label(new Rect(cardRect.x + CardPadding, y, innerWidth, bodyHeight), element.Body, _detailStyle);
                y += bodyHeight + 4f;
            }

            var actions = element.Actions ?? new PanelAction[0];
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                var buttonRect = new Rect(cardRect.x + CardPadding, y, Mathf.Min(180f, innerWidth), 24f);
                var previousEnabled = GUI.enabled;
                GUI.enabled = action.Enabled;
                if (GUI.Button(buttonRect, action.Label ?? string.Empty, _actionButtonStyle) && action.Enabled)
                {
                    result.ActivatedId = action.Id ?? string.Empty;
                }
                GUI.enabled = previousEnabled;
                y += 28f;
            }

            return cardRect.yMax + 4f;
        }

        private float MeasureDocument(PanelDocument document, float width)
        {
            var height = 0f;
            var sections = document.Sections ?? new PanelSection[0];
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                height += 26f;
                if (!section.Expanded)
                {
                    height += SectionSpacing;
                    continue;
                }

                height += 4f;
                var elements = section.Elements ?? new PanelElement[0];
                for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
                {
                    var element = elements[elementIndex];
                    if (element == null)
                    {
                        continue;
                    }

                    switch (element.Kind)
                    {
                        case PanelElementKind.Metadata:
                            height += MeasureMetadataHeight(width, element as PanelMetadataElement) + 6f;
                            break;
                        case PanelElementKind.Text:
                            height += MeasureTextElementHeight(width, element as PanelTextElement);
                            break;
                        case PanelElementKind.Action:
                            height += MeasureActionElementHeight(width, element as PanelActionElement) + 4f;
                            break;
                        case PanelElementKind.Card:
                            height += MeasureCardHeight(width, element as PanelCardElement) + 4f;
                            break;
                        case PanelElementKind.Spacer:
                            var spacer = element as PanelSpacerElement;
                            height += spacer != null ? Mathf.Max(0f, spacer.Height) : 0f;
                            break;
                    }
                }

                height += SectionSpacing;
            }

            return Mathf.Max(40f, height);
        }

        private float MeasureMetadataHeight(float width, PanelMetadataElement element)
        {
            if (element == null)
            {
                return 18f;
            }

            var valueWidth = Mathf.Max(40f, width - LabelWidth);
            var keyHeight = Mathf.Max(18f, _keyStyle.CalcHeight(new GUIContent(element.Label ?? string.Empty), LabelWidth));
            var valueHeight = Mathf.Max(18f, _valueStyle.CalcHeight(new GUIContent(element.Value ?? string.Empty), valueWidth));
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

            var style = element.Monospace ? _monoStyle : _detailStyle;
            height += Mathf.Max(18f, style.CalcHeight(new GUIContent(element.Value ?? string.Empty), width)) + 6f;
            return height;
        }

        private float MeasureActionElementHeight(float width, PanelActionElement element)
        {
            if (element == null)
            {
                return 34f;
            }

            var height = CardPadding + 24f + CardPadding;
            if (!string.IsNullOrEmpty(element.Hint))
            {
                height += Mathf.Max(18f, _detailStyle.CalcHeight(new GUIContent(element.Hint), Mathf.Max(40f, width - (CardPadding * 2f)))) + 4f;
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
                height += MeasureMetadataHeight(innerWidth, rows[i]) + 6f;
            }

            if (!string.IsNullOrEmpty(element.Body))
            {
                height += Mathf.Max(18f, _detailStyle.CalcHeight(new GUIContent(element.Body), innerWidth)) + 4f;
            }

            var actions = element.Actions ?? new PanelAction[0];
            for (var i = 0; i < actions.Length; i++)
            {
                height += 28f;
            }

            height += CardPadding;
            return height;
        }

        private void EnsureStyles(ImguiPanelTheme theme)
        {
            if (theme == null)
            {
                theme = new ImguiPanelTheme();
            }

            var key = BuildThemeKey(theme);
            if (string.Equals(_themeKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _themeKey = key;
            _backgroundFill = MakeFill(theme.BackgroundColor);
            _headerFill = MakeFill(theme.HeaderColor);
            _borderFill = MakeFill(theme.BorderColor);
            _dividerFill = MakeFill(theme.DividerColor);
            _cardFill = MakeBorderedTex(4, 4, theme.CardFillColor, theme.BorderColor);
            _actionFill = MakeBorderedTex(4, 4, theme.ActionFillColor, theme.BorderColor);
            _actionActiveFill = MakeBorderedTex(4, 4, theme.ActionActiveFillColor, theme.AccentColor);

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.fontSize = Mathf.Max(15, _titleStyle.fontSize + 1);
            _titleStyle.wordWrap = false;
            _titleStyle.clipping = TextClipping.Clip;
            ApplyTextColorToAllStates(_titleStyle, theme.TextColor);

            _subtitleStyle = new GUIStyle(GUI.skin.label);
            _subtitleStyle.wordWrap = false;
            _subtitleStyle.clipping = TextClipping.Clip;
            ApplyTextColorToAllStates(_subtitleStyle, theme.MutedTextColor);

            _sectionButtonStyle = new GUIStyle(GUI.skin.button);
            _sectionButtonStyle.alignment = TextAnchor.MiddleLeft;
            _sectionButtonStyle.fontStyle = FontStyle.Bold;
            _sectionButtonStyle.padding = new RectOffset(10, 10, 4, 4);
            ApplyBackgroundToAllStates(_sectionButtonStyle, _actionFill);
            _sectionButtonStyle.hover.background = _actionActiveFill;
            _sectionButtonStyle.active.background = _actionActiveFill;
            _sectionButtonStyle.focused.background = _actionActiveFill;
            _sectionButtonStyle.onNormal.background = _actionActiveFill;
            _sectionButtonStyle.onHover.background = _actionActiveFill;
            _sectionButtonStyle.onActive.background = _actionActiveFill;
            _sectionButtonStyle.onFocused.background = _actionActiveFill;
            ApplyTextColorToAllStates(_sectionButtonStyle, theme.TextColor);

            _actionButtonStyle = new GUIStyle(GUI.skin.button);
            _actionButtonStyle.fontStyle = FontStyle.Bold;
            _actionButtonStyle.alignment = TextAnchor.MiddleCenter;
            _actionButtonStyle.padding = new RectOffset(8, 8, 4, 4);
            ApplyBackgroundToAllStates(_actionButtonStyle, _actionFill);
            _actionButtonStyle.hover.background = _actionActiveFill;
            _actionButtonStyle.active.background = _actionActiveFill;
            _actionButtonStyle.focused.background = _actionActiveFill;
            _actionButtonStyle.onNormal.background = _actionActiveFill;
            _actionButtonStyle.onHover.background = _actionActiveFill;
            _actionButtonStyle.onActive.background = _actionActiveFill;
            _actionButtonStyle.onFocused.background = _actionActiveFill;
            ApplyTextColorToAllStates(_actionButtonStyle, theme.TextColor);

            _closeButtonStyle = new GUIStyle(_actionButtonStyle);
            _closeButtonStyle.fontSize = Mathf.Max(14, _closeButtonStyle.fontSize + 1);
            _closeButtonStyle.padding = new RectOffset(0, 0, 0, 2);

            _keyStyle = new GUIStyle(GUI.skin.label);
            _keyStyle.fontStyle = FontStyle.Bold;
            _keyStyle.wordWrap = false;
            _keyStyle.clipping = TextClipping.Clip;
            ApplyTextColorToAllStates(_keyStyle, theme.MutedTextColor);

            _valueStyle = new GUIStyle(GUI.skin.label);
            _valueStyle.wordWrap = true;
            _valueStyle.clipping = TextClipping.Clip;
            ApplyTextColorToAllStates(_valueStyle, theme.TextColor);

            _detailStyle = new GUIStyle(GUI.skin.label);
            _detailStyle.wordWrap = true;
            _detailStyle.clipping = TextClipping.Clip;
            ApplyTextColorToAllStates(_detailStyle, theme.TextColor);

            _monoStyle = new GUIStyle(GUI.skin.label);
            _monoStyle.wordWrap = true;
            _monoStyle.clipping = TextClipping.Clip;
            _monoStyle.font = GUI.skin != null ? GUI.skin.font : null;
            ApplyTextColorToAllStates(_monoStyle, theme.TextColor);

            _sectionLabelStyle = new GUIStyle(GUI.skin.label);
            _sectionLabelStyle.fontStyle = FontStyle.Bold;
            _sectionLabelStyle.wordWrap = false;
            _sectionLabelStyle.clipping = TextClipping.Clip;
            ApplyTextColorToAllStates(_sectionLabelStyle, theme.WarningColor);

            _cardStyle = new GUIStyle(GUI.skin.box);
            _cardStyle.padding = new RectOffset((int)CardPadding, (int)CardPadding, (int)CardPadding, (int)CardPadding);
            _cardStyle.margin = new RectOffset(0, 0, 0, 0);
            _cardStyle.border = new RectOffset(1, 1, 1, 1);
            ApplyBackgroundToAllStates(_cardStyle, _cardFill);
            ApplyTextColorToAllStates(_cardStyle, theme.TextColor);
        }

        private void DrawBorder(Rect rect)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), _borderFill);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), _borderFill);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), _borderFill);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), _borderFill);
        }

        private static string BuildThemeKey(ImguiPanelTheme theme)
        {
            return FormatColor(theme.BackgroundColor) + "|" +
                FormatColor(theme.HeaderColor) + "|" +
                FormatColor(theme.BorderColor) + "|" +
                FormatColor(theme.DividerColor) + "|" +
                FormatColor(theme.ActionFillColor) + "|" +
                FormatColor(theme.ActionActiveFillColor) + "|" +
                FormatColor(theme.CardFillColor) + "|" +
                FormatColor(theme.TextColor) + "|" +
                FormatColor(theme.MutedTextColor) + "|" +
                FormatColor(theme.AccentColor) + "|" +
                FormatColor(theme.WarningColor);
        }

        private static string FormatColor(Color color)
        {
            return color.r.ToString("F3") + "," +
                color.g.ToString("F3") + "," +
                color.b.ToString("F3") + "," +
                color.a.ToString("F3");
        }

        private static Texture2D MakeFill(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeBorderedTex(int width, int height, Color fillColor, Color borderColor)
        {
            var texture = new Texture2D(width, height);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    texture.SetPixel(x, y, isBorder ? borderColor : fillColor);
                }
            }

            texture.Apply();
            return texture;
        }

        private static void ApplyBackgroundToAllStates(GUIStyle style, Texture2D texture)
        {
            if (style == null)
            {
                return;
            }

            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.onNormal.background = texture;
            style.onHover.background = texture;
            style.onActive.background = texture;
            style.onFocused.background = texture;
        }

        private static void ApplyTextColorToAllStates(GUIStyle style, Color color)
        {
            if (style == null)
            {
                return;
            }

            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }
    }
}
