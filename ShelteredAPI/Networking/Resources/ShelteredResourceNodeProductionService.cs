using System;
using ModAPI.Core;
using ModAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Resources
{
    internal sealed class ShelteredResourceNodeProductionService : IDisposable
    {
        private const string SeedStreamName = "MultiplayerSync.World.ResourceNodes";
        private static readonly ShelteredResourceNodeProductionService _instance = new ShelteredResourceNodeProductionService();
        private readonly ShelteredResourceNodeRegistry _registry;
        private bool _disposed;

        public static ShelteredResourceNodeProductionService Instance
        {
            get { return _instance; }
        }

        public ShelteredResourceNodeProductionService()
            : this(new ShelteredResourceNodeRegistry(), true)
        {
        }

        internal ShelteredResourceNodeProductionService(ShelteredResourceNodeRegistry registry, bool subscribe)
        {
            _registry = registry ?? new ShelteredResourceNodeRegistry();
            if (subscribe)
            {
                ShelteredMultiplayerNetworkEvents.IntentReceived += OnIntentReceived;
                ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
            }
        }

        public ShelteredResourceNodeRegistry Registry
        {
            get { return _registry; }
        }

        public event Action<string, ShelteredResourceNodeEvent> ResourceNodeEventApplied;

        public ResourceNode GenerateNode(string kind, int gridX, int gridY, int minCapacity, int maxCapacity, int regenPerDay)
        {
            ModRandomStream stream = ModRandom.GetStream(SeedStreamName);
            int capacity = maxCapacity > minCapacity ? stream.Range(minCapacity, maxCapacity + 1) : minCapacity;
            if (capacity < 0)
                capacity = 0;

            ResourceNode node = new ResourceNode();
            node.Kind = string.IsNullOrEmpty(kind) ? "Unknown" : kind;
            node.GridX = gridX;
            node.GridY = gridY;
            node.Capacity = capacity;
            node.Remaining = capacity;
            node.RegenPerDay = regenPerDay;
            node.LastUpdatedTick = ResolveWorldTick();
            node.NodeId = ShelteredResourceNodeRegistry.ResolveNodeId(node);

            ShelteredResourceNodeEvent nodeEvent = CreateEvent(node, 0, 0, "Generated");
            PublishOrApply(ShelteredNetworkEventKinds.ResourceNodeGenerated, nodeEvent);
            return node.Copy();
        }

        public bool ClaimNode(string nodeId, int playerId, string factionId)
        {
            ResourceNode node;
            if (!_registry.TryGet(nodeId, out node))
                return false;

            node.OwnerPlayerId = playerId;
            node.OwnerFactionId = factionId ?? string.Empty;
            node.LastUpdatedTick = ResolveWorldTick();
            return PublishOrApply(ShelteredNetworkEventKinds.ResourceNodeClaimed, CreateEvent(node, playerId, 0, "Claimed"));
        }

        public bool HarvestNode(string nodeId, int amount, int playerId)
        {
            ResourceNode node;
            if (!_registry.TryGet(nodeId, out node) || amount <= 0)
                return false;

            node.Remaining = Math.Max(0, node.Remaining - amount);
            node.OwnerPlayerId = playerId > 0 ? playerId : node.OwnerPlayerId;
            node.IsDepleted = node.Remaining <= 0;
            node.LastUpdatedTick = ResolveWorldTick();
            bool published = PublishOrApply(ShelteredNetworkEventKinds.ResourceNodeHarvested, CreateEvent(node, playerId, -amount, "Harvested"));
            if (published && node.IsDepleted)
                PublishOrApply(ShelteredNetworkEventKinds.ResourceNodeDepleted, CreateEvent(node, playerId, 0, "Depleted"));
            return published;
        }

        public bool RegenerateNode(string nodeId, int amount)
        {
            ResourceNode node;
            if (!_registry.TryGet(nodeId, out node) || amount <= 0)
                return false;

            node.Remaining = node.Capacity > 0 ? Math.Min(node.Capacity, node.Remaining + amount) : node.Remaining + amount;
            node.IsDepleted = node.Remaining <= 0;
            node.LastUpdatedTick = ResolveWorldTick();
            return PublishOrApply(ShelteredNetworkEventKinds.ResourceNodeRegenerated, CreateEvent(node, 0, amount, "Regenerated"));
        }

        public void ApplyAuthoritative(string eventKind, ShelteredResourceNodeEvent nodeEvent)
        {
            if (_disposed || nodeEvent == null || nodeEvent.Node == null || string.IsNullOrEmpty(nodeEvent.Node.NodeId))
                return;

            _registry.Upsert(nodeEvent.Node);
            ShelteredWorldEvents.AppendAuthoritative(
                eventKind,
                !string.IsNullOrEmpty(nodeEvent.EventCorrelationId) ? nodeEvent.EventCorrelationId : nodeEvent.Node.NodeId,
                ToPayloadJson(nodeEvent),
                nodeEvent.PlayerId,
                NetworkDefaults.UnassignedPeerId);
            Raise(ResourceNodeEventApplied, eventKind, nodeEvent.Copy());
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ShelteredMultiplayerNetworkEvents.IntentReceived -= OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived -= OnAuthoritativeReceived;
            _disposed = true;
        }

        private bool PublishOrApply(string eventKind, ShelteredResourceNodeEvent nodeEvent)
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
            {
                ApplyAuthoritative(eventKind, nodeEvent);
                return true;
            }

            if (context.Mode == ShelteredMultiplayerSessionMode.Host)
            {
                ApplyAuthoritative(eventKind, nodeEvent);
                if (ShelteredMultiplayerNetworkEvents.IsAvailable)
                    return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(ToGameplayEvent(eventKind, nodeEvent));
                return true;
            }

            return ShelteredMultiplayerNetworkEvents.PublishIntent(ToGameplayEvent(eventKind, nodeEvent));
        }

        private void OnIntentReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null || !IsResourceNodeEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredMultiplayerSessionContext sessionContext = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (sessionContext == null || sessionContext.Mode != ShelteredMultiplayerSessionMode.Host)
            {
                context.Reject("Resource node intents require host authority.");
                return;
            }

            context.Accept(context.GameplayEvent.Copy());
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null || !IsResourceNodeEventKind(context.GameplayEvent.EventKind))
                return;

            ApplyAuthoritative(context.GameplayEvent.EventKind, FromGameplayEvent(context.GameplayEvent));
        }

        private static ShelteredResourceNodeEvent CreateEvent(ResourceNode node, int playerId, int delta, string reason)
        {
            ShelteredResourceNodeEvent nodeEvent = new ShelteredResourceNodeEvent();
            nodeEvent.Node = node.Copy();
            nodeEvent.PlayerId = playerId;
            nodeEvent.Delta = delta;
            nodeEvent.WorldTick = node.LastUpdatedTick;
            nodeEvent.Reason = reason ?? string.Empty;
            nodeEvent.EventCorrelationId = (reason ?? "Resource") + ":" + node.NodeId + ":" + node.LastUpdatedTick.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return nodeEvent;
        }

        private static ShelteredNetworkGameplayEvent ToGameplayEvent(string eventKind, ShelteredResourceNodeEvent nodeEvent)
        {
            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = eventKind ?? string.Empty;
            gameplayEvent.ActorId = nodeEvent.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            gameplayEvent.TargetId = nodeEvent.Node.NodeId;
            gameplayEvent.CorrelationId = nodeEvent.EventCorrelationId;
            gameplayEvent.GridX = nodeEvent.Node.GridX;
            gameplayEvent.GridY = nodeEvent.Node.GridY;
            gameplayEvent.DisplayName = nodeEvent.Node.Kind;
            gameplayEvent.Details = ToDetails(nodeEvent);
            return gameplayEvent;
        }

        private static ShelteredResourceNodeEvent FromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            // Compact pipe format avoids a second XML codec for the provisional resource-node layer.
            string[] parts = (gameplayEvent.Details ?? string.Empty).Split('|');
            ResourceNode node = new ResourceNode();
            node.NodeId = parts.Length > 0 ? parts[0] : gameplayEvent.TargetId;
            node.Kind = parts.Length > 1 ? parts[1] : gameplayEvent.DisplayName;
            node.GridX = ReadInt(parts, 2, gameplayEvent.GridX);
            node.GridY = ReadInt(parts, 3, gameplayEvent.GridY);
            node.Capacity = ReadInt(parts, 4, 0);
            node.Remaining = ReadInt(parts, 5, 0);
            node.RegenPerDay = ReadInt(parts, 6, 0);
            node.OwnerPlayerId = ReadInt(parts, 7, 0);
            node.OwnerFactionId = parts.Length > 8 ? parts[8] : string.Empty;
            node.DiscoveredBy = parts.Length > 9 ? parts[9] : string.Empty;
            node.IsDepleted = ReadBool(parts, 10);
            node.LastUpdatedTick = ReadLong(parts, 11, 0);

            ShelteredResourceNodeEvent nodeEvent = new ShelteredResourceNodeEvent();
            nodeEvent.Node = node;
            nodeEvent.PlayerId = ReadInt(parts, 12, 0);
            nodeEvent.Delta = ReadInt(parts, 13, 0);
            nodeEvent.WorldTick = ReadLong(parts, 14, node.LastUpdatedTick);
            nodeEvent.Reason = parts.Length > 15 ? parts[15] : string.Empty;
            nodeEvent.EventCorrelationId = gameplayEvent.CorrelationId;
            return nodeEvent;
        }

        private static string ToDetails(ShelteredResourceNodeEvent nodeEvent)
        {
            ResourceNode node = nodeEvent.Node;
            return Escape(node.NodeId) + "|"
                + Escape(node.Kind) + "|"
                + node.GridX + "|"
                + node.GridY + "|"
                + node.Capacity + "|"
                + node.Remaining + "|"
                + node.RegenPerDay + "|"
                + node.OwnerPlayerId + "|"
                + Escape(node.OwnerFactionId) + "|"
                + Escape(node.DiscoveredBy) + "|"
                + (node.IsDepleted ? "1" : "0") + "|"
                + node.LastUpdatedTick + "|"
                + nodeEvent.PlayerId + "|"
                + nodeEvent.Delta + "|"
                + nodeEvent.WorldTick + "|"
                + Escape(nodeEvent.Reason);
        }

        private static string ToPayloadJson(ShelteredResourceNodeEvent nodeEvent)
        {
            ResourceNode node = nodeEvent.Node;
            return "{\"nodeId\":\"" + EscapeJson(node.NodeId) + "\",\"kind\":\"" + EscapeJson(node.Kind)
                + "\",\"gridX\":" + node.GridX + ",\"gridY\":" + node.GridY
                + ",\"capacity\":" + node.Capacity + ",\"remaining\":" + node.Remaining
                + ",\"regenPerDay\":" + node.RegenPerDay + ",\"ownerPlayerId\":" + node.OwnerPlayerId
                + ",\"ownerFactionId\":\"" + EscapeJson(node.OwnerFactionId) + "\",\"isDepleted\":"
                + (node.IsDepleted ? "true" : "false") + "}";
        }

        private static bool IsResourceNodeEventKind(string eventKind)
        {
            return string.Equals(eventKind, ShelteredNetworkEventKinds.ResourceNodeGenerated, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.ResourceNodeClaimed, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.ResourceNodeHarvested, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.ResourceNodeDepleted, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.ResourceNodeRegenerated, StringComparison.Ordinal);
        }

        private static long ResolveWorldTick()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null ? context.WorldTick : 0;
        }

        private static int ReadInt(string[] parts, int index, int fallback)
        {
            int value;
            return parts.Length > index && int.TryParse(parts[index], out value) ? value : fallback;
        }

        private static long ReadLong(string[] parts, int index, long fallback)
        {
            long value;
            return parts.Length > index && long.TryParse(parts[index], out value) ? value : fallback;
        }

        private static bool ReadBool(string[] parts, int index)
        {
            return parts.Length > index && parts[index] == "1";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("|", "/");
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void Raise(Action<string, ShelteredResourceNodeEvent> handler, string kind, ShelteredResourceNodeEvent value)
        {
            if (handler != null)
                handler(kind, value);
        }
    }
}
