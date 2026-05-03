using System;
using System.Collections.Generic;
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
            switch (view)
            {
                case ScenarioBookBrowserViewKind.Types:
                    return BuildTypeRows();
                case ScenarioBookBrowserViewKind.Scenarios:
                    return BuildScenarioRows(selectedType);
                case ScenarioBookBrowserViewKind.Saves:
                    return BuildSaveRows(selectedScenario);
                default:
                    return new List<ScenarioBookRowModel>();
            }
        }

        public string GetHeaderTitle(ScenarioBookBrowserViewKind view, ScenarioBookType selectedType, ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Scenario Types";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return GetTypeLabel(selectedType);
            if (selectedScenario != null)
                return Safe(selectedScenario.DisplayName, selectedScenario.ScenarioId);

            return "Scenario Saves";
        }

        public string GetHeaderDetail(ScenarioBookBrowserViewKind view, ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Pick a scenario type first. Saves are shown only after a scenario is selected.";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return "Pick a scenario. The next page shows saves owned by that scenario.";
            if (selectedScenario != null)
                return Safe(selectedScenario.Description, "Choose Start New or load one of this scenario's saves.");

            return string.Empty;
        }

        public static string GetTypeLabel(ScenarioBookType type)
        {
            switch (type)
            {
                case ScenarioBookType.Surrounded: return "Surrounded Scenarios";
                case ScenarioBookType.Stasis: return "Stasis Scenarios";
                case ScenarioBookType.Draft: return "Draft Scenarios";
                default: return "Survival Scenarios";
            }
        }

        private List<ScenarioBookRowModel> BuildTypeRows()
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            rows.Add(BuildTypeRow(ScenarioBookType.Survival, "Survival Scenarios", "Custom scenarios that start from the standard shelter rules."));
            rows.Add(BuildTypeRow(ScenarioBookType.Surrounded, "Surrounded Scenarios", "Custom scenarios built on the Surrounded rule set."));
            rows.Add(BuildTypeRow(ScenarioBookType.Stasis, "Stasis Scenarios", "Custom scenarios built on the Stasis rule set."));
            if (ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
                rows.Add(BuildTypeRow(ScenarioBookType.Draft, "Draft Scenarios", "Local authoring drafts and unfinished scenario work."));
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
            ScenarioCatalogEntry[] entries = ListEntries(selectedType);
            for (int i = 0; i < entries.Length; i++)
            {
                ScenarioCatalogEntry entry = entries[i];
                if (entry == null)
                    continue;

                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Scenario,
                    Scenario = entry,
                    Title = Safe(entry.DisplayName, entry.ScenarioId),
                    Detail = BuildScenarioDetail(entry),
                    Badge = BuildScenarioBadge(entry),
                    IsLocked = !entry.CanStart
                });
            }

            if (selectedType == ScenarioBookType.Draft && ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
            {
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.CreateDraft,
                    Title = "Add New Scenario",
                    Detail = "Create a new local authoring draft.",
                    Badge = "Draft"
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
                Badge = entry.CanStart ? "Ready" : "Locked",
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
                    Title = "Slot " + save.absoluteSlot + ": " + Safe(save.name, "Saved Game"),
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

                if (entry.Source != ScenarioCatalogSource.Modded)
                    continue;

                if (type == ScenarioBookType.Survival && entry.BaseGameMode == ScenarioBaseGameMode.Survival)
                    entries.Add(entry);
                else if (type == ScenarioBookType.Surrounded && entry.BaseGameMode == ScenarioBaseGameMode.Surrounded)
                    entries.Add(entry);
                else if (type == ScenarioBookType.Stasis && entry.BaseGameMode == ScenarioBaseGameMode.Stasis)
                    entries.Add(entry);
            }

            return entries.ToArray();
        }

        private static string BuildScenarioDetail(ScenarioCatalogEntry entry)
        {
            string owner = !string.IsNullOrEmpty(entry.OwnerModId) ? entry.OwnerModId : "local";
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
            string updated = !string.IsNullOrEmpty(save.updatedAt) ? save.updatedAt : save.createdAt;
            string family = save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.familyName)
                ? save.saveInfo.familyName
                : "Unknown family";
            string days = save.saveInfo != null ? save.saveInfo.daysSurvived.ToString() + " day(s)" : "no day info";
            return family + " - " + days + " - " + Safe(updated, "unknown date");
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }
    }
}
