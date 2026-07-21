using System;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Application.Scheduling{
    internal enum ScenarioSchedulePolicyDecision
    {
        NotDue = 0,
        Due = 1,
        Skipped = 2
    }

    internal static class ScenarioSchedulePolicyEvaluator
    {
        public static ScenarioSchedulePolicyDecision Evaluate(
            ScenarioScheduledActionDefinition action,
            long nowMinutes,
            int successfulRuns,
            int attemptedRuns,
            long? lastAttemptMinutes,
            out string reason)
        {
            reason = null;
            if (action == null || action.DueTime == null)
                return ScenarioSchedulePolicyDecision.NotDue;

            ScenarioSchedulePolicy policy = action.Policy;
            long startMinutes = ToGameMinutes(action.DueTime.Day, action.DueTime.Hour, action.DueTime.Minute);
            if (nowMinutes < startMinutes)
                return ScenarioSchedulePolicyDecision.NotDue;

            long windowEndMinutes = ResolveWindowEndMinutes(policy);
            if (windowEndMinutes >= 0 && nowMinutes > windowEndMinutes)
                return ScenarioSchedulePolicyDecision.NotDue;

            int maxRuns = policy != null ? Math.Max(0, policy.MaxRuns) : 0;
            if (maxRuns > 0 && successfulRuns >= maxRuns)
                return ScenarioSchedulePolicyDecision.NotDue;

            long nextDueMinutes = ResolveNextDueMinutes(action, nowMinutes, attemptedRuns, lastAttemptMinutes);
            if (nextDueMinutes < 0 || nowMinutes < nextDueMinutes)
                return ScenarioSchedulePolicyDecision.NotDue;
            if (windowEndMinutes >= 0 && nextDueMinutes > windowEndMinutes)
                return ScenarioSchedulePolicyDecision.NotDue;

            float chance = policy != null ? policy.Chance : 1f;
            if (chance < 0f)
                chance = 0f;
            if (chance > 1f)
                chance = 1f;
            if (chance <= 0f || (chance < 1f && DeterministicRoll(action.Id, attemptedRuns) > chance))
            {
                reason = "Schedule chance missed for action '" + (action.Id ?? string.Empty) + "'.";
                return ScenarioSchedulePolicyDecision.Skipped;
            }

            return ScenarioSchedulePolicyDecision.Due;
        }

        private static long ResolveNextDueMinutes(
            ScenarioScheduledActionDefinition action,
            long nowMinutes,
            int attemptedRuns,
            long? lastAttemptMinutes)
        {
            ScenarioSchedulePolicy policy = action.Policy;
            long startMinutes = ToGameMinutes(action.DueTime.Day, action.DueTime.Hour, action.DueTime.Minute);
            bool repeatable = policy != null && policy.Repeatable;
            int jitter = policy != null ? Math.Max(0, policy.JitterMinutes) : 0;
            long jitterMinutes = ResolveJitterMinutes(action.Id, attemptedRuns, jitter);

            if (!repeatable)
            {
                if (!lastAttemptMinutes.HasValue)
                    return startMinutes + jitterMinutes;

                int retryMinutes = policy != null && policy.CooldownMinutes > 0 ? policy.CooldownMinutes : 1;
                return lastAttemptMinutes.Value + retryMinutes;
            }

            if (!lastAttemptMinutes.HasValue)
                return startMinutes + jitterMinutes;

            int cooldown = policy != null ? Math.Max(0, policy.CooldownMinutes) : 0;
            long baseMinutes = lastAttemptMinutes.Value + (cooldown > 0 ? cooldown : 1);
            return baseMinutes + jitterMinutes;
        }

        private static long ResolveWindowEndMinutes(ScenarioSchedulePolicy policy)
        {
            if (policy == null || policy.WindowEndDay <= 0)
                return -1L;
            return ToGameMinutes(policy.WindowEndDay, 23, 59);
        }

        private static long ResolveJitterMinutes(string actionId, int runIndex, int jitterMinutes)
        {
            if (jitterMinutes <= 0)
                return 0L;
            return (long)(Hash(actionId, runIndex, "jitter") % (jitterMinutes + 1));
        }

        private static float DeterministicRoll(string actionId, int runIndex)
        {
            int value = Hash(actionId, runIndex, "chance") % 1000000;
            return (float)value / 999999f;
        }

        private static int Hash(string actionId, int runIndex, string salt)
        {
            unchecked
            {
                int hash = 17;
                string text = (actionId ?? string.Empty) + ":" + runIndex.ToString() + ":" + salt;
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
                return hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
            }
        }

        public static long ToGameMinutes(int day, int hour, int minute)
        {
            return ((long)day * 24L * 60L) + ((long)hour * 60L) + minute;
        }
    }
}
