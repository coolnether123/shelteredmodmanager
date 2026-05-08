using System;
using System.Globalization;
using System.Text;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredWorldClockSample
    {
        public ShelteredWorldClockSample()
        {
            SessionId = string.Empty;
            SampleUtc = DateTime.MinValue;
        }

        public string SessionId { get; set; }
        public long WorldTick { get; set; }
        public float DeltaSeconds { get; set; }
        public int TickRate { get; set; }
        public DateTime SampleUtc { get; set; }
        public bool HostAuthoritative { get; set; }

        public ShelteredWorldClockSample Copy()
        {
            return new ShelteredWorldClockSample
            {
                SessionId = SessionId ?? string.Empty,
                WorldTick = WorldTick,
                DeltaSeconds = DeltaSeconds,
                TickRate = TickRate,
                SampleUtc = SampleUtc,
                HostAuthoritative = HostAuthoritative
            };
        }
    }

    internal sealed class ShelteredWorldClockDriftReport
    {
        public long LocalTick { get; set; }
        public long HostTick { get; set; }
        public long DriftTicks { get; set; }
        public TimeSpan SampleAge { get; set; }
        public bool IsHostAuthoritative { get; set; }
        public ShelteredWorldClockDriftSeverity Severity { get; set; }
        public bool RequiresDesyncDiagnostics { get; set; }
    }

    internal static class ShelteredWorldClockDiagnosticTime
    {
        public static DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }
    }

    internal enum ShelteredWorldClockCorrectionOutcome
    {
        Ignored = 0,
        Applied = 1,
        DesyncDiagnosticsRequired = 2
    }

    internal sealed class ShelteredWorldClockCorrectionResult
    {
        public ShelteredWorldClockCorrectionOutcome Outcome { get; set; }

        public string Reason { get; set; }

        public long LocalTick { get; set; }

        public long HostTick { get; set; }

        public long DriftTicks { get; set; }

        public ShelteredWorldClockDriftDecision DriftDecision { get; set; }

        public bool Applied
        {
            get { return Outcome == ShelteredWorldClockCorrectionOutcome.Applied; }
        }

        public bool RequiresDesyncDiagnostics
        {
            get
            {
                return Outcome == ShelteredWorldClockCorrectionOutcome.DesyncDiagnosticsRequired
                    || (DriftDecision != null && DriftDecision.RequiresDesyncDiagnostics);
            }
        }

        public static ShelteredWorldClockCorrectionResult Ignored(string reason, long localTick, long hostTick)
        {
            return Create(ShelteredWorldClockCorrectionOutcome.Ignored, reason, localTick, hostTick, null);
        }

        public static ShelteredWorldClockCorrectionResult AppliedCorrection(
            string reason,
            long localTick,
            long hostTick,
            ShelteredWorldClockDriftDecision decision)
        {
            return Create(ShelteredWorldClockCorrectionOutcome.Applied, reason, localTick, hostTick, decision);
        }

        public static ShelteredWorldClockCorrectionResult DesyncRequired(
            string reason,
            long localTick,
            long hostTick,
            ShelteredWorldClockDriftDecision decision)
        {
            return Create(
                ShelteredWorldClockCorrectionOutcome.DesyncDiagnosticsRequired,
                reason,
                localTick,
                hostTick,
                decision);
        }

        private static ShelteredWorldClockCorrectionResult Create(
            ShelteredWorldClockCorrectionOutcome outcome,
            string reason,
            long localTick,
            long hostTick,
            ShelteredWorldClockDriftDecision decision)
        {
            return new ShelteredWorldClockCorrectionResult
            {
                Outcome = outcome,
                Reason = reason ?? string.Empty,
                LocalTick = localTick,
                HostTick = hostTick,
                DriftTicks = decision != null ? decision.DriftTicks : hostTick - localTick,
                DriftDecision = decision
            };
        }
    }

    internal static class ShelteredWorldClockSampleCodec
    {
        public static ShelteredNetworkGameplayEvent ToGameplayEvent(ShelteredWorldClockSample sample)
        {
            ShelteredWorldClockSample normalized = Normalize(sample);
            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = ShelteredNetworkEventKinds.WorldClockSample;
            gameplayEvent.ActorId = normalized.SessionId;
            gameplayEvent.WorldTick = ToUInt32Tick(normalized.WorldTick);
            gameplayEvent.Details = Encode(normalized);
            return gameplayEvent;
        }

        public static bool TryFromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent, out ShelteredWorldClockSample sample)
        {
            sample = null;
            if (gameplayEvent == null
                || !string.Equals(gameplayEvent.EventKind, ShelteredNetworkEventKinds.WorldClockSample, StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryDecode(gameplayEvent.Details, out sample))
                return false;

            if (sample.SessionId.Length == 0)
                sample.SessionId = gameplayEvent.ActorId ?? string.Empty;

            return true;
        }

        private static string Encode(ShelteredWorldClockSample sample)
        {
            string[] fields = new string[]
            {
                "1",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(sample.SessionId ?? string.Empty)),
                sample.WorldTick.ToString(CultureInfo.InvariantCulture),
                sample.DeltaSeconds.ToString("R", CultureInfo.InvariantCulture),
                sample.TickRate.ToString(CultureInfo.InvariantCulture),
                sample.SampleUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                sample.HostAuthoritative ? "1" : "0"
            };

            return string.Join("|", fields);
        }

        private static bool TryDecode(string details, out ShelteredWorldClockSample sample)
        {
            sample = null;
            string[] fields = (details ?? string.Empty).Split('|');
            if (fields.Length != 7 || fields[0] != "1")
                return false;

            try
            {
                string sessionId = Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
                long worldTick = long.Parse(fields[2], CultureInfo.InvariantCulture);
                float deltaSeconds = float.Parse(fields[3], CultureInfo.InvariantCulture);
                int tickRate = int.Parse(fields[4], CultureInfo.InvariantCulture);
                long sampleTicks = long.Parse(fields[5], CultureInfo.InvariantCulture);

                sample = Normalize(new ShelteredWorldClockSample
                {
                    SessionId = sessionId,
                    WorldTick = worldTick,
                    DeltaSeconds = deltaSeconds,
                    TickRate = tickRate,
                    SampleUtc = sampleTicks > 0 ? new DateTime(sampleTicks, DateTimeKind.Utc) : DateTime.MinValue,
                    HostAuthoritative = fields[6] == "1"
                });
                return true;
            }
            catch
            {
                sample = null;
                return false;
            }
        }

        private static ShelteredWorldClockSample Normalize(ShelteredWorldClockSample sample)
        {
            ShelteredWorldClockSample normalized = sample != null
                ? sample.Copy()
                : new ShelteredWorldClockSample();

            normalized.SessionId = normalized.SessionId ?? string.Empty;
            if (normalized.WorldTick < 0)
                normalized.WorldTick = 0;
            if (normalized.DeltaSeconds < 0f)
                normalized.DeltaSeconds = 0f;
            if (normalized.TickRate <= 0)
                normalized.TickRate = ShelteredMultiplayerWorldClock.DefaultTickRate;
            if (normalized.SampleUtc == DateTime.MinValue)
                normalized.SampleUtc = ShelteredWorldClockDiagnosticTime.UtcNow();

            return normalized;
        }

        private static uint ToUInt32Tick(long tick)
        {
            if (tick <= 0)
                return 0;
            if (tick > uint.MaxValue)
                return uint.MaxValue;

            return (uint)tick;
        }
    }
}
