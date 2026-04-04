using System;
using Cortex.Bridge;

namespace Cortex.Shell.Bridge
{
    internal interface IDesktopBridgeHost : IDisposable
    {
        bool IsClientConnected { get; }
        void Start();
        void Stop();
        bool TryDequeueInbound(out BridgeMessageEnvelope envelope);
        void EnqueueOutbound(BridgeMessageEnvelope envelope);
    }
}
