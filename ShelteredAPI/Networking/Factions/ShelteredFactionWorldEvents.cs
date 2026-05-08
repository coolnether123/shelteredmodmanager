using System;

namespace ShelteredAPI.Networking.Factions
{
    public sealed class ShelteredFactionWorldEvent
    {
        public ShelteredFactionWorldEvent()
        {
            EventId = string.Empty;
            EventKind = string.Empty;
            FactionId = string.Empty;
            PayloadJson = string.Empty;
        }

        public string EventId { get; set; }
        public string EventKind { get; set; }
        public long WorldTick { get; set; }
        public string FactionId { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public string PayloadJson { get; set; }
    }

    public static class ShelteredFactionWorldEvents
    {
        public static ShelteredFactionWorldEvent CreateMarkerEvent(
            string eventKind,
            string factionId,
            int gridX,
            int gridY,
            long worldTick,
            string payloadJson)
        {
            ShelteredFactionWorldEvent factionEvent = new ShelteredFactionWorldEvent();
            factionEvent.EventKind = eventKind ?? string.Empty;
            factionEvent.FactionId = factionId ?? string.Empty;
            factionEvent.GridX = gridX;
            factionEvent.GridY = gridY;
            factionEvent.WorldTick = worldTick;
            factionEvent.PayloadJson = payloadJson ?? string.Empty;
            factionEvent.EventId = "factionevent:" + (eventKind ?? string.Empty) + ":" + (factionId ?? string.Empty) + ":" + gridX + ":" + gridY + ":" + worldTick;
            return factionEvent;
        }
    }
}
