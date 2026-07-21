using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Infrastructure;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    internal sealed class ScenarioFutureSurvivorRecruitBindingService
    {
        private readonly ScenarioActorResolver _actorResolver;
        private readonly List<PendingRecruit> _pendingAttributes = new List<PendingRecruit>();
        private readonly List<PendingRecruit> _activeVisitors = new List<PendingRecruit>();

        public ScenarioFutureSurvivorRecruitBindingService(ScenarioActorResolver actorResolver)
        {
            _actorResolver = actorResolver;
        }

        public bool ScheduleAskToJoin(
            ScenarioDefinition definition,
            FutureSurvivorDefinition survivor,
            float arrivalDelay,
            out string message)
        {
            message = null;
            if (survivor == null || survivor.Survivor == null)
            {
                message = "Future recruit configuration was missing.";
                return false;
            }

            FamilySpawner.CharacterAttributes queuedAttributes = null;
            string scheduleMessage = null;
            string seamMessage;
            bool scheduled;
            if (!SeamGuard.Try<bool>(
                "scenario.future-survivor.ask-to-join.schedule",
                SeamRecoveryPolicy.RetryOnce,
                delegate { return ScenarioFamilyMemberFactory.ScheduleRecruit(survivor.Survivor, arrivalDelay, out queuedAttributes, out scheduleMessage); },
                false,
                "Ask-to-join survivor binding unavailable - scenario still playable.",
                null,
                out scheduled,
                out seamMessage))
            {
                message = seamMessage;
                return false;
            }
            if (!scheduled)
            {
                message = scheduleMessage;
                return false;
            }

            if (queuedAttributes != null)
            {
                _pendingAttributes.Add(new PendingRecruit(definition, survivor, queuedAttributes));
                MMLog.WriteInfo("[ScenarioFutureSurvivorRecruitBinding] Queued ask-to-join survivor '"
                    + (survivor.Survivor.Name ?? survivor.Id ?? string.Empty) + "' for scenario actor binding.");
            }

            return true;
        }

        public void OnVisitorCreated(NpcVisitor visitor, FamilySpawner.CharacterAttributes queuedAttributes)
        {
            if (visitor == null || queuedAttributes == null)
                return;

            for (int i = 0; i < _pendingAttributes.Count; i++)
            {
                PendingRecruit pending = _pendingAttributes[i];
                if (pending == null || !object.ReferenceEquals(pending.QueuedAttributes, queuedAttributes))
                    continue;

                pending.Visitor = visitor;
                _pendingAttributes.RemoveAt(i);
                _activeVisitors.Add(pending);
                MMLog.WriteInfo("[ScenarioFutureSurvivorRecruitBinding] Scenario ask-to-join visitor spawned for future survivor '"
                    + pending.DisplayName + "'.");
                return;
            }
        }

        public void OnVisitorFinished(NpcVisitor visitor)
        {
            PendingRecruit pending = RemoveActiveVisitor(visitor);
            if (pending == null)
                return;

            MMLog.WriteInfo("[ScenarioFutureSurvivorRecruitBinding] Scenario ask-to-join visitor left without recruitment for future survivor '"
                + pending.DisplayName + "'. Actor remains offscreen.");
        }

        public void OnNpcAdopted(NpcVisitor visitor, FamilyMember member)
        {
            if (visitor == null || member == null)
                return;

            PendingRecruit pending = RemoveActiveVisitor(visitor);
            if (pending == null)
                return;

            ScenarioFamilyMemberFactory.ApplyConditions(member, pending.Survivor != null ? pending.Survivor.Survivor : null);

            if (_actorResolver == null)
                return;

            ScenarioActorRef actorRef = pending.Survivor.ActorRef
                ?? (pending.Survivor.Survivor != null ? pending.Survivor.Survivor.ActorRef : null);
            string bindMessage;
            if (_actorResolver.BindMaterializedFamilyMember(
                pending.Definition,
                actorRef,
                member,
                pending.Survivor.Survivor,
                pending.Survivor.ActorComponents,
                out bindMessage))
            {
                MMLog.WriteInfo("[ScenarioFutureSurvivorRecruitBinding] Bound accepted ask-to-join survivor '"
                    + (member.firstName ?? pending.DisplayName) + "' to scenario actor. " + (bindMessage ?? string.Empty));
            }
            else if (!string.IsNullOrEmpty(bindMessage))
            {
                MMLog.WriteWarning("[ScenarioFutureSurvivorRecruitBinding] " + bindMessage);
            }
        }

        private PendingRecruit RemoveActiveVisitor(NpcVisitor visitor)
        {
            if (visitor == null)
                return null;

            for (int i = 0; i < _activeVisitors.Count; i++)
            {
                PendingRecruit pending = _activeVisitors[i];
                if (pending == null || !object.ReferenceEquals(pending.Visitor, visitor))
                    continue;

                _activeVisitors.RemoveAt(i);
                return pending;
            }

            return null;
        }

        private sealed class PendingRecruit
        {
            public PendingRecruit(
                ScenarioDefinition definition,
                FutureSurvivorDefinition survivor,
                FamilySpawner.CharacterAttributes queuedAttributes)
            {
                Definition = definition;
                Survivor = survivor;
                QueuedAttributes = queuedAttributes;
            }

            public ScenarioDefinition Definition;
            public FutureSurvivorDefinition Survivor;
            public FamilySpawner.CharacterAttributes QueuedAttributes;
            public NpcVisitor Visitor;

            public string DisplayName
            {
                get
                {
                    if (Survivor != null && Survivor.Survivor != null && !string.IsNullOrEmpty(Survivor.Survivor.Name))
                        return Survivor.Survivor.Name;
                    return Survivor != null ? (Survivor.Id ?? string.Empty) : string.Empty;
                }
            }
        }
    }
}
