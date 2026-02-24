using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ModAPI.Events
{
    /// <summary>
    /// Trigger cadence for time-based mod workloads.
    /// </summary>
    public enum TimeTriggerCadence
    {
        SixHour = 1,
        Staggered = 2,
        Both = 3
    }

    /// <summary>
    /// Runtime tick type emitted by <see cref="GameTimeTriggerHelper"/>.
    /// </summary>
    public enum TimeTriggerKind
    {
        SixHour = 1,
        Staggered = 2
    }

    /// <summary>
    /// Immutable trigger descriptor passed to mods in priority batches.
    /// </summary>
    public sealed class TimeTriggerInfo
    {
        public string Id { get; private set; }
        public int Priority { get; private set; }
        public TimeTriggerCadence Cadence { get; private set; }

        internal TimeTriggerInfo(string id, int priority, TimeTriggerCadence cadence)
        {
            Id = id;
            Priority = priority;
            Cadence = cadence;
        }
    }

    /// <summary>
    /// Batch payload dispatched when a timed trigger fires.
    /// </summary>
    public sealed class TimeTriggerBatch
    {
        public TimeTriggerKind Kind { get; private set; }
        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Minute { get; private set; }
        public int TotalHours { get; private set; }
        public int TotalMinutes { get; private set; }
        public int Sequence { get; private set; }
        public int IntervalHours { get; private set; }
        public List<TimeTriggerInfo> PriorityList { get; private set; }

        internal TimeTriggerBatch(
            TimeTriggerKind kind,
            int day,
            int hour,
            int minute,
            int totalHours,
            int totalMinutes,
            int sequence,
            int intervalHours,
            List<TimeTriggerInfo> priorityList)
        {
            Kind = kind;
            Day = day;
            Hour = hour;
            Minute = minute;
            TotalHours = totalHours;
            TotalMinutes = totalMinutes;
            Sequence = sequence;
            IntervalHours = intervalHours;
            PriorityList = priorityList ?? new List<TimeTriggerInfo>();
        }
    }

    /// <summary>
    /// Deterministic in-game scheduler with fixed 6-hour and randomized staggered (default 4-6 hour) ticks.
    /// Six-hour ticks align to day quartiles (06:00, 12:00, 18:00, 24:00/new day).
    /// Mods can register named triggers with priority and receive ordered trigger batches.
    /// </summary>
    public static class GameTimeTriggerHelper
    {
        public static event Action<TimeTriggerBatch> OnSixHourTick;
        public static event Action<TimeTriggerBatch> OnStaggeredTick;

        private const int SixHourIntervalMinutes = 6 * 60;
        private const int SixHourPhaseOffsetMinutes = 0;
        private const int MaxCatchupMinutes = 72 * 60;
        private const string RandomStreamName = "ShelteredAPI.GameTimeTriggerHelper";

        private static readonly object Sync = new object();
        private static readonly object WarnOnceSync = new object();
        private static readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LocalWarnOnceKeys = new HashSet<string>(StringComparer.Ordinal);

        private static bool _initialized;
        private static bool _lifecycleSubscribed;
        private static bool _forceResync = true;

        private static int _staggeredMinHours = 4;
        private static int _staggeredMaxHours = 6;

        private static int _lastObservedTotalMinute = int.MinValue;
        private static int _nextSixHourTickMinute = -1;
        private static int _nextStaggeredTickMinute = -1;
        private static int _lastStaggeredTickMinute = 0;
        private static int _sixHourSequence = 0;
        private static int _staggeredSequence = 0;

        private static long _registrationOrderCounter = 0;

        // Reflection bridge to ModAPI.Core.ModRandom (ShelteredAPI intentionally has no compile-time dependency on ModAPI).
        private static Type _modRandomType;
        private static MethodInfo _modRandomGetStream;
        private static EventInfo _modRandomSeedChangedEvent;
        private static Delegate _modRandomSeedChangedDelegate;
        private static object _modRandomStream;
        private static MethodInfo _modRandomStreamRangeIntMethod;

        // Deterministic fallback PRNG if ModRandom bridge is unavailable.
        private static uint _fallbackState = 2463534242u;

        /// <summary>
        /// Current minimum staggered interval in in-game hours (inclusive).
        /// </summary>
        public static int StaggeredMinHours { get { return _staggeredMinHours; } }

        /// <summary>
        /// Current maximum staggered interval in in-game hours (inclusive).
        /// </summary>
        public static int StaggeredMaxHours { get { return _staggeredMaxHours; } }

        /// <summary>
        /// Registers or updates a trigger with default priority 100 and cadence <see cref="TimeTriggerCadence.Both"/>.
        /// </summary>
        public static void RegisterTrigger(string triggerId)
        {
            RegisterTrigger(triggerId, 100, TimeTriggerCadence.Both, null);
        }

        /// <summary>
        /// Registers or updates a trigger with cadence <see cref="TimeTriggerCadence.Both"/>.
        /// </summary>
        public static void RegisterTrigger(string triggerId, int priority)
        {
            RegisterTrigger(triggerId, priority, TimeTriggerCadence.Both, null);
        }

        /// <summary>
        /// Registers or updates a trigger.
        /// </summary>
        public static void RegisterTrigger(string triggerId, int priority, TimeTriggerCadence cadence)
        {
            RegisterTrigger(triggerId, priority, cadence, null);
        }

        /// <summary>
        /// Registers or updates a trigger with an optional callback invoked in priority order at dispatch time.
        /// </summary>
        public static void RegisterTrigger(string triggerId, int priority, TimeTriggerCadence cadence, Action<TimeTriggerBatch> callback)
        {
            if (string.IsNullOrEmpty(triggerId))
                return;

            EnsureInitialized();

            lock (Sync)
            {
                Registration existing;
                if (Registrations.TryGetValue(triggerId, out existing))
                {
                    existing.Priority = priority;
                    existing.Cadence = NormalizeCadence(cadence);
                    existing.Callback = callback;
                    Registrations[triggerId] = existing;
                }
                else
                {
                    _registrationOrderCounter++;
                    Registrations[triggerId] = new Registration
                    {
                        Id = triggerId,
                        Priority = priority,
                        Cadence = NormalizeCadence(cadence),
                        Callback = callback,
                        Order = _registrationOrderCounter
                    };
                }
            }
        }

        /// <summary>
        /// Removes a registered trigger.
        /// </summary>
        public static bool UnregisterTrigger(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId))
                return false;

            lock (Sync)
            {
                return Registrations.Remove(triggerId);
            }
        }

        /// <summary>
        /// Returns an ordered snapshot of registered triggers for the requested cadence.
        /// </summary>
        public static List<TimeTriggerInfo> GetPriorityList(TimeTriggerCadence cadence)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return BuildPrioritySnapshotNoLock(NormalizeCadence(cadence));
            }
        }

        /// <summary>
        /// Sets the deterministic staggered interval range in in-game hours (both inclusive).
        /// Example: min=4, max=6 produces 4h/5h/6h spacing.
        /// </summary>
        public static void ConfigureStaggeredRange(int minInclusive, int maxInclusive)
        {
            if (minInclusive < 1 || maxInclusive < minInclusive)
            {
                WarnOnce("GameTimeTriggerHelper.ConfigureStaggeredRange", "Invalid staggered range. Values must be >= 1 and max >= min.");
                return;
            }

            EnsureInitialized();

            lock (Sync)
            {
                _staggeredMinHours = minInclusive;
                _staggeredMaxHours = maxInclusive;
                _forceResync = true;
            }
        }

        private static TimeTriggerCadence NormalizeCadence(TimeTriggerCadence cadence)
        {
            if (cadence != TimeTriggerCadence.SixHour &&
                cadence != TimeTriggerCadence.Staggered &&
                cadence != TimeTriggerCadence.Both)
            {
                return TimeTriggerCadence.Both;
            }
            return cadence;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (Sync)
            {
                if (_initialized)
                    return;

                SubscribeLifecycleEventsNoLock();
                HookModRandomSeedChangesNoLock();

                int currentTotalMinute;
                if (TryGetCurrentTotalMinute(out currentTotalMinute))
                {
                    ResyncNoLock(currentTotalMinute, true);
                    _forceResync = false;
                }

                _initialized = true;
            }
        }

        private static void SubscribeLifecycleEventsNoLock()
        {
            if (_lifecycleSubscribed)
                return;

            try
            {
                GameEvents.OnSessionStarted += HandleSessionBoundary;
                GameEvents.OnNewGame += HandleSessionBoundary;
                GameEvents.OnAfterLoad += HandleAfterLoad;
                _lifecycleSubscribed = true;
            }
            catch (Exception ex)
            {
                WarnOnce("GameTimeTriggerHelper.SubscribeLifecycle", "Failed to subscribe lifecycle events: " + ex.Message);
            }
        }

        private static void HandleSessionBoundary()
        {
            lock (Sync)
            {
                _forceResync = true;
            }
        }

        private static void HandleAfterLoad(SaveData _)
        {
            lock (Sync)
            {
                _forceResync = true;
            }
        }

        private static void HookModRandomSeedChangesNoLock()
        {
            if (_modRandomSeedChangedDelegate != null)
                return;

            try
            {
                EnsureModRandomBridgeNoLock();
                if (_modRandomType == null)
                    return;

                _modRandomSeedChangedEvent = _modRandomType.GetEvent("OnSeedChanged", BindingFlags.Public | BindingFlags.Static);
                if (_modRandomSeedChangedEvent == null)
                    return;

                var method = typeof(GameTimeTriggerHelper).GetMethod("HandleModRandomSeedChanged", BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                    return;

                _modRandomSeedChangedDelegate = Delegate.CreateDelegate(_modRandomSeedChangedEvent.EventHandlerType, method);
                _modRandomSeedChangedEvent.AddEventHandler(null, _modRandomSeedChangedDelegate);
            }
            catch (Exception ex)
            {
                WarnOnce("GameTimeTriggerHelper.ModRandom.Hook", "Failed to hook ModRandom.OnSeedChanged: " + ex.Message);
            }
        }

        private static void HandleModRandomSeedChanged()
        {
            lock (Sync)
            {
                _modRandomStream = null;
                _modRandomStreamRangeIntMethod = null;
                _forceResync = true;
            }
        }

        internal static void TickFromGameTime()
        {
            // Skip scheduler work entirely when no mod is listening for timed work.
            if (!HasTimedConsumers())
                return;

            EnsureInitialized();

            if (IsGameLoading())
                return;

            int currentTotalMinute;
            if (!TryGetCurrentTotalMinute(out currentTotalMinute))
                return;

            List<DispatchJob> jobs = null;

            lock (Sync)
            {
                if (_forceResync || _lastObservedTotalMinute == int.MinValue || currentTotalMinute < _lastObservedTotalMinute)
                {
                    ResyncNoLock(currentTotalMinute, true);
                    _forceResync = false;
                    return;
                }

                if (currentTotalMinute == _lastObservedTotalMinute)
                    return;

                int delta = currentTotalMinute - _lastObservedTotalMinute;
                if (delta > MaxCatchupMinutes)
                {
                    ResyncNoLock(currentTotalMinute, false);
                    _forceResync = false;
                    WarnOnce("GameTimeTriggerHelper.Catchup", "Large time jump detected; scheduler resynced without replay.");
                    return;
                }

                jobs = new List<DispatchJob>();
                for (int minute = _lastObservedTotalMinute + 1; minute <= currentTotalMinute; minute++)
                {
                    CollectDispatchJobsForMinuteNoLock(minute, jobs);
                }

                _lastObservedTotalMinute = currentTotalMinute;
                _forceResync = false;
            }

            if (jobs == null || jobs.Count == 0)
                return;

            for (int i = 0; i < jobs.Count; i++)
            {
                ExecuteDispatchJob(jobs[i]);
            }
        }

        private static bool HasTimedConsumers()
        {
            lock (Sync)
            {
                if (OnSixHourTick != null || OnStaggeredTick != null)
                    return true;

                if (Registrations.Count == 0)
                    return false;

                foreach (var kv in Registrations)
                {
                    if (kv.Value.Callback != null)
                        return true;
                }

                return false;
            }
        }

        private static void ResyncNoLock(int currentTotalMinute, bool resetSequences)
        {
            if (currentTotalMinute < 0)
                currentTotalMinute = 0;

            _lastObservedTotalMinute = currentTotalMinute;
            _nextSixHourTickMinute = NextFixedTickAfter(currentTotalMinute, SixHourIntervalMinutes, SixHourPhaseOffsetMinutes);
            _lastStaggeredTickMinute = currentTotalMinute;
            _nextStaggeredTickMinute = currentTotalMinute + (NextStaggeredIntervalNoLock() * 60);

            if (resetSequences)
            {
                _sixHourSequence = 0;
                _staggeredSequence = 0;
            }
        }

        private static void CollectDispatchJobsForMinuteNoLock(int totalMinute, List<DispatchJob> jobs)
        {
            while (totalMinute >= _nextSixHourTickMinute)
            {
                _sixHourSequence++;
                jobs.Add(CreateDispatchJobNoLock(
                    TimeTriggerKind.SixHour,
                    _nextSixHourTickMinute,
                    _sixHourSequence,
                    SixHourIntervalMinutes));
                _nextSixHourTickMinute += SixHourIntervalMinutes;
            }

            while (totalMinute >= _nextStaggeredTickMinute)
            {
                int firedAt = _nextStaggeredTickMinute;
                int intervalUsed = firedAt - _lastStaggeredTickMinute;
                if (intervalUsed <= 0)
                    intervalUsed = _staggeredMinHours * 60;

                _staggeredSequence++;
                jobs.Add(CreateDispatchJobNoLock(
                    TimeTriggerKind.Staggered,
                    firedAt,
                    _staggeredSequence,
                    intervalUsed));

                _lastStaggeredTickMinute = firedAt;
                _nextStaggeredTickMinute = firedAt + (NextStaggeredIntervalNoLock() * 60);
            }
        }

        private static DispatchJob CreateDispatchJobNoLock(TimeTriggerKind kind, int totalMinutes, int sequence, int intervalMinutes)
        {
            var ordered = GetOrderedRegistrationsNoLock(kind);
            return new DispatchJob
            {
                Kind = kind,
                TotalMinutes = totalMinutes,
                Sequence = sequence,
                IntervalMinutes = intervalMinutes,
                OrderedRegistrations = ordered
            };
        }

        private static void ExecuteDispatchJob(DispatchJob job)
        {
            int day = (job.TotalMinutes / 1440) + 1;
            int hour = (job.TotalMinutes / 60) % 24;
            int minute = job.TotalMinutes % 60;
            if (hour < 0) hour = 0;
            if (minute < 0) minute = 0;
            int totalHours = job.TotalMinutes / 60;
            int intervalHours = Math.Max(1, job.IntervalMinutes / 60);

            List<TimeTriggerInfo> priorityList = new List<TimeTriggerInfo>(job.OrderedRegistrations.Count);
            for (int i = 0; i < job.OrderedRegistrations.Count; i++)
            {
                var reg = job.OrderedRegistrations[i];
                priorityList.Add(new TimeTriggerInfo(reg.Id, reg.Priority, reg.Cadence));
            }

            var batch = new TimeTriggerBatch(
                job.Kind,
                day,
                hour,
                minute,
                totalHours,
                job.TotalMinutes,
                job.Sequence,
                intervalHours,
                priorityList);

            if (job.Kind == TimeTriggerKind.SixHour)
            {
                var evt = OnSixHourTick;
                if (evt != null)
                {
                    try { evt(batch); }
                    catch (Exception ex) { WarnOnce("GameTimeTriggerHelper.OnSixHourTick", "OnSixHourTick subscriber threw: " + ex.Message); }
                }
            }
            else
            {
                var evt = OnStaggeredTick;
                if (evt != null)
                {
                    try { evt(batch); }
                    catch (Exception ex) { WarnOnce("GameTimeTriggerHelper.OnStaggeredTick", "OnStaggeredTick subscriber threw: " + ex.Message); }
                }
            }

            for (int i = 0; i < job.OrderedRegistrations.Count; i++)
            {
                var callback = job.OrderedRegistrations[i].Callback;
                if (callback == null)
                    continue;

                try { callback(batch); }
                catch (Exception ex)
                {
                    WarnOnce("GameTimeTriggerHelper.Trigger." + job.OrderedRegistrations[i].Id, "Trigger callback threw for '" + job.OrderedRegistrations[i].Id + "': " + ex.Message);
                }
            }
        }

        private static List<Registration> GetOrderedRegistrationsNoLock(TimeTriggerKind kind)
        {
            var list = new List<Registration>();
            foreach (var kv in Registrations)
            {
                var reg = kv.Value;
                if (!MatchesKind(reg.Cadence, kind))
                    continue;
                list.Add(reg);
            }

            list.Sort(RegistrationComparer.Instance);
            return list;
        }

        private static List<TimeTriggerInfo> BuildPrioritySnapshotNoLock(TimeTriggerCadence cadence)
        {
            TimeTriggerKind kind = cadence == TimeTriggerCadence.Staggered ? TimeTriggerKind.Staggered : TimeTriggerKind.SixHour;
            if (cadence == TimeTriggerCadence.Both)
            {
                var combined = new List<Registration>();
                foreach (var kv in Registrations)
                {
                    combined.Add(kv.Value);
                }
                combined.Sort(RegistrationComparer.Instance);

                var all = new List<TimeTriggerInfo>(combined.Count);
                for (int i = 0; i < combined.Count; i++)
                {
                    all.Add(new TimeTriggerInfo(combined[i].Id, combined[i].Priority, combined[i].Cadence));
                }
                return all;
            }

            var ordered = GetOrderedRegistrationsNoLock(kind);
            var snapshot = new List<TimeTriggerInfo>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                snapshot.Add(new TimeTriggerInfo(ordered[i].Id, ordered[i].Priority, ordered[i].Cadence));
            }
            return snapshot;
        }

        private static bool MatchesKind(TimeTriggerCadence cadence, TimeTriggerKind kind)
        {
            if (cadence == TimeTriggerCadence.Both)
                return true;

            if (kind == TimeTriggerKind.SixHour)
                return cadence == TimeTriggerCadence.SixHour;

            return cadence == TimeTriggerCadence.Staggered;
        }

        private static int NextFixedTickAfter(int currentMinute, int intervalMinutes, int offsetMinutes)
        {
            if (intervalMinutes <= 0)
                intervalMinutes = 1;

            int shifted = currentMinute - offsetMinutes;
            int remainder = shifted % intervalMinutes;
            if (remainder < 0)
                remainder += intervalMinutes;
            if (remainder == 0)
                return currentMinute + intervalMinutes;
            return currentMinute + (intervalMinutes - remainder);
        }

        private static int NextStaggeredIntervalNoLock()
        {
            int min = _staggeredMinHours;
            int max = _staggeredMaxHours;
            if (min < 1) min = 1;
            if (max < min) max = min;

            int value;
            if (TryNextModRandomRangeNoLock(min, max + 1, out value))
            {
                return value;
            }

            // XorShift fallback.
            uint x = _fallbackState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _fallbackState = x;
            int span = (max - min) + 1;
            return min + (int)(x % (uint)span);
        }

        private static bool TryNextModRandomRangeNoLock(int minInclusive, int maxExclusive, out int value)
        {
            value = minInclusive;
            try
            {
                EnsureModRandomBridgeNoLock();
                if (_modRandomGetStream == null)
                    return false;

                if (_modRandomStream == null || _modRandomStreamRangeIntMethod == null)
                {
                    _modRandomStream = _modRandomGetStream.Invoke(null, new object[] { RandomStreamName });
                    if (_modRandomStream == null)
                        return false;
                    _modRandomStreamRangeIntMethod = _modRandomStream.GetType().GetMethod("Range", new[] { typeof(int), typeof(int) });
                    if (_modRandomStreamRangeIntMethod == null)
                        return false;
                }

                object result = _modRandomStreamRangeIntMethod.Invoke(_modRandomStream, new object[] { minInclusive, maxExclusive });
                if (result is int)
                {
                    value = (int)result;
                    return true;
                }
            }
            catch (Exception ex)
            {
                WarnOnce("GameTimeTriggerHelper.ModRandom.Range", "Failed to read ModRandom stream; using fallback RNG. " + ex.Message);
                _modRandomStream = null;
                _modRandomStreamRangeIntMethod = null;
            }
            return false;
        }

        private static void EnsureModRandomBridgeNoLock()
        {
            if (_modRandomGetStream != null)
                return;

            _modRandomType = Type.GetType("ModAPI.Core.ModRandom, ModAPI", false);
            if (_modRandomType == null)
                return;

            _modRandomGetStream = _modRandomType.GetMethod("GetStream", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        }

        private static bool TryGetCurrentTotalMinute(out int totalMinute)
        {
            totalMinute = 0;
            try
            {
                int day = GameTime.Day;
                int hour = GameTime.Hour;
                int minute = GameTime.Minute;

                if (day < 1) day = 1;
                if (hour < 0) hour = 0;
                if (hour > 23) hour = hour % 24;
                if (minute < 0) minute = 0;
                if (minute > 59) minute = minute % 60;

                totalMinute = ((day - 1) * 1440) + (hour * 60) + minute;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsGameLoading()
        {
            try
            {
                var sm = SaveManager.instance;
                return sm != null && sm.isLoading;
            }
            catch
            {
                return false;
            }
        }

        private static void WarnOnce(string key, string message)
        {
            try
            {
                Type mmLogType = Type.GetType("ModAPI.Core.MMLog, ModAPI", false);
                if (mmLogType != null)
                {
                    MethodInfo warnOnce = mmLogType.GetMethod("WarnOnce", BindingFlags.Public | BindingFlags.Static);
                    if (warnOnce != null)
                    {
                        warnOnce.Invoke(null, new object[] { key, message });
                        return;
                    }
                }
            }
            catch
            {
            }

            lock (WarnOnceSync)
            {
                if (LocalWarnOnceKeys.Contains(key))
                    return;
                LocalWarnOnceKeys.Add(key);
            }

            Debug.LogWarning("[GameTimeTriggerHelper] " + message);
        }

        [HarmonyPatch(typeof(GameTime), "Awake")]
        private static class GameTime_Awake_Patch
        {
            private static void Postfix()
            {
                lock (Sync)
                {
                    _forceResync = true;
                }
            }
        }

        [HarmonyPatch(typeof(GameTime), "Update")]
        private static class GameTime_Update_Patch
        {
            private static void Postfix()
            {
                GameTimeTriggerHelper.TickFromGameTime();
            }
        }

        private struct Registration
        {
            public string Id;
            public int Priority;
            public TimeTriggerCadence Cadence;
            public Action<TimeTriggerBatch> Callback;
            public long Order;
        }

        private sealed class RegistrationComparer : IComparer<Registration>
        {
            public static readonly RegistrationComparer Instance = new RegistrationComparer();

            public int Compare(Registration x, Registration y)
            {
                int byPriority = x.Priority.CompareTo(y.Priority);
                if (byPriority != 0)
                    return byPriority;

                int byOrder = x.Order.CompareTo(y.Order);
                if (byOrder != 0)
                    return byOrder;

                return string.Compare(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class DispatchJob
        {
            public TimeTriggerKind Kind;
            public int TotalMinutes;
            public int Sequence;
            public int IntervalMinutes;
            public List<Registration> OrderedRegistrations;
        }
    }
}
