using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredAPI.Scenarios.Domain.Scheduling;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioCastMemberReferenceCandidate
    {
        public const string StartingKind = "starting";
        public const string FutureKind = "future";

        public string Token { get; set; }
        public string Kind { get; set; }
        public int Index { get; set; }
        public string DisplayName { get; set; }
        public string Detail { get; set; }
        public string Badge { get; set; }
        public string TargetId { get; set; }
        public ScenarioActorRef ActorRef { get; set; }
        public FamilyMemberConfig Member { get; set; }
        public FutureSurvivorDefinition FutureSurvivor { get; set; }
    }

    internal static class ScenarioCastMemberReferenceCatalog
    {
        public static List<ScenarioCastMemberReferenceCandidate> Build(ScenarioDefinition definition, bool includeStarting, bool includeFuture)
        {
            List<ScenarioCastMemberReferenceCandidate> results = new List<ScenarioCastMemberReferenceCandidate>();
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            if (includeStarting)
            {
                for (int i = 0; family != null && family.Members != null && i < family.Members.Count; i++)
                {
                    FamilyMemberConfig member = family.Members[i];
                    if (member == null)
                        continue;

                    ScenarioActorRef actorRef = member.ActorRef;
                    string name = ResolveDisplayName(actorRef, member.Name, "Starting survivor " + (i + 1).ToString(CultureInfo.InvariantCulture));
                    results.Add(new ScenarioCastMemberReferenceCandidate
                    {
                        Token = ScenarioCastMemberReferenceCandidate.StartingKind + ":" + i.ToString(CultureInfo.InvariantCulture),
                        Kind = ScenarioCastMemberReferenceCandidate.StartingKind,
                        Index = i,
                        DisplayName = name,
                        Detail = "Starting cast member",
                        Badge = "START",
                        TargetId = member.Name,
                        ActorRef = actorRef,
                        Member = member
                    });
                }
            }

            if (includeFuture)
            {
                for (int i = 0; family != null && family.FutureSurvivors != null && i < family.FutureSurvivors.Count; i++)
                {
                    FutureSurvivorDefinition future = family.FutureSurvivors[i];
                    if (future == null)
                        continue;

                    FamilyMemberConfig survivor = future.Survivor;
                    ScenarioActorRef actorRef = ShelteredAPI.Scenarios.Public.ShelteredScenarioAuthoring.ResolveFutureSurvivorActorReference(future);
                    string fallbackName = survivor != null ? survivor.Name : null;
                    string name = ResolveDisplayName(actorRef, fallbackName, !string.IsNullOrEmpty(future.Id) ? future.Id : "Future survivor " + (i + 1).ToString(CultureInfo.InvariantCulture));
                    results.Add(new ScenarioCastMemberReferenceCandidate
                    {
                        Token = ScenarioCastMemberReferenceCandidate.FutureKind + ":" + i.ToString(CultureInfo.InvariantCulture),
                        Kind = ScenarioCastMemberReferenceCandidate.FutureKind,
                        Index = i,
                        DisplayName = name,
                        Detail = "Arrives " + FormatSchedule(future.Arrival),
                        Badge = "FUTURE",
                        TargetId = !string.IsNullOrEmpty(future.Id) ? future.Id : fallbackName,
                        ActorRef = actorRef,
                        Member = survivor,
                        FutureSurvivor = future
                    });
                }
            }

            return results;
        }

        public static bool TryFindByToken(ScenarioDefinition definition, bool includeStarting, bool includeFuture, string token, out ScenarioCastMemberReferenceCandidate candidate)
        {
            candidate = null;
            List<ScenarioCastMemberReferenceCandidate> candidates = Build(definition, includeStarting, includeFuture);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != null && string.Equals(candidates[i].Token, token, StringComparison.OrdinalIgnoreCase))
                {
                    candidate = candidates[i];
                    return true;
                }
            }

            return false;
        }

        public static ScenarioCastMemberReferenceCandidate FindFirst(ScenarioDefinition definition, bool includeStarting, bool includeFuture)
        {
            List<ScenarioCastMemberReferenceCandidate> candidates = Build(definition, includeStarting, includeFuture);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        public static ScenarioCastMemberReferenceCandidate FindByActorRef(ScenarioDefinition definition, ScenarioActorRef actorRef, bool includeStarting, bool includeFuture)
        {
            if (actorRef == null)
                return null;

            List<ScenarioCastMemberReferenceCandidate> candidates = Build(definition, includeStarting, includeFuture);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != null && SameActorRef(candidates[i].ActorRef, actorRef))
                    return candidates[i];
            }

            return null;
        }

        public static ScenarioCastMemberReferenceCandidate FindByFutureSurvivorId(ScenarioDefinition definition, string survivorId)
        {
            if (string.IsNullOrEmpty(survivorId))
                return null;

            List<ScenarioCastMemberReferenceCandidate> candidates = Build(definition, false, true);
            for (int i = 0; i < candidates.Count; i++)
            {
                ScenarioCastMemberReferenceCandidate candidate = candidates[i];
                if (candidate != null && string.Equals(candidate.TargetId, survivorId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        public static string ResolveDisplayName(ScenarioDefinition definition, ScenarioActorRef actorRef, bool includeStarting, bool includeFuture, string fallback)
        {
            ScenarioCastMemberReferenceCandidate candidate = FindByActorRef(definition, actorRef, includeStarting, includeFuture);
            if (candidate != null && !string.IsNullOrEmpty(candidate.DisplayName))
                return candidate.DisplayName;

            return ResolveDisplayName(actorRef, fallback, fallback);
        }

        public static bool HasActorRef(ScenarioDefinition definition, ScenarioActorRef actorRef, bool includeStarting, bool includeFuture)
        {
            return actorRef == null || FindByActorRef(definition, actorRef, includeStarting, includeFuture) != null;
        }

        public static ScenarioActorRef CopyActorRef(ScenarioActorRef actorRef)
        {
            if (actorRef == null)
                return null;

            return new ScenarioActorRef
            {
                Kind = actorRef.Kind,
                LocalId = actorRef.LocalId,
                Domain = actorRef.Domain,
                BindingType = actorRef.BindingType,
                BindingKey = actorRef.BindingKey,
                DisplayNameFallback = actorRef.DisplayNameFallback,
                RequiredModId = actorRef.RequiredModId
            };
        }

        public static bool SameActorRef(ScenarioActorRef left, ScenarioActorRef right)
        {
            if (left == null || right == null)
                return false;

            if (!string.IsNullOrEmpty(left.Kind) || !string.IsNullOrEmpty(right.Kind))
                return string.Equals(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase)
                    && left.LocalId == right.LocalId
                    && string.Equals(left.Domain, right.Domain, StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(left.BindingKey) && string.IsNullOrEmpty(right.BindingKey))
                return false;

            return string.Equals(left.BindingType, right.BindingType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.BindingKey, right.BindingKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Domain, right.Domain, StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatActorRef(ScenarioActorRef actorRef)
        {
            if (actorRef == null)
                return "<none>";
            if (!string.IsNullOrEmpty(actorRef.DisplayNameFallback))
                return actorRef.DisplayNameFallback;
            if (!string.IsNullOrEmpty(actorRef.Kind))
                return actorRef.Kind + ":" + actorRef.LocalId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(actorRef.BindingKey))
                return actorRef.BindingKey;
            return "<actor>";
        }

        private static string ResolveDisplayName(ScenarioActorRef actorRef, string authoredName, string fallback)
        {
            if (actorRef != null && !string.IsNullOrEmpty(actorRef.DisplayNameFallback))
                return actorRef.DisplayNameFallback;
            if (!string.IsNullOrEmpty(authoredName))
                return authoredName;
            return !string.IsNullOrEmpty(fallback) ? fallback : "<missing>";
        }

        private static string FormatSchedule(ScenarioScheduleTime time)
        {
            if (time == null)
                return "unscheduled";
            return "day " + Math.Max(1, time.Day).ToString(CultureInfo.InvariantCulture)
                + " " + Math.Max(0, Math.Min(23, time.Hour)).ToString("D2")
                + ":" + Math.Max(0, Math.Min(59, time.Minute)).ToString("D2");
        }
    }
}
