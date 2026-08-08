using System;
using UnityEngine;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioAuthoringShortcutRouter
    {
        private readonly ScenarioAuthoringCommandService _commandService;
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioAuthoringInputCaptureService _inputCapture;
        private readonly ScenarioAuthoringSurfaceResolver _surfaceResolver;
        private ObjectClipboardEntry _objectClipboard;

        public ScenarioAuthoringShortcutRouter(
            ScenarioAuthoringCommandService commandService,
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            _commandService = commandService;
            _sectionHub = sectionHub;
            _inputCapture = inputCapture;
            _surfaceResolver = new ScenarioAuthoringSurfaceResolver(
                sectionHub != null ? sectionHub.BuildPlacement : null,
                sectionHub != null ? sectionHub.SceneSpritePlacement : null);
        }

        public bool TryRoute(ScenarioAuthoringState state, out bool changed)
        {
            changed = false;
            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
                return false;

            ShortcutChord chord = ReadChord();
            if (chord.Kind == ShortcutChordKind.None)
                return false;

            bool pixelEditorOpen = _sectionHub != null
                && _sectionHub.SpriteSwap != null
                && _sectionHub.SpriteSwap.GetCustomEditorModel(state) != null;
            ScenarioSpriteSwapAuthoringService.CustomEditorModel pixelEditor = pixelEditorOpen
                ? _sectionHub.SpriteSwap.GetCustomEditorModel(state)
                : null;
            ScenarioAuthoringSurfaceState surface = _surfaceResolver.Resolve(state, _inputCapture, pixelEditorOpen);
            if (surface.Kind == ScenarioAuthoringSurfaceKind.TextField)
            {
                if (chord.Kind != ShortcutChordKind.Escape)
                    return false;

                MarkHandled(state, "Finish the active text field before closing panels.", ref changed);
                return true;
            }

            // Global command palette is context-free: Ctrl+K toggles it from any non-text
            // surface so creators can jump to commands, elements, and help from anywhere.
            if (chord.Kind == ShortcutChordKind.GlobalSearch)
            {
                string searchMessage;
                bool searchChanged;
                Execute(state, ShellUxCommand.Simple(ShellUxCommandKind.ToggleGlobalSearch, ScenarioAuthoringActionIds.ActionShellToggleGlobalSearch), out searchChanged, out searchMessage);
                changed = searchChanged;
                MarkHandled(state, searchMessage, ref changed);
                return true;
            }

            if (chord.Kind == ShortcutChordKind.Escape)
                return TryRouteEscape(state, surface, pixelEditor, out changed);

            string message;
            bool handled;
            switch (surface.Kind)
            {
                case ScenarioAuthoringSurfaceKind.PixelEditor:
                    handled = HandlePixelShortcut(state, chord, out changed, out message);
                    break;
                case ScenarioAuthoringSurfaceKind.Modal:
                    handled = HandleModalShortcut(state, chord, out changed, out message);
                    break;
                case ScenarioAuthoringSurfaceKind.AuthoringWindow:
                case ScenarioAuthoringSurfaceKind.Placement:
                    handled = true;
                    message = "Shortcut unavailable while " + surface.Description.ToLowerInvariant() + " is active.";
                    break;
                case ScenarioAuthoringSurfaceKind.Selection:
                case ScenarioAuthoringSurfaceKind.AuthoringWorld:
                    handled = HandleWorldShortcut(state, chord, out changed, out message);
                    break;
                default:
                    handled = false;
                    message = null;
                    break;
            }

            if (handled)
                MarkHandled(state, message, ref changed);

            return handled;
        }

        private bool TryRouteEscape(
            ScenarioAuthoringState state,
            ScenarioAuthoringSurfaceState surface,
            ScenarioSpriteSwapAuthoringService.CustomEditorModel pixelEditor,
            out bool changed)
        {
            changed = false;
            if (surface == null || surface.Kind == ScenarioAuthoringSurfaceKind.Inactive)
                return false;

            if (surface.Kind == ScenarioAuthoringSurfaceKind.PixelEditor && pixelEditor != null && pixelEditor.Dirty)
            {
                MarkHandled(state, "Save or discard pixel edits before closing the editor.", ref changed);
                return true;
            }

            if (surface.Kind == ScenarioAuthoringSurfaceKind.AuthoringWorld)
                return false;

            if (string.IsNullOrEmpty(surface.ActionId))
            {
                MarkHandled(state, surface.Description + " owns Escape.", ref changed);
                return true;
            }

            if (surface.Command == null)
            {
                MarkHandled(state, surface.Description + " has no typed close command.", ref changed);
                return true;
            }

            string message;
            bool executeChanged;
            Execute(state, surface.Command, out executeChanged, out message);
            changed |= executeChanged;
            MarkHandled(state, !string.IsNullOrEmpty(message) ? message : (surface.Description + " closed."), ref changed);
            return true;
        }

        private void MarkHandled(ScenarioAuthoringState state, string message, ref bool changed)
        {
            if (state != null && !string.IsNullOrEmpty(message))
            {
                state.StatusMessage = message;
                changed = true;
            }

            if (_inputCapture != null)
                _inputCapture.MarkKeyboardShortcutHandled();
        }

        private bool HandlePixelShortcut(ScenarioAuthoringState state, ShortcutChord chord, out bool changed, out string message)
        {
            changed = false;
            message = null;
            if (chord.Kind == ShortcutChordKind.Undo)
                return Execute(state, SpriteSwapCommand.Undo(), out changed, out message);
            if (chord.Kind == ShortcutChordKind.Redo)
                return Execute(state, SpriteSwapCommand.Redo(), out changed, out message);
            if (chord.Kind == ShortcutChordKind.Copy)
                return Execute(state, SpriteSwapCommand.CopyPixels(), out changed, out message);
            if (chord.Kind == ShortcutChordKind.Paste)
                return Execute(state, SpriteSwapCommand.PastePixels(), out changed, out message);
            if (chord.Kind == ShortcutChordKind.Save)
                return Execute(state, SpriteSwapCommand.SavePicker(), out changed, out message);
            if (chord.Kind == ShortcutChordKind.Revert)
                return Execute(state, SpriteSwapCommand.Revert(), out changed, out message);

            message = "That shortcut is not available in the pixel editor.";
            return true;
        }

        private static bool HandleModalShortcut(ScenarioAuthoringState state, ShortcutChord chord, out bool changed, out string message)
        {
            changed = true;
            message = "Shortcut unavailable while a modal or picker is open.";
            return true;
        }

        private bool HandleWorldShortcut(ScenarioAuthoringState state, ShortcutChord chord, out bool changed, out string message)
        {
            changed = false;
            message = null;
            switch (chord.Kind)
            {
                case ShortcutChordKind.Undo:
                    return Execute(state, EditHistoryCommand.Undo, out changed, out message);
                case ShortcutChordKind.Redo:
                    return Execute(state, EditHistoryCommand.Redo, out changed, out message);
                case ShortcutChordKind.Copy:
                    return CopySelectedObject(state, out changed, out message);
                case ShortcutChordKind.Paste:
                    return PasteObjectClipboard(state, out changed, out message);
                case ShortcutChordKind.Save:
                    return Execute(state, EditorLifecycleCommand.SaveDraft, out changed, out message);
                case ShortcutChordKind.Duplicate:
                    return DuplicateSelectedObject(state, out changed, out message);
                case ShortcutChordKind.Delete:
                    return DeleteSelected(state, out changed, out message);
                case ShortcutChordKind.Revert:
                    return Execute(state, SpriteSwapCommand.Revert(), out changed, out message);
                default:
                    message = "That shortcut is not available in the authoring world.";
                    return true;
            }
        }

        private bool CopySelectedObject(ScenarioAuthoringState state, out bool changed, out string message)
        {
            changed = false;
            Obj_Base obj;
            if (!TryResolveSelectedObject(state, out obj))
            {
                message = "Select a placed shelter object before using Ctrl+C.";
                return true;
            }

            ObjectManager.ObjectType objectType = obj.GetObjectType();
            if (IsStructuralObject(objectType))
            {
                message = "Structural room, ladder, and light targets cannot be copied as object placements.";
                return true;
            }

            int level = obj.objectLevel > 0 ? obj.objectLevel : 1;
            _objectClipboard = new ObjectClipboardEntry
            {
                ObjectType = objectType,
                Level = level,
                Label = ScenarioBunkerDraftService.SafeObjectName(obj),
                SourceObject = obj
            };
            message = "Copied object placement '" + _objectClipboard.Label + "'. Ctrl+V starts a placement preview.";
            return true;
        }

        private bool PasteObjectClipboard(ScenarioAuthoringState state, out bool changed, out string message)
        {
            changed = false;
            if (_objectClipboard == null)
            {
                message = "Object clipboard is empty.";
                return true;
            }

            ObjectManager manager = ObjectManager.Instance;
            ObjectManager.ObjectType resolvedType;
            int resolvedLevel;
            GameObject prefab;
            Obj_Base prefabComponent;
            if (!ScenarioBuildPlacementAuthoringService.TryResolvePlaceableObject(
                    manager,
                    _objectClipboard.ObjectType,
                    _objectClipboard.Level,
                    _objectClipboard.Label,
                    out resolvedType,
                    out resolvedLevel,
                    out prefab,
                    out prefabComponent))
            {
                if (_objectClipboard.SourceObject != null)
                {
                    message = "Build placement service is not available.";
                    bool startedClone = _sectionHub != null
                        && _sectionHub.BuildPlacement != null
                        && _sectionHub.BuildPlacement.StartObjectClonePlacement(_objectClipboard.SourceObject, out message);
                    changed = startedClone;
                    return true;
                }

                message = "No compatible prefab is available for " + _objectClipboard.Label + ".";
                return true;
            }

            return Execute(
                state,
                BuildPlacementCommand.StartObject(resolvedType, resolvedLevel),
                out changed,
                out message);
        }

        private bool DuplicateSelectedObject(ScenarioAuthoringState state, out bool changed, out string message)
        {
            bool copyChanged;
            string copyMessage;
            CopySelectedObject(state, out copyChanged, out copyMessage);
            if (_objectClipboard == null)
            {
                changed = copyChanged;
                message = copyMessage;
                return true;
            }

            return PasteObjectClipboard(state, out changed, out message);
        }

        private bool DeleteSelected(ScenarioAuthoringState state, out bool changed, out string message)
        {
            changed = false;
            if (state == null || state.SelectedTarget == null)
            {
                message = "Select a placed object, room, ladder, or light before pressing Delete.";
                return true;
            }

            BuildPlacementCommand deleteCommand = BuildPlacementCommand.DeleteObject();
            ScenarioPlacementDefinitionKind structuralKind;
            Obj_Base selectedObject;
            if (TryResolveSelectedObject(state, out selectedObject)
                && ScenarioPlacementDefinitions.TryParseSpecialKind(selectedObject.GetObjectType().ToString(), out structuralKind))
            {
                if (structuralKind == ScenarioPlacementDefinitionKind.Room)
                    deleteCommand = BuildPlacementCommand.DeleteRoom();
                else if (structuralKind == ScenarioPlacementDefinitionKind.Ladder)
                    deleteCommand = BuildPlacementCommand.DeleteLadder();
                else if (structuralKind == ScenarioPlacementDefinitionKind.RoomLight)
                    deleteCommand = BuildPlacementCommand.DeleteLight();
            }
            else if (state.SelectedTarget.Kind == ScenarioAuthoringTargetKind.Room
                || state.SelectedTarget.Kind == ScenarioAuthoringTargetKind.Tile)
            {
                deleteCommand = BuildPlacementCommand.DeleteRoom();
            }
            else if (state.SelectedTarget.Kind == ScenarioAuthoringTargetKind.Light)
            {
                deleteCommand = BuildPlacementCommand.DeleteLight();
            }
            else
            {
                string targetText = (state.SelectedTarget.DisplayName ?? string.Empty)
                    + " " + (state.SelectedTarget.GameObjectName ?? string.Empty)
                    + " " + (state.SelectedTarget.TransformPath ?? string.Empty);
                if (targetText.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0)
                    deleteCommand = BuildPlacementCommand.DeleteLadder();
            }

            return Execute(state, deleteCommand, out changed, out message);
        }

        private bool Execute(ScenarioAuthoringState state, ScenarioAuthoringCommand command, out bool changed, out string message)
        {
            ScenarioCommandExecutionResult result = _commandService.ExecuteWithResult(state, command);
            changed = result.Changed;
            message = !string.IsNullOrEmpty(result.StatusMessage)
                ? result.StatusMessage
                : result.Reason;
            return true;
        }

        private static bool TryResolveSelectedObject(ScenarioAuthoringState state, out Obj_Base obj)
        {
            obj = null;
            UnityEngine.Object runtimeObject = state != null && state.SelectedTarget != null
                ? state.SelectedTarget.RuntimeObject
                : null;
            GameObject gameObject = runtimeObject as GameObject;
            if (gameObject == null)
            {
                Component component = runtimeObject as Component;
                gameObject = component != null ? component.gameObject : null;
            }

            obj = gameObject != null ? gameObject.GetComponentInParent<Obj_Base>() : null;
            return obj != null;
        }

        private static bool IsStructuralObject(ObjectManager.ObjectType objectType)
        {
            ScenarioPlacementDefinitionKind kind;
            return ScenarioPlacementDefinitions.TryParseSpecialKind(objectType.ToString(), out kind);
        }

        private static ShortcutChord ReadChord()
        {
            bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            if (ctrl)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Z))
                    return new ShortcutChord { Kind = shift ? ShortcutChordKind.Redo : ShortcutChordKind.Undo };
                if (UnityEngine.Input.GetKeyDown(KeyCode.Y))
                    return new ShortcutChord { Kind = ShortcutChordKind.Redo };
                if (UnityEngine.Input.GetKeyDown(KeyCode.C))
                    return new ShortcutChord { Kind = ShortcutChordKind.Copy };
                if (UnityEngine.Input.GetKeyDown(KeyCode.V))
                    return new ShortcutChord { Kind = ShortcutChordKind.Paste };
                if (UnityEngine.Input.GetKeyDown(KeyCode.X))
                    return new ShortcutChord { Kind = ShortcutChordKind.Cut };
                if (UnityEngine.Input.GetKeyDown(KeyCode.A))
                    return new ShortcutChord { Kind = ShortcutChordKind.SelectAll };
                if (UnityEngine.Input.GetKeyDown(KeyCode.S))
                    return new ShortcutChord { Kind = ShortcutChordKind.Save };
                if (UnityEngine.Input.GetKeyDown(KeyCode.D))
                    return new ShortcutChord { Kind = ShortcutChordKind.Duplicate };
                if (UnityEngine.Input.GetKeyDown(KeyCode.R))
                    return new ShortcutChord { Kind = ShortcutChordKind.Revert };
                if (UnityEngine.Input.GetKeyDown(KeyCode.K))
                    return new ShortcutChord { Kind = ShortcutChordKind.GlobalSearch };
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Delete))
                return new ShortcutChord { Kind = ShortcutChordKind.Delete };
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return new ShortcutChord { Kind = ShortcutChordKind.Escape };

            return new ShortcutChord { Kind = ShortcutChordKind.None };
        }

        private enum ShortcutChordKind
        {
            None = 0,
            Undo = 1,
            Redo = 2,
            Copy = 3,
            Paste = 4,
            Cut = 5,
            SelectAll = 6,
            Save = 7,
            Duplicate = 8,
            Delete = 9,
            Revert = 10,
            Escape = 11,
            GlobalSearch = 12
        }

        private struct ShortcutChord
        {
            public ShortcutChordKind Kind;
        }

        private sealed class ObjectClipboardEntry
        {
            public ObjectManager.ObjectType ObjectType;
            public int Level;
            public string Label;
            public Obj_Base SourceObject;
        }
    }
}
