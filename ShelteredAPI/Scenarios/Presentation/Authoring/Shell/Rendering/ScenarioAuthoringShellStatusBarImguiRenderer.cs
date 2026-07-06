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
            DrawChromePanel(rect, _statusStyle);
            bool isPlaytesting = ScenarioAuthoringRuntimeGuards.IsPlaytesting();
            ScenarioAuthoringInspectorAction playtestAction = new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionPlaytest,
                Label = isPlaytesting ? "End Test" : "Playtest",
                Hint = isPlaytesting ? "Stop playtest and return to frozen authoring." : "Apply the current draft into the live world.",
                Enabled = true,
                Emphasized = isPlaytesting
            };
            ScenarioAuthoringInspectorAction pauseMenuAction = new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionOpenPauseMenu,
                Label = "Pause Menu",
                Hint = "Open the vanilla pause menu while the editor keeps the shelter paused.",
                Enabled = true,
                Emphasized = false
            };
            float playtestWidth = Mathf.Clamp(MeasureButtonWidth(playtestAction, false, 34f), 104f, 148f);
            float pauseMenuWidth = Mathf.Clamp(MeasureButtonWidth(pauseMenuAction, false, 34f), 128f, 168f);
            float rightControlsWidth = playtestWidth + pauseMenuWidth + 114f;
            float rightControlsX = Math.Max(rect.x + 220f, rect.xMax - rightControlsWidth);
            float statusRight = rightControlsX - 18f;
            float x = rect.x + 26f;
            int factCount = Math.Min(4, shell.StatusEntries != null ? shell.StatusEntries.Length : 0);
            for (int i = 0; i < factCount; i++)
            {
                string value = shell.StatusEntries[i] ?? string.Empty;
                float available = statusRight - x;
                if (available < 48f)
                    break;

                float width = Math.Min(Math.Min(250f, value.Length * 7.5f + 30f), available);
                GUI.Label(new Rect(x, rect.y + 14f, width, 20f), ShortenToFit(value, width, _mutedTextStyle), _mutedTextStyle);
                x += width + 18f;
            }

            string message = shell != null && shell.StatusEntries != null && shell.StatusEntries.Length > 4
                ? shell.StatusEntries[4]
                : null;
            if (!string.IsNullOrEmpty(message))
            {
                Rect messageRect = new Rect(x, rect.y + 13f, Math.Max(120f, statusRight - x), 22f);
                GUI.Label(messageRect, ShortenToFit(message, messageRect.width, _mutedTextStyle), _mutedTextStyle);
            }

            Rect playtestRect = new Rect(rightControlsX, rect.y + 8f, playtestWidth, 30f);
            DrawButton(playtestRect, playtestAction, false);

            Rect pauseMenuRect = new Rect(rect.xMax - pauseMenuWidth - 16f, rect.y + 8f, pauseMenuWidth, 30f);
            DrawButton(pauseMenuRect, pauseMenuAction, false);

            DrawStatusToastCore(rect, message);
        }

        private void DrawStatusToastCore(Rect statusRect, string message)
        {
            _animations.GetToastProgress(message);
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
            float progress = _animations.GetBinaryProgress("collapsed.window.strip", true, 0.16f, ScenarioUiEasing.EaseOut, false);
            using (ScenarioUiGuiScope.Apply(progress, stripRect, 1f))
            {
            DrawChromePanel(stripRect, _statusStyle);

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
            }

            return stripRect;
        }

    }
}
