using System;
using System.Collections.Generic;

namespace ModAPI.Actors.Internal
{
    internal sealed partial class ActorSystemImpl
    {
        private void HandleSessionReset()
        {
            lock (_sync)
            {
                ClearStateLocked();
                _currentTick = 0L;
            }
        }

        private bool RefreshLiveActors()
        {
            bool changed = false;
            HashSet<int> seenFamily = new HashSet<int>();
            HashSet<int> seenVisitors = new HashSet<int>();

            try
            {
                FamilyManager familyManager = FamilyManager.Instance;
                if (familyManager != null)
                {
                    IList<FamilyMember> members = familyManager.GetAllFamilyMembers();
                    if (members != null)
                    {
                        for (int i = 0; i < members.Count; i++)
                        {
                            FamilyMember member = members[i];
                            if (member == null) continue;

                            int id = member.GetId();
                            seenFamily.Add(id);

                            try
                            {
                                if (UpsertFamilyActor(member))
                                    changed = true;

                                ReportRecovery(
                                    ActorFailureKind.LiveSync,
                                    ActorEventType.LiveSyncRecovered,
                                    BuiltInOwner,
                                    "core.family." + id,
                                    "Family actor sync recovered");
                            }
                            catch (Exception ex)
                            {
                                ReportFailure(
                                    ActorFailureKind.LiveSync,
                                    ActorEventType.LiveSyncFailed,
                                    BuiltInOwner,
                                    "core.family." + id,
                                    "Family actor sync failed: " + ex.Message,
                                    ex);
                            }
                        }
                    }
                }

                ReportRecovery(
                    ActorFailureKind.LiveSync,
                    ActorEventType.LiveSyncRecovered,
                    BuiltInOwner,
                    "core.family.scan",
                    "Family live sync recovered");
            }
            catch (Exception ex)
            {
                ReportFailure(
                    ActorFailureKind.LiveSync,
                    ActorEventType.LiveSyncFailed,
                    BuiltInOwner,
                    "core.family.scan",
                    "Family live sync failed: " + ex.Message,
                    ex);
            }

            try
            {
                NpcVisitManager manager = NpcVisitManager.Instance;
                if (manager != null && manager.Visitors != null)
                {
                    for (int i = 0; i < manager.Visitors.Count; i++)
                    {
                        NpcVisitor visitor = manager.Visitors[i];
                        if (visitor == null) continue;

                        int id = visitor.npcId;
                        seenVisitors.Add(id);

                        try
                        {
                            if (UpsertVisitorActor(visitor))
                                changed = true;

                            ReportRecovery(
                                ActorFailureKind.LiveSync,
                                ActorEventType.LiveSyncRecovered,
                                BuiltInOwner,
                                "core.visitor." + id,
                                "Visitor actor sync recovered");
                        }
                        catch (Exception ex)
                        {
                            ReportFailure(
                                ActorFailureKind.LiveSync,
                                ActorEventType.LiveSyncFailed,
                                BuiltInOwner,
                                "core.visitor." + id,
                                "Visitor actor sync failed: " + ex.Message,
                                ex);
                        }
                    }
                }

                ReportRecovery(
                    ActorFailureKind.LiveSync,
                    ActorEventType.LiveSyncRecovered,
                    BuiltInOwner,
                    "core.visitor.scan",
                    "Visitor live sync recovered");
            }
            catch (Exception ex)
            {
                ReportFailure(
                    ActorFailureKind.LiveSync,
                    ActorEventType.LiveSyncFailed,
                    BuiltInOwner,
                    "core.visitor.scan",
                    "Visitor live sync failed: " + ex.Message,
                    ex);
            }

            List<ActorRecord> unloaded = new List<ActorRecord>();
            lock (_sync)
            {
                foreach (ActorRecord record in _records.Values)
                {
                    if (record == null || record.Id == null || record.Origin == null) continue;
                    if (!string.Equals(record.Origin.SourceModId ?? string.Empty, "core", StringComparison.OrdinalIgnoreCase)) continue;

                    bool missing = false;
                    if (record.Id.Kind == ActorKind.Player) missing = !seenFamily.Contains(record.Id.LocalId);
                    else if (record.Id.Kind == ActorKind.Visitor) missing = !seenVisitors.Contains(record.Id.LocalId);
                    if (!missing) continue;

                    ActorFlags flags = record.Flags & ~ActorFlags.Loaded;
                    if (UpdateRecordStateLocked(record, ActorLifecycleState.Unloaded, ActorPresenceState.Offscreen, flags, _currentTick))
                        unloaded.Add(record.Clone());
                }

                _lastLiveRefreshTick = _currentTick;
                _lastLiveRefreshUpdateSequence = _updateSequence;
            }

            for (int i = 0; i < unloaded.Count; i++)
            {
                changed = true;
                RaiseActorStateChanged(unloaded[i]);
                Publish(ActorEventType.ActorStateChanged, BuiltInOwner, unloaded[i].Id, null, "Core actor unloaded");
            }

            return changed;
        }

        private bool UpsertFamilyActor(FamilyMember member)
        {
            ActorId id = new ActorId(ActorKind.Player, member.GetId(), string.Empty);
            ActorProfileComponent profile = BuildProfile(member);
            ActorRecord snapshot = null;
            bool created = false;
            bool changed = false;
            bool profileChanged = false;
            ActorEventType componentEventType = ActorEventType.ComponentUpdated;
            string componentMessage = string.Empty;

            lock (_sync)
            {
                ActorRecord record;
                if (!_records.TryGetValue(id, out record))
                {
                    record = new ActorRecord();
                    record.Id = new ActorId(id.Kind, id.LocalId, id.Domain);
                    record.LifecycleState = ActorLifecycleState.Active;
                    record.PresenceState = ResolvePresence(member);
                    record.Flags = ActorFlags.Persistent | ActorFlags.Loaded;
                    record.Origin = ActorOrigin.Core("family");
                    record.CreatedTick = _currentTick;
                    record.UpdatedTick = record.CreatedTick;
                    AddRecordLocked(record);
                    created = true;
                }
                else
                {
                    ActorFlags desiredFlags = record.Flags | ActorFlags.Persistent | ActorFlags.Loaded;
                    changed = UpdateRecordStateLocked(record, ActorLifecycleState.Active, ResolvePresence(member), desiredFlags, _currentTick);
                }

                BindLocked(id, CreateBinding("core.family", member.GetId().ToString(), "core", true), true);

                ActorComponentWriteResult profileResult = SetComponentLocked(id, profile, BuiltInOwner, out componentEventType, out componentMessage);
                profileChanged = ShouldPublishComponentWrite(profileResult, componentMessage);
                snapshot = record.Clone();
            }

            if (profileChanged)
                Publish(componentEventType, BuiltInOwner, id, profile.ComponentId, componentMessage);

            if (created)
            {
                RaiseActorCreated(snapshot);
                Publish(ActorEventType.ActorCreated, BuiltInOwner, id, null, "Family actor discovered");
            }
            else if (changed)
            {
                RaiseActorStateChanged(snapshot);
                Publish(ActorEventType.ActorStateChanged, BuiltInOwner, id, null, "Family actor refreshed");
            }

            return created || changed || profileChanged;
        }

        private bool UpsertVisitorActor(NpcVisitor visitor)
        {
            ActorId id = new ActorId(ActorKind.Visitor, visitor.npcId, string.Empty);
            ActorProfileComponent profile = BuildProfile(visitor);
            ActorRecord snapshot = null;
            bool created = false;
            bool changed = false;
            bool profileChanged = false;
            ActorEventType componentEventType = ActorEventType.ComponentUpdated;
            string componentMessage = string.Empty;

            lock (_sync)
            {
                ActorRecord record;
                if (!_records.TryGetValue(id, out record))
                {
                    record = new ActorRecord();
                    record.Id = new ActorId(id.Kind, id.LocalId, id.Domain);
                    record.LifecycleState = ActorLifecycleState.Active;
                    record.PresenceState = ResolvePresence(visitor);
                    record.Flags = ActorFlags.Loaded;
                    record.Origin = ActorOrigin.Core("visitor");
                    record.CreatedTick = _currentTick;
                    record.UpdatedTick = record.CreatedTick;
                    AddRecordLocked(record);
                    created = true;
                }
                else
                {
                    ActorFlags desiredFlags = record.Flags | ActorFlags.Loaded;
                    changed = UpdateRecordStateLocked(record, ActorLifecycleState.Active, ResolvePresence(visitor), desiredFlags, _currentTick);
                }

                BindLocked(id, CreateBinding("core.visitor", visitor.npcId.ToString(), "core", true), true);

                ActorComponentWriteResult profileResult = SetComponentLocked(id, profile, BuiltInOwner, out componentEventType, out componentMessage);
                profileChanged = ShouldPublishComponentWrite(profileResult, componentMessage);
                snapshot = record.Clone();
            }

            if (profileChanged)
                Publish(componentEventType, BuiltInOwner, id, profile.ComponentId, componentMessage);

            if (created)
            {
                RaiseActorCreated(snapshot);
                Publish(ActorEventType.ActorCreated, BuiltInOwner, id, null, "Visitor actor discovered");
            }
            else if (changed)
            {
                RaiseActorStateChanged(snapshot);
                Publish(ActorEventType.ActorStateChanged, BuiltInOwner, id, null, "Visitor actor refreshed");
            }

            return created || changed || profileChanged;
        }
    }
}
