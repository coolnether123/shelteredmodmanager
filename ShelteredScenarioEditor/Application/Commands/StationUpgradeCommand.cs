using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class StationUpgradeAutomationIds
    {
        public const string ChangeLevelPrefix = "station.level.";
        public const string ChangeUpgradePrefix = "station.upgrade.";
        public const string ChangeStatPrefix = "station.stat.";
        public const string ClearStatPrefix = "station.stat_clear.";
    }

    internal enum StationUpgradeCommandKind
    {
        ChangeObjectLevel,
        ChangeUpgradeLevel,
        ChangeStat,
        ClearStat
    }

    internal sealed class StationUpgradeCommand : ScenarioAuthoringCommand
    {
        private StationUpgradeCommand(StationUpgradeCommandKind kind, string name, int levelDelta, float statDelta, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.WorldSafetySnapshot)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            LevelDelta = levelDelta;
            StatDelta = statDelta;
        }

        public StationUpgradeCommandKind Kind { get; private set; }
        public string Name { get; private set; }
        public int LevelDelta { get; private set; }
        public float StatDelta { get; private set; }

        public static StationUpgradeCommand ChangeObjectLevel(int delta)
        {
            return new StationUpgradeCommand(
                StationUpgradeCommandKind.ChangeObjectLevel,
                null,
                delta,
                0f,
                StationUpgradeAutomationIds.ChangeLevelPrefix + delta.ToString(CultureInfo.InvariantCulture));
        }

        public static StationUpgradeCommand ChangeUpgradeLevel(string upgradeName, int delta)
        {
            return new StationUpgradeCommand(
                StationUpgradeCommandKind.ChangeUpgradeLevel,
                upgradeName,
                delta,
                0f,
                StationUpgradeAutomationIds.ChangeUpgradePrefix + (upgradeName ?? string.Empty) + "." + delta.ToString(CultureInfo.InvariantCulture));
        }

        public static StationUpgradeCommand ChangeStat(string statName, float delta)
        {
            return new StationUpgradeCommand(
                StationUpgradeCommandKind.ChangeStat,
                statName,
                0,
                delta,
                StationUpgradeAutomationIds.ChangeStatPrefix + (statName ?? string.Empty) + "." + delta.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public static StationUpgradeCommand ClearStat(string statName)
        {
            return new StationUpgradeCommand(
                StationUpgradeCommandKind.ClearStat,
                statName,
                0,
                0f,
                StationUpgradeAutomationIds.ClearStatPrefix + (statName ?? string.Empty));
        }
    }
}
