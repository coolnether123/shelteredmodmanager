using System;

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
            if (_disposed)
                return;

            _clock.AdvanceFixedDelta(deltaSeconds);
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

            _clock.TryApplyAuthoritativeEvent(context.GameplayEvent);
        }
    }
}
