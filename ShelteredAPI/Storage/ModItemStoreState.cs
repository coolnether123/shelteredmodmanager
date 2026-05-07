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
    internal sealed class ModItemStoreState
    {
        public string OwnerId;
        public string StoreId;
        public string DisplayName;
        public int Capacity;
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ModItemReservation> Reservations = new Dictionary<string, ModItemReservation>(StringComparer.OrdinalIgnoreCase);

        public int Used
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> pair in Items)
                    total += Math.Max(0, pair.Value);
                return total;
            }
        }

        public int GetReservedCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return 0;

            int total = 0;
            foreach (KeyValuePair<string, ModItemReservation> pair in Reservations)
            {
                ModItemReservation reservation = pair.Value;
                if (reservation != null && string.Equals(reservation.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    total += Math.Max(0, reservation.Quantity);
            }
            return total;
        }
    }
}
