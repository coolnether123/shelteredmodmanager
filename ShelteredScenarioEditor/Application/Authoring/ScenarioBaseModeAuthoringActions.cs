using System;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal static class ScenarioBaseModeAuthoringActions
    {
        public const string FocusedEditorKind = "base_mode";
        public const string ActionSwitchReloadPrefix = "scenario.mode.switch.reload.";
        public const string ActionSwitchOnlyPrefix = "scenario.mode.switch.only.";
        public const string ActionSwitchReloadKeepCastPrefix = "scenario.mode.switch.reload.keep_cast.";
        public const string ActionSwitchReloadDefaultFamilyPrefix = "scenario.mode.switch.reload.default_family.";
        public const string ActionSwitchOnlyKeepCastPrefix = "scenario.mode.switch.only.keep_cast.";
        public const string ActionSwitchOnlyDefaultFamilyPrefix = "scenario.mode.switch.only.default_family.";
        public const string ActionWatchOpeningCutscene = "scenario.mode.watch_opening_cutscene";
        public const string ActionSwitchCancel = "scenario.mode.switch.cancel";

        public static string SwitchReloadId(ScenarioBaseGameMode baseMode, string familyChoice)
        {
            string prefix = string.Equals(familyChoice, ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
                ? ActionSwitchReloadDefaultFamilyPrefix
                : ActionSwitchReloadKeepCastPrefix;
            return prefix + ((int)baseMode).ToString(CultureInfo.InvariantCulture);
        }

        public static string SwitchOnlyId(ScenarioBaseGameMode baseMode, string familyChoice)
        {
            string prefix = string.Equals(familyChoice, ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
                ? ActionSwitchOnlyDefaultFamilyPrefix
                : ActionSwitchOnlyKeepCastPrefix;
            return prefix + ((int)baseMode).ToString(CultureInfo.InvariantCulture);
        }

    }
}
