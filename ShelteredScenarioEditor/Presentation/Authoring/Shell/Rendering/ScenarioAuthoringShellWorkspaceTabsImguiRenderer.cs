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
        private void DrawWorkspaceTabsCore(Rect rect, string activeId, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            DrawChromePanel(rect, _headerStyle);
            ScenarioAuthoringShellWindowViewModel[] tabWindows = GetWorkspaceTabWindows(windows);
            if (tabWindows.Length == 0)
                return;

            const float gap = 4f;
            float moreWidth = Mathf.Clamp(ScenarioUiMeasuredLabel.Width("More >", _tabStyle, 16f), 82f, 112f);
            float usedWidth = 4f;
            int visibleCount = 0;
            for (int i = 0; i < tabWindows.Length; i++)
            {
                float measured = Math.Max(72f, ScenarioUiMeasuredLabel.Width(tabWindows[i] != null ? tabWindows[i].Title : string.Empty, _tabStyle, 16f));
                bool needsMore = i < tabWindows.Length - 1;
                if (usedWidth + measured + (needsMore ? moreWidth + gap : 0f) > rect.width - 4f)
                    break;
                usedWidth += measured + gap;
                visibleCount++;
            }
            bool overflow = visibleCount < tabWindows.Length;
            int activeIndex = -1;
            for (int i = 0; i < tabWindows.Length; i++)
                if (tabWindows[i] != null && string.Equals(tabWindows[i].Id, activeId, StringComparison.OrdinalIgnoreCase))
                    activeIndex = i;

            float x = rect.x + 4f;
            for (int slot = 0; slot < visibleCount; slot++)
            {
                int index = overflow && activeIndex >= visibleCount && slot == visibleCount - 1 ? activeIndex : slot;
                ScenarioAuthoringShellWindowViewModel window = tabWindows[index];
                float tabWidth = Math.Max(72f, ScenarioUiMeasuredLabel.Width(window != null ? window.Title : string.Empty, _tabStyle, 16f));
                Rect tabRect = new Rect(x, rect.y, Math.Min(tabWidth, rect.xMax - x), 36f);
                bool isActive = string.Equals(window.Id, activeId, StringComparison.OrdinalIgnoreCase);
                ShellUxCommand toggleCommand = ShellUxCommand.ToggleWindow(window.Id);
                ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
                {
                    Id = toggleCommand.AutomationId,
                    Command = toggleCommand,
                    Label = window.Title,
                    Hint = "Open the " + window.Title + " workspace.",
                    Enabled = true,
                    Emphasized = isActive
                };
                DrawButton(tabRect, action, true);
                if (isActive)
                    GUI.DrawTexture(new Rect(tabRect.x, tabRect.yMax - 4f, tabRect.width, 4f), ResolveWorkspaceRailTexture(window.Id));
                x = tabRect.xMax + gap;
            }

            if (overflow && x < rect.xMax)
            {
                ScenarioAuthoringInspectorAction moreAction = new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionShellToggleWindowMenu,
                    Command = ShellUxCommand.Simple(ShellUxCommandKind.ToggleWindowMenu, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu),
                    Label = "More >",
                    Hint = "Show remaining workspaces.",
                    Enabled = true,
                    Emphasized = false
                };
                DrawButton(new Rect(x, rect.y, Math.Min(moreWidth, rect.xMax - x), 36f), moreAction, true);
            }
        }

    }
}
