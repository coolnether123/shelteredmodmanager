using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

using ModAPI.Core;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringSetupState
    {
        private readonly List<string> _completedTours = new List<string>();

        public bool SetupFlowEnabled { get; set; }
        public bool ChecklistDismissed { get; set; }
        public string UpdatedAtUtc { get; set; }

        public List<string> CompletedTours
        {
            get { return _completedTours; }
        }

        public bool HasCompletedTour(string tourId)
        {
            if (string.IsNullOrEmpty(tourId))
                return false;

            for (int i = 0; i < _completedTours.Count; i++)
            {
                if (string.Equals(_completedTours[i], tourId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool AddCompletedTour(string tourId)
        {
            if (string.IsNullOrEmpty(tourId) || HasCompletedTour(tourId))
                return false;

            _completedTours.Add(tourId);
            return true;
        }

        public ScenarioAuthoringSetupState Copy()
        {
            ScenarioAuthoringSetupState copy = new ScenarioAuthoringSetupState();
            copy.SetupFlowEnabled = SetupFlowEnabled;
            copy.ChecklistDismissed = ChecklistDismissed;
            copy.UpdatedAtUtc = UpdatedAtUtc;
            for (int i = 0; i < _completedTours.Count; i++)
                copy.CompletedTours.Add(_completedTours[i]);
            return copy;
        }
    }

    internal sealed class ScenarioAuthoringSetupChecklistItem
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public bool Complete { get; set; }
    }

    internal sealed class ScenarioAuthoringSetupStateService
    {
        public const string SidecarFileName = "authoring_state.xml";

        public ScenarioAuthoringSetupState LoadForScenarioFile(string scenarioFilePath)
        {
            string path = GetSidecarPath(scenarioFilePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new ScenarioAuthoringSetupState
                {
                    SetupFlowEnabled = false,
                    ChecklistDismissed = true
                };
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlElement root = document.DocumentElement;
                ScenarioAuthoringSetupState state = new ScenarioAuthoringSetupState();
                state.SetupFlowEnabled = ReadBool(root, "setupFlowEnabled", true);
                state.ChecklistDismissed = ReadBool(root, "checklistDismissed", false);
                state.UpdatedAtUtc = root != null ? root.GetAttribute("updatedAtUtc") : null;

                XmlNodeList tourNodes = root != null ? root.SelectNodes("CompletedTours/Tour") : null;
                for (int i = 0; tourNodes != null && i < tourNodes.Count; i++)
                {
                    XmlElement tour = tourNodes[i] as XmlElement;
                    string id = tour != null ? tour.GetAttribute("id") : null;
                    state.AddCompletedTour(id);
                }

                return state;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringSetupState] Failed to load sidecar '" + path + "': " + ex.Message);
                return new ScenarioAuthoringSetupState
                {
                    SetupFlowEnabled = false,
                    ChecklistDismissed = true
                };
            }
        }

        public ScenarioAuthoringSetupState CreateInitialForScenarioFile(string scenarioFilePath)
        {
            ScenarioAuthoringSetupState state = new ScenarioAuthoringSetupState
            {
                SetupFlowEnabled = true,
                ChecklistDismissed = false
            };
            SaveForScenarioFile(scenarioFilePath, state);
            return state;
        }

        public bool SaveForScenarioFile(string scenarioFilePath, ScenarioAuthoringSetupState state)
        {
            string path = GetSidecarPath(scenarioFilePath);
            if (string.IsNullOrEmpty(path) || state == null)
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                state.UpdatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                builder.Append("<ScenarioAuthoringState setupFlowEnabled=\"")
                    .Append(state.SetupFlowEnabled ? "true" : "false")
                    .Append("\" checklistDismissed=\"")
                    .Append(state.ChecklistDismissed ? "true" : "false")
                    .Append("\" updatedAtUtc=\"")
                    .Append(Escape(state.UpdatedAtUtc))
                    .AppendLine("\">");
                builder.AppendLine("  <CompletedTours>");
                for (int i = 0; state.CompletedTours != null && i < state.CompletedTours.Count; i++)
                {
                    string tourId = state.CompletedTours[i];
                    if (string.IsNullOrEmpty(tourId))
                        continue;

                    builder.Append("    <Tour id=\"")
                        .Append(Escape(tourId))
                        .AppendLine("\" />");
                }

                builder.AppendLine("  </CompletedTours>");
                builder.AppendLine("</ScenarioAuthoringState>");
                File.WriteAllText(path, builder.ToString());
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringSetupState] Failed to save sidecar '" + path + "': " + ex.Message);
                return false;
            }
        }

        public bool SaveActive(ScenarioAuthoringState state)
        {
            return state != null
                && SaveForScenarioFile(state.ActiveScenarioFilePath, state.SetupState);
        }

        public static string GetSidecarPath(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return null;

            try
            {
                string directory = Path.GetDirectoryName(scenarioFilePath);
                return string.IsNullOrEmpty(directory) ? null : Path.Combine(directory, SidecarFileName);
            }
            catch
            {
                return null;
            }
        }

        private static bool ReadBool(XmlElement element, string attributeName, bool fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            bool parsed;
            return bool.TryParse(element.GetAttribute(attributeName), out parsed) ? parsed : fallback;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : SecurityElement.Escape(value);
        }
    }
}
