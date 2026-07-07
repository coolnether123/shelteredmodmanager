using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Application.Scheduling
{
    internal sealed class ScenarioJournalScheduledActionProvider : IScenarioScheduledActionProvider
    {
        public void AddActions(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            ScenarioJournalVanillaPolicyRuntime.SetActiveDefinition(definition);

            if (definition == null || definition.Journal == null || definition.Journal.Entries == null || target == null)
                return;

            for (int i = 0; i < definition.Journal.Entries.Count; i++)
            {
                JournalEntryDefinition entry = definition.Journal.Entries[i];
                if (entry == null)
                    continue;

                target.Add(BuildAction(entry, i));
            }
        }

        private static ScenarioScheduledActionDefinition BuildAction(JournalEntryDefinition entry, int index)
        {
            string entryId = !string.IsNullOrEmpty(entry.Id)
                ? entry.Id
                : index.ToString(CultureInfo.InvariantCulture);

            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
            action.Id = "journal." + entryId;
            action.ActionType = "WriteJournalEntry";
            action.GateId = entry.GateId;
            action.DueTime = entry.DueTime != null ? entry.DueTime : new ScenarioScheduleTime();
            action.Policy.Repeatable = entry.Mode == ScenarioJournalEntryMode.Repeat;
            action.Policy.CooldownMinutes = entry.CooldownMinutes;

            if (!string.IsNullOrEmpty(entry.TriggerId))
            {
                action.ConditionRefs.Add(new ScenarioConditionRef
                {
                    Id = entryId + ".trigger",
                    Kind = ScenarioConditionKind.CustomTrigger,
                    TargetId = entry.TriggerId
                });
            }

            for (int i = 0; entry.Conditions != null && i < entry.Conditions.Count; i++)
                action.ConditionRefs.Add(CopyCondition(entry.Conditions[i]));

            ScenarioEffectDefinition effect = new ScenarioEffectDefinition();
            effect.Id = "journal." + entryId + ".write";
            effect.Kind = ScenarioEffectKind.WriteJournalEntry;
            effect.TargetId = entryId;
            effect.ActorRef = CopyActorRef(entry.Writer);
            ScenarioPropertyBag.Set(effect.Properties, "entryId", entryId);
            ScenarioPropertyBag.Set(effect.Properties, "text", entry.Text ?? string.Empty);
            ScenarioPropertyBag.Set(effect.Properties, "repeatable", action.Policy.Repeatable ? "true" : "false");
            ScenarioPropertyBag.Set(effect.Properties, "format", "WriterPrefix");
            ScenarioPropertyBag.Set(effect.Properties, "writerMode", entry.Writer != null ? "Specific" : "AnyPresent");
            action.Effects.Add(effect);
            return action;
        }

        private static ScenarioConditionRef CopyCondition(ScenarioConditionRef source)
        {
            if (source == null)
                return null;

            ScenarioConditionRef copy = new ScenarioConditionRef();
            copy.Id = source.Id;
            copy.Kind = source.Kind;
            copy.TargetId = source.TargetId;
            copy.ActorRef = CopyActorRef(source.ActorRef);
            copy.Comparison = source.Comparison;
            copy.Quantity = source.Quantity;
            copy.StatId = source.StatId;
            copy.StatValue = source.StatValue;
            copy.TraitId = source.TraitId;
            copy.FlagId = source.FlagId;
            copy.FlagValue = source.FlagValue;
            copy.Time = source.Time;
            for (int i = 0; source.Properties != null && i < source.Properties.Count; i++)
            {
                ScenarioProperty property = source.Properties[i];
                if (property != null)
                    copy.Properties.Add(new ScenarioProperty { Key = property.Key, Value = property.Value });
            }
            return copy;
        }

        private static ScenarioActorRef CopyActorRef(ScenarioActorRef source)
        {
            if (source == null)
                return null;

            return new ScenarioActorRef
            {
                Kind = source.Kind,
                LocalId = source.LocalId,
                Domain = source.Domain,
                BindingType = source.BindingType,
                BindingKey = source.BindingKey,
                DisplayNameFallback = source.DisplayNameFallback,
                RequiredModId = source.RequiredModId
            };
        }
    }
}
