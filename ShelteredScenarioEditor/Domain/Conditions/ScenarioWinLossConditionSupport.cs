using System;
using ShelteredAPI.Scenarios.Domain.Conditions;

namespace ShelteredScenarioEditor.Domain.Conditions{
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
            ScenarioConditionKind kind,
            string label,
            string summary,
            ScenarioWinLossConditionFieldKind fieldKind)
        {
            Kind = kind;
            Label = label;
            Summary = summary;
            FieldKind = fieldKind;
        }

        public ScenarioConditionKind Kind { get; private set; }
        public string Label { get; private set; }
        public string Summary { get; private set; }
        public ScenarioWinLossConditionFieldKind FieldKind { get; private set; }
    }

    internal static class ScenarioWinLossConditionSupport
    {
        private static readonly ScenarioWinLossConditionDescriptor[] Descriptors = new[]
        {
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.SurviveDays, "Survive Days", "End after the shelter survives a number of days from scenario start.", ScenarioWinLossConditionFieldKind.Time),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.TimeReached, "Day Reached", "End when the runtime clock reaches a day/hour/minute.", ScenarioWinLossConditionFieldKind.Time),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.ItemQuantityAvailable, "Item Quantity Available", "End when the shelter has at least the requested item quantity.", ScenarioWinLossConditionFieldKind.Quantity),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.QuestCompleted, "Quest Completed", "End when a scenario quest is completed.", ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.QuestFailed, "Quest Failed", "End when a scenario quest fails.", ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.QuestActive, "Quest Active", "End when a scenario quest is active.", ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.SurvivorPresent, "Survivor Present", "End when a survivor id/name is present.", ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.BunkerExpansionUnlocked, "Bunker Expansion Unlocked", "End when a bunker expansion id is unlocked.", ScenarioWinLossConditionFieldKind.Target),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.ScenarioFlagSet, "Scenario Flag Set", "End when a scenario flag is set to the requested value.", ScenarioWinLossConditionFieldKind.Flag),
            new ScenarioWinLossConditionDescriptor(ScenarioConditionKind.CustomTrigger, "Custom Trigger Fired", "End when a custom trigger has fired.", ScenarioWinLossConditionFieldKind.Target)
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

        public static bool TryGetDescriptor(ScenarioConditionKind kind, out ScenarioWinLossConditionDescriptor descriptor)
        {
            for (int i = 0; i < Descriptors.Length; i++)
            {
                ScenarioWinLossConditionDescriptor candidate = Descriptors[i];
                if (candidate != null && candidate.Kind == kind)
                {
                    descriptor = candidate;
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        public static ScenarioWinLossConditionDescriptor NextDescriptor(ScenarioConditionKind currentKind)
        {
            for (int i = 0; i < Descriptors.Length; i++)
            {
                ScenarioWinLossConditionDescriptor candidate = Descriptors[i];
                if (candidate != null && candidate.Kind == currentKind)
                    return Descriptors[(i + 1) % Descriptors.Length];
            }

            return GetDefaultDescriptor();
        }
    }
}
