using System;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Trade
{
    internal sealed class ShelteredMultiplayerTradeCaravanService
    {
        private readonly ShelteredMultiplayerTradeStateRegistry _states;
        private readonly ShelteredMultiplayerTradeCargoReservationService _reservations;
        private readonly IShelteredMapEntityRegistry _mapEntities;

        public ShelteredMultiplayerTradeCaravanService(
            ShelteredMultiplayerTradeStateRegistry states,
            ShelteredMultiplayerTradeCargoReservationService reservations)
            : this(states, reservations, ShelteredMapEntities.Registry)
        {
        }

        internal ShelteredMultiplayerTradeCaravanService(
            ShelteredMultiplayerTradeStateRegistry states,
            ShelteredMultiplayerTradeCargoReservationService reservations,
            IShelteredMapEntityRegistry mapEntities)
        {
            _states = states;
            _reservations = reservations;
            _mapEntities = mapEntities;
        }

        public ShelteredTravelStartedEvent LaunchCaravan(
            ShelteredMultiplayerTradeEvent acceptedTrade,
            int startGridX,
            int startGridY,
            int destinationGridX,
            int destinationGridY,
            float worldUnitsPerTick,
            long startTick,
            long expectedArrivalTick)
        {
            if (acceptedTrade == null)
                throw new ArgumentNullException("acceptedTrade");
            if (string.IsNullOrEmpty(acceptedTrade.TradeId))
                throw new ArgumentException("Trade id is required.", "acceptedTrade");

            ApplyState(acceptedTrade, ShelteredNetworkEventKinds.TradeOfferAccepted, startTick);
            ApplyState(acceptedTrade, ShelteredNetworkEventKinds.TradeCargoReserved, startTick);

            ShelteredTravelStartedEvent started = new ShelteredTravelStartedEvent();
            started.TravelId = CreateTravelId(acceptedTrade.TradeId);
            started.OwnerPlayerId = ParsePlayerId(acceptedTrade.SourceOwnerId);
            started.OwnerPeerId = 0;
            started.PartyId = 0;
            started.StartTick = startTick;
            started.StartGridX = startGridX;
            started.StartGridY = startGridY;
            started.DestinationGridX = destinationGridX;
            started.DestinationGridY = destinationGridY;
            started.WorldUnitsPerTick = worldUnitsPerTick;
            started.ExpectedArrivalTick = expectedArrivalTick;
            started.SeedStreamName = "MultiplayerSync.Trade." + NormalizeIdPart(acceptedTrade.TradeId);

            ShelteredMultiplayerTradeEvent launched = ApplyState(
                acceptedTrade,
                ShelteredNetworkEventKinds.TradeCaravanLaunched,
                startTick);
            UpsertCaravanEntity(launched, started, startGridX, startGridY, "launched", true);
            AppendTravelStarted(started);
            return started;
        }

        public ShelteredMultiplayerTradeEvent Arrive(
            ShelteredMultiplayerTradeEvent tradeEvent,
            ShelteredTravelStartedEvent started,
            long arrivalTick)
        {
            if (tradeEvent == null)
                throw new ArgumentNullException("tradeEvent");

            ShelteredMultiplayerTradeEvent arrived = ApplyState(
                tradeEvent,
                ShelteredNetworkEventKinds.TradeCaravanArrived,
                arrivalTick);

            if (started != null)
            {
                UpsertCaravanEntity(
                    arrived,
                    started,
                    started.DestinationGridX,
                    started.DestinationGridY,
                    "arrived",
                    true);
                AppendTravelArrived(started, arrivalTick, ShelteredNetworkEventKinds.TradeCaravanArrived);
            }

            return arrived;
        }

        public ItemTransferResult Complete(ShelteredMultiplayerTradeEvent tradeEvent, long completionTick)
        {
            if (tradeEvent == null)
                return ItemTransferResult.Failed(null, 0, "Trade event is required");

            ShelteredMultiplayerTradeState existing;
            if (_states != null && _states.TryGet(tradeEvent.TradeId, out existing)
                && existing.State == ShelteredMultiplayerTradeStateKind.Completed)
            {
                return ItemTransferResult.Ok(tradeEvent.TradeId, 0, 0);
            }

            if (_reservations == null)
                return ItemTransferResult.Failed(tradeEvent.TradeId, 0, "Cargo reservation service is required");

            ItemTransferResult transfer = _reservations.CommitToTarget(tradeEvent);
            if (!transfer.Success)
            {
                Fail(tradeEvent, completionTick, transfer.ErrorMessage);
                return transfer;
            }

            ShelteredMultiplayerTradeEvent completed = ApplyState(
                tradeEvent,
                ShelteredNetworkEventKinds.TradeCompleted,
                completionTick);
            UpsertTerminalCaravanEntity(completed, "completed", completionTick);
            return transfer;
        }

        public ShelteredMultiplayerTradeEvent Cancel(ShelteredMultiplayerTradeEvent tradeEvent, long tick, string reason)
        {
            if (tradeEvent == null)
                throw new ArgumentNullException("tradeEvent");

            if (_reservations != null)
                _reservations.Release(tradeEvent.TradeId);

            ShelteredMultiplayerTradeEvent cancelled = ApplyState(
                tradeEvent,
                ShelteredNetworkEventKinds.TradeCancelled,
                tick,
                reason);
            UpsertTerminalCaravanEntity(cancelled, "cancelled", tick);
            return cancelled;
        }

        public ShelteredMultiplayerTradeEvent Fail(ShelteredMultiplayerTradeEvent tradeEvent, long tick, string reason)
        {
            if (tradeEvent == null)
                throw new ArgumentNullException("tradeEvent");

            if (_reservations != null)
                _reservations.Release(tradeEvent.TradeId);

            ShelteredMultiplayerTradeEvent failed = ApplyState(
                tradeEvent,
                ShelteredNetworkEventKinds.TradeFailed,
                tick,
                reason);
            UpsertTerminalCaravanEntity(failed, "failed", tick);
            return failed;
        }

        private ShelteredMultiplayerTradeEvent ApplyState(
            ShelteredMultiplayerTradeEvent source,
            string eventKind,
            long tick)
        {
            return ApplyState(source, eventKind, tick, string.Empty);
        }

        private ShelteredMultiplayerTradeEvent ApplyState(
            ShelteredMultiplayerTradeEvent source,
            string eventKind,
            long tick,
            string reason)
        {
            ShelteredMultiplayerTradeEvent copy = source.Copy();
            copy.EventKind = eventKind ?? string.Empty;
            copy.WorldTick = tick < 0 ? 0 : (uint)Math.Min(tick, uint.MaxValue);
            copy.EventId = CreateEventId(copy.TradeId, eventKind, tick);
            copy.CorrelationId = string.IsNullOrEmpty(copy.CorrelationId) ? copy.TradeId : copy.CorrelationId;
            copy.RejectionReason = reason ?? string.Empty;

            if (_states != null)
                _states.Apply(copy);

            return copy;
        }

        private void UpsertCaravanEntity(
            ShelteredMultiplayerTradeEvent tradeEvent,
            ShelteredTravelStartedEvent started,
            int gridX,
            int gridY,
            string state,
            bool online)
        {
            if (_mapEntities == null || tradeEvent == null)
                return;

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = CreateMapEntityId(tradeEvent.TradeId);
            entity.Kind = ShelteredMapEntityKind.TradeCaravan;
            entity.OwnerPlayerId = started != null ? started.OwnerPlayerId : ParsePlayerId(tradeEvent.SourceOwnerId);
            entity.OwnerPeerId = started != null ? started.OwnerPeerId : (byte)0;
            entity.DisplayName = "Trade Caravan";
            entity.GridX = gridX;
            entity.GridY = gridY;
            entity.IsOnline = online;
            entity.IsVisible = true;
            entity.State = state ?? string.Empty;
            entity.UpdatedWorldTick = tradeEvent.WorldTick;
            entity.PayloadJson = "{\"tradeId\":\"" + EscapeJson(tradeEvent.TradeId) + "\"}";
            _mapEntities.Upsert(entity);
        }

        private void UpsertTerminalCaravanEntity(ShelteredMultiplayerTradeEvent tradeEvent, string state, long tick)
        {
            if (_mapEntities == null || tradeEvent == null)
                return;

            ShelteredMapEntity existing = _mapEntities.Get(CreateMapEntityId(tradeEvent.TradeId));
            ShelteredTravelStartedEvent started = new ShelteredTravelStartedEvent();
            started.OwnerPlayerId = existing != null ? existing.OwnerPlayerId : ParsePlayerId(tradeEvent.SourceOwnerId);
            started.OwnerPeerId = existing != null ? existing.OwnerPeerId : (byte)0;
            UpsertCaravanEntity(
                tradeEvent,
                started,
                existing != null ? existing.GridX : 0,
                existing != null ? existing.GridY : 0,
                state,
                false);
        }

        private static void AppendTravelStarted(ShelteredTravelStartedEvent started)
        {
            if (started == null)
                return;

            ShelteredWorldEvents.AppendAuthoritative(
                ShelteredNetworkEventKinds.TravelStarted,
                started.TravelId,
                ShelteredTravelContractCodec.ToPayloadJson(started),
                started.OwnerPlayerId,
                started.OwnerPeerId);
        }

        private static void AppendTravelArrived(ShelteredTravelStartedEvent started, long arrivalTick, string resultKind)
        {
            if (started == null)
                return;

            ShelteredTravelArrivedEvent arrived = new ShelteredTravelArrivedEvent();
            arrived.TravelId = started.TravelId;
            arrived.ArrivalTick = arrivalTick;
            arrived.ArrivalGridX = started.DestinationGridX;
            arrived.ArrivalGridY = started.DestinationGridY;
            arrived.ResultKind = resultKind ?? string.Empty;
            arrived.ResultPayloadJson = string.Empty;

            ShelteredWorldEvents.AppendAuthoritative(
                ShelteredNetworkEventKinds.TravelArrived,
                arrived.TravelId,
                ShelteredTravelContractCodec.ToPayloadJson(arrived),
                started.OwnerPlayerId,
                started.OwnerPeerId);
        }

        internal static string CreateMapEntityId(string tradeId)
        {
            return "mapentity:tradecaravan:" + (tradeId ?? string.Empty);
        }

        private static string CreateTravelId(string tradeId)
        {
            return "tradecaravan:" + NormalizeIdPart(tradeId);
        }

        private static string CreateEventId(string tradeId, string eventKind, long tick)
        {
            return "tradeevent:" + NormalizeIdPart(eventKind) + ":" + NormalizeIdPart(tradeId) + ":" + tick.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int ParsePlayerId(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return 0;

            int parsed;
            return int.TryParse(ownerId, out parsed) ? parsed : 0;
        }

        private static string NormalizeIdPart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "none";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '_'
                    || c == '-')
                {
                    continue;
                }

                chars[i] = '_';
            }

            return new string(chars);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
