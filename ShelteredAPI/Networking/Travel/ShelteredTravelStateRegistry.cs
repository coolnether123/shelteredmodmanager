using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Travel
{
    internal sealed class ShelteredTravelStateRegistry : IShelteredTravelStateRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ShelteredTravelState> _states =
            new Dictionary<string, ShelteredTravelState>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedEventIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly IShelteredMapEntityRegistry _mapEntities;

        public ShelteredTravelStateRegistry()
            : this(ShelteredMapEntities.Registry)
        {
        }

        internal ShelteredTravelStateRegistry(IShelteredMapEntityRegistry mapEntities)
        {
            _mapEntities = mapEntities;
        }

        public ShelteredTravelApplyResult ApplyTravelStarted(ShelteredTravelStartedEvent started, string eventId)
        {
            if (started == null || string.IsNullOrEmpty(started.TravelId))
                return ShelteredTravelApplyResult.Ignored("missing-travel-start");
            if (IsDuplicateEvent(eventId))
                return ShelteredTravelApplyResult.IgnoredDuplicate;

            ShelteredTravelState state = new ShelteredTravelState();
            state.TravelId = started.TravelId;
            state.OwnerPlayerId = started.OwnerPlayerId;
            state.OwnerPeerId = started.OwnerPeerId;
            state.PartyId = started.PartyId;
            state.State = ShelteredTravelStateKind.Active;
            state.LastAuthoritativeTick = started.StartTick;
            state.LastEventId = NormalizeEventId(eventId);
            state.StartedEvent = started.Copy();
            state.LastPredictedGridX = started.StartGridX;
            state.LastPredictedGridY = started.StartGridY;

            lock (_sync)
            {
                MarkEventApplied(eventId);
                _states[started.TravelId] = state;
            }

            UpsertMapEntity(state, ShelteredTravelPrediction.Predict(state.StartedEvent, state.LastAuthoritativeTick));
            return ShelteredTravelApplyResult.Applied;
        }

        public ShelteredTravelApplyResult ApplyTravelCorrected(ShelteredTravelCorrectedEvent corrected, string eventId)
        {
            return ApplyTravelCorrected(corrected, eventId, false);
        }

        public ShelteredTravelApplyResult ApplyTravelCorrected(ShelteredTravelCorrectedEvent corrected, string eventId, bool force)
        {
            if (corrected == null || string.IsNullOrEmpty(corrected.TravelId))
                return ShelteredTravelApplyResult.Ignored("missing-travel-correction");
            if (IsDuplicateEvent(eventId))
                return ShelteredTravelApplyResult.IgnoredDuplicate;

            ShelteredTravelState updated;
            lock (_sync)
            {
                ShelteredTravelState state;
                _states.TryGetValue(corrected.TravelId, out state);

                if (!force && state != null && state.LatestCorrection != null
                    && corrected.CorrectionTick < state.LatestCorrection.CorrectionTick)
                {
                    MarkEventApplied(eventId);
                    return ShelteredTravelApplyResult.IgnoredOutOfOrder;
                }

                if (!force && state != null && corrected.CorrectionTick < state.LastAuthoritativeTick)
                {
                    MarkEventApplied(eventId);
                    return ShelteredTravelApplyResult.IgnoredOutOfOrder;
                }

                if (state == null)
                    state = CreateStateFromCorrection(corrected);
                else
                    state = state.Copy();

                state.State = IsTerminalCorrection(corrected)
                    ? ShelteredTravelStateKind.Interrupted
                    : ShelteredTravelStateKind.Corrected;
                state.LastAuthoritativeTick = corrected.CorrectionTick;
                state.LastEventId = NormalizeEventId(eventId);
                state.LatestCorrection = corrected.Copy();
                state.StartedEvent = ShelteredTravelPrediction.CreateCorrectedStart(state.StartedEvent, corrected);
                state.LastPredictedGridX = corrected.CorrectedGridX;
                state.LastPredictedGridY = corrected.CorrectedGridY;
                _states[corrected.TravelId] = state;
                MarkEventApplied(eventId);
                updated = state.Copy();
            }

            UpsertMapEntity(updated, ShelteredTravelPrediction.Predict(updated.StartedEvent, updated.LastAuthoritativeTick));
            return ShelteredTravelApplyResult.Applied;
        }

        public ShelteredTravelApplyResult ApplyTravelArrived(ShelteredTravelArrivedEvent arrived, string eventId)
        {
            if (arrived == null || string.IsNullOrEmpty(arrived.TravelId))
                return ShelteredTravelApplyResult.Ignored("missing-travel-arrival");
            if (IsDuplicateEvent(eventId))
                return ShelteredTravelApplyResult.IgnoredDuplicate;

            ShelteredTravelState updated;
            lock (_sync)
            {
                ShelteredTravelState state;
                _states.TryGetValue(arrived.TravelId, out state);
                if (state == null)
                    state = new ShelteredTravelState { TravelId = arrived.TravelId };
                else
                    state = state.Copy();

                state.State = IsCancelledArrival(arrived) ? ShelteredTravelStateKind.Cancelled : ShelteredTravelStateKind.Arrived;
                state.LastAuthoritativeTick = arrived.ArrivalTick;
                state.LastEventId = NormalizeEventId(eventId);
                state.ArrivalEvent = arrived.Copy();
                state.LastPredictedGridX = arrived.ArrivalGridX;
                state.LastPredictedGridY = arrived.ArrivalGridY;
                _states[arrived.TravelId] = state;
                MarkEventApplied(eventId);
                updated = state.Copy();
            }

            UpsertMapEntity(updated, CreateTerminalPrediction(updated, arrived));
            return ShelteredTravelApplyResult.Applied;
        }

        public ShelteredTravelPredictionResult Predict(string travelId, long worldTick)
        {
            ShelteredTravelState state;
            lock (_sync)
            {
                if (!_states.TryGetValue(travelId ?? string.Empty, out state) || state.StartedEvent == null)
                    return null;
                state = state.Copy();
            }

            ShelteredTravelPredictionResult prediction = ShelteredTravelPrediction.Predict(state.StartedEvent, worldTick);
            ShelteredTravelState entityState = state.Copy();
            lock (_sync)
            {
                ShelteredTravelState current;
                if (_states.TryGetValue(state.TravelId, out current))
                {
                    current.LastPredictedGridX = prediction.GridX;
                    current.LastPredictedGridY = prediction.GridY;
                    if (current.State == ShelteredTravelStateKind.Corrected)
                        current.State = ShelteredTravelStateKind.Active;
                    entityState = current.Copy();
                }
            }

            UpsertMapEntity(entityState, prediction);
            return prediction;
        }

        public IList<ShelteredTravelState> GetActive()
        {
            List<ShelteredTravelState> active = new List<ShelteredTravelState>();
            lock (_sync)
            {
                foreach (ShelteredTravelState state in _states.Values)
                {
                    if (state != null && IsActiveState(state.State))
                        active.Add(state.Copy());
                }
            }

            active.Sort(CompareStates);
            return active;
        }

        internal void ImportSnapshot(IList<ShelteredTravelState> states, string reason)
        {
            lock (_sync)
            {
                _states.Clear();
                _appliedEventIds.Clear();
                if (states != null)
                {
                    for (int i = 0; i < states.Count; i++)
                    {
                        ShelteredTravelState state = states[i];
                        if (state == null || string.IsNullOrEmpty(state.TravelId))
                            continue;

                        _states[state.TravelId] = state.Copy();
                        if (!string.IsNullOrEmpty(state.LastEventId))
                            _appliedEventIds.Add(state.LastEventId);
                    }
                }
            }

            if (states != null)
            {
                for (int i = 0; i < states.Count; i++)
                {
                    ShelteredTravelState state = states[i];
                    if (state != null)
                        UpsertMapEntity(state, CreateSnapshotPrediction(state));
                }
            }
        }

        public bool Remove(string travelId)
        {
            string id = travelId ?? string.Empty;
            if (id.Length == 0)
                return false;

            bool removed;
            lock (_sync)
            {
                removed = _states.Remove(id);
            }

            if (removed && _mapEntities != null)
                _mapEntities.Remove(CreateMapEntityId(id));
            return removed;
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                _states.Clear();
                _appliedEventIds.Clear();
            }
        }

        private bool IsDuplicateEvent(string eventId)
        {
            string normalized = NormalizeEventId(eventId);
            if (normalized.Length == 0)
                return false;

            lock (_sync)
            {
                return _appliedEventIds.Contains(normalized);
            }
        }

        private void MarkEventApplied(string eventId)
        {
            string normalized = NormalizeEventId(eventId);
            if (normalized.Length > 0)
                _appliedEventIds.Add(normalized);
        }

        private void UpsertMapEntity(ShelteredTravelState state, ShelteredTravelPredictionResult prediction)
        {
            if (_mapEntities == null || state == null || state.TravelId.Length == 0)
                return;

            int gridX = prediction != null ? prediction.GridX : state.LastPredictedGridX;
            int gridY = prediction != null ? prediction.GridY : state.LastPredictedGridY;
            long updatedWorldTick = prediction != null ? prediction.CurrentTick : state.LastAuthoritativeTick;

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = CreateMapEntityId(state.TravelId);
            entity.Kind = ResolveMapEntityKind(state);
            entity.OwnerPlayerId = state.OwnerPlayerId;
            entity.OwnerPeerId = state.OwnerPeerId;
            entity.DisplayName = "Expedition " + state.PartyId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            entity.GridX = gridX;
            entity.GridY = gridY;
            ApplyPredictedWorldPosition(entity, prediction);
            entity.IsOnline = IsActiveState(state.State);
            entity.IsVisible = true;
            entity.State = state.State.ToString();
            entity.UpdatedWorldTick = updatedWorldTick;
            entity.PayloadJson = "{\"travelId\":\"" + EscapeJson(state.TravelId) + "\"}";
            _mapEntities.Upsert(entity);
        }

        private static ShelteredTravelPredictionResult CreateSnapshotPrediction(ShelteredTravelState state)
        {
            if (state == null)
                return null;

            if (state.StartedEvent != null)
                return ShelteredTravelPrediction.Predict(state.StartedEvent, state.LastAuthoritativeTick);

            ShelteredTravelPredictionResult prediction = new ShelteredTravelPredictionResult();
            prediction.CurrentTick = state.LastAuthoritativeTick;
            prediction.GridX = state.LastPredictedGridX;
            prediction.GridY = state.LastPredictedGridY;
            prediction.IsComplete = !IsActiveState(state.State);
            return prediction;
        }

        private static ShelteredTravelPredictionResult CreateTerminalPrediction(
            ShelteredTravelState state,
            ShelteredTravelArrivedEvent arrived)
        {
            if (arrived == null)
                return CreateSnapshotPrediction(state);

            ShelteredTravelPredictionResult prediction = new ShelteredTravelPredictionResult();
            prediction.CurrentTick = arrived.ArrivalTick;
            prediction.ExpectedArrivalTick = arrived.ArrivalTick;
            prediction.IsComplete = true;
            prediction.Progress01 = 1f;
            prediction.GridX = arrived.ArrivalGridX;
            prediction.GridY = arrived.ArrivalGridY;
            prediction.HasWorldPosition = arrived.HasWorldPosition;
            prediction.WorldX = arrived.ArrivalWorldX;
            prediction.WorldY = arrived.ArrivalWorldY;
            return prediction;
        }

        private static void ApplyPredictedWorldPosition(
            ShelteredMapEntity entity,
            ShelteredTravelPredictionResult prediction)
        {
            if (entity == null || prediction == null)
                return;

            if (prediction.HasWorldPosition)
            {
                entity.WorldPosition = new Vector2(prediction.WorldX, prediction.WorldY);
                entity.MapPixels = ResolveMapPixels(entity.WorldPosition);
                return;
            }

            Vector2 worldPosition;
            if (TryResolveGridWorldPosition(prediction.GridX, prediction.GridY, out worldPosition))
            {
                entity.WorldPosition = worldPosition;
                entity.MapPixels = ResolveMapPixels(worldPosition);
            }
        }

        private static bool TryResolveGridWorldPosition(int gridX, int gridY, out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;
            if (ExpeditionMap.Instance == null)
                return false;

            worldPosition = ExpeditionMap.Instance.GetGridRefCentreWorldPos(new ExpeditionMap.GridRef(gridX, gridY));
            return true;
        }

        private static Vector3 ResolveMapPixels(Vector2 worldPosition)
        {
            ExplorationManager manager = ExplorationManager.Instance;
            if (manager == null)
                return Vector3.zero;

            return new Vector3(
                manager.WorldToMapPixelsX(worldPosition.x),
                manager.WorldToMapPixelsY(worldPosition.y),
                0f);
        }

        private static ShelteredTravelState CreateStateFromCorrection(ShelteredTravelCorrectedEvent corrected)
        {
            return new ShelteredTravelState
            {
                TravelId = corrected.TravelId ?? string.Empty,
                State = ShelteredTravelStateKind.Corrected,
                LastPredictedGridX = corrected.CorrectedGridX,
                LastPredictedGridY = corrected.CorrectedGridY
            };
        }

        private static ShelteredMapEntityKind ResolveMapEntityKind(ShelteredTravelState state)
        {
            return ShelteredMapEntityKind.Expedition;
        }

        private static bool IsActiveState(ShelteredTravelStateKind state)
        {
            return state == ShelteredTravelStateKind.Active || state == ShelteredTravelStateKind.Corrected;
        }

        private static bool IsTerminalCorrection(ShelteredTravelCorrectedEvent corrected)
        {
            string reason = corrected != null ? corrected.Reason ?? string.Empty : string.Empty;
            return string.Equals(reason, ShelteredTravelCorrectionReasons.Stopped, StringComparison.Ordinal)
                || string.Equals(reason, ShelteredTravelCorrectionReasons.Ambush, StringComparison.Ordinal);
        }

        private static bool IsCancelledArrival(ShelteredTravelArrivedEvent arrived)
        {
            return arrived != null
                && string.Equals(arrived.ResultKind ?? string.Empty, ShelteredTravelArrivalKinds.Cancelled, StringComparison.Ordinal);
        }

        internal static string CreateMapEntityId(string travelId)
        {
            return "mapentity:travel:" + (travelId ?? string.Empty);
        }

        private static string NormalizeEventId(string eventId)
        {
            return (eventId ?? string.Empty).Trim();
        }

        private static int CompareStates(ShelteredTravelState left, ShelteredTravelState right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return string.Compare(left.TravelId, right.TravelId, StringComparison.Ordinal);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
