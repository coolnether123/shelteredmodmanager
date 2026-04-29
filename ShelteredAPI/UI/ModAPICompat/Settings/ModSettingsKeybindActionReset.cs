using System;
using ModAPI.Spine;

namespace ModAPI.Internal.UI
{
    internal interface IKeybindActionResetProvider
    {
        bool ResetKeybindActionToDefault(string actionId);
    }

    internal static class ModSettingsKeybindActionReset
    {
        internal static bool Reset(
            ISettingsProvider provider,
            SettingDefinition primaryDef,
            SettingDefinition secondaryDef,
            object settingsObject,
            Func<SettingDefinition, object, object, bool> applySettingValue)
        {
            string settingId = primaryDef != null ? primaryDef.Id : (secondaryDef != null ? secondaryDef.Id : null);
            string actionId = ModSettingsKeybindLayout.GetKeybindActionBaseId(settingId);
            var resetProvider = provider as IKeybindActionResetProvider;
            if (resetProvider != null && !string.IsNullOrEmpty(actionId))
                return resetProvider.ResetKeybindActionToDefault(actionId);

            if (applySettingValue == null)
                return false;

            bool changed = false;
            if (primaryDef != null)
                changed |= applySettingValue(primaryDef, settingsObject, primaryDef.DefaultValue);
            if (secondaryDef != null)
                changed |= applySettingValue(secondaryDef, settingsObject, secondaryDef.DefaultValue);

            var persistentProvider = provider as ISettingsProvider2;
            if (changed && persistentProvider != null)
                persistentProvider.Save();

            return changed;
        }
    }
}
