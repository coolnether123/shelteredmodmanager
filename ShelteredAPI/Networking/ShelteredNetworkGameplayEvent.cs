using ModAPI.Networking.Events;
using ModAPI.Networking.Serialization;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Sheltered-facing POCO for gameplay events that need network synchronization.
    /// EventKind is the stable subscription key, for example "Expedition.Started" or "Location.Searched".
    /// </summary>
    public sealed class ShelteredNetworkGameplayEvent
    {
        public const string EnvelopeEventName = "Sheltered.GameplayEvent";
        public const ushort LegacyVersion = 1;
        public const ushort CurrentVersion = 2;

        public ShelteredNetworkGameplayEvent()
        {
            EventKind = string.Empty;
            ActorId = string.Empty;
            TargetId = string.Empty;
            Details = string.Empty;
            PeerId = 255;
            BunkerOwnerId = -1;
            DisplayName = string.Empty;
            WorldPosition = Vector2.zero;
            MapPixels = Vector3.zero;
            GridX = 0;
            GridY = 0;
            IsOnline = true;
        }

        public string EventKind { get; set; }
        public string ActorId { get; set; }
        public string TargetId { get; set; }
        public string Details { get; set; }
        public int PeerId { get; set; }
        public int BunkerOwnerId { get; set; }
        public string DisplayName { get; set; }
        public Vector2 WorldPosition { get; set; }
        public Vector3 MapPixels { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public bool IsOnline { get; set; }
    }

    internal sealed class ShelteredNetworkGameplayEventSerializer
        : NetworkEventPayloadSerializer<ShelteredNetworkGameplayEvent>
    {
        private readonly bool _includeBunkerData;

        public ShelteredNetworkGameplayEventSerializer()
            : this(ShelteredNetworkGameplayEvent.CurrentVersion, true)
        {
        }

        private ShelteredNetworkGameplayEventSerializer(ushort version, bool includeBunkerData)
            : base(ShelteredNetworkGameplayEvent.EnvelopeEventName, version)
        {
            _includeBunkerData = includeBunkerData;
        }

        public static ShelteredNetworkGameplayEventSerializer CreateLegacy()
        {
            return new ShelteredNetworkGameplayEventSerializer(ShelteredNetworkGameplayEvent.LegacyVersion, false);
        }

        protected override void Write(ShelteredNetworkGameplayEvent value, ref BitWriter writer)
        {
            if (value == null)
                value = new ShelteredNetworkGameplayEvent();

            writer.WriteString(value.EventKind ?? string.Empty);
            writer.WriteString(value.ActorId ?? string.Empty);
            writer.WriteString(value.TargetId ?? string.Empty);
            writer.WriteString(value.Details ?? string.Empty);

            if (_includeBunkerData)
            {
                writer.WriteInt32(value.PeerId);
                writer.WriteInt32(value.BunkerOwnerId);
                writer.WriteString(value.DisplayName ?? string.Empty);
                writer.WriteInt32(ToNetworkCoordinate(value.WorldPosition.x));
                writer.WriteInt32(ToNetworkCoordinate(value.WorldPosition.y));
                writer.WriteInt32(ToNetworkCoordinate(value.MapPixels.x));
                writer.WriteInt32(ToNetworkCoordinate(value.MapPixels.y));
                writer.WriteInt32(ToNetworkCoordinate(value.MapPixels.z));
                writer.WriteInt32(value.GridX);
                writer.WriteInt32(value.GridY);
                writer.WriteBool(value.IsOnline);
            }
        }

        protected override ShelteredNetworkGameplayEvent Read(ref BitReader reader)
        {
            ShelteredNetworkGameplayEvent value = new ShelteredNetworkGameplayEvent();
            value.EventKind = reader.ReadString();
            value.ActorId = reader.ReadString();
            value.TargetId = reader.ReadString();
            value.Details = reader.ReadString();

            if (_includeBunkerData && reader.Remaining > 0)
            {
                value.PeerId = reader.ReadInt32();
                value.BunkerOwnerId = reader.ReadInt32();
                value.DisplayName = reader.ReadString();
                value.WorldPosition = new Vector2(
                    FromNetworkCoordinate(reader.ReadInt32()),
                    FromNetworkCoordinate(reader.ReadInt32()));
                value.MapPixels = new Vector3(
                    FromNetworkCoordinate(reader.ReadInt32()),
                    FromNetworkCoordinate(reader.ReadInt32()),
                    FromNetworkCoordinate(reader.ReadInt32()));
                value.GridX = reader.ReadInt32();
                value.GridY = reader.ReadInt32();
                value.IsOnline = reader.ReadBool();
            }

            return value;
        }

        private static int ToNetworkCoordinate(float value)
        {
            return (int)System.Math.Round(value * 1000f);
        }

        private static float FromNetworkCoordinate(int value)
        {
            return value / 1000f;
        }
    }
}
