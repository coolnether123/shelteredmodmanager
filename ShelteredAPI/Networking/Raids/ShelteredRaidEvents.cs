using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace ShelteredAPI.Networking.Raids
{
    internal static class ShelteredRaidEvents
    {
        public static bool IsRaidEventKind(string eventKind)
        {
            return string.Equals(eventKind, ShelteredNetworkEventKinds.RaidIntent, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.RaidAccepted, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.RaidRejected, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.RaidLaunched, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.RaidWarning, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.RaidArrived, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.RaidResolved, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.RaidCancelled, StringComparison.Ordinal);
        }

        public static ShelteredNetworkGameplayEvent ToGameplayEvent(ShelteredRaidEvent raidEvent)
        {
            if (raidEvent == null)
                throw new ArgumentNullException("raidEvent");

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = raidEvent.EventKind ?? string.Empty;
            gameplayEvent.ActorId = raidEvent.AttackerPlayerId.ToString(CultureInfo.InvariantCulture);
            gameplayEvent.TargetId = raidEvent.RaidId ?? string.Empty;
            gameplayEvent.Details = ToDetailsXml(raidEvent);
            return gameplayEvent;
        }

        public static ShelteredRaidEvent FromGameplayEvent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null)
                throw new ArgumentNullException("gameplayEvent");

            ShelteredRaidEvent raidEvent = FromDetailsXml(gameplayEvent.Details);
            raidEvent.EventKind = gameplayEvent.EventKind ?? string.Empty;
            if (string.IsNullOrEmpty(raidEvent.RaidId))
                raidEvent.RaidId = gameplayEvent.TargetId ?? string.Empty;
            return raidEvent;
        }

        public static string ToDetailsXml(ShelteredRaidEvent raidEvent)
        {
            return WriteDetails(delegate(XmlWriter writer)
            {
                writer.WriteStartElement("RaidEvent");
                writer.WriteAttributeString("raidId", raidEvent.RaidId ?? string.Empty);
                writer.WriteAttributeString("attackerPlayerId", FormatInt(raidEvent.AttackerPlayerId));
                writer.WriteAttributeString("defenderPlayerId", FormatInt(raidEvent.DefenderPlayerId));
                writer.WriteAttributeString("targetBunkerOwnerId", FormatInt(raidEvent.TargetBunkerOwnerId));
                writer.WriteAttributeString("startTick", FormatLong(raidEvent.StartTick));
                writer.WriteAttributeString("arrivalTick", FormatLong(raidEvent.ArrivalTick));
                writer.WriteAttributeString("raidStrength", FormatInt(raidEvent.RaidStrength));
                writer.WriteAttributeString("warningTick", FormatLong(raidEvent.WarningTick));
                writer.WriteAttributeString("defenseScore", FormatInt(raidEvent.DefenseScore));
                writer.WriteAttributeString("resultPayloadJson", raidEvent.ResultPayloadJson ?? string.Empty);
                writer.WriteAttributeString("rejectionReason", raidEvent.RejectionReason ?? string.Empty);
                writer.WriteEndElement();
            });
        }

        public static ShelteredRaidEvent FromDetailsXml(string detailsXml)
        {
            ShelteredRaidEvent raidEvent = new ShelteredRaidEvent();
            XmlElement root = ReadRoot(detailsXml);
            if (root == null)
                return raidEvent;

            raidEvent.RaidId = ReadAttribute(root, "raidId");
            raidEvent.AttackerPlayerId = ReadIntAttribute(root, "attackerPlayerId", 0);
            raidEvent.DefenderPlayerId = ReadIntAttribute(root, "defenderPlayerId", 0);
            raidEvent.TargetBunkerOwnerId = ReadIntAttribute(root, "targetBunkerOwnerId", 0);
            raidEvent.StartTick = ReadLongAttribute(root, "startTick", 0);
            raidEvent.ArrivalTick = ReadLongAttribute(root, "arrivalTick", 0);
            raidEvent.RaidStrength = ReadIntAttribute(root, "raidStrength", 0);
            raidEvent.WarningTick = ReadLongAttribute(root, "warningTick", 0);
            raidEvent.DefenseScore = ReadIntAttribute(root, "defenseScore", 0);
            raidEvent.ResultPayloadJson = ReadAttribute(root, "resultPayloadJson");
            raidEvent.RejectionReason = ReadAttribute(root, "rejectionReason");
            return raidEvent;
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
                    write(writer);

                return stringWriter.ToString();
            }
        }

        private static XmlElement ReadRoot(string detailsXml)
        {
            if (string.IsNullOrEmpty(detailsXml))
                return null;

            XmlDocument document = new XmlDocument();
            using (StringReader stringReader = new StringReader(detailsXml))
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ProhibitDtd = true;
                settings.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(stringReader, settings))
                    document.Load(reader);
            }

            return document.DocumentElement != null && document.DocumentElement.Name == "RaidEvent"
                ? document.DocumentElement
                : null;
        }

        private static string ReadAttribute(XmlElement element, string name)
        {
            return element != null && element.HasAttribute(name) ? element.GetAttribute(name) ?? string.Empty : string.Empty;
        }

        private static int ReadIntAttribute(XmlElement element, string name, int fallback)
        {
            int parsed;
            return int.TryParse(ReadAttribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static long ReadLongAttribute(XmlElement element, string name, long fallback)
        {
            long parsed;
            return long.TryParse(ReadAttribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static string FormatInt(int value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static string FormatLong(long value) { return value.ToString(CultureInfo.InvariantCulture); }
    }
}
