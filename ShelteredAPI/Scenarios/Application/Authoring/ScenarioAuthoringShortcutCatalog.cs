using System.Collections.Generic;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    // Enumerable, metadata-carrying registry for the context-aware authoring
    // keyboard router (ScenarioAuthoringShortcutRouter). Each descriptor records
    // the key chord, a human-readable description, and the context/surface where
    // the shortcut applies. This catalog is the single source of truth that the
    // Keyboard Shortcuts help overlay is generated from, so the overlay can never
    // drift from a separately hand-maintained list. ScenarioAuthoringShortcutCatalogVerification
    // asserts every descriptor carries a chord + description and surfaces in the overlay.
    internal enum ScenarioAuthoringShortcutContext
    {
        Global = 0,
        WorldEditing = 1,
        Placement = 2,
        PixelEditor = 3,
        TextField = 4,
        ModalOrPicker = 5
    }

    internal sealed class ScenarioAuthoringShortcutDescriptor
    {
        public ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext context, string keyChord, string description)
        {
            Context = context;
            KeyChord = keyChord;
            Description = description;
        }

        public ScenarioAuthoringShortcutContext Context { get; private set; }
        public string KeyChord { get; private set; }
        public string Description { get; private set; }
    }

    internal static class ScenarioAuthoringShortcutCatalog
    {
        // Order here defines display order in the overlay. Contexts mirror the
        // surfaces ScenarioAuthoringSurfaceResolver resolves and the chords the
        // router handles in ReadChord / Handle*Shortcut / TryRouteEscape.
        private static readonly ScenarioAuthoringShortcutDescriptor[] Descriptors =
        {
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.Global, "F1", "Open this keyboard shortcuts reference."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.Global, "Ctrl+S", "Save the current scenario draft."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.Global, "Esc", "Close the active panel, picker, or placement."),

            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.WorldEditing, "Ctrl+Z", "Undo the last authoring change."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.WorldEditing, "Ctrl+Y / Ctrl+Shift+Z", "Redo the last undone change."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.WorldEditing, "Ctrl+C", "Copy the selected object placement."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.WorldEditing, "Ctrl+V", "Paste the copied object as a placement preview."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.WorldEditing, "Ctrl+D", "Duplicate the selected object placement."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.WorldEditing, "Delete", "Delete the selected object, room, ladder, or light."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.WorldEditing, "Ctrl+R", "Revert the selected sprite swap."),

            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.Placement, "Esc", "Cancel the active build or scene-sprite placement."),

            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.PixelEditor, "Ctrl+Z", "Undo the last pixel edit."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.PixelEditor, "Ctrl+Y", "Redo the last pixel edit."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.PixelEditor, "Ctrl+C", "Copy the pixel canvas."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.PixelEditor, "Ctrl+V", "Paste into the pixel canvas."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.PixelEditor, "Ctrl+S", "Save the pixel edit into the draft."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.PixelEditor, "Ctrl+R", "Revert the pixel edit to its source."),
            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.PixelEditor, "Esc", "Prompt to save or discard before closing the pixel editor."),

            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.TextField, "Esc", "Finish editing the focused text field before panels close."),

            new ScenarioAuthoringShortcutDescriptor(ScenarioAuthoringShortcutContext.ModalOrPicker, "Esc", "Close the open modal, picker, or focused editor.")
        };

        // Display order for context groups in the overlay.
        private static readonly ScenarioAuthoringShortcutContext[] ContextOrder =
        {
            ScenarioAuthoringShortcutContext.Global,
            ScenarioAuthoringShortcutContext.WorldEditing,
            ScenarioAuthoringShortcutContext.Placement,
            ScenarioAuthoringShortcutContext.PixelEditor,
            ScenarioAuthoringShortcutContext.TextField,
            ScenarioAuthoringShortcutContext.ModalOrPicker
        };

        public static IList<ScenarioAuthoringShortcutDescriptor> All
        {
            get { return Descriptors; }
        }

        public static IList<ScenarioAuthoringShortcutContext> ContextsInDisplayOrder
        {
            get { return ContextOrder; }
        }

        public static string GetContextTitle(ScenarioAuthoringShortcutContext context)
        {
            switch (context)
            {
                case ScenarioAuthoringShortcutContext.Global:
                    return "Global";
                case ScenarioAuthoringShortcutContext.WorldEditing:
                    return "World editing";
                case ScenarioAuthoringShortcutContext.Placement:
                    return "Placement";
                case ScenarioAuthoringShortcutContext.PixelEditor:
                    return "Pixel editor";
                case ScenarioAuthoringShortcutContext.TextField:
                    return "Text fields";
                case ScenarioAuthoringShortcutContext.ModalOrPicker:
                    return "Modals & pickers";
                default:
                    return context.ToString();
            }
        }

        // Maps the router's resolved surface to a catalog context so the overlay
        // can highlight the group that matches the live keyboard surface.
        public static ScenarioAuthoringShortcutContext FromSurfaceKind(ScenarioAuthoringSurfaceKind kind)
        {
            switch (kind)
            {
                case ScenarioAuthoringSurfaceKind.TextField:
                    return ScenarioAuthoringShortcutContext.TextField;
                case ScenarioAuthoringSurfaceKind.PixelEditor:
                    return ScenarioAuthoringShortcutContext.PixelEditor;
                case ScenarioAuthoringSurfaceKind.Modal:
                case ScenarioAuthoringSurfaceKind.AuthoringWindow:
                    return ScenarioAuthoringShortcutContext.ModalOrPicker;
                case ScenarioAuthoringSurfaceKind.Placement:
                    return ScenarioAuthoringShortcutContext.Placement;
                case ScenarioAuthoringSurfaceKind.Selection:
                case ScenarioAuthoringSurfaceKind.AuthoringWorld:
                    return ScenarioAuthoringShortcutContext.WorldEditing;
                default:
                    return ScenarioAuthoringShortcutContext.Global;
            }
        }

        // Best-effort active context resolved from the authoring state alone (the
        // help overlay owns the live keyboard surface while it is open, so the
        // group we highlight is the editing surface the author will return to).
        public static ScenarioAuthoringShortcutContext ResolveActiveContext(ScenarioAuthoringState state)
        {
            if (state == null || !state.IsActive)
                return ScenarioAuthoringShortcutContext.Global;

            if (ScenarioBuildPlacementAuthoringService.Instance != null
                && ScenarioBuildPlacementAuthoringService.Instance.HasActivePlacement)
            {
                return ScenarioAuthoringShortcutContext.Placement;
            }

            if (!string.IsNullOrEmpty(state.FocusedEditorKind))
                return ScenarioAuthoringShortcutContext.ModalOrPicker;
            if (state.SpriteSwapPicker != null && state.SpriteSwapPicker.IsOpen)
                return ScenarioAuthoringShortcutContext.ModalOrPicker;

            return ScenarioAuthoringShortcutContext.WorldEditing;
        }
    }
}
