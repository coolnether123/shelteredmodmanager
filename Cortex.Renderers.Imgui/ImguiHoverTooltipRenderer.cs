using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Rendering.RuntimeUi.Tooltips;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiHoverTooltipRenderer : IHoverTooltipRenderer, IDisposable
    {
        private const float DefaultTooltipWidth = 420f;

        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;
        private readonly IWorkbenchFrameContext _frameContext;
        private readonly HoverThemeResources _themeResources = new HoverThemeResources();
        private readonly HoverTooltipRuntimeState _runtimeState = new HoverTooltipRuntimeState();

        private Rect _textTooltipAnchorRect = new Rect(0f, 0f, 0f, 0f);
        private string _textTooltipText = string.Empty;

        public ImguiHoverTooltipRenderer()
            : this(new ImguiModuleResources(), null, true)
        {
        }

        internal ImguiHoverTooltipRenderer(ImguiModuleResources moduleResources, IWorkbenchFrameContext frameContext)
            : this(moduleResources, frameContext, false)
        {
        }

        private ImguiHoverTooltipRenderer(ImguiModuleResources moduleResources, IWorkbenchFrameContext frameContext, bool ownsModuleResources)
        {
            _moduleResources = moduleResources ?? new ImguiModuleResources();
            _ownsModuleResources = ownsModuleResources;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
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

            var frameSnapshot = _frameContext.Snapshot;
            if (!frameSnapshot.HasCurrentEvent && (frameSnapshot.PointerPosition.X <= 0f && frameSnapshot.PointerPosition.Y <= 0f))
            {
                return;
            }

            var viewportSize = frameSnapshot.ViewportSize.Width > 0f && frameSnapshot.ViewportSize.Height > 0f
                ? frameSnapshot.ViewportSize
                : new RenderSize(1920f, 1080f);
            var maxWidth = Mathf.Min(620f, Mathf.Max(260f, viewportSize.Width - 24f));
            var content = new GUIContent(_textTooltipText);
            var width = Mathf.Min(maxWidth, Mathf.Max(240f, _themeResources.ContainerStyle.CalcSize(content).x + 16f));
            var height = Mathf.Max(30f, _themeResources.ContainerStyle.CalcHeight(content, width) + 2f);
            var rect = ToRect(HoverTooltipPlacement.BuildTextRect(
                ToRenderRect(_textTooltipAnchorRect),
                frameSnapshot.CurrentMousePosition,
                viewportSize,
                width,
                height));
            GUI.Box(rect, content, _themeResources.ContainerStyle);
            ImguiStyleUtil.DrawBorder(rect, _themeResources.BorderFill, 1f);
        }

        public void ClearRichState()
        {
            HoverTooltipInteractionController.Reset(_runtimeState);
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
            if (_themeResources.ContainerStyle == null)
            {
                result.HiddenReason = "theme-uninitialized";
                return false;
            }

            var frameSnapshot = _frameContext.Snapshot;
            var pointerInput = RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput(frameSnapshot, mousePosition);
            var plan = HoverTooltipLayoutPlanner.BuildLayout(
                _runtimeState,
                currentModel,
                mousePosition,
                viewportSize,
                frameSnapshot.ViewportSize,
                hasMouse,
                DateTime.UtcNow,
                tooltipWidth,
                DefaultTooltipWidth,
                frameSnapshot.AllowsVisualRefresh,
                new ImguiHoverTooltipLayoutMeasurer(_themeResources));
            if (!plan.Visible)
            {
                result.HiddenReason = plan.HiddenReason;
                return false;
            }

            var activeModel = plan.Model;
            var hoveredPart = plan.HoveredPart;

            GUI.Box(ToRect(plan.TooltipRect), GUIContent.none, _themeResources.ContainerStyle);
            ImguiStyleUtil.DrawBorder(ToRect(plan.TooltipRect), _themeResources.BorderFill, 1f);
            if (!string.IsNullOrEmpty(activeModel.QualifiedPath) && _themeResources.PathStyle != null)
            {
                GUI.Label(ToRect(plan.PathRect), activeModel.QualifiedPath, _themeResources.PathStyle);
            }

            if (!string.IsNullOrEmpty(plan.MetaText) && _themeResources.MetaStyle != null)
            {
                GUI.Label(ToRect(plan.MetaRect), plan.MetaText, _themeResources.MetaStyle);
            }

            DrawHoveredPart(plan.PartLayouts, hoveredPart);
            DrawTooltipParts(plan.PartLayouts);
            if (!string.IsNullOrEmpty(plan.DetailText) && _themeResources.DetailStyle != null)
            {
                GUI.Label(ToRect(plan.DetailRect), plan.DetailText, _themeResources.DetailStyle);
            }

            if (object.ReferenceEquals(activeModel, currentModel) ||
                (hasMouse && HoverTooltipInteractionController.IsPointerWithinRichHoverSurface(_runtimeState, mousePosition)))
            {
                HoverTooltipInteractionController.RefreshKeepAlive(_runtimeState, DateTime.UtcNow);
            }

            result.Visible = true;
            result.Model = activeModel;
            result.HoveredPart = hoveredPart;
            result.TooltipRect = plan.TooltipRect;
            result.ActivatedPart = HoverTooltipInteractionController.HandlePartPointerInput(_runtimeState, pointerInput, hoveredPart);
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
            _themeResources.MetaStyle.clipping = TextClipping.Clip;

            _themeResources.PathStyle = new GUIStyle(_themeResources.MetaStyle);
            _themeResources.PathStyle.wordWrap = false;

            _themeResources.SignatureStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.SignatureStyle, textColor);
            _themeResources.SignatureStyle.wordWrap = false;
            _themeResources.SignatureStyle.alignment = TextAnchor.UpperLeft;
            _themeResources.SignatureStyle.fontStyle = FontStyle.Bold;
            _themeResources.SignatureStyle.clipping = TextClipping.Clip;

            _themeResources.LinkStyle = new GUIStyle(_themeResources.SignatureStyle);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.LinkStyle, accentColor);

            _themeResources.DetailStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.DetailStyle, textColor);
            _themeResources.DetailStyle.wordWrap = true;
            _themeResources.DetailStyle.alignment = TextAnchor.UpperLeft;
            _themeResources.DetailStyle.clipping = TextClipping.Clip;
        }

        private void DrawHoveredPart(IList<HoverTooltipPartLayout> visuals, EditorHoverContentPart hoveredPart)
        {
            if (hoveredPart == null || !hoveredPart.IsInteractive || _themeResources.HoverFill == null)
            {
                return;
            }

            var hoveredRect = HoverTooltipInteractionController.FindPartBounds(visuals, hoveredPart);
            if (hoveredRect.Width <= 0f || hoveredRect.Height <= 0f)
            {
                return;
            }

            var hoveredUnityRect = ToRect(hoveredRect);
            GUI.DrawTexture(hoveredUnityRect, _themeResources.HoverFill);
            if (_themeResources.LinkStyle != null)
            {
                GUI.Label(hoveredUnityRect, hoveredPart.Text ?? string.Empty, _themeResources.LinkStyle);
            }

            if (_themeResources.UnderlineFill != null)
            {
                GUI.DrawTexture(new Rect(hoveredUnityRect.x, hoveredUnityRect.yMax - 1f, hoveredUnityRect.width, 1f), _themeResources.UnderlineFill);
            }
        }

        private void DrawTooltipParts(IList<HoverTooltipPartLayout> layouts)
        {
            if (layouts == null)
            {
                return;
            }

            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                var part = layout != null ? layout.Part : null;
                if (part == null)
                {
                    continue;
                }

                var style = part.IsInteractive ? _themeResources.LinkStyle : _themeResources.SignatureStyle;
                GUI.Label(ToRect(layout.Bounds), part.Text ?? string.Empty, style);
            }
        }

        private static Rect ToRect(RenderRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static RenderRect ToRenderRect(Rect rect)
        {
            return new RenderRect(rect.x, rect.y, rect.width, rect.height);
        }

        private sealed class ImguiHoverTooltipLayoutMeasurer : IHoverTooltipLayoutMeasurer
        {
            private readonly HoverThemeResources _themeResources;

            public ImguiHoverTooltipLayoutMeasurer(HoverThemeResources themeResources)
            {
                _themeResources = themeResources;
            }

            public float MeasurePartWidth(EditorHoverContentPart part)
            {
                var text = part != null ? part.Text ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    return 0f;
                }

                var style = part != null && part.IsInteractive
                    ? (_themeResources.LinkStyle ?? _themeResources.SignatureStyle ?? GUI.skin.label)
                    : (_themeResources.SignatureStyle ?? GUI.skin.label);
                return Mathf.Max(2f, style.CalcSize(new GUIContent(text)).x);
            }

            public float MeasurePathHeight(string text, float width)
            {
                return MeasureHeight(_themeResources.PathStyle, text, width, 16f);
            }

            public float MeasureMetaHeight(string text, float width)
            {
                return MeasureHeight(_themeResources.MetaStyle, text, width, 16f);
            }

            public float MeasureDetailHeight(string text, float width)
            {
                return MeasureHeight(_themeResources.DetailStyle, text, width, 0f);
            }

            public float MeasureLineHeight()
            {
                var style = _themeResources.SignatureStyle ?? GUI.skin.label;
                return Mathf.Max(18f, style.CalcSize(new GUIContent("Ag")).y + 2f);
            }

            private static float MeasureHeight(GUIStyle style, string text, float width, float minimum)
            {
                if (style == null || string.IsNullOrEmpty(text))
                {
                    return 0f;
                }

                return Mathf.Max(minimum, style.CalcHeight(new GUIContent(text), width));
            }
        }

        private sealed class HoverThemeResources
        {
            public string ThemeKey = string.Empty;
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
    }
}
