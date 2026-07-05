using System;
using System.Collections.Generic;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;

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
            bool visible = tutorial != null && tutorial.Visible;
            float progress = _animations.GetTutorialOverlayProgress(visible);
            if (!visible || progress <= 0.001f)
                return;

            Rect targetRect = ResolveTutorialTargetRect(tutorial, topRect, statusRect, windowRects, shell);
            if (targetRect.width > 0f && targetRect.height > 0f)
                DrawTutorialTargetHighlight(targetRect, progress);

            Rect calloutRect = BuildTutorialCalloutRect(availableRect, targetRect);
            Rect animatedRect = new Rect(calloutRect.x, calloutRect.y - ((1f - progress) * 8f), calloutRect.width, calloutRect.height);
            using (ScenarioUiGuiScope.Apply(progress, animatedRect, 1f))
                DrawTutorialCallout(animatedRect, tutorial);

            inputCapture.RegisterInteractiveRect(calloutRect);
            inputCapture.SetPopupOpen(true);
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

                ScenarioAuthoringInspectorAction displayTab = compact ? CloneWithLabel(tab, CompactStageLabel(tab.Label)) : tab;
                float width = ResolvePrimaryStageTabWidth(displayTab, compact);
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
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
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
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f));
            GUILayout.Label("STEP " + (tutorial.StepIndex + 1) + " / " + tutorial.StepCount, _mutedTextStyle);
            GUILayout.Label(tutorial.Title ?? "TUTORIAL", _sectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.Label(tutorial.Body ?? string.Empty, _textStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            DrawButton(GUILayoutUtility.GetRect(136f, 30f, GUILayout.Width(136f), GUILayout.Height(30f)), tutorial.PrimaryAction, false);
            GUILayout.FlexibleSpace();
            DrawButton(GUILayoutUtility.GetRect(70f, 28f, GUILayout.Width(70f), GUILayout.Height(28f)), tutorial.HelpAction, false);
            DrawButton(GUILayoutUtility.GetRect(70f, 28f, GUILayout.Width(70f), GUILayout.Height(28f)), tutorial.SkipAction, false);
            DrawButton(GUILayoutUtility.GetRect(70f, 28f, GUILayout.Width(70f), GUILayout.Height(28f)), tutorial.NextAction, false);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
