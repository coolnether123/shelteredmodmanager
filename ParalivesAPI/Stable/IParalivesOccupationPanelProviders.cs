using System;
using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupationPanelProviders
    {
        int RegisteredProviderCount { get; }

        IDisposable Register(IParalivesOccupationPanelProvider provider);

        IDisposable Register(
            Func<ulong, int, bool> canProvide,
            Func<ulong, int, ParalivesOccupationPanel> buildPanel);

        bool Unregister(IParalivesOccupationPanelProvider provider);
    }
}
