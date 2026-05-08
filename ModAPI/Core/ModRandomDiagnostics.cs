using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ModAPI.Core
{
    public enum RngTraceMode
    {
        Off = 0,
        ErrorsOnly = 1,
        Sampled = 2,
        Full = 3
    }

    public static class RngDebugOptions
    {
        public static bool Enabled = false;
        public static RngTraceMode TraceMode = RngTraceMode.Off;
        public static int SampleRate = 100;
        public static int BufferSize = 2048;
        public static bool IncludeCallsite = false;
        public static bool MultiplayerStrict = false;
        public static Func<long> WorldTickProvider;

        public static void ResetDefaults()
        {
            Enabled = false;
            TraceMode = RngTraceMode.Off;
            SampleRate = 100;
            BufferSize = 2048;
            IncludeCallsite = false;
            MultiplayerStrict = false;
            WorldTickProvider = null;
        }
    }

    public sealed class RngTraceEvent
    {
        public long UtcTicks;
        public long WorldTick;
        public string StreamName;
        public string Operation;
        public ulong StepBefore;
        public ulong StepAfter;
        public uint ValueHash;
        public int ThreadId;
        public int Flags;

        internal RngTraceEvent Copy()
        {
            return new RngTraceEvent
            {
                UtcTicks = UtcTicks,
                WorldTick = WorldTick,
                StreamName = StreamName ?? string.Empty,
                Operation = Operation ?? string.Empty,
                StepBefore = StepBefore,
                StepAfter = StepAfter,
                ValueHash = ValueHash,
                ThreadId = ThreadId,
                Flags = Flags
            };
        }
    }

    internal sealed class RngTraceBuffer
    {
        private readonly object _sync = new object();
        private RngTraceEvent[] _events;
        private int _next;
        private int _count;

        public RngTraceBuffer(int capacity)
        {
            Reset(capacity);
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _count;
                }
            }
        }

        public void Reset(int capacity)
        {
            if (capacity <= 0)
                capacity = 1;

            lock (_sync)
            {
                _events = new RngTraceEvent[capacity];
                _next = 0;
                _count = 0;
            }
        }

        public void Add(RngTraceEvent traceEvent)
        {
            if (traceEvent == null)
                return;

            lock (_sync)
            {
                if (_events == null || _events.Length != NormalizeCapacity(RngDebugOptions.BufferSize))
                    Reset(NormalizeCapacity(RngDebugOptions.BufferSize));

                _events[_next] = traceEvent.Copy();
                _next = (_next + 1) % _events.Length;
                if (_count < _events.Length)
                    _count++;
            }
        }

        public RngTraceEvent[] Snapshot()
        {
            lock (_sync)
            {
                RngTraceEvent[] snapshot = new RngTraceEvent[_count];
                if (_count == 0)
                    return snapshot;

                int start = _count == _events.Length ? _next : 0;
                for (int i = 0; i < _count; i++)
                {
                    RngTraceEvent traceEvent = _events[(start + i) % _events.Length];
                    snapshot[i] = traceEvent != null ? traceEvent.Copy() : null;
                }

                return snapshot;
            }
        }

        private static int NormalizeCapacity(int capacity)
        {
            return capacity > 0 ? capacity : 1;
        }
    }

    public sealed class DeterminismDigest
    {
        public int MasterSeed;
        public RandomnessMode Mode;
        public ulong MasterStep;
        public uint MasterHash;
        public int StreamCount;
        public uint StreamHash;
        public uint CombinedHash;

        public override string ToString()
        {
            return "seed=" + MasterSeed
                + ", mode=" + Mode
                + ", masterStep=" + MasterStep
                + ", masterHash=" + MasterHash.ToString("X8")
                + ", streamCount=" + StreamCount
                + ", streamHash=" + StreamHash.ToString("X8")
                + ", combined=" + CombinedHash.ToString("X8");
        }
    }

    internal static class ModRandomDiagnostics
    {
        internal const int TraceFlagSampled = 1;
        internal const int TraceFlagStrictWarning = 2;

        private static readonly RngTraceBuffer Buffer = new RngTraceBuffer(RngDebugOptions.BufferSize);
        [ThreadStatic]
        private static int _multiplayerSensitiveDepth;

        internal static bool IsMultiplayerSensitive
        {
            get { return _multiplayerSensitiveDepth > 0; }
        }

        public static IDisposable EnterMultiplayerSensitiveContext(string reason)
        {
            _multiplayerSensitiveDepth++;
            return new MultiplayerSensitiveScope();
        }

        internal static RngTraceEvent[] SnapshotTrace()
        {
            return Buffer.Snapshot();
        }

        internal static void Trace(string streamName, string operation, ulong stepBefore, ulong stepAfter, ulong value)
        {
            bool strict = RngDebugOptions.MultiplayerStrict;
            bool enabled = RngDebugOptions.Enabled && RngDebugOptions.TraceMode != RngTraceMode.Off;
            bool defaultStream = IsDefaultStream(streamName);

            if (strict && defaultStream && IsMultiplayerSensitive)
            {
                try
                {
                    MMLog.WarnOnce(
                        "ModRandom.MultiplayerStrict.DefaultStream",
                        "ModRandom default stream was used inside a multiplayer-sensitive context. Use a named MultiplayerSync stream.");
                }
                catch
                {
                }
            }

            if (!enabled)
                return;

            int flags = 0;
            if (RngDebugOptions.TraceMode == RngTraceMode.Sampled)
            {
                int sampleRate = RngDebugOptions.SampleRate > 0 ? RngDebugOptions.SampleRate : 100;
                if ((stepAfter % (ulong)sampleRate) != 0)
                    return;
                flags |= TraceFlagSampled;
            }

            if (strict && defaultStream && IsMultiplayerSensitive)
                flags |= TraceFlagStrictWarning;

            Buffer.Add(new RngTraceEvent
            {
                UtcTicks = DateTime.UtcNow.Ticks,
                WorldTick = ResolveWorldTick(),
                StreamName = streamName ?? string.Empty,
                Operation = operation ?? string.Empty,
                StepBefore = stepBefore,
                StepAfter = stepAfter,
                ValueHash = HashValue(value),
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                Flags = flags
            });
        }

        internal static string Dump(string reason, DeterminismDigest digest)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"version\": 1,");
            builder.AppendLine("  \"reason\": \"" + Escape(reason) + "\",");
            builder.AppendLine("  \"utc\": \"" + DateTime.UtcNow.ToString("o") + "\",");
            builder.AppendLine("  \"digest\": \"" + Escape(digest != null ? digest.ToString() : string.Empty) + "\",");
            builder.AppendLine("  \"events\": [");

            RngTraceEvent[] events = SnapshotTrace();
            for (int i = 0; i < events.Length; i++)
            {
                RngTraceEvent e = events[i];
                if (e == null)
                    continue;

                builder.Append("    {\"tick\":").Append(e.WorldTick)
                    .Append(",\"stream\":\"").Append(Escape(e.StreamName))
                    .Append("\",\"op\":\"").Append(Escape(e.Operation))
                    .Append("\",\"before\":").Append(e.StepBefore)
                    .Append(",\"after\":").Append(e.StepAfter)
                    .Append(",\"hash\":\"").Append(e.ValueHash.ToString("X8"))
                    .Append("\",\"flags\":").Append(e.Flags).Append("}");
                if (i + 1 < events.Length)
                    builder.Append(",");
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        internal static void WriteDump(string reason, string path, DeterminismDigest digest)
        {
            string content = Dump(reason, digest);
            if (string.IsNullOrEmpty(path))
            {
                try
                {
                    MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.General, "ModRandom", content);
                }
                catch
                {
                }

                return;
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
        }

        private static long ResolveWorldTick()
        {
            try
            {
                return RngDebugOptions.WorldTickProvider != null ? RngDebugOptions.WorldTickProvider() : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool IsDefaultStream(string streamName)
        {
            return string.IsNullOrEmpty(streamName)
                || string.Equals(streamName, "default", StringComparison.OrdinalIgnoreCase);
        }

        private static uint HashValue(ulong value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)value) * 16777619u;
                hash = (hash ^ (uint)(value >> 32)) * 16777619u;
                return hash;
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class MultiplayerSensitiveScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                    return;

                if (_multiplayerSensitiveDepth > 0)
                    _multiplayerSensitiveDepth--;
                _disposed = true;
            }
        }
    }
}
