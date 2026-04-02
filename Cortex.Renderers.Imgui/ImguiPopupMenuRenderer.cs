using System;
using System.Collections.Generic;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Rendering.RuntimeUi.PopupMenus;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiPopupMenuRenderer : IPopupMenuRenderer, IDisposable
    {
        private const float HoverAccentWidth = 3f;

        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;
        private readonly IWorkbenchFrameContext _frameContext;
        private readonly PopupThemeResources _themeResources = new PopupThemeResources();
        private readonly PopupMenuRuntimeState _runtimeState = new PopupMenuRuntimeState();

        public ImguiPopupMenuRenderer()
            : this(new ImguiModuleResources(), null, true)
        {
        }

        internal ImguiPopupMenuRenderer(ImguiModuleResources moduleResources, IWorkbenchFrameContext frameContext)
            : this(moduleResources, frameContext, false)
        {
        }

        private ImguiPopupMenuRenderer(ImguiModuleResources moduleResources, IWorkbenchFrameContext frameContext, bool ownsModuleResources)
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

        public void Reset()
        {
            PopupMenuInteractionController.Reset(_runtimeState);
        }

        public void QueueScrollDelta(float delta)
        {
            PopupMenuInteractionController.QueueScrollDelta(_runtimeState, delta);
        }

        public bool TryCapturePointerInput(RenderRect menuRect, RenderPoint localMouse)
        {
            var input = RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput(_frameContext.Snapshot, localMouse);
            var capture = PopupMenuInteractionController.TryCapturePointerInput(_runtimeState, menuRect, input);
            if (capture.ShouldConsumeInput)
            {
                _frameContext.ConsumeCurrentInput();
            }

            return capture.Captured;
        }

        public PopupMenuRenderResult Draw(
            RenderPoint position,
            RenderSize viewportSize,
            string headerText,
            IList<PopupMenuItemModel> items,
            RenderPoint localMouse,
            PopupMenuThemePalette theme)
        {
            EnsureTheme(theme);
            var result = new PopupMenuRenderResult();
            var safeItems = items ?? new PopupMenuItemModel[0];
            PopupMenuInteractionController.ResetForMenu(_runtimeState, headerText, safeItems);

            var layout = PopupMenuLayoutPlanner.BuildLayout(position, viewportSize, safeItems);
            var menuRect = layout.MenuRect;
            var drawLayout = PopupMenuLayoutPlanner.BuildDrawLayout(position, viewportSize, safeItems, _runtimeState.ScrollOffset, headerText);
            var unityMenuRect = ToRect(menuRect);
            var unityMouse = new Vector2(localMouse.X, localMouse.Y);
            result.MenuRect = menuRect;

            GUI.Box(unityMenuRect, GUIContent.none, _themeResources.MenuStyle);
            GUI.Label(ToRect(drawLayout.HeaderTextRect), headerText ?? string.Empty, _themeResources.HeaderStyle);

            var viewportRect = ToRect(drawLayout.ViewportRect);
            var input = RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput(_frameContext.Snapshot, localMouse);
            var preparedFrame = PopupMenuInteractionController.PrepareFrame(
                _runtimeState,
                input,
                layout.MaxScroll,
                RuntimeUiHitTest.Contains(menuRect, input.PointerPosition));
            var maxScroll = preparedFrame.MaxScroll;
            var hasScroll = preparedFrame.HasScroll;
            drawLayout = PopupMenuLayoutPlanner.BuildDrawLayout(position, viewportSize, safeItems, preparedFrame.ScrollOffset, headerText);

            var contentWidth = drawLayout.ContentWidth;
            var viewportMouse = new Vector2(unityMouse.x - viewportRect.x, unityMouse.y - viewportRect.y);
            var queuedDownViewportMouse = new Vector2(preparedFrame.QueuedPointerDownPosition.X - viewportRect.x, preparedFrame.QueuedPointerDownPosition.Y - viewportRect.y);
            var queuedUpViewportMouse = new Vector2(preparedFrame.QueuedPointerUpPosition.X - viewportRect.x, preparedFrame.QueuedPointerUpPosition.Y - viewportRect.y);
            GUI.BeginGroup(viewportRect);
            try
            {
                GUI.BeginGroup(new Rect(0f, 0f, contentWidth, viewportRect.height));
                try
                {
                    DrawItems(drawLayout.Items, viewportMouse, queuedDownViewportMouse, queuedUpViewportMouse, input, preparedFrame, ref result);
                }
                finally
                {
                    GUI.EndGroup();
                }

                if (hasScroll)
                {
                    DrawScrollChrome(input, drawLayout.ScrollChrome, maxScroll, drawLayout.ViewportRect.Height, queuedUpViewportMouse);
                }
            }
            finally
            {
                GUI.EndGroup();
            }

            var frameResult = PopupMenuInteractionController.CompleteFrame(_runtimeState, input, RuntimeUiHitTest.Contains(menuRect, input.PointerPosition));
            if (frameResult.ShouldClose)
            {
                result.ShouldClose = true;
            }
            return result;
        }

        public RenderRect PredictMenuRect(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items)
        {
            return PopupMenuLayoutPlanner.BuildLayout(position, viewportSize, items).MenuRect;
        }

        private void EnsureTheme(PopupMenuThemePalette theme)
        {
            var effectiveTheme = theme ?? new PopupMenuThemePalette();
            var themeKey = ImguiThemeUtility.BuildKey(
                effectiveTheme.BackgroundColor,
                effectiveTheme.BorderColor,
                effectiveTheme.TextColor,
                effectiveTheme.MutedTextColor,
                effectiveTheme.AccentColor,
                effectiveTheme.HoverFillColor,
                effectiveTheme.PressedFillColor);
            if (string.Equals(_themeResources.ThemeKey, themeKey, StringComparison.Ordinal) && _themeResources.MenuStyle != null)
            {
                return;
            }

            _themeResources.ThemeKey = themeKey;
            var backgroundColor = ImguiThemeUtility.ResolveColor(effectiveTheme.BackgroundColor, new Color(0.12f, 0.13f, 0.15f, 0.98f));
            var borderColor = ImguiThemeUtility.ResolveColor(effectiveTheme.BorderColor, new Color(0.24f, 0.27f, 0.31f, 1f));
            var textColor = ImguiThemeUtility.ResolveColor(effectiveTheme.TextColor, new Color(0.95f, 0.95f, 0.96f, 1f));
            var mutedTextColor = ImguiThemeUtility.ResolveColor(effectiveTheme.MutedTextColor, new Color(0.69f, 0.72f, 0.77f, 1f));
            var accentColor = ImguiThemeUtility.ResolveColor(effectiveTheme.AccentColor, new Color(0.31f, 0.54f, 0.84f, 1f));
            var hoverFillColor = ImguiThemeUtility.ResolveColor(effectiveTheme.HoverFillColor, new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f));
            var pressedFillColor = ImguiThemeUtility.ResolveColor(effectiveTheme.PressedFillColor, new Color(accentColor.r, accentColor.g, accentColor.b, 0.28f));

            var textureCache = _moduleResources.TextureCache;
            _themeResources.MenuStyle = new GUIStyle(GUI.skin.box);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_themeResources.MenuStyle, textureCache.GetFill(backgroundColor));
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.MenuStyle, textColor);
            _themeResources.MenuStyle.padding = new RectOffset(6, 6, 6, 6);
            _themeResources.MenuStyle.margin = new RectOffset(0, 0, 0, 0);
            _themeResources.MenuStyle.border = new RectOffset(1, 1, 1, 1);

            _themeResources.ButtonStyle = new GUIStyle(GUI.skin.box);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_themeResources.ButtonStyle, null);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.ButtonStyle, textColor);
            _themeResources.ButtonStyle.alignment = TextAnchor.MiddleLeft;
            _themeResources.ButtonStyle.padding = new RectOffset(8, 8, 3, 3);
            _themeResources.ButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            _themeResources.ButtonStyle.border = new RectOffset(0, 0, 0, 0);

            _themeResources.HeaderStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyTextColorToAllStates(_themeResources.HeaderStyle, mutedTextColor);
            _themeResources.HeaderStyle.fontStyle = FontStyle.Bold;
            _themeResources.HeaderStyle.wordWrap = false;

            _themeResources.BorderFill = textureCache.GetFill(borderColor);
            _themeResources.HoverFill = textureCache.GetFill(hoverFillColor);
            _themeResources.PressedFill = textureCache.GetFill(pressedFillColor);
            _themeResources.HoverAccentFill = textureCache.GetFill(accentColor);
        }

        private void DrawItems(
            IList<PopupMenuItemLayout> itemLayouts,
            Vector2 viewportMouse,
            Vector2 queuedDownViewportMouse,
            Vector2 queuedUpViewportMouse,
            RuntimeUiPointerFrameInput input,
            PopupMenuPreparedFrame preparedFrame,
            ref PopupMenuRenderResult result)
        {
            for (var i = 0; i < itemLayouts.Count; i++)
            {
                var itemLayout = itemLayouts[i];
                var item = itemLayout != null ? itemLayout.Item : null;
                if (item == null)
                {
                    continue;
                }

                if (item.IsSeparator)
                {
                    GUI.DrawTexture(ToRect(itemLayout.SeparatorRect), _themeResources.BorderFill);
                    continue;
                }

                if (item.IsSectionHeader)
                {
                    GUI.Label(ToRect(itemLayout.LabelRect), item.Label ?? string.Empty, _themeResources.HeaderStyle);
                    continue;
                }

                var itemRect = ToRect(itemLayout.Bounds);
                var isPointerOverItem = itemRect.Contains(viewportMouse);
                var isQueuedMouseDownOnItem = preparedFrame.HasQueuedPointerDown && preparedFrame.QueuedPointerDownButton == 0 && itemRect.Contains(queuedDownViewportMouse);
                var isQueuedMouseUpOnItem = preparedFrame.HasQueuedPointerUp && preparedFrame.QueuedPointerUpButton == 0 && itemRect.Contains(queuedUpViewportMouse);
                var commandId = item.CommandId ?? string.Empty;
                var interaction = PopupMenuInteractionController.EvaluateItemInteraction(
                    _runtimeState,
                    input,
                    commandId,
                    item.Enabled,
                    isPointerOverItem,
                    isQueuedMouseDownOnItem,
                    isQueuedMouseUpOnItem);

                var previousEnabled = GUI.enabled;
                GUI.enabled = item.Enabled;
                DrawItemBackground(itemRect, item.Enabled, isPointerOverItem, interaction.IsPressedVisual);
                if (interaction.ShouldClose)
                {
                    result.ActivatedCommandId = interaction.ActivatedCommandId ?? commandId;
                    result.ShouldClose = true;
                }

                GUI.enabled = previousEnabled;
                GUI.Label(ToRect(itemLayout.LabelRect), item.Label ?? string.Empty);
                if (!string.IsNullOrEmpty(item.ShortcutText))
                {
                    GUI.Label(ToRect(itemLayout.ShortcutRect), item.ShortcutText, _themeResources.HeaderStyle);
                }
            }
        }

        private void DrawScrollChrome(RuntimeUiPointerFrameInput input, PopupMenuScrollChromeLayout chromeLayout, float maxScroll, float viewportHeight, Vector2 queuedUpViewportMouse)
        {
            if (chromeLayout == null)
            {
                return;
            }

            GUI.Box(ToRect(chromeLayout.ChromeRect), GUIContent.none);
            GUI.Box(ToRect(chromeLayout.UpButtonRect), "^");
            GUI.Box(ToRect(chromeLayout.DownButtonRect), "v");
            GUI.Box(ToRect(chromeLayout.ThumbRect), GUIContent.none);

            if (PopupMenuInteractionController.HandleScrollChromeInteraction(
                _runtimeState,
                input,
                maxScroll,
                chromeLayout.UpButtonRect,
                chromeLayout.DownButtonRect,
                chromeLayout.TrackRect,
                chromeLayout.ThumbRect,
                viewportHeight,
                new RenderPoint(queuedUpViewportMouse.x, queuedUpViewportMouse.y)) &&
                input.EventKind == RuntimeUiPointerEventKind.Up &&
                input.PointerButton == 0)
            {
                _frameContext.ConsumeCurrentInput();
            }
        }

        private void DrawItemBackground(Rect itemRect, bool enabled, bool hovered, bool pressed)
        {
            GUI.Box(itemRect, GUIContent.none, _themeResources.ButtonStyle);
            if (!enabled || !hovered)
            {
                return;
            }

            GUI.DrawTexture(itemRect, pressed ? _themeResources.PressedFill : _themeResources.HoverFill);
            GUI.DrawTexture(new Rect(itemRect.x, itemRect.y, HoverAccentWidth, itemRect.height), _themeResources.HoverAccentFill);
        }

        private static Rect ToRect(RenderRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private sealed class PopupThemeResources
        {
            public string ThemeKey = string.Empty;
            public GUIStyle MenuStyle;
            public GUIStyle ButtonStyle;
            public GUIStyle HeaderStyle;
            public Texture2D BorderFill;
            public Texture2D HoverFill;
            public Texture2D PressedFill;
            public Texture2D HoverAccentFill;
        }
    }
}
