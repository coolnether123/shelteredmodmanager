using ShelteredAPI.Bunkers;
using UnityEngine;

namespace ShelteredAPI.Networking.World
{
    internal static class ShelteredMapEntities
    {
        private static readonly IShelteredMapEntityRegistry _registry = new ShelteredMapEntityRegistry();

        public static IShelteredMapEntityRegistry Registry
        {
            get { return _registry; }
        }

        public static ShelteredMapEntity RegisterBunkerFromAssignment(
            ShelteredMultiplayerBunkerAssignmentRecord record)
        {
            if (record == null)
                return null;

            ShelteredMapEntity entity = CreateBunkerEntity(record);
            return _registry.Upsert(entity);
        }

        public static ShelteredMapEntity Upsert(ShelteredMapEntity entity)
        {
            return _registry.Upsert(entity);
        }

        public static System.Collections.Generic.IList<ShelteredMapEntity> GetAll()
        {
            return _registry.GetAll();
        }

        public static void Clear()
        {
            Clear(string.Empty);
        }

        public static void Clear(string reason)
        {
            _registry.Clear(reason);
        }

        private static ShelteredMapEntity CreateBunkerEntity(
            ShelteredMultiplayerBunkerAssignmentRecord record)
        {
            BunkerMapRecord mapRecord = ShelteredBunkers.GetBunkerMapRecord(record.BunkerOwnerId);
            ExpeditionMap.GridRef gridRef = mapRecord != null
                ? mapRecord.GridRef
                : new ExpeditionMap.GridRef(0, 0);
            Vector3 mapPixels = mapRecord != null ? mapRecord.MapPixels : Vector3.zero;

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = "mapentity:bunker:" + record.BunkerOwnerId;
            entity.Kind = ShelteredMapEntityKind.Bunker;
            entity.OwnerPlayerId = record.PlayerId;
            entity.OwnerPeerId = record.NetworkPeerId;
            entity.BunkerOwnerId = record.BunkerOwnerId;
            entity.DisplayName = record.DisplayName ?? string.Empty;
            entity.WorldPosition = record.Position;
            entity.MapPixels = mapPixels;
            entity.GridX = gridRef.x;
            entity.GridY = gridRef.y;
            entity.IsOnline = record.IsOnline;
            entity.IsVisible = true;
            entity.State = record.IsOnline ? "online" : "offline";
            entity.PayloadJson = string.Empty;
            return entity;
        }
    }
}
