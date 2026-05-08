using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace ShelteredAPI.Networking.Locations
{
    internal static class ShelteredLocationEvents
    {
        public static bool IsLocationEventKind(string eventKind)
        {
            return string.Equals(eventKind, ShelteredNetworkEventKinds.LocationGenerated, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.LocationDiscovered, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.LocationLootGenerated, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.LocationLootTaken, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.LocationDepleted, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.LocationCorrected, StringComparison.Ordinal);
        }

        public static ShelteredNetworkGameplayEvent ToGameplayEvent(string eventKind, ShelteredLocationEvent locationEvent)
        {
            if (locationEvent == null)
                throw new ArgumentNullException("locationEvent");

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = eventKind ?? string.Empty;
            gameplayEvent.ActorId = locationEvent.PlayerId.ToString(CultureInfo.InvariantCulture);
            gameplayEvent.TargetId = locationEvent.LocationId ?? string.Empty;
            gameplayEvent.CorrelationId = !string.IsNullOrEmpty(locationEvent.EventCorrelationId)
                ? locationEvent.EventCorrelationId
                : locationEvent.LocationId;
            gameplayEvent.Details = ToDetailsXml(locationEvent);
            gameplayEvent.GridX = locationEvent.GridX;
            gameplayEvent.GridY = locationEvent.GridY;
            return gameplayEvent;
        }

        public static ShelteredLocationEvent FromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null)
                throw new ArgumentNullException("gameplayEvent");

            ShelteredLocationEvent locationEvent = FromDetailsXml(gameplayEvent.Details);
            if (string.IsNullOrEmpty(locationEvent.LocationId))
                locationEvent.LocationId = gameplayEvent.TargetId ?? string.Empty;
            if (locationEvent.GridX == 0)
                locationEvent.GridX = gameplayEvent.GridX;
            if (locationEvent.GridY == 0)
                locationEvent.GridY = gameplayEvent.GridY;
            if (locationEvent.PlayerId <= 0)
            {
                int playerId;
                if (int.TryParse(gameplayEvent.ActorId, NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId))
                    locationEvent.PlayerId = playerId;
            }

            return locationEvent;
        }

        public static string ToPayloadJson(ShelteredLocationEvent locationEvent)
        {
            if (locationEvent == null)
                return "{}";

            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            AppendJsonString(builder, "locationId", locationEvent.LocationId);
            AppendJsonInt(builder, "gridX", locationEvent.GridX);
            AppendJsonInt(builder, "gridY", locationEvent.GridY);
            AppendJsonString(builder, "locationKind", locationEvent.LocationKind);
            AppendJsonString(builder, "seedStreamName", locationEvent.SeedStreamName);
            AppendJsonLong(builder, "worldTick", locationEvent.WorldTick);
            AppendJsonInt(builder, "playerId", locationEvent.PlayerId);
            AppendJsonBool(builder, "isGenerated", locationEvent.IsGenerated);
            AppendJsonBool(builder, "isSearched", locationEvent.IsSearched);
            AppendJsonBool(builder, "isDepleted", locationEvent.IsDepleted);
            AppendJsonString(builder, "remainingLootSummaryJson", locationEvent.RemainingLootSummaryJson);
            AppendJsonString(builder, "reason", locationEvent.Reason);
            builder.Append("}");
            return builder.ToString();
        }

        private static string ToDetailsXml(ShelteredLocationEvent locationEvent)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = false;

            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
                {
                    writer.WriteStartElement("LocationEvent");
                    writer.WriteAttributeString("locationId", locationEvent.LocationId ?? string.Empty);
                    writer.WriteAttributeString("gridX", locationEvent.GridX.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("gridY", locationEvent.GridY.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("locationKind", locationEvent.LocationKind ?? string.Empty);
                    writer.WriteAttributeString("seedStreamName", locationEvent.SeedStreamName ?? string.Empty);
                    writer.WriteAttributeString("worldTick", locationEvent.WorldTick.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("playerId", locationEvent.PlayerId.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("isGenerated", locationEvent.IsGenerated ? "true" : "false");
                    writer.WriteAttributeString("isSearched", locationEvent.IsSearched ? "true" : "false");
                    writer.WriteAttributeString("isDepleted", locationEvent.IsDepleted ? "true" : "false");
                    writer.WriteAttributeString("remainingLootSummaryJson", locationEvent.RemainingLootSummaryJson ?? string.Empty);
                    writer.WriteAttributeString("reason", locationEvent.Reason ?? string.Empty);
                    writer.WriteAttributeString("eventCorrelationId", locationEvent.EventCorrelationId ?? string.Empty);
                    writer.WriteStartElement("Loot");
                    for (int i = 0; locationEvent.Loot != null && i < locationEvent.Loot.Count; i++)
                    {
                        LootItemRecord loot = locationEvent.Loot[i];
                        if (loot == null)
                            continue;

                        writer.WriteStartElement("Item");
                        if (loot.VanillaItemTypeInt.HasValue)
                            writer.WriteAttributeString("vanillaItemTypeInt", loot.VanillaItemTypeInt.Value.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("customItemId", loot.CustomItemId ?? string.Empty);
                        writer.WriteAttributeString("count", loot.Count.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("source", loot.Source ?? string.Empty);
                        writer.WriteAttributeString("takenByPlayerId", loot.TakenByPlayerId.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("takenTick", loot.TakenTick.ToString(CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                return stringWriter.ToString();
            }
        }

        private static ShelteredLocationEvent FromDetailsXml(string detailsXml)
        {
            ShelteredLocationEvent locationEvent = new ShelteredLocationEvent();
            if (string.IsNullOrEmpty(detailsXml))
                return locationEvent;

            XmlDocument document = new XmlDocument();
            using (StringReader stringReader = new StringReader(detailsXml))
            {
                using (XmlReader reader = XmlReader.Create(stringReader, CreateReaderSettings()))
                {
                    document.Load(reader);
                }
            }

            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "LocationEvent")
                return locationEvent;

            locationEvent.LocationId = ReadAttribute(root, "locationId");
            locationEvent.GridX = ReadInt(root, "gridX", 0);
            locationEvent.GridY = ReadInt(root, "gridY", 0);
            locationEvent.LocationKind = ReadAttribute(root, "locationKind");
            locationEvent.SeedStreamName = ReadAttribute(root, "seedStreamName");
            locationEvent.WorldTick = ReadLong(root, "worldTick", 0);
            locationEvent.PlayerId = ReadInt(root, "playerId", 0);
            locationEvent.IsGenerated = ReadBool(root, "isGenerated");
            locationEvent.IsSearched = ReadBool(root, "isSearched");
            locationEvent.IsDepleted = ReadBool(root, "isDepleted");
            locationEvent.RemainingLootSummaryJson = ReadAttribute(root, "remainingLootSummaryJson");
            locationEvent.Reason = ReadAttribute(root, "reason");
            locationEvent.EventCorrelationId = ReadAttribute(root, "eventCorrelationId");

            List<LootItemRecord> loot = new List<LootItemRecord>();
            XmlNodeList items = root.GetElementsByTagName("Item");
            for (int i = 0; i < items.Count; i++)
            {
                XmlElement item = items[i] as XmlElement;
                if (item == null)
                    continue;

                LootItemRecord record = new LootItemRecord();
                string vanilla = ReadAttribute(item, "vanillaItemTypeInt");
                int vanillaInt;
                if (int.TryParse(vanilla, NumberStyles.Integer, CultureInfo.InvariantCulture, out vanillaInt))
                    record.VanillaItemTypeInt = vanillaInt;
                record.CustomItemId = ReadAttribute(item, "customItemId");
                record.Count = ReadInt(item, "count", 0);
                record.Source = ReadAttribute(item, "source");
                record.TakenByPlayerId = ReadInt(item, "takenByPlayerId", 0);
                record.TakenTick = ReadLong(item, "takenTick", 0);
                loot.Add(record);
            }

            locationEvent.Loot = loot;
            return locationEvent;
        }

        private static XmlReaderSettings CreateReaderSettings()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ProhibitDtd = true;
            settings.XmlResolver = null;
            return settings;
        }

        private static string ReadAttribute(XmlElement element, string attributeName)
        {
            return element != null && element.HasAttribute(attributeName)
                ? element.GetAttribute(attributeName) ?? string.Empty
                : string.Empty;
        }

        private static int ReadInt(XmlElement element, string attributeName, int fallback)
        {
            int value;
            return int.TryParse(ReadAttribute(element, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static long ReadLong(XmlElement element, string attributeName, long fallback)
        {
            long value;
            return long.TryParse(ReadAttribute(element, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool ReadBool(XmlElement element, string attributeName)
        {
            return string.Equals(ReadAttribute(element, attributeName), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendJsonString(StringBuilder builder, string name, string value)
        {
            AppendJsonName(builder, name);
            builder.Append("\"").Append(ShelteredLocationLootDiagnostics.EscapeJson(value)).Append("\"");
        }

        private static void AppendJsonInt(StringBuilder builder, string name, int value)
        {
            AppendJsonName(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJsonLong(StringBuilder builder, string name, long value)
        {
            AppendJsonName(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJsonBool(StringBuilder builder, string name, bool value)
        {
            AppendJsonName(builder, name);
            builder.Append(value ? "true" : "false");
        }

        private static void AppendJsonName(StringBuilder builder, string name)
        {
            if (builder.Length > 1)
                builder.Append(",");
            builder.Append("\"").Append(name).Append("\":");
        }
    }
}
