using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float RichHoverDwellSeconds = 0.90f;
        private const float RichHoverGraceSeconds = 0.30f;
        private const float RichHoverMouseMovePx = 5f;
        private const float RichHoverPopupMaxWidth = 380f;
        private const float RichHoverPopupMinWidth = 150f;
        private const float RichHoverPopupPaddingX = 14f;
        private const float RichHoverPopupPaddingY = 10f;
        private const float RichHoverPopupGapY = 6f;
        private const float RichHoverPopupActionHeight = 24f;
        private const float RichHoverPopupActionGap = 4f;
        private const float RichHoverPopupVerticalGap = 8f;
        private const string RichHoverActionBack = "rich.help.back";
        private const string RichHoverActionTopicPrefix = "rich.help.topic.";
        private const int RichHoverAutoPopupInlineCharacters = 96;

        private void DrawTooltipOverlayCore(float scaledWidth, float scaledHeight, Rect hudReserveRect, Rect contentRect)
        {
            string tip = _animations.ResolveTooltip(GUI.tooltip);
            float alpha = _animations.GetTooltipAlpha(tip);
            if (string.IsNullOrEmpty(tip) || alpha <= 0.001f)
                return;

            GUIStyle tipStyle = _mutedTextStyle;
            if (tipStyle == null)
                return;
            tipStyle.wordWrap = true;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            float maxWidth = 320f;
            Vector2 size = tipStyle.CalcSize(new GUIContent(tip));
            float width = Math.Min(maxWidth, size.x + 18f);
            float height = tipStyle.CalcHeight(new GUIContent(tip), width - 14f) + 10f;
            bool topChromeHover = mouse.y <= TopBarHeight + 4f;
            Rect tipRect = topChromeHover
                ? BuildTopChromeTooltipRect(mouse, width, height, scaledWidth, scaledHeight, hudReserveRect, contentRect)
                : BuildContentTooltipRect(mouse, width, height, scaledWidth, scaledHeight, hudReserveRect, contentRect);
            using (ScenarioUiGuiScope.Apply(alpha, tipRect, 1f))
            {
                DrawChromePanel(tipRect, _uiContext.Styles.Menu);
                GUI.Label(new Rect(tipRect.x + 7f, tipRect.y + 5f, tipRect.width - 14f, tipRect.height - 10f), tip, tipStyle);
            }
        }

        private Rect BuildTopChromeTooltipRect(Vector2 mouse, float width, float height, float scaledWidth, float scaledHeight, Rect hudReserveRect, Rect contentRect)
        {
            Rect bounds = BuildTooltipBounds(contentRect, scaledWidth, scaledHeight);
            Rect avoidRect = BuildTooltipAvoidanceRect(mouse, bounds, scaledWidth, true);
            return PlaceTooltipAroundAvoidance(avoidRect, width, height, bounds, scaledWidth, scaledHeight, hudReserveRect);
        }

        private Rect BuildContentTooltipRect(Vector2 mouse, float width, float height, float scaledWidth, float scaledHeight, Rect hudReserveRect, Rect contentRect)
        {
            Rect bounds = BuildTooltipBounds(contentRect, scaledWidth, scaledHeight);
            const float horizontalOffset = 16f;
            const float verticalOffset = 16f;

            float x = mouse.x + horizontalOffset;
            float y = mouse.y + verticalOffset;

            if (x + width > bounds.xMax)
                x = mouse.x - width - horizontalOffset;

            if (y + height > bounds.yMax)
                y = mouse.y - height - verticalOffset;

            return new Rect(
                Mathf.Clamp(x, bounds.x, Math.Max(bounds.x, bounds.xMax - width)),
                Mathf.Clamp(y, bounds.y, Math.Max(bounds.y, bounds.yMax - height)),
                width,
                height);
        }

        private Rect PlaceTooltipAroundAvoidance(Rect avoidRect, float width, float height, Rect bounds, float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            const float gap = 10f;
            float centerX = avoidRect.x + (avoidRect.width * 0.5f);
            float centerY = avoidRect.y + (avoidRect.height * 0.5f);
            Rect[] candidates = new[]
            {
                new Rect(centerX - (width * 0.5f), avoidRect.yMax + gap, width, height),
                new Rect(centerX - (width * 0.5f), avoidRect.y - height - gap, width, height),
                new Rect(avoidRect.xMax + gap, centerY - (height * 0.5f), width, height),
                new Rect(avoidRect.x - width - gap, centerY - (height * 0.5f), width, height)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Rect clamped = ClampTooltipRect(candidates[i], bounds, scaledWidth, scaledHeight, hudReserveRect);
                if (!clamped.Overlaps(avoidRect))
                    return clamped;
            }

            Rect fallback = new Rect(centerX - (width * 0.5f), avoidRect.yMax + gap, width, height);
            Rect fallbackClamped = ClampTooltipRect(fallback, bounds, scaledWidth, scaledHeight, hudReserveRect);
            if (fallbackClamped.Overlaps(avoidRect))
            {
                float belowY = Math.Min(bounds.yMax - height, avoidRect.yMax + gap);
                float aboveY = Math.Max(bounds.y, avoidRect.y - height - gap);
                fallbackClamped.y = belowY + height <= bounds.yMax && belowY >= avoidRect.yMax
                    ? belowY
                    : aboveY;
                fallbackClamped.x = Mathf.Clamp(fallbackClamped.x, bounds.x, Math.Max(bounds.x, bounds.xMax - width));
            }
            return fallbackClamped;
        }

        private Rect BuildTooltipBounds(Rect contentRect, float scaledWidth, float scaledHeight)
        {
            Rect fallback = new Rect(Margin, TopBarHeight + Gutter, Math.Max(120f, scaledWidth - (Margin * 2f)), Math.Max(120f, scaledHeight - TopBarHeight - StatusHeight - (Gutter * 2f)));
            Rect bounds = contentRect.width > 0f && contentRect.height > 0f ? contentRect : fallback;
            return new Rect(
                bounds.x + 6f,
                bounds.y + 6f,
                Math.Max(120f, bounds.width - 12f),
                Math.Max(80f, bounds.height - 12f));
        }

        private Rect BuildTooltipAvoidanceRect(Vector2 mouse, Rect bounds, float scaledWidth, bool topChrome)
        {
            if (topChrome)
            {
                float topWidth = Mathf.Clamp(scaledWidth * 0.32f, 300f, 520f);
                return new Rect(
                    Mathf.Clamp(mouse.x - (topWidth * 0.5f), 0f, Math.Max(0f, scaledWidth - topWidth)),
                    0f,
                    topWidth,
                    TopBarHeight + 8f);
            }

            float width = Math.Min(Math.Max(520f, bounds.width * 0.58f), Math.Max(220f, bounds.width));
            float height = 168f;
            return new Rect(
                Mathf.Clamp(mouse.x - (width * 0.5f), bounds.x, Math.Max(bounds.x, bounds.xMax - width)),
                Mathf.Clamp(mouse.y - 56f, bounds.y, Math.Max(bounds.y, bounds.yMax - height)),
                width,
                height);
        }

        private Rect ClampTooltipRect(Rect rect, Rect bounds, float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            rect.x = Mathf.Clamp(rect.x, bounds.x, Math.Max(bounds.x, bounds.xMax - rect.width));
            rect.y = Mathf.Clamp(rect.y, bounds.y, Math.Max(bounds.y, bounds.yMax - rect.height));
            if (rect.Overlaps(hudReserveRect))
            {
                float shiftedLeft = hudReserveRect.x - rect.width - Gutter;
                if (shiftedLeft >= bounds.x)
                    rect.x = shiftedLeft;
                else
                    rect.y = Math.Min(bounds.yMax - rect.height, hudReserveRect.yMax + Gutter);
            }
            rect.x = Mathf.Clamp(rect.x, bounds.x, Math.Max(bounds.x, bounds.xMax - rect.width));
            rect.y = Mathf.Clamp(rect.y, bounds.y, Math.Max(bounds.y, bounds.yMax - rect.height));
            return rect;
        }

        private void BeginRichHoverFrame()
        {
            Event evt = Event.current;
            if (evt != null)
                UpdateRichHoverMouseMotion(evt.mousePosition);
            _richHoverSourceHoveredThisFrame = false;
            _richHoverSourceKeyThisFrame = null;
            _richHoverCandidate = null;
            _richHoverCandidateSourceRect = RuntimeCompat.ZeroRect();
            _richHoverSiblingAvoidRects.Clear();
        }

        private void RegisterRichHoverSiblingAvoidRect(Rect rect)
        {
            if (rect.width > 0f && rect.height > 0f)
                _richHoverSiblingAvoidRects.Add(rect);
        }

        private bool RegisterRichHoverHelpSource(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (!ShouldUseRichHoverHelp(action))
                return false;

            Event evt = Event.current;
            if (evt == null || !rect.Contains(evt.mousePosition))
                return true;

            RichHoverHelpModel model = BuildRichHoverHelpModel(action);
            if (model == null)
                return true;

            float now = Time.realtimeSinceStartup;
            if (!string.Equals(_richHoverCandidateKey, model.Key, StringComparison.Ordinal)
                || IsRichHoverMouseMovingFast())
            {
                _richHoverCandidateKey = model.Key;
                _richHoverCandidateSince = now;
            }

            _richHoverCandidate = model;
            _richHoverCandidateSourceRect = rect;
            _richHoverSourceHoveredThisFrame = true;
            _richHoverSourceKeyThisFrame = model.Key;
            return true;
        }

        private bool IsRichHoverHelpActive()
        {
            return _activeRichHoverHelp != null;
        }

        private bool DrawRichHoverHelpOverlayCore(
            float scaledWidth,
            float scaledHeight,
            Rect hudReserveRect,
            Rect contentRect,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            UpdateRichHoverHelpState(scaledWidth, scaledHeight, hudReserveRect, contentRect);
            if (_activeRichHoverHelp == null)
                return false;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CloseRichHoverHelp();
                if (inputCapture != null)
                    inputCapture.MarkKeyboardShortcutHandled();
                return false;
            }

            Rect popupRect = _activeRichHoverPopupRect;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            bool popupHovered = Event.current != null && popupRect.Contains(mouse);
            if (popupHovered)
                _richHoverLastHoveredAt = Time.realtimeSinceStartup;
            bool sourceBridgeHovered = Event.current != null
                && IsRichHoverSourcePopupBridgeHovered(mouse, _activeRichHoverSourceRect, popupRect);
            if (sourceBridgeHovered)
                _richHoverLastHoveredAt = Time.realtimeSinceStartup;

            DrawRichHoverHelpPopup(popupRect, _activeRichHoverHelp);
            if (inputCapture != null)
            {
                inputCapture.RegisterInteractiveRect(_activeRichHoverSourceRect);
                inputCapture.RegisterInteractiveRect(popupRect);
                inputCapture.SetPopupOpen(true);
            }

            ConsumeRichHoverInput(popupRect);
            return true;
        }

        private void UpdateRichHoverHelpState(float scaledWidth, float scaledHeight, Rect hudReserveRect, Rect contentRect)
        {
            float now = Time.realtimeSinceStartup;
            bool activeSourceHovered = _activeRichHoverHelp != null
                && _richHoverSourceHoveredThisFrame
                && string.Equals(_richHoverSourceKeyThisFrame, _activeRichHoverHelp.Key, StringComparison.Ordinal);
            bool sourceBridgeHovered = Event.current != null
                && IsRichHoverSourcePopupBridgeHovered(Event.current.mousePosition, _activeRichHoverSourceRect, _activeRichHoverPopupRect);
            bool popupHovered = _activeRichHoverHelp != null
                && Event.current != null
                && _activeRichHoverPopupRect.Contains(Event.current.mousePosition);

            if (_activeRichHoverHelp != null && (activeSourceHovered || popupHovered || sourceBridgeHovered))
                _richHoverLastHoveredAt = now;

            if (_richHoverCandidate != null
                && IsRichHoverPopupAnchorStable()
                && now - _richHoverCandidateSince >= RichHoverDwellSeconds
                && (_activeRichHoverHelp == null || !string.Equals(_activeRichHoverHelp.Key, _richHoverCandidate.Key, StringComparison.Ordinal)))
            {
                OpenRichHoverHelp(_richHoverCandidate, _richHoverCandidateSourceRect, scaledWidth, scaledHeight, hudReserveRect, contentRect);
                return;
            }

            if (_activeRichHoverHelp != null
                && !activeSourceHovered
                && !popupHovered
                && !sourceBridgeHovered
                && now - _richHoverLastHoveredAt > RichHoverGraceSeconds)
            {
                CloseRichHoverHelp();
            }
        }

        private void OpenRichHoverHelp(
            RichHoverHelpModel model,
            Rect sourceRect,
            float scaledWidth,
            float scaledHeight,
            Rect hudReserveRect,
            Rect contentRect)
        {
            _activeRichHoverHelp = model;
            _activeRichHoverSourceRect = sourceRect;
            _activeRichHoverPopupRect = BuildRichHoverPopupRect(model, sourceRect, scaledWidth, scaledHeight, hudReserveRect, contentRect);
            _richHoverLastHoveredAt = Time.realtimeSinceStartup;
        }

        private void CloseRichHoverHelp()
        {
            _activeRichHoverHelp = null;
            _activeRichHoverSourceRect = RuntimeCompat.ZeroRect();
            _activeRichHoverPopupRect = RuntimeCompat.ZeroRect();
            _richHoverTopicBackStack.Clear();
        }

        private Rect BuildRichHoverPopupRect(
            RichHoverHelpModel model,
            Rect sourceRect,
            float scaledWidth,
            float scaledHeight,
            Rect hudReserveRect,
            Rect contentRect)
        {
            Rect bounds = BuildTooltipBounds(contentRect, scaledWidth, scaledHeight);
            float width = CalculateRichHoverPopupWidth(model, bounds);
            float height = Mathf.Clamp(
                CalculateRichHoverPopupHeight(model, width),
                72f,
                Math.Min(420f, bounds.height));
            Rect avoidRect = sourceRect.width > 0f && sourceRect.height > 0f
                ? sourceRect
                : BuildTooltipAvoidanceRect(Event.current != null ? Event.current.mousePosition : Vector2.zero, bounds, scaledWidth, false);
            return PlaceRichHoverPopupRect(avoidRect, width, height, bounds, hudReserveRect);
        }

        private void DrawRichHoverHelpPopup(Rect rect, RichHoverHelpModel help)
        {
            DrawRichHoverChromePanel(rect);
            Rect contentRect = new Rect(
                rect.x + RichHoverPopupPaddingX,
                rect.y + RichHoverPopupPaddingY,
                Math.Max(0f, rect.width - (RichHoverPopupPaddingX * 2f)),
                Math.Max(0f, rect.height - (RichHoverPopupPaddingY * 2f)));
            float titleHeight = ResolveRichHoverTitleHeight(help != null ? help.Title : null, contentRect.width);
            float bodyHeight = ResolveRichHoverBodyHeight(help != null ? help.Body : null, contentRect.width);
            float actionHeight = MeasureRichHoverActionAreaHeight(help != null ? help.Actions : null, contentRect.width);

            GUIStyle titleStyle = new GUIStyle(_sectionTitleStyle)
            {
                normal =
                {
                    textColor = new Color(0.74f, 0.61f, 0.21f, 1f)
                }
            };
            titleStyle.wordWrap = true;
            Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, Math.Max(16f, titleHeight));
            GUI.Label(titleRect, (help != null ? (help.Title ?? "Help") : "Help").ToUpperInvariant(), titleStyle);

            if (bodyHeight > 0f)
            {
                GUIStyle bodyStyle = new GUIStyle(_uiContext.Styles.PaperBodyText)
                {
                    wordWrap = true
                };
                float bodyBottom = rect.yMax - RichHoverPopupPaddingY - actionHeight;
                Rect bodyRect = new Rect(
                    contentRect.x,
                    titleRect.yMax + RichHoverPopupGapY,
                    contentRect.width,
                    Math.Max(0f, bodyBottom - (titleRect.yMax + RichHoverPopupGapY)));
                GUI.Label(bodyRect, help != null ? help.Body : string.Empty, bodyStyle);
            }

            if (actionHeight > 0f)
            {
                Rect actionRect = new Rect(
                    contentRect.x,
                    rect.yMax - RichHoverPopupPaddingY - actionHeight,
                    contentRect.width,
                    actionHeight);
                DrawRichHoverHelpActions(actionRect, help.Actions);
            }
        }

        private void DrawRichHoverHelpActions(Rect rect, ScenarioAuthoringInspectorAction[] actions)
        {
            if (rect.height <= 0f || rect.width <= 0f || actions == null || actions.Length == 0)
                return;

            float x = rect.x;
            float y = rect.y;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 74f, 160f);
                if (x > rect.x && x + width > rect.xMax)
                {
                    x = rect.x;
                    y += RichHoverPopupActionHeight + RichHoverPopupActionGap;
                }

                if (y + RichHoverPopupActionHeight > rect.yMax)
                    break;

                Rect buttonRect = new Rect(x, y, width, RichHoverPopupActionHeight);
                if (DrawPlainButton(buttonRect, new GUIContent(action.Label ?? string.Empty), action.Emphasized ? _activeButtonStyle : _buttonStyle, action.Enabled))
                    ExecuteRichHoverHelpAction(action);
                x += width + RichHoverPopupActionGap;
            }
        }

        private void DrawRichHoverChromePanel(Rect rect)
        {
            if (_uiContext == null || _uiContext.Styles == null)
            {
                DrawChromePanel(rect, _rootPanelStyle);
                return;
            }

            ScenarioUiAtlasSkin.DrawCornerCutShadow(rect);
            Color old = GUI.color;
            GUI.color = new Color(0.95f, 0.90f, 0.79f, 1f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);
        }

        private void UpdateRichHoverMouseMotion(Vector2 mousePosition)
        {
            int frame = Time.frameCount;
            if (_richHoverMouseSampleFrame == frame)
                return;

            _richHoverMouseSampleFrame = frame;
            if (!_richHoverMouseHasLastPosition)
            {
                _richHoverMouseHasLastPosition = true;
                _richHoverMouseLastPosition = mousePosition;
                _richHoverMouseMovingFastThisFrame = false;
                return;
            }

            float distance = Vector2.Distance(mousePosition, _richHoverMouseLastPosition);
            _richHoverMouseMovingFastThisFrame = distance > RichHoverMouseMovePx;
            _richHoverMouseLastPosition = mousePosition;
        }

        private bool IsRichHoverMouseMovingFast()
        {
            return _richHoverMouseMovingFastThisFrame;
        }

        private bool IsRichHoverPopupAnchorStable()
        {
            return !_richHoverMouseMovingFastThisFrame;
        }

        private bool IsRichHoverSourcePopupBridgeHovered(Vector2 mouse, Rect sourceRect, Rect popupRect)
        {
            if (sourceRect.Contains(mouse) || popupRect.Contains(mouse))
                return true;

            return IsMouseBetweenSourceAndPopup(mouse, sourceRect, popupRect);
        }

        private bool IsMouseBetweenSourceAndPopup(Vector2 mouse, Rect sourceRect, Rect popupRect)
        {
            if (sourceRect.width <= 0f || sourceRect.height <= 0f || popupRect.width <= 0f || popupRect.height <= 0f)
                return false;

            if (sourceRect.Overlaps(popupRect))
                return true;

            float left = Math.Min(sourceRect.x, popupRect.x);
            float right = Math.Max(sourceRect.xMax, popupRect.xMax);
            float top = Math.Min(sourceRect.y, popupRect.y);
            float bottom = Math.Max(sourceRect.yMax, popupRect.yMax);

            if (popupRect.y > sourceRect.yMax)
                return mouse.x >= left - RichHoverPopupVerticalGap
                    && mouse.x <= right + RichHoverPopupVerticalGap
                    && mouse.y >= sourceRect.yMax - RichHoverPopupVerticalGap
                    && mouse.y <= popupRect.y;

            if (sourceRect.y > popupRect.yMax)
                return mouse.x >= left - RichHoverPopupVerticalGap
                    && mouse.x <= right + RichHoverPopupVerticalGap
                    && mouse.y >= popupRect.yMax - RichHoverPopupVerticalGap
                    && mouse.y <= sourceRect.y;

            if (popupRect.x > sourceRect.xMax)
                return mouse.y >= top - RichHoverPopupVerticalGap
                    && mouse.y <= bottom + RichHoverPopupVerticalGap
                    && mouse.x >= sourceRect.xMax - RichHoverPopupVerticalGap
                    && mouse.x <= popupRect.x;

            return mouse.y >= top - RichHoverPopupVerticalGap
                && mouse.y <= bottom + RichHoverPopupVerticalGap
                && mouse.x >= popupRect.xMax - RichHoverPopupVerticalGap
                && mouse.x <= sourceRect.x;
        }

        private float ResolveRichHoverTitleHeight(string title, float width)
        {
            GUIStyle titleStyle = new GUIStyle(_sectionTitleStyle)
            {
                normal =
                {
                    textColor = new Color(0.74f, 0.61f, 0.21f, 1f)
                },
                wordWrap = true
            };
            return titleStyle.CalcHeight(new GUIContent(title ?? "Help"), width);
        }

        private float ResolveRichHoverBodyHeight(string body, float width)
        {
            if (string.IsNullOrEmpty(body))
                return 0f;

            GUIStyle bodyStyle = new GUIStyle(_uiContext.Styles.PaperBodyText)
            {
                wordWrap = true
            };
            return bodyStyle.CalcHeight(new GUIContent(body), width);
        }

        private float CalculateRichHoverPopupWidth(RichHoverHelpModel model, Rect bounds)
        {
            string title = model != null ? model.Title : null;
            float titleWidth = !string.IsNullOrEmpty(title)
                ? new GUIStyle(_sectionTitleStyle).CalcSize(new GUIContent(title)).x
                : 0f;
            float bodyWidth = MeasureRichHoverTextMaxLineWidth(_uiContext.Styles.PaperBodyText, model != null ? model.Body : null);
            float actionWidth = MeasureRichHoverActionRowWidth(model != null ? model.Actions : null);

            float width = Math.Max(RichHoverPopupMinWidth, Math.Max(titleWidth, Math.Max(bodyWidth, actionWidth)) + (RichHoverPopupPaddingX * 2f));
            return Mathf.Clamp(width, RichHoverPopupMinWidth, Math.Min(RichHoverPopupMaxWidth, bounds.width));
        }

        private float CalculateRichHoverPopupHeight(RichHoverHelpModel model, float width)
        {
            float innerWidth = Math.Max(1f, width - (RichHoverPopupPaddingX * 2f));
            float titleHeight = ResolveRichHoverTitleHeight(model != null ? (model.Title ?? string.Empty) : string.Empty, innerWidth);
            float bodyHeight = ResolveRichHoverBodyHeight(model != null ? model.Body : null, innerWidth);
            float actionHeight = MeasureRichHoverActionAreaHeight(model != null ? model.Actions : null, innerWidth);
            return (RichHoverPopupPaddingY * 2f)
                + Math.Max(16f, titleHeight)
                + (titleHeight > 0f && bodyHeight > 0f ? RichHoverPopupGapY : 0f)
                + bodyHeight
                + (bodyHeight > 0f && actionHeight > 0f ? RichHoverPopupGapY : 0f)
                + actionHeight
                + (actionHeight > 0f ? RichHoverPopupGapY : 0f);
        }

        private float MeasureRichHoverActionAreaHeight(ScenarioAuthoringInspectorAction[] actions, float availableWidth)
        {
            if (actions == null || actions.Length == 0 || availableWidth <= 0f)
                return 0f;

            float rowWidth = 0f;
            float totalHeight = 0f;
            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 74f, 160f);
                if (rowWidth > 0f && rowWidth + RichHoverPopupActionGap + width > availableWidth)
                {
                    if (totalHeight == 0f)
                        totalHeight = RichHoverPopupActionHeight;
                    else
                        totalHeight += RichHoverPopupActionHeight + RichHoverPopupActionGap;
                    rowWidth = width;
                }
                else
                {
                    rowWidth += (rowWidth > 0f ? RichHoverPopupActionGap : 0f) + width;
                    if (totalHeight == 0f)
                        totalHeight = RichHoverPopupActionHeight;
                }
            }

            return totalHeight;
        }

        private float MeasureRichHoverActionRowWidth(ScenarioAuthoringInspectorAction[] actions)
        {
            if (actions == null || actions.Length == 0)
                return 0f;

            float width = 0f;
            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                if (width > 0f)
                    width += RichHoverPopupActionGap;
                width += Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 74f, 160f);
            }

            return width;
        }

        private float MeasureRichHoverTextMaxLineWidth(GUIStyle style, string text)
        {
            if (string.IsNullOrEmpty(text) || style == null)
                return 0f;

            float maxWidth = 0f;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                float width = style.CalcSize(new GUIContent(lines[i])).x;
                if (width > maxWidth)
                    maxWidth = width;
            }

            return maxWidth;
        }

        private Rect PlaceRichHoverPopupRect(
            Rect sourceRect,
            float width,
            float height,
            Rect bounds,
            Rect hudReserveRect)
        {
            float gap = 8f;
            float centerX = sourceRect.x + (sourceRect.width * 0.5f);
            float centerY = sourceRect.y + (sourceRect.height * 0.5f);
            Rect[] candidates = new[]
            {
                new Rect(centerX - (width * 0.5f), sourceRect.yMax + gap, width, height),
                new Rect(centerX - (width * 0.5f), sourceRect.y - height - gap, width, height),
                new Rect(sourceRect.xMax + gap, centerY - (height * 0.5f), width, height),
                new Rect(sourceRect.x - width - gap, centerY - (height * 0.5f), width, height)
            };

            Rect best = RuntimeCompat.ZeroRect();
            float bestScore = float.MaxValue;
            bool found = false;
            for (int i = 0; i < candidates.Length; i++)
            {
                Rect candidate = ConstrainRichHoverPopupRect(candidates[i], bounds, hudReserveRect);
                if (candidate.Overlaps(sourceRect))
                    continue;

                float score = CalculateRichHoverPlacementScore(candidate, sourceRect);
                if (!found || score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    found = true;
                }
            }

            if (found)
                return best;

            Rect fallback = ConstrainRichHoverPopupRect(candidates[0], bounds, hudReserveRect);
            if (fallback.Overlaps(sourceRect))
                fallback = ConstrainRichHoverPopupRect(candidates[1], bounds, hudReserveRect);
            return fallback;
        }

        private float CalculateRichHoverPlacementScore(Rect candidate, Rect sourceRect)
        {
            float overlap = 0f;
            for (int i = 0; i < _richHoverSiblingAvoidRects.Count; i++)
            {
                Rect avoid = _richHoverSiblingAvoidRects[i];
                if (avoid.Overlaps(sourceRect))
                    continue;
                overlap += CalculateOverlapArea(candidate, avoid);
            }

            float sourceCenterX = sourceRect.x + (sourceRect.width * 0.5f);
            float sourceCenterY = sourceRect.y + (sourceRect.height * 0.5f);
            float candidateCenterX = candidate.x + (candidate.width * 0.5f);
            float candidateCenterY = candidate.y + (candidate.height * 0.5f);
            float distance = Mathf.Abs(candidateCenterX - sourceCenterX) + Mathf.Abs(candidateCenterY - sourceCenterY);
            return (overlap * 1000f) + distance;
        }

        private static float CalculateOverlapArea(Rect left, Rect right)
        {
            float width = Math.Max(0f, Math.Min(left.xMax, right.xMax) - Math.Max(left.x, right.x));
            float height = Math.Max(0f, Math.Min(left.yMax, right.yMax) - Math.Max(left.y, right.y));
            return width * height;
        }

        private Rect ConstrainRichHoverPopupRect(Rect rect, Rect bounds, Rect hudReserveRect)
        {
            rect.x = Mathf.Clamp(rect.x, bounds.x, Math.Max(bounds.x, bounds.xMax - rect.width));
            rect.y = Mathf.Clamp(rect.y, bounds.y, Math.Max(bounds.y, bounds.yMax - rect.height));

            if (!rect.Overlaps(hudReserveRect))
                return rect;

            float shiftedLeft = hudReserveRect.x - rect.width - Gutter;
            if (shiftedLeft >= bounds.x)
            {
                rect.x = shiftedLeft;
            }
            else
            {
                float shiftedRight = hudReserveRect.xMax + Gutter;
                if (shiftedRight + rect.width <= bounds.xMax)
                    rect.x = shiftedRight;
                else
                    rect.y = Math.Min(bounds.yMax - rect.height, hudReserveRect.yMax + Gutter);
            }

            rect.x = Mathf.Clamp(rect.x, bounds.x, Math.Max(bounds.x, bounds.xMax - rect.width));
            rect.y = Mathf.Clamp(rect.y, bounds.y, Math.Max(bounds.y, bounds.yMax - rect.height));
            return rect;
        }

        private void ExecuteRichHoverHelpAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return;

            if (string.Equals(action.Id, RichHoverActionBack, StringComparison.Ordinal))
            {
                if (_richHoverTopicBackStack.Count == 0)
                    return;

                string topicId = _richHoverTopicBackStack[_richHoverTopicBackStack.Count - 1];
                _richHoverTopicBackStack.RemoveAt(_richHoverTopicBackStack.Count - 1);
                ReplaceActiveRichHoverTopic(topicId, false);
                return;
            }

            if (action.Id.StartsWith(RichHoverActionTopicPrefix, StringComparison.Ordinal))
            {
                string topicId = action.Id.Substring(RichHoverActionTopicPrefix.Length);
                ReplaceActiveRichHoverTopic(topicId, true);
                return;
            }

            ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
            CloseRichHoverHelp();
            if (Event.current != null)
                Event.current.Use();
        }

        private void ReplaceActiveRichHoverTopic(string topicId, bool pushCurrent)
        {
            if (pushCurrent && _activeRichHoverHelp != null && !string.IsNullOrEmpty(_activeRichHoverHelp.TopicId))
                _richHoverTopicBackStack.Add(_activeRichHoverHelp.TopicId);

            RichHoverHelpModel model = BuildTopicRichHoverHelpModel(topicId);
            if (model == null)
                return;

            _activeRichHoverHelp = model;
            if (Event.current != null)
                Event.current.Use();
        }

        private void ConsumeRichHoverInput(Rect rect)
        {
            Event evt = Event.current;
            if (evt == null || !rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.MouseDown
                || evt.type == EventType.MouseUp
                || evt.type == EventType.MouseDrag
                || evt.type == EventType.ScrollWheel)
            {
                evt.Use();
            }
        }

        private RichHoverHelpModel BuildRichHoverHelpModel(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return null;

            string topicId = ResolveActionTopicId(action);
            if (!string.IsNullOrEmpty(topicId) && action.Id != null && action.Id.StartsWith(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix, StringComparison.Ordinal))
                return BuildTopicRichHoverHelpModel(topicId);

            string body = action.Enabled
                ? (!string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint)
                : (!string.IsNullOrEmpty(action.DisabledReason) ? action.DisabledReason : (!string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint));
            if (string.IsNullOrEmpty(body))
                return null;

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            if (!string.IsNullOrEmpty(topicId))
                actions.Add(RichHoverAction(RichHoverActionTopicPrefix + topicId, "More", "Open this help topic in the popup.", true, true));
            if (!string.IsNullOrEmpty(action.Id) && action.Enabled)
                actions.Add(RichHoverAction(action.Id, "Open", "Use this control.", true, false));

            return new RichHoverHelpModel
            {
                Key = BuildRichHoverKey(action),
                Title = action.Label,
                Body = body,
                TopicId = topicId,
                Actions = actions.ToArray()
            };
        }

        private RichHoverHelpModel BuildTopicRichHoverHelpModel(string topicId)
        {
            ScenarioAuthoringHelpPage page = TutorialContent.FindHelpPage(topicId);
            if (page == null)
                return null;

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            if (_richHoverTopicBackStack.Count > 0)
                actions.Add(RichHoverAction(RichHoverActionBack, "Back", "Return to the previous help card.", true, false));
            actions.Add(RichHoverAction(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + page.Id, "Open Topic", "Open the full workshop help topic.", true, true));
            if (page.Stage != ScenarioStageKind.None)
                actions.Add(RichHoverAction(ScenarioAuthoringActionIds.ActionStageSelectPrefix + page.Stage, "Go", "Open the related workspace.", true, false));
            else if (!string.IsNullOrEmpty(page.WindowId))
                actions.Add(RichHoverAction(ScenarioAuthoringActionIds.ActionWindowTogglePrefix + page.WindowId, "Focus", "Focus the related editor panel.", true, false));

            if (!string.IsNullOrEmpty(page.TourId))
                actions.Add(RichHoverAction(ScenarioAuthoringActionIds.ActionTourStartPrefix + page.TourId, "Tour", "Start the related spotlight tour.", true, false));

            return new RichHoverHelpModel
            {
                Key = "topic:" + page.Id,
                Title = page.Title,
                Body = page.Body,
                TopicId = page.Id,
                Actions = actions.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorAction RichHoverAction(string id, string label, string hint, bool enabled, bool emphasized)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = id,
                Label = label,
                Hint = hint,
                Enabled = enabled,
                Emphasized = emphasized
            };
        }

        private static bool ShouldUseRichHoverHelp(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return false;

            string topicId = ResolveActionTopicId(action);
            if (!string.IsNullOrEmpty(topicId))
                return true;

            string text = !string.IsNullOrEmpty(action.DisabledReason)
                ? action.DisabledReason
                : (!string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint);
            if (string.IsNullOrEmpty(text))
                return false;

            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
                return true;

            if (text.Length > RichHoverAutoPopupInlineCharacters)
                return true;

            return false;
        }

        private static string BuildRichHoverKey(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return null;

            string topicId = ResolveActionTopicId(action);
            if (!string.IsNullOrEmpty(topicId) && action.Id != null && action.Id.StartsWith(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix, StringComparison.Ordinal))
                return "topic:" + topicId;

            return "action:" + (action.Id ?? action.Label ?? string.Empty);
        }

        private static string ResolveActionTopicId(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return null;

            if (action.Id.StartsWith(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix, StringComparison.Ordinal))
                return action.Id.Substring(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix.Length);

            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellOpenHelp, StringComparison.Ordinal))
                return TutorialContent.TopicSetup;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionPlaytest, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionPlaytestRestart, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Test, StringComparison.Ordinal))
                return TutorialContent.TopicTest;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.People, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionToolFamily, StringComparison.Ordinal))
                return TutorialContent.TopicCast;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.InventoryStorage, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionToolInventory, StringComparison.Ordinal))
                return TutorialContent.TopicSupplies;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Events, StringComparison.Ordinal))
                return TutorialContent.TopicTimelineConditions;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests, StringComparison.Ordinal))
                return TutorialContent.TopicStory;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Publish, StringComparison.Ordinal))
                return TutorialContent.TopicPublish;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Bunker, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.BunkerInside, StringComparison.Ordinal))
                return TutorialContent.TopicWorldCamera;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Map, StringComparison.Ordinal))
                return TutorialContent.TopicMap;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionToolAssets, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionWindowTogglePrefix + ScenarioAuthoringWindowIds.PixelEditor, StringComparison.Ordinal))
                return TutorialContent.TopicArtPixelEditor;

            return null;
        }

        private sealed class RichHoverHelpModel
        {
            public string Key;
            public string Title;
            public string Body;
            public string TopicId;
            public ScenarioAuthoringInspectorAction[] Actions;
        }

    }
}
