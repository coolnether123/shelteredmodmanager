using ModAPI.Core;
using ShelteredAPI.Core;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
namespace ShelteredScenarioEditor.Infrastructure.Unity{
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

            if (runtimeObject.GetComponent<ScenarioEditorLauncherOverlay>() == null)
                runtimeObject.AddComponent<ScenarioEditorLauncherOverlay>();
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
                ScenarioOpeningCutsceneAuthoringService openingCutscene = ScenarioCompositionRoot.Resolve<ScenarioOpeningCutsceneAuthoringService>();
                if (ScenarioOpeningCutsceneAuthoringService.IsPreviewActive)
                {
                    ScenarioCompositionRoot.Resolve<ScenarioAuthoringPresentationService>().Update();
                    openingCutscene.UpdateActivePreview();
                }
                else
                {
                    ScenarioAuthoringBootstrapService.Instance.Update();
                    openingCutscene.UpdateActivePreview();
                    openingCutscene.UpdateAuthoringIntroCutsceneFallback();
                    ScenarioOpeningCutsceneAuthoringService.RestoreStaleCutscenePanelIfAuthoringVisible();
                }
                ScenarioCompositionRoot.Resolve<ScenarioAuthoringEditorCameraService>().Update();
            }
            catch (System.Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringRuntimeDriver] Update failed: " + ex);
            }
        }
    }
}
