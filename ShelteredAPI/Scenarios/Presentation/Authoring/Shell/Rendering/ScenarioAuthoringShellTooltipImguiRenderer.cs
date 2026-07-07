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
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float RichHoverDwellSeconds = 0.40f;
        private const float RichHoverGraceSeconds = 0.30f;
        private const string RichHoverActionBack = "rich.help.back";
        private const string RichHoverActionTopicPrefix = "rich.help.topic.";

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
            _richHoverSourceHoveredThisFrame = false;
            _richHoverSourceKeyThisFrame = null;
            _richHoverCandidate = null;
            _richHoverCandidateSourceRect = RuntimeCompat.ZeroRect();
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
            if (!string.Equals(_richHoverCandidateKey, model.Key, StringComparison.Ordinal))
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
            bool popupHovered = Event.current != null && popupRect.Contains(Event.current.mousePosition);
            if (popupHovered)
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
            bool popupHovered = _activeRichHoverHelp != null
                && Event.current != null
                && _activeRichHoverPopupRect.Contains(Event.current.mousePosition);

            if (_activeRichHoverHelp != null && (activeSourceHovered || popupHovered))
                _richHoverLastHoveredAt = now;

            if (_richHoverCandidate != null
                && now - _richHoverCandidateSince >= RichHoverDwellSeconds
                && (_activeRichHoverHelp == null || !string.Equals(_activeRichHoverHelp.Key, _richHoverCandidate.Key, StringComparison.Ordinal)))
            {
                OpenRichHoverHelp(_richHoverCandidate, _richHoverCandidateSourceRect, scaledWidth, scaledHeight, hudReserveRect, contentRect);
                return;
            }

            if (_activeRichHoverHelp != null
                && !activeSourceHovered
                && !popupHovered
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
            float width = Mathf.Clamp(bounds.width * 0.34f, 340f, 460f);
            float bodyHeight = _textStyle != null
                ? _textStyle.CalcHeight(new GUIContent(model.Body ?? string.Empty), width - 28f)
                : 96f;
            float actionRows = model.Actions != null && model.Actions.Length > 0 ? 42f : 8f;
            float height = Mathf.Clamp(80f + bodyHeight + actionRows, 164f, Math.Min(360f, bounds.height));
            Rect avoidRect = sourceRect.width > 0f && sourceRect.height > 0f
                ? sourceRect
                : BuildTooltipAvoidanceRect(Event.current != null ? Event.current.mousePosition : Vector2.zero, bounds, scaledWidth, false);
            return PlaceTooltipAroundAvoidance(avoidRect, width, height, bounds, scaledWidth, scaledHeight, hudReserveRect);
        }

        private void DrawRichHoverHelpPopup(Rect rect, RichHoverHelpModel help)
        {
            DrawChromePanel(rect, _uiContext.Styles.Menu);
            Rect titleRect = new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 24f);
            GUI.Label(titleRect, (help.Title ?? "Help").ToUpperInvariant(), _sectionTitleStyle);
            Rect bodyRect = new Rect(rect.x + 14f, titleRect.yMax + 8f, rect.width - 28f, rect.height - 88f);
            GUI.Label(bodyRect, help.Body ?? string.Empty, _textStyle);

            Rect actionRect = new Rect(rect.x + 12f, rect.yMax - 42f, rect.width - 24f, 30f);
            DrawRichHoverHelpActions(actionRect, help.Actions);
        }

        private void DrawRichHoverHelpActions(Rect rect, ScenarioAuthoringInspectorAction[] actions)
        {
            float x = rect.x;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 74f, 156f);
                if (x + width > rect.xMax)
                    break;

                Rect buttonRect = new Rect(x, rect.y, width, rect.height);
                if (GUI.Button(buttonRect, new GUIContent(action.Label ?? string.Empty), action.Emphasized ? _activeButtonStyle : _buttonStyle))
                    ExecuteRichHoverHelpAction(action);
                x += width + 6f;
            }
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

            if (!string.IsNullOrEmpty(ResolveActionTopicId(action)))
                return true;

            string text = !string.IsNullOrEmpty(action.DisabledReason)
                ? action.DisabledReason
                : (!string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint);
            return !string.IsNullOrEmpty(text) && text.Length > 90;
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
