using System;

namespace ShelteredAPI.Networking.World
{
    internal enum ShelteredWorldClockDriftSeverity
    {
        None = 0,
        SmallCorrection = 1,
        DesyncDiagnosticsRequired = 2
    }

    internal sealed class ShelteredWorldClockDriftDecision
    {
        public ShelteredWorldClockDriftSeverity Severity { get; set; }

        public long LocalTick { get; set; }

        public long HostTick { get; set; }

        public long DriftTicks { get; set; }

        public int SmallCorrectionMaxTicks { get; set; }

        public bool CanApplyCorrection { get; set; }

        public bool RequiresDesyncDiagnostics { get; set; }

        public string Reason { get; set; }
    }

    internal sealed class ShelteredWorldClockDriftPolicy
    {
        public const int DefaultSmallCorrectionMaxTicks = 20;

        private readonly int _smallCorrectionMaxTicks;

        public ShelteredWorldClockDriftPolicy()
            : this(DefaultSmallCorrectionMaxTicks)
        {
        }

        internal ShelteredWorldClockDriftPolicy(int smallCorrectionMaxTicks)
        {
            _smallCorrectionMaxTicks = smallCorrectionMaxTicks > 0
                ? smallCorrectionMaxTicks
                : DefaultSmallCorrectionMaxTicks;
        }

        public int SmallCorrectionMaxTicks
        {
            get { return _smallCorrectionMaxTicks; }
        }

        public ShelteredWorldClockDriftDecision Evaluate(long localTick, long hostTick)
        {
            long normalizedLocalTick = localTick > 0 ? localTick : 0;
            long normalizedHostTick = hostTick > 0 ? hostTick : 0;
            long driftTicks = normalizedHostTick - normalizedLocalTick;
            long absoluteDriftTicks = AbsoluteTicks(driftTicks);

            if (absoluteDriftTicks == 0)
                return Create(
                    ShelteredWorldClockDriftSeverity.None,
                    normalizedLocalTick,
                    normalizedHostTick,
                    driftTicks,
                    false,
                    false,
                    "no-drift");

            if (absoluteDriftTicks <= _smallCorrectionMaxTicks)
                return Create(
                    ShelteredWorldClockDriftSeverity.SmallCorrection,
                    normalizedLocalTick,
                    normalizedHostTick,
                    driftTicks,
                    true,
                    false,
                    "small-correction");

            return Create(
                ShelteredWorldClockDriftSeverity.DesyncDiagnosticsRequired,
                normalizedLocalTick,
                normalizedHostTick,
                driftTicks,
                false,
                true,
                "large-drift-desync-diagnostics-required");
        }

        private ShelteredWorldClockDriftDecision Create(
            ShelteredWorldClockDriftSeverity severity,
            long localTick,
            long hostTick,
            long driftTicks,
            bool canApplyCorrection,
            bool requiresDesyncDiagnostics,
            string reason)
        {
            return new ShelteredWorldClockDriftDecision
            {
                Severity = severity,
                LocalTick = localTick,
                HostTick = hostTick,
                DriftTicks = driftTicks,
                SmallCorrectionMaxTicks = _smallCorrectionMaxTicks,
                CanApplyCorrection = canApplyCorrection,
                RequiresDesyncDiagnostics = requiresDesyncDiagnostics,
                Reason = reason ?? string.Empty
            };
        }

        private static long AbsoluteTicks(long value)
        {
            if (value == long.MinValue)
                return long.MaxValue;

            return Math.Abs(value);
        }
    }
}
