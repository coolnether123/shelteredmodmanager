using System;
using ModAPI.Core;
using ShelteredAPI.Networking.Diagnostics;

namespace ShelteredAPI.Networking.World
{
    /// <summary>
    /// Advances the shared world clock from deterministic fixed rules and listens for rare
    /// authoritative correction samples. It intentionally does not broadcast periodic clock ticks.
    /// </summary>
    internal sealed class ShelteredMultiplayerWorldClockCorrectionService : IDisposable
    {
        private readonly ShelteredMultiplayerWorldClock _clock;
        private bool _disposed;

        public ShelteredMultiplayerWorldClockCorrectionService()
            : this(ShelteredMultiplayerWorldClock.Instance)
        {
        }

        internal ShelteredMultiplayerWorldClockCorrectionService(ShelteredMultiplayerWorldClock clock)
        {
            _clock = clock ?? ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
        }

        public void Update(float deltaSeconds)
        {
            UpdateFromRuntimeFrame(deltaSeconds);
        }

        public void UpdateFromRuntimeFrame(float deltaSeconds)
        {
            if (_disposed)
                return;

            _clock.AdvanceRuntimeBridgeDelta(deltaSeconds);
        }

        public void AdvanceFixedSteps(long stepCount)
        {
            if (_disposed)
                return;

            _clock.AdvanceFixedSteps(stepCount);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived -= OnAuthoritativeReceived;
            _disposed = true;
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null)
                return;

            ShelteredWorldClockCorrectionResult result = _clock.TryApplyAuthoritativeEventDetailed(context.GameplayEvent);
            if (result != null && result.RequiresDesyncDiagnostics)
                TryWriteDriftDiagnostic(result);
        }

        private static void TryWriteDriftDiagnostic(ShelteredWorldClockCorrectionResult result)
        {
            try
            {
                MMLog.WriteWithSource(
                    MMLog.LogLevel.Warning,
                    MMLog.LogCategory.Network,
                    "ShelteredAPI.WorldClock",
                    "Large host world-clock drift detected. localTick=" + result.LocalTick
                    + ", hostTick=" + result.HostTick
                    + ", driftTicks=" + result.DriftTicks
                    + ", reason=" + (result.Reason ?? string.Empty) + ".");
                new ShelteredMultiplayerDesyncDiagnostics().DumpReport("world-clock-large-drift");
            }
            catch
            {
                // GuardrailAllow: SilentCatch - drift diagnostics are best-effort and must not break event handling.
            }
        }
    }
}
