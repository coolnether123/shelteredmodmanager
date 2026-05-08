using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Resources
{
    internal sealed class ShelteredResourceNodeRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ResourceNode> _nodes =
            new Dictionary<string, ResourceNode>(StringComparer.Ordinal);
        private readonly Func<long> _worldTickSource;

        public ShelteredResourceNodeRegistry()
            : this(ResolveCoordinatorWorldTick)
        {
        }

        internal ShelteredResourceNodeRegistry(Func<long> worldTickSource)
        {
            _worldTickSource = worldTickSource ?? ResolveCoordinatorWorldTick;
        }

        public ResourceNode Upsert(ResourceNode node)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            ResourceNode copy = node.Copy();
            copy.NodeId = ResolveNodeId(copy);
            if (copy.Capacity < 0)
                copy.Capacity = 0;
            if (copy.Remaining < 0)
                copy.Remaining = 0;
            if (copy.Remaining > copy.Capacity && copy.Capacity > 0)
                copy.Remaining = copy.Capacity;
            copy.IsDepleted = copy.Remaining <= 0;
            if (copy.LastUpdatedTick <= 0)
                copy.LastUpdatedTick = ResolveWorldTick();

            lock (_sync)
            {
                _nodes[copy.NodeId] = copy;
            }

            RegisterMapEntity(copy);
            return copy.Copy();
        }

        public bool TryGet(string nodeId, out ResourceNode node)
        {
            node = null;
            string key = Normalize(nodeId);
            if (key.Length == 0)
                return false;

            lock (_sync)
            {
                ResourceNode existing;
                if (!_nodes.TryGetValue(key, out existing))
                    return false;
                node = existing.Copy();
                return true;
            }
        }

        public IList<ResourceNode> GetAll()
        {
            lock (_sync)
            {
                List<ResourceNode> result = new List<ResourceNode>();
                foreach (ResourceNode node in _nodes.Values)
                    result.Add(node.Copy());
                result.Sort(CompareNodes);
                return result;
            }
        }

        public bool Harvest(string nodeId, int amount, int playerId, long tick, out ResourceNode updated)
        {
            updated = null;
            if (amount <= 0)
                return false;

            lock (_sync)
            {
                ResourceNode existing;
                if (!_nodes.TryGetValue(Normalize(nodeId), out existing))
                    return false;

                ResourceNode copy = existing.Copy();
                copy.Remaining = Math.Max(0, copy.Remaining - amount);
                copy.OwnerPlayerId = playerId > 0 ? playerId : copy.OwnerPlayerId;
                copy.IsDepleted = copy.Remaining <= 0;
                copy.LastUpdatedTick = tick;
                _nodes[copy.NodeId] = copy;
                updated = copy.Copy();
            }

            RegisterMapEntity(updated);
            return true;
        }

        public bool Regenerate(string nodeId, int amount, long tick, out ResourceNode updated)
        {
            updated = null;
            if (amount <= 0)
                return false;

            lock (_sync)
            {
                ResourceNode existing;
                if (!_nodes.TryGetValue(Normalize(nodeId), out existing))
                    return false;

                ResourceNode copy = existing.Copy();
                copy.Remaining = copy.Capacity > 0 ? Math.Min(copy.Capacity, copy.Remaining + amount) : copy.Remaining + amount;
                copy.IsDepleted = copy.Remaining <= 0;
                copy.LastUpdatedTick = tick;
                _nodes[copy.NodeId] = copy;
                updated = copy.Copy();
            }

            RegisterMapEntity(updated);
            return true;
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                _nodes.Clear();
            }
        }

        internal static string ResolveNodeId(ResourceNode node)
        {
            string explicitId = Normalize(node != null ? node.NodeId : string.Empty);
            if (explicitId.Length > 0)
                return explicitId;

            if (node == null)
                throw new ArgumentNullException("node");

            return "resourcenode:" + Normalize(node.Kind)
                + ":" + node.GridX.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + node.GridY.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void RegisterMapEntity(ResourceNode node)
        {
            if (node == null)
                return;

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = "mapentity:resourcenode:" + node.NodeId;
            entity.Kind = ShelteredMapEntityKind.ResourceNode;
            entity.OwnerPlayerId = node.OwnerPlayerId;
            entity.DisplayName = node.Kind ?? string.Empty;
            entity.WorldPosition = new Vector2(node.GridX, node.GridY);
            entity.MapPixels = Vector3.zero;
            entity.GridX = node.GridX;
            entity.GridY = node.GridY;
            entity.IsOnline = true;
            entity.IsVisible = true;
            entity.State = node.IsDepleted ? "depleted" : "available";
            entity.PayloadJson = "{\"remaining\":" + node.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"capacity\":" + node.Capacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
            entity.UpdatedWorldTick = node.LastUpdatedTick;
            ShelteredMapEntities.Upsert(entity);
        }

        private long ResolveWorldTick()
        {
            long tick = _worldTickSource();
            return tick > 0 ? tick : 0;
        }

        private static long ResolveCoordinatorWorldTick()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null ? context.WorldTick : 0;
        }

        private static int CompareNodes(ResourceNode left, ResourceNode right)
        {
            return string.Compare(left.NodeId, right.NodeId, StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
