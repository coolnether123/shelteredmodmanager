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
        private Rect DrawCommandDockCore(Rect contentRect, ScenarioAuthoringState state)
        {
            if (state != null && state.ActiveTool == ScenarioAuthoringTool.Assets)
                return RuntimeCompat.ZeroRect();

            ScenarioAuthoringInspectorAction[] actions = BuildCommandDockActions(state);
            if (actions == null || actions.Length == 0)
                return RuntimeCompat.ZeroRect();

            float gap = 8f;
            float buttonsWidth = 0f;
            for (int i = 0; i < actions.Length; i++)
                buttonsWidth += Mathf.Clamp(MeasureButtonWidth(actions[i], false, 24f), 104f, 168f);
            float width = Mathf.Clamp(20f + buttonsWidth + (gap * (actions.Length - 1)), 360f, Math.Min(760f, contentRect.width - 40f));
            Rect rect = new Rect(
                contentRect.x + ((contentRect.width - width) * 0.5f),
                contentRect.yMax - CommandDockHeight - 22f,
                width,
                CommandDockHeight);
            string signature = state != null
                ? state.ActiveTool.ToString() + ":" + (state.SelectedTarget != null ? state.SelectedTarget.Id : "none")
                : "none";
            float appear = _animations.GetBinaryProgress("command.dock.visible", true, 0.14f, ScenarioUiEasing.EaseOut, false);
            float swap = 1f - _animations.GetPulseProgress("command.dock.content", signature, 0.16f, ScenarioUiEasing.EaseOut);
            using (ScenarioUiGuiScope.Apply(appear * Mathf.Clamp01(swap), rect, 1f))
            {
            DrawChromePanel(rect, _rootPanelStyle);
            float x = rect.x + 10f;
            for (int i = 0; i < actions.Length; i++)
            {
                float buttonWidth = Mathf.Clamp(MeasureButtonWidth(actions[i], false, 24f), 104f, 168f);
                DrawButton(new Rect(x, rect.y + 8f, buttonWidth, 32f), actions[i], false);
                x += buttonWidth + gap;
            }
            }
            return rect;
        }

        private static ScenarioAuthoringInspectorAction[] BuildCommandDockActions(ScenarioAuthoringState state)
        {
            ScenarioAuthoringTarget target = state != null ? state.SelectedTarget : null;
            bool hasTarget = target != null;
            bool authoredTarget = hasTarget && !string.IsNullOrEmpty(target.ScenarioReferenceId);
            bool canReplace = hasTarget && target.SupportsReplace;
            bool insideLayer = state != null && state.ActiveStage == ScenarioStageKind.BunkerInside;

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            actions.Add(new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionToolSelect,
                Label = "Select",
                Hint = "Pick and inspect shelter objects.",
                Enabled = true,
                Emphasized = state != null && state.ActiveTool == ScenarioAuthoringTool.Select
            });

            if (!hasTarget)
            {
                actions.Add(new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionToolAssets,
                    Label = "Place Art",
                    Hint = "Open the scenario art tray for snapped scene assets.",
                    Enabled = true
                });
                actions.Add(new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionCaptureShelterObjects,
                    Label = "Capture Live",
                    Hint = insideLayer
                        ? "Capture the current live shelter object layout into the draft."
                        : "Switch to Interior before capturing live shelter objects.",
                    Enabled = insideLayer
                });
                actions.Add(DisabledAction("No Selection", "Pick a live or authored object to edit object-specific rules."));
                return actions.ToArray();
            }

            actions.Add(new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionCaptureSelectedObject,
                Label = authoredTarget ? "Refresh" : "Capture",
                Hint = authoredTarget ? "Refresh this authored placement from the live object." : "Capture this live object into the scenario draft.",
                Enabled = true,
                Emphasized = !authoredTarget
            });
            actions.Add(new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                Label = "Edit Art",
                Hint = canReplace ? "Open the selected visual in the art editor." : "This target has no replaceable visual.",
                Enabled = canReplace
            });
            actions.Add(new ScenarioAuthoringInspectorAction
            {
                Id = authoredTarget
                    ? ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove
                    : ScenarioAuthoringActionIds.ActionSelectionClear,
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
                Label = action.Label,
                Hint = action.Hint,
                Detail = action.Detail,
                Badge = action.Badge,
                IconText = action.IconText,
                PreviewSprite = action.PreviewSprite,
                Enabled = action.Enabled,
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
