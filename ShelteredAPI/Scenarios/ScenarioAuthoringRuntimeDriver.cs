using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
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
                ScenarioAuthoringBootstrapService.Instance.Update();
                ScenarioSpriteSwapService.Instance.Update();
            }
            catch (System.Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringRuntimeDriver] Update failed: " + ex);
            }
        }
    }
}
