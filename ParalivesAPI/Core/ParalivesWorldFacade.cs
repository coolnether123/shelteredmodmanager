using System.Collections.Generic;
using UnityEngine;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesWorldFacade
    {
        internal ParalivesWorldFacade()
        {
        }

        public bool TryGetLot(ulong lotGuid, out global::AssetLot lot)
        {
            lot = null;
            if (lotGuid == 0UL)
                return false;

            try
            {
                if (global::LotManager.Instance != null)
                    lot = global::LotManager.Instance.GetLotByGUID(lotGuid);
                if (lot == null && global::AssetManager.Instance != null)
                    lot = global::AssetManager.Instance.GetAsset(lotGuid) as global::AssetLot;
            }
            catch
            {
                lot = null;
            }

            return lot != null;
        }

        public bool TryGetLotAt(Vector3 position, out ulong lotGuid)
        {
            lotGuid = 0UL;
            try
            {
                if (global::LotManager.Instance == null)
                    return false;

                lotGuid = global::LotManager.Instance.GetLotFromPosition(position);
                return lotGuid != 0UL;
            }
            catch
            {
                lotGuid = 0UL;
                return false;
            }
        }

        public bool TryGetItem(int itemInstanceId, out global::ItemObjectRoot item)
        {
            item = null;
            if (itemInstanceId < 0 || global::ItemManager.Instance == null)
                return false;

            try
            {
                item = global::ItemManager.Instance.GetItemByInstanceID(itemInstanceId);
            }
            catch
            {
                item = null;
            }

            return item != null;
        }

        public global::ItemObjectRoot[] GetLotItems(ulong lotGuid)
        {
            if (lotGuid == 0UL || global::ItemManager.Instance == null)
                return new global::ItemObjectRoot[0];

            try
            {
                List<global::ItemObjectRoot> items = global::ItemManager.Instance.GetLotItems(lotGuid);
                return items == null ? new global::ItemObjectRoot[0] : items.ToArray();
            }
            catch
            {
                return new global::ItemObjectRoot[0];
            }
        }

        public bool TrySpawnItem(ulong catalogueGuid, ulong lotGuid, out global::ItemObjectRoot item)
        {
            item = null;
            if (catalogueGuid == 0UL || lotGuid == 0UL || global::ItemManager.Instance == null)
                return false;

            try
            {
                item = global::ItemManager.Instance.SpawnItemGeneric(catalogueGuid, lotGuid);
            }
            catch
            {
                item = null;
            }

            return item != null;
        }
    }
}
