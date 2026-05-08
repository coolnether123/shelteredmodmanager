using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace ShelteredAPI.Networking.Travel
{
    [Serializable]
    internal sealed class ShelteredTravelStartedEvent
    {
        public ShelteredTravelStartedEvent()
        {
            TravelId = string.Empty;
            SeedStreamName = string.Empty;
        }

        public string TravelId { get; set; }
        public int OwnerPlayerId { get; set; }
        public byte OwnerPeerId { get; set; }
        public int PartyId { get; set; }
        public long StartTick { get; set; }
        public int StartGridX { get; set; }
        public int StartGridY { get; set; }
        public int DestinationGridX { get; set; }
        public int DestinationGridY { get; set; }
        public bool HasWorldPosition { get; set; }
        public float StartWorldX { get; set; }
        public float StartWorldY { get; set; }
        public float DestinationWorldX { get; set; }
        public float DestinationWorldY { get; set; }
        public float WorldUnitsPerTick { get; set; }
        public long ExpectedArrivalTick { get; set; }
        public string SeedStreamName { get; set; }

        public ShelteredTravelStartedEvent Copy()
        {
            return new ShelteredTravelStartedEvent
            {
                TravelId = TravelId ?? string.Empty,
                OwnerPlayerId = OwnerPlayerId,
                OwnerPeerId = OwnerPeerId,
                PartyId = PartyId,
                StartTick = StartTick,
                StartGridX = StartGridX,
                StartGridY = StartGridY,
                DestinationGridX = DestinationGridX,
                DestinationGridY = DestinationGridY,
                HasWorldPosition = HasWorldPosition,
                StartWorldX = StartWorldX,
                StartWorldY = StartWorldY,
                DestinationWorldX = DestinationWorldX,
                DestinationWorldY = DestinationWorldY,
                WorldUnitsPerTick = WorldUnitsPerTick,
                ExpectedArrivalTick = ExpectedArrivalTick,
                SeedStreamName = SeedStreamName ?? string.Empty
            };
        }
    }

    [Serializable]
    internal sealed class ShelteredTravelCorrectedEvent
    {
        public ShelteredTravelCorrectedEvent()
        {
            TravelId = string.Empty;
            Reason = string.Empty;
        }

        public string TravelId { get; set; }
        public long CorrectionTick { get; set; }
        public int CorrectedGridX { get; set; }
        public int CorrectedGridY { get; set; }
        public int DestinationGridX { get; set; }
        public int DestinationGridY { get; set; }
        public bool HasWorldPosition { get; set; }
        public float CorrectedWorldX { get; set; }
        public float CorrectedWorldY { get; set; }
        public float DestinationWorldX { get; set; }
        public float DestinationWorldY { get; set; }
        public float WorldUnitsPerTick { get; set; }
        public long ExpectedArrivalTick { get; set; }
        public string Reason { get; set; }

        public ShelteredTravelCorrectedEvent Copy()
        {
            return new ShelteredTravelCorrectedEvent
            {
                TravelId = TravelId ?? string.Empty,
                CorrectionTick = CorrectionTick,
                CorrectedGridX = CorrectedGridX,
                CorrectedGridY = CorrectedGridY,
                DestinationGridX = DestinationGridX,
                DestinationGridY = DestinationGridY,
                HasWorldPosition = HasWorldPosition,
                CorrectedWorldX = CorrectedWorldX,
                CorrectedWorldY = CorrectedWorldY,
                DestinationWorldX = DestinationWorldX,
                DestinationWorldY = DestinationWorldY,
                WorldUnitsPerTick = WorldUnitsPerTick,
                ExpectedArrivalTick = ExpectedArrivalTick,
                Reason = Reason ?? string.Empty
            };
        }
    }

    [Serializable]
    internal sealed class ShelteredTravelArrivedEvent
    {
        public ShelteredTravelArrivedEvent()
        {
            TravelId = string.Empty;
            ResultKind = string.Empty;
            ResultPayloadJson = string.Empty;
        }

        public string TravelId { get; set; }
        public long ArrivalTick { get; set; }
        public int ArrivalGridX { get; set; }
        public int ArrivalGridY { get; set; }
        public bool HasWorldPosition { get; set; }
        public float ArrivalWorldX { get; set; }
        public float ArrivalWorldY { get; set; }
        public string ResultKind { get; set; }
        public string ResultPayloadJson { get; set; }

        public ShelteredTravelArrivedEvent Copy()
        {
            return new ShelteredTravelArrivedEvent
            {
                TravelId = TravelId ?? string.Empty,
                ArrivalTick = ArrivalTick,
                ArrivalGridX = ArrivalGridX,
                ArrivalGridY = ArrivalGridY,
                HasWorldPosition = HasWorldPosition,
                ArrivalWorldX = ArrivalWorldX,
                ArrivalWorldY = ArrivalWorldY,
                ResultKind = ResultKind ?? string.Empty,
                ResultPayloadJson = ResultPayloadJson ?? string.Empty
            };
        }
    }

    [Serializable]
    internal sealed class ShelteredTravelPredictionResult
    {
        public bool IsComplete { get; set; }
        public long CurrentTick { get; set; }
        public float Progress01 { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public bool HasWorldPosition { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public long ExpectedArrivalTick { get; set; }
    }

    internal static class ShelteredTravelContractCodec
    {
        public static bool IsTravelEventKind(string eventKind)
        {
            return string.Equals(eventKind, ShelteredNetworkEventKinds.TravelStarted, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TravelCorrected, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.TravelArrived, StringComparison.Ordinal);
        }

        public static ShelteredNetworkGameplayEvent ToGameplayEvent(ShelteredTravelStartedEvent started)
        {
            if (started == null)
                throw new ArgumentNullException("started");

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = ShelteredNetworkEventKinds.TravelStarted;
            gameplayEvent.ActorId = started.OwnerPlayerId.ToString(CultureInfo.InvariantCulture);
            gameplayEvent.TargetId = started.TravelId ?? string.Empty;
            gameplayEvent.Details = ToDetailsXml(started);
            gameplayEvent.PeerId = started.OwnerPeerId;
            gameplayEvent.GridX = started.StartGridX;
            gameplayEvent.GridY = started.StartGridY;
            return gameplayEvent;
        }

        public static ShelteredNetworkGameplayEvent ToGameplayEvent(ShelteredTravelCorrectedEvent corrected)
        {
            if (corrected == null)
                throw new ArgumentNullException("corrected");

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = ShelteredNetworkEventKinds.TravelCorrected;
            gameplayEvent.TargetId = corrected.TravelId ?? string.Empty;
            gameplayEvent.Details = ToDetailsXml(corrected);
            gameplayEvent.GridX = corrected.CorrectedGridX;
            gameplayEvent.GridY = corrected.CorrectedGridY;
            return gameplayEvent;
        }

        public static ShelteredNetworkGameplayEvent ToGameplayEvent(ShelteredTravelArrivedEvent arrived)
        {
            if (arrived == null)
                throw new ArgumentNullException("arrived");

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = ShelteredNetworkEventKinds.TravelArrived;
            gameplayEvent.TargetId = arrived.TravelId ?? string.Empty;
            gameplayEvent.Details = ToDetailsXml(arrived);
            gameplayEvent.GridX = arrived.ArrivalGridX;
            gameplayEvent.GridY = arrived.ArrivalGridY;
            return gameplayEvent;
        }

        public static ShelteredTravelStartedEvent StartedFromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null)
                throw new ArgumentNullException("gameplayEvent");

            return StartedFromDetailsXml(gameplayEvent.Details);
        }

        public static ShelteredTravelCorrectedEvent CorrectedFromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null)
                throw new ArgumentNullException("gameplayEvent");

            return CorrectedFromDetailsXml(gameplayEvent.Details);
        }

        public static ShelteredTravelArrivedEvent ArrivedFromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null)
                throw new ArgumentNullException("gameplayEvent");

            return ArrivedFromDetailsXml(gameplayEvent.Details);
        }

        public static string ToDetailsXml(ShelteredTravelStartedEvent started)
        {
            if (started == null)
                throw new ArgumentNullException("started");

            return WriteDetails(delegate(XmlWriter writer)
            {
                writer.WriteStartElement("TravelStarted");
                writer.WriteAttributeString("travelId", started.TravelId ?? string.Empty);
                writer.WriteAttributeString("ownerPlayerId", FormatInt(started.OwnerPlayerId));
                writer.WriteAttributeString("ownerPeerId", FormatInt(started.OwnerPeerId));
                writer.WriteAttributeString("partyId", FormatInt(started.PartyId));
                writer.WriteAttributeString("startTick", FormatLong(started.StartTick));
                writer.WriteAttributeString("startGridX", FormatInt(started.StartGridX));
                writer.WriteAttributeString("startGridY", FormatInt(started.StartGridY));
                writer.WriteAttributeString("destinationGridX", FormatInt(started.DestinationGridX));
                writer.WriteAttributeString("destinationGridY", FormatInt(started.DestinationGridY));
                writer.WriteAttributeString("hasWorldPosition", FormatBool(started.HasWorldPosition));
                writer.WriteAttributeString("startWorldX", FormatFloat(started.StartWorldX));
                writer.WriteAttributeString("startWorldY", FormatFloat(started.StartWorldY));
                writer.WriteAttributeString("destinationWorldX", FormatFloat(started.DestinationWorldX));
                writer.WriteAttributeString("destinationWorldY", FormatFloat(started.DestinationWorldY));
                writer.WriteAttributeString("worldUnitsPerTick", FormatFloat(started.WorldUnitsPerTick));
                writer.WriteAttributeString("expectedArrivalTick", FormatLong(started.ExpectedArrivalTick));
                writer.WriteAttributeString("seedStreamName", started.SeedStreamName ?? string.Empty);
                writer.WriteEndElement();
            });
        }

        public static string ToDetailsXml(ShelteredTravelCorrectedEvent corrected)
        {
            if (corrected == null)
                throw new ArgumentNullException("corrected");

            return WriteDetails(delegate(XmlWriter writer)
            {
                writer.WriteStartElement("TravelCorrected");
                writer.WriteAttributeString("travelId", corrected.TravelId ?? string.Empty);
                writer.WriteAttributeString("correctionTick", FormatLong(corrected.CorrectionTick));
                writer.WriteAttributeString("correctedGridX", FormatInt(corrected.CorrectedGridX));
                writer.WriteAttributeString("correctedGridY", FormatInt(corrected.CorrectedGridY));
                writer.WriteAttributeString("destinationGridX", FormatInt(corrected.DestinationGridX));
                writer.WriteAttributeString("destinationGridY", FormatInt(corrected.DestinationGridY));
                writer.WriteAttributeString("hasWorldPosition", FormatBool(corrected.HasWorldPosition));
                writer.WriteAttributeString("correctedWorldX", FormatFloat(corrected.CorrectedWorldX));
                writer.WriteAttributeString("correctedWorldY", FormatFloat(corrected.CorrectedWorldY));
                writer.WriteAttributeString("destinationWorldX", FormatFloat(corrected.DestinationWorldX));
                writer.WriteAttributeString("destinationWorldY", FormatFloat(corrected.DestinationWorldY));
                writer.WriteAttributeString("worldUnitsPerTick", FormatFloat(corrected.WorldUnitsPerTick));
                writer.WriteAttributeString("expectedArrivalTick", FormatLong(corrected.ExpectedArrivalTick));
                writer.WriteAttributeString("reason", corrected.Reason ?? string.Empty);
                writer.WriteEndElement();
            });
        }

        public static string ToDetailsXml(ShelteredTravelArrivedEvent arrived)
        {
            if (arrived == null)
                throw new ArgumentNullException("arrived");

            return WriteDetails(delegate(XmlWriter writer)
            {
                writer.WriteStartElement("TravelArrived");
                writer.WriteAttributeString("travelId", arrived.TravelId ?? string.Empty);
                writer.WriteAttributeString("arrivalTick", FormatLong(arrived.ArrivalTick));
                writer.WriteAttributeString("arrivalGridX", FormatInt(arrived.ArrivalGridX));
                writer.WriteAttributeString("arrivalGridY", FormatInt(arrived.ArrivalGridY));
                writer.WriteAttributeString("hasWorldPosition", FormatBool(arrived.HasWorldPosition));
                writer.WriteAttributeString("arrivalWorldX", FormatFloat(arrived.ArrivalWorldX));
                writer.WriteAttributeString("arrivalWorldY", FormatFloat(arrived.ArrivalWorldY));
                writer.WriteAttributeString("resultKind", arrived.ResultKind ?? string.Empty);
                writer.WriteAttributeString("resultPayloadJson", arrived.ResultPayloadJson ?? string.Empty);
                writer.WriteEndElement();
            });
        }

        public static string ToPayloadJson(ShelteredTravelStartedEvent started)
        {
            if (started == null)
                throw new ArgumentNullException("started");

            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            AppendJsonString(builder, "travelId", started.TravelId);
            AppendJsonInt(builder, "ownerPlayerId", started.OwnerPlayerId);
            AppendJsonInt(builder, "ownerPeerId", started.OwnerPeerId);
            AppendJsonInt(builder, "partyId", started.PartyId);
            AppendJsonLong(builder, "startTick", started.StartTick);
            AppendJsonInt(builder, "startGridX", started.StartGridX);
            AppendJsonInt(builder, "startGridY", started.StartGridY);
            AppendJsonInt(builder, "destinationGridX", started.DestinationGridX);
            AppendJsonInt(builder, "destinationGridY", started.DestinationGridY);
            AppendJsonBool(builder, "hasWorldPosition", started.HasWorldPosition);
            AppendJsonFloat(builder, "startWorldX", started.StartWorldX);
            AppendJsonFloat(builder, "startWorldY", started.StartWorldY);
            AppendJsonFloat(builder, "destinationWorldX", started.DestinationWorldX);
            AppendJsonFloat(builder, "destinationWorldY", started.DestinationWorldY);
            AppendJsonFloat(builder, "worldUnitsPerTick", started.WorldUnitsPerTick);
            AppendJsonLong(builder, "expectedArrivalTick", started.ExpectedArrivalTick);
            AppendJsonString(builder, "seedStreamName", started.SeedStreamName);
            builder.Append("}");
            return builder.ToString();
        }

        public static string ToPayloadJson(ShelteredTravelCorrectedEvent corrected)
        {
            if (corrected == null)
                throw new ArgumentNullException("corrected");

            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            AppendJsonString(builder, "travelId", corrected.TravelId);
            AppendJsonLong(builder, "correctionTick", corrected.CorrectionTick);
            AppendJsonInt(builder, "correctedGridX", corrected.CorrectedGridX);
            AppendJsonInt(builder, "correctedGridY", corrected.CorrectedGridY);
            AppendJsonInt(builder, "destinationGridX", corrected.DestinationGridX);
            AppendJsonInt(builder, "destinationGridY", corrected.DestinationGridY);
            AppendJsonBool(builder, "hasWorldPosition", corrected.HasWorldPosition);
            AppendJsonFloat(builder, "correctedWorldX", corrected.CorrectedWorldX);
            AppendJsonFloat(builder, "correctedWorldY", corrected.CorrectedWorldY);
            AppendJsonFloat(builder, "destinationWorldX", corrected.DestinationWorldX);
            AppendJsonFloat(builder, "destinationWorldY", corrected.DestinationWorldY);
            AppendJsonFloat(builder, "worldUnitsPerTick", corrected.WorldUnitsPerTick);
            AppendJsonLong(builder, "expectedArrivalTick", corrected.ExpectedArrivalTick);
            AppendJsonString(builder, "reason", corrected.Reason);
            builder.Append("}");
            return builder.ToString();
        }

        public static string ToPayloadJson(ShelteredTravelArrivedEvent arrived)
        {
            if (arrived == null)
                throw new ArgumentNullException("arrived");

            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            AppendJsonString(builder, "travelId", arrived.TravelId);
            AppendJsonLong(builder, "arrivalTick", arrived.ArrivalTick);
            AppendJsonInt(builder, "arrivalGridX", arrived.ArrivalGridX);
            AppendJsonInt(builder, "arrivalGridY", arrived.ArrivalGridY);
            AppendJsonBool(builder, "hasWorldPosition", arrived.HasWorldPosition);
            AppendJsonFloat(builder, "arrivalWorldX", arrived.ArrivalWorldX);
            AppendJsonFloat(builder, "arrivalWorldY", arrived.ArrivalWorldY);
            AppendJsonString(builder, "resultKind", arrived.ResultKind);
            AppendJsonString(builder, "resultPayloadJson", arrived.ResultPayloadJson);
            builder.Append("}");
            return builder.ToString();
        }

        public static ShelteredTravelStartedEvent StartedFromDetailsXml(string detailsXml)
        {
            XmlElement root = ReadRoot(detailsXml, "TravelStarted");
            ShelteredTravelStartedEvent started = new ShelteredTravelStartedEvent();
            if (root == null)
                return started;

            started.TravelId = ReadAttribute(root, "travelId");
            started.OwnerPlayerId = ReadIntAttribute(root, "ownerPlayerId", 0);
            started.OwnerPeerId = (byte)ReadIntAttribute(root, "ownerPeerId", 0);
            started.PartyId = ReadIntAttribute(root, "partyId", 0);
            started.StartTick = ReadLongAttribute(root, "startTick", 0);
            started.StartGridX = ReadIntAttribute(root, "startGridX", 0);
            started.StartGridY = ReadIntAttribute(root, "startGridY", 0);
            started.DestinationGridX = ReadIntAttribute(root, "destinationGridX", 0);
            started.DestinationGridY = ReadIntAttribute(root, "destinationGridY", 0);
            started.HasWorldPosition = ReadBoolAttribute(root, "hasWorldPosition", false);
            started.StartWorldX = ReadFloatAttribute(root, "startWorldX", 0f);
            started.StartWorldY = ReadFloatAttribute(root, "startWorldY", 0f);
            started.DestinationWorldX = ReadFloatAttribute(root, "destinationWorldX", 0f);
            started.DestinationWorldY = ReadFloatAttribute(root, "destinationWorldY", 0f);
            started.WorldUnitsPerTick = ReadFloatAttribute(root, "worldUnitsPerTick", 0f);
            started.ExpectedArrivalTick = ReadLongAttribute(root, "expectedArrivalTick", 0);
            started.SeedStreamName = ReadAttribute(root, "seedStreamName");
            return started;
        }

        public static ShelteredTravelCorrectedEvent CorrectedFromDetailsXml(string detailsXml)
        {
            XmlElement root = ReadRoot(detailsXml, "TravelCorrected");
            ShelteredTravelCorrectedEvent corrected = new ShelteredTravelCorrectedEvent();
            if (root == null)
                return corrected;

            corrected.TravelId = ReadAttribute(root, "travelId");
            corrected.CorrectionTick = ReadLongAttribute(root, "correctionTick", 0);
            corrected.CorrectedGridX = ReadIntAttribute(root, "correctedGridX", 0);
            corrected.CorrectedGridY = ReadIntAttribute(root, "correctedGridY", 0);
            corrected.DestinationGridX = ReadIntAttribute(root, "destinationGridX", 0);
            corrected.DestinationGridY = ReadIntAttribute(root, "destinationGridY", 0);
            corrected.HasWorldPosition = ReadBoolAttribute(root, "hasWorldPosition", false);
            corrected.CorrectedWorldX = ReadFloatAttribute(root, "correctedWorldX", 0f);
            corrected.CorrectedWorldY = ReadFloatAttribute(root, "correctedWorldY", 0f);
            corrected.DestinationWorldX = ReadFloatAttribute(root, "destinationWorldX", 0f);
            corrected.DestinationWorldY = ReadFloatAttribute(root, "destinationWorldY", 0f);
            corrected.WorldUnitsPerTick = ReadFloatAttribute(root, "worldUnitsPerTick", 0f);
            corrected.ExpectedArrivalTick = ReadLongAttribute(root, "expectedArrivalTick", 0);
            corrected.Reason = ReadAttribute(root, "reason");
            return corrected;
        }

        public static ShelteredTravelArrivedEvent ArrivedFromDetailsXml(string detailsXml)
        {
            XmlElement root = ReadRoot(detailsXml, "TravelArrived");
            ShelteredTravelArrivedEvent arrived = new ShelteredTravelArrivedEvent();
            if (root == null)
                return arrived;

            arrived.TravelId = ReadAttribute(root, "travelId");
            arrived.ArrivalTick = ReadLongAttribute(root, "arrivalTick", 0);
            arrived.ArrivalGridX = ReadIntAttribute(root, "arrivalGridX", 0);
            arrived.ArrivalGridY = ReadIntAttribute(root, "arrivalGridY", 0);
            arrived.HasWorldPosition = ReadBoolAttribute(root, "hasWorldPosition", false);
            arrived.ArrivalWorldX = ReadFloatAttribute(root, "arrivalWorldX", 0f);
            arrived.ArrivalWorldY = ReadFloatAttribute(root, "arrivalWorldY", 0f);
            arrived.ResultKind = ReadAttribute(root, "resultKind");
            arrived.ResultPayloadJson = ReadAttribute(root, "resultPayloadJson");
            return arrived;
        }

        private delegate void WriteXml(XmlWriter writer);

        private static string WriteDetails(WriteXml write)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = false;

            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
                {
                    write(writer);
                }

                return stringWriter.ToString();
            }
        }

        private static XmlElement ReadRoot(string detailsXml, string expectedRootName)
        {
            if (string.IsNullOrEmpty(detailsXml))
                return null;

            XmlDocument document = new XmlDocument();
            using (StringReader stringReader = new StringReader(detailsXml))
            {
                using (XmlReader reader = XmlReader.Create(stringReader, CreateReaderSettings()))
                {
                    document.Load(reader);
                }
            }

            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != expectedRootName)
                return null;

            return root;
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

        private static long ReadLongAttribute(XmlElement element, string attributeName, long fallback)
        {
            long parsed;
            return long.TryParse(ReadAttribute(element, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static float ReadFloatAttribute(XmlElement element, string attributeName, float fallback)
        {
            float parsed;
            return float.TryParse(ReadAttribute(element, attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static bool ReadBoolAttribute(XmlElement element, string attributeName, bool fallback)
        {
            bool parsed;
            return bool.TryParse(ReadAttribute(element, attributeName), out parsed)
                ? parsed
                : fallback;
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatLong(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
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

        private static void AppendJsonFloat(StringBuilder builder, string name, float value)
        {
            AppendJsonName(builder, name);
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
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
