using System;
using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Shared{
    /// <summary>Canonical case-insensitive accessors for serialized scenario property lists.</summary>
    public static class ScenarioPropertyBag
    {
        /// <summary>Sets an existing key or appends it when missing.</summary>
        public static void Set(List<ScenarioProperty> properties, string key, string value)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    property.Value = value;
                    return;
                }
            }

            properties.Add(new ScenarioProperty { Key = key, Value = value });
        }

        /// <summary>Gets a string value, or null when the key is absent.</summary>
        public static string GetString(List<ScenarioProperty> properties, string key)
        {
            return GetString(properties, key, null);
        }

        /// <summary>Gets a string value with an explicit fallback.</summary>
        public static string GetString(List<ScenarioProperty> properties, string key, string fallback)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return fallback;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value ?? fallback;
            }

            return fallback;
        }

        /// <summary>Returns the first non-empty value among the supplied keys.</summary>
        public static string FirstString(List<ScenarioProperty> properties, params string[] keys)
        {
            for (int i = 0; keys != null && i < keys.Length; i++)
            {
                string value = GetString(properties, keys[i]);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return null;
        }

        /// <summary>Gets an invariant integer value with a fallback.</summary>
        public static int GetInt(List<ScenarioProperty> properties, string key, int fallback)
        {
            int value;
            return TryGetInt(properties, key, out value) ? value : fallback;
        }

        /// <summary>Attempts to parse an invariant integer value.</summary>
        public static bool TryGetInt(List<ScenarioProperty> properties, string key, out int value)
        {
            return int.TryParse(GetString(properties, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Gets an invariant floating-point value with a fallback.</summary>
        public static float GetFloat(List<ScenarioProperty> properties, string key, float fallback)
        {
            float value;
            return TryGetFloat(properties, key, out value) ? value : fallback;
        }

        /// <summary>Attempts to parse an invariant floating-point value.</summary>
        public static bool TryGetFloat(List<ScenarioProperty> properties, string key, out float value)
        {
            return float.TryParse(GetString(properties, key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Gets a Boolean value with a fallback.</summary>
        public static bool GetBool(List<ScenarioProperty> properties, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(GetString(properties, key), out value) ? value : fallback;
        }
    }
}
