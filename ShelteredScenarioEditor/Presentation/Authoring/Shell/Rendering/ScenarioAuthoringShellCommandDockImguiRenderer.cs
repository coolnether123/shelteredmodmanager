using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
using ShelteredScenarioEditor.Presentation.UiKit;
using ShelteredScenarioEditor.Presentation.UiKit.Animation;
using ShelteredScenarioEditor.Presentation.UiKit.Frame;
using ShelteredScenarioEditor.Presentation.UiKit.Theme;
using ShelteredScenarioEditor.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private Rect DrawCommandDockCore(Rect contentRect, ScenarioAuthoringState state, float openProgress)
        {
            // TODO(centralize): Command dock is still a separate bottom-center action strip.
            // Merge these selection/build commands into the central workspace command area.
            if (state != null && state.ActiveTool == ScenarioAuthoringTool.Assets)
                return RuntimeCompat.ZeroRect();

            ScenarioAuthoringInspectorAction[] actions = BuildCommandDockActions(state);
            if (actions == null || actions.Length == 0)
                return RuntimeCompat.ZeroRect();

            float gap = 8f;
            float maxWidth = Math.Min(760f, contentRect.width - 40f);
            float buttonsWidth = 0f;
            for (int i = 0; i < actions.Length; i++)
                buttonsWidth += ResolveCommandDockButtonWidth(actions[i]);
            float naturalWidth = 20f + buttonsWidth + (gap * (actions.Length - 1));
            float width = Mathf.Clamp(naturalWidth, 280f, Math.Max(280f, maxWidth));
            Rect rect = new Rect(
                contentRect.x + ((contentRect.width - width) * 0.5f),
                contentRect.yMax - CommandDockHeight - 22f,
                width,
                CommandDockHeight);
            string signature = state != null
                ? state.ActiveTool.ToString() + ":" + (state.SelectedTarget != null ? state.SelectedTarget.Id : "none")
                : "none";
            float appear = openProgress * _animations.GetBinaryProgress("command.dock.visible", true, 0.14f, ScenarioUiEasing.EaseOut, false);
            float swap = 1f - _animations.GetPulseProgress("command.dock.content", signature, 0.16f, ScenarioUiEasing.EaseOut);
            Rect animatedRect = ResolveSlidingChromeRect(rect, openProgress, ScenarioUiSlideDirection.Down);
            using (ScenarioUiGuiScope.Apply(appear * Mathf.Clamp01(swap), animatedRect, 1f))
            {
            DrawChromePanel(animatedRect, _rootPanelStyle);
            float x = animatedRect.x + 10f;
            float availableButtonWidth = animatedRect.width - 20f - (gap * (actions.Length - 1));
            float overflow = Math.Max(0f, buttonsWidth - availableButtonWidth);
            for (int i = 0; i < actions.Length; i++)
            {
                float buttonWidth = ResolveCommandDockButtonWidth(actions[i]);
                if (overflow > 0f)
                {
                    float shrink = Math.Min(overflow, Math.Max(0f, buttonWidth - 88f));
                    buttonWidth -= shrink;
                    overflow -= shrink;
                }
                DrawButton(new Rect(x, animatedRect.y + 8f, buttonWidth, 32f), actions[i], false);
                x += buttonWidth + gap;
            }
            }
            return animatedRect;
        }

        private float ResolveCommandDockButtonWidth(ScenarioAuthoringInspectorAction action)
        {
            return Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 88f, 176f);
        }

        private static ScenarioAuthoringInspectorAction[] BuildCommandDockActions(ScenarioAuthoringState state)
        {
            ScenarioAuthoringTarget target = state != null ? state.SelectedTarget : null;
            bool hasTarget = target != null;
            bool authoredTarget = hasTarget && !string.IsNullOrEmpty(target.ScenarioReferenceId);
            bool canReplace = hasTarget && target.SupportsReplace;

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            actions.Add(new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionToolSelect,
                Command = ToolCommand.Select(ScenarioAuthoringTool.Select),
                Label = "Select",
                Hint = "Pick and inspect shelter objects.",
                Enabled = true,
                Emphasized = state != null && state.ActiveTool == ScenarioAuthoringTool.Select
            });

            if (!hasTarget)
            {
                actions.Add(DisabledAction("Pick Target", "Pick a live or authored object to edit object-specific rules. Use Build on the tool rail for placement."));
                return actions.ToArray();
            }

            actions.Add(new ScenarioAuthoringInspectorAction
            {
                Id = SpriteSwapCommand.OpenPicker().AutomationId,
                Command = SpriteSwapCommand.OpenPicker(),
                Label = "Edit Art",
                Hint = canReplace ? "Open the selected visual in the art editor." : "This target has no replaceable visual.",
                Enabled = canReplace
            });
            actions.Add(new ScenarioAuthoringInspectorAction
            {
                Id = authoredTarget
                    ? SceneSpritePlacementCommand.Remove().AutomationId
                    : SelectionCommand.Clear().AutomationId,
                Command = authoredTarget ? (ScenarioAuthoringCommand)SceneSpritePlacementCommand.Remove() : SelectionCommand.Clear(),
                Label = authoredTarget ? "Remove" : "Clear",
                Hint = authoredTarget ? "Remove this authored placement from the draft." : "Clear the current selection.",
                Enabled = true,
                Emphasized = authoredTarget
            });
            return actions.ToArray();
        }

        private static ScenarioAuthoringInspectorAction DisabledAction(string label, string hint)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Label = label,
                Hint = hint,
                Enabled = false
            };
        }

        private static ScenarioAuthoringInspectorAction CloneEmphasized(ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = action.Id,
                Command = action.Command,
                Label = action.Label,
                Hint = action.Hint,
                Detail = action.Detail,
                Badge = action.Badge,
                IconText = action.IconText,
                PreviewSprite = action.PreviewSprite,
                Enabled = action.Enabled,
                DisabledReason = action.DisabledReason,
                Emphasized = true
            };
        }

        private static ScenarioAuthoringInspectorAction CloneWithLabel(ScenarioAuthoringInspectorAction action, string label)
        {
            if (action == null)
                return null;

            return new ScenarioAuthoringInspectorAction
            {
                Id = action.Id,
                Command = action.Command,
                Label = label,
                Hint = action.Hint,
                Detail = action.Detail,
                Badge = action.Badge,
                IconText = action.IconText,
                PreviewSprite = action.PreviewSprite,
                Enabled = action.Enabled,
                Emphasized = action.Emphasized
            };
        }

        private static bool IsChildStageTab(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && !string.IsNullOrEmpty(action.Label)
                && action.Label.StartsWith("- ", StringComparison.Ordinal);
        }

        private static string CleanChildStageLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;

            return label.StartsWith("- ", StringComparison.Ordinal) ? label.Substring(2) : label;
        }

    }
}
