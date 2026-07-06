using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioAuthoringEditorCameraService
    {
        private const float BasePanSpeed = 8f;
        private const float PanAcceleration = 22f;
        private const float PanDeceleration = 28f;
        private const float MinZoom = 2f;
        private const float ZoomStep = 1.5f;
        private const float ZoomEaseSpeed = 14f;
        private const float LeftDragPanThresholdPixels = 5f;

        private readonly ScenarioAuthoringInputCaptureService _inputCapture;
        private readonly ScenarioAuthoringVanillaPanelVisibilityService _panelVisibility;
        private bool _middleDragging;
        private bool _leftDragCandidate;
        private bool _leftDragging;
        private Vector3 _lastMiddleMousePosition;
        private Vector3 _lastLeftMousePosition;
        private Vector3 _leftDragStartMousePosition;
        private Vector2 _keyboardPanVelocity = Vector2.zero;
        private float _targetOrthographicSize = -1f;
        private int _suppressSelectionFrame = -1;

        public ScenarioAuthoringEditorCameraService(
            ScenarioAuthoringInputCaptureService inputCapture,
            ScenarioAuthoringVanillaPanelVisibilityService panelVisibility)
        {
            _inputCapture = inputCapture;
            _panelVisibility = panelVisibility;
        }

        public void Update()
        {
            if (!CanRunCameraUpdate())
            {
                ResetDragState();
                _keyboardPanVelocity = Vector2.zero;
                _targetOrthographicSize = -1f;
                return;
            }

            BasicCamera basicCamera = ResolveBasicCamera();
            Camera camera = basicCamera != null ? basicCamera.GetComponent<Camera>() : Camera.main;
            if (camera == null || !camera.orthographic)
                return;

            if (_targetOrthographicSize < 0f)
                _targetOrthographicSize = camera.orthographicSize;

            Vector3 translation = ResolveKeyboardPan(camera);
            translation += ResolveMouseDragPan(camera);
            if (translation.sqrMagnitude > 0.000001f)
                camera.transform.position = ClampCameraPosition(camera, basicCamera, PreserveCameraZ(camera, camera.transform.position + translation));

            ApplyWheelZoom(camera, basicCamera);
            ApplyEasedZoom(camera, basicCamera);
            camera.transform.position = ClampCameraPosition(camera, basicCamera, camera.transform.position);
        }

        public bool ShouldSuppressSelectionClickThisFrame()
        {
            return _leftDragging || Time.frameCount <= _suppressSelectionFrame;
        }

        private bool CanRunCameraUpdate()
        {
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive() || ScenarioAuthoringRuntimeGuards.IsPlaytesting())
                return false;
            if (_panelVisibility != null && _panelVisibility.HasBlockingPanelOpen())
                return false;
            return !IsTextFieldFocused();
        }

        private static BasicCamera ResolveBasicCamera()
        {
            Camera main = Camera.main;
            BasicCamera basic = main != null ? main.GetComponent<BasicCamera>() : null;
            return basic != null ? basic : Object.FindObjectOfType<BasicCamera>();
        }

        private Vector3 ResolveKeyboardPan(Camera camera)
        {
            Vector2 direction = Vector2.zero;
            if (!ShouldSuppressWorldCameraInput())
            {
                if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
                    direction.x -= 1f;
                if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
                    direction.x += 1f;
                if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
                    direction.y -= 1f;
                if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
                    direction.y += 1f;
            }

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            float deltaTime = GetRealDeltaTime();
            float acceleration = direction.sqrMagnitude > 0.0001f ? PanAcceleration : PanDeceleration;
            _keyboardPanVelocity = Vector2.MoveTowards(_keyboardPanVelocity, direction, acceleration * deltaTime);
            float zoomScale = Mathf.Max(0.5f, camera.orthographicSize / 4f);
            return new Vector3(_keyboardPanVelocity.x, _keyboardPanVelocity.y, 0f) * BasePanSpeed * zoomScale * deltaTime;
        }

        private Vector3 ResolveMouseDragPan(Camera camera)
        {
            Vector3 translation = ResolveMiddleMousePan(camera);
            translation += ResolveLeftEmptyWorldMousePan(camera);
            return translation;
        }

        private Vector3 ResolveMiddleMousePan(Camera camera)
        {
            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                _middleDragging = true;
                _lastMiddleMousePosition = UnityEngine.Input.mousePosition;
                return Vector3.zero;
            }

            if (!UnityEngine.Input.GetMouseButton(2))
            {
                _middleDragging = false;
                return Vector3.zero;
            }

            if (!_middleDragging)
            {
                _middleDragging = true;
                _lastMiddleMousePosition = UnityEngine.Input.mousePosition;
                return Vector3.zero;
            }

            Vector3 current = UnityEngine.Input.mousePosition;
            Vector3 lastWorld = ScreenToCameraPlane(camera, _lastMiddleMousePosition);
            Vector3 currentWorld = ScreenToCameraPlane(camera, current);
            _lastMiddleMousePosition = current;
            Vector3 delta = lastWorld - currentWorld;
            delta.z = 0f;
            return delta;
        }

        private Vector3 ResolveLeftEmptyWorldMousePan(Camera camera)
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                ScenarioAuthoringState state = ScenarioAuthoringBackendService.Instance.CurrentState;
                _leftDragCandidate = !ShouldSuppressWorldCameraInput()
                    && !ScenarioBuildPlacementAuthoringService.Instance.HasActivePlacement
                    && (state == null || state.HoveredTarget == null);
                _leftDragging = false;
                _leftDragStartMousePosition = UnityEngine.Input.mousePosition;
                _lastLeftMousePosition = _leftDragStartMousePosition;
                return Vector3.zero;
            }

            if (!UnityEngine.Input.GetMouseButton(0))
            {
                if (_leftDragging)
                    _suppressSelectionFrame = Time.frameCount + 1;
                _leftDragCandidate = false;
                _leftDragging = false;
                return Vector3.zero;
            }

            if (!_leftDragCandidate)
                return Vector3.zero;

            Vector3 current = UnityEngine.Input.mousePosition;
            if (!_leftDragging && (current - _leftDragStartMousePosition).sqrMagnitude < LeftDragPanThresholdPixels * LeftDragPanThresholdPixels)
                return Vector3.zero;

            _leftDragging = true;
            _suppressSelectionFrame = Time.frameCount + 1;
            Vector3 lastWorld = ScreenToCameraPlane(camera, _lastLeftMousePosition);
            Vector3 currentWorld = ScreenToCameraPlane(camera, current);
            _lastLeftMousePosition = current;
            Vector3 delta = lastWorld - currentWorld;
            delta.z = 0f;
            return delta;
        }

        private void ApplyWheelZoom(Camera camera, BasicCamera basicCamera)
        {
            if (ShouldSuppressWorldCameraInput())
                return;

            float wheel = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) <= 0.0001f)
                return;

            ScenarioAuthoringState state = ScenarioAuthoringBackendService.Instance.CurrentState;
            float scrollSpeed = state != null && state.Settings != null ? state.Settings.GetFloat("input.scroll_speed", 1f) : 1f;
            _targetOrthographicSize = ClampZoom(camera, basicCamera, _targetOrthographicSize - (wheel * ZoomStep * scrollSpeed));
        }

        private void ApplyEasedZoom(Camera camera, BasicCamera basicCamera)
        {
            _targetOrthographicSize = ClampZoom(camera, basicCamera, _targetOrthographicSize);
            float deltaTime = GetRealDeltaTime();
            camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, _targetOrthographicSize, 1f - Mathf.Exp(-ZoomEaseSpeed * deltaTime));
            camera.orthographicSize = ClampZoom(camera, basicCamera, camera.orthographicSize);
        }

        private static Vector3 ScreenToCameraPlane(Camera camera, Vector3 screenPosition)
        {
            screenPosition.z = Mathf.Abs(camera.transform.position.z);
            return camera.ScreenToWorldPoint(screenPosition);
        }

        private static float ClampZoom(Camera camera, BasicCamera basicCamera, float requested)
        {
            Rect bounds = basicCamera != null ? basicCamera.CameraBounds : new Rect();
            float maxZoom = 20f;
            if (bounds.width > 0.001f && bounds.height > 0.001f)
            {
                float aspect = camera.aspect > 0.001f ? camera.aspect : ((float)Screen.width / Mathf.Max(1f, Screen.height));
                maxZoom = Mathf.Max(MinZoom, Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * aspect)));
            }

            return Mathf.Clamp(requested, MinZoom, maxZoom);
        }

        private static Vector3 ClampCameraPosition(Camera camera, BasicCamera basicCamera, Vector3 position)
        {
            if (basicCamera == null)
                return position;

            Rect bounds = basicCamera.CameraBounds;
            if (bounds.width <= 0.001f || bounds.height <= 0.001f)
                return position;

            Vector3 current = camera.transform.position;
            Vector3 delta = position - current;
            Vector3 bottomLeft = camera.ScreenToWorldPoint(new Vector3(0f, 0f, 0f)) + delta;
            Vector3 topRight = camera.ScreenToWorldPoint(new Vector3(camera.pixelWidth, camera.pixelHeight, 0f)) + delta;
            if (bottomLeft.x < bounds.xMin)
                position.x += bounds.xMin - bottomLeft.x;
            if (topRight.x > bounds.xMax)
                position.x -= topRight.x - bounds.xMax;
            if (bottomLeft.y < bounds.yMin)
                position.y += bounds.yMin - bottomLeft.y;
            if (topRight.y > bounds.yMax)
                position.y -= topRight.y - bounds.yMax;
            return PreserveCameraZ(camera, position);
        }

        private bool ShouldSuppressWorldCameraInput()
        {
            return _inputCapture != null && _inputCapture.ShouldSuppressWorldInputNow();
        }

        private void ResetDragState()
        {
            _middleDragging = false;
            _leftDragCandidate = false;
            _leftDragging = false;
        }

        private static Vector3 PreserveCameraZ(Camera camera, Vector3 position)
        {
            if (camera != null)
                position.z = camera.transform.position.z;
            return position;
        }

        private static float GetRealDeltaTime()
        {
            float deltaTime = RealTime.deltaTime;
            if (deltaTime <= 0f)
                deltaTime = Time.unscaledDeltaTime;
            return Mathf.Clamp(deltaTime, 0f, 0.05f);
        }

        private static bool IsTextFieldFocused()
        {
            GameObject selected = UICamera.selectedObject;
            if (selected == null)
                return false;

            return selected.GetComponent<UIInput>() != null || selected.GetComponentInChildren<UIInput>(true) != null;
        }
    }
}
