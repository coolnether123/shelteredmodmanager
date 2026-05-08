using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using ModAPI.Networking;
using ShelteredAPI.Bunkers;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Persistence
{
    internal sealed class ShelteredMultiplayerWorldSnapshot
    {
        public int Version = ShelteredMultiplayerPersistenceKeys.SnapshotSchemaVersion;
        public string SessionId = string.Empty;
        public int MasterSeed;
        public long WorldTick;
        public string CompatibilityHash = ShelteredMultiplayerPersistenceKeys.CompatibilityHashUnknown;
        public long EventJournalCursorTick;
        public string EventJournalCursorEventId = string.Empty;

        public readonly List<ShelteredMultiplayerSnapshotBunkerAssignment> BunkerAssignments =
            new List<ShelteredMultiplayerSnapshotBunkerAssignment>();
        public readonly List<ShelteredMultiplayerSnapshotMapEntity> MapEntities =
            new List<ShelteredMultiplayerSnapshotMapEntity>();
        public readonly List<ShelteredMultiplayerSnapshotTravelState> ActiveTravel =
            new List<ShelteredMultiplayerSnapshotTravelState>();
        public readonly List<ShelteredMultiplayerSnapshotTradeState> TradeStates =
            new List<ShelteredMultiplayerSnapshotTradeState>();
        public readonly List<ShelteredMultiplayerSnapshotWorldEvent> RetainedEvents =
            new List<ShelteredMultiplayerSnapshotWorldEvent>();
        public readonly List<ShelteredMultiplayerSnapshotKeyValue> MapKnowledge =
            new List<ShelteredMultiplayerSnapshotKeyValue>();
        public readonly List<ShelteredMultiplayerSnapshotKeyValue> RaidStates =
            new List<ShelteredMultiplayerSnapshotKeyValue>();
        public readonly List<ShelteredMultiplayerSnapshotKeyValue> SettlementStates =
            new List<ShelteredMultiplayerSnapshotKeyValue>();
        public readonly List<ShelteredMultiplayerSnapshotKeyValue> LocationLootStates =
            new List<ShelteredMultiplayerSnapshotKeyValue>();

        public bool IsUsable
        {
            get { return Version > 0 && !string.IsNullOrEmpty(SessionId); }
        }

        public string ToXml()
        {
            StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = false;

            using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
            {
                writer.WriteStartElement("ShelteredMultiplayerWorldSnapshot");
                WriteAttribute(writer, "version", Version);
                WriteAttribute(writer, "sessionId", SessionId);
                WriteAttribute(writer, "masterSeed", MasterSeed);
                WriteAttribute(writer, "worldTick", WorldTick);
                WriteAttribute(writer, "compatibilityHash", CompatibilityHash);
                WriteAttribute(writer, "eventCursorTick", EventJournalCursorTick);
                writer.WriteAttributeString("eventCursorId", EventJournalCursorEventId ?? string.Empty);

                WriteBunkers(writer);
                WriteMapEntities(writer);
                WriteTravels(writer);
                WriteTrades(writer);
                WriteEvents(writer);
                WriteKeyValues(writer, "MapKnowledge", MapKnowledge);
                WriteKeyValues(writer, "RaidStates", RaidStates);
                WriteKeyValues(writer, "SettlementStates", SettlementStates);
                WriteKeyValues(writer, "LocationLootStates", LocationLootStates);

                writer.WriteEndElement();
            }

            return stringWriter.ToString();
        }

        public static bool TryFromXml(string xml, out ShelteredMultiplayerWorldSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;

            if (string.IsNullOrEmpty(xml))
            {
                error = "Snapshot payload is empty.";
                return false;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                using (StringReader stringReader = new StringReader(xml))
                {
                    using (XmlReader reader = XmlReader.Create(stringReader, CreateReaderSettings()))
                    {
                        document.Load(reader);
                    }
                }

                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "ShelteredMultiplayerWorldSnapshot")
                {
                    error = "Snapshot root element is invalid.";
                    return false;
                }

                ShelteredMultiplayerWorldSnapshot result = new ShelteredMultiplayerWorldSnapshot();
                result.Version = ReadInt(root, "version", 0);
                if (result.Version <= 0 || result.Version > ShelteredMultiplayerPersistenceKeys.SnapshotSchemaVersion)
                {
                    error = "Snapshot schema version is not supported.";
                    return false;
                }

                result.SessionId = ReadAttribute(root, "sessionId");
                result.MasterSeed = ReadInt(root, "masterSeed", 0);
                result.WorldTick = ReadLong(root, "worldTick", 0);
                result.CompatibilityHash = ReadAttribute(root, "compatibilityHash");
                if (string.IsNullOrEmpty(result.CompatibilityHash))
                    result.CompatibilityHash = ShelteredMultiplayerPersistenceKeys.CompatibilityHashUnknown;
                result.EventJournalCursorTick = ReadLong(root, "eventCursorTick", 0);
                result.EventJournalCursorEventId = ReadAttribute(root, "eventCursorId");

                ReadBunkers(root, result);
                ReadMapEntities(root, result);
                ReadTravels(root, result);
                ReadTrades(root, result);
                ReadEvents(root, result);
                ReadKeyValues(root, "MapKnowledge", result.MapKnowledge);
                ReadKeyValues(root, "RaidStates", result.RaidStates);
                ReadKeyValues(root, "SettlementStates", result.SettlementStates);
                ReadKeyValues(root, "LocationLootStates", result.LocationLootStates);

                if (!result.IsUsable)
                {
                    error = "Snapshot is missing required session data.";
                    return false;
                }

                snapshot = result;
                return true;
            }
            catch (Exception ex)
            {
                error = "Malformed multiplayer world snapshot: " + ex.Message;
                return false;
            }
        }

        private void WriteBunkers(XmlWriter writer)
        {
            writer.WriteStartElement("BunkerAssignments");
            for (int i = 0; i < BunkerAssignments.Count; i++)
            {
                ShelteredMultiplayerSnapshotBunkerAssignment item = BunkerAssignments[i];
                writer.WriteStartElement("Bunker");
                WriteAttribute(writer, "networkPeerId", item.NetworkPeerId);
                WriteAttribute(writer, "playerId", item.PlayerId);
                WriteAttribute(writer, "bunkerOwnerId", item.BunkerOwnerId);
                WriteAttribute(writer, "x", item.X);
                WriteAttribute(writer, "y", item.Y);
                writer.WriteAttributeString("displayName", item.DisplayName ?? string.Empty);
                WriteAttribute(writer, "isOnline", item.IsOnline);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void WriteMapEntities(XmlWriter writer)
        {
            writer.WriteStartElement("MapEntities");
            for (int i = 0; i < MapEntities.Count; i++)
            {
                ShelteredMultiplayerSnapshotMapEntity item = MapEntities[i];
                writer.WriteStartElement("Entity");
                writer.WriteAttributeString("id", item.EntityId ?? string.Empty);
                writer.WriteAttributeString("kind", item.Kind ?? string.Empty);
                WriteAttribute(writer, "ownerPlayerId", item.OwnerPlayerId);
                WriteAttribute(writer, "ownerPeerId", item.OwnerPeerId);
                WriteAttribute(writer, "bunkerOwnerId", item.BunkerOwnerId);
                writer.WriteAttributeString("displayName", item.DisplayName ?? string.Empty);
                WriteAttribute(writer, "worldX", item.WorldX);
                WriteAttribute(writer, "worldY", item.WorldY);
                WriteAttribute(writer, "mapX", item.MapX);
                WriteAttribute(writer, "mapY", item.MapY);
                WriteAttribute(writer, "mapZ", item.MapZ);
                WriteAttribute(writer, "gridX", item.GridX);
                WriteAttribute(writer, "gridY", item.GridY);
                WriteAttribute(writer, "isOnline", item.IsOnline);
                WriteAttribute(writer, "isVisible", item.IsVisible);
                writer.WriteAttributeString("state", item.State ?? string.Empty);
                WriteAttribute(writer, "updatedTick", item.UpdatedWorldTick);
                writer.WriteString(item.PayloadJson ?? string.Empty);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void WriteTravels(XmlWriter writer)
        {
            writer.WriteStartElement("ActiveTravel");
            for (int i = 0; i < ActiveTravel.Count; i++)
            {
                ShelteredMultiplayerSnapshotTravelState item = ActiveTravel[i];
                writer.WriteStartElement("Travel");
                writer.WriteAttributeString("id", item.TravelId ?? string.Empty);
                writer.WriteAttributeString("state", item.State ?? string.Empty);
                WriteAttribute(writer, "ownerPlayerId", item.OwnerPlayerId);
                WriteAttribute(writer, "ownerPeerId", item.OwnerPeerId);
                WriteAttribute(writer, "partyId", item.PartyId);
                WriteAttribute(writer, "lastTick", item.LastAuthoritativeTick);
                writer.WriteAttributeString("lastEventId", item.LastEventId ?? string.Empty);
                WriteAttribute(writer, "gridX", item.LastPredictedGridX);
                WriteAttribute(writer, "gridY", item.LastPredictedGridY);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void WriteTrades(XmlWriter writer)
        {
            writer.WriteStartElement("TradeStates");
            for (int i = 0; i < TradeStates.Count; i++)
            {
                ShelteredMultiplayerSnapshotTradeState item = TradeStates[i];
                writer.WriteStartElement("Trade");
                writer.WriteAttributeString("id", item.TradeId ?? string.Empty);
                writer.WriteAttributeString("sourceOwnerId", item.SourceOwnerId ?? string.Empty);
                writer.WriteAttributeString("targetOwnerId", item.TargetOwnerId ?? string.Empty);
                writer.WriteAttributeString("state", item.State ?? string.Empty);
                WriteAttribute(writer, "lastTick", item.LastAuthoritativeTick);
                writer.WriteAttributeString("lastEventId", item.LastEventId ?? string.Empty);
                writer.WriteAttributeString("lastEventKind", item.LastEventKind ?? string.Empty);
                writer.WriteAttributeString("failureReason", item.FailureReason ?? string.Empty);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void WriteEvents(XmlWriter writer)
        {
            writer.WriteStartElement("RetainedEvents");
            for (int i = 0; i < RetainedEvents.Count; i++)
            {
                ShelteredMultiplayerSnapshotWorldEvent item = RetainedEvents[i];
                writer.WriteStartElement("Event");
                writer.WriteAttributeString("id", item.EventId ?? string.Empty);
                writer.WriteAttributeString("kind", item.EventKind ?? string.Empty);
                writer.WriteAttributeString("correlationId", item.CorrelationId ?? string.Empty);
                WriteAttribute(writer, "sourcePlayerId", item.SourcePlayerId);
                WriteAttribute(writer, "sourcePeerId", item.SourceNetworkPeerId);
                WriteAttribute(writer, "worldTick", item.WorldTick);
                WriteAttribute(writer, "delta", item.WorldDeltaSeconds);
                WriteAttribute(writer, "authoritative", item.Authoritative);
                WriteAttribute(writer, "createdUtcTicks", item.CreatedUtcTicks);
                writer.WriteString(item.PayloadJson ?? string.Empty);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteKeyValues(XmlWriter writer, string elementName, IList<ShelteredMultiplayerSnapshotKeyValue> values)
        {
            writer.WriteStartElement(elementName);
            for (int i = 0; i < values.Count; i++)
            {
                writer.WriteStartElement("Item");
                writer.WriteAttributeString("key", values[i].Key ?? string.Empty);
                writer.WriteString(values[i].Value ?? string.Empty);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadBunkers(XmlElement root, ShelteredMultiplayerWorldSnapshot snapshot)
        {
            XmlNodeList nodes = root.GetElementsByTagName("Bunker");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement e = nodes[i] as XmlElement;
                if (e == null)
                    continue;
                snapshot.BunkerAssignments.Add(new ShelteredMultiplayerSnapshotBunkerAssignment
                {
                    NetworkPeerId = ReadInt(e, "networkPeerId", NetworkDefaults.UnassignedPeerId),
                    PlayerId = ReadInt(e, "playerId", 0),
                    BunkerOwnerId = ReadInt(e, "bunkerOwnerId", 0),
                    X = ReadFloat(e, "x", 0f),
                    Y = ReadFloat(e, "y", 0f),
                    DisplayName = ReadAttribute(e, "displayName"),
                    IsOnline = ReadBool(e, "isOnline", false)
                });
            }
        }

        private static void ReadMapEntities(XmlElement root, ShelteredMultiplayerWorldSnapshot snapshot)
        {
            XmlNodeList nodes = root.GetElementsByTagName("Entity");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement e = nodes[i] as XmlElement;
                if (e == null)
                    continue;
                snapshot.MapEntities.Add(new ShelteredMultiplayerSnapshotMapEntity
                {
                    EntityId = ReadAttribute(e, "id"),
                    Kind = ReadAttribute(e, "kind"),
                    OwnerPlayerId = ReadInt(e, "ownerPlayerId", 0),
                    OwnerPeerId = ReadInt(e, "ownerPeerId", NetworkDefaults.UnassignedPeerId),
                    BunkerOwnerId = ReadInt(e, "bunkerOwnerId", 0),
                    DisplayName = ReadAttribute(e, "displayName"),
                    WorldX = ReadFloat(e, "worldX", 0f),
                    WorldY = ReadFloat(e, "worldY", 0f),
                    MapX = ReadFloat(e, "mapX", 0f),
                    MapY = ReadFloat(e, "mapY", 0f),
                    MapZ = ReadFloat(e, "mapZ", 0f),
                    GridX = ReadInt(e, "gridX", 0),
                    GridY = ReadInt(e, "gridY", 0),
                    IsOnline = ReadBool(e, "isOnline", false),
                    IsVisible = ReadBool(e, "isVisible", true),
                    State = ReadAttribute(e, "state"),
                    UpdatedWorldTick = ReadLong(e, "updatedTick", 0),
                    PayloadJson = e.InnerText ?? string.Empty
                });
            }
        }

        private static void ReadTravels(XmlElement root, ShelteredMultiplayerWorldSnapshot snapshot)
        {
            XmlNodeList nodes = root.GetElementsByTagName("Travel");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement e = nodes[i] as XmlElement;
                if (e == null)
                    continue;
                snapshot.ActiveTravel.Add(new ShelteredMultiplayerSnapshotTravelState
                {
                    TravelId = ReadAttribute(e, "id"),
                    State = ReadAttribute(e, "state"),
                    OwnerPlayerId = ReadInt(e, "ownerPlayerId", 0),
                    OwnerPeerId = ReadInt(e, "ownerPeerId", NetworkDefaults.UnassignedPeerId),
                    PartyId = ReadInt(e, "partyId", 0),
                    LastAuthoritativeTick = ReadLong(e, "lastTick", 0),
                    LastEventId = ReadAttribute(e, "lastEventId"),
                    LastPredictedGridX = ReadInt(e, "gridX", 0),
                    LastPredictedGridY = ReadInt(e, "gridY", 0)
                });
            }
        }

        private static void ReadTrades(XmlElement root, ShelteredMultiplayerWorldSnapshot snapshot)
        {
            XmlNodeList nodes = root.GetElementsByTagName("Trade");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement e = nodes[i] as XmlElement;
                if (e == null)
                    continue;
                snapshot.TradeStates.Add(new ShelteredMultiplayerSnapshotTradeState
                {
                    TradeId = ReadAttribute(e, "id"),
                    SourceOwnerId = ReadAttribute(e, "sourceOwnerId"),
                    TargetOwnerId = ReadAttribute(e, "targetOwnerId"),
                    State = ReadAttribute(e, "state"),
                    LastAuthoritativeTick = ReadLong(e, "lastTick", 0),
                    LastEventId = ReadAttribute(e, "lastEventId"),
                    LastEventKind = ReadAttribute(e, "lastEventKind"),
                    FailureReason = ReadAttribute(e, "failureReason")
                });
            }
        }

        private static void ReadEvents(XmlElement root, ShelteredMultiplayerWorldSnapshot snapshot)
        {
            XmlNodeList nodes = root.GetElementsByTagName("Event");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement e = nodes[i] as XmlElement;
                if (e == null)
                    continue;
                snapshot.RetainedEvents.Add(new ShelteredMultiplayerSnapshotWorldEvent
                {
                    EventId = ReadAttribute(e, "id"),
                    EventKind = ReadAttribute(e, "kind"),
                    CorrelationId = ReadAttribute(e, "correlationId"),
                    SourcePlayerId = ReadInt(e, "sourcePlayerId", 0),
                    SourceNetworkPeerId = ReadInt(e, "sourcePeerId", NetworkDefaults.UnassignedPeerId),
                    WorldTick = ReadLong(e, "worldTick", 0),
                    WorldDeltaSeconds = ReadFloat(e, "delta", 0f),
                    Authoritative = ReadBool(e, "authoritative", true),
                    CreatedUtcTicks = ReadLong(e, "createdUtcTicks", 0),
                    PayloadJson = e.InnerText ?? string.Empty
                });
            }
        }

        private static void ReadKeyValues(XmlElement root, string elementName, IList<ShelteredMultiplayerSnapshotKeyValue> values)
        {
            XmlNodeList sections = root.GetElementsByTagName(elementName);
            if (sections.Count == 0)
                return;

            XmlElement section = sections[0] as XmlElement;
            if (section == null)
                return;

            XmlNodeList items = section.GetElementsByTagName("Item");
            for (int i = 0; i < items.Count; i++)
            {
                XmlElement item = items[i] as XmlElement;
                if (item == null)
                    continue;
                values.Add(new ShelteredMultiplayerSnapshotKeyValue(ReadAttribute(item, "key"), item.InnerText ?? string.Empty));
            }
        }

        private static XmlReaderSettings CreateReaderSettings()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ProhibitDtd = true;
            settings.XmlResolver = null;
            return settings;
        }

        private static string ReadAttribute(XmlElement element, string name)
        {
            return element != null && element.HasAttribute(name) ? element.GetAttribute(name) ?? string.Empty : string.Empty;
        }

        private static int ReadInt(XmlElement element, string name, int fallback)
        {
            int value;
            return int.TryParse(ReadAttribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static long ReadLong(XmlElement element, string name, long fallback)
        {
            long value;
            return long.TryParse(ReadAttribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static float ReadFloat(XmlElement element, string name, float fallback)
        {
            float value;
            return float.TryParse(ReadAttribute(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool ReadBool(XmlElement element, string name, bool fallback)
        {
            bool value;
            return bool.TryParse(ReadAttribute(element, name), out value) ? value : fallback;
        }

        private static void WriteAttribute(XmlWriter writer, string name, int value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteAttribute(XmlWriter writer, string name, string value)
        {
            writer.WriteAttributeString(name, value ?? string.Empty);
        }

        private static void WriteAttribute(XmlWriter writer, string name, long value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteAttribute(XmlWriter writer, string name, float value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteAttribute(XmlWriter writer, string name, bool value)
        {
            writer.WriteAttributeString(name, value ? "true" : "false");
        }
    }

    internal sealed class ShelteredMultiplayerSnapshotBunkerAssignment
    {
        public int NetworkPeerId;
        public int PlayerId;
        public int BunkerOwnerId;
        public float X;
        public float Y;
        public string DisplayName = string.Empty;
        public bool IsOnline;

        public ShelteredMultiplayerBunkerAssignmentRecord ToRecord()
        {
            byte peerId = NetworkPeerId >= 0 && NetworkPeerId <= byte.MaxValue
                ? (byte)NetworkPeerId
                : NetworkDefaults.UnassignedPeerId;
            return new ShelteredMultiplayerBunkerAssignmentRecord(
                peerId,
                PlayerId,
                BunkerOwnerId,
                new Vector2(X, Y),
                DisplayName,
                IsOnline);
        }

        public static ShelteredMultiplayerSnapshotBunkerAssignment FromRecord(ShelteredMultiplayerBunkerAssignmentRecord record)
        {
            return new ShelteredMultiplayerSnapshotBunkerAssignment
            {
                NetworkPeerId = record != null ? record.NetworkPeerId : NetworkDefaults.UnassignedPeerId,
                PlayerId = record != null ? record.PlayerId : 0,
                BunkerOwnerId = record != null ? record.BunkerOwnerId : 0,
                X = record != null ? record.Position.x : 0f,
                Y = record != null ? record.Position.y : 0f,
                DisplayName = record != null ? record.DisplayName ?? string.Empty : string.Empty,
                IsOnline = record != null && record.IsOnline
            };
        }
    }

    internal sealed class ShelteredMultiplayerSnapshotMapEntity
    {
        public string EntityId = string.Empty;
        public string Kind = string.Empty;
        public int OwnerPlayerId;
        public int OwnerPeerId;
        public int BunkerOwnerId;
        public string DisplayName = string.Empty;
        public float WorldX;
        public float WorldY;
        public float MapX;
        public float MapY;
        public float MapZ;
        public int GridX;
        public int GridY;
        public bool IsOnline;
        public bool IsVisible;
        public string State = string.Empty;
        public string PayloadJson = string.Empty;
        public long UpdatedWorldTick;

        public ShelteredMapEntity ToEntity()
        {
            ShelteredMapEntityKind kind = ShelteredMapEntityKind.Unknown;
            try
            {
                if (!string.IsNullOrEmpty(Kind))
                    kind = (ShelteredMapEntityKind)Enum.Parse(typeof(ShelteredMapEntityKind), Kind, false);
            }
            catch
            {
                kind = ShelteredMapEntityKind.Unknown;
            }

            return new ShelteredMapEntity
            {
                EntityId = EntityId ?? string.Empty,
                Kind = kind,
                OwnerPlayerId = OwnerPlayerId,
                OwnerPeerId = OwnerPeerId >= 0 && OwnerPeerId <= byte.MaxValue ? (byte)OwnerPeerId : NetworkDefaults.UnassignedPeerId,
                BunkerOwnerId = BunkerOwnerId,
                DisplayName = DisplayName ?? string.Empty,
                WorldPosition = new Vector2(WorldX, WorldY),
                MapPixels = new Vector3(MapX, MapY, MapZ),
                GridX = GridX,
                GridY = GridY,
                IsOnline = IsOnline,
                IsVisible = IsVisible,
                State = State ?? string.Empty,
                PayloadJson = PayloadJson ?? string.Empty,
                UpdatedWorldTick = UpdatedWorldTick
            };
        }

        public static ShelteredMultiplayerSnapshotMapEntity FromEntity(ShelteredMapEntity entity)
        {
            return new ShelteredMultiplayerSnapshotMapEntity
            {
                EntityId = entity != null ? entity.EntityId ?? string.Empty : string.Empty,
                Kind = entity != null ? entity.Kind.ToString() : ShelteredMapEntityKind.Unknown.ToString(),
                OwnerPlayerId = entity != null ? entity.OwnerPlayerId : 0,
                OwnerPeerId = entity != null ? entity.OwnerPeerId : NetworkDefaults.UnassignedPeerId,
                BunkerOwnerId = entity != null ? entity.BunkerOwnerId : 0,
                DisplayName = entity != null ? entity.DisplayName ?? string.Empty : string.Empty,
                WorldX = entity != null ? entity.WorldPosition.x : 0f,
                WorldY = entity != null ? entity.WorldPosition.y : 0f,
                MapX = entity != null ? entity.MapPixels.x : 0f,
                MapY = entity != null ? entity.MapPixels.y : 0f,
                MapZ = entity != null ? entity.MapPixels.z : 0f,
                GridX = entity != null ? entity.GridX : 0,
                GridY = entity != null ? entity.GridY : 0,
                IsOnline = entity != null && entity.IsOnline,
                IsVisible = entity == null || entity.IsVisible,
                State = entity != null ? entity.State ?? string.Empty : string.Empty,
                PayloadJson = entity != null ? entity.PayloadJson ?? string.Empty : string.Empty,
                UpdatedWorldTick = entity != null ? entity.UpdatedWorldTick : 0
            };
        }
    }

    internal sealed class ShelteredMultiplayerSnapshotTravelState
    {
        public string TravelId = string.Empty;
        public int OwnerPlayerId;
        public int OwnerPeerId;
        public int PartyId;
        public string State = string.Empty;
        public long LastAuthoritativeTick;
        public string LastEventId = string.Empty;
        public int LastPredictedGridX;
        public int LastPredictedGridY;
    }

    internal sealed class ShelteredMultiplayerSnapshotTradeState
    {
        public string TradeId = string.Empty;
        public string SourceOwnerId = string.Empty;
        public string TargetOwnerId = string.Empty;
        public string State = string.Empty;
        public long LastAuthoritativeTick;
        public string LastEventId = string.Empty;
        public string LastEventKind = string.Empty;
        public string FailureReason = string.Empty;
    }

    internal sealed class ShelteredMultiplayerSnapshotWorldEvent
    {
        public string EventId = string.Empty;
        public string EventKind = string.Empty;
        public string CorrelationId = string.Empty;
        public int SourcePlayerId;
        public int SourceNetworkPeerId;
        public long WorldTick;
        public float WorldDeltaSeconds;
        public string PayloadJson = string.Empty;
        public bool Authoritative;
        public long CreatedUtcTicks;

        public ShelteredWorldEventRecord ToRecord()
        {
            return new ShelteredWorldEventRecord
            {
                EventId = EventId ?? string.Empty,
                EventKind = EventKind ?? string.Empty,
                CorrelationId = CorrelationId ?? string.Empty,
                SourcePlayerId = SourcePlayerId,
                SourceNetworkPeerId = SourceNetworkPeerId >= 0 && SourceNetworkPeerId <= byte.MaxValue
                    ? (byte)SourceNetworkPeerId
                    : NetworkDefaults.UnassignedPeerId,
                WorldTick = WorldTick,
                WorldDeltaSeconds = WorldDeltaSeconds,
                PayloadJson = PayloadJson ?? string.Empty,
                Authoritative = Authoritative,
                CreatedUtc = CreatedUtcTicks > 0 ? new DateTime(CreatedUtcTicks, DateTimeKind.Utc) : DateTime.UtcNow
            };
        }

        public static ShelteredMultiplayerSnapshotWorldEvent FromRecord(ShelteredWorldEventRecord record)
        {
            return new ShelteredMultiplayerSnapshotWorldEvent
            {
                EventId = record != null ? record.EventId ?? string.Empty : string.Empty,
                EventKind = record != null ? record.EventKind ?? string.Empty : string.Empty,
                CorrelationId = record != null ? record.CorrelationId ?? string.Empty : string.Empty,
                SourcePlayerId = record != null ? record.SourcePlayerId : 0,
                SourceNetworkPeerId = record != null ? record.SourceNetworkPeerId : NetworkDefaults.UnassignedPeerId,
                WorldTick = record != null ? record.WorldTick : 0,
                WorldDeltaSeconds = record != null ? record.WorldDeltaSeconds : 0f,
                PayloadJson = record != null ? record.PayloadJson ?? string.Empty : string.Empty,
                Authoritative = record == null || record.Authoritative,
                CreatedUtcTicks = record != null && record.CreatedUtc != DateTime.MinValue ? record.CreatedUtc.Ticks : 0
            };
        }
    }

    internal sealed class ShelteredMultiplayerSnapshotKeyValue
    {
        public ShelteredMultiplayerSnapshotKeyValue()
        {
            Key = string.Empty;
            Value = string.Empty;
        }

        public ShelteredMultiplayerSnapshotKeyValue(string key, string value)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Key;
        public string Value;
    }
}
