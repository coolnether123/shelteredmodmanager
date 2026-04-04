using System;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityShellStartupContext
    {
        private readonly Action<string> _setStatusMessage;

        public UnityShellStartupContext(ICortexHostServices hostServices, Action<string> setStatusMessage)
        {
            HostServices = hostServices;
            _setStatusMessage = setStatusMessage;
        }

        public ICortexHostServices HostServices { get; private set; }

        public void SetStatusMessage(string statusMessage)
        {
            if (_setStatusMessage != null && !string.IsNullOrEmpty(statusMessage))
            {
                _setStatusMessage(statusMessage);
            }
        }
    }

    public interface IUnityShellStartupAction
    {
        void OnShellStarted(UnityShellStartupContext context);
    }
}
