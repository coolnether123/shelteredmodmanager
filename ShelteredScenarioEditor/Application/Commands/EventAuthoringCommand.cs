using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Timeline;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum EventAuthoringOperation
    {
        ToggleVanillaWorldEventSuppression,
        ApplyTimelinePreset,
        AddManualTrigger, AddScheduledTrigger, DeleteTrigger, CycleTriggerType,
        AdjustTriggerDay, AdjustTriggerHour, AdjustTriggerMinute, SetTriggerTarget,
        AddGate, DeleteGate, ToggleGateMode, AddGateCondition, DeleteGateCondition,
        CycleGateConditionKind, SetGateConditionActor, SetGateConditionTarget,
        AdjustGateConditionQuantity, SetGateConditionFlagValue,
        AddScheduledAction, AddWorldEvent, DeleteScheduledAction, CycleScheduledActionType,
        CycleScheduledActionGate, ToggleScheduledActionRepeat, AdjustScheduledActionCooldown,
        AdjustScheduledActionWindowEndDay, AdjustScheduledActionChance, AdjustScheduledActionJitter,
        AdjustScheduledActionMaxRuns, AdjustScheduledActionDay, AdjustScheduledActionHour,
        AdjustScheduledActionMinute, SetWorldEventType, SetWorldEventNpcType, SetWorldEventOutcome,
        AddWorldEventTradeItem, AddWorldEventWeapon, AddWorldEventArmor,
        DeleteWorldEventTradeItem, DeleteWorldEventWeapon, DeleteWorldEventArmor,
        SetWorldEventTradeItem, SetWorldEventWeapon, SetWorldEventArmor,
        AdjustWorldEventTradeQuantity, AdjustWorldEventWeaponQuantity, AdjustWorldEventArmorQuantity,
        AdjustWorldEventRaidMinimum, AdjustWorldEventRaidMaximum,
        AddScheduledEffect, DeleteScheduledEffect, CycleScheduledEffectKind,
        SetScheduledEffectActor, SetScheduledEffectTarget, AdjustScheduledEffectQuantity,
        AdjustScheduledEffectWeatherDuration, SetScheduledEffectFlagValue,
        AddJournalEntry, ToggleFirstVanillaJournalEntry, DeleteJournalEntry, CycleJournalEntryGate,
        ToggleJournalEntryRepeat, ClearJournalEntryWriter, AdjustJournalEntryDay,
        AdjustJournalEntryHour, AdjustJournalEntryMinute, SetJournalEntryId,
        SetJournalEntryText, SetJournalEntryWriter, ToggleVanillaJournalCategory,
        OpenWorldEventEditor, OpenWorldEventItemPicker
    }

    internal sealed class EventAuthoringCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private EventAuthoringCommand(EventAuthoringOperation operation, int index, int childIndex, int delta, string value, string category)
            : base(BuildAutomationId(operation, index, childIndex, delta, value, category), IsDestructive(operation) ? ScenarioAuthoringCommandPolicy.SafetySnapshot : ScenarioAuthoringCommandPolicy.Default)
        {
            Operation = operation;
            Index = index;
            ChildIndex = childIndex;
            Delta = delta;
            Value = value;
            Category = category;
        }

        internal EventAuthoringOperation Operation { get; private set; }
        internal int Index { get; private set; }
        internal int ChildIndex { get; private set; }
        internal int Delta { get; private set; }
        internal string Value { get; private set; }
        internal string Category { get; private set; }

        internal static EventAuthoringCommand Create(EventAuthoringOperation operation, int index = -1, int childIndex = -1, int delta = 0, string value = null, string category = null)
        {
            return new EventAuthoringCommand(operation, index, childIndex, delta, value, category);
        }

        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            return Create(Operation, Index, ChildIndex, Delta, value, Category);
        }

        private static bool IsDestructive(EventAuthoringOperation operation)
        {
            return operation == EventAuthoringOperation.DeleteTrigger
                || operation == EventAuthoringOperation.DeleteGate
                || operation == EventAuthoringOperation.DeleteGateCondition
                || operation == EventAuthoringOperation.DeleteScheduledAction
                || operation == EventAuthoringOperation.DeleteWorldEventTradeItem
                || operation == EventAuthoringOperation.DeleteWorldEventWeapon
                || operation == EventAuthoringOperation.DeleteWorldEventArmor
                || operation == EventAuthoringOperation.DeleteScheduledEffect
                || operation == EventAuthoringOperation.DeleteJournalEntry;
        }

        private static string BuildAutomationId(EventAuthoringOperation operation, int index, int childIndex, int delta, string value, string category)
        {
            string i = index.ToString(CultureInfo.InvariantCulture);
            string pair = i + "." + childIndex.ToString(CultureInfo.InvariantCulture);
            string signed = i + "." + delta.ToString(CultureInfo.InvariantCulture);
            string token = ScenarioAutomationIdCodec.EncodeToken(value ?? string.Empty);
            string pairToken = pair + "." + token;
            switch (operation)
            {
                case EventAuthoringOperation.ToggleVanillaWorldEventSuppression: return ScenarioAuthoringActionIds.ActionWorldEventSuppressionPrefix + (category ?? string.Empty);
                case EventAuthoringOperation.ApplyTimelinePreset: return ScenarioTimelinePresetService.ActionPrefix + (value ?? string.Empty);
                case EventAuthoringOperation.AddManualTrigger: return ScenarioAuthoringActionIds.ActionTriggerAddManual;
                case EventAuthoringOperation.AddScheduledTrigger: return ScenarioAuthoringActionIds.ActionTriggerAddScheduled;
                case EventAuthoringOperation.DeleteTrigger: return ScenarioAuthoringActionIds.ActionTriggerDeletePrefix + i;
                case EventAuthoringOperation.CycleTriggerType: return ScenarioAuthoringActionIds.ActionTriggerTypePrefix + i;
                case EventAuthoringOperation.AdjustTriggerDay: return ScenarioAuthoringActionIds.ActionTriggerDayPrefix + signed;
                case EventAuthoringOperation.AdjustTriggerHour: return ScenarioAuthoringActionIds.ActionTriggerHourPrefix + signed;
                case EventAuthoringOperation.AdjustTriggerMinute: return ScenarioAuthoringActionIds.ActionTriggerMinutePrefix + signed;
                case EventAuthoringOperation.SetTriggerTarget: return ScenarioAuthoringActionIds.ActionTriggerTargetPrefix + i + "." + token;
                case EventAuthoringOperation.AddGate: return ScenarioAuthoringActionIds.ActionGateAdd;
                case EventAuthoringOperation.DeleteGate: return ScenarioAuthoringActionIds.ActionGateDeletePrefix + i;
                case EventAuthoringOperation.ToggleGateMode: return ScenarioAuthoringActionIds.ActionGateModePrefix + i;
                case EventAuthoringOperation.AddGateCondition: return ScenarioAuthoringActionIds.ActionGateConditionAddPrefix + i;
                case EventAuthoringOperation.DeleteGateCondition: return ScenarioAuthoringActionIds.ActionGateConditionDeletePrefix + pair;
                case EventAuthoringOperation.CycleGateConditionKind: return ScenarioAuthoringActionIds.ActionGateConditionKindPrefix + pair;
                case EventAuthoringOperation.SetGateConditionActor: return ScenarioAuthoringActionIds.ActionGateConditionActorPrefix + pairToken;
                case EventAuthoringOperation.SetGateConditionTarget: return ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix + pairToken;
                case EventAuthoringOperation.AdjustGateConditionQuantity: return ScenarioAuthoringActionIds.ActionGateConditionQuantityPrefix + pair + "." + delta.ToString(CultureInfo.InvariantCulture);
                case EventAuthoringOperation.SetGateConditionFlagValue: return ScenarioAuthoringActionIds.ActionGateConditionFlagValuePrefix + pairToken;
                case EventAuthoringOperation.AddScheduledAction: return ScenarioAuthoringActionIds.ActionScheduledActionAdd;
                case EventAuthoringOperation.AddWorldEvent: return ScenarioAuthoringActionIds.ActionWorldEventAdd;
                case EventAuthoringOperation.DeleteScheduledAction: return ScenarioAuthoringActionIds.ActionScheduledActionDeletePrefix + i;
                case EventAuthoringOperation.CycleScheduledActionType: return ScenarioAuthoringActionIds.ActionScheduledActionTypePrefix + i;
                case EventAuthoringOperation.CycleScheduledActionGate: return ScenarioAuthoringActionIds.ActionScheduledActionGatePrefix + i;
                case EventAuthoringOperation.ToggleScheduledActionRepeat: return ScenarioAuthoringActionIds.ActionScheduledActionRepeatPrefix + i;
                case EventAuthoringOperation.AdjustScheduledActionCooldown: return ScenarioAuthoringActionIds.ActionScheduledActionCooldownPrefix + signed;
                case EventAuthoringOperation.AdjustScheduledActionWindowEndDay: return ScenarioAuthoringActionIds.ActionScheduledActionWindowEndDayPrefix + signed;
                case EventAuthoringOperation.AdjustScheduledActionChance: return ScenarioAuthoringActionIds.ActionScheduledActionChancePrefix + signed;
                case EventAuthoringOperation.AdjustScheduledActionJitter: return ScenarioAuthoringActionIds.ActionScheduledActionJitterPrefix + signed;
                case EventAuthoringOperation.AdjustScheduledActionMaxRuns: return ScenarioAuthoringActionIds.ActionScheduledActionMaxRunsPrefix + signed;
                case EventAuthoringOperation.AdjustScheduledActionDay: return ScenarioAuthoringActionIds.ActionScheduledActionDayPrefix + signed;
                case EventAuthoringOperation.AdjustScheduledActionHour: return ScenarioAuthoringActionIds.ActionScheduledActionHourPrefix + signed;
                case EventAuthoringOperation.AdjustScheduledActionMinute: return ScenarioAuthoringActionIds.ActionScheduledActionMinutePrefix + signed;
                case EventAuthoringOperation.SetWorldEventType: return ScenarioAuthoringActionIds.ActionWorldEventEventTypePrefix + i + "." + token;
                case EventAuthoringOperation.SetWorldEventNpcType: return ScenarioAuthoringActionIds.ActionWorldEventNpcTypePrefix + i + "." + token;
                case EventAuthoringOperation.SetWorldEventOutcome: return ScenarioAuthoringActionIds.ActionWorldEventOutcomePrefix + i + "." + token;
                case EventAuthoringOperation.AddWorldEventTradeItem: return ScenarioAuthoringActionIds.ActionWorldEventTradeAddPrefix + i;
                case EventAuthoringOperation.AddWorldEventWeapon: return ScenarioAuthoringActionIds.ActionWorldEventWeaponAddPrefix + i;
                case EventAuthoringOperation.AddWorldEventArmor: return ScenarioAuthoringActionIds.ActionWorldEventArmorAddPrefix + i;
                case EventAuthoringOperation.DeleteWorldEventTradeItem: return ScenarioAuthoringActionIds.ActionWorldEventTradeDeletePrefix + pair;
                case EventAuthoringOperation.DeleteWorldEventWeapon: return ScenarioAuthoringActionIds.ActionWorldEventWeaponDeletePrefix + pair;
                case EventAuthoringOperation.DeleteWorldEventArmor: return ScenarioAuthoringActionIds.ActionWorldEventArmorDeletePrefix + pair;
                case EventAuthoringOperation.SetWorldEventTradeItem: return ScenarioAuthoringActionIds.ActionWorldEventTradeItemPrefix + pairToken;
                case EventAuthoringOperation.SetWorldEventWeapon: return ScenarioAuthoringActionIds.ActionWorldEventWeaponItemPrefix + pairToken;
                case EventAuthoringOperation.SetWorldEventArmor: return ScenarioAuthoringActionIds.ActionWorldEventArmorItemPrefix + pairToken;
                case EventAuthoringOperation.AdjustWorldEventTradeQuantity: return ScenarioAuthoringActionIds.ActionWorldEventTradeQuantityPrefix + pair + "." + delta.ToString(CultureInfo.InvariantCulture);
                case EventAuthoringOperation.AdjustWorldEventWeaponQuantity: return ScenarioAuthoringActionIds.ActionWorldEventWeaponQuantityPrefix + pair + "." + delta.ToString(CultureInfo.InvariantCulture);
                case EventAuthoringOperation.AdjustWorldEventArmorQuantity: return ScenarioAuthoringActionIds.ActionWorldEventArmorQuantityPrefix + pair + "." + delta.ToString(CultureInfo.InvariantCulture);
                case EventAuthoringOperation.AdjustWorldEventRaidMinimum: return ScenarioAuthoringActionIds.ActionWorldEventRaidMinPrefix + signed;
                case EventAuthoringOperation.AdjustWorldEventRaidMaximum: return ScenarioAuthoringActionIds.ActionWorldEventRaidMaxPrefix + signed;
                case EventAuthoringOperation.AddScheduledEffect: return ScenarioAuthoringActionIds.ActionScheduledActionEffectAddPrefix + i;
                case EventAuthoringOperation.DeleteScheduledEffect: return ScenarioAuthoringActionIds.ActionScheduledActionEffectDeletePrefix + pair;
                case EventAuthoringOperation.CycleScheduledEffectKind: return ScenarioAuthoringActionIds.ActionScheduledActionEffectKindPrefix + pair;
                case EventAuthoringOperation.SetScheduledEffectActor: return ScenarioAuthoringActionIds.ActionScheduledActionEffectActorPrefix + pairToken;
                case EventAuthoringOperation.SetScheduledEffectTarget: return ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix + pairToken;
                case EventAuthoringOperation.AdjustScheduledEffectQuantity: return ScenarioAuthoringActionIds.ActionScheduledActionEffectQuantityPrefix + pair + "." + delta.ToString(CultureInfo.InvariantCulture);
                case EventAuthoringOperation.AdjustScheduledEffectWeatherDuration: return ScenarioAuthoringActionIds.ActionScheduledActionEffectWeatherDurationPrefix + pair + "." + delta.ToString(CultureInfo.InvariantCulture);
                case EventAuthoringOperation.SetScheduledEffectFlagValue: return ScenarioAuthoringActionIds.ActionScheduledActionEffectFlagValuePrefix + pairToken;
                case EventAuthoringOperation.AddJournalEntry: return ScenarioAuthoringActionIds.ActionJournalEntryAdd;
                case EventAuthoringOperation.ToggleFirstVanillaJournalEntry: return ScenarioAuthoringActionIds.ActionJournalVanillaSuppressFirst;
                case EventAuthoringOperation.DeleteJournalEntry: return ScenarioAuthoringActionIds.ActionJournalEntryDeletePrefix + i;
                case EventAuthoringOperation.CycleJournalEntryGate: return ScenarioAuthoringActionIds.ActionJournalEntryGatePrefix + i;
                case EventAuthoringOperation.ToggleJournalEntryRepeat: return ScenarioAuthoringActionIds.ActionJournalEntryRepeatPrefix + i;
                case EventAuthoringOperation.ClearJournalEntryWriter: return ScenarioAuthoringActionIds.ActionJournalEntryWriterAnyPrefix + i;
                case EventAuthoringOperation.AdjustJournalEntryDay: return ScenarioAuthoringActionIds.ActionJournalEntryDayPrefix + signed;
                case EventAuthoringOperation.AdjustJournalEntryHour: return ScenarioAuthoringActionIds.ActionJournalEntryHourPrefix + signed;
                case EventAuthoringOperation.AdjustJournalEntryMinute: return ScenarioAuthoringActionIds.ActionJournalEntryMinutePrefix + signed;
                case EventAuthoringOperation.SetJournalEntryId: return ScenarioAuthoringActionIds.ActionJournalEntryIdPrefix + i + "." + token;
                case EventAuthoringOperation.SetJournalEntryText: return ScenarioAuthoringActionIds.ActionJournalEntryTextPrefix + i + "." + token;
                case EventAuthoringOperation.SetJournalEntryWriter: return ScenarioAuthoringActionIds.ActionJournalEntryWriterPrefix + i + "." + token;
                case EventAuthoringOperation.ToggleVanillaJournalCategory: return ScenarioAuthoringActionIds.ActionJournalVanillaCategoryPrefix + (category ?? string.Empty);
                case EventAuthoringOperation.OpenWorldEventEditor: return ScenarioAuthoringLocalActionIds.ActionWorldEventEditorOpenPrefix + i;
                case EventAuthoringOperation.OpenWorldEventItemPicker: return ScenarioAuthoringLocalActionIds.ActionWorldEventItemPickerOpenPrefix + i + "." + (category ?? string.Empty) + "." + childIndex.ToString(CultureInfo.InvariantCulture);
                default: return "scenario.event.unknown";
            }
        }
    }
}
