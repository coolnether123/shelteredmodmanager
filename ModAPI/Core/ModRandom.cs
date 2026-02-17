using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Core
{
    /// <summary>
    /// Randomness mode for the generator.
    /// </summary>
    public enum RandomnessMode
    {
        /// <summary>
        /// Fast, deterministic XorShift64* algorithm (default).
        /// Recommended for gameplay and synced multiplayer logic.
        /// </summary>
        XorShift,

        /// <summary>
        /// Legacy System.Random. Not recommended for cross-platform determinism.
        /// </summary>
        Legacy
    }

    /// <summary>
    /// Deterministic random number generator using XorShift64*.
    /// Guaranteed identical results across Steam (x86) and Epic (x64) builds.
    /// </summary>
    public static class ModRandom
    {
        private static ulong _state;
        private static int _seed;
        private static ulong _stepCount;
        private static bool _initialized;
        private static RandomnessMode _mode = RandomnessMode.XorShift;
        private static Random _legacyRandom;
        private static readonly Dictionary<string, ModRandomStream> _namedStreams = new Dictionary<string, ModRandomStream>(StringComparer.Ordinal);
        
        // Thread-local storage for coroutine safety
        private static readonly object _lock = new object();

        /// <summary>
        /// Fired when the master seed changes (e.g. on save load).
        /// </summary>
        public static event Action OnSeedChanged;

        /// <summary>
        /// When true, the RNG will restore its previous seed and step count on load.
        /// When false (default), a new random seed is generated every time a save is loaded.
        /// </summary>
        public static bool IsDeterministic { get; set; }

        internal static void NotifySeedChanged()
        {
            if (OnSeedChanged != null)
            {
                try { OnSeedChanged(); } catch { }
            }
        }

        /// <summary>
        /// Initialize the master seed. Call this once on game start/session load.
        /// </summary>
        public static void Initialize(int seed, RandomnessMode mode = RandomnessMode.XorShift)
        {
            lock (_lock)
            {
                _seed = seed;
                _mode = mode;
                _state = (ulong)(seed == 0 ? 1234567 : seed);
                _stepCount = 0;
                _initialized = true;
                _namedStreams.Clear();

                if (mode == RandomnessMode.Legacy)
                {
                    _legacyRandom = new Random(seed);
                }
                else
                {
                    _legacyRandom = null;
                }
                
                MMLog.WriteDebug(string.Format("[ModRandom] Initialized with seed {0} in mode {1}", seed, mode));
            }
        }

        /// <summary>
        /// Get the current seed.
        /// </summary>
        public static int CurrentSeed { get { return _seed; } }

        /// <summary>
        /// How many numbers have been generated since initialization.
        /// </summary>
        public static ulong CurrentStep { get { return _stepCount; } }

        /// <summary>
        /// Check if random system is ready.
        /// </summary>
        public static bool IsInitialized { get { return _initialized; } }

        /// <summary>
        /// Fast-forward the RNG state by generating N numbers.
        /// Used for restoring state from a save.
        /// </summary>
        public static void FastForward(ulong steps)
        {
            lock (_lock)
            {
                if (!_initialized) Initialize(12345);
                
                if (_mode == RandomnessMode.XorShift)
                {
                    for (ulong i = 0; i < steps; i++)
                    {
                        NextULongInternal();
                    }
                }
                else
                {
                    for (ulong i = 0; i < steps; i++)
                    {
                        _legacyRandom.NextDouble();
                    }
                    _stepCount += steps;
                }
                
                MMLog.WriteDebug(string.Format("[ModRandom] Fast-forwarded {0} steps. Current Step: {1}", steps, _stepCount));
            }
        }

        // --- Core Generation Methods ---

        /// <summary>
        /// Integer range [minInclusive, maxExclusive).
        /// Matches Unity's Random.Range behavior for ints.
        /// </summary>
        public static int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            lock (_lock)
            {
                return minInclusive + (int)(NextULongInternal() % (ulong)(maxExclusive - minInclusive));
            }
        }

        /// <summary>
        /// Float range [minInclusive, maxInclusive].
        /// </summary>
        public static float Range(float min, float max)
        {
            if (max <= min) return min;
            return min + (float)(ValueDoubleInclusive() * (double)(max - min));
        }

        /// <summary>
        /// Bias-free integer range for large ranges (> 10,000).
        /// Uses rejection sampling to eliminate modulo bias.
        /// </summary>
        public static int RangeUnbiased(int min, int max)
        {
            uint range = (uint)(max - min);
            if (range == 0) return min;

            uint limit = uint.MaxValue - ((uint.MaxValue % range) + 1) % range;
            uint result;

            lock (_lock)
            {
                do { result = (uint)NextULongInternal(); } while (result > limit);
            }
            
            return min + (int)(result % range);
        }

        /// <summary>
        /// Float between 0.0 and 1.0 (inclusive).
        /// </summary>
        public static float Value()
        {
            return (float)ValueDouble();
        }

        /// <summary>
        /// Double between 0.0 and 1.0 (exclusive upper bound for XorShift mode).
        /// </summary>
        public static double ValueDouble()
        {
            lock (_lock)
            {
                if (_mode == RandomnessMode.Legacy)
                {
                    _stepCount++;
                    return _legacyRandom.NextDouble();
                }

                // 53 bits of entropy mapped into [0,1) using double precision.
                return (double)(NextULongInternal() >> 11) * (1.0 / 9007199254740992.0);
            }
        }

        /// <summary>
        /// True/False with specified probability.
        /// </summary>
        public static bool Bool(float probability)
        {
            return Value() < probability;
        }

        /// <summary>
        /// True/False with 50% probability.
        /// </summary>
        public static bool Bool()
        {
            return Value() < 0.5f;
        }

        /// <summary>
        /// Random selection from array.
        /// </summary>
        public static T Choose<T>(params T[] items)
        {
            if (items == null || items.Length == 0) return default(T);
            return items[Range(0, items.Length)];
        }

        /// <summary>
        /// Shuffle a list in-place (Fisher-Yates).
        /// </summary>
        public static void Shuffle<T>(IList<T> list)
        {
            if (list == null) return;
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        /// <summary>
        /// Gaussian/normal distribution (bell curve).
        /// </summary>
        public static float Gaussian(float mean, float stdDev)
        {
            // Box-Muller transform
            float u1 = 1.0f - Value(); // avoid 0
            float u2 = Value();
            float normal = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
            return mean + stdDev * normal;
        }

        /// <summary>
        /// Weighted random selection.
        /// </summary>
        public static T Weighted<T>(T[] items, float[] weights)
        {
            if (items == null || weights == null || items.Length == 0 || items.Length != weights.Length)
                return default(T);

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            
            float random = (float)(ValueDouble() * total);
            float current = 0f;
            
            for (int i = 0; i < weights.Length; i++)
            {
                current += weights[i];
                if (random <= current) return items[i];
            }
            return items[items.Length - 1];
        }

        /// <summary>
        /// Get a deterministic named stream isolated from the global stream.
        /// </summary>
        public static ModRandomStream GetStream(string streamName)
        {
            if (string.IsNullOrEmpty(streamName))
            {
                streamName = "default";
            }

            lock (_lock)
            {
                ModRandomStream stream;
                if (_namedStreams.TryGetValue(streamName, out stream))
                {
                    return stream;
                }

                int streamSeed = DeriveStreamSeed(_seed, streamName);
                stream = new ModRandomStream(streamSeed);
                _namedStreams[streamName] = stream;
                return stream;
            }
        }

        internal static ModRandomStateSnapshot CreateSnapshot()
        {
            lock (_lock)
            {
                ModRandomStateSnapshot snapshot = new ModRandomStateSnapshot();
                snapshot.MasterSeed = _seed;
                snapshot.MasterState = _state;
                snapshot.StepCount = _stepCount;
                snapshot.Mode = _mode;

                int count = _namedStreams.Count;
                snapshot.StreamNames = new string[count];
                snapshot.StreamStates = new ulong[count];
                snapshot.StreamSteps = new ulong[count];

                int i = 0;
                foreach (KeyValuePair<string, ModRandomStream> kvp in _namedStreams)
                {
                    snapshot.StreamNames[i] = kvp.Key;
                    snapshot.StreamStates[i] = kvp.Value.InternalState;
                    snapshot.StreamSteps[i] = kvp.Value.CurrentStep;
                    i++;
                }

                return snapshot;
            }
        }

        internal static void RestoreSnapshot(ModRandomStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Initialize(12345);
                return;
            }

            lock (_lock)
            {
                _seed = snapshot.MasterSeed;
                _mode = snapshot.Mode;
                _stepCount = snapshot.StepCount;
                _initialized = true;
                _namedStreams.Clear();

                if (_mode == RandomnessMode.Legacy)
                {
                    _legacyRandom = new Random(_seed);
                    if (_stepCount > 0)
                    {
                        for (ulong i = 0; i < _stepCount; i++)
                        {
                            _legacyRandom.NextDouble();
                        }
                    }

                    // Legacy has no direct internal-state restoration API.
                    _state = (ulong)(_seed == 0 ? 1234567 : _seed);
                }
                else
                {
                    _legacyRandom = null;
                    _state = snapshot.MasterState == 0 ? 1234567ul : snapshot.MasterState;
                }

                if (snapshot.StreamNames != null && snapshot.StreamStates != null && snapshot.StreamSteps != null)
                {
                    int count = snapshot.StreamNames.Length;
                    if (snapshot.StreamStates.Length < count) count = snapshot.StreamStates.Length;
                    if (snapshot.StreamSteps.Length < count) count = snapshot.StreamSteps.Length;

                    for (int i = 0; i < count; i++)
                    {
                        string name = snapshot.StreamNames[i];
                        if (string.IsNullOrEmpty(name))
                        {
                            continue;
                        }

                        ModRandomStream stream = new ModRandomStream(1);
                        stream.SetInternalState(snapshot.StreamStates[i], snapshot.StreamSteps[i]);
                        _namedStreams[name] = stream;
                    }
                }
            }
        }

        private static int DeriveStreamSeed(int masterSeed, string streamName)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < streamName.Length; i++)
                {
                    hash ^= streamName[i];
                    hash *= 16777619u;
                }

                hash ^= (uint)masterSeed;
                hash *= 16777619u;

                int seed = (int)hash;
                return seed == 0 ? 1 : seed;
            }
        }

        private static double ValueDoubleInclusive()
        {
            lock (_lock)
            {
                if (_mode == RandomnessMode.Legacy)
                {
                    _stepCount++;
                    return _legacyRandom.NextDouble();
                }

                // Inclusive 1.0 endpoint for Unity-like float range semantics.
                return (double)NextULongInternal() / 18446744073709551615.0;
            }
        }

        // --- Internal XorShift64* Algorithm ---
        private static ulong NextULong()
        {
            lock (_lock)
            {
                return NextULongInternal();
            }
        }

        private static ulong NextULongInternal()
        {
            if (!_initialized) Initialize(12345);

            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            _stepCount++;
            return _state * 2685821657736338717ul;
        }
    }

    internal class ModRandomStateSnapshot
    {
        public int MasterSeed;
        public ulong MasterState;
        public ulong StepCount;
        public RandomnessMode Mode;
        public string[] StreamNames;
        public ulong[] StreamStates;
        public ulong[] StreamSteps;
    }

    /// <summary>
    /// Instance-based random stream for mod isolation.
    /// Prevents "RNG steal" between mods.
    /// </summary>
    public class ModRandomStream
    {
        private ulong _state;
        private ulong _stepCount;

        public ModRandomStream(int seed)
        {
            _state = (ulong)(seed == 0 ? 1234567 : seed);
            _stepCount = 0;
        }

        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextULong() % (ulong)(max - min));
        }

        public float Range(float min, float max)
        {
            return min + Value() * (max - min);
        }

        public float Value()
        {
            return (float)((double)(NextULong() >> 11) * (1.0 / 9007199254740992.0));
        }

        public bool Bool(float probability = 0.5f)
        {
            return Value() < probability;
        }

        public T Choose<T>(params T[] items)
        {
            if (items == null || items.Length == 0) return default(T);
            return items[Range(0, items.Length)];
        }

        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) return;
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            _stepCount++;
            return _state * 2685821657736338717ul;
        }

        public ulong CurrentStep { get { return _stepCount; } }
        internal ulong InternalState { get { return _state; } }

        internal void SetInternalState(ulong state, ulong stepCount)
        {
            _state = state == 0 ? 1ul : state;
            _stepCount = stepCount;
        }
    }
}
