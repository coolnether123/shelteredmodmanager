using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerMenuController : MonoBehaviour
    {
        private const string RuntimeObjectName = "ShelteredAPI.MultiplayerTestRuntime";
        private MultiplayerConnectionTestService _service;

        public MultiplayerConnectionTestService Service
        {
            get { return _service; }
        }

        public static void ShowWindow()
        {
            MultiplayerMenuController controller = EnsureController();
            if (controller == null)
                return;

            MultiplayerConnectionTestWindow window = controller.gameObject.GetComponent<MultiplayerConnectionTestWindow>();
            if (window == null)
                window = controller.gameObject.AddComponent<MultiplayerConnectionTestWindow>();

            window.Initialize(controller);
            window.enabled = true;
        }

        private static MultiplayerMenuController EnsureController()
        {
            GameObject runtimeObject = GameObject.Find(RuntimeObjectName);
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject(RuntimeObjectName);
                Object.DontDestroyOnLoad(runtimeObject);
            }

            MultiplayerMenuController controller = runtimeObject.GetComponent<MultiplayerMenuController>();
            if (controller == null)
                controller = runtimeObject.AddComponent<MultiplayerMenuController>();

            controller.EnsureService();
            return controller;
        }

        private void Awake()
        {
            EnsureService();
        }

        private void Update()
        {
            if (_service != null)
                _service.Update();
        }

        private void OnDestroy()
        {
            DisposeService();
        }

        private void OnApplicationQuit()
        {
            DisposeService();
        }

        private void EnsureService()
        {
            if (_service == null)
            {
                _service = new MultiplayerConnectionTestService();
                MMLog.WriteWithSource(MMLog.LogLevel.Debug, MMLog.LogCategory.General,
                    "ShelteredAPI.MultiplayerTest.Runtime", "Runtime service created.");
            }
        }

        private void DisposeService()
        {
            if (_service == null)
                return;

            _service.Dispose();
            _service = null;
        }
    }
}
