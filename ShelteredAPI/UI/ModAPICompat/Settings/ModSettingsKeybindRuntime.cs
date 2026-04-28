using System;
using ModAPI.Core;
using ModAPI.Spine;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal static class ModSettingsKeybindRuntime
    {
        internal static bool ApplySettingValue(SettingDefinition def, object settingsObject, object newValue)
        {
            if (def == null)
                return false;

            try
            {
                if (def.Validate != null && !def.Validate(newValue, settingsObject))
                    return false;

                if (def.Setter != null)
                    def.Setter(settingsObject, newValue);

                if (def.OnChanged != null)
                    def.OnChanged(settingsObject);

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ModSettingsPanel] Failed to apply keybind value for " + def.Id + ": " + ex.Message);
                return false;
            }
        }

        internal static KeyCode ReadKeyCode(SettingDefinition def, object settingsObject)
        {
            if (def == null)
                return KeyCode.None;

            object value = null;
            try
            {
                if (def.Getter != null)
                    value = def.Getter(settingsObject);
            }
            catch
            {
            }

            if (value is KeyCode)
                return (KeyCode)value;
            if (value is int)
                return (KeyCode)(int)value;

            if (value != null)
            {
                try
                {
                    return (KeyCode)Enum.Parse(typeof(KeyCode), value.ToString(), true);
                }
                catch
                {
                }
            }

            if (def.DefaultValue is KeyCode)
                return (KeyCode)def.DefaultValue;

            return KeyCode.None;
        }
    }
}
