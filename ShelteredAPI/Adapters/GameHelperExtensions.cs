using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;

namespace ShelteredAPI.Adapters
{
    /// <summary>
    /// Strongly-typed gameplay adapters on top of IGameHelper.
    /// </summary>
    public static class GameHelperExtensions
    {
        /// <summary>
        /// Get total owned items by strongly-typed item enum.
        /// </summary>
        public static int GetTotalOwned(this IGameHelper helper, ItemManager.ItemType itemType)
        {
            return ShelteredInternalHelper.GetItemCountStrict(itemType);
        }

        /// <summary>
        /// Returns true if the provided family member is currently in any active exploration party.
        /// </summary>
        public static bool IsAwayOnExpedition(this IGameHelper helper, FamilyMember member)
        {
            return ShelteredInternalHelper.IsFamilyMemberAwayOnExpedition(member);
        }
    }

    internal static class ShelteredInternalHelper
    {
        internal static int GetItemCountStrict(ItemManager.ItemType itemType)
        {
            int count = 0;

            try
            {
                var inv = InventoryManager.Instance;
                if (inv != null)
                {
                    count += inv.GetItemCountInStorage(itemType, true);
                }
            }
            catch { }

            try
            {
                var fm = FoodManager.Instance;
                if (fm != null)
                {
                    if (itemType == ItemManager.ItemType.Ration)
                        count += fm.Rations;
                    else if (itemType == ItemManager.ItemType.Meat)
                        count += fm.Meat;
                }
            }
            catch { }

            try
            {
                var wm = WaterManager.Instance;
                if (wm != null && itemType == ItemManager.ItemType.Water)
                {
                    count += (int)wm.StoredWater;
                }
            }
            catch { }

            try
            {
                var em = EntertainmentManager.Instance;
                if (em != null)
                {
                    if (itemType == ItemManager.ItemType.Book)
                        count += em.Books;
                    else if (itemType == ItemManager.ItemType.Toy)
                        count += em.Toys;
                    else if (itemType == ItemManager.ItemType.Record)
                        count += em.Records;
                }
            }
            catch { }

            return count;
        }

        internal static bool IsFamilyMemberAwayOnExpedition(FamilyMember member)
        {
            if (member == null) return false;

            var explorationManager = ExplorationManager.Instance;
            if (explorationManager == null) return false;

            try
            {
                List<ExplorationParty> list = explorationManager.GetAllExplorarionParties();
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var party = list[i];
                        if (party != null && party.ContainsFamilyMember(member))
                            return true;
                    }
                }
            }
            catch { }

            try
            {
                Dictionary<int, ExplorationParty> partiesById;
                if (TryGetField(explorationManager, "m_parties", out partiesById) ||
                    TryGetField(explorationManager, "parties", out partiesById))
                {
                    if (partiesById != null)
                    {
                        foreach (var kv in partiesById)
                        {
                            var party = kv.Value;
                            if (party != null && party.ContainsFamilyMember(member))
                                return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool TryGetField<T>(object instance, string fieldName, out T value)
        {
            value = default(T);
            if (instance == null || string.IsNullOrEmpty(fieldName))
                return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type cursor = instance.GetType();

            while (cursor != null)
            {
                try
                {
                    FieldInfo field = cursor.GetField(fieldName, flags);
                    if (field != null)
                    {
                        object raw = field.GetValue(instance);
                        if (raw is T)
                        {
                            value = (T)raw;
                            return true;
                        }
                    }
                }
                catch { }

                cursor = cursor.BaseType;
            }

            return false;
        }
    }
}
