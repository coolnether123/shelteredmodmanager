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

        private static readonly ActionDefinition[] ActionCatalog =
        {
            new ActionDefinition(ZoomIn, "Zoom In", "Map", new InputBinding(KeyCode.Equals, KeyCode.KeypadPlus), "Zoom map in.", false),
            new ActionDefinition(ZoomOut, "Zoom Out", "Map", new InputBinding(KeyCode.Minus, KeyCode.KeypadMinus), "Zoom map out.", false),
            new ActionDefinition(OpenJournal, "Open Journal", "UI", new InputBinding(KeyCode.J, KeyCode.None), "Open quest/journal panel.", false),
            new ActionDefinition(OpenStatusReport, "Open Status Report", "UI", new InputBinding(KeyCode.R, KeyCode.None), "Open status report panel.", false),
            new ActionDefinition(ToggleMute, "Toggle Mute", "Audio", new InputBinding(KeyCode.M, KeyCode.None), "Toggle all audio mute.", false),
            new ActionDefinition(ScrollUp, "Scroll Up", "UI", new InputBinding(KeyCode.UpArrow, KeyCode.PageUp), "Scroll lists/windows upward.", false),
            new ActionDefinition(ScrollDown, "Scroll Down", "UI", new InputBinding(KeyCode.DownArrow, KeyCode.PageDown), "Scroll lists/windows downward.", false)
        };

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            for (int i = 0; i < ActionCatalog.Length; i++)
            {
                ActionDefinition def = ActionCatalog[i];
                if (!def.IsRuntimeBound)
                    continue;

                Register(def.Id, def.Label, def.Category, def.DefaultBinding, def.Description);
            }
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

        private sealed class ActionDefinition
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string Category;
            public readonly InputBinding DefaultBinding;
            public readonly string Description;
            public readonly bool IsRuntimeBound;

            public ActionDefinition(string id, string label, string category, InputBinding defaultBinding, string description, bool isRuntimeBound)
            {
                Id = id;
                Label = label;
                Category = category;
                DefaultBinding = defaultBinding;
                Description = description;
                IsRuntimeBound = isRuntimeBound;
            }
        }
    }
}
