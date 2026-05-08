using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredMapEntityRegistry : IShelteredMapEntityRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ShelteredMapEntity> _entities =
            new Dictionary<string, ShelteredMapEntity>(StringComparer.Ordinal);
        private readonly Func<long> _worldTickSource;

        public ShelteredMapEntityRegistry()
            : this(ResolveCoordinatorWorldTick)
        {
        }

        internal ShelteredMapEntityRegistry(Func<long> worldTickSource)
        {
            _worldTickSource = worldTickSource ?? ResolveCoordinatorWorldTick;
        }

        public ShelteredMapEntity Upsert(ShelteredMapEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException("entity");

            ShelteredMapEntity copy = entity.Clone();
            copy.EntityId = ResolveEntityId(copy);
            if (copy.UpdatedWorldTick <= 0)
                copy.UpdatedWorldTick = ResolveWorldTick();

            lock (_sync)
            {
                _entities[copy.EntityId] = copy;
            }

            return copy.Clone();
        }

        public bool Remove(string entityId)
        {
            string key = NormalizeEntityId(entityId);
            if (key.Length == 0)
                return false;

            lock (_sync)
            {
                return _entities.Remove(key);
            }
        }

        public ShelteredMapEntity Get(string entityId)
        {
            string key = NormalizeEntityId(entityId);
            if (key.Length == 0)
                return null;

            lock (_sync)
            {
                ShelteredMapEntity entity;
                return _entities.TryGetValue(key, out entity) ? entity.Clone() : null;
            }
        }

        public IList<ShelteredMapEntity> GetAll()
        {
            lock (_sync)
            {
                return CloneSorted(_entities.Values);
            }
        }

        public IList<ShelteredMapEntity> GetByKind(ShelteredMapEntityKind kind)
        {
            List<ShelteredMapEntity> matches = new List<ShelteredMapEntity>();
            lock (_sync)
            {
                foreach (ShelteredMapEntity entity in _entities.Values)
                {
                    if (entity != null && entity.Kind == kind)
                        matches.Add(entity);
                }
            }

            return CloneSorted(matches);
        }

        public IList<ShelteredMapEntity> GetByOwnerPlayerId(int playerId)
        {
            List<ShelteredMapEntity> matches = new List<ShelteredMapEntity>();
            lock (_sync)
            {
                foreach (ShelteredMapEntity entity in _entities.Values)
                {
                    if (entity != null && entity.OwnerPlayerId == playerId)
                        matches.Add(entity);
                }
            }

            return CloneSorted(matches);
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                _entities.Clear();
            }
        }

        internal static string ResolveEntityId(ShelteredMapEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException("entity");

            string explicitId = NormalizeEntityId(entity.EntityId);
            if (explicitId.Length > 0)
                return explicitId;

            if (entity.Kind == ShelteredMapEntityKind.Bunker && entity.BunkerOwnerId >= 0)
                return "mapentity:bunker:" + entity.BunkerOwnerId;

            if (entity.Kind != ShelteredMapEntityKind.Unknown
                && entity.OwnerPlayerId > 0
                && entity.BunkerOwnerId >= 0)
            {
                return "mapentity:" + entity.Kind + ":player:" + entity.OwnerPlayerId
                    + ":bunker:" + entity.BunkerOwnerId;
            }

            if (entity.Kind != ShelteredMapEntityKind.Unknown && entity.OwnerPlayerId > 0)
                return "mapentity:" + entity.Kind + ":player:" + entity.OwnerPlayerId;

            throw new ArgumentException("Map entity requires EntityId or stable kind/owner identity.", "entity");
        }

        private long ResolveWorldTick()
        {
            long tick = _worldTickSource();
            return tick > 0 ? tick : 0;
        }

        private static long ResolveCoordinatorWorldTick()
        {
            ShelteredMultiplayerSessionContext context =
                ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null ? context.WorldTick : 0;
        }

        private static string NormalizeEntityId(string entityId)
        {
            return (entityId ?? string.Empty).Trim();
        }

        private static IList<ShelteredMapEntity> CloneSorted(IEnumerable<ShelteredMapEntity> entities)
        {
            List<ShelteredMapEntity> copies = new List<ShelteredMapEntity>();
            foreach (ShelteredMapEntity entity in entities)
            {
                if (entity != null)
                    copies.Add(entity.Clone());
            }

            copies.Sort(CompareEntities);
            return copies;
        }

        private static int CompareEntities(ShelteredMapEntity left, ShelteredMapEntity right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.Compare(left.EntityId, right.EntityId, StringComparison.Ordinal);
        }
    }
}
