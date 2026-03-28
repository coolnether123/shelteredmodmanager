using System;
using System.Collections.Generic;
using System.Text;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiPopupMenuRenderer : IPopupMenuRenderer, IDisposable
    {
        private const float HeaderHeight = 28f;
        private const float ItemHeight = 24f;
        private const float SeparatorHeight = 8f;
        private const float ScrollWheelStep = 18f;
        private const float RawAxisScrollScale = 10f;
        private const float ScrollButtonHeight = 18f;
        private const float ScrollChromeWidth = 18f;
        private const float HorizontalPadding = 8f;
        private const float VerticalPadding = 6f;
        private const float HoverAccentWidth = 3f;

        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;
        private readonly PopupThemeResources _themeResources = new PopupThemeResources();

        private Vector2 _scrollPosition = new Vector2(0f, 0f);
        private string _scrollKey = string.Empty;
        private float _pendingScrollDelta;
        private string _pressedCommandId = string.Empty;
        private int _lastRawScrollFrame = -1;
        private bool _queuedMouseDown;
        private bool _queuedMouseUp;
        private bool _queuedOutsideClickClose;
        private int _queuedMouseDownButton = -1;
        private int _queuedMouseUpButton = -1;
        private Vector2 _queuedMouseDownPosition = Vector2.zero;
        private Vector2 _queuedMouseUpPosition = Vector2.zero;

        public ImguiPopupMenuRenderer()
            : this(new ImguiModuleResources(), true)
        {
        }

        internal ImguiPopupMenuRenderer(ImguiModuleResources moduleResources)
            : this(moduleResources, false)
        {
        }

        private ImguiPopupMenuRenderer(ImguiModuleResources moduleResources, bool ownsModuleResources)
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

        public void Reset()
        {
            _scrollPosition = new Vector2(0f, 0f);
            _scrollKey = string.Empty;
            _pendingScrollDelta = 0f;
            _pressedCommandId = string.Empty;
            _lastRawScrollFrame = -1;
            ClearQueuedPointerState();
        }

        public void QueueScrollDelta(float delta)
        {
            _pendingScrollDelta += delta * ScrollWheelStep;
        }

        public bool TryCapturePointerInput(RenderRect menuRect, RenderPoint localMouse)
        {
            var current = Event.current;
            if (current == null)
            {
                return false;
            }

            var unityMenuRect = ToRect(menuRect);
            var unityMouse = new Vector2(localMouse.X, localMouse.Y);
            var isInsideMenu = unityMenuRect.Contains(unityMouse);
            if (current.type == EventType.MouseDown)
            {
                if (isInsideMenu)
                {
                    QueuePointerDown(current.button, unityMouse);
                    current.Use();
                    return true;
                }

                _queuedOutsideClickClose = true;
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseUp)
            {
                if (isInsideMenu)
                {
                    QueuePointerUp(current.button, unityMouse);
                    current.Use();
                    return true;
                }

                current.Use();
                return true;
            }

            if (!isInsideMenu)
            {
                return false;
            }

            if (current.type == EventType.ScrollWheel)
            {
                QueueScrollDelta(current.delta.y);
                current.Use();
                return true;
            }

            if (Time.frameCount == _lastRawScrollFrame)
            {
                return false;
            }

            var rawAxis = ReadRawScrollAxis();
            if (Mathf.Abs(rawAxis) <= 0.0001f)
            {
                return false;
            }

            _lastRawScrollFrame = Time.frameCount;
            QueueScrollDelta(-rawAxis * RawAxisScrollScale);
            return true;
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
            ResetScrollStateForMenu(headerText, safeItems);

            float contentHeight;
            var menuRect = BuildMenuRect(position, viewportSize, safeItems, out contentHeight);
            var unityMenuRect = ToRect(menuRect);
            var unityMouse = new Vector2(localMouse.X, localMouse.Y);
            result.MenuRect = menuRect;

            GUI.Box(unityMenuRect, GUIContent.none, _themeResources.MenuStyle);
            GUI.Label(
                new Rect(unityMenuRect.x + HorizontalPadding, unityMenuRect.y + VerticalPadding, unityMenuRect.width - (HorizontalPadding * 2f), 18f),
                headerText ?? string.Empty,
                _themeResources.HeaderStyle);

            var viewportRect = new Rect(
                unityMenuRect.x + 4f,
                unityMenuRect.y + HeaderHeight,
                Mathf.Max(0f, unityMenuRect.width - 8f),
                Mathf.Max(0f, unityMenuRect.height - HeaderHeight - VerticalPadding));
            var maxScroll = Mathf.Max(0f, contentHeight - viewportRect.height);
            var hasScroll = maxScroll > 0f;
            QueueDrawPhaseScroll(unityMenuRect, unityMouse, maxScroll);
            _scrollPosition.y = ClampScrollOffset(_scrollPosition.y + _pendingScrollDelta, maxScroll);
            _pendingScrollDelta = 0f;

            var current = Event.current;
            if (ShouldHandleScrollWheel(current, viewportRect, unityMouse, maxScroll))
            {
                _scrollPosition.y = ClampScrollOffset(_scrollPosition.y + (current.delta.y * ScrollWheelStep), maxScroll);
                current.Use();
            }

            var contentWidth = Mathf.Max(0f, viewportRect.width - (hasScroll ? ScrollChromeWidth : 0f));
            var viewportMouse = new Vector2(unityMouse.x - viewportRect.x, unityMouse.y - viewportRect.y);
            var queuedDownViewportMouse = new Vector2(_queuedMouseDownPosition.x - viewportRect.x, _queuedMouseDownPosition.y - viewportRect.y);
            var queuedUpViewportMouse = new Vector2(_queuedMouseUpPosition.x - viewportRect.x, _queuedMouseUpPosition.y - viewportRect.y);
            GUI.BeginGroup(viewportRect);
            try
            {
                GUI.BeginGroup(new Rect(0f, 0f, contentWidth, viewportRect.height));
                try
                {
                    DrawItems(safeItems, contentWidth, viewportMouse, queuedDownViewportMouse, queuedUpViewportMouse, current, ref result);
                }
                finally
                {
                    GUI.EndGroup();
                }

                if (hasScroll)
                {
                    DrawScrollChrome(current, viewportRect, viewportMouse, contentWidth, maxScroll, queuedUpViewportMouse);
                }
            }
            finally
            {
                GUI.EndGroup();
            }

            if (_queuedOutsideClickClose)
            {
                result.ShouldClose = true;
            }

            if (current != null && current.type == EventType.MouseDown && !unityMenuRect.Contains(unityMouse))
            {
                result.ShouldClose = true;
            }

            ClearQueuedPointerState();
            return result;
        }

        public RenderRect PredictMenuRect(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items)
        {
            float contentHeight;
            return BuildMenuRect(position, viewportSize, items ?? new PopupMenuItemModel[0], out contentHeight);
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
            IList<PopupMenuItemModel> items,
            float contentWidth,
            Vector2 viewportMouse,
            Vector2 queuedDownViewportMouse,
            Vector2 queuedUpViewportMouse,
            Event current,
            ref PopupMenuRenderResult result)
        {
            var y = -_scrollPosition.y;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.IsSeparator)
                {
                    GUI.DrawTexture(new Rect(HorizontalPadding, y + 3f, contentWidth - (HorizontalPadding * 2f), 1f), _themeResources.BorderFill);
                    y += SeparatorHeight;
                    continue;
                }

                if (item.IsSectionHeader)
                {
                    GUI.Label(new Rect(10f, y + 3f, contentWidth - 20f, 18f), item.Label ?? string.Empty, _themeResources.HeaderStyle);
                    y += ItemHeight;
                    continue;
                }

                var itemRect = new Rect(6f, y, contentWidth - 12f, ItemHeight);
                var isPointerOverItem = itemRect.Contains(viewportMouse);
                var isQueuedMouseDownOnItem = _queuedMouseDown && _queuedMouseDownButton == 0 && itemRect.Contains(queuedDownViewportMouse);
                var isQueuedMouseUpOnItem = _queuedMouseUp && _queuedMouseUpButton == 0 && itemRect.Contains(queuedUpViewportMouse);
                var commandId = item.CommandId ?? string.Empty;
                var isPressedVisual = item.Enabled &&
                    (!string.IsNullOrEmpty(_pressedCommandId) && string.Equals(_pressedCommandId, commandId, StringComparison.Ordinal));

                var previousEnabled = GUI.enabled;
                GUI.enabled = item.Enabled;
                DrawItemBackground(itemRect, item.Enabled, isPointerOverItem, isPressedVisual);

                if (item.Enabled && ((current != null && current.type == EventType.MouseDown && current.button == 0 && isPointerOverItem) || isQueuedMouseDownOnItem))
                {
                    _pressedCommandId = commandId;
                    if (current != null && current.type == EventType.MouseDown && current.button == 0 && isPointerOverItem)
                    {
                        current.Use();
                    }
                }

                var canActivateFromRelease = string.IsNullOrEmpty(_pressedCommandId) ||
                    string.Equals(_pressedCommandId, commandId, StringComparison.Ordinal);
                if (item.Enabled &&
                    (((current != null && current.type == EventType.MouseUp && current.button == 0 && isPointerOverItem) || isQueuedMouseUpOnItem) && canActivateFromRelease))
                {
                    result.ActivatedCommandId = commandId;
                    result.ShouldClose = true;
                    _pressedCommandId = string.Empty;
                    if (current != null && current.type == EventType.MouseUp && current.button == 0 && isPointerOverItem)
                    {
                        current.Use();
                    }
                }

                if ((current != null && current.type == EventType.MouseUp && current.button == 0) || (_queuedMouseUp && _queuedMouseUpButton == 0))
                {
                    _pressedCommandId = string.Empty;
                }

                GUI.enabled = previousEnabled;
                GUI.Label(new Rect(itemRect.x + 8f, itemRect.y + 3f, itemRect.width - 70f, 18f), item.Label ?? string.Empty);
                if (!string.IsNullOrEmpty(item.ShortcutText))
                {
                    var shortcutRect = new Rect(itemRect.xMax - 78f, itemRect.y + 3f, 70f, 18f);
                    GUI.Label(shortcutRect, item.ShortcutText, _themeResources.HeaderStyle);
                }

                y += ItemHeight + 2f;
            }
        }

        private void DrawScrollChrome(Event current, Rect viewportRect, Vector2 viewportMouse, float contentWidth, float maxScroll, Vector2 queuedUpViewportMouse)
        {
            var chromeRect = new Rect(contentWidth, 0f, ScrollChromeWidth, viewportRect.height);
            var upRect = new Rect(chromeRect.x, chromeRect.y, chromeRect.width, ScrollButtonHeight);
            var downRect = new Rect(chromeRect.x, chromeRect.yMax - ScrollButtonHeight, chromeRect.width, ScrollButtonHeight);
            var trackRect = new Rect(chromeRect.x, upRect.yMax, chromeRect.width, Mathf.Max(0f, chromeRect.height - (ScrollButtonHeight * 2f)));
            var thumbHeight = Mathf.Max(24f, trackRect.height * Mathf.Clamp01(viewportRect.height / Mathf.Max(viewportRect.height, viewportRect.height + maxScroll)));
            var thumbTravel = Mathf.Max(0f, trackRect.height - thumbHeight);
            var thumbY = thumbTravel <= 0f
                ? trackRect.y
                : trackRect.y + ((_scrollPosition.y / Mathf.Max(1f, maxScroll)) * thumbTravel);
            var thumbRect = new Rect(trackRect.x + 2f, thumbY, Mathf.Max(0f, trackRect.width - 4f), thumbHeight);

            GUI.Box(chromeRect, GUIContent.none);
            GUI.Box(upRect, "^");
            GUI.Box(downRect, "v");
            GUI.Box(thumbRect, GUIContent.none);

            var hasCurrentMouseUp = current != null && current.type == EventType.MouseUp && current.button == 0;
            var hasQueuedMouseUp = _queuedMouseUp && _queuedMouseUpButton == 0;
            if (!hasCurrentMouseUp && !hasQueuedMouseUp)
            {
                return;
            }

            var effectiveMouse = hasQueuedMouseUp ? queuedUpViewportMouse : viewportMouse;
            if (upRect.Contains(effectiveMouse))
            {
                ApplyScrollStep(-ScrollWheelStep * 4f, maxScroll);
                if (hasCurrentMouseUp)
                {
                    current.Use();
                }
                return;
            }

            if (downRect.Contains(effectiveMouse))
            {
                ApplyScrollStep(ScrollWheelStep * 4f, maxScroll);
                if (hasCurrentMouseUp)
                {
                    current.Use();
                }
                return;
            }

            if (!trackRect.Contains(effectiveMouse))
            {
                return;
            }

            if (effectiveMouse.y < thumbRect.y)
            {
                ApplyScrollStep(-viewportRect.height * 0.75f, maxScroll);
                if (hasCurrentMouseUp)
                {
                    current.Use();
                }
                return;
            }

            if (effectiveMouse.y > thumbRect.yMax)
            {
                ApplyScrollStep(viewportRect.height * 0.75f, maxScroll);
                if (hasCurrentMouseUp)
                {
                    current.Use();
                }
            }
        }

        private void ApplyScrollStep(float delta, float maxScroll)
        {
            _scrollPosition.y = ClampScrollOffset(_scrollPosition.y + delta, maxScroll);
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

        private static bool ShouldHandleScrollWheel(Event current, Rect viewportRect, Vector2 localMouse, float maxScroll)
        {
            return current != null &&
                current.type == EventType.ScrollWheel &&
                viewportRect.Contains(localMouse) &&
                maxScroll > 0f;
        }

        private void QueueDrawPhaseScroll(Rect menuRect, Vector2 localMouse, float maxScroll)
        {
            if (maxScroll <= 0f || !menuRect.Contains(localMouse) || Time.frameCount == _lastRawScrollFrame)
            {
                return;
            }

            var rawAxis = ReadRawScrollAxis();
            if (Mathf.Abs(rawAxis) <= 0.0001f)
            {
                return;
            }

            _lastRawScrollFrame = Time.frameCount;
            QueueScrollDelta(-rawAxis * RawAxisScrollScale);

            var current = Event.current;
            if (current != null && current.type == EventType.ScrollWheel)
            {
                current.Use();
            }
        }

        private static float ReadRawScrollAxis()
        {
            var smoothedAxis = Input.GetAxis("Mouse ScrollWheel");
            var instantAxis = Input.GetAxisRaw("Mouse ScrollWheel");
            return Mathf.Abs(instantAxis) > Mathf.Abs(smoothedAxis) ? instantAxis : smoothedAxis;
        }

        private void ResetScrollStateForMenu(string headerText, IList<PopupMenuItemModel> items)
        {
            var menuKey = BuildMenuKey(headerText, items);
            if (string.Equals(_scrollKey, menuKey, StringComparison.Ordinal))
            {
                return;
            }

            _scrollKey = menuKey;
            _scrollPosition = new Vector2(0f, 0f);
            _pendingScrollDelta = 0f;
            _pressedCommandId = string.Empty;
        }

        private void QueuePointerDown(int button, Vector2 position)
        {
            _queuedMouseDown = true;
            _queuedMouseDownButton = button;
            _queuedMouseDownPosition = position;
        }

        private void QueuePointerUp(int button, Vector2 position)
        {
            _queuedMouseUp = true;
            _queuedMouseUpButton = button;
            _queuedMouseUpPosition = position;
        }

        private void ClearQueuedPointerState()
        {
            _queuedMouseDown = false;
            _queuedMouseUp = false;
            _queuedOutsideClickClose = false;
            _queuedMouseDownButton = -1;
            _queuedMouseUpButton = -1;
            _queuedMouseDownPosition = Vector2.zero;
            _queuedMouseUpPosition = Vector2.zero;
        }

        private static float ClampScrollOffset(float scrollOffset, float maxScroll)
        {
            return Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, maxScroll));
        }

        private static string BuildMenuKey(string headerText, IList<PopupMenuItemModel> items)
        {
            var builder = new StringBuilder(headerText ?? string.Empty);
            if (items == null)
            {
                return builder.ToString();
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                builder
                    .Append('|')
                    .Append(item.CommandId ?? string.Empty)
                    .Append(':')
                    .Append(item.Label ?? string.Empty)
                    .Append(':')
                    .Append(item.IsSeparator ? '1' : '0')
                    .Append(':')
                    .Append(item.IsSectionHeader ? '1' : '0');
            }

            return builder.ToString();
        }

        private static RenderRect BuildMenuRect(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items, out float contentHeight)
        {
            var itemCount = 0;
            var separatorCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                if (items[i].IsSeparator)
                {
                    separatorCount++;
                }
                else
                {
                    itemCount++;
                }
            }

            contentHeight = (itemCount * (ItemHeight + 2f)) + (separatorCount * SeparatorHeight) + VerticalPadding;
            var maxHeight = Mathf.Max(HeaderHeight + ItemHeight + VerticalPadding, viewportSize.Height - 12f);
            var height = Mathf.Min(maxHeight, HeaderHeight + contentHeight);
            var x = Mathf.Min(position.X, Mathf.Max(6f, viewportSize.Width - 280f - 6f));
            var y = Mathf.Min(position.Y, Mathf.Max(6f, viewportSize.Height - height - 6f));
            return new RenderRect(x, y, 280f, height);
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
