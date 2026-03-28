using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiHoverTooltipRenderer : IHoverTooltipRenderer, IDisposable
    {
        private const float DefaultTooltipWidth = 420f;
        private static readonly HoverTooltipPlacementOptions TooltipPlacementOptions = HoverTooltipPlacement.CreateAnchoredOptions(4f);

        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;
        private readonly HoverThemeResources _themeResources = new HoverThemeResources();
        private readonly HoverTooltipPlacementState _tooltipPlacementState = new HoverTooltipPlacementState();

        private Rect _textTooltipAnchorRect = new Rect(0f, 0f, 0f, 0f);
        private string _textTooltipText = string.Empty;
        private Vector2 _lastValidViewport = Vector2.zero;
        private string _pressedTooltipPartKey = string.Empty;

        public ImguiHoverTooltipRenderer()
            : this(new ImguiModuleResources(), true)
        {
        }

        internal ImguiHoverTooltipRenderer(ImguiModuleResources moduleResources)
            : this(moduleResources, false)
        {
        }

        private ImguiHoverTooltipRenderer(ImguiModuleResources moduleResources, bool ownsModuleResources)
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
            if (string.IsNullOrEmpty(_textTooltipText) || _themeResources.ContainerStyle == null)
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
            var width = Mathf.Min(maxWidth, Mathf.Max(240f, _themeResources.ContainerStyle.CalcSize(content).x + 16f));
            var height = Mathf.Max(30f, _themeResources.ContainerStyle.CalcHeight(content, width) + 2f);
            var rect = ToRect(HoverTooltipPlacement.BuildTextRect(
                ToRenderRect(_textTooltipAnchorRect),
                new RenderPoint(current.mousePosition.x, current.mousePosition.y),
                new RenderSize(Screen.width, Screen.height),
                width,
                height));
            GUI.Box(rect, content, _themeResources.ContainerStyle);
            ImguiStyleUtil.DrawBorder(rect, _themeResources.BorderFill, 1f);
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
            if (currentModel == null)
            {
                result.HiddenReason = "model-null";
                return false;
            }

            var mouse = new Vector2(mousePosition.X, mousePosition.Y);
            var effectiveViewport = ResolveTooltipViewportSize(viewportSize);
            if (!IsUsableTooltipViewport(effectiveViewport) || _themeResources.ContainerStyle == null)
            {
                result.HiddenReason = !IsUsableTooltipViewport(effectiveViewport)
                    ? "viewport-invalid|" + effectiveViewport.x.ToString("F1") + "x" + effectiveViewport.y.ToString("F1") +
                      "|screen=" + Screen.width.ToString() + "x" + Screen.height.ToString()
                    : "theme-uninitialized";
                return false;
            }

            var signatureParts = currentModel.SignatureParts != null && currentModel.SignatureParts.Length > 0
                ? currentModel.SignatureParts
                : BuildFallbackParts(currentModel);
            var effectiveWidth = ResolveTooltipWidth(signatureParts, tooltipWidth);
            var signatureHeight = LayoutTooltipParts(new Rect(0f, 0f, effectiveWidth - 16f, 0f), signatureParts, null, false);
            if (signatureHeight <= 0f)
            {
                result.HiddenReason = "signature-height-zero";
                ClearRichState();
                return false;
            }

            var tooltipSpawnX = HoverTooltipPlacement.ResolveSpawnX(_tooltipPlacementState, currentModel.Key ?? string.Empty, mouse.x);
            var tooltipRect = BuildTooltipRect(
                currentModel.AnchorRect,
                mouse,
                tooltipSpawnX,
                effectiveViewport,
                effectiveWidth,
                Mathf.Max(30f, signatureHeight + 16f));

            var partVisuals = new List<TooltipPartVisual>();
            var signatureRect = BuildSignatureRect(tooltipRect);
            LayoutTooltipParts(signatureRect, signatureParts, partVisuals, false);
            var hoveredPart = FindHoveredTooltipPart(partVisuals, mouse);

            GUI.Box(tooltipRect, GUIContent.none, _themeResources.ContainerStyle);
            ImguiStyleUtil.DrawBorder(tooltipRect, _themeResources.BorderFill, 1f);

            partVisuals.Clear();
            LayoutTooltipParts(signatureRect, signatureParts, partVisuals, true);
            hoveredPart = FindHoveredTooltipPart(partVisuals, mouse);
            DrawHoveredPart(partVisuals, hoveredPart);

            result.Visible = true;
            result.Model = currentModel;
            result.HoveredPart = hoveredPart;
            result.TooltipRect = ToRenderRect(tooltipRect);
            result.ActivatedPart = HandleTooltipPartInteraction(Event.current, hoveredPart);
            return true;
        }

        private void EnsureTheme(HoverTooltipThemePalette theme)
        {
            var effectiveTheme = theme ?? new HoverTooltipThemePalette();
            var themeKey = ImguiThemeUtility.BuildKey(
                effectiveTheme.BackgroundColor,
                effectiveTheme.BorderColor,
                effectiveTheme.TextColor,
                effectiveTheme.MutedTextColor,
                effectiveTheme.AccentColor,
                effectiveTheme.HoverFillColor);
            if (string.Equals(_themeResources.ThemeKey, themeKey, StringComparison.Ordinal) && _themeResources.ContainerStyle != null)
            {
                return;
            }

            _themeResources.ThemeKey = themeKey;
            var backgroundColor = ImguiThemeUtility.ResolveColor(effectiveTheme.BackgroundColor, new Color(0.11f, 0.12f, 0.14f, 0.98f));
            var borderColor = ImguiThemeUtility.ResolveColor(effectiveTheme.BorderColor, new Color(0.26f, 0.31f, 0.37f, 1f));
            var textColor = ImguiThemeUtility.ResolveColor(effectiveTheme.TextColor, new Color(0.94f, 0.94f, 0.95f, 1f));
            var mutedTextColor = ImguiThemeUtility.ResolveColor(effectiveTheme.MutedTextColor, new Color(0.68f, 0.72f, 0.77f, 1f));
            var accentColor = ImguiThemeUtility.ResolveColor(effectiveTheme.AccentColor, new Color(0.31f, 0.54f, 0.84f, 1f));
            var hoverFillColor = ImguiThemeUtility.ResolveColor(effectiveTheme.HoverFillColor, new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f));

            var textureCache = _moduleResources.TextureCache;
            _themeResources.HoverFill = textureCache.GetFill(hoverFillColor);
            _themeResources.UnderlineFill = textureCache.GetFill(accentColor);
            _themeResources.BorderFill = textureCache.GetFill(borderColor);

            _themeResources.ContainerStyle = new GUIStyle(GUI.skin.box);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_themeResources.ContainerStyle, textureCache.GetFill(backgroundColor));
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.ContainerStyle, textColor);
            _themeResources.ContainerStyle.alignment = TextAnchor.UpperLeft;
            _themeResources.ContainerStyle.wordWrap = true;
            _themeResources.ContainerStyle.padding = new RectOffset(8, 8, 8, 8);
            _themeResources.ContainerStyle.margin = new RectOffset(0, 0, 0, 0);
            _themeResources.ContainerStyle.border = new RectOffset(1, 1, 1, 1);

            _themeResources.MetaStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.MetaStyle, mutedTextColor);
            _themeResources.MetaStyle.wordWrap = true;
            _themeResources.MetaStyle.alignment = TextAnchor.UpperLeft;

            _themeResources.SignatureStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.SignatureStyle, textColor);
            _themeResources.SignatureStyle.wordWrap = false;
            _themeResources.SignatureStyle.alignment = TextAnchor.UpperLeft;
            _themeResources.SignatureStyle.fontStyle = FontStyle.Bold;

            _themeResources.LinkStyle = new GUIStyle(_themeResources.SignatureStyle);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.LinkStyle, accentColor);
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

        private float ResolveTooltipWidth(EditorHoverContentPart[] parts, float tooltipWidth)
        {
            var maxWidth = tooltipWidth > 0f ? tooltipWidth : DefaultTooltipWidth;
            var signatureStyle = _themeResources.SignatureStyle ?? GUI.skin.label;
            var linkStyle = _themeResources.LinkStyle ?? signatureStyle;
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

            return Mathf.Min(maxWidth, Mathf.Max(180f, width + 20f));
        }

        private float LayoutTooltipParts(Rect bounds, EditorHoverContentPart[] parts, List<TooltipPartVisual> visuals, bool draw)
        {
            var signatureStyle = _themeResources.SignatureStyle ?? GUI.skin.label;
            var linkStyle = _themeResources.LinkStyle ?? signatureStyle;
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

        private void DrawHoveredPart(List<TooltipPartVisual> visuals, EditorHoverContentPart hoveredPart)
        {
            if (hoveredPart == null || !hoveredPart.IsInteractive || _themeResources.HoverFill == null)
            {
                return;
            }

            var hoveredRect = FindTooltipPartRect(visuals, hoveredPart);
            if (hoveredRect.width <= 0f || hoveredRect.height <= 0f)
            {
                return;
            }

            GUI.DrawTexture(hoveredRect, _themeResources.HoverFill);
            if (_themeResources.LinkStyle != null)
            {
                GUI.Label(hoveredRect, hoveredPart.Text ?? string.Empty, _themeResources.LinkStyle);
            }

            if (_themeResources.UnderlineFill != null)
            {
                GUI.DrawTexture(new Rect(hoveredRect.x, hoveredRect.yMax - 1f, hoveredRect.width, 1f), _themeResources.UnderlineFill);
            }
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

        private static Rect BuildSignatureRect(Rect tooltipRect)
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

        private static string BuildTooltipPartKey(EditorHoverContentPart part)
        {
            var navigationTarget = part != null ? part.NavigationTarget : null;
            return (part != null ? part.Text ?? string.Empty : string.Empty) + "|" +
                (navigationTarget != null ? navigationTarget.MetadataName ?? string.Empty : string.Empty) + "|" +
                (navigationTarget != null ? navigationTarget.DefinitionDocumentPath ?? string.Empty : string.Empty);
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

        private sealed class HoverThemeResources
        {
            public string ThemeKey = string.Empty;
            public GUIStyle ContainerStyle;
            public GUIStyle MetaStyle;
            public GUIStyle SignatureStyle;
            public GUIStyle LinkStyle;
            public Texture2D HoverFill;
            public Texture2D UnderlineFill;
            public Texture2D BorderFill;
        }
    }
}
