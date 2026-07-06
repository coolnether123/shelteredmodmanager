using System;
using System.Globalization;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioBaseModeAuthoringActions
    {
        public const string FocusedEditorKind = "base_mode";
        public const string ActionSwitchReloadPrefix = "scenario.mode.switch.reload.";
        public const string ActionSwitchOnlyPrefix = "scenario.mode.switch.only.";
        public const string ActionSwitchReloadKeepCastPrefix = "scenario.mode.switch.reload.keep_cast.";
        public const string ActionSwitchReloadDefaultFamilyPrefix = "scenario.mode.switch.reload.default_family.";
        public const string ActionSwitchOnlyKeepCastPrefix = "scenario.mode.switch.only.keep_cast.";
        public const string ActionSwitchOnlyDefaultFamilyPrefix = "scenario.mode.switch.only.default_family.";
        public const string ActionSwitchCancel = "scenario.mode.switch.cancel";

        public static string SwitchReload(ScenarioBaseGameMode baseMode, string familyChoice)
        {
            string prefix = string.Equals(familyChoice, ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
                ? ActionSwitchReloadDefaultFamilyPrefix
                : ActionSwitchReloadKeepCastPrefix;
            return prefix + ((int)baseMode).ToString(CultureInfo.InvariantCulture);
        }

        public static string SwitchOnly(ScenarioBaseGameMode baseMode, string familyChoice)
        {
            string prefix = string.Equals(familyChoice, ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
                ? ActionSwitchOnlyDefaultFamilyPrefix
                : ActionSwitchOnlyKeepCastPrefix;
            return prefix + ((int)baseMode).ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParseBaseMode(string actionId, string prefix, out ScenarioBaseGameMode baseMode)
        {
            baseMode = ScenarioBaseGameMode.Survival;
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(prefix) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            int value;
            if (!int.TryParse(actionId.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return false;

            if (!Enum.IsDefined(typeof(ScenarioBaseGameMode), value))
                return false;

            baseMode = (ScenarioBaseGameMode)value;
            return true;
        }
    }
}
