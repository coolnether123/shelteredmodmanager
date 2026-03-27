using System;
using System.Collections.Generic;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiHoverTooltipRenderer : IHoverTooltipRenderer
    {
        private const float DefaultTooltipWidth = 420f;
        private const double StickyHoverGraceMs = 700d;

        private Rect _textTooltipAnchorRect = new Rect(0f, 0f, 0f, 0f);
        private string _textTooltipText = string.Empty;
        private HoverTooltipRenderModel _stickyModel;
        private string _stickyHoverKey = string.Empty;
        private string _stickyHoverDocumentPath = string.Empty;
        private Rect _stickyHoverAnchorRect = new Rect(0f, 0f, 0f, 0f);
        private Rect _stickyHoverTooltipRect = new Rect(0f, 0f, 0f, 0f);
        private Vector2 _lastValidViewport = Vector2.zero;
        private DateTime _stickyHoverKeepAliveUtc = DateTime.MinValue;
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
            _stickyModel = null;
            _stickyHoverKey = string.Empty;
            _stickyHoverDocumentPath = string.Empty;
            _stickyHoverAnchorRect = new Rect(0f, 0f, 0f, 0f);
            _stickyHoverTooltipRect = new Rect(0f, 0f, 0f, 0f);
            _lastValidViewport = Vector2.zero;
            _stickyHoverKeepAliveUtc = DateTime.MinValue;
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
            var current = Event.current;
            var mouse = new Vector2(mousePosition.X, mousePosition.Y);
            var effectiveViewport = ResolveTooltipViewportSize(viewportSize);
            if (!IsUsableTooltipViewport(effectiveViewport) || _containerStyle == null)
            {
                return false;
            }

            var activeModel = ResolveVisibleModel(currentModel, mouse, hasMouse);
            if (activeModel == null)
            {
                return false;
            }

            var effectiveWidth = tooltipWidth > 0f ? tooltipWidth : DefaultTooltipWidth;
            var signatureParts = activeModel.SignatureParts != null && activeModel.SignatureParts.Length > 0
                ? activeModel.SignatureParts
                : BuildFallbackParts(activeModel);
            var canUpdateTooltipVisuals = current == null ||
                current.type == EventType.Repaint ||
                current.type == EventType.MouseMove ||
                current.type == EventType.MouseDrag ||
                current.type == EventType.MouseDown ||
                current.type == EventType.MouseUp;

            var initialMetaText = BuildMetaText(activeModel, null);
            var initialDetailText = BuildDetailText(activeModel, null);
            var pathHeight = CalculateHeight(_pathStyle, activeModel.QualifiedPath, effectiveWidth - 16f, 16f);
            var metaHeight = CalculateHeight(_metaStyle, initialMetaText, effectiveWidth - 16f, 16f);
            var signatureHeight = LayoutTooltipParts(new Rect(0f, 0f, effectiveWidth - 16f, 0f), signatureParts, null, false);
            if (signatureHeight <= 0f)
            {
                ClearRichState();
                return false;
            }

            var tooltipRect = BuildTooltipRect(activeModel.AnchorRect, mouse, effectiveViewport, effectiveWidth, BuildTooltipHeight(effectiveWidth, pathHeight, metaHeight, signatureHeight, initialDetailText));
            var partVisuals = new List<TooltipPartVisual>();
            var signatureRect = BuildTooltipSignatureRect(tooltipRect, pathHeight, metaHeight);
            LayoutTooltipParts(signatureRect, signatureParts, partVisuals, false);
            var hoveredPart = FindHoveredTooltipPart(partVisuals, mouse);

            var metaText = BuildMetaText(activeModel, hoveredPart);
            metaHeight = CalculateHeight(_metaStyle, metaText, effectiveWidth - 16f, 16f);
            var detailText = BuildDetailText(activeModel, hoveredPart);
            var finalHeight = BuildTooltipHeight(effectiveWidth, pathHeight, metaHeight, signatureHeight, detailText);
            if (canUpdateTooltipVisuals || !HasArea(_stickyHoverTooltipRect))
            {
                tooltipRect = BuildTooltipRect(activeModel.AnchorRect, mouse, effectiveViewport, effectiveWidth, finalHeight);
                _stickyHoverTooltipRect = tooltipRect;
            }
            else
            {
                tooltipRect = _stickyHoverTooltipRect;
            }

            GUI.Box(tooltipRect, GUIContent.none, _containerStyle);
            DrawBorder(tooltipRect, _borderFill, 1f);
            if (!string.IsNullOrEmpty(activeModel.QualifiedPath) && _pathStyle != null)
            {
                GUI.Label(BuildTooltipPathRect(tooltipRect, pathHeight), activeModel.QualifiedPath, _pathStyle);
            }

            if (!string.IsNullOrEmpty(metaText) && _metaStyle != null)
            {
                GUI.Label(BuildTooltipMetaRect(tooltipRect, pathHeight, metaHeight), metaText, _metaStyle);
            }

            partVisuals.Clear();
            signatureRect = BuildTooltipSignatureRect(tooltipRect, pathHeight, metaHeight);
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

            if (!string.IsNullOrEmpty(detailText) && _detailStyle != null)
            {
                var detailRect = BuildTooltipDetailRect(tooltipRect, pathHeight, metaHeight, signatureHeight);
                GUI.Label(detailRect, detailText, _detailStyle);
            }

            if (object.ReferenceEquals(activeModel, currentModel) || (hasMouse && IsPointerWithinRichHoverSurface(mouse)))
            {
                RefreshStickyHoverKeepAlive();
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

        private HoverTooltipRenderModel ResolveVisibleModel(HoverTooltipRenderModel currentModel, Vector2 mousePosition, bool hasMouse)
        {
            if (currentModel != null)
            {
                var currentKey = currentModel.Key ?? string.Empty;
                if (!string.IsNullOrEmpty(_stickyHoverKey) &&
                    !string.Equals(currentKey, _stickyHoverKey, StringComparison.Ordinal) &&
                    (IsPointerWithinRichHoverSurface(mousePosition) || DateTime.UtcNow <= _stickyHoverKeepAliveUtc))
                {
                    return _stickyModel;
                }

                _stickyModel = currentModel;
                _stickyHoverKey = currentKey;
                _stickyHoverDocumentPath = currentModel.DocumentPath ?? string.Empty;
                _stickyHoverAnchorRect = ToRect(currentModel.AnchorRect);
                RefreshStickyHoverKeepAlive();
                return currentModel;
            }

            if (_stickyModel == null)
            {
                return null;
            }

            if ((hasMouse && IsPointerWithinRichHoverSurface(mousePosition)) || DateTime.UtcNow <= _stickyHoverKeepAliveUtc)
            {
                return _stickyModel;
            }

            ClearRichState();
            return null;
        }

        private Vector2 ResolveTooltipViewportSize(RenderSize viewportSize)
        {
            var size = new Vector2(viewportSize.Width, viewportSize.Height);
            if (IsUsableTooltipViewport(size))
            {
                _lastValidViewport = size;
                return size;
            }

            return _lastValidViewport;
        }

        private static bool IsUsableTooltipViewport(Vector2 viewportSize)
        {
            return viewportSize.x >= 64f && viewportSize.y >= 64f;
        }

        private static HoverTooltipPartModel[] BuildFallbackParts(HoverTooltipRenderModel model)
        {
            return new[]
            {
                new HoverTooltipPartModel
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

        private static string BuildMetaText(HoverTooltipRenderModel model, HoverTooltipPartModel hoveredPart)
        {
            return model != null && model.BuildMetaText != null
                ? model.BuildMetaText(hoveredPart)
                : string.Empty;
        }

        private static string BuildDetailText(HoverTooltipRenderModel model, HoverTooltipPartModel hoveredPart)
        {
            if (model == null)
            {
                return string.Empty;
            }

            var detail = model.BuildDocumentationText != null ? model.BuildDocumentationText(hoveredPart) : string.Empty;
            var supplemental = model.GetSupplementalSections != null ? FormatSupplementalSections(model.GetSupplementalSections(hoveredPart)) : string.Empty;

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

        private float LayoutTooltipParts(Rect bounds, HoverTooltipPartModel[] parts, List<TooltipPartVisual> visuals, bool draw)
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

        private static HoverTooltipPartModel FindHoveredTooltipPart(List<TooltipPartVisual> visuals, Vector2 mousePosition)
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

        private static Rect FindTooltipPartRect(List<TooltipPartVisual> visuals, HoverTooltipPartModel hoveredPart)
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

        private float BuildTooltipHeight(float tooltipWidth, float pathHeight, float metaHeight, float signatureHeight, string detailText)
        {
            var detailHeight = string.IsNullOrEmpty(detailText) || _detailStyle == null ? 0f : _detailStyle.CalcHeight(new GUIContent(detailText), tooltipWidth - 16f);
            return Mathf.Min(360f, 14f + pathHeight + metaHeight + signatureHeight + (detailHeight > 0f ? detailHeight + 8f : 0f));
        }

        private Rect BuildTooltipRect(RenderRect anchorRect, Vector2 mousePosition, Vector2 viewportSize, float tooltipWidth, float height)
        {
            var unityAnchorRect = ToRect(anchorRect);
            if (HasArea(unityAnchorRect))
            {
                return ClampTooltipRect(BuildTooltipRectFromAnchor(unityAnchorRect, viewportSize, tooltipWidth, height), viewportSize);
            }

            if (HasArea(_stickyHoverTooltipRect))
            {
                var stickyRect = _stickyHoverTooltipRect;
                stickyRect.width = tooltipWidth;
                stickyRect.height = height;
                return ClampTooltipRect(stickyRect, viewportSize);
            }

            return ClampTooltipRect(new Rect(mousePosition.x + 18f, mousePosition.y + 18f, tooltipWidth, height), viewportSize);
        }

        private static Rect BuildTooltipRectFromAnchor(Rect anchorRect, Vector2 viewportSize, float tooltipWidth, float height)
        {
            var x = anchorRect.xMin - 4f;
            var y = anchorRect.yMax - 3f;
            if (y + height > viewportSize.y - 12f)
            {
                y = anchorRect.yMin - height + 3f;
            }

            return new Rect(x, y, tooltipWidth, height);
        }

        private static Rect ClampTooltipRect(Rect rect, Vector2 viewportSize)
        {
            rect.x = Mathf.Min(rect.x, Mathf.Max(8f, viewportSize.x - rect.width - 12f));
            rect.y = Mathf.Min(rect.y, Mathf.Max(8f, viewportSize.y - rect.height - 12f));
            rect.x = Mathf.Max(8f, rect.x);
            rect.y = Mathf.Max(8f, rect.y);
            return rect;
        }

        private bool IsPointerWithinRichHoverSurface(Vector2 mousePosition)
        {
            if (IsPointerWithinTooltip(mousePosition))
            {
                return true;
            }

            if (HasArea(_stickyHoverAnchorRect) && _stickyHoverAnchorRect.Contains(mousePosition))
            {
                return true;
            }

            var bridgeRect = BuildHoverBridgeRect();
            return HasArea(bridgeRect) && bridgeRect.Contains(mousePosition);
        }

        private bool IsPointerWithinTooltip(Vector2 mousePosition)
        {
            return HasArea(_stickyHoverTooltipRect) && _stickyHoverTooltipRect.Contains(mousePosition);
        }

        private Rect BuildHoverBridgeRect()
        {
            if (!HasArea(_stickyHoverAnchorRect) || !HasArea(_stickyHoverTooltipRect))
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            var overlapLeft = Mathf.Max(_stickyHoverAnchorRect.xMin, _stickyHoverTooltipRect.xMin);
            var overlapRight = Mathf.Min(_stickyHoverAnchorRect.xMax, _stickyHoverTooltipRect.xMax);
            if (overlapRight > overlapLeft)
            {
                return Rect.MinMaxRect(
                    overlapLeft - 6f,
                    Mathf.Min(_stickyHoverAnchorRect.yMin, _stickyHoverTooltipRect.yMin) - 4f,
                    overlapRight + 6f,
                    Mathf.Max(_stickyHoverAnchorRect.yMax, _stickyHoverTooltipRect.yMax) + 4f);
            }

            var overlapTop = Mathf.Max(_stickyHoverAnchorRect.yMin, _stickyHoverTooltipRect.yMin);
            var overlapBottom = Mathf.Min(_stickyHoverAnchorRect.yMax, _stickyHoverTooltipRect.yMax);
            if (overlapBottom > overlapTop)
            {
                return Rect.MinMaxRect(
                    Mathf.Min(_stickyHoverAnchorRect.xMin, _stickyHoverTooltipRect.xMin) - 4f,
                    overlapTop - 6f,
                    Mathf.Max(_stickyHoverAnchorRect.xMax, _stickyHoverTooltipRect.xMax) + 4f,
                    overlapBottom + 6f);
            }

            var anchorCenter = _stickyHoverAnchorRect.center;
            var tooltipCenter = _stickyHoverTooltipRect.center;
            return Rect.MinMaxRect(
                Mathf.Min(anchorCenter.x, tooltipCenter.x) - 8f,
                Mathf.Min(anchorCenter.y, tooltipCenter.y) - 8f,
                Mathf.Max(anchorCenter.x, tooltipCenter.x) + 8f,
                Mathf.Max(anchorCenter.y, tooltipCenter.y) + 8f);
        }

        private void RefreshStickyHoverKeepAlive()
        {
            _stickyHoverKeepAliveUtc = DateTime.UtcNow.AddMilliseconds(StickyHoverGraceMs);
        }

        private static Rect BuildTooltipPathRect(Rect tooltipRect, float pathHeight)
        {
            return new Rect(tooltipRect.x + 8f, tooltipRect.y + 7f, tooltipRect.width - 16f, Mathf.Max(0f, pathHeight));
        }

        private static Rect BuildTooltipMetaRect(Rect tooltipRect, float pathHeight, float metaHeight)
        {
            return new Rect(tooltipRect.x + 8f, tooltipRect.y + 7f + pathHeight, tooltipRect.width - 16f, Mathf.Max(0f, metaHeight));
        }

        private static Rect BuildTooltipSignatureRect(Rect tooltipRect, float pathHeight, float metaHeight)
        {
            return new Rect(tooltipRect.x + 8f, tooltipRect.y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f), tooltipRect.width - 16f, Mathf.Max(0f, tooltipRect.height - pathHeight - metaHeight - 14f));
        }

        private static Rect BuildTooltipDetailRect(Rect tooltipRect, float pathHeight, float metaHeight, float signatureHeight)
        {
            return new Rect(tooltipRect.x + 8f, tooltipRect.y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f) + signatureHeight + 6f, tooltipRect.width - 16f, Mathf.Max(0f, tooltipRect.height - pathHeight - metaHeight - signatureHeight - 20f));
        }

        private HoverTooltipPartModel HandleTooltipPartInteraction(Event current, HoverTooltipPartModel hoveredPart)
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

        private static string FormatSupplementalSections(HoverTooltipSectionModel[] sections)
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

        private static string BuildTooltipPartKey(HoverTooltipPartModel part)
        {
            return (part != null ? part.Text ?? string.Empty : string.Empty) + "|" + (part != null && part.Tag != null ? part.Tag.GetHashCode().ToString() : string.Empty);
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
            public HoverTooltipPartModel Part;
            public Rect Rect;
        }
    }
}
