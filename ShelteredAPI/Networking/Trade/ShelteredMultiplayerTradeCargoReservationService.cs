using System;
using System.Collections.Generic;
using ModAPI.Actors;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Trade
{
    internal sealed class ShelteredTradeCargoReservation
    {
        public ShelteredTradeCargoReservation()
        {
            TradeId = string.Empty;
            SourceOwnerId = string.Empty;
            TargetOwnerId = string.Empty;
            SourceStoreId = string.Empty;
            ItemId = string.Empty;
            ReservationId = string.Empty;
            AssignmentId = string.Empty;
        }

        public string TradeId;
        public string SourceOwnerId;
        public string TargetOwnerId;
        public string SourceStoreId;
        public string ItemId;
        public int Quantity;
        public bool StoreBacked;
        public string ReservationId;
        public string AssignmentId;

        public ShelteredTradeCargoReservation Copy()
        {
            return new ShelteredTradeCargoReservation
            {
                TradeId = TradeId ?? string.Empty,
                SourceOwnerId = SourceOwnerId ?? string.Empty,
                TargetOwnerId = TargetOwnerId ?? string.Empty,
                SourceStoreId = SourceStoreId ?? string.Empty,
                ItemId = ItemId ?? string.Empty,
                Quantity = Quantity,
                StoreBacked = StoreBacked,
                ReservationId = ReservationId ?? string.Empty,
                AssignmentId = AssignmentId ?? string.Empty
            };
        }
    }

    internal sealed class ShelteredTradeCargoReservationResult
    {
        private readonly List<ShelteredTradeCargoReservation> _reservations;

        private ShelteredTradeCargoReservationResult(
            bool success,
            string errorMessage,
            List<ShelteredTradeCargoReservation> reservations)
        {
            Success = success;
            ErrorMessage = errorMessage ?? string.Empty;
            _reservations = reservations ?? new List<ShelteredTradeCargoReservation>();
        }

        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public IList<ShelteredTradeCargoReservation> Reservations { get { return _reservations.AsReadOnly(); } }

        public static ShelteredTradeCargoReservationResult Ok(List<ShelteredTradeCargoReservation> reservations)
        {
            return new ShelteredTradeCargoReservationResult(true, string.Empty, reservations);
        }

        public static ShelteredTradeCargoReservationResult Failed(string errorMessage)
        {
            return new ShelteredTradeCargoReservationResult(false, errorMessage, new List<ShelteredTradeCargoReservation>());
        }
    }

    internal sealed class ShelteredMultiplayerTradeCargoReservationService
    {
        private readonly Func<string, IItemStore> _resolveOwnerStore;
        private readonly ICharacterItemAssignmentService _assignments;
        private readonly Dictionary<string, List<ShelteredTradeCargoReservation>> _reservationsByTrade =
            new Dictionary<string, List<ShelteredTradeCargoReservation>>(StringComparer.Ordinal);

        public ShelteredMultiplayerTradeCargoReservationService(Func<string, IItemStore> resolveOwnerStore)
            : this(resolveOwnerStore, ShelteredCharacterItems.Service)
        {
        }

        internal ShelteredMultiplayerTradeCargoReservationService(
            Func<string, IItemStore> resolveOwnerStore,
            ICharacterItemAssignmentService assignments)
        {
            _resolveOwnerStore = resolveOwnerStore;
            _assignments = assignments;
        }

        public ShelteredTradeCargoReservationResult Reserve(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            ShelteredTradeCargoValidationResult validation =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoAvailable(tradeEvent, _resolveOwnerStore);
            if (!validation.Success)
                return ShelteredTradeCargoReservationResult.Failed(validation.ErrorMessage);

            if (string.IsNullOrEmpty(tradeEvent.TradeId))
                return ShelteredTradeCargoReservationResult.Failed("Trade id is required.");

            Release(tradeEvent.TradeId);

            List<ShelteredTradeCargoReservation> reservations = new List<ShelteredTradeCargoReservation>();
            for (int i = 0; i < tradeEvent.Cargo.Count; i++)
            {
                ShelteredTradeCargoDto cargo = tradeEvent.Cargo[i];
                IItemStore source = ResolveStore(cargo.SourceOwnerId);
                if (source == null)
                {
                    Release(reservations);
                    return ShelteredTradeCargoReservationResult.Failed("Source store was not found for owner '" + cargo.SourceOwnerId + "'.");
                }

                ShelteredTradeCargoReservation reservation = ReserveLine(tradeEvent.TradeId, cargo, source);
                if (reservation == null)
                {
                    Release(reservations);
                    return ShelteredTradeCargoReservationResult.Failed("Unable to reserve cargo for item '" + cargo.ItemId + "'.");
                }

                reservations.Add(reservation);
            }

            _reservationsByTrade[tradeEvent.TradeId] = CloneList(reservations);
            return ShelteredTradeCargoReservationResult.Ok(CloneList(reservations));
        }

        public bool HasReservation(string tradeId)
        {
            if (string.IsNullOrEmpty(tradeId))
                return false;

            return _reservationsByTrade.ContainsKey(tradeId);
        }

        public IList<ShelteredTradeCargoReservation> GetReservations(string tradeId)
        {
            List<ShelteredTradeCargoReservation> reservations;
            if (string.IsNullOrEmpty(tradeId) || !_reservationsByTrade.TryGetValue(tradeId, out reservations))
                return new List<ShelteredTradeCargoReservation>();

            return CloneList(reservations);
        }

        public bool Release(string tradeId)
        {
            List<ShelteredTradeCargoReservation> reservations;
            if (string.IsNullOrEmpty(tradeId) || !_reservationsByTrade.TryGetValue(tradeId, out reservations))
                return false;

            Release(reservations);
            _reservationsByTrade.Remove(tradeId);
            return true;
        }

        public ItemTransferResult CommitToTarget(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (tradeEvent == null)
                return ItemTransferResult.Failed(null, 0, "Trade event is required");

            string targetError;
            if (!CanTargetsAccept(tradeEvent, out targetError))
                return ItemTransferResult.Failed(null, 0, targetError);

            List<ShelteredTradeCargoReservation> reservations;
            _reservationsByTrade.TryGetValue(tradeEvent.TradeId ?? string.Empty, out reservations);

            List<CommittedCargoLine> committed = new List<CommittedCargoLine>();
            for (int i = 0; i < tradeEvent.Cargo.Count; i++)
            {
                ShelteredTradeCargoDto cargo = tradeEvent.Cargo[i];
                IItemStore source = ResolveStore(cargo.SourceOwnerId);
                IItemStore target = ResolveStore(cargo.TargetOwnerId);
                if (source == null || target == null)
                    return ItemTransferResult.Failed(cargo.ItemId, cargo.Count, "Source and target stores are required");

                ShelteredTradeCargoReservation reservation = FindReservation(reservations, cargo);
                ItemTransferResult removeResult = RemoveReservedOrAvailable(source, reservation, cargo);
                if (!removeResult.Success)
                    return removeResult;

                ItemTransferResult addResult = target.Add(cargo.ItemId, cargo.Count);
                if (!addResult.Success || addResult.Moved != cargo.Count)
                {
                    RollbackFailedAdd(source, target, cargo.ItemId, cargo.Count, addResult);
                    RollbackCommitted(committed);
                    return ItemTransferResult.Failed(cargo.ItemId, cargo.Count, addResult.ErrorMessage ?? "Target store rejected cargo");
                }

                committed.Add(new CommittedCargoLine(source, target, cargo.ItemId, cargo.Count));
                ReleaseSoftAssignment(reservation);
            }

            _reservationsByTrade.Remove(tradeEvent.TradeId ?? string.Empty);
            return ItemTransferResult.Ok(tradeEvent.TradeId, tradeEvent.Cargo.Count, tradeEvent.Cargo.Count);
        }

        private ShelteredTradeCargoReservation ReserveLine(string tradeId, ShelteredTradeCargoDto cargo, IItemStore source)
        {
            IReservableItemStore reservable = source as IReservableItemStore;
            if (reservable != null)
            {
                ItemReservationResult result = reservable.Reserve(cargo.ItemId, cargo.Count, tradeId);
                if (!result.Success)
                    return null;

                return new ShelteredTradeCargoReservation
                {
                    TradeId = tradeId,
                    SourceOwnerId = cargo.SourceOwnerId,
                    TargetOwnerId = cargo.TargetOwnerId,
                    SourceStoreId = source.StoreId,
                    ItemId = cargo.ItemId,
                    Quantity = cargo.Count,
                    StoreBacked = true,
                    ReservationId = result.ReservationId
                };
            }

            CharacterItemAssignment assignment;
            try
            {
                assignment = _assignments.Assign(
                    CreateCargoActorId(tradeId),
                    source,
                    cargo.ItemId,
                    cargo.Count,
                    CharacterItemAssignmentKind.CargoReserved,
                    CharacterItemSlot.Backpack);
            }
            catch
            {
                return null;
            }

            return new ShelteredTradeCargoReservation
            {
                TradeId = tradeId,
                SourceOwnerId = cargo.SourceOwnerId,
                TargetOwnerId = cargo.TargetOwnerId,
                SourceStoreId = source.StoreId,
                ItemId = cargo.ItemId,
                Quantity = cargo.Count,
                StoreBacked = false,
                AssignmentId = assignment != null ? assignment.AssignmentId : string.Empty
            };
        }

        private ItemTransferResult RemoveReservedOrAvailable(
            IItemStore source,
            ShelteredTradeCargoReservation reservation,
            ShelteredTradeCargoDto cargo)
        {
            if (reservation != null && reservation.StoreBacked)
            {
                IReservableItemStore reservable = source as IReservableItemStore;
                if (reservable == null)
                    return ItemTransferResult.Failed(cargo.ItemId, cargo.Count, "Reserved source no longer supports reservations");

                return reservable.CommitReservation(reservation.ReservationId);
            }

            return source.Remove(cargo.ItemId, cargo.Count);
        }

        private bool CanTargetsAccept(ShelteredMultiplayerTradeEvent tradeEvent, out string error)
        {
            error = string.Empty;
            for (int i = 0; i < tradeEvent.Cargo.Count; i++)
            {
                ShelteredTradeCargoDto cargo = tradeEvent.Cargo[i];
                IItemStore target = ResolveStore(cargo.TargetOwnerId);
                if (target == null)
                {
                    error = "Target store was not found for owner '" + cargo.TargetOwnerId + "'.";
                    return false;
                }

                if (!target.CanAdd(cargo.ItemId, cargo.Count))
                {
                    error = "Target store cannot accept cargo for item '" + cargo.ItemId + "'.";
                    return false;
                }
            }

            List<TargetCapacityRequest> capacityRequests = new List<TargetCapacityRequest>();
            for (int i = 0; i < tradeEvent.Cargo.Count; i++)
            {
                ShelteredTradeCargoDto cargo = tradeEvent.Cargo[i];
                IItemStore target = ResolveStore(cargo.TargetOwnerId);
                TargetCapacityRequest request = FindCapacityRequest(capacityRequests, target);
                if (request == null)
                {
                    request = new TargetCapacityRequest(target);
                    capacityRequests.Add(request);
                }

                request.Quantity += cargo.Count;
            }

            for (int i = 0; i < capacityRequests.Count; i++)
            {
                TargetCapacityRequest request = capacityRequests[i];
                if (request.Store != null
                    && request.Store.Capacity > 0
                    && request.Store.Used + request.Quantity > request.Store.Capacity)
                {
                    error = "Target store cannot accept all cargo without exceeding capacity.";
                    return false;
                }
            }

            return true;
        }

        private void Release(List<ShelteredTradeCargoReservation> reservations)
        {
            for (int i = 0; reservations != null && i < reservations.Count; i++)
            {
                ShelteredTradeCargoReservation reservation = reservations[i];
                if (reservation == null)
                    continue;

                if (reservation.StoreBacked)
                {
                    IItemStore source = ResolveStore(reservation.SourceOwnerId);
                    IReservableItemStore reservable = source as IReservableItemStore;
                    if (reservable != null)
                        reservable.CancelReservation(reservation.ReservationId);
                }
                else
                {
                    ReleaseSoftAssignment(reservation);
                }
            }
        }

        private void ReleaseSoftAssignment(ShelteredTradeCargoReservation reservation)
        {
            if (reservation != null && !string.IsNullOrEmpty(reservation.AssignmentId))
                _assignments.Unassign(reservation.AssignmentId);
        }

        private IItemStore ResolveStore(string ownerId)
        {
            if (_resolveOwnerStore == null)
                return null;

            try
            {
                return _resolveOwnerStore(ownerId);
            }
            catch
            {
                return null;
            }
        }

        private static ShelteredTradeCargoReservation FindReservation(
            List<ShelteredTradeCargoReservation> reservations,
            ShelteredTradeCargoDto cargo)
        {
            for (int i = 0; reservations != null && i < reservations.Count; i++)
            {
                ShelteredTradeCargoReservation reservation = reservations[i];
                if (reservation != null
                    && string.Equals(reservation.SourceOwnerId, cargo.SourceOwnerId, StringComparison.Ordinal)
                    && string.Equals(reservation.TargetOwnerId, cargo.TargetOwnerId, StringComparison.Ordinal)
                    && string.Equals(reservation.ItemId, cargo.ItemId, StringComparison.Ordinal)
                    && reservation.Quantity == cargo.Count)
                {
                    return reservation;
                }
            }

            return null;
        }

        private static List<ShelteredTradeCargoReservation> CloneList(List<ShelteredTradeCargoReservation> reservations)
        {
            List<ShelteredTradeCargoReservation> copy = new List<ShelteredTradeCargoReservation>();
            for (int i = 0; reservations != null && i < reservations.Count; i++)
            {
                if (reservations[i] != null)
                    copy.Add(reservations[i].Copy());
            }

            return copy;
        }

        private static ActorId CreateCargoActorId(string tradeId)
        {
            int hash = (tradeId ?? string.Empty).GetHashCode();
            if (hash == int.MinValue)
                hash = 0;
            return new ActorId(ActorKind.Synthetic, Math.Abs(hash), "trade-cargo");
        }

        private static void RollbackFailedAdd(IItemStore source, IItemStore target, string itemId, int removedCount, ItemTransferResult addResult)
        {
            int targetMoved = addResult != null ? Math.Max(0, addResult.Moved) : 0;
            if (targetMoved > 0)
                target.Remove(itemId, targetMoved);

            if (removedCount > 0)
                source.Add(itemId, removedCount);
        }

        private static void RollbackCommitted(List<CommittedCargoLine> committed)
        {
            for (int i = committed != null ? committed.Count - 1 : -1; i >= 0; i--)
            {
                CommittedCargoLine line = committed[i];
                if (line == null || line.Source == null || line.Target == null)
                    continue;

                ItemTransferResult removed = line.Target.Remove(line.ItemId, line.Quantity);
                if (removed.Success && removed.Moved > 0)
                    line.Source.Add(line.ItemId, removed.Moved);
            }
        }

        private static TargetCapacityRequest FindCapacityRequest(List<TargetCapacityRequest> requests, IItemStore store)
        {
            for (int i = 0; requests != null && i < requests.Count; i++)
            {
                if (object.ReferenceEquals(requests[i].Store, store))
                    return requests[i];
            }

            return null;
        }

        private sealed class CommittedCargoLine
        {
            public CommittedCargoLine(IItemStore source, IItemStore target, string itemId, int quantity)
            {
                Source = source;
                Target = target;
                ItemId = itemId ?? string.Empty;
                Quantity = quantity;
            }

            public readonly IItemStore Source;
            public readonly IItemStore Target;
            public readonly string ItemId;
            public readonly int Quantity;
        }

        private sealed class TargetCapacityRequest
        {
            public TargetCapacityRequest(IItemStore store)
            {
                Store = store;
            }

            public readonly IItemStore Store;
            public int Quantity;
        }
    }
}
