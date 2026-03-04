using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Centralized key-validation and parsing policy for all keybind flows.
    /// </summary>
    public static class KeyValidationPolicy
    {
        private static readonly HashSet<KeyCode> ReservedSystemKeys = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> MenuDisallowedKeys = new HashSet<KeyCode>
        {
            KeyCode.CapsLock,
            KeyCode.Home
        };

        static KeyValidationPolicy()
        {
            ReservedSystemKeys.Add(KeyCode.Escape);
            AddReservedIfPresent("LeftWindows");
            AddReservedIfPresent("RightWindows");
            AddReservedIfPresent("Menu");
            AddReservedIfPresent("SysReq");
            AddReservedIfPresent("Break");
        }

        public static bool IsKeyBindable(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (ReservedSystemKeys.Contains(key)) return false;
            return Enum.IsDefined(typeof(KeyCode), key);
        }

        public static bool IsValidForContext(KeyCode key, InputContext context)
        {
            if (key == KeyCode.None) return true; // Explicit unbind operation.
            if (!IsKeyBindable(key)) return false;

            switch (context)
            {
                case InputContext.Menu:
                    // Reserve a small subset of gameplay-critical toggles from being reused in menu context.
                    if (MenuDisallowedKeys.Contains(key)) return false;
                    return true;
                case InputContext.Gameplay:
                    return true;
                case InputContext.System:
                    return true;
                default:
                    return true;
            }
        }

        public static KeyCode ParseKeyCodeSafe(object rawValue, KeyCode fallback, string actionId, InputContext context)
        {
            KeyCode parsed;
            if (TryParseRawKey(rawValue, out parsed) && IsValidForContext(parsed, context))
            {
                return parsed;
            }

            // Preserve legacy/default reserved keys when they match the action fallback.
            if (TryParseRawKey(rawValue, out parsed) && parsed == fallback)
            {
                return fallback;
            }

            string rawText = rawValue == null ? "<null>" : rawValue.ToString();
            MMLog.WriteInfo("[KeyValidationPolicy] Keybind " + actionId + " had invalid value '" + rawText
                + "', using default " + fallback + ".");
            return fallback;
        }

        private static bool TryParseRawKey(object rawValue, out KeyCode parsed)
        {
            parsed = KeyCode.None;
            if (rawValue == null) return false;

            if (rawValue is KeyCode)
            {
                KeyCode kc = (KeyCode)rawValue;
                if (!Enum.IsDefined(typeof(KeyCode), kc)) return false;
                parsed = kc;
                return true;
            }

            if (rawValue is int)
            {
                int numeric = (int)rawValue;
                if (!Enum.IsDefined(typeof(KeyCode), numeric)) return false;
                parsed = (KeyCode)numeric;
                return true;
            }

            string raw = rawValue as string;
            if (raw == null) raw = rawValue.ToString();
            if (string.IsNullOrEmpty(raw)) return false;

            int parsedInt;
            if (int.TryParse(raw, out parsedInt))
            {
                if (!Enum.IsDefined(typeof(KeyCode), parsedInt)) return false;
                parsed = (KeyCode)parsedInt;
                return true;
            }

            try
            {
                object enumObj = Enum.Parse(typeof(KeyCode), raw, true);
                if (!(enumObj is KeyCode)) return false;
                KeyCode enumKey = (KeyCode)enumObj;
                if (!Enum.IsDefined(typeof(KeyCode), enumKey)) return false;
                parsed = enumKey;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AddReservedIfPresent(string enumName)
        {
            try
            {
                object enumObj = Enum.Parse(typeof(KeyCode), enumName, true);
                if (enumObj is KeyCode)
                    ReservedSystemKeys.Add((KeyCode)enumObj);
            }
            catch { }
        }
    }
}
