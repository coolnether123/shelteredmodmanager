using System;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Shared identifier helpers for Sheltered-specific actions.
    /// </summary>
    public static class ShelteredInputActions
    {
        public const string IdPrefix = "sheltered.";

        public static bool IsShelteredAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            return actionId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
