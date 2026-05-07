using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI.Runtime;
using UnityEngine;
using GameItemDefinition = global::ItemDefinition;

namespace ShelteredAPI.Storage
{
    internal sealed class ModItemStore : ItemStoreBase, IReservableItemStore
    {
        private readonly ModItemStoreState _state;

        internal ModItemStore(ModItemStoreState state)
        {
            _state = state;
        }

        public override string StoreId { get { return _state.StoreId; } }
        public override string DisplayName { get { return _state.DisplayName; } }
        public override ItemStoreKind Kind { get { return ItemStoreKind.Mod; } }
        public override int Capacity { get { return _state.Capacity; } }
        public override int Used { get { return _state.Used; } }

        public override ItemStoreSnapshot Snapshot()
        {
            ItemStoreSnapshot snapshot = new ItemStoreSnapshot
            {
                StoreId = StoreId,
                DisplayName = DisplayName,
                Kind = Kind,
                Capacity = Capacity,
                Used = Used
            };

            lock (_state)
            {
                foreach (KeyValuePair<string, int> pair in _state.Items)
                {
                    int available = GetAvailableCount(pair.Key);
                    if (available > 0)
                        snapshot.Items.Add(CreateItem(pair.Key, available));
                }
            }

            return snapshot;
        }

        public override int GetCount(string itemId)
        {
            lock (_state)
            {
                int count;
                return !string.IsNullOrEmpty(itemId) && _state.Items.TryGetValue(itemId, out count) ? count : 0;
            }
        }

        public override bool CanAdd(string itemId, int quantity)
        {
            lock (_state)
            {
                return !string.IsNullOrEmpty(itemId)
                    && quantity > 0
                    && (_state.Capacity <= 0 || _state.Used + quantity <= _state.Capacity);
            }
        }

        public override bool CanRemove(string itemId, int quantity)
        {
            return quantity > 0 && GetAvailableCount(itemId) >= quantity;
        }

        public override ItemTransferResult Add(string itemId, int quantity)
        {
            ItemTransferResult validation;
            if (!IsValidQuantity(itemId, quantity, out validation))
                return validation;

            lock (_state)
            {
                if (!CanAdd(itemId, quantity))
                    return ItemTransferResult.Failed(itemId, quantity, "Store capacity would be exceeded");

                int count = GetCount(itemId);
                _state.Items[itemId] = count + quantity;
                return ItemTransferResult.Ok(itemId, quantity, quantity);
            }
        }

        public override ItemTransferResult Remove(string itemId, int quantity)
        {
            ItemTransferResult validation;
            if (!IsValidQuantity(itemId, quantity, out validation))
                return validation;

            lock (_state)
            {
                if (!CanRemove(itemId, quantity))
                    return ItemTransferResult.Failed(itemId, quantity, "Store does not contain enough unreserved items");

                RemoveFromState(itemId, quantity);
                return ItemTransferResult.Ok(itemId, quantity, quantity);
            }
        }

        public ItemReservationResult Reserve(string itemId, int quantity, string ownerToken)
        {
            if (string.IsNullOrEmpty(ownerToken))
                ownerToken = "anonymous";
            if (string.IsNullOrEmpty(itemId))
                return ItemReservationResult.Failed(itemId, quantity, ownerToken, "Item ID is required");
            if (quantity <= 0)
                return ItemReservationResult.Failed(itemId, quantity, ownerToken, "Quantity must be greater than zero");

            lock (_state)
            {
                if (GetAvailableCount(itemId) < quantity)
                    return ItemReservationResult.Failed(itemId, quantity, ownerToken, "Store does not contain enough unreserved items");

                string reservationId = StoreId + ".reservation." + Guid.NewGuid().ToString("N");
                _state.Reservations[reservationId] = new ModItemReservation
                {
                    ReservationId = reservationId,
                    ItemId = itemId,
                    Quantity = quantity,
                    OwnerToken = ownerToken
                };

                return ItemReservationResult.Ok(reservationId, itemId, quantity, quantity, ownerToken);
            }
        }

        public ItemTransferResult CommitReservation(string reservationId)
        {
            if (string.IsNullOrEmpty(reservationId))
                return ItemTransferResult.Failed(null, 0, "Reservation ID is required");

            lock (_state)
            {
                ModItemReservation reservation;
                if (!_state.Reservations.TryGetValue(reservationId, out reservation))
                    return ItemTransferResult.Failed(null, 0, "Reservation was not found");

                if (GetCount(reservation.ItemId) < reservation.Quantity)
                    return ItemTransferResult.Failed(reservation.ItemId, reservation.Quantity, "Reserved items are no longer available");

                _state.Reservations.Remove(reservationId);
                RemoveFromState(reservation.ItemId, reservation.Quantity);
                return ItemTransferResult.Ok(reservation.ItemId, reservation.Quantity, reservation.Quantity);
            }
        }

        public ItemTransferResult CancelReservation(string reservationId)
        {
            if (string.IsNullOrEmpty(reservationId))
                return ItemTransferResult.Failed(null, 0, "Reservation ID is required");

            lock (_state)
            {
                ModItemReservation reservation;
                if (!_state.Reservations.TryGetValue(reservationId, out reservation))
                    return ItemTransferResult.Failed(null, 0, "Reservation was not found");

                _state.Reservations.Remove(reservationId);
                return ItemTransferResult.Ok(reservation.ItemId, reservation.Quantity, 0);
            }
        }

        public int GetAvailableCount(string itemId)
        {
            lock (_state)
            {
                return Math.Max(0, GetCount(itemId) - _state.GetReservedCount(itemId));
            }
        }

        private void RemoveFromState(string itemId, int quantity)
        {
            int count = GetCount(itemId) - quantity;
            if (count > 0)
                _state.Items[itemId] = count;
            else
                _state.Items.Remove(itemId);
        }
    }
}
