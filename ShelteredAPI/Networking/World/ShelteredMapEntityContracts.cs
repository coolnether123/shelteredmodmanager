using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Networking.World
{
    internal enum ShelteredMapEntityKind
    {
        Unknown,
        Bunker,
        Expedition,
        TradeCaravan,
        RaidParty,
        Settlement,
        ResourceNode,
        FactionMarker
    }

    internal sealed class ShelteredMapEntity
    {
        public ShelteredMapEntity()
        {
            EntityId = string.Empty;
            DisplayName = string.Empty;
            State = string.Empty;
            PayloadJson = string.Empty;
        }

        public string EntityId;
        public ShelteredMapEntityKind Kind;
        public int OwnerPlayerId;
        public byte OwnerPeerId;
        public int BunkerOwnerId;
        public string DisplayName;
        public Vector2 WorldPosition;
        public Vector3 MapPixels;
        public int GridX;
        public int GridY;
        public bool IsOnline;
        public bool IsVisible;
        public string State;
        public string PayloadJson;
        public long UpdatedWorldTick;

        internal ShelteredMapEntity Clone()
        {
            return new ShelteredMapEntity
            {
                EntityId = EntityId ?? string.Empty,
                Kind = Kind,
                OwnerPlayerId = OwnerPlayerId,
                OwnerPeerId = OwnerPeerId,
                BunkerOwnerId = BunkerOwnerId,
                DisplayName = DisplayName ?? string.Empty,
                WorldPosition = WorldPosition,
                MapPixels = MapPixels,
                GridX = GridX,
                GridY = GridY,
                IsOnline = IsOnline,
                IsVisible = IsVisible,
                State = State ?? string.Empty,
                PayloadJson = PayloadJson ?? string.Empty,
                UpdatedWorldTick = UpdatedWorldTick
            };
        }
    }

    internal interface IShelteredMapEntityRegistry
    {
        ShelteredMapEntity Upsert(ShelteredMapEntity entity);
        bool Remove(string entityId);
        ShelteredMapEntity Get(string entityId);
        IList<ShelteredMapEntity> GetAll();
        IList<ShelteredMapEntity> GetByKind(ShelteredMapEntityKind kind);
        IList<ShelteredMapEntity> GetByOwnerPlayerId(int playerId);
        void Clear(string reason);
    }
}
