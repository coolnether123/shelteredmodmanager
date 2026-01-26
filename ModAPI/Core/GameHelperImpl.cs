using System;
using System.Collections.Generic;
using ModAPI.Content;
using ModAPI.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    internal class GameHelperImpl : IGameHelper
    {
        public int GetTotalOwned(string itemId)
        {
            if (!ContentInjector.ResolveItemType(itemId, out var type))
                return 0;

            int count = 0;

            // 1. Standard items
            try
            {
                var inv = InventoryManager.Instance;
                if (inv != null)
                {
                    count += inv.GetItemCountInStorage(type, true);
                }
            }
            catch { }

            // 2. Rations and Meat
            try
            {
                var fm = FoodManager.Instance;
                if (fm != null)
                {
                    // Items like Rations or Meat might be in FoodManager
                    // Need to check if they match the type or are handled specifically
                    if (type == ItemManager.ItemType.Ration)
                        count += fm.Rations;
                    else if (type == ItemManager.ItemType.Meat)
                        count += fm.Meat;
                }
            }
            catch { }

            // 3. Stored water
            try
            {
                var wm = WaterManager.Instance;
                if (wm != null && type == ItemManager.ItemType.Water)
                {
                    count += (int)wm.StoredWater;
                }
            }
            catch { }

            // 4. Books/Toys/Records
            try
            {
                var em = EntertainmentManager.Instance;
                if (em != null)
                {
                    if (type == ItemManager.ItemType.Book) count += em.Books;
                    else if (type == ItemManager.ItemType.Toy) count += em.Toys;
                    else if (type == ItemManager.ItemType.Record) count += em.Records;
                }
            }
            catch { }

            return count;
        }

        public int GetInventoryCount(string itemId)
        {
            if (!ContentInjector.ResolveItemType(itemId, out var type))
                return 0;

            try
            {
                var inv = InventoryManager.Instance;
                return inv != null ? inv.GetItemCountInStorage(type, true) : 0;
            }
            catch { return 0; }
        }

        public FamilyMember FindMember(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            try
            {
                var fm = FamilyManager.Instance;
                if (fm == null) return null;
                var members = fm.GetAllFamilyMembers();
                if (members == null) return null;
                for (int i = 0; i < members.Count; i++)
                {
                    // FamilyMember doesn't have characterId, use firstName for string lookups
                    if (members[i] != null && members[i].firstName == characterId)
                        return members[i];
                }
            }
            catch { }
            return null;
        }
    }
}
