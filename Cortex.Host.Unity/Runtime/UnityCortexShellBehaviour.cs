using Cortex;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering.RuntimeUi;
using Cortex.Renderers.DearImgui;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    /// <summary>
    /// Unity host adapter that forwards MonoBehaviour lifecycle events into the host-neutral Cortex shell controller.
    /// </summary>
    public sealed class UnityCortexShellBehaviour : MonoBehaviour, IUnityRenderPresentationHost
    {
        private const int OverlayGuiDepth = -10000;
        private readonly CortexShellController _controller = new CortexShellController();
        private ICortexHostServices _hostServices;
        private UnityRenderPresentationCoordinator _presentationCoordinator;
        private DearImguiPresentationBehaviour _dearImguiPresentation;

        public void ConfigureHostServices(ICortexHostServices hostServices)
        {
            _hostServices = hostServices;
            _controller.ConfigureHostServices(_hostServices);
        }

        private void Awake()
        {
            gameObject.name = "Cortex.Shell";
            DontDestroyOnLoad(gameObject);
            AttachDearImguiPresentation();
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
            var previousDepth = GUI.depth;
            GUI.depth = OverlayGuiDepth;
            if (string.Equals(_controller.CurrentRendererId, DearImguiWorkbenchRenderer.RendererIdValue, System.StringComparison.OrdinalIgnoreCase))
            {
                if (_dearImguiPresentation != null)
                {
                    _dearImguiPresentation.RenderOnGui();
                }

                GUI.depth = previousDepth;
                return;
            }

            try
            {
                _controller.RenderShell();
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }

        private void AttachDearImguiPresentation()
        {
            if (_dearImguiPresentation == null)
            {
                _dearImguiPresentation = gameObject.GetComponent<DearImguiPresentationBehaviour>();
                if (_dearImguiPresentation == null)
                {
                    _dearImguiPresentation = gameObject.AddComponent<DearImguiPresentationBehaviour>();
                }
            }

            _dearImguiPresentation.Configure(_controller);
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
