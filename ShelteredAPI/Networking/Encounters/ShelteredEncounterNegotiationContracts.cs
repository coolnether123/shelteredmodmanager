using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using ShelteredAPI.Networking.Trade;

namespace ShelteredAPI.Networking.Encounters
{
    public enum ShelteredEncounterActionKind
    {
        Unknown,
        Trade,
        Fight,
        Flee
    }

    public enum ShelteredEncounterNegotiationStateKind
    {
        Proposed,
        Accepted,
        Declined,
        Resolved,
        Expired
    }

    public sealed class ShelteredEncounterNegotiationEvent
    {
        public ShelteredEncounterNegotiationEvent()
        {
            EventId = string.Empty;
            CorrelationId = string.Empty;
            EventKind = string.Empty;
            EncounterId = string.Empty;
            InitiatorTravelId = string.Empty;
            ResponderTravelId = string.Empty;
            Reason = string.Empty;
            State = ShelteredEncounterNegotiationStateKind.Proposed;
        }

        public string EventId { get; set; }
        public string CorrelationId { get; set; }
        public uint WorldTick { get; set; }
        public string EventKind { get; set; }
        public string EncounterId { get; set; }
        public int InitiatorPlayerId { get; set; }
        public byte InitiatorPeerId { get; set; }
        public string InitiatorTravelId { get; set; }
        public int ResponderPlayerId { get; set; }
        public byte ResponderPeerId { get; set; }
        public string ResponderTravelId { get; set; }
        public ShelteredEncounterActionKind OfferedAction { get; set; }
        public ShelteredEncounterNegotiationStateKind State { get; set; }
        public string Reason { get; set; }
        public ShelteredMultiplayerTradeEvent TradeOffer { get; set; }

        public ShelteredEncounterNegotiationEvent Copy()
        {
            return new ShelteredEncounterNegotiationEvent
            {
                EventId = EventId ?? string.Empty,
                CorrelationId = CorrelationId ?? string.Empty,
                WorldTick = WorldTick,
                EventKind = EventKind ?? string.Empty,
                EncounterId = EncounterId ?? string.Empty,
                InitiatorPlayerId = InitiatorPlayerId,
                InitiatorPeerId = InitiatorPeerId,
                InitiatorTravelId = InitiatorTravelId ?? string.Empty,
                ResponderPlayerId = ResponderPlayerId,
                ResponderPeerId = ResponderPeerId,
                ResponderTravelId = ResponderTravelId ?? string.Empty,
                OfferedAction = OfferedAction,
                State = State,
                Reason = Reason ?? string.Empty,
                TradeOffer = TradeOffer != null ? TradeOffer.Copy() : null
            };
        }
    }

    public static class ShelteredEncounterNegotiationContractCodec
    {
        public static bool IsEncounterEventKind(string eventKind)
        {
            return string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterInteractionIntent, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationProposed, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationAccepted, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationDeclined, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationResolved, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationExpired, StringComparison.Ordinal);
        }

        public static ShelteredNetworkGameplayEvent ToGameplayEvent(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent == null)
                throw new ArgumentNullException("encounterEvent");

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventId = encounterEvent.EventId ?? string.Empty;
            gameplayEvent.CorrelationId = encounterEvent.CorrelationId ?? string.Empty;
            gameplayEvent.WorldTick = encounterEvent.WorldTick;
            gameplayEvent.EventKind = encounterEvent.EventKind ?? string.Empty;
            gameplayEvent.ActorId = FormatInt(encounterEvent.InitiatorPlayerId);
            gameplayEvent.TargetId = encounterEvent.EncounterId ?? string.Empty;
            gameplayEvent.Details = ToDetailsXml(encounterEvent);
            gameplayEvent.PeerId = encounterEvent.InitiatorPeerId;
            return gameplayEvent;
        }

        public static ShelteredEncounterNegotiationEvent FromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null)
                throw new ArgumentNullException("gameplayEvent");

            ShelteredEncounterNegotiationEvent encounterEvent = FromDetailsXml(gameplayEvent.Details);
            encounterEvent.EventId = gameplayEvent.EventId ?? string.Empty;
            encounterEvent.CorrelationId = gameplayEvent.CorrelationId ?? string.Empty;
            encounterEvent.WorldTick = gameplayEvent.WorldTick;
            encounterEvent.EventKind = gameplayEvent.EventKind ?? string.Empty;
            if (string.IsNullOrEmpty(encounterEvent.EncounterId))
                encounterEvent.EncounterId = gameplayEvent.TargetId ?? string.Empty;
            if (encounterEvent.InitiatorPlayerId <= 0)
                encounterEvent.InitiatorPlayerId = ReadInt(gameplayEvent.ActorId, 0);
            if (encounterEvent.InitiatorPeerId == 0 && gameplayEvent.PeerId > 0 && gameplayEvent.PeerId <= byte.MaxValue)
                encounterEvent.InitiatorPeerId = (byte)gameplayEvent.PeerId;
            encounterEvent.State = ResolveStateKind(encounterEvent.EventKind, encounterEvent.State);
            return encounterEvent;
        }

        public static string ToDetailsXml(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent == null)
                throw new ArgumentNullException("encounterEvent");

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = false;

            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
                {
                    writer.WriteStartElement("EncounterNegotiation");
                    writer.WriteAttributeString("encounterId", encounterEvent.EncounterId ?? string.Empty);
                    writer.WriteAttributeString("initiatorPlayerId", FormatInt(encounterEvent.InitiatorPlayerId));
                    writer.WriteAttributeString("initiatorPeerId", FormatInt(encounterEvent.InitiatorPeerId));
                    writer.WriteAttributeString("initiatorTravelId", encounterEvent.InitiatorTravelId ?? string.Empty);
                    writer.WriteAttributeString("responderPlayerId", FormatInt(encounterEvent.ResponderPlayerId));
                    writer.WriteAttributeString("responderPeerId", FormatInt(encounterEvent.ResponderPeerId));
                    writer.WriteAttributeString("responderTravelId", encounterEvent.ResponderTravelId ?? string.Empty);
                    writer.WriteAttributeString("offeredAction", encounterEvent.OfferedAction.ToString());
                    writer.WriteAttributeString("state", encounterEvent.State.ToString());
                    writer.WriteAttributeString("reason", encounterEvent.Reason ?? string.Empty);
                    if (encounterEvent.TradeOffer != null)
                    {
                        writer.WriteAttributeString(
                            "tradeOfferDetails",
                            ShelteredMultiplayerTradeContractCodec.ToDetailsXml(encounterEvent.TradeOffer));
                    }

                    writer.WriteEndElement();
                }

                return stringWriter.ToString();
            }
        }

        public static ShelteredEncounterNegotiationEvent FromDetailsXml(string detailsXml)
        {
            ShelteredEncounterNegotiationEvent encounterEvent = new ShelteredEncounterNegotiationEvent();
            if (string.IsNullOrEmpty(detailsXml))
                return encounterEvent;

            XmlDocument document = new XmlDocument();
            using (StringReader stringReader = new StringReader(detailsXml))
            {
                using (XmlReader reader = XmlReader.Create(stringReader, CreateReaderSettings()))
                {
                    document.Load(reader);
                }
            }

            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "EncounterNegotiation")
                return encounterEvent;

            encounterEvent.EncounterId = ReadAttribute(root, "encounterId");
            encounterEvent.InitiatorPlayerId = ReadIntAttribute(root, "initiatorPlayerId", 0);
            encounterEvent.InitiatorPeerId = (byte)ReadIntAttribute(root, "initiatorPeerId", 0);
            encounterEvent.InitiatorTravelId = ReadAttribute(root, "initiatorTravelId");
            encounterEvent.ResponderPlayerId = ReadIntAttribute(root, "responderPlayerId", 0);
            encounterEvent.ResponderPeerId = (byte)ReadIntAttribute(root, "responderPeerId", 0);
            encounterEvent.ResponderTravelId = ReadAttribute(root, "responderTravelId");
            encounterEvent.OfferedAction = ReadEnum(
                ReadAttribute(root, "offeredAction"),
                ShelteredEncounterActionKind.Unknown);
            encounterEvent.State = ReadEnum(
                ReadAttribute(root, "state"),
                ShelteredEncounterNegotiationStateKind.Proposed);
            encounterEvent.Reason = ReadAttribute(root, "reason");

            string tradeOfferDetails = ReadAttribute(root, "tradeOfferDetails");
            if (!string.IsNullOrEmpty(tradeOfferDetails))
                encounterEvent.TradeOffer = ShelteredMultiplayerTradeContractCodec.FromDetailsXml(tradeOfferDetails);

            return encounterEvent;
        }

        public static string ToPayloadJson(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent == null)
                throw new ArgumentNullException("encounterEvent");

            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            AppendJsonString(builder, "encounterId", encounterEvent.EncounterId);
            AppendJsonString(builder, "eventKind", encounterEvent.EventKind);
            AppendJsonString(builder, "state", encounterEvent.State.ToString());
            AppendJsonString(builder, "offeredAction", encounterEvent.OfferedAction.ToString());
            AppendJsonInt(builder, "initiatorPlayerId", encounterEvent.InitiatorPlayerId);
            AppendJsonInt(builder, "initiatorPeerId", encounterEvent.InitiatorPeerId);
            AppendJsonString(builder, "initiatorTravelId", encounterEvent.InitiatorTravelId);
            AppendJsonInt(builder, "responderPlayerId", encounterEvent.ResponderPlayerId);
            AppendJsonInt(builder, "responderPeerId", encounterEvent.ResponderPeerId);
            AppendJsonString(builder, "responderTravelId", encounterEvent.ResponderTravelId);
            AppendJsonString(builder, "reason", encounterEvent.Reason);
            builder.Append("}");
            return builder.ToString();
        }

        internal static ShelteredEncounterNegotiationStateKind ResolveStateKind(
            string eventKind,
            ShelteredEncounterNegotiationStateKind fallback)
        {
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterInteractionIntent, StringComparison.Ordinal))
                return ShelteredEncounterNegotiationStateKind.Proposed;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationProposed, StringComparison.Ordinal))
                return ShelteredEncounterNegotiationStateKind.Proposed;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationAccepted, StringComparison.Ordinal))
                return ShelteredEncounterNegotiationStateKind.Accepted;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationDeclined, StringComparison.Ordinal))
                return ShelteredEncounterNegotiationStateKind.Declined;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationResolved, StringComparison.Ordinal))
                return ShelteredEncounterNegotiationStateKind.Resolved;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationExpired, StringComparison.Ordinal))
                return ShelteredEncounterNegotiationStateKind.Expired;

            return fallback;
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
            return ReadInt(ReadAttribute(element, attributeName), fallback);
        }

        private static int ReadInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value ?? string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static T ReadEnum<T>(string value, T fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;

            try
            {
                return (T)Enum.Parse(typeof(T), value, false);
            }
            catch
            {
                return fallback;
            }
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static void AppendJsonString(StringBuilder builder, string name, string value)
        {
            AppendJsonName(builder, name);
            builder.Append("\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
        }

        private static void AppendJsonInt(StringBuilder builder, string name, int value)
        {
            AppendJsonName(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJsonName(StringBuilder builder, string name)
        {
            if (builder.Length > 1)
                builder.Append(",");

            builder.Append("\"").Append(EscapeJson(name)).Append("\":");
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
