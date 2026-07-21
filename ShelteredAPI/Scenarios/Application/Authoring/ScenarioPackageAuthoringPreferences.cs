using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    internal sealed class ScenarioAcceptedWarning
    {
        public string Fingerprint { get; set; }
        public string Note { get; set; }
    }

    internal sealed class ScenarioPackageAuthoringPreferences
    {
        private const string FileName = ".scenario-authoring-package.xml";

        public ScenarioPackageAuthoringPreferences()
        {
            IncludeReadme = true;
            AcceptedWarnings = new List<ScenarioAcceptedWarning>();
        }

        public bool IncludeReadme { get; set; }
        public List<ScenarioAcceptedWarning> AcceptedWarnings { get; private set; }

        public int CountAccepted(ScenarioValidationResult validation)
        {
            int count = 0;
            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : null;
            for (int i = 0; issues != null && i < issues.Length; i++)
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Warning && Find(BuildFingerprint(issues, i)) != null) count++;
            return count;
        }

        public ScenarioAcceptedWarning Find(string fingerprint)
        {
            for (int i = 0; i < AcceptedWarnings.Count; i++)
                if (AcceptedWarnings[i] != null && string.Equals(AcceptedWarnings[i].Fingerprint, fingerprint, StringComparison.Ordinal)) return AcceptedWarnings[i];
            return null;
        }

        public void Accept(string fingerprint, string note)
        {
            ScenarioAcceptedWarning warning = Find(fingerprint);
            if (warning == null)
            {
                warning = new ScenarioAcceptedWarning { Fingerprint = fingerprint };
                AcceptedWarnings.Add(warning);
            }
            warning.Note = TrimNote(note);
        }

        public void Remove(string fingerprint)
        {
            for (int i = AcceptedWarnings.Count - 1; i >= 0; i--)
                if (AcceptedWarnings[i] != null && string.Equals(AcceptedWarnings[i].Fingerprint, fingerprint, StringComparison.Ordinal)) AcceptedWarnings.RemoveAt(i);
        }

        public static ScenarioPackageAuthoringPreferences Load(string scenarioFilePath)
        {
            ScenarioPackageAuthoringPreferences result = new ScenarioPackageAuthoringPreferences();
            string path = ResolvePath(scenarioFilePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;
            try
            {
                XmlDocument document = new XmlDocument(); document.Load(path);
                XmlElement root = document.DocumentElement;
                bool includeReadme;
                if (root != null && bool.TryParse(root.GetAttribute("includeReadme"), out includeReadme)) result.IncludeReadme = includeReadme;
                XmlNodeList nodes = root != null ? root.SelectNodes("AcceptedWarnings/Warning") : null;
                for (int i = 0; nodes != null && i < nodes.Count; i++)
                {
                    XmlElement item = nodes[i] as XmlElement;
                    if (item != null && !string.IsNullOrEmpty(item.GetAttribute("fingerprint")))
                        result.AcceptedWarnings.Add(new ScenarioAcceptedWarning { Fingerprint = item.GetAttribute("fingerprint"), Note = item.InnerText ?? string.Empty });
                }
            }
            catch { }
            return result;
        }

        public void Save(string scenarioFilePath)
        {
            string path = ResolvePath(scenarioFilePath);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("An active draft path is required.");
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartElement("ScenarioPackageAuthoring"); writer.WriteAttributeString("includeReadme", IncludeReadme ? "true" : "false");
                writer.WriteStartElement("AcceptedWarnings");
                for (int i = 0; i < AcceptedWarnings.Count; i++)
                {
                    ScenarioAcceptedWarning warning = AcceptedWarnings[i];
                    if (warning == null || string.IsNullOrEmpty(warning.Fingerprint)) continue;
                    writer.WriteStartElement("Warning"); writer.WriteAttributeString("fingerprint", warning.Fingerprint); writer.WriteString(warning.Note ?? string.Empty); writer.WriteEndElement();
                }
                writer.WriteEndElement(); writer.WriteEndElement();
            }
        }

        internal static string BuildFingerprint(ScenarioValidationIssue[] issues, int index)
        {
            ScenarioValidationIssue issue = issues != null && index >= 0 && index < issues.Length ? issues[index] : null;
            if (issue == null) return string.Empty;
            int occurrence = 0;
            for (int i = 0; i < index; i++)
                if (issues[i] != null && issues[i].Severity == issue.Severity && string.Equals(issues[i].Message, issue.Message, StringComparison.Ordinal)) occurrence++;
            string value = issue.Severity + "|" + (issue.Message ?? string.Empty) + "|" + occurrence.ToString(CultureInfo.InvariantCulture);
            ulong hash = 14695981039346656037UL;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++) { hash ^= bytes[i]; hash *= 1099511628211UL; }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static string ResolvePath(string scenarioFilePath)
        {
            string directory = !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetDirectoryName(scenarioFilePath) : null;
            return string.IsNullOrEmpty(directory) ? null : Path.Combine(directory, FileName);
        }

        private static string TrimNote(string note)
        {
            string value = (note ?? string.Empty).Trim();
            return value.Length <= 160 ? value : value.Substring(0, 160);
        }
    }
}
