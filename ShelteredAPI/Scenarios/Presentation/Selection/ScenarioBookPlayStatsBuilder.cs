using System;
using System.Collections.Generic;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Selection;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal static class ScenarioBookPlayStatsBuilder
    {
        public static ScenarioBookPlayStatsModel Build(ScenarioCatalogEntry scenario, IList<ScenarioBookRowModel> rows)
        {
            ScenarioBookPlayStatsModel stats = new ScenarioBookPlayStatsModel();
            stats.ScoreSummary = "Score not available yet";

            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                ScenarioBookRowModel row = rows[i];
                if (row == null || row.Kind != ScenarioBookRowKind.LoadSave || row.Save == null)
                    continue;

                AddSave(stats, row.Save, row.SaveDetail);
            }

            if (stats.SaveCount == 0 && scenario != null && scenario.SaveCount > 0)
                stats.SaveCount = scenario.SaveCount;

            return stats;
        }

        private static void AddSave(ScenarioBookPlayStatsModel stats, SaveEntry save, ScenarioBookSaveDetailModel detail)
        {
            stats.SaveCount++;
            int days = detail != null ? detail.DaysSurvived : (save.saveInfo != null ? save.saveInfo.daysSurvived : 0);
            if (days > stats.BestDaySurvived)
                stats.BestDaySurvived = days;

            if (detail == null)
                return;

            AddBindingStats(stats, detail);
            AddOutcomeStats(stats, detail);
        }

        private static void AddBindingStats(ScenarioBookPlayStatsModel stats, ScenarioBookSaveDetailModel detail)
        {
            if (!detail.HasBinding)
                return;

            stats.HasBindingData = true;
            if (detail.IsConvertedToNormalSave)
                stats.ConvertedSaveCount++;
            else if (detail.IsActive)
                stats.ActiveSaveCount++;
        }

        private static void AddOutcomeStats(ScenarioBookPlayStatsModel stats, ScenarioBookSaveDetailModel detail)
        {
            if (string.IsNullOrEmpty(detail.ScenarioOutcome))
                return;

            stats.HasOutcomeData = true;
            stats.CompletedSaveCount++;
            if (string.Equals(detail.ScenarioOutcome, "Win", StringComparison.OrdinalIgnoreCase))
                stats.WinCount++;
            else if (string.Equals(detail.ScenarioOutcome, "Loss", StringComparison.OrdinalIgnoreCase))
                stats.LossCount++;
        }
    }
}
