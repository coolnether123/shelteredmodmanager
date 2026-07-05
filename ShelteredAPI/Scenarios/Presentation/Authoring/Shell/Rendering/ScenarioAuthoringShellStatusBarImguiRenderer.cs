using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
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
        private void DrawStatusBarCore(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            GUI.Box(rect, GUIContent.none, _statusStyle);
            const float rightControlsWidth = 528f;
            float rightControlsX = Math.Max(rect.x + 220f, rect.xMax - rightControlsWidth);
            float statusRight = rightControlsX - 18f;
            float x = rect.x + 26f;
            for (int i = 0; shell.StatusEntries != null && i < shell.StatusEntries.Length; i++)
            {
                string value = shell.StatusEntries[i] ?? string.Empty;
                float available = statusRight - x;
                if (available < 48f)
                    break;

                float width = Math.Min(Math.Min(250f, value.Length * 7.5f + 30f), available);
                GUI.Label(new Rect(x, rect.y + 14f, width, 20f), ShortenToFit(value, width, _mutedTextStyle), _mutedTextStyle);
                x += width + 18f;
            }

            bool isPlaytesting = ScenarioAuthoringRuntimeGuards.IsPlaytesting();
            Rect playtestRect = new Rect(rightControlsX, rect.y + 9f, 120f, 28f);
            DrawButton(playtestRect, new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionPlaytest,
                Label = isPlaytesting ? "End Test" : "Playtest",
                Hint = isPlaytesting ? "Stop playtest and return to frozen authoring." : "Apply the current draft into the live world.",
                Enabled = true,
                Emphasized = isPlaytesting
            }, false);

            Rect pauseMenuRect = new Rect(rect.xMax - 144f, rect.y + 9f, 128f, 28f);
            DrawButton(pauseMenuRect, new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionOpenPauseMenu,
                Label = "Pause Menu",
                Hint = "Open the vanilla pause menu while the editor keeps the shelter paused.",
                Enabled = true,
                Emphasized = false
            }, false);

            if (pauseMenuRect.x - (rightControlsX + 156f) > 120f)
            {
                GUI.Label(new Rect(rightControlsX + 156f, rect.y + 14f, 18f, 18f), "-", _mutedTextStyle);
                GUI.Box(new Rect(rightControlsX + 184f, rect.y + 20f, 80f, 4f), GUIContent.none, _uiContext.Styles.Field);
                GUI.Label(new Rect(rightControlsX + 278f, rect.y + 14f, 48f, 18f), "100%", _textStyle);
            }

            string toast = shell != null && shell.StatusEntries != null && shell.StatusEntries.Length > 0
                ? shell.StatusEntries[0]
                : null;
            DrawStatusToastCore(rect, toast);
        }

        private void DrawStatusToastCore(Rect statusRect, string message)
        {
            float progress = _animations.GetToastProgress(message);
            if (string.IsNullOrEmpty(message) || progress <= 0.001f)
                return;

            float width = Mathf.Clamp((message.Length * 7.5f) + 34f, 220f, 520f);
            Rect rect = new Rect(
                statusRect.x + 24f,
                statusRect.y - Mathf.Lerp(0f, 40f, progress),
                width,
                30f);
            using (ScenarioUiGuiScope.Apply(progress, rect, 1f))
            {
                GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 18f), ShortenToFit(message, rect.width - 24f, _mutedTextStyle), _mutedTextStyle);
            }
        }

        private Rect DrawCollapsedWindowStripCore(Rect statusRect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            int count = 0;
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && window.Collapsed)
                    count++;
            }

            if (count == 0)
                return RuntimeCompat.ZeroRect();

            float width = Math.Min(620f, Math.Max(220f, count * 132f + 18f));
            Rect stripRect = new Rect(
                statusRect.x + 14f,
                statusRect.y - 36f,
                width,
                30f);
            GUI.Box(stripRect, GUIContent.none, _statusStyle);

            float x = stripRect.x + 6f;
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window == null || !window.Collapsed)
                    continue;

                float buttonWidth = Math.Min(126f, stripRect.xMax - x - 6f);
                if (buttonWidth < 56f)
                    break;

                DrawButton(new Rect(x, stripRect.y + 4f, buttonWidth, 22f), new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionWindowRestorePrefix + window.Id,
                    Label = Shorten(window.Title, 16),
                    Hint = "Restore the " + window.Title + " window.",
                    Enabled = true
                }, false);
                x += buttonWidth + 6f;
            }

            return stripRect;
        }

    }
}
