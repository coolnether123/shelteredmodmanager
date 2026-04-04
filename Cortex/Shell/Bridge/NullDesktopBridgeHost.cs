using Cortex.Bridge;

namespace Cortex.Shell.Bridge
{
    /// <summary>
    /// No-op bridge host used when the named-pipe transport is unavailable
    /// (e.g. Unity 5.3 / legacy Mono runtimes that do not support System.IO.Pipes).
    /// </summary>
    internal sealed class NullDesktopBridgeHost : IDesktopBridgeHost
    {
        public bool IsClientConnected { get { return false; } }

        public void Start() { }

        public void Stop() { }

        public bool TryDequeueInbound(out BridgeMessageEnvelope envelope)
        {
            envelope = null;
            return false;
        }

        public void EnqueueOutbound(BridgeMessageEnvelope envelope) { }

        public void Dispose() { }
    }
}
