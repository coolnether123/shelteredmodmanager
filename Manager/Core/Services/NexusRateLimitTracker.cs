using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;

namespace Manager.Core.Services
{
    /// <summary>
    /// Coordinates Nexus API quotas for one service instance. Quota state is
    /// partitioned by credential scope and never stores API keys or tokens.
    /// </summary>
    internal sealed class NexusRateLimitTracker
    {
        private const string HourlyRemainingHeader = "X-RL-Hourly-Remaining";
        private const string DailyRemainingHeader = "X-RL-Daily-Remaining";
        private const string DateHeader = "Date";
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, QuotaState> _states = new Dictionary<string, QuotaState>(StringComparer.Ordinal);
        private readonly Func<DateTime> _utcNow;

        internal NexusRateLimitTracker()
            : this(delegate { return DateTime.UtcNow; })
        {
        }

        internal NexusRateLimitTracker(Func<DateTime> utcNow)
        {
            _utcNow = utcNow ?? delegate { return DateTime.UtcNow; };
        }

        internal bool TryAcquire(string credentialScope, out Lease lease, out string message)
        {
            string scope = NormalizeScope(credentialScope);
            lock (_syncRoot)
            {
                DateTime nowUtc = NormalizeUtc(_utcNow());
                QuotaState state = GetOrCreateState(scope, nowUtc);
                ClearExpiredWindows(state, nowUtc);

                if (state.DailyRemaining.HasValue && state.DailyRemaining.Value <= 0)
                {
                    lease = null;
                    message = BuildMessage("daily", state.DailyWindowStartUtc.AddDays(1));
                    return false;
                }

                if (state.HourlyRemaining.HasValue && state.HourlyRemaining.Value <= 0)
                {
                    lease = null;
                    message = BuildMessage("hourly", state.HourlyWindowStartUtc.AddHours(1));
                    return false;
                }

                bool dailyReserved = state.DailyRemaining.HasValue;
                bool hourlyReserved = state.HourlyRemaining.HasValue;
                if (dailyReserved)
                    state.DailyRemaining = Math.Max(0, state.DailyRemaining.Value - 1);
                if (hourlyReserved)
                    state.HourlyRemaining = Math.Max(0, state.HourlyRemaining.Value - 1);

                lease = new Lease(
                    scope,
                    hourlyReserved,
                    dailyReserved,
                    state.HourlyWindowStartUtc,
                    state.DailyWindowStartUtc);
                message = null;
                return true;
            }
        }

        internal void Release(Lease lease)
        {
            if (lease == null)
                return;

            lock (_syncRoot)
            {
                if (lease.Completed)
                    return;
                lease.Completed = true;

                QuotaState state;
                if (!_states.TryGetValue(lease.CredentialScope, out state))
                    return;

                if (lease.HourlyReserved && state.HourlyRemaining.HasValue &&
                    state.HourlyWindowStartUtc == lease.HourlyWindowStartUtc)
                {
                    state.HourlyRemaining++;
                }

                if (lease.DailyReserved && state.DailyRemaining.HasValue &&
                    state.DailyWindowStartUtc == lease.DailyWindowStartUtc)
                {
                    state.DailyRemaining++;
                }
            }
        }

        internal void Observe(HttpWebResponse response, Lease lease)
        {
            if (response == null || lease == null)
                return;

            lock (_syncRoot)
            {
                lease.Completed = true;
            }

            int hourlyRemaining;
            int dailyRemaining;
            bool hasHourly = TryReadRemaining(response, HourlyRemainingHeader, out hourlyRemaining);
            bool hasDaily = TryReadRemaining(response, DailyRemainingHeader, out dailyRemaining);
            if (!hasHourly && !hasDaily)
                return;

            DateTime observedUtc = ReadServerUtc(response, NormalizeUtc(_utcNow()));
            DateTime hourlyWindowStartUtc = StartOfHour(observedUtc);
            DateTime dailyWindowStartUtc = observedUtc.Date;

            lock (_syncRoot)
            {
                QuotaState state = GetOrCreateState(lease.CredentialScope, observedUtc);
                if (hasHourly)
                {
                    MergeRemaining(
                        ref state.HourlyRemaining,
                        ref state.HourlyWindowStartUtc,
                        hourlyRemaining,
                        hourlyWindowStartUtc);
                }

                if (hasDaily)
                {
                    MergeRemaining(
                        ref state.DailyRemaining,
                        ref state.DailyWindowStartUtc,
                        dailyRemaining,
                        dailyWindowStartUtc);
                }
            }
        }

        internal bool TryGetBlockingMessage(string credentialScope, out string message)
        {
            string scope = NormalizeScope(credentialScope);
            lock (_syncRoot)
            {
                DateTime nowUtc = NormalizeUtc(_utcNow());
                QuotaState state = GetOrCreateState(scope, nowUtc);
                ClearExpiredWindows(state, nowUtc);

                if (state.DailyRemaining.HasValue && state.DailyRemaining.Value <= 0)
                {
                    message = BuildMessage("daily", state.DailyWindowStartUtc.AddDays(1));
                    return true;
                }

                if (state.HourlyRemaining.HasValue && state.HourlyRemaining.Value <= 0)
                {
                    message = BuildMessage("hourly", state.HourlyWindowStartUtc.AddHours(1));
                    return true;
                }
            }

            message = null;
            return false;
        }

        private QuotaState GetOrCreateState(string scope, DateTime nowUtc)
        {
            QuotaState state;
            if (_states.TryGetValue(scope, out state))
                return state;

            state = new QuotaState
            {
                HourlyWindowStartUtc = StartOfHour(nowUtc),
                DailyWindowStartUtc = nowUtc.Date
            };
            _states.Add(scope, state);
            return state;
        }

        private static void MergeRemaining(
            ref int? currentRemaining,
            ref DateTime currentWindowStartUtc,
            int reportedRemaining,
            DateTime reportedWindowStartUtc)
        {
            if (reportedWindowStartUtc < currentWindowStartUtc)
                return;

            if (reportedWindowStartUtc > currentWindowStartUtc)
            {
                currentWindowStartUtc = reportedWindowStartUtc;
                currentRemaining = reportedRemaining;
                return;
            }

            currentRemaining = currentRemaining.HasValue
                ? Math.Min(currentRemaining.Value, reportedRemaining)
                : reportedRemaining;
        }

        private static bool TryReadRemaining(HttpWebResponse response, string headerName, out int remaining)
        {
            string raw = response.Headers[headerName];
            return int.TryParse(raw, out remaining) && remaining >= 0;
        }

        private static DateTime ReadServerUtc(HttpWebResponse response, DateTime fallbackUtc)
        {
            DateTime parsed;
            return DateTime.TryParse(
                response.Headers[DateHeader],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed)
                ? NormalizeUtc(parsed)
                : fallbackUtc;
        }

        private static void ClearExpiredWindows(QuotaState state, DateTime nowUtc)
        {
            DateTime hourlyWindowStartUtc = StartOfHour(nowUtc);
            if (hourlyWindowStartUtc > state.HourlyWindowStartUtc)
            {
                state.HourlyWindowStartUtc = hourlyWindowStartUtc;
                state.HourlyRemaining = null;
            }

            DateTime dailyWindowStartUtc = nowUtc.Date;
            if (dailyWindowStartUtc > state.DailyWindowStartUtc)
            {
                state.DailyWindowStartUtc = dailyWindowStartUtc;
                state.DailyRemaining = null;
            }
        }

        private static DateTime StartOfHour(DateTime utc)
        {
            return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        private static string NormalizeScope(string credentialScope)
        {
            return string.IsNullOrEmpty(credentialScope) ? "anonymous" : credentialScope;
        }

        private static string BuildMessage(string windowName, DateTime resetUtc)
        {
            return "Nexus API " + windowName + " rate limit reached. Try again after " +
                resetUtc.ToString("yyyy-MM-dd HH:mm 'UTC'") + ".";
        }

        internal sealed class Lease
        {
            internal readonly string CredentialScope;
            internal readonly bool HourlyReserved;
            internal readonly bool DailyReserved;
            internal readonly DateTime HourlyWindowStartUtc;
            internal readonly DateTime DailyWindowStartUtc;
            internal bool Completed;

            internal Lease(
                string credentialScope,
                bool hourlyReserved,
                bool dailyReserved,
                DateTime hourlyWindowStartUtc,
                DateTime dailyWindowStartUtc)
            {
                CredentialScope = credentialScope;
                HourlyReserved = hourlyReserved;
                DailyReserved = dailyReserved;
                HourlyWindowStartUtc = hourlyWindowStartUtc;
                DailyWindowStartUtc = dailyWindowStartUtc;
            }
        }

        private sealed class QuotaState
        {
            internal int? HourlyRemaining;
            internal int? DailyRemaining;
            internal DateTime HourlyWindowStartUtc;
            internal DateTime DailyWindowStartUtc;
        }
    }
}
