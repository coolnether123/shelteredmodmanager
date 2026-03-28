using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiHoverTooltipRenderer : IHoverTooltipRenderer
    {
        private const float DefaultTooltipWidth = 420f;
        private static readonly HoverTooltipPlacementOptions TooltipPlacementOptions = new HoverTooltipPlacementOptions
        {
            AnchorVerticalOffset = 4f,
            FallbackCursorOffsetX = 18f,
            FallbackCursorOffsetY = 18f,
            ClampMinX = 8f,
            ClampMinY = 8f,
            ClampRightMargin = 12f,
            ClampBottomMargin = 12f
        };

        private Rect _textTooltipAnchorRect = new Rect(0f, 0f, 0f, 0f);
        private string _textTooltipText = string.Empty;
        private Vector2 _lastValidViewport = Vector2.zero;
        private readonly HoverTooltipPlacementState _tooltipPlacementState = new HoverTooltipPlacementState();
        private string _pressedTooltipPartKey = string.Empty;

        private string _themeCacheKey = string.Empty;
        private GUIStyle _containerStyle;
        private GUIStyle _pathStyle;
        private GUIStyle _metaStyle;
        private GUIStyle _signatureStyle;
        private GUIStyle _linkStyle;
        private GUIStyle _detailStyle;
        private Texture2D _hoverFill;
        private Texture2D _underlineFill;
        private Texture2D _borderFill;

        public void ResetTextTooltip()
        {
            _textTooltipAnchorRect = new Rect(0f, 0f, 0f, 0f);
            _textTooltipText = string.Empty;
        }

        public void RegisterTextTooltip(RenderRect anchorRect, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _textTooltipAnchorRect = ToRect(anchorRect);
            _textTooltipText = text;
        }

        public void DrawTextTooltip(HoverTooltipThemePalette theme)
        {
            EnsureTheme(theme);
            if (string.IsNullOrEmpty(_textTooltipText) || _containerStyle == null)
            {
                return;
            }

            var current = Event.current;
            if (current == null)
            {
                return;
            }

            var maxWidth = Mathf.Min(620f, Mathf.Max(260f, Screen.width - 24f));
            var content = new GUIContent(_textTooltipText);
            var width = Mathf.Min(maxWidth, Mathf.Max(240f, _containerStyle.CalcSize(content).x + 16f));
            var height = Mathf.Max(30f, _containerStyle.CalcHeight(content, width) + 2f);
            var x = Mathf.Min(current.mousePosition.x + 18f, Mathf.Max(8f, Screen.width - width - 12f));
            var y = current.mousePosition.y + 18f;
            if (y + height > Screen.height - 12f)
            {
                y = Mathf.Max(8f, _textTooltipAnchorRect.yMin - height - 8f);
            }

            var rect = new Rect(x, y, width, height);
            GUI.Box(rect, content, _containerStyle);
            DrawBorder(rect, _borderFill, 1f);
        }

        public void ClearRichState()
        {
            _lastValidViewport = Vector2.zero;
            HoverTooltipPlacement.Reset(_tooltipPlacementState);
            _pressedTooltipPartKey = string.Empty;
        }

        public bool DrawRichTooltip(
            HoverTooltipRenderModel currentModel,
            RenderPoint mousePosition,
            RenderSize viewportSize,
            bool hasMouse,
            HoverTooltipThemePalette theme,
            float tooltipWidth,
            out HoverTooltipRenderResult result)
        {
            EnsureTheme(theme);
            result = new HoverTooltipRenderResult();
            var activeModel = currentModel;
            if (activeModel == null)
            {
                result.HiddenReason = "model-null";
                return false;
            }

            var current = Event.current;
            var mouse = new Vector2(mousePosition.X, mousePosition.Y);
            var effectiveViewport = ResolveTooltipViewportSize(viewportSize);
            if (!IsUsableTooltipViewport(effectiveViewport) || _containerStyle == null)
            {
                result.HiddenReason = !IsUsableTooltipViewport(effectiveViewport)
                    ? "viewport-invalid|" + effectiveViewport.x.ToString("F1") + "x" + effectiveViewport.y.ToString("F1") +
                      "|screen=" + Screen.width.ToString() + "x" + Screen.height.ToString()
                    : "theme-uninitialized";
                return false;
            }

            var maxWidth = tooltipWidth > 0f ? tooltipWidth : DefaultTooltipWidth;
            var signatureParts = activeModel.SignatureParts != null && activeModel.SignatureParts.Length > 0
                ? activeModel.SignatureParts
                : BuildFallbackParts(activeModel);
            var signatureWidth = MeasureTooltipPartsWidth(signatureParts);
            var effectiveWidth = Mathf.Min(maxWidth, Mathf.Max(180f, signatureWidth + 20f));
            var signatureHeight = LayoutTooltipParts(new Rect(0f, 0f, effectiveWidth - 16f, 0f), signatureParts, null, false);
            if (signatureHeight <= 0f)
            {
                result.HiddenReason = "signature-height-zero";
                ClearRichState();
                return false;
            }

            var tooltipSpawnX = HoverTooltipPlacement.ResolveSpawnX(_tooltipPlacementState, activeModel.Key ?? string.Empty, mouse.x);
            var tooltipRect = BuildTooltipRect(activeModel.AnchorRect, mouse, tooltipSpawnX, effectiveViewport, effectiveWidth, BuildCompactTooltipHeight(signatureHeight));
            var partVisuals = new List<TooltipPartVisual>();
            var signatureRect = BuildCompactTooltipSignatureRect(tooltipRect);
            LayoutTooltipParts(signatureRect, signatureParts, partVisuals, false);
            var hoveredPart = FindHoveredTooltipPart(partVisuals, mouse);

            GUI.Box(tooltipRect, GUIContent.none, _containerStyle);
            DrawBorder(tooltipRect, _borderFill, 1f);

            partVisuals.Clear();
            signatureRect = BuildCompactTooltipSignatureRect(tooltipRect);
            LayoutTooltipParts(signatureRect, signatureParts, partVisuals, true);
            hoveredPart = FindHoveredTooltipPart(partVisuals, mouse);
            if (hoveredPart != null && hoveredPart.IsInteractive && _hoverFill != null)
            {
                var hoveredRect = FindTooltipPartRect(partVisuals, hoveredPart);
                if (HasArea(hoveredRect))
                {
                    GUI.DrawTexture(hoveredRect, _hoverFill);
                    if (_linkStyle != null)
                    {
                        GUI.Label(hoveredRect, hoveredPart.Text ?? string.Empty, _linkStyle);
                    }

                    if (_underlineFill != null)
                    {
                        GUI.DrawTexture(new Rect(hoveredRect.x, hoveredRect.yMax - 1f, hoveredRect.width, 1f), _underlineFill);
                    }
                }
            }

            result.Visible = true;
            result.Model = activeModel;
            result.HoveredPart = hoveredPart;
            result.TooltipRect = ToRenderRect(tooltipRect);
            result.ActivatedPart = HandleTooltipPartInteraction(current, hoveredPart);
            return true;
        }

        private void EnsureTheme(HoverTooltipThemePalette theme)
        {
            var effectiveTheme = theme ?? new HoverTooltipThemePalette();
            var themeKey = BuildThemeKey(effectiveTheme);
            if (string.Equals(_themeCacheKey, themeKey, StringComparison.Ordinal) && _containerStyle != null)
            {
                return;
            }

            _themeCacheKey = themeKey;
            var backgroundColor = ToColor(effectiveTheme.BackgroundColor, new Color(0.11f, 0.12f, 0.14f, 0.98f));
            var borderColor = ToColor(effectiveTheme.BorderColor, new Color(0.26f, 0.31f, 0.37f, 1f));
            var textColor = ToColor(effectiveTheme.TextColor, new Color(0.94f, 0.94f, 0.95f, 1f));
            var mutedTextColor = ToColor(effectiveTheme.MutedTextColor, new Color(0.68f, 0.72f, 0.77f, 1f));
            var accentColor = ToColor(effectiveTheme.AccentColor, new Color(0.31f, 0.54f, 0.84f, 1f));
            var hoverFillColor = ToColor(effectiveTheme.HoverFillColor, new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f));

            _hoverFill = MakeFill(hoverFillColor);
            _underlineFill = MakeFill(accentColor);
            _borderFill = MakeFill(borderColor);

            _containerStyle = new GUIStyle(GUI.skin.box);
            ApplyBackgroundToAllStates(_containerStyle, MakeFill(backgroundColor));
            ApplyTextColorToAllStates(_containerStyle, textColor);
            _containerStyle.alignment = TextAnchor.UpperLeft;
            _containerStyle.wordWrap = true;
            _containerStyle.padding = new RectOffset(8, 8, 8, 8);
            _containerStyle.margin = new RectOffset(0, 0, 0, 0);
            _containerStyle.border = new RectOffset(1, 1, 1, 1);

            _pathStyle = new GUIStyle(GUI.skin.label);
            ApplyTextColorToAllStates(_pathStyle, mutedTextColor);
            _pathStyle.wordWrap = true;
            _pathStyle.alignment = TextAnchor.UpperLeft;
            _pathStyle.fontStyle = FontStyle.Italic;

            _metaStyle = new GUIStyle(GUI.skin.label);
            ApplyTextColorToAllStates(_metaStyle, mutedTextColor);
            _metaStyle.wordWrap = true;
            _metaStyle.alignment = TextAnchor.UpperLeft;

            _signatureStyle = new GUIStyle(GUI.skin.label);
            ApplyTextColorToAllStates(_signatureStyle, textColor);
            _signatureStyle.wordWrap = false;
            _signatureStyle.alignment = TextAnchor.UpperLeft;
            _signatureStyle.fontStyle = FontStyle.Bold;

            _linkStyle = new GUIStyle(_signatureStyle);
            ApplyTextColorToAllStates(_linkStyle, accentColor);

            _detailStyle = new GUIStyle(GUI.skin.label);
            ApplyTextColorToAllStates(_detailStyle, textColor);
            _detailStyle.wordWrap = true;
            _detailStyle.alignment = TextAnchor.UpperLeft;
        }

        private Vector2 ResolveTooltipViewportSize(RenderSize viewportSize)
        {
            var size = new Vector2(viewportSize.Width, viewportSize.Height);
            if (IsUsableTooltipViewport(size))
            {
                _lastValidViewport = size;
                return size;
            }

            var screenViewport = new Vector2(Screen.width, Screen.height);
            if (IsUsableTooltipViewport(screenViewport))
            {
                _lastValidViewport = screenViewport;
                return screenViewport;
            }

            return _lastValidViewport;
        }

        private static bool IsUsableTooltipViewport(Vector2 viewportSize)
        {
            return viewportSize.x >= 64f && viewportSize.y >= 64f;
        }

        private static EditorHoverContentPart[] BuildFallbackParts(HoverTooltipRenderModel model)
        {
            return new[]
            {
                new EditorHoverContentPart
                {
                    Text = model != null ? model.SymbolDisplay ?? string.Empty : string.Empty,
                    IsInteractive = false
                }
            };
        }

        private static float CalculateHeight(GUIStyle style, string text, float width, float minimum)
        {
            if (style == null || string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            return Mathf.Max(minimum, style.CalcHeight(new GUIContent(text), width));
        }

        private float MeasureTooltipPartsWidth(EditorHoverContentPart[] parts)
        {
            var signatureStyle = _signatureStyle ?? GUI.skin.label;
            var linkStyle = _linkStyle ?? signatureStyle;
            var width = 0f;
            for (var i = 0; parts != null && i < parts.Length; i++)
            {
                var part = parts[i];
                var text = part != null ? part.Text ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var style = part.IsInteractive ? linkStyle : signatureStyle;
                width += Mathf.Max(2f, style.CalcSize(new GUIContent(text)).x);
            }

            return width;
        }

        private static string BuildMetaText(HoverTooltipRenderModel model, EditorHoverContentPart hoveredPart)
        {
            return hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.SummaryText)
                ? hoveredPart.SummaryText ?? string.Empty
                : (model != null ? model.SummaryText ?? string.Empty : string.Empty);
        }

        private static string BuildDetailText(HoverTooltipRenderModel model, EditorHoverContentPart hoveredPart)
        {
            if (model == null)
            {
                return string.Empty;
            }

            var detail = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.DocumentationText)
                ? hoveredPart.DocumentationText ?? string.Empty
                : model.DocumentationText ?? string.Empty;
            var supplemental = hoveredPart != null
                ? FormatSupplementalSections(hoveredPart.SupplementalSections)
                : string.Empty;

            if (string.IsNullOrEmpty(detail))
            {
                return supplemental;
            }

            if (string.IsNullOrEmpty(supplemental))
            {
                return detail;
            }

            return supplemental + Environment.NewLine + detail;
        }

        private float LayoutTooltipParts(Rect bounds, EditorHoverContentPart[] parts, List<TooltipPartVisual> visuals, bool draw)
        {
            var signatureStyle = _signatureStyle ?? GUI.skin.label;
            var linkStyle = _linkStyle ?? signatureStyle;
            var x = bounds.x;
            var y = bounds.y;
            var maxX = bounds.x + Mathf.Max(8f, bounds.width);
            var lineHeight = Mathf.Max(18f, signatureStyle.CalcSize(new GUIContent("Ag")).y + 2f);

            for (var i = 0; parts != null && i < parts.Length; i++)
            {
                var part = parts[i];
                var text = part != null ? part.Text ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var style = part.IsInteractive ? linkStyle : signatureStyle;
                var width = Mathf.Max(2f, style.CalcSize(new GUIContent(text)).x);
                if (x > bounds.x && x + width > maxX)
                {
                    x = bounds.x;
                    y += lineHeight;
                }

                var rect = new Rect(x, y, width, lineHeight);
                if (visuals != null)
                {
                    visuals.Add(new TooltipPartVisual { Part = part, Rect = rect });
                }

                if (draw)
                {
                    GUI.Label(rect, text, style);
                }

                x += width;
            }

            return Mathf.Max(lineHeight, (y - bounds.y) + lineHeight);
        }

        private float MeasureSections(EditorHoverSection[] sections, float width)
        {
            var bounds = new Rect(0f, 0f, width, 0f);
            return LayoutSections(bounds, sections, null, false);
        }

        private float LayoutSections(Rect bounds, EditorHoverSection[] sections, List<TooltipPartVisual> visuals, bool draw)
        {
            if (sections == null || sections.Length == 0)
            {
                return 0f;
            }

            var y = bounds.y;
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var titleText = HumanizeSectionTitle(section.Title);
                if (!string.IsNullOrEmpty(titleText) && _metaStyle != null)
                {
                    var titleHeight = Mathf.Max(14f, _metaStyle.CalcHeight(new GUIContent(titleText), bounds.width));
                    if (draw)
                    {
                        GUI.Label(new Rect(bounds.x, y, bounds.width, titleHeight), titleText, _metaStyle);
                    }

                    y += titleHeight + 2f;
                }

                if (section.DisplayParts != null && section.DisplayParts.Length > 0)
                {
                    var partHeight = LayoutTooltipParts(new Rect(bounds.x, y, bounds.width, 0f), section.DisplayParts, visuals, draw);
                    y += partHeight + 4f;
                    continue;
                }

                if (!string.IsNullOrEmpty(section.Text) && _detailStyle != null)
                {
                    var textHeight = Mathf.Max(14f, _detailStyle.CalcHeight(new GUIContent(section.Text), bounds.width));
                    if (draw)
                    {
                        GUI.Label(new Rect(bounds.x, y, bounds.width, textHeight), section.Text, _detailStyle);
                    }

                    y += textHeight + 4f;
                }
            }

            return Mathf.Max(0f, y - bounds.y);
        }

        private static EditorHoverContentPart FindHoveredTooltipPart(List<TooltipPartVisual> visuals, Vector2 mousePosition)
        {
            if (visuals == null)
            {
                return null;
            }

            for (var i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                if (visual != null && visual.Part != null && visual.Part.IsInteractive && visual.Rect.Contains(mousePosition))
                {
                    return visual.Part;
                }
            }

            return null;
        }

        private static Rect FindTooltipPartRect(List<TooltipPartVisual> visuals, EditorHoverContentPart hoveredPart)
        {
            if (visuals == null || hoveredPart == null)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            for (var i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                if (visual != null && object.ReferenceEquals(visual.Part, hoveredPart))
                {
                    return visual.Rect;
                }
            }

            return new Rect(0f, 0f, 0f, 0f);
        }

        private static float BuildCompactTooltipHeight(float signatureHeight)
        {
            return Mathf.Max(30f, signatureHeight + 16f);
        }

        private Rect BuildTooltipRect(RenderRect anchorRect, Vector2 mousePosition, float tooltipSpawnX, Vector2 viewportSize, float tooltipWidth, float height)
        {
            var rect = HoverTooltipPlacement.BuildRect(
                anchorRect,
                new RenderPoint(mousePosition.x, mousePosition.y),
                tooltipSpawnX,
                new RenderSize(viewportSize.x, viewportSize.y),
                tooltipWidth,
                height,
                TooltipPlacementOptions);
            return ToRect(rect);
        }

        private static Rect BuildCompactTooltipSignatureRect(Rect tooltipRect)
        {
            return new Rect(tooltipRect.x + 8f, tooltipRect.y + 8f, tooltipRect.width - 16f, Mathf.Max(0f, tooltipRect.height - 16f));
        }

        private EditorHoverContentPart HandleTooltipPartInteraction(Event current, EditorHoverContentPart hoveredPart)
        {
            if (current == null || current.button != 0)
            {
                return null;
            }

            var partKey = hoveredPart != null && hoveredPart.IsInteractive ? BuildTooltipPartKey(hoveredPart) : string.Empty;
            if (current.type == EventType.MouseDown)
            {
                _pressedTooltipPartKey = partKey;
                if (!string.IsNullOrEmpty(partKey))
                {
                    current.Use();
                }

                return null;
            }

            if (current.type != EventType.MouseUp)
            {
                return null;
            }

            var shouldActivate = !string.IsNullOrEmpty(partKey) && string.Equals(_pressedTooltipPartKey, partKey, StringComparison.Ordinal);
            _pressedTooltipPartKey = string.Empty;
            if (!shouldActivate)
            {
                return null;
            }

            current.Use();
            return hoveredPart;
        }

        private static string FormatSupplementalSections(EditorHoverSection[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null || string.IsNullOrEmpty(section.Text))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(section.Title))
                {
                    lines.Add(HumanizeSectionTitle(section.Title) + ": " + section.Text);
                }
                else
                {
                    lines.Add(section.Text);
                }
            }

            return lines.Count > 0 ? string.Join(Environment.NewLine, lines.ToArray()) : string.Empty;
        }

        private static string HumanizeSectionTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            var result = string.Empty;
            for (var i = 0; i < title.Length; i++)
            {
                var current = title[i];
                if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(title[i - 1]))
                {
                    result += " ";
                }

                result += current;
            }

            return result;
        }

        private static bool HasArea(Rect rect)
        {
            return rect.width > 0f && rect.height > 0f;
        }

        private static bool HasInlineOverloadSummary(EditorHoverContentPart[] signatureParts, string overloadSummary)
        {
            if (signatureParts == null || signatureParts.Length == 0 || string.IsNullOrEmpty(overloadSummary))
            {
                return false;
            }

            for (var i = 0; i < signatureParts.Length; i++)
            {
                var text = signatureParts[i] != null ? signatureParts[i].Text ?? string.Empty : string.Empty;
                if (!string.IsNullOrEmpty(text) && text.IndexOf(overloadSummary, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildTooltipPartKey(EditorHoverContentPart part)
        {
            var navigationTarget = part != null ? part.NavigationTarget : null;
            return (part != null ? part.Text ?? string.Empty : string.Empty) + "|" +
                (navigationTarget != null ? navigationTarget.MetadataName ?? string.Empty : string.Empty) + "|" +
                (navigationTarget != null ? navigationTarget.DefinitionDocumentPath ?? string.Empty : string.Empty);
        }

        private static void DrawBorder(Rect rect, Texture2D texture, float thickness)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f || thickness <= 0f)
            {
                return;
            }

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), texture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), texture);
        }

        private static string BuildThemeKey(HoverTooltipThemePalette theme)
        {
            return FormatColor(theme.BackgroundColor) + "|" +
                FormatColor(theme.BorderColor) + "|" +
                FormatColor(theme.TextColor) + "|" +
                FormatColor(theme.MutedTextColor) + "|" +
                FormatColor(theme.AccentColor) + "|" +
                FormatColor(theme.HoverFillColor);
        }

        private static string FormatColor(RenderColor color)
        {
            return color.R.ToString("F3") + "|" + color.G.ToString("F3") + "|" + color.B.ToString("F3") + "|" + color.A.ToString("F3");
        }

        private static Color ToColor(RenderColor color, Color fallback)
        {
            if (color.A == 0f && color.R == 0f && color.G == 0f && color.B == 0f)
            {
                return fallback;
            }

            return new Color(color.R, color.G, color.B, color.A);
        }

        private static Texture2D MakeFill(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
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

        private static Rect ToRect(RenderRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static RenderRect ToRenderRect(Rect rect)
        {
            return new RenderRect(rect.x, rect.y, rect.width, rect.height);
        }

        private sealed class TooltipPartVisual
        {
            public EditorHoverContentPart Part;
            public Rect Rect;
        }
    }
}
