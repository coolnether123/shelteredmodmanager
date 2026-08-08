using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Selection;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioLibraryOrganizationVerification
    {
        public static void Verify(string root, ScenarioValidationResult result)
        {
            string preferencePath = Path.Combine(Path.Combine(root, "ScenarioLibrary"), "library.json");
            ScenarioLibraryPreferenceStore preferences = new ScenarioLibraryPreferenceStore(preferencePath);
            List<ScenarioBookRowModel> rows = BuildRows();

            AssertOrder(ScenarioLibraryOrganizer.Order(rows, ScenarioLibrarySortMode.PinnedFirst, preferences),
                "tool", "new-play", "old-play", "unknown", result, "default");
            AssertOrder(ScenarioLibraryOrganizer.Order(rows, ScenarioLibrarySortMode.RecentlyPlayed, preferences),
                "tool", "new-play", "old-play", "unknown", result, "recently played");
            AssertOrder(ScenarioLibraryOrganizer.Order(rows, ScenarioLibrarySortMode.RecentlyDownloaded, preferences),
                "tool", "unknown", "old-play", "new-play", result, "recently downloaded");
            AssertOrder(ScenarioLibraryOrganizer.Order(rows, ScenarioLibrarySortMode.CreationDate, preferences),
                "tool", "new-play", "old-play", "unknown", result, "creation date");
            AssertOrder(ScenarioLibraryOrganizer.Order(rows, ScenarioLibrarySortMode.Name, preferences),
                "tool", "old-play", "new-play", "unknown", result, "name");

            preferences.TogglePinned("unknown");
            preferences.SetSortMode(ScenarioLibrarySortMode.RecentlyPlayed);
            ScenarioLibraryPreferenceStore reloaded = new ScenarioLibraryPreferenceStore(preferencePath);
            Assert(reloaded.IsPinned("UNKNOWN"), "Pin state did not survive a store reload.", result);
            Assert(reloaded.SortMode == ScenarioLibrarySortMode.RecentlyPlayed, "Sort mode did not survive a store reload.", result);
            AssertOrder(ScenarioLibraryOrganizer.Order(rows, ScenarioLibrarySortMode.CreationDate, reloaded),
                "tool", "unknown", "new-play", "old-play", result, "pinned creation date");
            ScenarioLibrarySortMode[] modes = (ScenarioLibrarySortMode[])Enum.GetValues(typeof(ScenarioLibrarySortMode));
            for (int i = 0; i < modes.Length; i++)
            {
                IList<ScenarioBookRowModel> pinnedOrder = ScenarioLibraryOrganizer.Order(rows, modes[i], reloaded);
                Assert(Id(pinnedOrder, 0) == "tool" && Id(pinnedOrder, 1) == "unknown",
                    "Pinned row did not lead scenario rows in " + modes[i] + " mode.", result);
            }

            List<ScenarioBookRowModel> rowsWithArchive = BuildRows();
            rowsWithArchive.Add(new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.OpenScenarioSaves,
                Scenario = new ScenarioCatalogEntry
                {
                    ScenarioId = "unlimited-surrounded",
                    BaseGameMode = ScenarioBaseGameMode.Surrounded
                },
                Title = "Surrounded - Unlimited Saves"
            });
            IList<ScenarioBookRowModel> archiveOrder = ScenarioLibraryOrganizer.Order(
                rowsWithArchive,
                ScenarioLibrarySortMode.Name,
                reloaded);
            Assert(Id(archiveOrder, archiveOrder.Count - 1) == "unlimited-surrounded",
                "Unlimited vanilla archives were mixed into installed custom scenario sorting.", result);

            string relative = ScenarioLibraryOrganizer.RelativePlayed(
                new DateTime(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc));
            Assert(relative == "played 2h ago", "Relative play-time label was not compact and deterministic.", result);
        }

        private static List<ScenarioBookRowModel> BuildRows()
        {
            DateTime baseline = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            return new List<ScenarioBookRowModel>
            {
                new ScenarioBookRowModel { Kind = ScenarioBookRowKind.Type, Title = "tool" },
                Row("old-play", "Alpha", baseline, baseline.AddDays(9), baseline.AddDays(1), 20),
                Row("new-play", "Bravo", baseline.AddDays(8), baseline.AddDays(8), baseline.AddDays(7), 10),
                Row("unknown", "Zulu", null, baseline.AddDays(10), null, 30)
            };
        }

        private static ScenarioBookRowModel Row(
            string id,
            string title,
            DateTime? played,
            DateTime? installed,
            DateTime? created,
            int order)
        {
            ScenarioCatalogEntry scenario = new ScenarioCatalogEntry
            {
                ScenarioId = id,
                DisplayName = title,
                LastPlayedUtc = played,
                InstalledUtc = installed,
                CreatedUtc = created,
                Order = order
            };
            return new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Scenario,
                Scenario = scenario,
                Title = title
            };
        }

        private static void AssertOrder(
            IList<ScenarioBookRowModel> rows,
            string first,
            string second,
            string third,
            string fourth,
            ScenarioValidationResult result,
            string mode)
        {
            string actual = Id(rows, 0) + "," + Id(rows, 1) + "," + Id(rows, 2) + "," + Id(rows, 3);
            string expected = first + "," + second + "," + third + "," + fourth;
            Assert(actual == expected, "Unexpected " + mode + " order. Expected " + expected + "; got " + actual + ".", result);
        }

        private static string Id(IList<ScenarioBookRowModel> rows, int index)
        {
            ScenarioBookRowModel row = rows != null && index >= 0 && index < rows.Count ? rows[index] : null;
            return row != null && row.Scenario != null ? row.Scenario.ScenarioId : (row != null ? row.Title : "<missing>");
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition)
                result.AddError("Scenario library organization contract: " + message);
        }
    }
}
