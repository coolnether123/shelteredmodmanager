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

        public void ConfigureHostServices(ICortexHostServices hostServices)
        {
            _controller.ConfigureHostServices(hostServices);
        }

        private void Awake()
        {
            gameObject.name = "Cortex.Shell";
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _controller.StartShell();
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
    }
}
