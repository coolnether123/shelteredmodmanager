using System;
using System.Collections.Generic;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Trade
{
    internal static class ShelteredMultiplayerTradeCargoResolver
    {
        public static ShelteredTradeCargoValidationResult ValidateCargoShape(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (tradeEvent == null)
                return ShelteredTradeCargoValidationResult.Failed("Trade event is required.", 0, 0);

            if (IsBlank(tradeEvent.SourceOwnerId))
                return ShelteredTradeCargoValidationResult.Failed("Source owner is required.", 0, 0);

            if (IsBlank(tradeEvent.TargetOwnerId))
                return ShelteredTradeCargoValidationResult.Failed("Target owner is required.", 0, 0);

            if (tradeEvent.Cargo == null || tradeEvent.Cargo.Count == 0)
                return ShelteredTradeCargoValidationResult.Failed("Cargo is required.", 0, 0);

            int totalItemCount = 0;
            for (int i = 0; i < tradeEvent.Cargo.Count; i++)
            {
                ShelteredTradeCargoDto cargo = tradeEvent.Cargo[i];
                if (cargo == null)
                    return ShelteredTradeCargoValidationResult.Failed("Cargo line is required.", tradeEvent.Cargo.Count, totalItemCount);

                if (IsBlank(cargo.ItemId))
                    return ShelteredTradeCargoValidationResult.Failed("Cargo item id is required.", tradeEvent.Cargo.Count, totalItemCount);

                if (cargo.Count <= 0)
                    return ShelteredTradeCargoValidationResult.Failed("Cargo count must be positive.", tradeEvent.Cargo.Count, totalItemCount);

                if (IsBlank(cargo.SourceOwnerId))
                    return ShelteredTradeCargoValidationResult.Failed("Cargo source owner is required.", tradeEvent.Cargo.Count, totalItemCount);

                if (IsBlank(cargo.TargetOwnerId))
                    return ShelteredTradeCargoValidationResult.Failed("Cargo target owner is required.", tradeEvent.Cargo.Count, totalItemCount);

                if (!string.Equals(cargo.SourceOwnerId, tradeEvent.SourceOwnerId, StringComparison.Ordinal))
                    return ShelteredTradeCargoValidationResult.Failed("Cargo source owner must match trade source owner.", tradeEvent.Cargo.Count, totalItemCount);

                if (!string.Equals(cargo.TargetOwnerId, tradeEvent.TargetOwnerId, StringComparison.Ordinal))
                    return ShelteredTradeCargoValidationResult.Failed("Cargo target owner must match trade target owner.", tradeEvent.Cargo.Count, totalItemCount);

                if (int.MaxValue - totalItemCount < cargo.Count)
                    return ShelteredTradeCargoValidationResult.Failed("Cargo total exceeds supported count.", tradeEvent.Cargo.Count, totalItemCount);

                totalItemCount += cargo.Count;
            }

            return ShelteredTradeCargoValidationResult.Ok(tradeEvent.Cargo.Count, totalItemCount);
        }

        public static ShelteredTradeCargoValidationResult ValidateCargoAvailable(
            ShelteredMultiplayerTradeEvent tradeEvent,
            Func<string, IItemStore> resolveOwnerStore)
        {
            ShelteredTradeCargoValidationResult shape = ValidateCargoShape(tradeEvent);
            if (!shape.Success)
                return shape;

            if (resolveOwnerStore == null)
            {
                return ShelteredTradeCargoValidationResult.Failed(
                    "Source owner store resolver is required.",
                    shape.TotalCargoLines,
                    shape.TotalItemCount);
            }

            List<CargoRequest> requests = BuildSourceItemRequests(tradeEvent);
            for (int i = 0; i < requests.Count; i++)
            {
                CargoRequest request = requests[i];
                IItemStore source = ResolveStore(resolveOwnerStore, request.OwnerId);
                if (source == null)
                {
                    return ShelteredTradeCargoValidationResult.Failed(
                        "Source store was not found for owner '" + request.OwnerId + "'.",
                        shape.TotalCargoLines,
                        shape.TotalItemCount);
                }

                int available = GetAvailableCount(source, request.ItemId);
                if (available < request.Count)
                {
                    return ShelteredTradeCargoValidationResult.Failed(
                        "Source store does not contain enough cargo for item '" + request.ItemId + "'.",
                        shape.TotalCargoLines,
                        shape.TotalItemCount);
                }
            }

            return shape;
        }

        public static ShelteredTradeCargoValidationResult BuildCargoSummary(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            return ValidateCargoShape(tradeEvent);
        }

        private static List<CargoRequest> BuildSourceItemRequests(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            List<CargoRequest> requests = new List<CargoRequest>();
            for (int i = 0; i < tradeEvent.Cargo.Count; i++)
            {
                ShelteredTradeCargoDto cargo = tradeEvent.Cargo[i];
                CargoRequest request = FindRequest(requests, cargo.SourceOwnerId, cargo.ItemId);
                if (request == null)
                {
                    request = new CargoRequest(cargo.SourceOwnerId, cargo.ItemId);
                    requests.Add(request);
                }

                request.Count += cargo.Count;
            }

            return requests;
        }

        private static CargoRequest FindRequest(List<CargoRequest> requests, string ownerId, string itemId)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                CargoRequest request = requests[i];
                if (string.Equals(request.OwnerId, ownerId, StringComparison.Ordinal)
                    && string.Equals(request.ItemId, itemId, StringComparison.Ordinal))
                {
                    return request;
                }
            }

            return null;
        }

        private static IItemStore ResolveStore(Func<string, IItemStore> resolveOwnerStore, string ownerId)
        {
            try
            {
                return resolveOwnerStore(ownerId);
            }
            catch
            {
                return null;
            }
        }

        private static int GetAvailableCount(IItemStore store, string itemId)
        {
            IReservableItemStore reservable = store as IReservableItemStore;
            return reservable != null ? reservable.GetAvailableCount(itemId) : store.GetCount(itemId);
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }

        private sealed class CargoRequest
        {
            public CargoRequest(string ownerId, string itemId)
            {
                OwnerId = ownerId ?? string.Empty;
                ItemId = itemId ?? string.Empty;
            }

            public readonly string OwnerId;
            public readonly string ItemId;
            public int Count;
        }
    }
}
