using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringInputCaptureService
    {
        private readonly List<Rect> _interactiveRects = new List<Rect>();
        private readonly ScenarioAuthoringScrollFocusService _scrollFocusService;

        public ScenarioAuthoringInputCaptureService(ScenarioAuthoringScrollFocusService scrollFocusService)
        {
            _scrollFocusService = scrollFocusService;
        }

        public bool PointerOverAuthoringUi { get; private set; }
        public bool PopupOpen { get; private set; }
        public bool DraggingShellChrome { get; private set; }
        public bool KeyboardCaptured { get; private set; }

        public void BeginFrame()
        {
            _interactiveRects.Clear();
            PointerOverAuthoringUi = false;
            PopupOpen = false;
            DraggingShellChrome = false;
            KeyboardCaptured = false;
            _scrollFocusService.BeginFrame();
        }

        public void RegisterInteractiveRect(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            _interactiveRects.Add(rect);
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
            Vector2 pointer = GetPointerPosition();
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

        private static Vector2 GetPointerPosition()
        {
            Vector3 mouse = UnityEngine.Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }
    }
}
