using System.Collections.Generic;
using UnityEngine;
using Cortex.Modules.Editor;

namespace Cortex.Modules.Shared
{
    internal sealed class PopupMenuSurface
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
        private Vector2 _scrollPosition = new Vector2(0f, 0f);
        private string _scrollKey = string.Empty;
        private float _pendingScrollDelta;
        private string _pressedCommandId = string.Empty;
        private string _lastPressedCommandId = string.Empty;
        private string _lastReleasedCommandId = string.Empty;
        private int _pressCount;
        private int _releaseCount;
        private int _activationCount;
        private int _scrollCount;
        private int _lastRawScrollFrame = -1;
        private bool _queuedMouseDown;
        private bool _queuedMouseUp;
        private bool _queuedOutsideClickClose;
        private int _queuedMouseDownButton = -1;
        private int _queuedMouseUpButton = -1;
        private Vector2 _queuedMouseDownPosition = Vector2.zero;
        private Vector2 _queuedMouseUpPosition = Vector2.zero;
        private bool _loggedMissingPressWarning;
        private Texture2D _hoverFill;
        private Texture2D _pressedFill;
        private Texture2D _hoverAccentFill;

        public void Reset()
        {
            _scrollPosition = new Vector2(0f, 0f);
            _scrollKey = string.Empty;
            _pendingScrollDelta = 0f;
            _pressedCommandId = string.Empty;
            _lastPressedCommandId = string.Empty;
            _lastReleasedCommandId = string.Empty;
            _pressCount = 0;
            _releaseCount = 0;
            _activationCount = 0;
            _scrollCount = 0;
            _lastRawScrollFrame = -1;
            _queuedMouseDown = false;
            _queuedMouseUp = false;
            _queuedOutsideClickClose = false;
            _queuedMouseDownButton = -1;
            _queuedMouseUpButton = -1;
            _queuedMouseDownPosition = Vector2.zero;
            _queuedMouseUpPosition = Vector2.zero;
            _loggedMissingPressWarning = false;
        }

        public void QueueScrollDelta(float delta)
        {
            _pendingScrollDelta += delta * ScrollWheelStep;
        }

        public bool TryCapturePointerInput(Event current, Rect menuRect, Vector2 localMouse)
        {
            if (current == null)
            {
                return false;
            }

            var isInsideMenu = menuRect.Contains(localMouse);
            if (current.type == EventType.MouseDown)
            {
                if (isInsideMenu)
                {
                    _queuedMouseDown = true;
                    _queuedMouseDownButton = current.button;
                    _queuedMouseDownPosition = localMouse;
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
                    _queuedMouseUp = true;
                    _queuedMouseUpButton = current.button;
                    _queuedMouseUpPosition = localMouse;
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

            var smoothedAxis = Input.GetAxis("Mouse ScrollWheel");
            var instantAxis = Input.GetAxisRaw("Mouse ScrollWheel");
            var rawAxis = Mathf.Abs(instantAxis) > Mathf.Abs(smoothedAxis) ? instantAxis : smoothedAxis;
            if (Mathf.Abs(rawAxis) <= 0.0001f)
            {
                return false;
            }

            _lastRawScrollFrame = Time.frameCount;
            QueueScrollDelta(-rawAxis * RawAxisScrollScale);
            return true;
        }

        public string BuildDiagnosticsSummary()
        {
            return "Presses=" + _pressCount +
                ", Releases=" + _releaseCount +
                ", Activations=" + _activationCount +
                ", Scrolls=" + _scrollCount +
                ", LastPressed='" + (_lastPressedCommandId ?? string.Empty) + "'" +
                ", LastReleased='" + (_lastReleasedCommandId ?? string.Empty) + "'" +
                ", MissingPressWarning=" + _loggedMissingPressWarning + ".";
        }

        public PopupMenuResult Draw(
            Vector2 position,
            Vector2 viewportSize,
            string headerText,
            IList<PopupMenuItem> items,
            Event current,
            Vector2 localMouse,
            GUIStyle menuStyle,
            GUIStyle buttonStyle,
            GUIStyle headerStyle)
        {
            var result = new PopupMenuResult();
            var safeItems = items ?? new PopupMenuItem[0];
            var menuKey = BuildMenuKey(headerText, safeItems);
            EnsureStateTextures();
            if (!string.Equals(_scrollKey, menuKey))
            {
                _scrollKey = menuKey;
                _scrollPosition = new Vector2(0f, 0f);
                _pendingScrollDelta = 0f;
                _pressedCommandId = string.Empty;
            }

            float contentHeight;
            var menuRect = BuildMenuRect(position, viewportSize, safeItems, out contentHeight);
            result.MenuRect = menuRect;

            GUI.Box(menuRect, GUIContent.none, menuStyle);
            GUI.Label(
                new Rect(menuRect.x + HorizontalPadding, menuRect.y + VerticalPadding, menuRect.width - (HorizontalPadding * 2f), 18f),
                headerText ?? string.Empty,
                headerStyle);

            var viewportRect = new Rect(
                menuRect.x + 4f,
                menuRect.y + HeaderHeight,
                Mathf.Max(0f, menuRect.width - 8f),
                Mathf.Max(0f, menuRect.height - HeaderHeight - VerticalPadding));
            var maxScroll = Mathf.Max(0f, contentHeight - viewportRect.height);
            var hasScroll = maxScroll > 0f;
            QueueDrawPhaseScroll(current, menuRect, localMouse, maxScroll);
            _scrollPosition.y = ClampScrollOffset(_scrollPosition.y + _pendingScrollDelta, maxScroll);
            _pendingScrollDelta = 0f;

            if (ShouldHandleScrollWheel(current, viewportRect, localMouse, maxScroll))
            {
                _scrollPosition.y = ClampScrollOffset(_scrollPosition.y + (current.delta.y * ScrollWheelStep), maxScroll);
                _scrollCount++;
                current.Use();
            }

            var contentWidth = Mathf.Max(0f, viewportRect.width - (hasScroll ? ScrollChromeWidth : 0f));
            var viewportMouse = new Vector2(
                localMouse.x - viewportRect.x,
                localMouse.y - viewportRect.y);
            var queuedDownViewportMouse = new Vector2(
                _queuedMouseDownPosition.x - viewportRect.x,
                _queuedMouseDownPosition.y - viewportRect.y);
            var queuedUpViewportMouse = new Vector2(
                _queuedMouseUpPosition.x - viewportRect.x,
                _queuedMouseUpPosition.y - viewportRect.y);
            GUI.BeginGroup(viewportRect);
            try
            {
                GUI.BeginGroup(new Rect(0f, 0f, contentWidth, viewportRect.height));
                try
                {
                    var y = -_scrollPosition.y;
                    for (var i = 0; i < safeItems.Count; i++)
                    {
                        var item = safeItems[i];
                        if (item == null)
                        {
                            continue;
                        }

                        if (item.IsSeparator)
                        {
                            GUI.Box(new Rect(HorizontalPadding, y + 3f, contentWidth - (HorizontalPadding * 2f), 1f), GUIContent.none);
                            y += SeparatorHeight;
                            continue;
                        }

                        var itemRect = new Rect(6f, y, contentWidth - 12f, ItemHeight);
                        var isPointerOverItem = itemRect.Contains(viewportMouse);
                        var isQueuedMouseDownOnItem = _queuedMouseDown && _queuedMouseDownButton == 0 && itemRect.Contains(queuedDownViewportMouse);
                        var isQueuedMouseUpOnItem = _queuedMouseUp && _queuedMouseUpButton == 0 && itemRect.Contains(queuedUpViewportMouse);
                        var isPressedVisual = item.Enabled &&
                            (!string.IsNullOrEmpty(_pressedCommandId) && string.Equals(_pressedCommandId, item.CommandId ?? string.Empty));

                        var previousEnabled = GUI.enabled;
                        GUI.enabled = item.Enabled;
                        DrawItemBackground(itemRect, item.Enabled, isPointerOverItem, isPressedVisual, buttonStyle);

                        if (item.Enabled && ((current != null && current.type == EventType.MouseDown && current.button == 0 && isPointerOverItem) || isQueuedMouseDownOnItem))
                        {
                            _pressedCommandId = item.CommandId ?? string.Empty;
                            _lastPressedCommandId = _pressedCommandId;
                            _pressCount++;
                            EditorInteractionLog.WriteContextMenu(
                                "Menu mouse down on '" + _pressedCommandId +
                                "'. Mouse=(" + localMouse.x.ToString("F1") + "," + localMouse.y.ToString("F1") + ")" +
                                ", ScrollY=" + _scrollPosition.y.ToString("F1") + ".");
                            if (current != null && current.type == EventType.MouseDown && current.button == 0 && isPointerOverItem)
                            {
                                current.Use();
                            }
                        }

                        var canActivateFromRelease = string.IsNullOrEmpty(_pressedCommandId) ||
                            string.Equals(_pressedCommandId, item.CommandId ?? string.Empty);
                        if ((current != null && current.type == EventType.MouseUp && current.button == 0 && isPointerOverItem) || isQueuedMouseUpOnItem)
                        {
                            _releaseCount++;
                            _lastReleasedCommandId = item.CommandId ?? string.Empty;
                            if (string.IsNullOrEmpty(_pressedCommandId) && !_loggedMissingPressWarning)
                            {
                                _loggedMissingPressWarning = true;
                                EditorInteractionLog.WriteContextMenu(
                                    "MouseUp reached '" + (_lastReleasedCommandId ?? string.Empty) +
                                    "' without a prior menu MouseDown. Unity event delivery is dropping the press event.");
                            }
                        }

                        if (item.Enabled && (((current != null && current.type == EventType.MouseUp && current.button == 0 && isPointerOverItem) || isQueuedMouseUpOnItem) && canActivateFromRelease))
                        {
                            _activationCount++;
                            EditorInteractionLog.WriteContextMenu(
                                "Menu activation fired for '" + (item.CommandId ?? string.Empty) +
                                "'. Pressed='" + (_pressedCommandId ?? string.Empty) +
                                "', ScrollY=" + _scrollPosition.y.ToString("F1") + ".");
                            result.ActivatedCommandId = item.CommandId ?? string.Empty;
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
                            var gestureRect = new Rect(itemRect.xMax - 78f, itemRect.y + 3f, 70f, 18f);
                            GUI.Label(gestureRect, item.ShortcutText, headerStyle);
                        }

                        y += ItemHeight + 2f;
                    }
                }
                finally
                {
                    GUI.EndGroup();
                }

                if (hasScroll)
                {
                    DrawScrollChrome(
                        current,
                        viewportRect,
                        viewportMouse,
                        contentWidth,
                        maxScroll,
                        queuedUpViewportMouse);
                }
            }
            finally
            {
                GUI.EndGroup();
            }

            if (_queuedOutsideClickClose)
            {
                EditorInteractionLog.WriteContextMenu(
                    "Closing menu because of outside click. Mouse=(" + localMouse.x.ToString("F1") + "," + localMouse.y.ToString("F1") + ").");
                result.ShouldClose = true;
            }

            if (current != null && current.type == EventType.MouseDown && !menuRect.Contains(localMouse))
            {
                EditorInteractionLog.WriteContextMenu(
                    "Closing menu because of outside click. Mouse=(" + localMouse.x.ToString("F1") + "," + localMouse.y.ToString("F1") + ").");
                result.ShouldClose = true;
            }

            _queuedMouseDown = false;
            _queuedMouseUp = false;
            _queuedOutsideClickClose = false;
            _queuedMouseDownButton = -1;
            _queuedMouseUpButton = -1;

            return result;
        }

        public Rect PredictMenuRect(Vector2 position, Vector2 viewportSize, IList<PopupMenuItem> items)
        {
            float contentHeight;
            return BuildMenuRect(position, viewportSize, items ?? new PopupMenuItem[0], out contentHeight);
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
            _scrollCount++;
        }

        private void DrawItemBackground(Rect itemRect, bool enabled, bool hovered, bool pressed, GUIStyle buttonStyle)
        {
            GUI.Box(itemRect, GUIContent.none, buttonStyle);
            if (!enabled)
            {
                return;
            }

            if (hovered)
            {
                GUI.DrawTexture(itemRect, pressed ? _pressedFill : _hoverFill);
                GUI.DrawTexture(
                    new Rect(itemRect.x, itemRect.y, HoverAccentWidth, itemRect.height),
                    _hoverAccentFill);
            }
        }

        private void EnsureStateTextures()
        {
            if (_hoverFill != null && _pressedFill != null && _hoverAccentFill != null)
            {
                return;
            }

            _hoverFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.18f));
            _pressedFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.28f));
            _hoverAccentFill = MakeFill(CortexIdeLayout.GetAccentColor());
        }

        private static bool ShouldHandleScrollWheel(Event current, Rect viewportRect, Vector2 localMouse, float maxScroll)
        {
            return current != null &&
                current.type == EventType.ScrollWheel &&
                viewportRect.Contains(localMouse) &&
                maxScroll > 0f;
        }

        private void QueueDrawPhaseScroll(Event current, Rect menuRect, Vector2 localMouse, float maxScroll)
        {
            if (maxScroll <= 0f || !menuRect.Contains(localMouse) || Time.frameCount == _lastRawScrollFrame)
            {
                return;
            }

            var smoothedAxis = Input.GetAxis("Mouse ScrollWheel");
            var instantAxis = Input.GetAxisRaw("Mouse ScrollWheel");
            var rawAxis = Mathf.Abs(instantAxis) > Mathf.Abs(smoothedAxis) ? instantAxis : smoothedAxis;
            if (Mathf.Abs(rawAxis) <= 0.0001f)
            {
                return;
            }

            _lastRawScrollFrame = Time.frameCount;
            QueueScrollDelta(-rawAxis * RawAxisScrollScale);

            if (current != null && current.type == EventType.ScrollWheel)
            {
                current.Use();
            }
        }

        private static float ClampScrollOffset(float scrollOffset, float maxScroll)
        {
            return Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, maxScroll));
        }

        private static Texture2D MakeFill(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static string BuildMenuKey(string headerText, IList<PopupMenuItem> items)
        {
            var key = headerText ?? string.Empty;
            if (items == null)
            {
                return key;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                key += "|" + (item.CommandId ?? string.Empty) + ":" + (item.Label ?? string.Empty) + ":" + (item.IsSeparator ? "1" : "0");
            }

            return key;
        }

        private static Rect BuildMenuRect(Vector2 position, Vector2 viewportSize, IList<PopupMenuItem> items, out float contentHeight)
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
            var maxHeight = Mathf.Max(HeaderHeight + ItemHeight + VerticalPadding, viewportSize.y - 12f);
            var height = Mathf.Min(maxHeight, HeaderHeight + contentHeight);
            var x = Mathf.Min(position.x, Mathf.Max(6f, viewportSize.x - 280f - 6f));
            var y = Mathf.Min(position.y, Mathf.Max(6f, viewportSize.y - height - 6f));
            return new Rect(x, y, 280f, height);
        }
    }

    internal sealed class PopupMenuItem
    {
        public string CommandId;
        public string Label;
        public string ShortcutText;
        public bool Enabled;
        public bool IsSeparator;
    }

    internal struct PopupMenuResult
    {
        public bool ShouldClose;
        public string ActivatedCommandId;
        public Rect MenuRect;
    }
}
