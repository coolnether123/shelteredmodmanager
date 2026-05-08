using System;
using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Networking;
using ShelteredAPI.Networking.Map;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Knowledge
{
    internal sealed class ShelteredMapKnowledgeService : IShelteredMapKnowledgeService
    {
        private static readonly ShelteredMapKnowledgeService _instance = new ShelteredMapKnowledgeService();

        private readonly object _sync = new object();
        private readonly Dictionary<string, MapKnowledgeRecord> _records =
            new Dictionary<string, MapKnowledgeRecord>(StringComparer.Ordinal);

        public static ShelteredMapKnowledgeService Instance
        {
            get { return _instance; }
        }

        public static bool DebugRevealAll;

        private ShelteredMapKnowledgeService()
        {
        }

        public MapKnowledgeRecord GetKnowledge(int viewerPlayerId, string entityId)
        {
            string key = CreateKey(viewerPlayerId, entityId);
            lock (_sync)
            {
                MapKnowledgeRecord record;
                return _records.TryGetValue(key, out record) ? record.Clone() : null;
            }
        }

        public MapKnowledgeRecord Reveal(int viewerPlayerId, string entityId, MapKnowledgeLevel level, string reason)
        {
            ShelteredMapEntity entity = ShelteredMapEntities.Get(entityId);
            MapKnowledgeRecord record = CreateRecord(viewerPlayerId, entity, level);
            record.DiscoveredByEventId = ShelteredMapDiscoveryEvents.AppendReveal(record, reason);

            lock (_sync)
            {
                _records[CreateKey(viewerPlayerId, record.EntityId)] = record.Clone();
            }

            return record.Clone();
        }

        public bool Forget(int viewerPlayerId, string entityId, string reason)
        {
            string key = CreateKey(viewerPlayerId, entityId);
            bool removed;
            lock (_sync)
            {
                removed = _records.Remove(key);
            }

            if (removed)
                ShelteredMapDiscoveryEvents.AppendForget(viewerPlayerId, entityId, reason);

            return removed;
        }

        public bool CanSeeExactLocation(int viewerPlayerId, string entityId)
        {
            ShelteredMapEntity entity = ShelteredMapEntities.Get(entityId);
            if (entity == null)
                return false;

            MapKnowledgeLevel level = ResolveKnowledgeLevel(viewerPlayerId, entity);
            return level == MapKnowledgeLevel.Scouted
                || level == MapKnowledgeLevel.Identified
                || level == MapKnowledgeLevel.Confirmed
                || level == MapKnowledgeLevel.Allied
                || level == MapKnowledgeLevel.DebugFull;
        }

        public ShelteredMultiplayerMapMarker BuildDisplayMarker(int viewerPlayerId, ShelteredMapEntity entity)
        {
            if (entity == null || !entity.IsVisible)
                return null;

            MapKnowledgeRecord knowledge = ResolveDisplayKnowledge(viewerPlayerId, entity);
            bool exact = CanSeeExactLocation(viewerPlayerId, entity.EntityId);
            bool local = entity.OwnerPlayerId > 0 && entity.OwnerPlayerId == viewerPlayerId;
            bool known = knowledge.KnowledgeLevel == MapKnowledgeLevel.Identified
                || knowledge.KnowledgeLevel == MapKnowledgeLevel.Confirmed
                || knowledge.KnowledgeLevel == MapKnowledgeLevel.Allied
                || knowledge.KnowledgeLevel == MapKnowledgeLevel.DebugFull
                || local;

            string label = known && !string.IsNullOrEmpty(knowledge.KnownDisplayName)
                ? knowledge.KnownDisplayName
                : "?";

            return new ShelteredMultiplayerMapMarker(
                CreateMarkerId(entity),
                label,
                entity.BunkerOwnerId,
                entity.OwnerPeerId,
                ResolveMapPixels(entity, knowledge, exact),
                local,
                entity.IsOnline,
                known ? ResolveVisualKind(entity.Kind, local) : ShelteredMultiplayerMapMarkerVisualKind.Unknown,
                entity.EntityId,
                knowledge.IsStale);
        }

        internal void Clear(string reason)
        {
            lock (_sync)
            {
                _records.Clear();
            }
        }

        internal static MapContactKind ToContactKind(ShelteredMapEntityKind kind)
        {
            if (kind == ShelteredMapEntityKind.Bunker)
                return MapContactKind.Bunker;
            if (kind == ShelteredMapEntityKind.Expedition)
                return MapContactKind.Expedition;
            if (kind == ShelteredMapEntityKind.TradeCaravan)
                return MapContactKind.Caravan;
            if (kind == ShelteredMapEntityKind.RaidParty)
                return MapContactKind.RaidParty;
            if (kind == ShelteredMapEntityKind.Settlement)
                return MapContactKind.Settlement;
            if (kind == ShelteredMapEntityKind.ResourceNode)
                return MapContactKind.ResourceNode;
            if (kind == ShelteredMapEntityKind.FactionMarker)
                return MapContactKind.FactionTerritory;

            return MapContactKind.Unknown;
        }

        private MapKnowledgeRecord ResolveDisplayKnowledge(int viewerPlayerId, ShelteredMapEntity entity)
        {
            MapKnowledgeRecord explicitRecord = GetKnowledge(viewerPlayerId, entity.EntityId);
            if (explicitRecord != null)
                return explicitRecord;

            return CreateRecord(viewerPlayerId, entity, ResolveDefaultLevel(viewerPlayerId, entity));
        }

        private MapKnowledgeLevel ResolveKnowledgeLevel(int viewerPlayerId, ShelteredMapEntity entity)
        {
            MapKnowledgeRecord explicitRecord = GetKnowledge(viewerPlayerId, entity.EntityId);
            return explicitRecord != null
                ? explicitRecord.KnowledgeLevel
                : ResolveDefaultLevel(viewerPlayerId, entity);
        }

        private static MapKnowledgeLevel ResolveDefaultLevel(int viewerPlayerId, ShelteredMapEntity entity)
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (DebugRevealAll || IsFogDisabled(context))
                return MapKnowledgeLevel.DebugFull;
            if (context != null && context.Mode == ShelteredMultiplayerSessionMode.Host)
                return MapKnowledgeLevel.DebugFull;
            if (entity.OwnerPlayerId > 0 && entity.OwnerPlayerId == viewerPlayerId)
                return MapKnowledgeLevel.Confirmed;
            if (entity.Kind == ShelteredMapEntityKind.Bunker && entity.OwnerPlayerId > 0)
                return MapKnowledgeLevel.Suspicious;

            return MapKnowledgeLevel.Unknown;
        }

        private static bool IsFogDisabled(ShelteredMultiplayerSessionContext context)
        {
            return context != null
                && context.IsMultiplayerActive
                && context.SetupSettings != null
                && !context.SetupSettings.Fog;
        }

        private static MapKnowledgeRecord CreateRecord(
            int viewerPlayerId,
            ShelteredMapEntity entity,
            MapKnowledgeLevel level)
        {
            if (entity == null)
                throw new ArgumentException("Knowledge reveal requires a registered map entity.", "entityId");

            return new MapKnowledgeRecord
            {
                EntityId = entity.EntityId ?? string.Empty,
                ViewerPlayerId = viewerPlayerId,
                KnowledgeLevel = level,
                KnownKind = IsKnownKind(level) ? ToContactKind(entity.Kind) : MapContactKind.Unknown,
                KnownDisplayName = IsKnownIdentity(level) ? entity.DisplayName ?? string.Empty : string.Empty,
                LastKnownGridX = entity.GridX,
                LastKnownGridY = entity.GridY,
                LastKnownWorldTick = entity.UpdatedWorldTick,
                IsStale = false
            };
        }

        private static bool IsKnownKind(MapKnowledgeLevel level)
        {
            return level == MapKnowledgeLevel.Scouted
                || level == MapKnowledgeLevel.Identified
                || level == MapKnowledgeLevel.Confirmed
                || level == MapKnowledgeLevel.Allied
                || level == MapKnowledgeLevel.DebugFull;
        }

        private static bool IsKnownIdentity(MapKnowledgeLevel level)
        {
            return level == MapKnowledgeLevel.Identified
                || level == MapKnowledgeLevel.Confirmed
                || level == MapKnowledgeLevel.Allied
                || level == MapKnowledgeLevel.DebugFull;
        }

        private static Vector3 ResolveMapPixels(
            ShelteredMapEntity entity,
            MapKnowledgeRecord knowledge,
            bool exact)
        {
            if (exact || knowledge == null)
                return entity.MapPixels;

            return entity.MapPixels;
        }

        private static ShelteredMultiplayerMapMarkerVisualKind ResolveVisualKind(
            ShelteredMapEntityKind kind,
            bool local)
        {
            if (kind == ShelteredMapEntityKind.Bunker)
                return local
                    ? ShelteredMultiplayerMapMarkerVisualKind.LocalBunker
                    : ShelteredMultiplayerMapMarkerVisualKind.RemoteBunker;
            if (kind == ShelteredMapEntityKind.Expedition)
                return ShelteredMultiplayerMapMarkerVisualKind.Expedition;
            if (kind == ShelteredMapEntityKind.TradeCaravan)
                return ShelteredMultiplayerMapMarkerVisualKind.TradeCaravan;
            if (kind == ShelteredMapEntityKind.RaidParty)
                return ShelteredMultiplayerMapMarkerVisualKind.RaidParty;
            if (kind == ShelteredMapEntityKind.Settlement)
                return ShelteredMultiplayerMapMarkerVisualKind.Settlement;
            if (kind == ShelteredMapEntityKind.ResourceNode)
                return ShelteredMultiplayerMapMarkerVisualKind.ResourceNode;
            if (kind == ShelteredMapEntityKind.FactionMarker)
                return ShelteredMultiplayerMapMarkerVisualKind.FactionMarker;

            return ShelteredMultiplayerMapMarkerVisualKind.Unknown;
        }

        private static string CreateMarkerId(ShelteredMapEntity entity)
        {
            if (entity != null && entity.Kind == ShelteredMapEntityKind.Bunker)
                return ShelteredMultiplayerMapMarkerAssignmentResolver.CreateMarkerId(entity.BunkerOwnerId);

            return "multiplayer-map-" + (entity != null ? entity.EntityId : string.Empty);
        }

        private static string CreateKey(int viewerPlayerId, string entityId)
        {
            return viewerPlayerId + "|" + Normalize(entityId);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
