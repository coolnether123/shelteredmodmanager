using System;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredMultiplayerWorldClock
    {
        internal const int DefaultTickRate = 20;

        private const string FixedAdvanceReason = "world-clock-fixed-advance";
        private const string RemoteCorrectionReason = "world-clock-remote-correction";
        private const string ResetReasonPrefix = "world-clock-reset";
        private static readonly ShelteredMultiplayerWorldClock _instance =
            new ShelteredMultiplayerWorldClock(ShelteredMultiplayerSessionCoordinator.Instance);

        private readonly object _sync = new object();
        private readonly ShelteredMultiplayerSessionCoordinator _coordinator;
        private double _fractionalTicks;
        private ShelteredWorldClockSample _lastHostSample;

        internal ShelteredMultiplayerWorldClock()
            : this(ShelteredMultiplayerSessionCoordinator.Instance)
        {
        }

        internal ShelteredMultiplayerWorldClock(ShelteredMultiplayerSessionCoordinator coordinator)
        {
            if (coordinator == null)
                throw new ArgumentNullException("coordinator");

            _coordinator = coordinator;
        }

        public static ShelteredMultiplayerWorldClock Instance
        {
            get { return _instance; }
        }

        public long AdvanceFixedDelta(float deltaSeconds)
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            if (context == null || !context.IsMultiplayerActive)
                return 0;

            return Advance(context, deltaSeconds, FixedAdvanceReason);
        }

        public bool ApplyRemoteSample(ShelteredWorldClockSample sample)
        {
            if (sample == null)
                return false;

            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            if (context == null
                || context.Mode != ShelteredMultiplayerSessionMode.Client
                || !sample.HostAuthoritative
                || !IsCurrentSession(context, sample.SessionId))
            {
                return false;
            }

            long sampleTick = sample.WorldTick < 0 ? 0 : sample.WorldTick;
            float sampleDelta = sample.DeltaSeconds < 0f ? 0f : sample.DeltaSeconds;
            long currentTick = context.WorldTick < 0 ? 0 : context.WorldTick;
            if (sampleTick < currentTick)
                return false;

            lock (_sync)
            {
                if (_lastHostSample != null && sampleTick < _lastHostSample.WorldTick)
                    return false;

                _lastHostSample = new ShelteredWorldClockSample
                {
                    SessionId = sample.SessionId,
                    WorldTick = sampleTick,
                    DeltaSeconds = sampleDelta,
                    TickRate = sample.TickRate,
                    SampleUtc = sample.SampleUtc,
                    HostAuthoritative = sample.HostAuthoritative
                };
            }

            if (sampleTick <= currentTick)
                return false;

            lock (_sync)
            {
                _fractionalTicks = 0d;
            }

            _coordinator.SetWorldTick(sampleTick, sampleDelta, RemoteCorrectionReason);
            return true;
        }

        public ShelteredWorldClockSample BuildLocalSample()
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            if (context == null)
                return new ShelteredWorldClockSample { TickRate = DefaultTickRate, SampleUtc = DateTime.UtcNow };

            // SampleUtc is diagnostic metadata only; gameplay progression is driven by fixed deltas and ticks.
            return new ShelteredWorldClockSample
            {
                SessionId = context.SessionId,
                WorldTick = context.WorldTick < 0 ? 0 : context.WorldTick,
                DeltaSeconds = context.WorldDeltaSeconds < 0f ? 0f : context.WorldDeltaSeconds,
                TickRate = NormalizeTickRate(context.TickRate),
                SampleUtc = DateTime.UtcNow,
                HostAuthoritative = context.Mode == ShelteredMultiplayerSessionMode.Host
            };
        }

        public long GetCurrentTick()
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            return context != null && context.WorldTick > 0 ? context.WorldTick : 0;
        }

        public ShelteredWorldClockDriftReport GetDriftReport()
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            ShelteredWorldClockSample lastSample;
            lock (_sync)
            {
                lastSample = _lastHostSample != null ? _lastHostSample.Copy() : null;
            }

            long localTick = context != null ? context.WorldTick : 0;
            long hostTick = lastSample != null ? lastSample.WorldTick : localTick;
            DateTime sampleUtc = lastSample != null ? lastSample.SampleUtc : DateTime.UtcNow;
            TimeSpan sampleAge = sampleUtc > DateTime.MinValue ? DateTime.UtcNow - sampleUtc : TimeSpan.Zero;
            if (sampleAge < TimeSpan.Zero)
                sampleAge = TimeSpan.Zero;

            return new ShelteredWorldClockDriftReport
            {
                LocalTick = localTick,
                HostTick = hostTick,
                DriftTicks = localTick - hostTick,
                SampleAge = sampleAge,
                IsHostAuthoritative = context != null && context.Mode == ShelteredMultiplayerSessionMode.Host
            };
        }

        public void Reset(string reason)
        {
            lock (_sync)
            {
                _fractionalTicks = 0d;
                _lastHostSample = null;
            }

            _coordinator.SetWorldTick(0, 0f, BuildResetReason(reason));
        }

        public bool BroadcastLocalSample()
        {
            ShelteredWorldClockSample sample = BuildLocalSample();
            if (!sample.HostAuthoritative || !ShelteredMultiplayerNetworkEvents.IsAvailable)
                return false;

            return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(
                ShelteredWorldClockSampleCodec.ToGameplayEvent(sample));
        }

        public bool TryApplyAuthoritativeEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            ShelteredWorldClockSample sample;
            return ShelteredWorldClockSampleCodec.TryFromGameplayEvent(gameplayEvent, out sample)
                && ApplyRemoteSample(sample);
        }

        private long Advance(ShelteredMultiplayerSessionContext context, float deltaSeconds, string reason)
        {
            float normalizedDelta = deltaSeconds < 0f ? 0f : deltaSeconds;
            int tickRate = NormalizeTickRate(context.TickRate);
            long ticksToAdvance;

            lock (_sync)
            {
                _fractionalTicks += normalizedDelta * tickRate;
                ticksToAdvance = (long)Math.Floor(_fractionalTicks);
                if (ticksToAdvance <= 0)
                    return context.WorldTick;

                _fractionalTicks -= ticksToAdvance;
            }

            long nextTick = context.WorldTick + ticksToAdvance;
            if (nextTick < 0)
                nextTick = 0;

            return _coordinator.SetWorldTick(nextTick, normalizedDelta, reason).WorldTick;
        }

        private static int NormalizeTickRate(int tickRate)
        {
            return tickRate > 0 ? tickRate : DefaultTickRate;
        }

        private static bool IsCurrentSession(ShelteredMultiplayerSessionContext context, string sessionId)
        {
            if (context == null)
                return false;

            return string.Equals(context.SessionId ?? string.Empty, sessionId ?? string.Empty, StringComparison.Ordinal);
        }

        private static string BuildResetReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return ResetReasonPrefix;

            return ResetReasonPrefix + ":" + reason;
        }
    }
}
