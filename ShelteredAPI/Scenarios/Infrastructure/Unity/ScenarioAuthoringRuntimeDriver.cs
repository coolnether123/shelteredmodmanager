using ModAPI.Core;
using ShelteredAPI.Core;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioAuthoringRuntimeDriver : MonoBehaviour
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioAuthoring.RuntimeDriver";
        private static ScenarioAuthoringRuntimeDriver _instance;

        public static void EnsureCreated()
        {
            if (_instance != null)
                return;

            GameObject runtimeObject = GameObject.Find(RuntimeObjectName);
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject(RuntimeObjectName);
                DontDestroyOnLoad(runtimeObject);
                MMLog.WriteInfo("[ScenarioAuthoringRuntimeDriver] Created runtime driver GameObject.");
            }

            _instance = runtimeObject.GetComponent<ScenarioAuthoringRuntimeDriver>();
            if (_instance == null)
            {
                _instance = runtimeObject.AddComponent<ScenarioAuthoringRuntimeDriver>();
                MMLog.WriteInfo("[ScenarioAuthoringRuntimeDriver] Added runtime driver component.");
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            try
            {
                ScenarioCompositionRoot.EnsureAuthoringInitialized();
                ShelteredApiRuntimeBootstrap.EnsureAuthoringApiRegistered();
                ScenarioAuthoringBootstrapService.Instance.Update();
                ScenarioSpriteSwapService.Instance.Update();
                ScenarioCompositionRoot.Resolve<ScenarioAuthoringEditorCameraService>().Update();
            }
            catch (System.Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringRuntimeDriver] Update failed: " + ex);
            }
        }
    }
}
