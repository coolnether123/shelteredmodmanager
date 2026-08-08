using System;
using System.Globalization;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum ShellUxCommandKind
    {
        SelectStage,
        ToggleWindow,
        CollapseWindow,
        RestoreWindow,
        SelectInspectorTab,
        ToggleInspectorPin,
        ToggleSetting,
        StepSetting,
        SelectSetting,
        ToggleShell,
        ShowShell,
        ReturnFromVanilla,
        HideAll,
        ResetLayout,
        FocusSelection,
        ToggleWindowMenu,
        OpenSettings,
        OpenHelp,
        OpenShortcuts,
        ShowHelpPages,
        OpenTimeline,
        CloseSettings,
        CloseHelp,
        ResetSettings,
        ToggleGlobalSearch,
        CloseGlobalSearch,
        OpenHelpTopic,
        StartTour,
        TourNext,
        TourBack,
        TourExit,
        DismissSetup,
        TutorialOpenTarget,
        TutorialNext,
        TutorialBack,
        TutorialSkipPrompt,
        TutorialSkipCancel,
        TutorialSkip,
        TutorialReset,
        HelpPageNext,
        HelpPagePrevious,
        SetLaunchMode,
        ToggleLaunchSelectable,
        StepLaunchValue,
        ToggleChecklistItem,
        SetChecklistNote
    }

    internal sealed class ShellUxCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private ShellUxCommand(
            ShellUxCommandKind kind,
            string automationId,
            string key,
            string value,
            int delta,
            ScenarioStageKind stage,
            ScenarioAuthoringInspectorTab inspectorTab,
            ScenarioLaunchSetupMode launchMode)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            Key = key;
            Value = value;
            Delta = delta;
            Stage = stage;
            InspectorTab = inspectorTab;
            LaunchMode = launchMode;
        }

        public ShellUxCommandKind Kind { get; private set; }
        public string Key { get; private set; }
        public string Value { get; private set; }
        public int Delta { get; private set; }
        public ScenarioStageKind Stage { get; private set; }
        public ScenarioAuthoringInspectorTab InspectorTab { get; private set; }
        public ScenarioLaunchSetupMode LaunchMode { get; private set; }

        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            return Kind == ShellUxCommandKind.SetChecklistNote
                ? ChecklistNote(Key, value)
                : this;
        }

        public static ShellUxCommand Simple(ShellUxCommandKind kind, string automationId)
        {
            return Create(kind, automationId, null, null, 0, ScenarioStageKind.None, ScenarioAuthoringInspectorTab.Properties, default(ScenarioLaunchSetupMode));
        }

        public static ShellUxCommand Keyed(ShellUxCommandKind kind, string automationId, string key)
        {
            return Create(kind, automationId, key, null, 0, ScenarioStageKind.None, ScenarioAuthoringInspectorTab.Properties, default(ScenarioLaunchSetupMode));
        }

        public static ShellUxCommand Valued(ShellUxCommandKind kind, string automationId, string key, string value)
        {
            return Create(kind, automationId, key, value, 0, ScenarioStageKind.None, ScenarioAuthoringInspectorTab.Properties, default(ScenarioLaunchSetupMode));
        }

        public static ShellUxCommand Stepped(ShellUxCommandKind kind, string automationId, string key, int delta)
        {
            return Create(kind, automationId, key, null, delta, ScenarioStageKind.None, ScenarioAuthoringInspectorTab.Properties, default(ScenarioLaunchSetupMode));
        }

        public static ShellUxCommand ForStage(string automationId, ScenarioStageKind stage)
        {
            return Create(ShellUxCommandKind.SelectStage, automationId, null, null, 0, stage, ScenarioAuthoringInspectorTab.Properties, default(ScenarioLaunchSetupMode));
        }

        public static ShellUxCommand ForStage(string automationId, ScenarioStageKind stage, string statusMessage)
        {
            return Create(ShellUxCommandKind.SelectStage, automationId, null, statusMessage, 0, stage, ScenarioAuthoringInspectorTab.Properties, default(ScenarioLaunchSetupMode));
        }

        public static ShellUxCommand ForInspectorTab(string automationId, ScenarioAuthoringInspectorTab tab)
        {
            return Create(ShellUxCommandKind.SelectInspectorTab, automationId, null, null, 0, ScenarioStageKind.None, tab, default(ScenarioLaunchSetupMode));
        }

        public static ShellUxCommand ForLaunchMode(string automationId, ScenarioLaunchSetupMode mode)
        {
            return Create(ShellUxCommandKind.SetLaunchMode, automationId, null, null, 0, ScenarioStageKind.None, ScenarioAuthoringInspectorTab.Properties, mode);
        }

        public static ShellUxCommand SelectStage(ScenarioStageKind stage)
        {
            return ForStage(ScenarioAuthoringActionIds.ActionStageSelectPrefix + stage, stage);
        }

        public static ShellUxCommand ToggleWindow(string windowId)
        {
            return Keyed(ShellUxCommandKind.ToggleWindow, ScenarioAuthoringActionIds.ActionWindowTogglePrefix + windowId, windowId);
        }

        public static ShellUxCommand CollapseWindow(string windowId)
        {
            return Keyed(ShellUxCommandKind.CollapseWindow, ScenarioAuthoringActionIds.ActionWindowCollapsePrefix + windowId, windowId);
        }

        public static ShellUxCommand RestoreWindow(string windowId)
        {
            return Keyed(ShellUxCommandKind.RestoreWindow, ScenarioAuthoringActionIds.ActionWindowRestorePrefix + windowId, windowId);
        }

        public static ShellUxCommand InspectorPin(string key)
        {
            return Keyed(ShellUxCommandKind.ToggleInspectorPin, ScenarioAuthoringActionIds.ActionInspectorPinTogglePrefix + key, key);
        }

        public static ShellUxCommand SettingToggle(string settingId)
        {
            return Keyed(ShellUxCommandKind.ToggleSetting, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + settingId, settingId);
        }

        public static ShellUxCommand SettingStep(string settingId, int delta)
        {
            string prefix = delta < 0 ? ScenarioAuthoringActionIds.ActionSettingDecreasePrefix : ScenarioAuthoringActionIds.ActionSettingIncreasePrefix;
            return Stepped(ShellUxCommandKind.StepSetting, prefix + settingId, settingId, delta < 0 ? -1 : 1);
        }

        public static ShellUxCommand SettingChoice(string settingId, string value)
        {
            return Valued(ShellUxCommandKind.SelectSetting, ScenarioAuthoringActionIds.ActionSettingSelectPrefix + settingId + "." + ScenarioAutomationIdCodec.EncodeToken(value), settingId, value);
        }

        public static ShellUxCommand HelpTopic(string topicId)
        {
            return Keyed(ShellUxCommandKind.OpenHelpTopic, ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + topicId, topicId);
        }

        public static ShellUxCommand Tour(string tourId)
        {
            return Keyed(ShellUxCommandKind.StartTour, ScenarioAuthoringActionIds.ActionTourStartPrefix + tourId, tourId);
        }

        public static ShellUxCommand SetLaunchMode(ScenarioLaunchSetupMode mode)
        {
            return ForLaunchMode("launch_setup.mode." + mode, mode);
        }

        public static ShellUxCommand LaunchSelectable(string categoryId)
        {
            return Keyed(ShellUxCommandKind.ToggleLaunchSelectable, "launch_setup.selectable." + categoryId, categoryId);
        }

        public static ShellUxCommand LaunchValue(string categoryId, int delta)
        {
            return Stepped(ShellUxCommandKind.StepLaunchValue, "launch_setup.value." + categoryId + "." + delta.ToString(CultureInfo.InvariantCulture), categoryId, delta);
        }

        public static ShellUxCommand ChecklistToggle(string itemId)
        {
            return Keyed(ShellUxCommandKind.ToggleChecklistItem, ScenarioAuthorTestChecklistService.ToggleActionPrefix + itemId, itemId);
        }

        public static ShellUxCommand ChecklistNote(string itemId, string note)
        {
            return Valued(ShellUxCommandKind.SetChecklistNote, ScenarioAuthorTestChecklistService.NoteActionPrefix + itemId + "." + ScenarioAutomationIdCodec.EncodeToken(note), itemId, note);
        }

        private static ShellUxCommand Create(ShellUxCommandKind kind, string automationId, string key, string value, int delta, ScenarioStageKind stage, ScenarioAuthoringInspectorTab inspectorTab, ScenarioLaunchSetupMode launchMode)
        {
            return new ShellUxCommand(kind, automationId, key, value, delta, stage, inspectorTab, launchMode);
        }
    }

}
