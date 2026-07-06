using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
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
            string playStartReason = null;
            bool canStartPlay = isPlaytesting || CanStartPlay(out playStartReason);
            ScenarioAuthoringInspectorAction playtestAction = new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionPlaytest,
                Label = isPlaytesting ? "End Test" : "Playtest",
                Hint = isPlaytesting ? "Stop playtest and return to frozen authoring." : canStartPlay ? "Apply the current draft into the live world." : playStartReason,
                Enabled = canStartPlay,
                Emphasized = isPlaytesting,
                DisabledReason = canStartPlay ? null : playStartReason
            };
            ScenarioAuthoringInspectorAction pauseMenuAction = new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionOpenPauseMenu,
                Label = "Pause Menu",
                Hint = "Open the vanilla pause menu while the editor keeps the shelter paused.",
                Enabled = true,
                Emphasized = false
            };
            float playtestWidth = Math.Max(104f, MeasureButtonWidth(playtestAction, false, 34f));
            float pauseMenuWidth = Math.Max(128f, MeasureButtonWidth(pauseMenuAction, false, 34f));
            float rightPadding = 16f;
            float buttonGap = 104f;
            Rect pauseMenuRect = new Rect(rect.xMax - pauseMenuWidth - rightPadding, rect.y + 8f, pauseMenuWidth, 30f);
            Rect playtestRect = new Rect(pauseMenuRect.x - buttonGap - playtestWidth, rect.y + 8f, playtestWidth, 30f);
            float statusRight = playtestRect.x - 14f;
            float x = rect.x + 26f;
            string message = null;
            for (int i = 0; shell != null && shell.StatusEntries != null && i < shell.StatusEntries.Length; i++)
            {
                string value = shell.StatusEntries[i] ?? string.Empty;
                if (!IsPrimaryStatusFact(value))
                {
                    if (message == null && !IsSecondaryStatusFact(value))
                        message = value;
                    continue;
                }

                float available = statusRight - x;
                if (available < 48f)
                    break;

                float measuredWidth = ScenarioUiMeasuredLabel.Width(value, _mutedTextStyle, 16f);
                float width = Math.Min(measuredWidth, available);
                DrawStatusLabel(new Rect(x, rect.y + 14f, width, 20f), value, false);
                x += width + 18f;
            }

            if (!string.IsNullOrEmpty(message))
            {
                Rect messageRect = new Rect(x, rect.y + 13f, Math.Max(0f, statusRight - x), 22f);
                if (messageRect.width > 8f)
                    DrawStatusLabel(messageRect, message, true);
            }

            DrawButton(playtestRect, playtestAction, false);
            DrawButton(pauseMenuRect, pauseMenuAction, false);

            DrawStatusToastCore(rect, message);
        }

        private void DrawPlaytestControlStripCore(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            DrawChromePanel(rect, _statusStyle);
            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            bool reloadPending = state != null && state.ReloadPending;
            string reloadReason = reloadPending && !string.IsNullOrEmpty(state.ReloadPendingReason)
                ? state.ReloadPendingReason
                : "Scenario world is reloading; controls are disabled until the editor reconnects.";
            ScenarioAuthoringInspectorAction stopAction = new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionPlaytest,
                Label = reloadPending ? "Stop Playtest" : "Stop Playtest",
                Hint = reloadPending ? reloadReason : "Stop playtest and restore frozen authoring.",
                Enabled = !reloadPending,
                Emphasized = !reloadPending,
                DisabledReason = reloadPending ? reloadReason : null
            };
            ScenarioAuthoringInspectorAction restartAction = new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionPlaytestRestart,
                Label = "Restart",
                Hint = reloadPending ? reloadReason : "Save the draft and reload the authored world. This is a full restart, not an in-place tick rewind.",
                Enabled = !reloadPending,
                Emphasized = false,
                DisabledReason = reloadPending ? reloadReason : null
            };

            float rightPadding = 16f;
            float stopWidth = Math.Max(132f, MeasureButtonWidth(stopAction, false, 34f));
            float restartWidth = Math.Max(104f, MeasureButtonWidth(restartAction, false, 34f));
            Rect stopRect = new Rect(rect.xMax - stopWidth - rightPadding, rect.y + 8f, stopWidth, 30f);
            Rect restartRect = new Rect(stopRect.x - restartWidth - 8f, rect.y + 8f, restartWidth, 30f);
            float statusRight = restartRect.x - 14f;

            float x = rect.x + 26f;
            if (reloadPending)
            {
                DrawStatusLabel(new Rect(x, rect.y + 14f, Math.Max(80f, statusRight - x), 20f), "Restarting playtest...", true);
            }
            else
            {
                DrawStatusLabel(new Rect(x, rect.y + 14f, 124f, 20f), "Playtest running", false);
                x += 142f;
                DrawStatusLabel(new Rect(x, rect.y + 14f, 160f, 20f), "Day " + GameTime.Day + " " + GameTime.Hour.ToString("D2") + ":" + GameTime.Minute.ToString("D2"), false);
                x += 178f;
                string seed = "ModRandom seed: " + ModRandom.CurrentSeed.ToString();
                if (statusRight - x > 140f)
                    DrawStatusLabel(new Rect(x, rect.y + 14f, statusRight - x, 20f), seed, true);
            }

            DrawButton(restartRect, restartAction, false);
            DrawButton(stopRect, stopAction, false);

            string message = reloadPending ? reloadReason : null;
            for (int i = 0; shell != null && shell.StatusEntries != null && i < shell.StatusEntries.Length; i++)
            {
                string value = shell.StatusEntries[i] ?? string.Empty;
                if (!IsPrimaryStatusFact(value) && !IsSecondaryStatusFact(value))
                {
                    if (!reloadPending)
                        message = value;
                    break;
                }
            }

            DrawStatusToastCore(rect, message);
        }

        private static bool IsPrimaryStatusFact(string value)
        {
            return StartsWithStatusPrefix(value, "Workspace:")
                || StartsWithStatusPrefix(value, "Mode:")
                || StartsWithStatusPrefix(value, "Grid:")
                || StartsWithStatusPrefix(value, "Placing:");
        }

        private static bool IsSecondaryStatusFact(string value)
        {
            return StartsWithStatusPrefix(value, "Layer:")
                || StartsWithStatusPrefix(value, "Hover:");
        }

        private static bool StartsWithStatusPrefix(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanStartPlay(out string reason)
        {
            reason = null;
            try
            {
                ScenarioEditorController controller = ScenarioEditorController.Instance;
                ScenarioEditorSession session = controller != null ? controller.CurrentSession : null;
                return new ScenarioPlayStartReadiness().CanStartPlay(session != null ? session.WorkingDefinition : null, out reason);
            }
            catch (Exception ex)
            {
                reason = "Playtest readiness could not be checked: " + ex.Message;
                return false;
            }
        }

        private void DrawStatusLabel(Rect rect, string value, bool tooltipWhenTruncated)
        {
            string fitted = ShortenToFit(value, rect.width, _mutedTextStyle);
            string tooltip = tooltipWhenTruncated && !string.Equals(fitted, value ?? string.Empty, StringComparison.Ordinal)
                ? value ?? string.Empty
                : string.Empty;
            GUI.Label(rect, new GUIContent(fitted, tooltip), _mutedTextStyle);
        }

        private void DrawStatusToastCore(Rect statusRect, string message)
        {
            _animations.GetToastProgress(message);
        }

        private Rect DrawCollapsedWindowStripCore(Rect statusRect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            // TODO(centralize): Collapsed window restore strip belongs to the old multi-window
            // shell. Replace with central workspace navigation/state once windows are merged.
            int count = 0;
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (ShouldRenderCollapsedStatusChip(window))
                    count++;
            }

            if (count == 0)
                return RuntimeCompat.ZeroRect();

            float measuredWidth = 18f;
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (!ShouldRenderCollapsedStatusChip(window))
                    continue;

                measuredWidth += Math.Max(96f, ScenarioUiMeasuredLabel.Width(window.Title ?? string.Empty, _buttonStyle, 24f)) + 6f;
            }

            float width = Math.Min(Math.Max(220f, measuredWidth), Math.Max(220f, statusRect.width - 28f));
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
                if (!ShouldRenderCollapsedStatusChip(window))
                    continue;

                float buttonWidth = Math.Max(96f, ScenarioUiMeasuredLabel.Width(window.Title ?? string.Empty, _buttonStyle, 24f));
                buttonWidth = Math.Min(buttonWidth, stripRect.xMax - x - 6f);
                if (buttonWidth < 56f)
                    break;

                DrawButton(new Rect(x, stripRect.y + 4f, buttonWidth, 22f), new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionWindowRestorePrefix + window.Id,
                    Label = window.Title,
                    Hint = "Restore the " + window.Title + " window.",
                    Enabled = true
                }, false);
                x += buttonWidth + 6f;
            }
            }

            return stripRect;
        }

        private static bool ShouldRenderCollapsedStatusChip(ScenarioAuthoringShellWindowViewModel window)
        {
            if (window == null || !window.Collapsed)
                return false;

            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

    }
}
