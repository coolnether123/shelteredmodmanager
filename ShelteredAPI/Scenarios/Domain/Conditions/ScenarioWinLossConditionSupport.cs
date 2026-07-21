using System;

namespace ShelteredAPI.Scenarios.Domain.Conditions{
    internal enum ScenarioWinLossConditionFieldKind
    {
        None = 0,
        Time = 1,
        Quantity = 2,
        Target = 3,
        Flag = 4
    }

    internal sealed class ScenarioWinLossConditionDescriptor
    {
        public ScenarioWinLossConditionDescriptor(
            string canonicalType,
            string label,
            string summary,
            ScenarioConditionKind runtimeKind,
            ScenarioWinLossConditionFieldKind fieldKind)
        {
            CanonicalType = canonicalType;
            Label = label;
            Summary = summary;
            RuntimeKind = runtimeKind;
            FieldKind = fieldKind;
        }

        public string CanonicalType { get; private set; }
        public string Label { get; private set; }
        public string Summary { get; private set; }
        public ScenarioConditionKind RuntimeKind { get; private set; }
        public ScenarioWinLossConditionFieldKind FieldKind { get; private set; }
    }

    internal static class ScenarioWinLossConditionSupport
    {
        private static readonly ScenarioWinLossConditionDescriptor[] Descriptors = new[]
        {
            new ScenarioWinLossConditionDescriptor("surviveDays", "Survive Days", "End after the shelter survives a number of days from scenario start.", ScenarioConditionKind.TimeReached, ScenarioWinLossConditionFieldKind.Time),
            new ScenarioWinLossConditionDescriptor("timeReached", "Day Reached", "End when the runtime clock reaches a day/hour/minute.", ScenarioConditionKind.TimeReached, ScenarioWinLossConditionFieldKind.Time),
            new ScenarioWinLossConditionDescriptor("itemQuantityAvailable", "Item Quantity Available", "End when the shelter has at least the requested item quantity.", ScenarioConditionKind.ItemQuantityAvailable, ScenarioWinLossConditionFieldKind.Quantity),
            new ScenarioWinLossConditionDescriptor("questCompleted", "Quest Completed", "End when a scenario quest is completed.", ScenarioConditionKind.QuestCompleted, ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor("questFailed", "Quest Failed", "End when a scenario quest fails.", ScenarioConditionKind.QuestFailed, ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor("questActive", "Quest Active", "End when a scenario quest is active.", ScenarioConditionKind.QuestActive, ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor("survivorPresent", "Survivor Present", "End when a survivor id/name is present.", ScenarioConditionKind.SurvivorPresent, ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor("bunkerExpansionUnlocked", "Bunker Expansion Unlocked", "End when a bunker expansion id is unlocked.", ScenarioConditionKind.BunkerExpansionUnlocked, ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor("scenarioFlagSet", "Scenario Flag Set", "End when a scenario flag is set to the requested value.", ScenarioConditionKind.ScenarioFlagSet, ScenarioWinLossConditionFieldKind.Flag),
            new ScenarioWinLossConditionDescriptor("customTrigger", "Custom Trigger Fired", "End when a custom trigger has fired.", ScenarioConditionKind.CustomTrigger, ScenarioWinLossConditionFieldKind.Target)
        };

        public static ScenarioWinLossConditionDescriptor[] GetDescriptors()
        {
            ScenarioWinLossConditionDescriptor[] copy = new ScenarioWinLossConditionDescriptor[Descriptors.Length];
            Array.Copy(Descriptors, copy, Descriptors.Length);
            return copy;
        }

        public static ScenarioWinLossConditionDescriptor GetDefaultDescriptor()
        {
            return Descriptors[1];
        }

        public static bool TryGetDescriptor(string type, out ScenarioWinLossConditionDescriptor descriptor)
        {
            string normalized = Normalize(type);
            for (int i = 0; i < Descriptors.Length; i++)
            {
                ScenarioWinLossConditionDescriptor candidate = Descriptors[i];
                if (candidate != null && string.Equals(Normalize(candidate.CanonicalType), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    descriptor = candidate;
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        public static ScenarioWinLossConditionDescriptor NextDescriptor(string currentType)
        {
            string normalized = Normalize(currentType);
            for (int i = 0; i < Descriptors.Length; i++)
            {
                ScenarioWinLossConditionDescriptor candidate = Descriptors[i];
                if (candidate != null && string.Equals(Normalize(candidate.CanonicalType), normalized, StringComparison.OrdinalIgnoreCase))
                    return Descriptors[(i + 1) % Descriptors.Length];
            }

            return GetDefaultDescriptor();
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            return value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
