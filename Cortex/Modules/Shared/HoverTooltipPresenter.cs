using System;
using System.Collections.Generic;
using Cortex.LanguageService.Protocol;
using UnityEngine;

namespace Cortex.Modules.Shared
{
    internal sealed class HoverTooltipStyleSet
    {
        public float Width;
        public GUIStyle ContainerStyle;
        public GUIStyle PathStyle;
        public GUIStyle MetaStyle;
        public GUIStyle SignatureStyle;
        public GUIStyle LinkStyle;
        public GUIStyle DetailStyle;
        public Texture2D HoverFill;
        public Texture2D UnderlineFill;
        public Texture2D BorderFill;
    }

    internal sealed class HoverTooltipRenderModel
    {
        public string Key = string.Empty;
        public string DocumentPath = string.Empty;
        public Rect AnchorRect = new Rect(0f, 0f, 0f, 0f);
        public string QualifiedPath = string.Empty;
        public LanguageServiceHoverResponse Response;
        public LanguageServiceHoverDisplayPart[] SignatureParts;
        public Func<LanguageServiceHoverDisplayPart, string> BuildMetaText;
        public Func<LanguageServiceHoverDisplayPart, string> BuildDocumentationText;
        public Func<LanguageServiceHoverDisplayPart, LanguageServiceHoverSection[]> GetSupplementalSections;
    }

    internal sealed class HoverTooltipRenderResult
    {
        public bool Visible;
        public HoverTooltipRenderModel Model;
        public LanguageServiceHoverDisplayPart HoveredPart;
        public LanguageServiceHoverDisplayPart ActivatedPart;
        public Rect TooltipRect = new Rect(0f, 0f, 0f, 0f);
    }

    internal sealed class HoverTooltipPresenter
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

        public void ResetTextTooltip()
        {
            _textTooltipAnchorRect = new Rect(0f, 0f, 0f, 0f);
            _textTooltipText = string.Empty;
        }

        public void RegisterTextTooltip(Rect anchorRect, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _textTooltipAnchorRect = anchorRect;
            _textTooltipText = text;
        }

        public void DrawTextTooltip(HoverTooltipStyleSet styles)
        {
            if (string.IsNullOrEmpty(_textTooltipText) || styles == null || styles.ContainerStyle == null)
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
            var width = Mathf.Min(maxWidth, Mathf.Max(240f, styles.ContainerStyle.CalcSize(content).x + 16f));
            var height = Mathf.Max(30f, styles.ContainerStyle.CalcHeight(content, width) + 2f);
            var x = Mathf.Min(current.mousePosition.x + 18f, Mathf.Max(8f, Screen.width - width - 12f));
            var y = current.mousePosition.y + 18f;
            if (y + height > Screen.height - 12f)
            {
                y = Mathf.Max(8f, _textTooltipAnchorRect.yMin - height - 8f);
            }

            var rect = new Rect(x, y, width, height);
            GUI.Box(rect, content, styles.ContainerStyle);
            DrawBorder(rect, styles.BorderFill, 1f);
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
            Event current,
            Vector2 mousePosition,
            Vector2 viewportSize,
            bool hasMouse,
            HoverTooltipStyleSet styles,
            out HoverTooltipRenderResult result)
        {
            result = new HoverTooltipRenderResult();
            var effectiveViewport = ResolveTooltipViewportSize(viewportSize);
            if (!IsUsableTooltipViewport(effectiveViewport) || styles == null || styles.ContainerStyle == null)
            {
                return false;
            }

            var activeModel = ResolveVisibleModel(currentModel, mousePosition, hasMouse);
            if (activeModel == null)
            {
                return false;
            }

            var tooltipWidth = styles.Width > 0f ? styles.Width : DefaultTooltipWidth;
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
            var pathHeight = CalculateHeight(styles.PathStyle, activeModel.QualifiedPath, tooltipWidth - 16f, 16f);
            var metaHeight = CalculateHeight(styles.MetaStyle, initialMetaText, tooltipWidth - 16f, 16f);
            var signatureHeight = LayoutTooltipParts(new Rect(0f, 0f, tooltipWidth - 16f, 0f), signatureParts, null, false, styles);
            if (signatureHeight <= 0f)
            {
                ClearRichState();
                return false;
            }

            var tooltipRect = BuildTooltipRect(activeModel.AnchorRect, mousePosition, effectiveViewport, tooltipWidth, BuildTooltipHeight(styles, tooltipWidth, pathHeight, metaHeight, signatureHeight, initialDetailText));
            var partVisuals = new List<TooltipPartVisual>();
            var signatureRect = BuildTooltipSignatureRect(tooltipRect, pathHeight, metaHeight);
            LayoutTooltipParts(signatureRect, signatureParts, partVisuals, false, styles);
            var hoveredPart = FindHoveredTooltipPart(partVisuals, mousePosition);

            var metaText = BuildMetaText(activeModel, hoveredPart);
            metaHeight = CalculateHeight(styles.MetaStyle, metaText, tooltipWidth - 16f, 16f);
            var detailText = BuildDetailText(activeModel, hoveredPart);
            var finalHeight = BuildTooltipHeight(styles, tooltipWidth, pathHeight, metaHeight, signatureHeight, detailText);
            if (canUpdateTooltipVisuals || !HasArea(_stickyHoverTooltipRect))
            {
                tooltipRect = BuildTooltipRect(activeModel.AnchorRect, mousePosition, effectiveViewport, tooltipWidth, finalHeight);
                _stickyHoverTooltipRect = tooltipRect;
            }
            else
            {
                tooltipRect = _stickyHoverTooltipRect;
            }

            GUI.Box(tooltipRect, GUIContent.none, styles.ContainerStyle);
            DrawBorder(tooltipRect, styles.BorderFill, 1f);
            if (!string.IsNullOrEmpty(activeModel.QualifiedPath) && styles.PathStyle != null)
            {
                GUI.Label(BuildTooltipPathRect(tooltipRect, pathHeight), activeModel.QualifiedPath, styles.PathStyle);
            }

            if (!string.IsNullOrEmpty(metaText) && styles.MetaStyle != null)
            {
                GUI.Label(BuildTooltipMetaRect(tooltipRect, pathHeight, metaHeight), metaText, styles.MetaStyle);
            }

            partVisuals.Clear();
            signatureRect = BuildTooltipSignatureRect(tooltipRect, pathHeight, metaHeight);
            LayoutTooltipParts(signatureRect, signatureParts, partVisuals, true, styles);
            hoveredPart = FindHoveredTooltipPart(partVisuals, mousePosition);
            if (hoveredPart != null && hoveredPart.IsInteractive && styles.HoverFill != null)
            {
                var hoveredRect = FindTooltipPartRect(partVisuals, hoveredPart);
                if (HasArea(hoveredRect))
                {
                    GUI.DrawTexture(hoveredRect, styles.HoverFill);
                    if (styles.LinkStyle != null)
                    {
                        GUI.Label(hoveredRect, hoveredPart.Text ?? string.Empty, styles.LinkStyle);
                    }

                    if (styles.UnderlineFill != null)
                    {
                        GUI.DrawTexture(new Rect(hoveredRect.x, hoveredRect.yMax - 1f, hoveredRect.width, 1f), styles.UnderlineFill);
                    }
                }
            }

            if (!string.IsNullOrEmpty(detailText) && styles.DetailStyle != null)
            {
                var detailRect = BuildTooltipDetailRect(tooltipRect, pathHeight, metaHeight, signatureHeight);
                GUI.Label(detailRect, detailText, styles.DetailStyle);
            }

            if (object.ReferenceEquals(activeModel, currentModel) ||
                (hasMouse && IsPointerWithinRichHoverSurface(mousePosition)))
            {
                RefreshStickyHoverKeepAlive();
            }
            result.Visible = true;
            result.Model = activeModel;
            result.HoveredPart = hoveredPart;
            result.TooltipRect = tooltipRect;
            result.ActivatedPart = HandleTooltipPartInteraction(current, hoveredPart);
            return true;
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
                _stickyHoverAnchorRect = currentModel.AnchorRect;
                RefreshStickyHoverKeepAlive();
                return currentModel;
            }

            if (_stickyModel == null)
            {
                return null;
            }

            if ((hasMouse && IsPointerWithinRichHoverSurface(mousePosition)) ||
                DateTime.UtcNow <= _stickyHoverKeepAliveUtc)
            {
                return _stickyModel;
            }

            ClearRichState();
            return null;
        }

        private Vector2 ResolveTooltipViewportSize(Vector2 viewportSize)
        {
            if (IsUsableTooltipViewport(viewportSize))
            {
                _lastValidViewport = viewportSize;
                return viewportSize;
            }

            return _lastValidViewport;
        }

        private static bool IsUsableTooltipViewport(Vector2 viewportSize)
        {
            return viewportSize.x >= 64f && viewportSize.y >= 64f;
        }

        private static LanguageServiceHoverDisplayPart[] BuildFallbackParts(HoverTooltipRenderModel model)
        {
            return new[]
            {
                new LanguageServiceHoverDisplayPart
                {
                    Text = model != null && model.Response != null ? model.Response.SymbolDisplay ?? string.Empty : string.Empty,
                    Classification = string.Empty,
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

        private static string BuildMetaText(HoverTooltipRenderModel model, LanguageServiceHoverDisplayPart hoveredPart)
        {
            return model != null && model.BuildMetaText != null
                ? model.BuildMetaText(hoveredPart)
                : string.Empty;
        }

        private static string BuildDetailText(HoverTooltipRenderModel model, LanguageServiceHoverDisplayPart hoveredPart)
        {
            if (model == null)
            {
                return string.Empty;
            }

            var detail = model.BuildDocumentationText != null
                ? model.BuildDocumentationText(hoveredPart)
                : string.Empty;
            var supplemental = model.GetSupplementalSections != null
                ? FormatSupplementalSections(model.GetSupplementalSections(hoveredPart))
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

        private float LayoutTooltipParts(
            Rect bounds,
            LanguageServiceHoverDisplayPart[] parts,
            List<TooltipPartVisual> visuals,
            bool draw,
            HoverTooltipStyleSet styles)
        {
            var signatureStyle = styles.SignatureStyle ?? GUI.skin.label;
            var linkStyle = styles.LinkStyle ?? signatureStyle;
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

        private static LanguageServiceHoverDisplayPart FindHoveredTooltipPart(List<TooltipPartVisual> visuals, Vector2 mousePosition)
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

        private static Rect FindTooltipPartRect(List<TooltipPartVisual> visuals, LanguageServiceHoverDisplayPart hoveredPart)
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

        private static float BuildTooltipHeight(HoverTooltipStyleSet styles, float tooltipWidth, float pathHeight, float metaHeight, float signatureHeight, string detailText)
        {
            var detailHeight = string.IsNullOrEmpty(detailText) || styles.DetailStyle == null
                ? 0f
                : styles.DetailStyle.CalcHeight(new GUIContent(detailText), tooltipWidth - 16f);
            return Mathf.Min(360f, 14f + pathHeight + metaHeight + signatureHeight + (detailHeight > 0f ? detailHeight + 8f : 0f));
        }

        private Rect BuildTooltipRect(Rect anchorRect, Vector2 mousePosition, Vector2 viewportSize, float tooltipWidth, float height)
        {
            if (HasArea(anchorRect))
            {
                return ClampTooltipRect(BuildTooltipRectFromAnchor(anchorRect, viewportSize, tooltipWidth, height), viewportSize);
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
            return new Rect(
                tooltipRect.x + 8f,
                tooltipRect.y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f),
                tooltipRect.width - 16f,
                Mathf.Max(0f, tooltipRect.height - pathHeight - metaHeight - 14f));
        }

        private static Rect BuildTooltipDetailRect(Rect tooltipRect, float pathHeight, float metaHeight, float signatureHeight)
        {
            return new Rect(
                tooltipRect.x + 8f,
                tooltipRect.y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f) + signatureHeight + 6f,
                tooltipRect.width - 16f,
                Mathf.Max(0f, tooltipRect.height - pathHeight - metaHeight - signatureHeight - 20f));
        }

        private LanguageServiceHoverDisplayPart HandleTooltipPartInteraction(Event current, LanguageServiceHoverDisplayPart hoveredPart)
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

            var shouldActivate = !string.IsNullOrEmpty(partKey) &&
                string.Equals(_pressedTooltipPartKey, partKey, StringComparison.Ordinal);
            _pressedTooltipPartKey = string.Empty;
            if (!shouldActivate)
            {
                return null;
            }

            current.Use();
            return hoveredPart;
        }

        private static string FormatSupplementalSections(LanguageServiceHoverSection[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var text = !string.IsNullOrEmpty(section.Text)
                    ? section.Text
                    : FlattenDisplayParts(section.DisplayParts);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(section.Title))
                {
                    lines.Add(HumanizeSectionTitle(section.Title) + ": " + text);
                }
                else
                {
                    lines.Add(text);
                }
            }

            return lines.Count > 0
                ? string.Join(Environment.NewLine, lines.ToArray())
                : string.Empty;
        }

        private static string FlattenDisplayParts(LanguageServiceHoverDisplayPart[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                text += parts[i] != null ? parts[i].Text ?? string.Empty : string.Empty;
            }

            return text.Trim();
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

        private static string BuildTooltipPartKey(LanguageServiceHoverDisplayPart part)
        {
            return (part != null ? part.SymbolDisplay ?? string.Empty : string.Empty) +
                "|" + (part != null ? part.MetadataName ?? string.Empty : string.Empty) +
                "|" + (part != null ? part.DefinitionDocumentPath ?? string.Empty : string.Empty) +
                "|" + (part != null && part.DefinitionRange != null ? part.DefinitionRange.Start.ToString() : string.Empty);
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

        private sealed class TooltipPartVisual
        {
            public LanguageServiceHoverDisplayPart Part;
            public Rect Rect;
        }
    }
}
