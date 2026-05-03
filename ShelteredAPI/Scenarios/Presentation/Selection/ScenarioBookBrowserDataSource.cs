using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioBookBrowserDataSource
    {
        private readonly IScenarioSelectionCatalogService _catalog;
        private readonly IScenarioSaveLibrary _saveLibrary;

        public ScenarioBookBrowserDataSource(IScenarioSelectionCatalogService catalog, IScenarioSaveLibrary saveLibrary)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");

            _catalog = catalog;
            _saveLibrary = saveLibrary;
        }

        public void Refresh()
        {
            _catalog.Refresh();
        }

        public List<ScenarioBookRowModel> BuildRows(
            ScenarioBookBrowserViewKind view,
            ScenarioBookType selectedType,
            ScenarioCatalogEntry selectedScenario)
        {
            return BuildRows(view, selectedType, selectedScenario, null);
        }

        public List<ScenarioBookRowModel> BuildRows(
            ScenarioBookBrowserViewKind view,
            ScenarioBookType selectedType,
            ScenarioCatalogEntry selectedScenario,
            string searchFilter)
        {
            List<ScenarioBookRowModel> rows;
            switch (view)
            {
                case ScenarioBookBrowserViewKind.Types:
                    rows = BuildTypeRows();
                    break;
                case ScenarioBookBrowserViewKind.Scenarios:
                    rows = BuildScenarioRows(selectedType);
                    break;
                case ScenarioBookBrowserViewKind.Saves:
                    rows = BuildSaveRows(selectedScenario);
                    break;
                case ScenarioBookBrowserViewKind.DraftDetails:
                    rows = new List<ScenarioBookRowModel>();
                    break;
                default:
                    rows = new List<ScenarioBookRowModel>();
                    break;
            }

            return FilterRows(rows, searchFilter);
        }

        public string GetHeaderTitle(ScenarioBookBrowserViewKind view, ScenarioBookType selectedType, ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Custom Scenarios";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return GetTypeLabel(selectedType);
            if (view == ScenarioBookBrowserViewKind.DraftDetails)
                return selectedScenario != null ? Safe(selectedScenario.DisplayName, selectedScenario.ScenarioId) : "Draft Details";
            if (selectedScenario != null)
                return Safe(selectedScenario.DisplayName, selectedScenario.ScenarioId);

            return "Scenario Saves";
        }

        public string GetHeaderDetail(ScenarioBookBrowserViewKind view, ScenarioBookType selectedType, ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Drafts, scenario modes, and published custom scenarios.";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return selectedType == ScenarioBookType.Draft
                    ? "Drafts are authoring work. Open one to edit details or continue building it."
                    : "Pick a scenario. The next page shows saves owned by that scenario.";
            if (view == ScenarioBookBrowserViewKind.DraftDetails)
                return "Edit the local draft details or open the authoring save.";
            if (selectedScenario != null)
                return "Read the scenario notes, then choose a save slot.";

            return string.Empty;
        }

        public static string GetTypeLabel(ScenarioBookType type)
        {
            switch (type)
            {
                case ScenarioBookType.Surrounded: return "Surrounded Scenarios";
                case ScenarioBookType.Stasis: return "Stasis Scenarios";
                case ScenarioBookType.Draft: return "Draft Scenarios";
                default: return "Published Scenarios";
            }
        }

        private List<ScenarioBookRowModel> BuildTypeRows()
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            if (ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
                rows.Add(BuildTypeRow(ScenarioBookType.Draft, "Draft Scenarios", "Authoring workspace for unfinished scenarios, not normal play content."));

            rows.Add(BuildTypeRow(ScenarioBookType.Surrounded, "Surrounded Scenarios", "Custom scenarios built on the Surrounded rule set."));
            rows.Add(BuildTypeRow(ScenarioBookType.Stasis, "Stasis Scenarios", "Custom scenarios built on the Stasis rule set."));
            AddPublishedScenarioRows(rows);
            return rows;
        }

        private ScenarioBookRowModel BuildTypeRow(ScenarioBookType type, string title, string detail)
        {
            return new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Type,
                Type = type,
                Title = title,
                Detail = detail,
                Badge = CountEntries(type).ToString() + " scenario(s)"
            };
        }

        private List<ScenarioBookRowModel> BuildScenarioRows(ScenarioBookType selectedType)
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            if (selectedType == ScenarioBookType.Draft && ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
            {
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.CreateDraft,
                    Title = "Add New Scenario",
                    Detail = "Create a new authoring draft for scenario-building work.",
                    Badge = "Authoring"
                });
            }

            ScenarioCatalogEntry[] entries = ListEntries(selectedType);
            for (int i = 0; i < entries.Length; i++)
            {
                ScenarioCatalogEntry entry = entries[i];
                if (entry == null)
                    continue;

                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Scenario,
                    Type = selectedType,
                    Scenario = entry,
                    Title = Safe(entry.DisplayName, entry.ScenarioId),
                    Detail = entry.Source == ScenarioCatalogSource.Draft
                        ? Safe(entry.Description, BuildScenarioDetail(entry))
                        : BuildScenarioDetail(entry),
                    Badge = BuildScenarioBadge(entry),
                    IsLocked = !entry.CanStart
                });
            }

            return rows;
        }

        private List<ScenarioBookRowModel> BuildSaveRows(ScenarioCatalogEntry entry)
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            if (entry == null)
                return rows;

            rows.Add(new ScenarioBookRowModel
            {
                Kind = entry.Source == ScenarioCatalogSource.Draft
                    ? ScenarioBookRowKind.OpenDraft
                    : ScenarioBookRowKind.StartScenario,
                Scenario = entry,
                Title = entry.Source == ScenarioCatalogSource.Draft ? "Open Draft" : "Start New",
                Detail = entry.Source == ScenarioCatalogSource.Draft
                    ? "Load the draft's authoring save and reopen the scenario editor."
                    : "Create a new scenario-owned save for this scenario.",
                Badge = entry.Source == ScenarioCatalogSource.Draft ? "Authoring" : "New Game",
                IsLocked = !entry.CanStart
            });

            if (entry.Source == ScenarioCatalogSource.Draft)
                return rows;

            SaveEntry[] saves = new SaveEntry[0];
            try { saves = _saveLibrary.ListSaves(entry.StorageScenarioId); }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookBrowser] Save enumeration failed for "
                    + entry.StorageScenarioId + ": " + ex.Message);
            }

            for (int i = 0; i < saves.Length; i++)
            {
                SaveEntry save = saves[i];
                if (save == null)
                    continue;

                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.LoadSave,
                    Scenario = entry,
                    Save = save,
                    Title = BuildSaveSlotTitle(save),
                    Detail = BuildSaveDetail(save),
                    Badge = "Load",
                    IsLocked = !entry.CanStart,
                    CanDelete = true
                });
            }

            return rows;
        }

        private int CountEntries(ScenarioBookType type)
        {
            return ListEntries(type).Length;
        }

        private ScenarioCatalogEntry[] ListEntries(ScenarioBookType type)
        {
            ScenarioCatalogEntry[] all = _catalog.ListAll();
            List<ScenarioCatalogEntry> entries = new List<ScenarioCatalogEntry>();
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry entry = all[i];
                if (entry == null)
                    continue;

                if (type == ScenarioBookType.Draft)
                {
                    if (entry.Source == ScenarioCatalogSource.Draft)
                        entries.Add(entry);
                    continue;
                }

                if (type == ScenarioBookType.Surrounded && IsPlayableScenarioMode(entry, ScenarioBaseGameMode.Surrounded))
                    entries.Add(entry);
                else if (type == ScenarioBookType.Stasis && IsPlayableScenarioMode(entry, ScenarioBaseGameMode.Stasis))
                    entries.Add(entry);
            }

            return entries.ToArray();
        }

        private void AddPublishedScenarioRows(List<ScenarioBookRowModel> rows)
        {
            ScenarioCatalogEntry[] all = _catalog.ListAll();
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry entry = all[i];
                if (entry == null || entry.Source != ScenarioCatalogSource.Modded)
                    continue;

                if (entry.BaseGameMode == ScenarioBaseGameMode.Surrounded
                    || entry.BaseGameMode == ScenarioBaseGameMode.Stasis)
                {
                    continue;
                }

                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Scenario,
                    Type = ScenarioBookType.Published,
                    Scenario = entry,
                    Title = Safe(entry.DisplayName, entry.ScenarioId),
                    Detail = Safe(entry.Description, BuildScenarioDetail(entry)),
                    Badge = BuildScenarioBadge(entry),
                    IsLocked = !entry.CanStart
                });
            }
        }

        private static bool IsPlayableScenarioMode(ScenarioCatalogEntry entry, ScenarioBaseGameMode mode)
        {
            if (entry == null || entry.BaseGameMode != mode)
                return false;

            return entry.Source == ScenarioCatalogSource.Modded || entry.Source == ScenarioCatalogSource.Vanilla;
        }

        private static string BuildScenarioDetail(ScenarioCatalogEntry entry)
        {
            string owner = entry.Source == ScenarioCatalogSource.Vanilla
                ? "vanilla"
                : (!string.IsNullOrEmpty(entry.OwnerModId) ? entry.OwnerModId : "local");
            string mode = entry.BaseGameMode.ToString();
            string state = entry.CanStart ? "Ready" : "Locked";
            return owner + " - " + mode + " - " + state;
        }

        private static string BuildScenarioBadge(ScenarioCatalogEntry entry)
        {
            if (entry != null && entry.Source == ScenarioCatalogSource.Draft)
                return "Draft";

            return entry != null ? entry.SaveCount.ToString() + " save(s)" : string.Empty;
        }

        private static string BuildSaveDetail(SaveEntry save)
        {
            string family = save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.familyName)
                ? save.saveInfo.familyName
                : "Unknown family";
            string days = save.saveInfo != null ? save.saveInfo.daysSurvived.ToString() + " day(s)" : "no day info";
            return family + ", " + days + "\n" + BuildDifficultyLine(save);
        }

        private static string BuildSaveSlotTitle(SaveEntry save)
        {
            if (save == null)
                return "Slot";

            return "Slot " + save.absoluteSlot + ":\n" + FormatDisplayTime(GetSaveTime(save));
        }

        internal static string FormatDisplayTime(string rawTime)
        {
            if (string.IsNullOrEmpty(rawTime))
                return string.Empty;

            try
            {
                bool hasExplicitOffset =
                    rawTime.IndexOf('Z') >= 0 ||
                    rawTime.IndexOf('+') >= 0 ||
                    rawTime.LastIndexOf('-') > 9;

                DateTimeOffset dto;
                if (hasExplicitOffset && DateTimeOffset.TryParse(rawTime, out dto))
                    return dto.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

                DateTime dt;
                if (DateTime.TryParse(rawTime, out dt))
                {
                    if (dt.Kind == DateTimeKind.Utc)
                        return dt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

                    return dt.ToString("g", CultureInfo.CurrentCulture);
                }
            }
            catch
            {
            }

            return rawTime;
        }

        private static string GetSaveTime(SaveEntry save)
        {
            if (save == null)
                return string.Empty;
            if (save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.saveTime))
                return save.saveInfo.saveTime;
            if (!string.IsNullOrEmpty(save.updatedAt))
                return save.updatedAt;
            return save.createdAt;
        }

        private static string BuildDifficultyLine(SaveEntry save)
        {
            int difficulty = save != null && save.saveInfo != null ? save.saveInfo.difficulty : -1;
            switch (difficulty)
            {
                case 0: return "Difficulty: Easy";
                case 1: return "Difficulty: Normal";
                case 2: return "Difficulty: Hard";
                case 3: return "Difficulty: Hardcore";
                case 4: return "Difficulty: Custom";
                default: return "Difficulty: Unknown";
            }
        }

        private static List<ScenarioBookRowModel> FilterRows(List<ScenarioBookRowModel> rows, string searchFilter)
        {
            if (rows == null)
                return new List<ScenarioBookRowModel>();
            if (string.IsNullOrEmpty(searchFilter))
                return rows;

            List<ScenarioBookRowModel> filtered = new List<ScenarioBookRowModel>();
            for (int i = 0; i < rows.Count; i++)
            {
                ScenarioBookRowModel row = rows[i];
                if (MatchesSearch(row, searchFilter))
                    filtered.Add(row);
            }

            return filtered;
        }

        private static bool MatchesSearch(ScenarioBookRowModel row, string searchFilter)
        {
            if (row == null)
                return false;

            return ContainsSearch(row.Title, searchFilter)
                || ContainsSearch(row.Detail, searchFilter)
                || ContainsSearch(row.Badge, searchFilter)
                || ContainsSearch(row.Type.ToString(), searchFilter)
                || (row.Scenario != null && MatchesScenario(row.Scenario, searchFilter))
                || (row.Save != null && MatchesSave(row.Save, searchFilter));
        }

        private static bool MatchesScenario(ScenarioCatalogEntry scenario, string searchFilter)
        {
            return ContainsSearch(scenario.ScenarioId, searchFilter)
                || ContainsSearch(scenario.DisplayName, searchFilter)
                || ContainsSearch(scenario.Description, searchFilter)
                || ContainsSearch(scenario.OwnerModId, searchFilter)
                || ContainsSearch(scenario.Version, searchFilter)
                || ContainsSearch(scenario.BaseGameMode.ToString(), searchFilter)
                || ContainsSearch(scenario.Source.ToString(), searchFilter);
        }

        private static bool MatchesSave(SaveEntry save, string searchFilter)
        {
            return ContainsSearch(save.id, searchFilter)
                || ContainsSearch(save.name, searchFilter)
                || ContainsSearch(save.createdAt, searchFilter)
                || ContainsSearch(save.updatedAt, searchFilter)
                || ContainsSearch(save.gameVersion, searchFilter)
                || ContainsSearch(save.modApiVersion, searchFilter)
                || ContainsSearch(save.scenarioId, searchFilter)
                || ContainsSearch(save.scenarioVersion, searchFilter)
                || ContainsSearch(GetSaveTime(save), searchFilter)
                || (save.saveInfo != null && ContainsSearch(save.saveInfo.familyName, searchFilter));
        }

        private static bool ContainsSearch(string value, string searchFilter)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }
    }
}
