using System.Globalization;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal static class ScenarioBookScoreDisplayReader
    {
        public static void ApplyScoreDisplay(ScenarioCatalogEntry scenario, ScenarioBookPlayStatsModel stats)
        {
            if (stats == null)
                return;

            stats.ScoreSummary = null;
            if (stats.ScoreLines != null)
                stats.ScoreLines.Clear();

            if (scenario == null)
                return;

            if (scenario.Source == ScenarioCatalogSource.Vanilla)
            {
                ApplyVanillaScoreDisplay(scenario, stats);
                return;
            }

            ApplyCustomScoreDisplay(stats);
        }

        public static string BuildSaveScoreLabel(ScenarioBookSaveDetailModel detail)
        {
            if (detail == null || !detail.HasScoreSnapshot || !detail.ScoreHasTotal)
                return string.Empty;

            return "Score: " + detail.ScoreTotal.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryGetScoreTotal(ScenarioBookSaveDetailModel detail, out int score)
        {
            score = 0;
            if (detail == null || !detail.HasScoreSnapshot || !detail.ScoreHasTotal)
                return false;

            score = detail.ScoreTotal;
            return true;
        }

        private static void ApplyVanillaScoreDisplay(ScenarioCatalogEntry scenario, ScenarioBookPlayStatsModel stats)
        {
            SaveGlobal global = null;
            try { global = SaveGlobal.Instance; }
            catch { }

            if (global == null)
                return;

            // Vanilla game-over panels delete the current slot after completion,
            // so a per-run leaderboard needs a future archive. For now the book
            // can only show the persisted global best-score counters.
            if (scenario.BaseGameMode == ScenarioBaseGameMode.Surrounded)
            {
                int points = global.SurroundedPoints;
                if (points <= 0)
                    return;

                stats.HasScoreData = true;
                stats.ScoreSummary = "Best Score: " + points.ToString(CultureInfo.InvariantCulture);
                AddLine(stats, "Best Score", points.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (scenario.BaseGameMode != ScenarioBaseGameMode.Stasis)
                return;

            int days = global.StasisHighScore_days;
            int books = global.StasisHighScore_books;
            int embryos = global.StasisHighScore_embryos;
            int seeds = global.StasisHighScore_seeds;
            if (days <= 0 && books <= 0 && embryos <= 0 && seeds <= 0)
                return;

            stats.HasScoreData = true;
            stats.ScoreSummary = "Best Stasis: "
                + days.ToString(CultureInfo.InvariantCulture) + " days, "
                + books.ToString(CultureInfo.InvariantCulture) + " books, "
                + embryos.ToString(CultureInfo.InvariantCulture) + " embryos, "
                + seeds.ToString(CultureInfo.InvariantCulture) + " seeds";
            AddLine(stats, "Best Days", days.ToString(CultureInfo.InvariantCulture));
            AddLine(stats, "Books", books.ToString(CultureInfo.InvariantCulture));
            AddLine(stats, "Embryos", embryos.ToString(CultureInfo.InvariantCulture));
            AddLine(stats, "Seeds", seeds.ToString(CultureInfo.InvariantCulture));
        }

        private static void ApplyCustomScoreDisplay(ScenarioBookPlayStatsModel stats)
        {
            if (!stats.HasBestScoreTotal)
                return;

            string score = stats.BestScoreTotal.ToString(CultureInfo.InvariantCulture);
            stats.ScoreSummary = "Best Score: " + score;
            AddLine(stats, "Best Score", score);
            AddLine(stats, "Scored Saves", stats.ScoredSaveCount.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddLine(ScenarioBookPlayStatsModel stats, string label, string value)
        {
            if (stats == null || stats.ScoreLines == null || string.IsNullOrEmpty(value))
                return;

            stats.ScoreLines.Add(new ScenarioBookStatLine
            {
                Label = label,
                Value = value
            });
        }
    }
}
