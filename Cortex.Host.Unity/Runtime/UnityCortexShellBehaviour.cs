using Cortex;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering.RuntimeUi;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    /// <summary>
    /// Unity host adapter that forwards MonoBehaviour lifecycle events into the host-neutral Cortex shell controller.
    /// </summary>
    public sealed class UnityCortexShellBehaviour : MonoBehaviour, IUnityRenderPresentationHost
    {
        private readonly CortexShellController _controller = new CortexShellController();
        private ICortexHostServices _hostServices;
        private UnityRenderPresentationCoordinator _presentationCoordinator;

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
            SynchronizePresentation();
        }

        private void OnDestroy()
        {
            if (_presentationCoordinator != null)
            {
                _presentationCoordinator.Shutdown(this);
            }

            _controller.ShutdownShell();
        }

        private void Update()
        {
            _controller.UpdateShell();
            SynchronizePresentation();
        }

        private void OnGUI()
        {
            _controller.RenderShell();
        }

        private void SynchronizePresentation()
        {
            if (_presentationCoordinator == null)
            {
                _presentationCoordinator = new UnityRenderPresentationCoordinator(
                    _hostServices != null ? _hostServices.FrameContext : null,
                    new UnityRenderHostCatalogBuilder(),
                    new UnityExternalHostLauncher());
            }

            _presentationCoordinator.Synchronize(this, _hostServices != null ? _hostServices.Environment : null);
        }

        CortexSettings IUnityRenderPresentationHost.Settings
        {
            get { return _controller.CurrentSettings; }
        }

        void IUnityRenderPresentationHost.ApplyStatusMessage(string statusMessage)
        {
            _controller.SetHostStatusMessage(statusMessage);
        }

        bool IUnityRenderPresentationHost.ApplyRuntimeUiFactory(IWorkbenchRuntimeUiFactory runtimeUiFactory)
        {
            return _controller.ApplyRuntimeUiFactory(runtimeUiFactory);
        }

        void IUnityRenderPresentationHost.RequestExternalHostShutdown()
        {
            _controller.RequestExternalHostShutdown();
        }

        void IUnityRenderPresentationHost.RegisterOrUpdateStatusItem(StatusItemContribution contribution)
        {
            _controller.RegisterOrUpdateStatusItem(contribution);
        }

        void IUnityRenderPresentationHost.RegisterOrUpdateSettingContribution(SettingContribution contribution)
        {
            _controller.RegisterOrUpdateSettingContribution(contribution);
        }
    }
}
