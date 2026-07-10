using System;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    // Facts shown compactly in draft rows and, in richer form, in the draft detail
    // pane. Row facts are cheap (no validation, no export folder walk); detail facts
    // are computed lazily for the single selected draft only.
    internal sealed class ScenarioBookDraftFactsModel
    {
        public string BaseModeLabel = "Standard";
        public string LastEditedText = "unknown";
        public bool HasRecoveryData;
        public bool HasHistory;
        public bool HasExport;
        public string LastExportText;
        public bool ValidationComputed;
        public bool ValidationAvailable;
        public int ErrorCount;
        public int WarningCount;
        public string ValidationSummary = "Not checked";
    }

    internal static class ScenarioBookDraftFacts
    {
        public static string BaseModeLabel(ScenarioBaseGameMode mode)
        {
            switch (mode)
            {
                case ScenarioBaseGameMode.Surrounded: return "Surrounded";
                case ScenarioBaseGameMode.Stasis: return "Stasis";
                default: return "Standard";
            }
        }

        public static string RelativeTime(DateTime utc)
        {
            if (utc == DateTime.MinValue)
                return "unknown";
            return ScenarioDraftSnapshotService.FormatAge(utc);
        }

        public static string RelativeTimeFromIso(string rawIsoUtc)
        {
            if (string.IsNullOrEmpty(rawIsoUtc))
                return "unknown";

            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(rawIsoUtc, out dto))
                return ScenarioDraftSnapshotService.FormatAge(dto.UtcDateTime);

            DateTime dt;
            if (DateTime.TryParse(rawIsoUtc, out dt))
                return ScenarioDraftSnapshotService.FormatAge(dt.ToUniversalTime());

            return "unknown";
        }

        // An autosave written after the last manual save signals unrecovered work.
        public static bool HasUnsavedRecovery(string scenarioFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(scenarioFilePath) || !File.Exists(scenarioFilePath))
                    return false;

                string autosaves = GetHistoryDirectory(scenarioFilePath, "autosaves");
                if (!Directory.Exists(autosaves))
                    return false;

                DateTime manualUtc = File.GetLastWriteTimeUtc(scenarioFilePath);
                string[] files = Directory.GetFiles(autosaves, "*.xml", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    if (File.GetLastWriteTimeUtc(files[i]) > manualUtc)
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool HasHistory(string scenarioFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(scenarioFilePath))
                    return false;

                return HistoryDirectoryHasContent(GetHistoryDirectory(scenarioFilePath, "autosaves"))
                    || HistoryDirectoryHasContent(GetHistoryDirectory(scenarioFilePath, "versions"));
            }
            catch
            {
                return false;
            }
        }

        // Cheap facts safe to build for every visible draft row.
        public static ScenarioBookDraftFactsModel BuildRowFacts(ScenarioCatalogEntry entry, SaveEntry draftSave, string scenarioFilePath)
        {
            ScenarioBookDraftFactsModel facts = new ScenarioBookDraftFactsModel();
            facts.BaseModeLabel = BaseModeLabel(entry != null ? entry.BaseGameMode : ScenarioBaseGameMode.Survival);
            facts.LastEditedText = ResolveLastEdited(draftSave, scenarioFilePath);
            facts.HasRecoveryData = HasUnsavedRecovery(scenarioFilePath);
            facts.HasHistory = facts.HasRecoveryData || HasHistory(scenarioFilePath);
            return facts;
        }

        // Richer facts for the single selected draft: adds validation and export state.
        public static ScenarioBookDraftFactsModel BuildDetailFacts(ScenarioCatalogEntry entry)
        {
            ScenarioBookDraftFactsModel facts = new ScenarioBookDraftFactsModel();
            if (entry == null)
                return facts;

            facts.BaseModeLabel = BaseModeLabel(entry.BaseGameMode);

            string scenarioFilePath = ResolveDraftFilePath(entry);
            if (!string.IsNullOrEmpty(scenarioFilePath))
            {
                try
                {
                    if (File.Exists(scenarioFilePath))
                        facts.LastEditedText = RelativeTime(File.GetLastWriteTimeUtc(scenarioFilePath));
                }
                catch
                {
                }

                facts.HasRecoveryData = HasUnsavedRecovery(scenarioFilePath);
                facts.HasHistory = facts.HasRecoveryData || HasHistory(scenarioFilePath);
            }

            ApplyExportFacts(facts, entry);
            ApplyValidationFacts(facts, entry, scenarioFilePath);
            return facts;
        }

        public static string BuildDeleteMessage(string draftName, ScenarioBookDraftFactsModel facts)
        {
            string name = string.IsNullOrEmpty(draftName) ? "this draft" : draftName;
            string export = facts != null && facts.HasExport
                ? "Its exported package is kept" + FormatExportSuffix(facts) + "."
                : "No exported package exists for this draft.";
            string recovery = facts != null && facts.HasRecoveryData
                ? "Unsaved recovery data (a newer autosave) will be removed with the draft."
                : (facts != null && facts.HasHistory
                    ? "Its autosave history will be removed with the draft."
                    : "No recovery data will be lost.");
            return "Delete '" + name + "'?\n" + export + "\n" + recovery;
        }

        public static string BuildDuplicateMessage(string draftName, ScenarioBookDraftFactsModel facts)
        {
            string name = string.IsNullOrEmpty(draftName) ? "this draft" : draftName;
            string export = facts != null && facts.HasExport
                ? "The original's exported package is kept and is not copied."
                : "No exported package exists for this draft.";
            return "Duplicate '" + name + "'?\nA separate editable copy is created. " + export;
        }

        public static string BuildRenameMessage(string draftName, string newFileName, ScenarioBookDraftFactsModel facts)
        {
            string name = string.IsNullOrEmpty(draftName) ? "this draft" : draftName;
            string target = string.IsNullOrEmpty(newFileName) ? "a new file name" : "'" + newFileName + "'";
            string export = facts != null && facts.HasExport
                ? "The existing exported package keeps its current name until you export again."
                : "No exported package exists for this draft.";
            return "Rename '" + name + "' file to " + target + "?\n" + export;
        }

        private static string FormatExportSuffix(ScenarioBookDraftFactsModel facts)
        {
            return facts != null && !string.IsNullOrEmpty(facts.LastExportText)
                ? " (last export " + facts.LastExportText + ")"
                : string.Empty;
        }

        private static string ResolveLastEdited(SaveEntry draftSave, string scenarioFilePath)
        {
            if (draftSave != null && !string.IsNullOrEmpty(draftSave.updatedAt))
                return RelativeTimeFromIso(draftSave.updatedAt);

            try
            {
                if (!string.IsNullOrEmpty(scenarioFilePath) && File.Exists(scenarioFilePath))
                    return RelativeTime(File.GetLastWriteTimeUtc(scenarioFilePath));
            }
            catch
            {
            }

            return "unknown";
        }

        private static void ApplyExportFacts(ScenarioBookDraftFactsModel facts, ScenarioCatalogEntry entry)
        {
            try
            {
                string exportRoot;
                DateTime lastExportUtc;
                if (ScenarioPublishExportService.TryGetExistingExportInfo(entry.ScenarioId, entry.DisplayName, out exportRoot, out lastExportUtc))
                {
                    facts.HasExport = true;
                    facts.LastExportText = RelativeTime(lastExportUtc);
                }
            }
            catch
            {
            }
        }

        private static void ApplyValidationFacts(ScenarioBookDraftFactsModel facts, ScenarioCatalogEntry entry, string scenarioFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(scenarioFilePath) || !File.Exists(scenarioFilePath))
                {
                    facts.ValidationSummary = "Not checked";
                    return;
                }

                IScenarioDefinitionSerializer serializer = ScenarioCompositionRoot.Resolve<IScenarioDefinitionSerializer>();
                IScenarioDefinitionValidator validator = ScenarioCompositionRoot.Resolve<IScenarioDefinitionValidator>();
                ScenarioDefinition definition = serializer != null ? serializer.Load(scenarioFilePath) : null;

                ScenarioAuthoringValidationSnapshot snapshot = ScenarioAuthoringValidationSnapshot.Evaluate(validator, definition, scenarioFilePath);
                facts.ValidationComputed = true;
                facts.ValidationAvailable = snapshot.ValidationAvailable;
                facts.ErrorCount = snapshot.ErrorCount;
                facts.WarningCount = snapshot.WarningCount;
                facts.ValidationSummary = BuildValidationSummary(snapshot);
            }
            catch (Exception ex)
            {
                facts.ValidationSummary = "Not checked";
                MMLog.WriteWarning("[ScenarioBookBrowser] Draft validation facts failed: " + ex.Message);
            }
        }

        private static string BuildValidationSummary(ScenarioAuthoringValidationSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.ValidationAvailable)
                return "Not checked";

            if (snapshot.ErrorCount > 0)
            {
                string summary = snapshot.ErrorCount.ToString() + " error(s)";
                if (snapshot.WarningCount > 0)
                    summary += ", " + snapshot.WarningCount.ToString() + " warning(s)";
                return summary;
            }

            if (snapshot.WarningCount > 0)
                return snapshot.WarningCount.ToString() + " warning(s)";

            return "OK";
        }

        private static string ResolveDraftFilePath(ScenarioCatalogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ScenarioId))
                return null;

            try
            {
                ScenarioInfo info;
                if (ScenarioAuthoringDraftRepository.Instance.TryGet(entry.ScenarioId, out info) && info != null)
                    return info.FilePath;
            }
            catch
            {
            }

            return null;
        }

        private static string GetHistoryDirectory(string scenarioFilePath, string kind)
        {
            return Path.Combine(Path.Combine(Path.GetDirectoryName(scenarioFilePath), ".history"), kind);
        }

        private static bool HistoryDirectoryHasContent(string directory)
        {
            return Directory.Exists(directory)
                && Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly).Length > 0;
        }
    }
}
