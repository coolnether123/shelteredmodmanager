using System;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredMultiplayerWorldClockSyncService : IDisposable
    {
        private const float HostBroadcastIntervalSeconds = 0.25f;

        private readonly ShelteredMultiplayerWorldClock _clock;
        private float _secondsSinceBroadcast;
        private long _lastBroadcastTick = -1;
        private bool _disposed;

        public ShelteredMultiplayerWorldClockSyncService()
            : this(ShelteredMultiplayerWorldClock.Instance)
        {
        }

        internal ShelteredMultiplayerWorldClockSyncService(ShelteredMultiplayerWorldClock clock)
        {
            _clock = clock ?? ShelteredMultiplayerWorldClock.Instance;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
        }

        public void Update(float deltaSeconds)
        {
            if (_disposed)
                return;

            long tick = _clock.UpdateFromHostFrame(deltaSeconds);
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || context.Mode != ShelteredMultiplayerSessionMode.Host)
                return;

            _secondsSinceBroadcast += deltaSeconds > 0f ? deltaSeconds : 0f;
            if (tick == _lastBroadcastTick)
                return;
            if (_lastBroadcastTick >= 0 && _secondsSinceBroadcast < HostBroadcastIntervalSeconds)
                return;

            if (_clock.BroadcastLocalSample())
            {
                _lastBroadcastTick = tick;
                _secondsSinceBroadcast = 0f;
            }
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
