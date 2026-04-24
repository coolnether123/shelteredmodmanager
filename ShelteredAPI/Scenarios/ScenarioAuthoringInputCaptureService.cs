using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringInputCaptureService
    {
        private readonly List<Rect> _interactiveRects = new List<Rect>();
        private readonly ScenarioAuthoringScrollFocusService _scrollFocusService;
        private float _coordinateScale = 1f;
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

        public void BeginFrame(float coordinateScale)
        {
            PointerOverAuthoringUiLastFrame = PointerOverAuthoringUi;
            PopupOpenLastFrame = PopupOpen;
            _interactiveRects.Clear();
            PointerOverAuthoringUi = false;
            PopupOpen = false;
            DraggingShellChrome = false;
            KeyboardCaptured = false;
            _coordinateScale = coordinateScale > 0.001f ? coordinateScale : 1f;
            _scrollFocusService.BeginFrame();
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

        public void CompleteFrame()
        {
            Vector2 pointer = GetPointerPosition(_coordinateScale);
            PointerOverAuthoringUi = false;
            for (int i = 0; i < _interactiveRects.Count; i++)
            {
                if (_interactiveRects[i].Contains(pointer))
                {
                    PointerOverAuthoringUi = true;
                    break;
                }
            }

            _scrollFocusService.CompleteFrame(pointer);
            if (_scrollFocusService.PointerOverScrollableRegion)
                PointerOverAuthoringUi = true;
            if (PopupOpen)
                PointerOverAuthoringUi = true;
        }

        public bool ShouldSuppressWorldInput()
        {
            return PointerOverAuthoringUi
                || PointerOverAuthoringUiLastFrame
                || PopupOpen
                || PopupOpenLastFrame
                || DraggingShellChrome
                || KeyboardCaptured;
        }

        public bool ShouldBlockGameCameraInput()
        {
            return ShouldSuppressWorldInput();
        }

        private static Rect Expand(Rect rect, float padding)
        {
            return new Rect(
                rect.x - padding,
                rect.y - padding,
                rect.width + (padding * 2f),
                rect.height + (padding * 2f));
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
    }
}
