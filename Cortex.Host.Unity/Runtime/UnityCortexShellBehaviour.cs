using Cortex;
using Cortex.Presentation.Abstractions;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    /// <summary>
    /// Unity host adapter that forwards MonoBehaviour lifecycle events into the host-neutral Cortex shell controller.
    /// </summary>
    public sealed class UnityCortexShellBehaviour : MonoBehaviour
    {
        private readonly CortexShellController _controller = new CortexShellController();
        private ICortexHostServices _hostServices;

        public void ConfigureHostServices(ICortexHostServices hostServices)
        {
            _hostServices = hostServices;
            _controller.ConfigureHostServices(_hostServices);
        }

        private void Awake()
        {
            gameObject.name = "Cortex.Shell";
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _controller.StartShell();
            RunStartupAction();
        }

        private void OnDestroy()
        {
            _controller.ShutdownShell();
        }

        private void Update()
        {
            _controller.UpdateShell();
        }

        private void OnGUI()
        {
            _controller.RenderShell();
        }

        private void RunStartupAction()
        {
            var unityHostServices = _hostServices as UnityCortexHostServices;
            var startupAction = unityHostServices != null ? unityHostServices.StartupAction : null;
            if (startupAction == null)
            {
                return;
            }

            try
            {
                startupAction.OnShellStarted(new UnityShellStartupContext(
                    _hostServices,
                    _controller.SetHostStatusMessage));
            }
            catch (System.Exception ex)
            {
                _controller.SetHostStatusMessage("Host startup action failed: " + ex.Message);
                Debug.LogError("[Cortex.Host.Unity] Host startup action failed: " + ex);
            }
        }
    }
}
