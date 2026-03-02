using ModAPI.InputActions;
using UnityEngine;
using System;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Sheltered-specific action identifiers and default keyboard bindings.
    /// </summary>
    public static class ShelteredInputActions
    {
        public const string IdPrefix = "sheltered.";
        public const string ZoomIn = "sheltered.map.zoom_in";
        public const string ZoomOut = "sheltered.map.zoom_out";
        public const string OpenJournal = "sheltered.ui.open_journal";
        public const string OpenStatusReport = "sheltered.ui.open_status_report";
        public const string ToggleMute = "sheltered.audio.toggle_mute";
        public const string ScrollUp = "sheltered.ui.scroll_up";
        public const string ScrollDown = "sheltered.ui.scroll_down";

        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            Register(ZoomIn, "Zoom In", "Map", new InputBinding(KeyCode.Equals, KeyCode.KeypadPlus), "Zoom map in.");
            Register(ZoomOut, "Zoom Out", "Map", new InputBinding(KeyCode.Minus, KeyCode.KeypadMinus), "Zoom map out.");
            Register(OpenJournal, "Open Journal", "UI", new InputBinding(KeyCode.J, KeyCode.None), "Open quest/journal panel.");
            Register(OpenStatusReport, "Open Status Report", "UI", new InputBinding(KeyCode.R, KeyCode.None), "Open status report panel.");
            Register(ToggleMute, "Toggle Mute", "Audio", new InputBinding(KeyCode.M, KeyCode.None), "Toggle all audio mute.");
            Register(ScrollUp, "Scroll Up", "UI", new InputBinding(KeyCode.UpArrow, KeyCode.PageUp), "Scroll lists/windows upward.");
            Register(ScrollDown, "Scroll Down", "UI", new InputBinding(KeyCode.DownArrow, KeyCode.PageDown), "Scroll lists/windows downward.");
        }

        private static void Register(string id, string label, string category, InputBinding defaultBinding, string description)
        {
            InputActionRegistry.Register(new ModInputAction(id, label, category, defaultBinding, description));
        }

        public static bool IsShelteredAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            return actionId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
