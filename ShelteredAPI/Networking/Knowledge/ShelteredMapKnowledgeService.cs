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

        public MapKnowledgeRecord GetEffectiveKnowledge(int viewerPlayerId, ShelteredMapEntity entity)
        {
            if (entity == null)
                return null;

            MapKnowledgeLevel defaultLevel = ResolveDefaultLevel(viewerPlayerId, entity);
            MapKnowledgeRecord explicitRecord = GetKnowledge(viewerPlayerId, entity.EntityId);
            if (explicitRecord == null)
                return CreateRecord(viewerPlayerId, entity, defaultLevel);

            MapKnowledgeLevel effectiveLevel = MaxLevel(defaultLevel, explicitRecord.KnowledgeLevel);
            if (effectiveLevel == explicitRecord.KnowledgeLevel)
                return explicitRecord;

            return CreateRecord(viewerPlayerId, entity, effectiveLevel);
        }

        public MapKnowledgeRecord Reveal(int viewerPlayerId, string entityId, MapKnowledgeLevel level, string reason)
        {
            ShelteredMapEntity entity = ShelteredMapEntities.Get(entityId);
            MapKnowledgeRecord existing = GetKnowledge(viewerPlayerId, entityId);
            MapKnowledgeLevel effectiveLevel = existing != null
                ? MaxLevel(existing.KnowledgeLevel, level)
                : level;
            MapKnowledgeRecord record = CreateRecord(viewerPlayerId, entity, effectiveLevel);
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

            MapKnowledgeRecord knowledge = GetEffectiveKnowledge(viewerPlayerId, entity);
            return knowledge != null && IsExactLocationKnown(knowledge.KnowledgeLevel);
        }

        public ShelteredMapEntity BuildVisibleEntity(int viewerPlayerId, ShelteredMapEntity entity)
        {
            if (entity == null || !entity.IsVisible)
                return null;

            MapKnowledgeRecord knowledge = GetEffectiveKnowledge(viewerPlayerId, entity);
            if (knowledge == null || !IsVisibleLevel(knowledge.KnowledgeLevel))
                return null;

            ShelteredMapEntity visible = entity.Clone();
            bool local = IsLocalEntity(viewerPlayerId, entity);
            bool identityKnown = local || IsKnownIdentity(knowledge.KnowledgeLevel);
            bool detailsKnown = local || IsFullDetailsKnown(knowledge.KnowledgeLevel);

            visible.GridX = knowledge.LastKnownGridX;
            visible.GridY = knowledge.LastKnownGridY;
            visible.UpdatedWorldTick = knowledge.LastKnownWorldTick;

            if (!identityKnown)
            {
                visible.DisplayName = string.Empty;
                visible.OwnerPeerId = NetworkDefaults.UnassignedPeerId;
            }

            if (!detailsKnown)
            {
                visible.State = string.Empty;
                visible.PayloadJson = string.Empty;
            }

            return visible;
        }

        public IList<ShelteredMapEntity> GetVisibleEntities(int viewerPlayerId, IShelteredMapEntityRegistry registry)
        {
            List<ShelteredMapEntity> visible = new List<ShelteredMapEntity>();
            if (registry == null)
                return visible;

            IList<ShelteredMapEntity> entities = registry.GetAll();
            for (int i = 0; i < entities.Count; i++)
            {
                ShelteredMapEntity entity = BuildVisibleEntity(viewerPlayerId, entities[i]);
                if (entity != null)
                    visible.Add(entity);
            }

            return visible;
        }

        public ShelteredMultiplayerMapMarker BuildDisplayMarker(int viewerPlayerId, ShelteredMapEntity entity)
        {
            ShelteredMapEntity visibleEntity = BuildVisibleEntity(viewerPlayerId, entity);
            if (visibleEntity == null)
                return null;

            MapKnowledgeRecord knowledge = GetEffectiveKnowledge(viewerPlayerId, entity);
            bool exact = knowledge != null && IsExactLocationKnown(knowledge.KnowledgeLevel);
            bool local = entity.OwnerPlayerId > 0 && entity.OwnerPlayerId == viewerPlayerId;
            bool knownKind = local || (knowledge != null && IsKnownKind(knowledge.KnowledgeLevel));
            bool knownIdentity = local || (knowledge != null && IsKnownIdentity(knowledge.KnowledgeLevel));

            string label = knownIdentity && !string.IsNullOrEmpty(knowledge.KnownDisplayName)
                ? knowledge.KnownDisplayName
                : "?";

            return new ShelteredMultiplayerMapMarker(
                CreateMarkerId(visibleEntity),
                label,
                visibleEntity.BunkerOwnerId,
                visibleEntity.OwnerPeerId,
                ResolveMapPixels(visibleEntity, knowledge, exact),
                local,
                visibleEntity.IsOnline,
                knownKind ? ResolveVisualKind(visibleEntity.Kind, local) : ShelteredMultiplayerMapMarkerVisualKind.Unknown,
                visibleEntity.EntityId,
                knowledge != null && knowledge.IsStale);
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

        private static MapKnowledgeLevel ResolveDefaultLevel(int viewerPlayerId, ShelteredMapEntity entity)
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (DebugRevealAll || IsFogDisabled(context))
                return MapKnowledgeLevel.DebugFull;
            if (entity.OwnerPlayerId > 0 && entity.OwnerPlayerId == viewerPlayerId)
                return MapKnowledgeLevel.Confirmed;
            if (entity.Kind == ShelteredMapEntityKind.Bunker && entity.OwnerPlayerId > 0)
                return MapKnowledgeLevel.Suspicious;
            if (entity.Kind == ShelteredMapEntityKind.UnknownContact)
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

        private static bool IsVisibleLevel(MapKnowledgeLevel level)
        {
            return level == MapKnowledgeLevel.Suspicious
                || level == MapKnowledgeLevel.Scouted
                || level == MapKnowledgeLevel.Identified
                || level == MapKnowledgeLevel.Confirmed
                || level == MapKnowledgeLevel.Allied
                || level == MapKnowledgeLevel.DebugFull;
        }

        private static bool IsExactLocationKnown(MapKnowledgeLevel level)
        {
            return level == MapKnowledgeLevel.Scouted
                || level == MapKnowledgeLevel.Identified
                || level == MapKnowledgeLevel.Confirmed
                || level == MapKnowledgeLevel.Allied
                || level == MapKnowledgeLevel.DebugFull;
        }

        private static bool IsFullDetailsKnown(MapKnowledgeLevel level)
        {
            return level == MapKnowledgeLevel.Confirmed
                || level == MapKnowledgeLevel.Allied
                || level == MapKnowledgeLevel.DebugFull;
        }

        private static bool IsLocalEntity(int viewerPlayerId, ShelteredMapEntity entity)
        {
            return entity != null && entity.OwnerPlayerId > 0 && entity.OwnerPlayerId == viewerPlayerId;
        }

        private static MapKnowledgeLevel MaxLevel(MapKnowledgeLevel left, MapKnowledgeLevel right)
        {
            return left >= right ? left : right;
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
