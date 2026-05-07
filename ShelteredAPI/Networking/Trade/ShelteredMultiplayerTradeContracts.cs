using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace ShelteredAPI.Networking.Trade
{
    /// <summary>
    /// Data-only cargo line for future shared inventory and caravan movement.
    /// No storage mutation is performed by these contracts.
    /// </summary>
    public sealed class ShelteredTradeCargoDto
    {
        public ShelteredTradeCargoDto()
        {
            ItemId = string.Empty;
            SourceOwnerId = string.Empty;
            TargetOwnerId = string.Empty;
        }

        public string ItemId { get; set; }
        public int Count { get; set; }
        public string SourceOwnerId { get; set; }
        public string TargetOwnerId { get; set; }

        public ShelteredTradeCargoDto Copy()
        {
            ShelteredTradeCargoDto copy = new ShelteredTradeCargoDto();
            copy.ItemId = ItemId;
            copy.Count = Count;
            copy.SourceOwnerId = SourceOwnerId;
            copy.TargetOwnerId = TargetOwnerId;
            return copy;
        }
    }

    /// <summary>
    /// Shared-world trade/caravan event DTO carried inside ShelteredNetworkGameplayEvent.Details.
    /// EventId, CorrelationId, and WorldTick mirror the network envelope after routing.
    /// </summary>
    public sealed class ShelteredMultiplayerTradeEvent
    {
        private readonly List<ShelteredTradeCargoDto> _cargo = new List<ShelteredTradeCargoDto>();

        public ShelteredMultiplayerTradeEvent()
        {
            EventId = string.Empty;
            CorrelationId = string.Empty;
            EventKind = string.Empty;
            TradeId = string.Empty;
            SourceOwnerId = string.Empty;
            TargetOwnerId = string.Empty;
            RejectionReason = string.Empty;
        }

        public string EventId { get; set; }
        public string CorrelationId { get; set; }
        public uint WorldTick { get; set; }
        public string EventKind { get; set; }
        public string TradeId { get; set; }
        public string SourceOwnerId { get; set; }
        public string TargetOwnerId { get; set; }
        public string RejectionReason { get; set; }

        public IList<ShelteredTradeCargoDto> Cargo
        {
            get { return _cargo; }
        }

        public ShelteredMultiplayerTradeEvent Copy()
        {
            ShelteredMultiplayerTradeEvent copy = new ShelteredMultiplayerTradeEvent();
            copy.EventId = EventId;
            copy.CorrelationId = CorrelationId;
            copy.WorldTick = WorldTick;
            copy.EventKind = EventKind;
            copy.TradeId = TradeId;
            copy.SourceOwnerId = SourceOwnerId;
            copy.TargetOwnerId = TargetOwnerId;
            copy.RejectionReason = RejectionReason;

            for (int i = 0; i < _cargo.Count; i++)
                copy.Cargo.Add(_cargo[i] != null ? _cargo[i].Copy() : new ShelteredTradeCargoDto());

            return copy;
        }
    }

    public static class ShelteredMultiplayerTradeContractCodec
    {
        public static bool IsTradeEventKind(string eventKind)
        {
            return string.Equals(eventKind, ShelteredNetworkEventKinds.TradeOfferIntent, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TradeOfferAccepted, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TradeOfferRejected, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCaravanLaunched, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCaravanArrived, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCompleted, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCancelled, StringComparison.Ordinal);
        }

        public static ShelteredNetworkGameplayEvent ToGameplayEvent(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (tradeEvent == null)
                throw new ArgumentNullException("tradeEvent");

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventId = tradeEvent.EventId ?? string.Empty;
            gameplayEvent.CorrelationId = tradeEvent.CorrelationId ?? string.Empty;
            gameplayEvent.WorldTick = tradeEvent.WorldTick;
            gameplayEvent.EventKind = tradeEvent.EventKind ?? string.Empty;
            gameplayEvent.ActorId = tradeEvent.SourceOwnerId ?? string.Empty;
            gameplayEvent.TargetId = tradeEvent.TargetOwnerId ?? string.Empty;
            gameplayEvent.Details = ToDetailsXml(tradeEvent);
            return gameplayEvent;
        }

        public static ShelteredMultiplayerTradeEvent FromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null)
                throw new ArgumentNullException("gameplayEvent");

            ShelteredMultiplayerTradeEvent tradeEvent = FromDetailsXml(gameplayEvent.Details);
            tradeEvent.EventId = gameplayEvent.EventId ?? string.Empty;
            tradeEvent.CorrelationId = gameplayEvent.CorrelationId ?? string.Empty;
            tradeEvent.WorldTick = gameplayEvent.WorldTick;
            tradeEvent.EventKind = gameplayEvent.EventKind ?? string.Empty;

            if (string.IsNullOrEmpty(tradeEvent.SourceOwnerId))
                tradeEvent.SourceOwnerId = gameplayEvent.ActorId ?? string.Empty;
            if (string.IsNullOrEmpty(tradeEvent.TargetOwnerId))
                tradeEvent.TargetOwnerId = gameplayEvent.TargetId ?? string.Empty;

            return tradeEvent;
        }

        public static string ToDetailsXml(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (tradeEvent == null)
                throw new ArgumentNullException("tradeEvent");

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = false;

            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
                {
                    writer.WriteStartElement("TradeEvent");
                    writer.WriteAttributeString("tradeId", tradeEvent.TradeId ?? string.Empty);
                    writer.WriteAttributeString("sourceOwnerId", tradeEvent.SourceOwnerId ?? string.Empty);
                    writer.WriteAttributeString("targetOwnerId", tradeEvent.TargetOwnerId ?? string.Empty);
                    writer.WriteAttributeString("rejectionReason", tradeEvent.RejectionReason ?? string.Empty);
                    writer.WriteStartElement("Cargo");

                    IList<ShelteredTradeCargoDto> cargo = tradeEvent.Cargo;
                    for (int i = 0; i < cargo.Count; i++)
                    {
                        ShelteredTradeCargoDto item = cargo[i] ?? new ShelteredTradeCargoDto();
                        writer.WriteStartElement("Item");
                        writer.WriteAttributeString("itemId", item.ItemId ?? string.Empty);
                        writer.WriteAttributeString("count", item.Count.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("sourceOwnerId", item.SourceOwnerId ?? string.Empty);
                        writer.WriteAttributeString("targetOwnerId", item.TargetOwnerId ?? string.Empty);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                return stringWriter.ToString();
            }
        }

        public static ShelteredMultiplayerTradeEvent FromDetailsXml(string detailsXml)
        {
            ShelteredMultiplayerTradeEvent tradeEvent = new ShelteredMultiplayerTradeEvent();
            if (string.IsNullOrEmpty(detailsXml))
                return tradeEvent;

            XmlDocument document = new XmlDocument();
            using (StringReader stringReader = new StringReader(detailsXml))
            {
                using (XmlReader reader = XmlReader.Create(stringReader, CreateReaderSettings()))
                {
                    document.Load(reader);
                }
            }

            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "TradeEvent")
                return tradeEvent;

            tradeEvent.TradeId = ReadAttribute(root, "tradeId");
            tradeEvent.SourceOwnerId = ReadAttribute(root, "sourceOwnerId");
            tradeEvent.TargetOwnerId = ReadAttribute(root, "targetOwnerId");
            tradeEvent.RejectionReason = ReadAttribute(root, "rejectionReason");

            XmlNodeList cargoNodes = root.GetElementsByTagName("Item");
            for (int i = 0; i < cargoNodes.Count; i++)
            {
                XmlElement itemElement = cargoNodes[i] as XmlElement;
                if (itemElement == null)
                    continue;

                ShelteredTradeCargoDto item = new ShelteredTradeCargoDto();
                item.ItemId = ReadAttribute(itemElement, "itemId");
                item.Count = ReadIntAttribute(itemElement, "count", 0);
                item.SourceOwnerId = ReadAttribute(itemElement, "sourceOwnerId");
                item.TargetOwnerId = ReadAttribute(itemElement, "targetOwnerId");
                tradeEvent.Cargo.Add(item);
            }

            return tradeEvent;
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
            if (element == null || !element.HasAttribute(attributeName))
                return string.Empty;

            return element.GetAttribute(attributeName) ?? string.Empty;
        }

        private static int ReadIntAttribute(XmlElement element, string attributeName, int fallback)
        {
            int parsed;
            return int.TryParse(ReadAttribute(element, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }
    }
}
