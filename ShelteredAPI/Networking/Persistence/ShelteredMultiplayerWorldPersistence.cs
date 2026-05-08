using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Networking;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;
using ShelteredAPI.Persistence;

namespace ShelteredAPI.Networking.Persistence
{
    internal sealed class ShelteredMultiplayerWorldPersistence : ISaveable
    {
        private static readonly ShelteredMultiplayerWorldPersistence _instance =
            new ShelteredMultiplayerWorldPersistence(
                ShelteredMultiplayerSessionCoordinator.Instance,
                ShelteredMapEntities.Registry,
                ShelteredWorldEvents.Journal,
                ShelteredExpeditionTravelHookService.Instance.Registry);

        private readonly ShelteredMultiplayerSessionCoordinator _coordinator;
        private readonly IShelteredMapEntityRegistry _mapEntities;
        private readonly IShelteredWorldEventJournal _worldEvents;
        private readonly IShelteredTravelStateRegistry _travelStates;
        private string _snapshotPayload = string.Empty;
        private string _lastError = string.Empty;

        internal ShelteredMultiplayerWorldPersistence(
            ShelteredMultiplayerSessionCoordinator coordinator,
            IShelteredMapEntityRegistry mapEntities,
            IShelteredWorldEventJournal worldEvents,
            IShelteredTravelStateRegistry travelStates)
        {
            _coordinator = coordinator ?? ShelteredMultiplayerSessionCoordinator.Instance;
            _mapEntities = mapEntities;
            _worldEvents = worldEvents;
            _travelStates = travelStates;
        }

        public static ShelteredMultiplayerWorldPersistence Instance
        {
            get { return _instance; }
        }

        public string LastError
        {
            get { return _lastError; }
        }

        public void EnsureRegistered()
        {
            ModPersistence.Register(this);
        }

        public ShelteredMultiplayerWorldSnapshot Capture(string reason)
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            ShelteredMultiplayerWorldSnapshot snapshot = new ShelteredMultiplayerWorldSnapshot();
            snapshot.SessionId = context != null ? context.SessionId : string.Empty;
            snapshot.MasterSeed = ModRandom.CurrentSeed;
            snapshot.WorldTick = context != null ? context.WorldTick : 0;
            snapshot.CompatibilityHash = ShelteredMultiplayerPersistenceKeys.CompatibilityHashUnknown;

            ShelteredWorldEventReplayCursor cursor = ShelteredWorldEvents.GetReplayCursor("persistence");
            snapshot.EventJournalCursorTick = cursor.LastAppliedTick;
            snapshot.EventJournalCursorEventId = cursor.LastAppliedEventId;

            if (context != null)
            {
                ShelteredMultiplayerBunkerAssignmentRecord[] assignments = context.GetBunkerAssignmentSnapshot();
                for (int i = 0; i < assignments.Length; i++)
                    snapshot.BunkerAssignments.Add(ShelteredMultiplayerSnapshotBunkerAssignment.FromRecord(assignments[i]));
            }

            if (_mapEntities != null)
            {
                IList<ShelteredMapEntity> entities = _mapEntities.GetAll();
                for (int i = 0; i < entities.Count; i++)
                    snapshot.MapEntities.Add(ShelteredMultiplayerSnapshotMapEntity.FromEntity(entities[i]));
            }

            if (_travelStates != null)
            {
                IList<ShelteredTravelState> active = _travelStates.GetActive();
                for (int i = 0; i < active.Count; i++)
                    snapshot.ActiveTravel.Add(FromTravelState(active[i]));
            }

            if (_worldEvents != null)
            {
                IList<ShelteredWorldEventRecord> retained = _worldEvents.GetSince(0);
                for (int i = 0; i < retained.Count; i++)
                    snapshot.RetainedEvents.Add(ShelteredMultiplayerSnapshotWorldEvent.FromRecord(retained[i]));
            }

            _snapshotPayload = snapshot.ToXml();
            _lastError = string.Empty;
            return snapshot;
        }

        public bool TryLoadPersisted(out ShelteredMultiplayerWorldSnapshot snapshot, out string error)
        {
            return ShelteredMultiplayerWorldSnapshot.TryFromXml(_snapshotPayload, out snapshot, out error);
        }

        public bool Apply(ShelteredMultiplayerWorldSnapshot snapshot, string reason, out string error)
        {
            error = string.Empty;
            if (snapshot == null || !snapshot.IsUsable)
            {
                error = "Cannot apply an empty or incomplete multiplayer world snapshot.";
                _lastError = error;
                return false;
            }

            try
            {
                ShelteredMultiplayerSessionContext context = _coordinator.Context;
                if (context != null && context.IsMultiplayerActive
                    && !string.Equals(context.SessionId, snapshot.SessionId, StringComparison.Ordinal))
                {
                    error = "Snapshot session id does not match the active multiplayer session.";
                    _lastError = error;
                    return false;
                }

                if (_mapEntities != null)
                {
                    _mapEntities.Clear("snapshot-apply:" + (reason ?? string.Empty));
                    for (int i = 0; i < snapshot.MapEntities.Count; i++)
                        _mapEntities.Upsert(snapshot.MapEntities[i].ToEntity());
                }

                if (_worldEvents != null)
                {
                    _worldEvents.Clear("snapshot-apply:" + (reason ?? string.Empty));
                    for (int i = 0; i < snapshot.RetainedEvents.Count; i++)
                        _worldEvents.Append(snapshot.RetainedEvents[i].ToRecord());
                }

                ApplyTravelSnapshot(snapshot);

                if (context != null && context.IsMultiplayerActive)
                    _coordinator.SetWorldTick(snapshot.WorldTick, context.WorldDeltaSeconds, "snapshot-apply:" + (reason ?? string.Empty));

                _snapshotPayload = snapshot.ToXml();
                _lastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = "Failed to apply multiplayer world snapshot: " + ex.Message;
                _lastError = error;
                return false;
            }
        }

        public bool IsReadyForLoad()
        {
            return true;
        }

        public bool IsRelocationEnabled()
        {
            return true;
        }

        public bool SaveLoad(SaveData data)
        {
            if (data == null)
                return false;

            data.GroupStart(ShelteredMultiplayerPersistenceKeys.SaveGroupName);
            try
            {
                if (data.isSaving)
                    Capture("save");

                string payload = _snapshotPayload ?? string.Empty;
                data.SaveLoad(ShelteredMultiplayerPersistenceKeys.SnapshotPayloadKey, ref payload);

                if (!data.isSaving)
                {
                    ShelteredMultiplayerWorldSnapshot snapshot;
                    string error;
                    if (ShelteredMultiplayerWorldSnapshot.TryFromXml(payload, out snapshot, out error))
                    {
                        _snapshotPayload = payload;
                        Apply(snapshot, "load", out error);
                    }
                    else if (!string.IsNullOrEmpty(payload))
                    {
                        _lastError = error;
                        MMLog.WarnOnce("ShelteredMultiplayerWorldPersistence.LoadMalformed", error);
                    }
                }
            }
            catch (Exception ex)
            {
                _lastError = "Multiplayer world persistence failed: " + ex.Message;
                MMLog.WarnOnce("ShelteredMultiplayerWorldPersistence.SaveLoad", _lastError);
            }
            finally
            {
                data.GroupEnd();
            }

            return true;
        }

        private void ApplyTravelSnapshot(ShelteredMultiplayerWorldSnapshot snapshot)
        {
            ShelteredTravelStateRegistry concrete = _travelStates as ShelteredTravelStateRegistry;
            if (concrete == null)
                return;

            List<ShelteredTravelState> states = new List<ShelteredTravelState>();
            for (int i = 0; i < snapshot.ActiveTravel.Count; i++)
                states.Add(ToTravelState(snapshot.ActiveTravel[i]));
            concrete.ImportSnapshot(states, "snapshot-apply");
        }

        private static ShelteredMultiplayerSnapshotTravelState FromTravelState(ShelteredTravelState state)
        {
            return new ShelteredMultiplayerSnapshotTravelState
            {
                TravelId = state != null ? state.TravelId ?? string.Empty : string.Empty,
                OwnerPlayerId = state != null ? state.OwnerPlayerId : 0,
                OwnerPeerId = state != null ? state.OwnerPeerId : NetworkDefaults.UnassignedPeerId,
                PartyId = state != null ? state.PartyId : 0,
                State = state != null ? state.State.ToString() : string.Empty,
                LastAuthoritativeTick = state != null ? state.LastAuthoritativeTick : 0,
                LastEventId = state != null ? state.LastEventId ?? string.Empty : string.Empty,
                LastPredictedGridX = state != null ? state.LastPredictedGridX : 0,
                LastPredictedGridY = state != null ? state.LastPredictedGridY : 0
            };
        }

        private static ShelteredTravelState ToTravelState(ShelteredMultiplayerSnapshotTravelState snapshot)
        {
            ShelteredTravelStateKind stateKind = ShelteredTravelStateKind.Active;
            try
            {
                if (snapshot != null && !string.IsNullOrEmpty(snapshot.State))
                    stateKind = (ShelteredTravelStateKind)Enum.Parse(typeof(ShelteredTravelStateKind), snapshot.State, false);
            }
            catch
            {
                stateKind = ShelteredTravelStateKind.Active;
            }

            return new ShelteredTravelState
            {
                TravelId = snapshot != null ? snapshot.TravelId ?? string.Empty : string.Empty,
                OwnerPlayerId = snapshot != null ? snapshot.OwnerPlayerId : 0,
                OwnerPeerId = snapshot != null && snapshot.OwnerPeerId >= 0 && snapshot.OwnerPeerId <= byte.MaxValue
                    ? (byte)snapshot.OwnerPeerId
                    : NetworkDefaults.UnassignedPeerId,
                PartyId = snapshot != null ? snapshot.PartyId : 0,
                State = stateKind,
                LastAuthoritativeTick = snapshot != null ? snapshot.LastAuthoritativeTick : 0,
                LastEventId = snapshot != null ? snapshot.LastEventId ?? string.Empty : string.Empty,
                LastPredictedGridX = snapshot != null ? snapshot.LastPredictedGridX : 0,
                LastPredictedGridY = snapshot != null ? snapshot.LastPredictedGridY : 0
            };
        }
    }
}
