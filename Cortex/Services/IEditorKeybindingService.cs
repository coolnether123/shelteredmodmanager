using System.Collections.Generic;
using Cortex.Core.Models;
using UnityEngine;

namespace Cortex.Services
{
    internal interface IEditorKeybindingService
    {
        IList<EditorCommandBindingDefinition> GetCommandBindings();
        IList<EditorKeybinding> GetEffectiveBindings(CortexSettings settings);
        bool TryResolveCommand(CortexSettings settings, Event current, out string commandId);
        string FormatGesture(EditorKeybinding binding);
        void ResetToDefaults(CortexSettings settings);
    }

    internal sealed class EditorCommandBindingDefinition
    {
        public string BindingId = string.Empty;
        public string CommandId = string.Empty;
        public string Category = string.Empty;
        public string DisplayName = string.Empty;
        public string Description = string.Empty;
        public EditorKeybinding DefaultBinding = new EditorKeybinding();
    }
}
