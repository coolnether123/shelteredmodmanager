using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioAuthoringInputCaptureService
    {
        private const string OverlayCaptureOwnerId = "ShelteredAPI.ScenarioAuthoring";
        private readonly List<Rect> _interactiveRects = new List<Rect>();
        private readonly ScenarioAuthoringScrollFocusService _scrollFocusService;
        private IOverlayInputCaptureService _overlayInputCaptureService;
        private float _coordinateScale = 1f;
        private bool _textFieldFocusedThisGuiFrame;
        private bool _textFieldFocusedLastGuiFrame;
        private int _textFieldFocusGuiFrame = -1;
        private const float RectPadding = 6f;

        public ScenarioAuthoringInputCaptureService(ScenarioAuthoringScrollFocusService scrollFocusService)
        {
            _scrollFocusService = scrollFocusService;
        }

        public bool PointerOverAuthoringUi { get; private set; }
        public bool PointerOverAuthoringUiLastFrame { get; private set; }
        public bool PopupOpen { get; private set; }
        public bool PopupOpenLastFrame { get; private set; }
        public bool DraggingShellChrome { get; private set; }
        public bool KeyboardCaptured { get; private set; }
        public bool TextFieldFocused
        {
            get { return _textFieldFocusedLastGuiFrame; }
        }
        public bool TransitionActive { get; private set; }
        public bool KeyboardShortcutHandled { get; private set; }

        public void BeginFrame(float coordinateScale)
        {
            PointerOverAuthoringUiLastFrame = PointerOverAuthoringUi;
            PopupOpenLastFrame = PopupOpen;
            _interactiveRects.Clear();
            PointerOverAuthoringUi = false;
            PopupOpen = false;
            DraggingShellChrome = false;
            KeyboardCaptured = false;
            BeginTextFieldFocusFrame();
            TransitionActive = false;
            KeyboardShortcutHandled = false;
            _coordinateScale = coordinateScale > 0.001f ? coordinateScale : 1f;
            _scrollFocusService.BeginFrame(GetPointerPosition(_coordinateScale));
        }

        public void BeginFrame()
        {
            BeginFrame(1f);
        }

        public void RegisterInteractiveRect(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            _interactiveRects.Add(Expand(rect, RectPadding));
        }

        public void RegisterScrollRect(string ownerId, Rect rect)
        {
            RegisterInteractiveRect(rect);
            _scrollFocusService.RegisterRegion(ownerId, rect);
            _scrollFocusService.ConsumeScrollWheelIfNotFocused(ownerId, rect, Event.current, GetPointerPosition(_coordinateScale));
        }

        public void SetPopupOpen(bool open)
        {
            PopupOpen = open;
        }

        public void SetDraggingShellChrome(bool dragging)
        {
            DraggingShellChrome = dragging;
        }

        public void SetKeyboardCaptured(bool captured)
        {
            KeyboardCaptured = captured;
        }

        public void SetTextFieldFocused(bool focused)
        {
            BeginTextFieldFocusFrame();
            _textFieldFocusedThisGuiFrame = _textFieldFocusedThisGuiFrame || focused;
        }

        public void MarkKeyboardShortcutHandled()
        {
            KeyboardShortcutHandled = true;
            KeyboardCaptured = true;
        }

        public void SetTransitionActive(bool active)
        {
            TransitionActive = active;
        }

        public void SuppressWorldInputForAction()
        {
            PointerOverAuthoringUi = true;
        }

        public bool ShouldSuppressWorldInput()
        {
            return PointerOverAuthoringUi
                || PointerOverAuthoringUiLastFrame
                || PopupOpen
                || PopupOpenLastFrame
                || DraggingShellChrome
                || KeyboardCaptured
                || TextFieldFocused
                || TransitionActive;
        }

        public bool ShouldSuppressWorldInputNow()
        {
            return ShouldSuppressWorldInput()
                || IsPointerOverRegisteredUi(GetPointerPosition(_coordinateScale))
                || _scrollFocusService.PointerOverScrollableRegion;
        }

        public bool ShouldBlockGameCameraInput()
        {
            return ShouldSuppressWorldInput();
        }

        public void Clear()
        {
            _interactiveRects.Clear();
            PointerOverAuthoringUi = false;
            PointerOverAuthoringUiLastFrame = false;
            PopupOpen = false;
            PopupOpenLastFrame = false;
            DraggingShellChrome = false;
            KeyboardCaptured = false;
            _textFieldFocusedThisGuiFrame = false;
            _textFieldFocusedLastGuiFrame = false;
            _textFieldFocusGuiFrame = -1;
            TransitionActive = false;
            KeyboardShortcutHandled = false;
            _scrollFocusService.BeginFrame();
            UpdateOverlayInputCapture(false, false);
        }

        private static Rect Expand(Rect rect, float padding)
        {
            return new Rect(
                rect.x - padding,
                rect.y - padding,
                rect.width + (padding * 2f),
                rect.height + (padding * 2f));
        }

        private bool IsPointerOverRegisteredUi(Vector2 pointer)
        {
            for (int i = 0; i < _interactiveRects.Count; i++)
            {
                if (_interactiveRects[i].Contains(pointer))
                    return true;
            }

            return false;
        }

        private static Vector2 GetPointerPosition()
        {
            return GetPointerPosition(1f);
        }

        private static Vector2 GetPointerPosition(float coordinateScale)
        {
            Vector3 mouse = UnityEngine.Input.mousePosition;
            float scale = coordinateScale > 0.001f ? coordinateScale : 1f;
            return new Vector2(mouse.x / scale, (Screen.height - mouse.y) / scale);
        }

        private void BeginTextFieldFocusFrame()
        {
            int frame = Time.frameCount;
            if (_textFieldFocusGuiFrame == frame)
                return;

            _textFieldFocusGuiFrame = frame;
            _textFieldFocusedThisGuiFrame = false;
        }

        public void CompleteFrame()
        {
            Vector2 pointer = GetPointerPosition(_coordinateScale);
            PointerOverAuthoringUi = IsPointerOverRegisteredUi(pointer);

            _scrollFocusService.CompleteFrame(pointer);
            if (_scrollFocusService.PointerOverScrollableRegion)
            {
                PointerOverAuthoringUi = true;
                _scrollFocusService.ConsumeScrollWheelIfFocused(Event.current);
            }
            if (PopupOpen)
                PointerOverAuthoringUi = true;

            _textFieldFocusedLastGuiFrame = _textFieldFocusedThisGuiFrame;
            ScenarioAuthoringState state = ScenarioAuthoringBackendService.Instance.CurrentState;
            bool editorKeyboardCaptured = KeyboardCaptured
                || TextFieldFocused
                || (state != null
                    && state.IsActive
                    && state.ShellVisible
                    && !ScenarioAuthoringRuntimeGuards.IsPlaytesting());
            UpdateOverlayInputCapture(ShouldSuppressWorldInputNow(), editorKeyboardCaptured);
        }

        private void UpdateOverlayInputCapture(bool captureMouse, bool captureKeyboard)
        {
            IOverlayInputCaptureService service = ResolveOverlayInputCaptureService();
            if (service == null)
                return;

            if (captureMouse || captureKeyboard)
                service.ReportCapture(OverlayCaptureOwnerId, captureMouse, captureKeyboard);
            else
                service.ReleaseCapture(OverlayCaptureOwnerId);
        }

        private IOverlayInputCaptureService ResolveOverlayInputCaptureService()
        {
            if (_overlayInputCaptureService != null)
                return _overlayInputCaptureService;

            IOverlayInputCaptureService service;
            if (ModAPIRegistry.TryGetAPI<IOverlayInputCaptureService>(OverlayInputCaptureApi.Name, out service))
                _overlayInputCaptureService = service;
            return _overlayInputCaptureService;
        }
    }
}
