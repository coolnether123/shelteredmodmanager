using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using UnityEngine;

namespace Cortex.Services.Editor.Input
{
    internal sealed class EditorKeybindingService : IEditorKeybindingService
    {
        private static readonly EditorCommandBindingDefinition[] Definitions =
        {
            Create("select.all", "select.all", "Selection", "Select All", "Select the entire document.", KeyCode.A, true, false, false),
            Create("undo", "edit.undo", "Editing", "Undo", "Undo the previous edit.", KeyCode.Z, true, false, false),
            Create("redo", "edit.redo", "Editing", "Redo", "Redo the previous undo.", KeyCode.Y, true, false, false),
            Create("redo.alt", "edit.redo", "Editing", "Redo (Alt)", "Alternative redo binding.", KeyCode.Z, true, true, false),
            Create("caret.left", "caret.left", "Navigation", "Move Left", "Move the caret left.", KeyCode.LeftArrow, false, false, false),
            Create("caret.right", "caret.right", "Navigation", "Move Right", "Move the caret right.", KeyCode.RightArrow, false, false, false),
            Create("caret.up", "caret.up", "Navigation", "Move Up", "Move the caret up.", KeyCode.UpArrow, false, false, false),
            Create("caret.down", "caret.down", "Navigation", "Move Down", "Move the caret down.", KeyCode.DownArrow, false, false, false),
            Create("caret.line.start", "caret.line.start", "Navigation", "Line Start", "Move to the start of the current line.", KeyCode.Home, false, false, false),
            Create("caret.line.end", "caret.line.end", "Navigation", "Line End", "Move to the end of the current line.", KeyCode.End, false, false, false),
            Create("caret.document.start", "caret.document.start", "Navigation", "Document Start", "Move to the start of the document.", KeyCode.Home, true, false, false),
            Create("caret.document.end", "caret.document.end", "Navigation", "Document End", "Move to the end of the document.", KeyCode.End, true, false, false),
            Create("caret.page.up", "caret.page.up", "Navigation", "Page Up", "Move up by a page.", KeyCode.PageUp, false, false, false),
            Create("caret.page.down", "caret.page.down", "Navigation", "Page Down", "Move down by a page.", KeyCode.PageDown, false, false, false),
            Create("edit.backspace", "edit.backspace", "Editing", "Backspace", "Delete the character before the caret.", KeyCode.Backspace, false, false, false),
            Create("edit.delete", "edit.delete", "Editing", "Delete", "Delete the character at the caret.", KeyCode.Delete, false, false, false),
            Create("edit.indent", "edit.indent", "Editing", "Indent", "Indent the current selection.", KeyCode.Tab, false, false, false),
            Create("edit.outdent", "edit.outdent", "Editing", "Outdent", "Outdent the current selection.", KeyCode.Tab, false, true, false),
            Create("edit.newline", "edit.newline", "Editing", "New Line", "Insert a newline.", KeyCode.Return, false, false, false),
            Create("edit.complete", "edit.complete", "Editing", "Trigger Completion", "Open the completion list at the current caret.", KeyCode.Space, true, false, false),
            Create("search.find", "cortex.editor.find", "Search", "Find", "Open the find bar.", KeyCode.F, true, false, false),
            Create("search.next", "cortex.search.next", "Search", "Find Next", "Move to the next search result.", KeyCode.F3, false, false, false),
            Create("search.previous", "cortex.search.previous", "Search", "Find Previous", "Move to the previous search result.", KeyCode.F3, false, true, false),
            Create("search.close", "cortex.search.close", "Search", "Close Find", "Close the active find bar.", KeyCode.Escape, false, false, false),
            CreateUnbound("multi.above", "multi.above", "Multi-caret", "Add Caret Above", "Duplicate the active carets on the line above."),
            CreateUnbound("multi.below", "multi.below", "Multi-caret", "Add Caret Below", "Duplicate the active carets on the line below."),
            CreateUnbound("multi.clear", "multi.clear", "Multi-caret", "Clear Extra Carets", "Clear secondary carets."),
            CreateUnbound("move.line.up", "move.line.up", "Line Editing", "Move Line Up", "Move the current line block upward."),
            CreateUnbound("move.line.down", "move.line.down", "Line Editing", "Move Line Down", "Move the current line block downward.")
        };

        public IList<EditorCommandBindingDefinition> GetCommandBindings()
        {
            return Definitions;
        }

        public IList<EditorKeybinding> GetEffectiveBindings(CortexSettings settings)
        {
            var results = new List<EditorKeybinding>();
            var configured = settings != null && settings.EditorKeybindings != null
                ? settings.EditorKeybindings
                : new EditorKeybinding[0];
            for (var i = 0; i < Definitions.Length; i++)
            {
                var overrideBinding = FindBinding(configured, Definitions[i].BindingId);
                results.Add(NormalizeBinding(Definitions[i], CloneBinding(overrideBinding ?? Definitions[i].DefaultBinding)));
            }

            return results;
        }

        public bool TryResolveCommand(CortexSettings settings, Event current, out string commandId)
        {
            commandId = string.Empty;
            if (current == null || current.type != EventType.KeyDown)
            {
                return false;
            }

            var bindings = GetEffectiveBindings(settings);
            for (var i = 0; i < bindings.Count; i++)
            {
                if (!Matches(bindings[i], current))
                {
                    continue;
                }

                commandId = bindings[i].CommandId ?? string.Empty;
                return !string.IsNullOrEmpty(commandId);
            }

            return false;
        }

        public string FormatGesture(EditorKeybinding binding)
        {
            if (binding == null || string.IsNullOrEmpty(binding.Key))
            {
                return "Unbound";
            }

            var parts = new List<string>();
            if (binding.Control)
            {
                parts.Add("Ctrl");
            }

            if (binding.Shift)
            {
                parts.Add("Shift");
            }

            if (binding.Alt)
            {
                parts.Add("Alt");
            }

            parts.Add(binding.Key);
            return string.Join("+", parts.ToArray());
        }

        public void ResetToDefaults(CortexSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            var defaults = new EditorKeybinding[Definitions.Length];
            for (var i = 0; i < Definitions.Length; i++)
            {
                defaults[i] = CloneBinding(Definitions[i].DefaultBinding);
            }

            settings.EditorKeybindings = defaults;
        }

        private static EditorCommandBindingDefinition Create(
            string bindingId,
            string commandId,
            string category,
            string displayName,
            string description,
            KeyCode keyCode,
            bool control,
            bool shift,
            bool alt)
        {
            return new EditorCommandBindingDefinition
            {
                BindingId = bindingId,
                CommandId = commandId,
                Category = category,
                DisplayName = displayName,
                Description = description,
                DefaultBinding = new EditorKeybinding
                {
                    BindingId = bindingId,
                    CommandId = commandId,
                    Key = keyCode.ToString(),
                    Control = control,
                    Shift = shift,
                    Alt = alt
                }
            };
        }

        private static EditorCommandBindingDefinition CreateUnbound(
            string bindingId,
            string commandId,
            string category,
            string displayName,
            string description)
        {
            return new EditorCommandBindingDefinition
            {
                BindingId = bindingId,
                CommandId = commandId,
                Category = category,
                DisplayName = displayName,
                Description = description,
                DefaultBinding = new EditorKeybinding
                {
                    BindingId = bindingId,
                    CommandId = commandId,
                    Key = string.Empty,
                    Control = false,
                    Shift = false,
                    Alt = false
                }
            };
        }

        private static EditorKeybinding FindBinding(EditorKeybinding[] bindings, string bindingId)
        {
            if (bindings == null || string.IsNullOrEmpty(bindingId))
            {
                return null;
            }

            for (var i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != null && string.Equals(bindings[i].BindingId, bindingId, StringComparison.OrdinalIgnoreCase))
                {
                    return bindings[i];
                }
            }

            return null;
        }

        private static EditorKeybinding CloneBinding(EditorKeybinding binding)
        {
            return binding != null
                ? new EditorKeybinding
                {
                    BindingId = binding.BindingId ?? string.Empty,
                    CommandId = binding.CommandId ?? string.Empty,
                    Key = binding.Key ?? string.Empty,
                    Control = binding.Control,
                    Shift = binding.Shift,
                    Alt = binding.Alt
                }
                : new EditorKeybinding();
        }

        private static EditorKeybinding NormalizeBinding(EditorCommandBindingDefinition definition, EditorKeybinding binding)
        {
            if (binding == null)
            {
                return new EditorKeybinding();
            }

            if (!IsAdvancedCommand(definition) || !MatchesLegacyAdvancedDefault(binding))
            {
                return binding;
            }

            binding.Key = string.Empty;
            binding.Control = false;
            binding.Shift = false;
            binding.Alt = false;
            return binding;
        }

        private static bool IsAdvancedCommand(EditorCommandBindingDefinition definition)
        {
            var bindingId = definition != null ? definition.BindingId ?? string.Empty : string.Empty;
            return string.Equals(bindingId, "multi.above", StringComparison.Ordinal) ||
                string.Equals(bindingId, "multi.below", StringComparison.Ordinal) ||
                string.Equals(bindingId, "move.line.up", StringComparison.Ordinal) ||
                string.Equals(bindingId, "move.line.down", StringComparison.Ordinal);
        }

        private static bool MatchesLegacyAdvancedDefault(EditorKeybinding binding)
        {
            if (binding == null || string.IsNullOrEmpty(binding.Key))
            {
                return false;
            }

            return
                (string.Equals(binding.Key, KeyCode.UpArrow.ToString(), StringComparison.OrdinalIgnoreCase) && !binding.Control && binding.Shift && binding.Alt) ||
                (string.Equals(binding.Key, KeyCode.DownArrow.ToString(), StringComparison.OrdinalIgnoreCase) && !binding.Control && binding.Shift && binding.Alt) ||
                (string.Equals(binding.Key, KeyCode.UpArrow.ToString(), StringComparison.OrdinalIgnoreCase) && !binding.Control && !binding.Shift && binding.Alt) ||
                (string.Equals(binding.Key, KeyCode.DownArrow.ToString(), StringComparison.OrdinalIgnoreCase) && !binding.Control && !binding.Shift && binding.Alt);
        }

        private static bool Matches(EditorKeybinding binding, Event current)
        {
            if (binding == null || current == null || string.IsNullOrEmpty(binding.Key))
            {
                return false;
            }

            KeyCode expectedKey;
            if (!TryParseKeyCode(binding.Key, out expectedKey))
            {
                return false;
            }

            var keyMatches = current.keyCode == expectedKey ||
                (expectedKey == KeyCode.Return && current.keyCode == KeyCode.KeypadEnter);
            return keyMatches &&
                current.control == binding.Control &&
                current.shift == binding.Shift &&
                current.alt == binding.Alt;
        }

        private static bool TryParseKeyCode(string key, out KeyCode keyCode)
        {
            try
            {
                keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), key ?? string.Empty, true);
                return true;
            }
            catch
            {
                keyCode = KeyCode.None;
                return false;
            }
        }
    }
}
