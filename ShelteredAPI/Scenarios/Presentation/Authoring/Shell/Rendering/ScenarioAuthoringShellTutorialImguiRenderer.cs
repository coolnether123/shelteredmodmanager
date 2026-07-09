using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private void DrawHelpModalCore(Rect availableRect, ScenarioAuthoringHelpViewModel help, ScenarioAuthoringInputCaptureService inputCapture)
        {
            bool visible = help != null;
            float dimAlpha = _animations.GetHelpModalDimAlpha(visible);
            if (dimAlpha > 0.001f)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, dimAlpha);
                GUI.DrawTexture(availableRect, Texture2D.whiteTexture);
                GUI.color = oldColor;
            }

            if (!visible)
                return;

            Rect rect = new Rect(
                Math.Max(Margin, availableRect.x + (availableRect.width - 620f) * 0.5f),
                Math.Max(availableRect.y + Gutter, availableRect.y + (availableRect.height - 420f) * 0.5f),
                Math.Min(620f, availableRect.width - (Margin * 2f)),
                Math.Min(420f, availableRect.height - (Gutter * 2f)));
            float progress = _animations.GetHelpModalPanelProgress(true);
            float scale = Mathf.Lerp(0.975f, 1f, progress);
            using (ScenarioUiGuiScope.Apply(progress, rect, scale))
                DrawHelpModalPanel(rect, help);

            inputCapture.RegisterInteractiveRect(rect);
            inputCapture.SetPopupOpen(true);
        }

        private void DrawHelpModalPanel(Rect rect, ScenarioAuthoringHelpViewModel help)
        {
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(rect, help.Title, help.Subtitle, false, 46f, 0f);
            DrawHelpHeaderActions(regions.Header, help.HeaderActions);
            GUILayout.BeginArea(regions.Body);
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            GUILayout.Label((help.PageTitle ?? "Help").ToUpperInvariant(), _sectionTitleStyle);
            GUILayout.Space(6f);
            GUILayout.Label(help.Body ?? string.Empty, _textStyle);
            GUILayout.Space(12f);
            DrawHelpTopicActions(help.TopicActions);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawButton(GUILayoutUtility.GetRect(92f, 28f, GUILayout.Width(92f), GUILayout.Height(28f)), help.PreviousAction, false);
            GUILayout.FlexibleSpace();
            GUILayout.Label((help.PageIndex + 1) + " / " + help.PageCount, _mutedTextStyle, GUILayout.Width(54f));
            GUILayout.FlexibleSpace();
            DrawButton(GUILayoutUtility.GetRect(92f, 28f, GUILayout.Width(92f), GUILayout.Height(28f)), help.NextAction, false);
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
            DrawButton(GUILayoutUtility.GetRect(180f, 30f, GUILayout.Width(180f), GUILayout.Height(30f)), help.ReplayAction, false);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawHelpTopicActions(ScenarioAuthoringInspectorAction[] actions)
        {
            if (actions == null || actions.Length == 0)
                return;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                DrawButton(GUILayoutUtility.GetRect(160f, 28f, GUILayout.Width(160f), GUILayout.Height(28f)), action, false);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawHelpHeaderActions(Rect headerRect, ScenarioAuthoringInspectorAction[] actions)
        {
            float actionX = headerRect.xMax - 28f;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                DrawButton(new Rect(actionX, headerRect.y + 4f, 22f, 22f), action, false);
                actionX -= 24f;
            }
        }

        private void DrawTutorialOverlayCore(
            Rect availableRect,
            Rect topRect,
            Rect statusRect,
            Dictionary<string, Rect> windowRects,
            ScenarioAuthoringShellViewModel shell,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            ScenarioAuthoringTutorialViewModel tutorial = shell != null ? shell.Tutorial : null;
            ScenarioAuthoringTourViewModel tour = shell != null ? shell.Tour : null;
            if (tour != null && tour.Visible)
            {
                DrawSpotlightTourOverlay(availableRect, topRect, statusRect, windowRects, shell, tour, inputCapture);
                return;
            }

            bool visible = tutorial != null && tutorial.Visible;
            float progress = _animations.GetTutorialOverlayProgress(visible);
            if (!visible || progress <= 0.001f)
                return;

            Rect targetRect = ResolveTutorialTargetRect(tutorial, topRect, statusRect, windowRects, shell);
            if (IsFullSurfaceSpotlightTarget(targetRect, availableRect))
                targetRect = ZeroRect();
            DrawSpotlightDimming(availableRect, targetRect, progress);

            if (targetRect.width > 0f && targetRect.height > 0f)
                DrawSpotlightBorder(targetRect, progress);

            Rect calloutRect = BuildTutorialCalloutRect(availableRect, targetRect);
            calloutRect = ResolveTutorialCardRect("tutorial:" + (tutorial.StepId ?? tutorial.StepIndex.ToString()), calloutRect, availableRect);
            calloutRect = _animations.GetAnimatedRect("tutorial.card.rect", calloutRect, 0.18f);
            Rect animatedRect = new Rect(calloutRect.x, calloutRect.y - ((1f - progress) * 8f), calloutRect.width, calloutRect.height);
            DrawSpotlightPointer(animatedRect, targetRect, progress);
            using (ScenarioUiGuiScope.Apply(progress, animatedRect, 1f))
                DrawTutorialCallout(animatedRect, tutorial);

            HandleSpotlightTargetClick(tutorial.TargetId, targetRect, true);
            inputCapture.RegisterInteractiveRect(availableRect);
            inputCapture.RegisterInteractiveRect(calloutRect);
            if (targetRect.width > 0f && targetRect.height > 0f)
                inputCapture.RegisterInteractiveRect(targetRect);
            inputCapture.SetPopupOpen(true);
        }

        private void DrawSpotlightTourOverlay(
            Rect availableRect,
            Rect topRect,
            Rect statusRect,
            Dictionary<string, Rect> windowRects,
            ScenarioAuthoringShellViewModel shell,
            ScenarioAuthoringTourViewModel tour,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            bool visible = tour != null && tour.Visible;
            float progress = _animations.GetTutorialOverlayProgress(visible);
            if (!visible || progress <= 0.001f)
                return;

            Rect targetRect = ResolveTourTargetRect(tour.TargetId, topRect, statusRect, windowRects, shell);
            if (IsFullSurfaceSpotlightTarget(targetRect, availableRect))
                targetRect = ZeroRect();
            DrawSpotlightDimming(availableRect, targetRect, progress);

            if (targetRect.width > 0f && targetRect.height > 0f)
                DrawSpotlightBorder(targetRect, progress);

            Rect calloutRect = BuildTutorialCalloutRect(availableRect, targetRect);
            calloutRect = ResolveTutorialCardRect("tour:" + (tour.TourId ?? string.Empty) + ":" + tour.StepIndex.ToString(), calloutRect, availableRect);
            calloutRect = _animations.GetAnimatedRect("tour.card.rect", calloutRect, 0.18f);
            Rect animatedRect = new Rect(calloutRect.x, calloutRect.y - ((1f - progress) * 8f), calloutRect.width, calloutRect.height);
            DrawSpotlightPointer(animatedRect, targetRect, progress);
            using (ScenarioUiGuiScope.Apply(progress, animatedRect, 1f))
                DrawTourCallout(animatedRect, tour);

            HandleSpotlightTargetClick(tour.TargetId, targetRect, false);
            inputCapture.RegisterInteractiveRect(availableRect);
            inputCapture.RegisterInteractiveRect(calloutRect);
            if (targetRect.width > 0f && targetRect.height > 0f)
                inputCapture.RegisterInteractiveRect(targetRect);
            inputCapture.SetPopupOpen(true);
        }

        private Rect ResolveTourTargetRect(
            string targetId,
            Rect topRect,
            Rect statusRect,
            Dictionary<string, Rect> windowRects,
            ScenarioAuthoringShellViewModel shell)
        {
            if (string.IsNullOrEmpty(targetId))
                return ZeroRect();

            ScenarioAuthoringTourTargetRegistry registry = ScenarioCompositionRoot.Resolve<ScenarioAuthoringTourTargetRegistry>();
            Rect registered;
            if (registry != null && registry.TryGet(targetId, out registered))
                return registered;

            if (targetId.StartsWith("window:", StringComparison.Ordinal))
            {
                string windowId = targetId.Substring("window:".Length);
                Rect rect;
                return windowRects != null && windowRects.TryGetValue(windowId, out rect) ? rect : ZeroRect();
            }

            if (targetId.StartsWith("stage:", StringComparison.Ordinal))
            {
                string stage = targetId.Substring("stage:".Length);
                return ResolveTopBarActionRect(topRect, shell, ScenarioAuthoringActionIds.ActionStageSelectPrefix + stage);
            }

            if (targetId.StartsWith("action:", StringComparison.Ordinal))
            {
                string actionId = targetId.Substring("action:".Length);
                if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionPlaytest, StringComparison.Ordinal))
                    return BuildStatusPlaytestRect(statusRect);
                return ResolveTopBarActionRect(topRect, shell, actionId);
            }

            return ZeroRect();
        }

        private void HandleSpotlightTargetClick(string targetId, Rect targetRect, bool tutorial)
        {
            Event evt = Event.current;
            if (evt == null
                || evt.type != EventType.MouseDown
                || evt.button != 0
                || targetRect.width <= 0f
                || targetRect.height <= 0f
                || !targetRect.Contains(evt.mousePosition))
            {
                return;
            }

            string actionId = ResolveSpotlightTargetActionId(targetId);
            if (!string.IsNullOrEmpty(actionId))
                ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);

            ScenarioAuthoringBackendService.Instance.ExecuteAction(tutorial ? ScenarioAuthoringActionIds.ActionTutorialNext : ScenarioAuthoringActionIds.ActionTourNext);
            evt.Use();
        }

        private static string ResolveSpotlightTargetActionId(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return null;

            if (targetId.StartsWith("action:", StringComparison.Ordinal))
                return targetId.Substring("action:".Length);

            if (targetId.StartsWith("stage:", StringComparison.Ordinal))
                return ScenarioAuthoringActionIds.ActionStageSelectPrefix + targetId.Substring("stage:".Length);

            if (targetId.StartsWith("tool:", StringComparison.Ordinal))
            {
                string tool = targetId.Substring("tool:".Length);
                if (string.Equals(tool, ScenarioAuthoringTool.Select.ToString(), StringComparison.OrdinalIgnoreCase))
                    return ScenarioAuthoringActionIds.ActionToolSelect;
                if (string.Equals(tool, ScenarioAuthoringTool.Objects.ToString(), StringComparison.OrdinalIgnoreCase))
                    return ScenarioAuthoringActionIds.ActionToolObjects;
                if (string.Equals(tool, ScenarioAuthoringTool.Assets.ToString(), StringComparison.OrdinalIgnoreCase))
                    return ScenarioAuthoringActionIds.ActionToolAssets;
                if (string.Equals(tool, ScenarioAuthoringTool.Family.ToString(), StringComparison.OrdinalIgnoreCase))
                    return ScenarioAuthoringActionIds.ActionToolFamily;
            }

            return null;
        }

        private void DrawSpotlightDimming(Rect availableRect, Rect targetRect, float progress)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.64f * progress);

            if (targetRect.width <= 0f || targetRect.height <= 0f)
            {
                GUI.DrawTexture(availableRect, Texture2D.whiteTexture);
                GUI.color = oldColor;
                return;
            }

            Rect cutout = new Rect(targetRect.x - 8f, targetRect.y - 8f, targetRect.width + 16f, targetRect.height + 16f);
            cutout.xMin = Mathf.Clamp(cutout.xMin, availableRect.xMin, availableRect.xMax);
            cutout.xMax = Mathf.Clamp(cutout.xMax, availableRect.xMin, availableRect.xMax);
            cutout.yMin = Mathf.Clamp(cutout.yMin, availableRect.yMin, availableRect.yMax);
            cutout.yMax = Mathf.Clamp(cutout.yMax, availableRect.yMin, availableRect.yMax);

            GUI.DrawTexture(new Rect(availableRect.x, availableRect.y, availableRect.width, Mathf.Max(0f, cutout.yMin - availableRect.y)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(availableRect.x, cutout.yMax, availableRect.width, Mathf.Max(0f, availableRect.yMax - cutout.yMax)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(availableRect.x, cutout.yMin, Mathf.Max(0f, cutout.xMin - availableRect.x), cutout.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cutout.xMax, cutout.yMin, Mathf.Max(0f, availableRect.xMax - cutout.xMax), cutout.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawSpotlightPointer(Rect cardRect, Rect targetRect, float progress)
        {
            if (targetRect.width <= 0f || targetRect.height <= 0f || cardRect.width <= 0f || cardRect.height <= 0f)
                return;

            Vector2 from = ClosestPointOnRect(cardRect, targetRect.center);
            Vector2 to = ClosestPointOnRect(targetRect, cardRect.center);
            DrawGoldLine(from, to, 2f, progress);

            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.76f, 0.20f, 0.95f * progress);
            GUI.DrawTexture(new Rect(to.x - 4f, to.y - 4f, 8f, 8f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static Vector2 ClosestPointOnRect(Rect rect, Vector2 point)
        {
            float x = Mathf.Clamp(point.x, rect.xMin, rect.xMax);
            float y = Mathf.Clamp(point.y, rect.yMin, rect.yMax);
            float left = Math.Abs(x - rect.xMin);
            float right = Math.Abs(rect.xMax - x);
            float top = Math.Abs(y - rect.yMin);
            float bottom = Math.Abs(rect.yMax - y);
            float min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            if (min == left)
                x = rect.xMin;
            else if (min == right)
                x = rect.xMax;
            else if (min == top)
                y = rect.yMin;
            else
                y = rect.yMax;
            return new Vector2(x, y);
        }

        private static void DrawGoldLine(Vector2 start, Vector2 end, float width, float alpha)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(start, end);
            GUI.color = new Color(1f, 0.76f, 0.20f, 0.90f * alpha);
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - (width * 0.5f), length, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = color;
        }

        private void DrawSpotlightBorder(Rect targetRect, float progress)
        {
            Rect rect = new Rect(targetRect.x - 8f, targetRect.y - 8f, targetRect.width + 16f, targetRect.height + 16f);
            float pulse = 0.75f + (Mathf.Sin(Time.realtimeSinceStartup * 5f) * 0.25f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.98f, 0.78f, 0.28f, pulse * progress);
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);
            GUI.color = oldColor;
        }

        private void DrawTourCallout(Rect rect, ScenarioAuthoringTourViewModel tour)
        {
            DrawTutorialCardChrome(rect);
            GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f));
            GUILayout.Label("STEP " + (tour.StepIndex + 1) + " / " + tour.StepCount, _mutedTextStyle);
            GUILayout.Label(tour.Title ?? "TOUR", _sectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.Label(tour.Body ?? string.Empty, _textStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            DrawTutorialCardButton(tour.BackAction, 72f, 150f, 30f);
            GUILayout.FlexibleSpace();
            DrawTutorialCardButton(tour.ExitAction, 72f, 150f, 30f);
            GUILayout.Space(6f);
            DrawTutorialCardButton(tour.NextAction, 72f, 150f, 30f);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private Rect ResolveTutorialTargetRect(
            ScenarioAuthoringTutorialViewModel tutorial,
            Rect topRect,
            Rect statusRect,
            Dictionary<string, Rect> windowRects,
            ScenarioAuthoringShellViewModel shell)
        {
            if (tutorial == null)
                return ZeroRect();

            if (!string.IsNullOrEmpty(tutorial.TargetId))
                return ResolveTourTargetRect(tutorial.TargetId, topRect, statusRect, windowRects, shell);

            if (!string.IsNullOrEmpty(tutorial.TargetWindowId))
            {
                Rect rect;
                if (windowRects != null && windowRects.TryGetValue(tutorial.TargetWindowId, out rect))
                    return rect;
                if (tutorial.TargetStage != ScenarioStageKind.None)
                    return ResolveTopBarActionRect(topRect, shell, ScenarioAuthoringActionIds.ActionStageSelectPrefix + tutorial.TargetStage);
                return BuildWindowMenuButtonRect(topRect);
            }

            if (string.Equals(tutorial.TargetActionId, "playtest", StringComparison.Ordinal))
                return BuildStatusPlaytestRect(statusRect);

            return ZeroRect();
        }

        private static Rect BuildWindowMenuButtonRect(Rect topRect)
        {
            return new Rect(topRect.xMax - 116f, topRect.y + 54f, 106f, 30f);
        }

        private Rect ResolveTopBarActionRect(Rect topRect, ScenarioAuthoringShellViewModel shell, string actionId)
        {
            if (shell == null || string.IsNullOrEmpty(actionId))
                return ZeroRect();

            Rect toolbarRect = ResolveToolbarActionRect(topRect, shell.ToolbarActions, actionId);
            if (toolbarRect.width > 0f)
                return toolbarRect;

            return ResolveStageTabRect(topRect, shell.Tabs, actionId);
        }

        private Rect ResolveToolbarActionRect(Rect topRect, ScenarioAuthoringInspectorAction[] actions, string actionId)
        {
            bool compact = IsCompactTopBar(topRect);
            float rowY = compact ? 10f : 13f;
            float rowHeight = compact ? 28f : 30f;
            float actionRight = compact ? topRect.xMax - 104f : topRect.xMax - 10f;
            float primaryRowLeft = compact ? topRect.x + 12f : topRect.x + 18f + 220f + 20f;
            float toolbarWidth = MeasureTopBarActionsWidth(actions, compact);
            float compactToolbarX = Math.Min(topRect.x + 228f, topRect.xMax - 188f);
            float toolbarX = compact ? compactToolbarX : Math.Max(primaryRowLeft, actionRight - toolbarWidth);
            float x = toolbarX;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;
                if (compact && IsLowPriorityTopBarAction(action))
                    continue;

                float width = ResolveToolbarActionWidth(action, compact);
                Rect rect = new Rect(x, topRect.y + rowY, width, rowHeight);
                if (string.Equals(action.Id, actionId, StringComparison.Ordinal))
                    return rect;
                x = rect.xMax + (compact ? 3f : 4f);
            }

            return ZeroRect();
        }

        private Rect ResolveStageTabRect(Rect topRect, ScenarioAuthoringInspectorAction[] tabs, string actionId)
        {
            bool compact = IsCompactTopBar(topRect);
            float primaryRowY = compact ? 42f : 10f;
            float primaryRowHeight = compact ? 24f : 36f;
            float x = compact ? topRect.x + 12f : topRect.x + 18f + 220f + 20f;
            float right = compact ? topRect.xMax - 12f : topRect.xMax - 420f;
            for (int i = 0; tabs != null && i < tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = tabs[i];
                if (tab == null || IsChildStageTab(tab))
                    continue;

                float width = ResolvePrimaryStageTabWidth(tab, compact);
                Rect rect = new Rect(x, topRect.y + primaryRowY, width, primaryRowHeight);
                if (rect.xMax > right)
                    break;
                if (string.Equals(tab.Id, actionId, StringComparison.Ordinal))
                    return rect;
                x = rect.xMax + 2f;
            }

            float childX = compact ? topRect.x + 12f : topRect.x + 18f + 220f + 20f;
            float childY = compact ? 68f : 54f;
            float childHeight = compact ? 24f : 30f;
            float childRight = compact ? topRect.xMax - 12f : topRect.xMax - 126f;
            for (int i = 0; tabs != null && i < tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = tabs[i];
                if (tab == null || !IsChildStageTab(tab))
                    continue;

                ScenarioAuthoringInspectorAction displayTab = CloneWithLabel(tab, CleanChildStageLabel(tab.Label));
                float width = ResolveChildStageTabWidth(displayTab, compact);
                Rect rect = new Rect(childX, topRect.y + childY, width, childHeight);
                if (rect.xMax > childRight)
                    break;
                if (string.Equals(tab.Id, actionId, StringComparison.Ordinal))
                    return rect;
                childX = rect.xMax + 2f;
            }

            return ZeroRect();
        }

        private static Rect ZeroRect()
        {
            return new Rect(0f, 0f, 0f, 0f);
        }

        // A spotlight cutout only reads as a highlight when it frames a specific
        // control. A "target" that covers most of the surface (e.g. the welcome
        // step pointing at the whole Home window) is not a real spotlight, so we
        // drop the cutout and fall back to a uniform dim with a centered card.
        private static bool IsFullSurfaceSpotlightTarget(Rect targetRect, Rect availableRect)
        {
            if (targetRect.width <= 0f || targetRect.height <= 0f
                || availableRect.width <= 0f || availableRect.height <= 0f)
            {
                return false;
            }

            float coverX = targetRect.width / availableRect.width;
            float coverY = targetRect.height / availableRect.height;
            return coverX >= 0.72f && coverY >= 0.6f;
        }

        // Size a tour/tutorial card button to its label using the button style's
        // own measurement so labels ("HELP", "SKIP TOUR", "KEEP GOING") never get
        // clipped to an ellipsis at typical card widths.
        private void DrawTutorialCardButton(ScenarioAuthoringInspectorAction action, float minWidth, float maxWidth, float height)
        {
            float width = action != null
                ? Mathf.Clamp(MeasureButtonWidth(action, false, 24f), minWidth, maxWidth)
                : minWidth;
            DrawButton(GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height)), action, false);
        }

        private static Rect BuildStatusPlaytestRect(Rect statusRect)
        {
            const float rightControlsWidth = 528f;
            float rightControlsX = Math.Max(statusRect.x + 220f, statusRect.xMax - rightControlsWidth);
            return new Rect(rightControlsX, statusRect.y + 9f, 120f, 28f);
        }

        private void DrawTutorialTargetHighlight(Rect targetRect, float progress)
        {
            Rect rect = new Rect(targetRect.x - 6f, targetRect.y - 6f, targetRect.width + 12f, targetRect.height + 12f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.882f, 0.784f, 0.588f, 0.20f * progress);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.882f, 0.784f, 0.588f, 0.95f * progress);
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);
            GUI.color = oldColor;
        }

        private static Rect BuildTutorialCalloutRect(Rect availableRect, Rect targetRect)
        {
            float width = Math.Min(430f, availableRect.width - (Margin * 2f));
            float height = 218f;
            if (targetRect.width <= 0f || targetRect.height <= 0f || targetRect.yMax <= availableRect.y)
                return BuildTutorialTopCenterRect(availableRect, width, height);

            float minX = availableRect.x + Margin;
            float maxX = availableRect.xMax - width - Margin;
            float minY = availableRect.y + Margin;
            float maxY = availableRect.yMax - height - Margin;

            Rect[] candidates = new Rect[]
            {
                new Rect(targetRect.xMax + Gutter, Mathf.Clamp(targetRect.y, minY, maxY), width, height),
                new Rect(targetRect.x - width - Gutter, Mathf.Clamp(targetRect.y, minY, maxY), width, height),
                new Rect(Mathf.Clamp(targetRect.center.x - (width * 0.5f), minX, maxX), targetRect.yMax + Gutter, width, height),
                new Rect(Mathf.Clamp(targetRect.center.x - (width * 0.5f), minX, maxX), targetRect.y - height - Gutter, width, height),
                BuildTutorialTopCenterRect(availableRect, width, height)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Rect candidate = candidates[i];
                if (candidate.x < minX || candidate.x > maxX || candidate.y < minY || candidate.y > maxY)
                    continue;
                if (!candidate.Overlaps(targetRect))
                    return candidate;
            }

            Rect fallback = BuildTutorialTopCenterRect(availableRect, width, height);
            if (fallback.Overlaps(targetRect))
                fallback.y = Mathf.Clamp(targetRect.yMax + Gutter, minY, maxY);
            return fallback;
        }

        private static Rect BuildTutorialTopCenterRect(Rect availableRect, float width, float height)
        {
            return new Rect(
                availableRect.x + ((availableRect.width - width) * 0.5f),
                availableRect.y + Margin,
                width,
                height);
        }

        private void DrawTutorialCallout(Rect rect, ScenarioAuthoringTutorialViewModel tutorial)
        {
            DrawTutorialCardChrome(rect);
            GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f));
            GUILayout.Label((tutorial.SkipPromptVisible ? "SKIP TOUR?" : "STEP " + (tutorial.StepIndex + 1) + " / " + tutorial.StepCount), _mutedTextStyle);
            GUILayout.Label(tutorial.SkipPromptVisible ? "END THE TOUR" : (tutorial.Title ?? "TUTORIAL"), _sectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.Label(tutorial.SkipPromptVisible ? "You can replay this from Workshop Help later." : (tutorial.Body ?? string.Empty), _textStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (tutorial.SkipPromptVisible)
            {
                DrawTutorialCardButton(tutorial.SkipCancelAction, 96f, 180f, 30f);
                GUILayout.FlexibleSpace();
                DrawTutorialCardButton(tutorial.SkipAction, 96f, 180f, 30f);
            }
            else
            {
                DrawTutorialCardButton(tutorial.BackAction, 72f, 150f, 30f);
                GUILayout.FlexibleSpace();
                DrawTutorialCardButton(tutorial.HelpAction, 64f, 150f, 28f);
                GUILayout.Space(6f);
                DrawTutorialCardButton(tutorial.SkipPromptAction, 84f, 170f, 28f);
                GUILayout.Space(6f);
                DrawTutorialCardButton(tutorial.NextAction, 72f, 150f, 30f);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawTutorialCardChrome(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.76f, 0.20f, 0.95f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.76f, 0.20f, 0.18f);
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 28f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private Rect ResolveTutorialCardRect(string key, Rect preferredRect, Rect bounds)
        {
            if (!string.Equals(_tutorialCardDragKey, key, StringComparison.Ordinal))
            {
                _tutorialCardDragKey = key;
                _tutorialCardDragging = false;
                _tutorialCardManualRect = RuntimeCompat.ZeroRect();
            }

            Rect rect = _tutorialCardManualRect.width > 0f && _tutorialCardManualRect.height > 0f
                ? _tutorialCardManualRect
                : preferredRect;

            Event evt = Event.current;
            Rect dragRect = new Rect(rect.x, rect.y, rect.width, 34f);
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0 && dragRect.Contains(evt.mousePosition))
            {
                _tutorialCardDragging = true;
                _tutorialCardDragOffset = new Vector2(evt.mousePosition.x - rect.x, evt.mousePosition.y - rect.y);
                evt.Use();
            }
            else if (evt != null && evt.type == EventType.MouseUp && evt.button == 0)
            {
                _tutorialCardDragging = false;
            }

            if (_tutorialCardDragging)
            {
                Vector2 mouse = Event.current != null ? Event.current.mousePosition : new Vector2(UnityEngine.Input.mousePosition.x, Screen.height - UnityEngine.Input.mousePosition.y);
                rect.x = mouse.x - _tutorialCardDragOffset.x;
                rect.y = mouse.y - _tutorialCardDragOffset.y;
                rect = ClampRectToBounds(rect, bounds);
                _tutorialCardManualRect = rect;
            }

            return ClampRectToBounds(rect, bounds);
        }

        private static Rect ClampRectToBounds(Rect rect, Rect bounds)
        {
            rect.x = Mathf.Clamp(rect.x, bounds.xMin + Margin, bounds.xMax - rect.width - Margin);
            rect.y = Mathf.Clamp(rect.y, bounds.yMin + Margin, bounds.yMax - rect.height - Margin);
            return rect;
        }
    }
}
