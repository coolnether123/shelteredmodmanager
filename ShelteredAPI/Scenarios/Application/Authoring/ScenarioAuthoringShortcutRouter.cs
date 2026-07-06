using System;
using UnityEngine;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Application.Authoring{
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
            _surfaceResolver = new ScenarioAuthoringSurfaceResolver();
        }

        public bool TryRoute(ScenarioAuthoringState state, out bool changed)
        {
            changed = false;
            ShortcutChord chord = ReadChord();
            if (chord.Kind == ShortcutChordKind.None)
                return false;

            bool pixelEditorOpen = _sectionHub != null
                && _sectionHub.SpriteSwap != null
                && _sectionHub.SpriteSwap.GetCustomEditorModel(state) != null;
            ScenarioAuthoringSurfaceState surface = _surfaceResolver.Resolve(state, _inputCapture, pixelEditorOpen);
            if (surface.Kind == ScenarioAuthoringSurfaceKind.TextField)
                return false;

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
                case ScenarioAuthoringSurfaceKind.AuthoringWorld:
                    handled = HandleWorldShortcut(state, chord, out changed, out message);
                    break;
                default:
                    handled = false;
                    message = null;
                    break;
            }

            if (handled)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    state.StatusMessage = message;
                    changed = true;
                }
                if (_inputCapture != null)
                    _inputCapture.MarkKeyboardShortcutHandled();
            }

            return handled;
        }

        private bool HandlePixelShortcut(ScenarioAuthoringState state, ShortcutChord chord, out bool changed, out string message)
        {
            changed = false;
            message = null;
            if (chord.Kind == ShortcutChordKind.Undo)
                return Execute(state, ScenarioAuthoringActionIds.ActionHistoryUndo, out changed, out message);
            if (chord.Kind == ShortcutChordKind.Redo)
                return Execute(state, ScenarioAuthoringActionIds.ActionHistoryRedo, out changed, out message);
            if (chord.Kind == ShortcutChordKind.Copy)
                return Execute(state, ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy, out changed, out message);
            if (chord.Kind == ShortcutChordKind.Paste)
                return Execute(state, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste, out changed, out message);
            if (chord.Kind == ShortcutChordKind.Save)
                return Execute(state, ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave, out changed, out message);
            if (chord.Kind == ShortcutChordKind.Revert)
                return Execute(state, ScenarioAuthoringActionIds.ActionSpriteSwapRevert, out changed, out message);

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
                    return Execute(state, ScenarioAuthoringActionIds.ActionHistoryUndo, out changed, out message);
                case ShortcutChordKind.Redo:
                    return Execute(state, ScenarioAuthoringActionIds.ActionHistoryRedo, out changed, out message);
                case ShortcutChordKind.Copy:
                    return CopySelectedObject(state, out changed, out message);
                case ShortcutChordKind.Paste:
                    return PasteObjectClipboard(state, out changed, out message);
                case ShortcutChordKind.Save:
                    return Execute(state, ScenarioAuthoringActionIds.ActionSave, out changed, out message);
                case ShortcutChordKind.Duplicate:
                    return DuplicateSelectedObject(state, out changed, out message);
                case ShortcutChordKind.Delete:
                    return DeleteSelected(state, out changed, out message);
                case ShortcutChordKind.Revert:
                    return Execute(state, ScenarioAuthoringActionIds.ActionSpriteSwapRevert, out changed, out message);
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
                Label = ScenarioBunkerDraftService.SafeObjectName(obj)
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

            return Execute(
                state,
                ScenarioBuildPlacementAuthoringService.BuildObjectActionId(_objectClipboard.ObjectType, _objectClipboard.Level),
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

            string actionId = ResolveDeleteAction(state.SelectedTarget);
            if (string.IsNullOrEmpty(actionId))
            {
                message = "Delete is not available for the current selection.";
                return true;
            }

            return Execute(state, actionId, out changed, out message);
        }

        private bool Execute(ScenarioAuthoringState state, string actionId, out bool changed, out string message)
        {
            ScenarioAuthoringActionExecutionResult result = _commandService.ExecuteWithResult(state, actionId);
            changed = result != null && result.Result;
            message = result != null && !string.IsNullOrEmpty(result.StatusMessage)
                ? result.StatusMessage
                : (result != null ? result.Reason : null);
            return true;
        }

        private static string ResolveDeleteAction(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            switch (target.Kind)
            {
                case ScenarioAuthoringTargetKind.Room:
                case ScenarioAuthoringTargetKind.Tile:
                    return ScenarioAuthoringActionIds.ActionBuildDeleteRoom;
                case ScenarioAuthoringTargetKind.Light:
                    return ScenarioAuthoringActionIds.ActionBuildDeleteLight;
                case ScenarioAuthoringTargetKind.PlaceableObject:
                    return ScenarioAuthoringActionIds.ActionBuildDeleteObject;
                default:
                    return ScenarioAuthoringActionIds.ActionBuildDeleteObject;
            }
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
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Delete))
                return new ShortcutChord { Kind = ShortcutChordKind.Delete };

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
            Revert = 10
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
        }
    }
}
