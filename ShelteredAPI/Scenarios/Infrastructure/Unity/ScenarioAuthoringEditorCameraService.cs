using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioAuthoringEditorCameraService
    {
        private const float BasePanSpeed = 8f;
        private const float MinZoom = 2f;
        private const float ZoomStep = 1.5f;

        private readonly ScenarioAuthoringInputCaptureService _inputCapture;
        private readonly ScenarioAuthoringVanillaPanelVisibilityService _panelVisibility;
        private bool _middleDragging;
        private Vector3 _lastMousePosition;

        public ScenarioAuthoringEditorCameraService(
            ScenarioAuthoringInputCaptureService inputCapture,
            ScenarioAuthoringVanillaPanelVisibilityService panelVisibility)
        {
            _inputCapture = inputCapture;
            _panelVisibility = panelVisibility;
        }

        public void Update()
        {
            if (!CanControlCamera())
            {
                _middleDragging = false;
                return;
            }

            BasicCamera basicCamera = ResolveBasicCamera();
            Camera camera = basicCamera != null ? basicCamera.GetComponent<Camera>() : Camera.main;
            if (camera == null || !camera.orthographic)
                return;

            Vector3 translation = ResolveKeyboardPan(camera);
            translation += ResolveMiddleMousePan(camera);
            if (translation.sqrMagnitude > 0.000001f)
                camera.transform.position = ClampCameraPosition(camera, basicCamera, camera.transform.position + translation);

            ApplyWheelZoom(camera, basicCamera);
            camera.transform.position = ClampCameraPosition(camera, basicCamera, camera.transform.position);
        }

        private bool CanControlCamera()
        {
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive() || ScenarioAuthoringRuntimeGuards.IsPlaytesting())
                return false;
            if (_panelVisibility != null && _panelVisibility.HasBlockingPanelOpen())
                return false;
            if (_inputCapture != null && _inputCapture.ShouldSuppressWorldInputNow())
                return false;
            return !IsTextFieldFocused();
        }

        private static BasicCamera ResolveBasicCamera()
        {
            Camera main = Camera.main;
            BasicCamera basic = main != null ? main.GetComponent<BasicCamera>() : null;
            return basic != null ? basic : Object.FindObjectOfType<BasicCamera>();
        }

        private static Vector3 ResolveKeyboardPan(Camera camera)
        {
            Vector2 direction = Vector2.zero;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
                direction.x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
                direction.x += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
                direction.y -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
                direction.y += 1f;

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            float zoomScale = Mathf.Max(0.5f, camera.orthographicSize / 4f);
            return new Vector3(direction.x, direction.y, 0f) * BasePanSpeed * zoomScale * Time.unscaledDeltaTime;
        }

        private Vector3 ResolveMiddleMousePan(Camera camera)
        {
            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                _middleDragging = true;
                _lastMousePosition = UnityEngine.Input.mousePosition;
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
                _lastMousePosition = UnityEngine.Input.mousePosition;
                return Vector3.zero;
            }

            Vector3 current = UnityEngine.Input.mousePosition;
            Vector3 lastWorld = ScreenToCameraPlane(camera, _lastMousePosition);
            Vector3 currentWorld = ScreenToCameraPlane(camera, current);
            _lastMousePosition = current;
            Vector3 delta = lastWorld - currentWorld;
            delta.z = 0f;
            return delta;
        }

        private void ApplyWheelZoom(Camera camera, BasicCamera basicCamera)
        {
            float wheel = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) <= 0.0001f)
                return;

            ScenarioAuthoringState state = ScenarioAuthoringBackendService.Instance.CurrentState;
            float scrollSpeed = state != null && state.Settings != null ? state.Settings.GetFloat("input.scroll_speed", 1f) : 1f;
            float nextSize = camera.orthographicSize - (wheel * ZoomStep * scrollSpeed);
            camera.orthographicSize = ClampZoom(camera, basicCamera, nextSize);
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
            if (topRight.y > bounds.yMin)
                position.y -= topRight.y - bounds.yMin;
            if (bottomLeft.y < bounds.yMax)
                position.y += bounds.yMax - bottomLeft.y;
            return position;
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
