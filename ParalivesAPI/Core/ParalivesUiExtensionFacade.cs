using System;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesUiExtensionFacade
    {
        private readonly ParalivesOccupationPanelProviderRegistry _occupationPanels =
            new ParalivesOccupationPanelProviderRegistry();

        public int RegisteredOccupationPanelProviderCount
        {
            get { return _occupationPanels.RegisteredProviderCount; }
        }

        public IDisposable RegisterOccupationPanelProvider(IParalivesOccupationPanelProvider provider)
        {
            return _occupationPanels.Register(provider);
        }

        public IDisposable RegisterOccupationPanelProvider(
            Func<ulong, int, bool> canProvide,
            Func<ulong, int, ParalivesOccupationPanel> buildPanel)
        {
            return _occupationPanels.Register(canProvide, buildPanel);
        }

        public bool UnregisterOccupationPanelProvider(IParalivesOccupationPanelProvider provider)
        {
            return _occupationPanels.Unregister(provider);
        }

        internal bool TryApplyOccupationPanelProviders(global::UIOccupations window)
        {
            return _occupationPanels.TryApplyTo(window);
        }
    }
}
