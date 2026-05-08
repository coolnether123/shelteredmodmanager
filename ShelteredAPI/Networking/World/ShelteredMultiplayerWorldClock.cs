using System;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredMultiplayerWorldClock
    {
        internal const int DefaultTickRate = 20;

        private const string FixedAdvanceReason = "world-clock-fixed-advance";
        private const string RuntimeBridgeAdvanceReason = "world-clock-runtime-bridge-advance";
        private const string RemoteCorrectionReason = "world-clock-remote-correction";
        private const string ResetReasonPrefix = "world-clock-reset";
        private static readonly ShelteredMultiplayerWorldClock _instance =
            new ShelteredMultiplayerWorldClock(ShelteredMultiplayerSessionCoordinator.Instance);

        private readonly object _sync = new object();
        private readonly ShelteredMultiplayerSessionCoordinator _coordinator;
        private readonly IShelteredWorldTickScheduler _scheduler;
        private readonly ShelteredWorldClockDriftPolicy _driftPolicy;
        private ShelteredWorldClockSample _lastHostSample;

        internal ShelteredMultiplayerWorldClock()
            : this(ShelteredMultiplayerSessionCoordinator.Instance)
        {
        }

        internal ShelteredMultiplayerWorldClock(ShelteredMultiplayerSessionCoordinator coordinator)
            : this(coordinator, new ShelteredWorldTickScheduler(), new ShelteredWorldClockDriftPolicy())
        {
        }

        internal ShelteredMultiplayerWorldClock(
            ShelteredMultiplayerSessionCoordinator coordinator,
            IShelteredWorldTickScheduler scheduler,
            ShelteredWorldClockDriftPolicy driftPolicy)
        {
            if (coordinator == null)
                throw new ArgumentNullException("coordinator");

            _coordinator = coordinator;
            _scheduler = scheduler ?? new ShelteredWorldTickScheduler();
            _driftPolicy = driftPolicy ?? new ShelteredWorldClockDriftPolicy();
        }

        public static ShelteredMultiplayerWorldClock Instance
        {
            get { return _instance; }
        }

        public long AdvanceFixedSteps(long stepCount)
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            if (context == null || !context.IsMultiplayerActive)
                return 0;

            return Advance(context, _scheduler.AdvanceFixedSteps(stepCount, context.TickRate), FixedAdvanceReason);
        }

        public long AdvanceRuntimeBridgeDelta(float deltaSeconds)
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            if (context == null || !context.IsMultiplayerActive)
                return 0;

            return Advance(
                context,
                _scheduler.AccumulateFixedInterval(deltaSeconds, context.TickRate),
                RuntimeBridgeAdvanceReason);
        }

        public long AdvanceFixedDelta(float deltaSeconds)
        {
            return AdvanceRuntimeBridgeDelta(deltaSeconds);
        }

        public bool ApplyRemoteSample(ShelteredWorldClockSample sample)
        {
            return ApplyRemoteSampleDetailed(sample).Applied;
        }

        public ShelteredWorldClockCorrectionResult ApplyRemoteSampleDetailed(ShelteredWorldClockSample sample)
        {
            if (sample == null)
                return ShelteredWorldClockCorrectionResult.Ignored("missing-sample", GetCurrentTick(), 0);

            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            long currentTick = NormalizeTick(context != null ? context.WorldTick : 0);
            if (context == null || context.Mode != ShelteredMultiplayerSessionMode.Client)
                return ShelteredWorldClockCorrectionResult.Ignored("not-client-session", currentTick, NormalizeTick(sample.WorldTick));
            if (!sample.HostAuthoritative)
                return ShelteredWorldClockCorrectionResult.Ignored("non-authoritative-sample", currentTick, NormalizeTick(sample.WorldTick));
            if (!IsCurrentSession(context, sample.SessionId))
                return ShelteredWorldClockCorrectionResult.Ignored("foreign-session-sample", currentTick, NormalizeTick(sample.WorldTick));

            long sampleTick = NormalizeTick(sample.WorldTick);
            float sampleDelta = sample.DeltaSeconds < 0f ? 0f : sample.DeltaSeconds;
            if (sampleTick <= currentTick)
                return ShelteredWorldClockCorrectionResult.Ignored("stale-or-equal-host-sample", currentTick, sampleTick);

            ShelteredWorldClockSample normalizedSample = CopySample(sample, sampleTick, sampleDelta);
            lock (_sync)
            {
                if (_lastHostSample != null && sampleTick <= _lastHostSample.WorldTick)
                    return ShelteredWorldClockCorrectionResult.Ignored("non-monotonic-host-sample", currentTick, sampleTick);

                _lastHostSample = normalizedSample;
            }

            ShelteredWorldClockDriftDecision decision = _driftPolicy.Evaluate(currentTick, sampleTick);
            if (!decision.CanApplyCorrection)
            {
                return ShelteredWorldClockCorrectionResult.DesyncRequired(
                    decision.Reason,
                    currentTick,
                    sampleTick,
                    decision);
            }

            _scheduler.Reset();
            _coordinator.SetWorldTick(sampleTick, sampleDelta, RemoteCorrectionReason);
            return ShelteredWorldClockCorrectionResult.AppliedCorrection(
                decision.Reason,
                currentTick,
                sampleTick,
                decision);
        }

        public ShelteredWorldClockSample BuildLocalSample()
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            if (context == null)
                return new ShelteredWorldClockSample
                {
                    TickRate = DefaultTickRate,
                    SampleUtc = ShelteredWorldClockDiagnosticTime.UtcNow()
                };

            // SampleUtc is diagnostic metadata only; gameplay progression is driven by fixed scheduler ticks.
            return new ShelteredWorldClockSample
            {
                SessionId = context.SessionId,
                WorldTick = NormalizeTick(context.WorldTick),
                DeltaSeconds = context.WorldDeltaSeconds < 0f ? 0f : context.WorldDeltaSeconds,
                TickRate = NormalizeTickRate(context.TickRate),
                SampleUtc = ShelteredWorldClockDiagnosticTime.UtcNow(),
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
            DateTime sampleUtc = lastSample != null ? lastSample.SampleUtc : ShelteredWorldClockDiagnosticTime.UtcNow();
            TimeSpan sampleAge = sampleUtc > DateTime.MinValue
                ? ShelteredWorldClockDiagnosticTime.UtcNow() - sampleUtc
                : TimeSpan.Zero;
            if (sampleAge < TimeSpan.Zero)
                sampleAge = TimeSpan.Zero;

            ShelteredWorldClockDriftDecision decision = _driftPolicy.Evaluate(localTick, hostTick);
            return new ShelteredWorldClockDriftReport
            {
                LocalTick = localTick,
                HostTick = hostTick,
                DriftTicks = localTick - hostTick,
                SampleAge = sampleAge,
                IsHostAuthoritative = context != null && context.Mode == ShelteredMultiplayerSessionMode.Host,
                Severity = decision.Severity,
                RequiresDesyncDiagnostics = decision.RequiresDesyncDiagnostics
            };
        }

        public void Reset(string reason)
        {
            _scheduler.Reset();
            lock (_sync)
            {
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
            return TryApplyAuthoritativeEventDetailed(gameplayEvent).Applied;
        }

        public ShelteredWorldClockCorrectionResult TryApplyAuthoritativeEventDetailed(
            ShelteredNetworkGameplayEvent gameplayEvent)
        {
            ShelteredWorldClockSample sample;
            if (!ShelteredWorldClockSampleCodec.TryFromGameplayEvent(gameplayEvent, out sample))
                return ShelteredWorldClockCorrectionResult.Ignored("not-world-clock-sample", GetCurrentTick(), 0);

            return ApplyRemoteSampleDetailed(sample);
        }

        private long Advance(
            ShelteredMultiplayerSessionContext context,
            ShelteredWorldTickAdvance advance,
            string reason)
        {
            if (advance == null || advance.TicksToAdvance <= 0)
                return context.WorldTick;

            long nextTick = context.WorldTick + advance.TicksToAdvance;
            if (nextTick < 0)
                nextTick = 0;

            return _coordinator.SetWorldTick(nextTick, advance.DeltaSeconds, reason).WorldTick;
        }

        private static int NormalizeTickRate(int tickRate)
        {
            return tickRate > 0 ? tickRate : DefaultTickRate;
        }

        private static long NormalizeTick(long tick)
        {
            return tick > 0 ? tick : 0;
        }

        private static ShelteredWorldClockSample CopySample(
            ShelteredWorldClockSample sample,
            long sampleTick,
            float sampleDelta)
        {
            return new ShelteredWorldClockSample
            {
                SessionId = sample.SessionId,
                WorldTick = sampleTick,
                DeltaSeconds = sampleDelta,
                TickRate = NormalizeTickRate(sample.TickRate),
                SampleUtc = sample.SampleUtc,
                HostAuthoritative = sample.HostAuthoritative
            };
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
