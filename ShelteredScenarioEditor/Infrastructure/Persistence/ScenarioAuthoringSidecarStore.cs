using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using ModAPI.Core;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Infrastructure.Persistence
{
    /// <summary>
    /// Persists editor workflow metadata beside a draft without putting it in the
    /// public scenario schema. Published packages intentionally omit this sidecar.
    /// </summary>
    internal sealed class ScenarioAuthoringSidecarStore
    {
        internal const string SidecarSuffix = ".editor.xml";

        internal static string GetSidecarPath(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return null;

            string directory = Path.GetDirectoryName(scenarioFilePath);
            string fileName = Path.GetFileNameWithoutExtension(scenarioFilePath) + SidecarSuffix;
            return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
        }

        internal ScenarioEditorState Load(string scenarioFilePath, out string warning)
        {
            string sidecarPath = GetSidecarPath(scenarioFilePath);
            ScenarioEditorState state = LoadSidecarFile(sidecarPath, true, out warning);
            if (string.IsNullOrEmpty(warning) || string.IsNullOrEmpty(sidecarPath)
                || !File.Exists(sidecarPath + ".bak"))
                return state;

            string backupWarning;
            ScenarioEditorState recovered = LoadSidecarFile(sidecarPath + ".bak", false, out backupWarning);
            if (!string.IsNullOrEmpty(backupWarning))
                return state;

            warning = "Recovered scenario editor state from the last good sidecar backup.";
            MMLog.WriteWarning("[ScenarioAuthoringSidecar] " + warning);
            return recovered;
        }

        private static ScenarioEditorState LoadSidecarFile(
            string sidecarPath,
            bool missingIsEmpty,
            out string warning)
        {
            warning = null;
            ScenarioEditorState state = new ScenarioEditorState();
            if (string.IsNullOrEmpty(sidecarPath) || !File.Exists(sidecarPath))
            {
                if (!missingIsEmpty)
                    warning = "Scenario editor state file does not exist: " + (sidecarPath ?? string.Empty);
                return state;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ProhibitDtd = true;
                settings.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(sidecarPath, settings))
                    document.Load(reader);

                XmlElement root = document.DocumentElement;
                if (root == null || !string.Equals(root.Name, "ScenarioEditorState", StringComparison.Ordinal))
                    throw new FormatException("The root element must be ScenarioEditorState.");
                if (!string.Equals(root.GetAttribute("formatVersion"), "1", StringComparison.Ordinal))
                    throw new FormatException("Unsupported scenario editor state format version.");

                XmlElement setupElement = root.SelectSingleNode("Setup") as XmlElement;
                if (setupElement != null)
                {
                    state.SetupFlowEnabled = ReadBool(setupElement, "flowEnabled", false);
                    state.ChecklistDismissed = ReadBool(setupElement, "checklistDismissed", true);
                    state.UpdatedAtUtc = setupElement.GetAttribute("updatedAtUtc");

                    XmlNodeList tourNodes = setupElement.SelectNodes("CompletedTours/Tour");
                    for (int i = 0; tourNodes != null && i < tourNodes.Count; i++)
                    {
                        XmlElement tour = tourNodes[i] as XmlElement;
                        state.AddCompletedTour(tour != null ? tour.GetAttribute("id") : null);
                    }
                }

                XmlElement checklistElement = root.SelectSingleNode("AuthorTestChecklist") as XmlElement;
                XmlNodeList nodes = checklistElement != null ? checklistElement.SelectNodes("Item") : null;
                for (int i = 0; nodes != null && i < nodes.Count; i++)
                {
                    XmlElement element = nodes[i] as XmlElement;
                    string id = element != null ? element.GetAttribute("id") : null;
                    if (string.IsNullOrEmpty(id) || state.AuthorTestChecklist.Find(id) != null)
                        continue;

                    ScenarioAuthorTestChecklistItem item = new ScenarioAuthorTestChecklistItem();
                    item.Id = id;
                    bool isChecked;
                    item.Checked = bool.TryParse(element.GetAttribute("checked"), out isChecked) && isChecked;
                    DateTime checkedUtc;
                    if (DateTime.TryParse(
                        element.GetAttribute("checkedUtc"),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out checkedUtc))
                    {
                        item.CheckedUtc = checkedUtc.ToUniversalTime();
                    }

                    string sourceText = element.GetAttribute("source");
                    item.Source = ScenarioAuthorTestVerificationSource.None;
                    if (!string.IsNullOrEmpty(sourceText))
                    {
                        try
                        {
                            ScenarioAuthorTestVerificationSource source =
                                (ScenarioAuthorTestVerificationSource)Enum.Parse(
                                    typeof(ScenarioAuthorTestVerificationSource), sourceText, true);
                            if (Enum.IsDefined(typeof(ScenarioAuthorTestVerificationSource), source))
                                item.Source = source;
                        }
                        catch
                        {
                        }
                    }
                    XmlElement note = element.SelectSingleNode("Note") as XmlElement;
                    item.Note = note != null ? note.InnerText : null;
                    if (item.Checked || !string.IsNullOrEmpty(item.Note))
                        state.AuthorTestChecklist.Add(item);
                }

                return state;
            }
            catch (Exception ex)
            {
                warning = "Scenario editor checklist state could not be loaded from '"
                    + sidecarPath + "': " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringSidecar] " + warning);
                return state;
            }
        }

        internal void Save(string scenarioFilePath, ScenarioEditorState state)
        {
            Save(scenarioFilePath, state, false);
        }

        internal void Save(
            string scenarioFilePath,
            ScenarioEditorState state,
            bool preserveEmptyState)
        {
            string sidecarPath = GetSidecarPath(scenarioFilePath);
            if (string.IsNullOrEmpty(sidecarPath))
                throw new ArgumentException("Scenario file path is required.", "scenarioFilePath");

            if (!preserveEmptyState && (state == null || !state.HasPersistedContent))
            {
                if (File.Exists(sidecarPath))
                    File.Delete(sidecarPath);
                if (File.Exists(sidecarPath + ".bak"))
                    File.Delete(sidecarPath + ".bak");
                return;
            }

            string directory = Path.GetDirectoryName(sidecarPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string tempPath = Path.Combine(
                string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory,
                Path.GetFileName(sidecarPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                if (state == null)
                    state = new ScenarioEditorState();
                state.UpdatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.Encoding = System.Text.Encoding.UTF8;
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("ScenarioEditorState");
                        writer.WriteAttributeString("formatVersion", "1");
                        writer.WriteStartElement("Setup");
                        writer.WriteAttributeString("flowEnabled", state.SetupFlowEnabled.ToString());
                        writer.WriteAttributeString("checklistDismissed", state.ChecklistDismissed.ToString());
                        writer.WriteAttributeString("updatedAtUtc", state.UpdatedAtUtc);
                        writer.WriteStartElement("CompletedTours");
                        for (int i = 0; state.CompletedTours != null && i < state.CompletedTours.Count; i++)
                        {
                            string tourId = state.CompletedTours[i];
                            if (string.IsNullOrEmpty(tourId))
                                continue;
                            writer.WriteStartElement("Tour");
                            writer.WriteAttributeString("id", tourId);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                        writer.WriteStartElement("AuthorTestChecklist");
                        IList<ScenarioAuthorTestChecklistItem> items = state.AuthorTestChecklist != null
                            ? state.AuthorTestChecklist.Items
                            : null;
                        for (int i = 0; items != null && i < items.Count; i++)
                        {
                            ScenarioAuthorTestChecklistItem item = items[i];
                            if (item == null || string.IsNullOrEmpty(item.Id)
                                || (!item.Checked && string.IsNullOrEmpty(item.Note)))
                                continue;

                            writer.WriteStartElement("Item");
                            writer.WriteAttributeString("id", item.Id);
                            writer.WriteAttributeString("checked", item.Checked.ToString());
                            if (item.CheckedUtc.HasValue)
                                writer.WriteAttributeString("checkedUtc", item.CheckedUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                            if (item.Source != ScenarioAuthorTestVerificationSource.None)
                                writer.WriteAttributeString("source", item.Source.ToString());
                            if (!string.IsNullOrEmpty(item.Note))
                                writer.WriteElementString("Note", item.Note);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                        writer.WriteEndDocument();
                    }
                    stream.Flush();
                }

                string validationWarning;
                ScenarioEditorState validation = LoadSidecarFile(tempPath, false, out validationWarning);
                if (!string.IsNullOrEmpty(validationWarning) || validation == null)
                    throw new FormatException(validationWarning ?? "Scenario editor state validation failed.");

                if (File.Exists(sidecarPath))
                {
                    try
                    {
                        File.Replace(tempPath, sidecarPath, sidecarPath + ".bak", false);
                        tempPath = null;
                    }
                    catch (PlatformNotSupportedException)
                    {
                    }
                    catch (NotSupportedException)
                    {
                    }
                }

                if (!string.IsNullOrEmpty(tempPath))
                {
                    if (File.Exists(sidecarPath))
                    {
                        File.Copy(sidecarPath, sidecarPath + ".bak", true);
                        File.Delete(sidecarPath);
                    }
                    File.Move(tempPath, sidecarPath);
                    tempPath = null;
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static bool ReadBool(XmlElement element, string attributeName, bool fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            bool parsed;
            return bool.TryParse(element.GetAttribute(attributeName), out parsed) ? parsed : fallback;
        }
    }
}
