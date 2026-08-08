using System;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.People
{
    internal static class ScenarioActorReferenceIndex
    {
        public static bool Contains(ScenarioDefinition definition, ScenarioActorRef actorRef, bool includeStarting, bool includeFuture)
        {
            if (actorRef == null)
                return true;

            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            if (includeStarting)
            {
                for (int i = 0; family != null && family.Members != null && i < family.Members.Count; i++)
                {
                    FamilyMemberConfig member = family.Members[i];
                    if (member != null && Same(member.ActorRef, actorRef))
                        return true;
                }
            }

            if (includeFuture)
            {
                for (int i = 0; family != null && family.FutureSurvivors != null && i < family.FutureSurvivors.Count; i++)
                {
                    FutureSurvivorDefinition future = family.FutureSurvivors[i];
                    FamilyMemberConfig survivor = future != null ? future.Survivor : null;
                    ScenarioActorRef candidate = ScenarioFutureSurvivorActorReference.Resolve(future);
                    if (Same(candidate, actorRef))
                        return true;
                }
            }

            return false;
        }

        public static string Format(ScenarioActorRef actorRef)
        {
            if (actorRef == null)
                return "<none>";
            if (!string.IsNullOrEmpty(actorRef.DisplayNameFallback))
                return actorRef.DisplayNameFallback;
            if (!string.IsNullOrEmpty(actorRef.Kind))
                return actorRef.Kind + ":" + actorRef.LocalId.ToString(CultureInfo.InvariantCulture);
            return !string.IsNullOrEmpty(actorRef.BindingKey) ? actorRef.BindingKey : "<actor>";
        }

        private static bool Same(ScenarioActorRef left, ScenarioActorRef right)
        {
            if (left == null || right == null)
                return false;

            if (!string.IsNullOrEmpty(left.Kind) || !string.IsNullOrEmpty(right.Kind))
            {
                return string.Equals(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase)
                    && left.LocalId == right.LocalId
                    && string.Equals(left.Domain, right.Domain, StringComparison.OrdinalIgnoreCase);
            }

            return !string.IsNullOrEmpty(left.BindingKey)
                && !string.IsNullOrEmpty(right.BindingKey)
                && string.Equals(left.BindingType, right.BindingType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.BindingKey, right.BindingKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Domain, right.Domain, StringComparison.OrdinalIgnoreCase);
        }
    }
}
