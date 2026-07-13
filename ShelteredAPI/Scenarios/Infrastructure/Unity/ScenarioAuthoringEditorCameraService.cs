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
        private const float PixelEditorMinZoom = 0.75f;
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
        private bool _assetFrameActive;
        private Vector3 _assetFrameTarget;
        private bool _cameraLockActive;
        private bool _hasSavedCameraState;
        private Vector3 _savedCameraPosition;
        private float _savedOrthographicSize;

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

            if (_cameraLockActive)
            {
                ResetDragState();
                _keyboardPanVelocity = Vector2.zero;
                ApplyAssetFrame(camera, basicCamera);
                ApplyEasedZoom(camera, basicCamera);
                camera.transform.position = ClampCameraPosition(camera, basicCamera, camera.transform.position);
                return;
            }

            Vector3 translation = ResolveKeyboardPan(camera);
            translation += ResolveMouseDragPan(camera);
            if (translation.sqrMagnitude > 0.000001f)
            {
                _assetFrameActive = false;
                camera.transform.position = ClampCameraPosition(camera, basicCamera, PreserveCameraZ(camera, camera.transform.position + translation));
            }

            ApplyWheelZoom(camera, basicCamera);
            ApplyAssetFrame(camera, basicCamera);
            ApplyEasedZoom(camera, basicCamera);
            camera.transform.position = ClampCameraPosition(camera, basicCamera, camera.transform.position);
        }

        public bool ShouldSuppressSelectionClickThisFrame()
        {
            return _leftDragging || Time.frameCount <= _suppressSelectionFrame;
        }

        public bool FrameTarget(ScenarioAuthoringTarget target)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic || target == null)
                return false;

            Bounds bounds;
            if (!TryResolveTargetBounds(target, out bounds))
                return false;

            float aspect = camera.aspect > 0.001f ? camera.aspect : ((float)Screen.width / Mathf.Max(1f, Screen.height));
            float fitHeight = Mathf.Max(bounds.size.y, bounds.size.x / Mathf.Max(0.25f, aspect));
            _targetOrthographicSize = Mathf.Clamp(Mathf.Max(MinZoom, fitHeight * 0.9f), MinZoom, 8f);
            _assetFrameTarget = new Vector3(bounds.center.x, bounds.center.y, camera.transform.position.z);
            _assetFrameActive = true;
            ResetDragState();
            return true;
        }

        public bool BeginPixelEditorSession(ScenarioAuthoringTarget target, Rect editorWindowRect)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic)
                return false;

            if (!_cameraLockActive)
            {
                _savedCameraPosition = camera.transform.position;
                _savedOrthographicSize = camera.orthographicSize;
                _hasSavedCameraState = true;
            }

            _cameraLockActive = true;
            ResetDragState();
            _keyboardPanVelocity = Vector2.zero;
            return FrameTargetLeftOfWindow(target, editorWindowRect, camera, ResolveBasicCamera());
        }

        public void EndPixelEditorSession()
        {
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic && _hasSavedCameraState)
            {
                camera.transform.position = _savedCameraPosition;
                camera.orthographicSize = _savedOrthographicSize;
            }

            _cameraLockActive = false;
            _hasSavedCameraState = false;
            _targetOrthographicSize = -1f;
            _assetFrameActive = false;
            ResetDragState();
            _keyboardPanVelocity = Vector2.zero;
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
                    && (IsPlacementModeActive() || state == null || state.HoveredTarget == null);
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
            _assetFrameActive = false;
        }

        private void ApplyAssetFrame(Camera camera, BasicCamera basicCamera)
        {
            if (!_assetFrameActive)
                return;

            float deltaTime = GetRealDeltaTime();
            Vector3 target = ClampCameraPosition(camera, basicCamera, PreserveCameraZ(camera, _assetFrameTarget));
            camera.transform.position = Vector3.Lerp(camera.transform.position, target, 1f - Mathf.Exp(-ZoomEaseSpeed * deltaTime));
            if ((camera.transform.position - target).sqrMagnitude < 0.0004f)
                _assetFrameActive = false;
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

        private static float ClampZoom(Camera camera, BasicCamera basicCamera, float requested, float minZoom = MinZoom)
        {
            Rect bounds = basicCamera != null ? basicCamera.CameraBounds : new Rect();
            float maxZoom = 20f;
            if (bounds.width > 0.001f && bounds.height > 0.001f)
            {
                float aspect = camera.aspect > 0.001f ? camera.aspect : ((float)Screen.width / Mathf.Max(1f, Screen.height));
                maxZoom = Mathf.Max(minZoom, Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * aspect)));
            }

            return Mathf.Clamp(requested, minZoom, maxZoom);
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

        private static bool IsPlacementModeActive()
        {
            try
            {
                if (ScenarioBuildPlacementAuthoringService.Instance.HasActivePlacement)
                    return true;
            }
            catch
            {
            }

            try
            {
                return ShelteredAPI.Scenarios.Application.Assets.ScenarioSceneSpritePlacementAuthoringService.Instance.HasActivePlacement;
            }
            catch
            {
                return false;
            }
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

        private bool FrameTargetLeftOfWindow(
            ScenarioAuthoringTarget target,
            Rect editorWindowRect,
            Camera camera,
            BasicCamera basicCamera)
        {
            if (target == null || camera == null)
                return false;

            Bounds bounds;
            if (!TryResolveTargetBounds(target, out bounds))
                return false;

            float regionWidth = Mathf.Clamp(editorWindowRect.x - 18f, Screen.width * 0.30f, Screen.width);
            float regionHeight = Mathf.Max(240f, Screen.height - 36f);
            float regionAspect = regionWidth / Mathf.Max(1f, regionHeight);
            float fitHeight = Mathf.Max(bounds.size.y, bounds.size.x / Mathf.Max(0.25f, regionAspect));
            float orthographicSize = ClampZoom(
                camera,
                basicCamera,
                Mathf.Clamp(Mathf.Max(PixelEditorMinZoom, fitHeight * 0.62f), PixelEditorMinZoom, 6f),
                PixelEditorMinZoom);

            // Preserve the target's vertical screen position. The editor only
            // needs horizontal room; recentering vertically made the selected
            // object appear to jump upward when editing began.
            float desiredScreenY = Mathf.Clamp(
                camera.WorldToScreenPoint(bounds.center).y,
                Screen.height * 0.20f,
                Screen.height * 0.80f);

            camera.orthographicSize = orthographicSize;
            _targetOrthographicSize = orthographicSize;

            float desiredScreenX = regionWidth * 0.5f;
            float pixelsToWorld = (camera.orthographicSize * 2f) / Mathf.Max(1f, camera.pixelHeight);
            Vector3 screenOffsetWorld = new Vector3(
                (desiredScreenX - (camera.pixelWidth * 0.5f)) * pixelsToWorld,
                (desiredScreenY - (camera.pixelHeight * 0.5f)) * pixelsToWorld,
                0f);
            Vector3 position = new Vector3(bounds.center.x, bounds.center.y, camera.transform.position.z) - screenOffsetWorld;
            camera.transform.position = ClampCameraPosition(camera, basicCamera, PreserveCameraZ(camera, position));
            _assetFrameTarget = camera.transform.position;
            _assetFrameActive = false;
            return true;
        }

        private static bool TryResolveTargetBounds(ScenarioAuthoringTarget target, out Bounds bounds)
        {
            bounds = new Bounds(target != null ? target.WorldPosition : Vector3.zero, Vector3.one);
            GameObject gameObject = ResolveGameObject(target);
            if (gameObject == null)
                return target != null;

            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!initialized)
                bounds = new Bounds(gameObject.transform.position, Vector3.one);

            if (bounds.size.sqrMagnitude < 0.0001f)
                bounds.size = Vector3.one;
            return true;
        }

        private static GameObject ResolveGameObject(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.gameObject : null;
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
