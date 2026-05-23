using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace ShelteredAPI.Saves
{
    internal static class SaveInfoXmlMetadataReader
    {
        internal static bool TryRead(byte[] xmlBytes, SaveInfo target, out string error)
        {
            error = null;
            if (target == null)
            {
                error = "Target SaveInfo was null.";
                return false;
            }

            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                error = "XML payload was empty.";
                return false;
            }

            try
            {
                XmlDocument document = LoadDocument(xmlBytes);
                target.familyName = ReadString(document, "familyName", "Unknown");
                target.daysSurvived = ReadInt(document, "daysSurvived", 0);
                target.difficulty = ReadInt(document, "difficultySetting", 1);
                target.saveTime = ReadString(document, "timestamp", string.Empty);
                target.hasMapSizeMetadata = HasElement(document, "mapSize");
                target.mapSize = ReadInt(document, "mapSize", 0);
                target.fog = ReadBool(document, "fogSetting", false);
                target.rainDiff = ReadInt(document, "rainDifficulty", 1);
                target.resourceDiff = ReadInt(document, "resourcesDifficulty", 1);
                target.breachDiff = ReadInt(document, "breachDifficulty", 1);
                target.factionDiff = ReadInt(document, "factionDifficulty", 1);
                target.moodDiff = ReadInt(document, "moodDifficulty", 1);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static SaveInfo ReadOrDefault(byte[] xmlBytes)
        {
            SaveInfo info = new SaveInfo();
            string error;
            TryRead(xmlBytes, info, out error);
            return info;
        }

        private static XmlDocument LoadDocument(byte[] xmlBytes)
        {
            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            using (MemoryStream stream = new MemoryStream(xmlBytes))
            using (XmlTextReader reader = new XmlTextReader(stream))
            {
                reader.ProhibitDtd = true;
                reader.XmlResolver = null;
                document.Load(reader);
            }

            return document;
        }

        private static string ReadString(XmlDocument document, string elementName, string fallback)
        {
            XmlNodeList nodes = document.GetElementsByTagName(elementName);
            if (nodes == null || nodes.Count == 0 || nodes[0] == null)
            {
                return fallback;
            }

            return nodes[0].InnerText ?? fallback;
        }

        private static int ReadInt(XmlDocument document, string elementName, int fallback)
        {
            int value;
            return int.TryParse(ReadString(document, elementName, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static bool HasElement(XmlDocument document, string elementName)
        {
            XmlNodeList nodes = document.GetElementsByTagName(elementName);
            return nodes != null && nodes.Count > 0 && nodes[0] != null;
        }

        private static bool ReadBool(XmlDocument document, string elementName, bool fallback)
        {
            bool value;
            return bool.TryParse(ReadString(document, elementName, null), out value) ? value : fallback;
        }
    }
}
