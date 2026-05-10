using System;
using System.IO;
using System.Xml;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Selection;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal static class ScenarioBookSaveMetadataReader
    {
        public static ScenarioBookSaveDetailModel Read(string storageScenarioId, SaveEntry save)
        {
            ScenarioBookSaveDetailModel detail = new ScenarioBookSaveDetailModel();
            detail.Save = save;
            detail.IsVanilla = ScenarioSaveLibrary.IsVanillaScenarioSaveEntry(save);
            detail.DaysSurvived = save != null && save.saveInfo != null ? save.saveInfo.daysSurvived : 0;
            detail.SaveTime = GetSaveTime(save);

            if (save == null || detail.IsVanilla || save.absoluteSlot <= 0 || string.IsNullOrEmpty(storageScenarioId))
                return detail;

            string path = null;
            try
            {
                path = DirectoryProvider.EntryPath(storageScenarioId, save.absoluteSlot);
                if (!File.Exists(path))
                    return detail;

                XmlDocument document = LoadDocument(path);
                ReadBinding(document, detail);
                ReadRuntimeState(document, detail);
            }
            catch (Exception ex)
            {
                detail.MetadataError = ex.Message;
                MMLog.WriteWarning("[ScenarioBookBrowser] Could not read scenario save metadata for "
                    + storageScenarioId + "/slot " + save.absoluteSlot.ToString()
                    + (string.IsNullOrEmpty(path) ? string.Empty : " at " + path)
                    + ": " + ex.Message);
            }

            return detail;
        }

        public static string GetSaveTime(SaveEntry save)
        {
            if (save == null)
                return string.Empty;
            if (save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.saveTime))
                return save.saveInfo.saveTime;
            if (!string.IsNullOrEmpty(save.updatedAt))
                return save.updatedAt;
            return save.createdAt;
        }

        private static XmlDocument LoadDocument(string path)
        {
            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            using (XmlTextReader reader = new XmlTextReader(path))
            {
                reader.ProhibitDtd = true;
                reader.XmlResolver = null;
                document.Load(reader);
            }

            return document;
        }

        private static void ReadBinding(XmlDocument document, ScenarioBookSaveDetailModel detail)
        {
            XmlNode group = SelectRootGroup(document, "CustomScenarioBinding");
            if (group == null)
                return;

            string scenarioId = ReadString(group, "ScenarioId");
            if (string.IsNullOrEmpty(scenarioId))
                return;

            detail.HasBinding = true;
            detail.BindingScenarioId = scenarioId;
            detail.VersionApplied = ReadString(group, "VersionApplied");
            detail.IsActive = ReadBool(group, "IsActive", false);
            detail.IsConvertedToNormalSave = ReadBool(group, "IsConverted", false);
            detail.DayCreated = ReadInt(group, "DayCreated", 0);

            bool hasQuestInstance = ReadBool(group, "HasScenarioQuestInstanceId", false);
            int questInstanceId = ReadInt(group, "ScenarioQuestInstanceId", -1);
            if (hasQuestInstance && questInstanceId >= 0)
                detail.ScenarioQuestInstanceId = questInstanceId;
        }

        private static void ReadRuntimeState(XmlDocument document, ScenarioBookSaveDetailModel detail)
        {
            XmlNode group = SelectRootGroup(document, "CustomScenarioRuntimeState");
            if (group == null)
                return;

            detail.HasRuntimeState = true;
            detail.ScenarioOutcome = ReadString(group, "ScenarioOutcome");
            detail.ScenarioOutcomeConditionId = ReadString(group, "ScenarioOutcomeConditionId");
            detail.LastProcessedDay = ReadInt(group, "LastProcessedDay", 0);
        }

        private static XmlNode SelectRootGroup(XmlDocument document, string name)
        {
            if (document == null || string.IsNullOrEmpty(name))
                return null;

            return document.SelectSingleNode("/root/" + name);
        }

        private static string ReadString(XmlNode group, string name)
        {
            XmlNode node = group != null ? group.SelectSingleNode(name) : null;
            return node != null ? (node.InnerText ?? string.Empty) : string.Empty;
        }

        private static int ReadInt(XmlNode group, string name, int fallback)
        {
            int value;
            return int.TryParse(ReadString(group, name), out value) ? value : fallback;
        }

        private static bool ReadBool(XmlNode group, string name, bool fallback)
        {
            bool value;
            return bool.TryParse(ReadString(group, name), out value) ? value : fallback;
        }
    }
}
