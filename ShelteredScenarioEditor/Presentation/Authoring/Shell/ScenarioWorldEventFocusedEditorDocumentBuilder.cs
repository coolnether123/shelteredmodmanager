using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Inspector;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    /// <summary>Focused world-event document sections, kept out of the main presentation builder.</summary>
    internal sealed partial class ScenarioAuthoringPresentationBuilder
    {
    private static ScenarioAuthoringInspectorDocument BuildWorldEventFocusedEditorDocument(ScenarioAuthoringState state, ScenarioDefinition definition)
    {
        int actionIndex = state != null ? state.FocusedEditorIndex : -1;
        ScenarioScheduledActionDefinition action = GetScheduledAction(definition, actionIndex);
        ScenarioEffectDefinition effect = FindWorldEventEffect(action);
        int effectIndex = FindWorldEventEffectIndex(action);
        if (action == null || effect == null)
            return null;

        string eventType = ScenarioPropertyBag.GetString(effect.Properties, "eventType", "NpcVisit");
        List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
        List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
        facts.Add(Text(FormatWorldEventScheduleSummary(action) + " - " + ScenarioTimelineCreatorText.ScheduledActionName(definition, action)));
        facts.Add(Fact("Name", Safe(action.Id), "Stable timeline id saved in the scenario XML."));
        facts.Add(Property("Kind", "World event", FormatWorldEventEffect(effect), "WORLD"));
        facts.Add(Fact("Type", FormatWorldEventTypeLabel(eventType), FormatWorldEventEffect(effect)));
        facts.Add(Fact("When", FormatWorldEventScheduleSummary(action), "Shared schedule policy for this world event."));
        facts.Add(Fact("Validation", FormatWorldEventValidationState(action, effect), FormatWorldEventValidationFix(effect)));
        sections.Add(FactSection("focused_world_event_header", "WORLD EVENT", facts));

        List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
        AddWorldEventTypeActions(controls, actionIndex, eventType);
        sections.Add(ActionSection("focused_world_event_type", "WHAT / EVENT TYPE", controls));

        List<ScenarioAuthoringInspectorItem> when = new List<ScenarioAuthoringInspectorItem>();
        AddWorldEventWhenControls(when, action, actionIndex);
        sections.Add(ActionSection("focused_world_event_when", "WHEN", when));

        if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
            AddNpcVisitFocusedSections(sections, actionIndex, effectIndex, effect);
        else if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
            AddRaidFocusedSections(sections, actionIndex, effect);
        else if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
            AddBroadcastFocusedSections(sections, actionIndex, effect);
        else
            sections.Add(ActionSection("focused_world_event_unknown", "Fix", new List<ScenarioAuthoringInspectorItem>
            {
                Text("Unknown world event type. Pick Visitor, Raid, or Broadcast to repair this row."),
                ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.SetWorldEventType, actionIndex, value: "NpcVisit"), "Fix: Visitor", "Set this event to a supported NPC visit.", true, true, "WEV"))
            }));

        if (!string.IsNullOrEmpty(action.GateId) || (action.ConditionRefs != null && action.ConditionRefs.Count > 0))
        {
            List<ScenarioAuthoringInspectorItem> conditions = new List<ScenarioAuthoringInspectorItem>();
            conditions.Add(Property("Gate", string.IsNullOrEmpty(action.GateId) ? "No named gate" : action.GateId));
            for (int c = 0; action.ConditionRefs != null && c < action.ConditionRefs.Count; c++)
                conditions.Add(Property("Condition " + (c + 1).ToString(CultureInfo.InvariantCulture), ScenarioTimelineCreatorText.ConditionName(action.ConditionRefs[c].Kind), FormatConditionTarget(definition, action.ConditionRefs[c])));
            sections.Add(FactSection("focused_world_event_conditions", "CONDITIONS", conditions));
        }
        List<ScenarioAuthoringInspectorItem> advanced = new List<ScenarioAuthoringInspectorItem>();
        AddWorldEventAdvancedControls(advanced, action, actionIndex);
        sections.Add(ActionSection("focused_world_event_advanced", "ADVANCED / RUN POLICY", advanced));

        sections.Add(ActionSection("focused_world_event_footer", string.Empty, new List<ScenarioAuthoringInspectorItem>
        {
            ActionItem(Action(EditorLifecycleCommand.SaveFocusedEditor, "Save", "Close this world event editor and keep the entry.", true, true, "SV")),
            ActionItem(Action(EditorLifecycleCommand.CancelFocusedEditor, "Cancel", state != null && state.FocusedEditorIsNew ? "Discard this new world event and close the editor." : "Close this world event editor.", true, false, "CL")),
            ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.DeleteScheduledAction, actionIndex), "Remove World Event", "Remove this scheduled world event.", true, false, "RM"))
        }));

        return new ScenarioAuthoringInspectorDocument
        {
            Title = "World Event - " + FormatWorldEventTypeLabel(eventType),
            Subtitle = FormatWorldEventScheduleSummary(action),
            HeaderActions = BuildModalCloseHeaderActions(EditorLifecycleCommand.CancelFocusedEditor, "Close this world event editor."),
            Sections = sections.ToArray()
        };
    }

    private static void AddNpcVisitFocusedSections(List<ScenarioAuthoringInspectorSection> sections, int actionIndex, int effectIndex, ScenarioEffectDefinition effect)
    {
        string npcType = ScenarioPropertyBag.GetString(effect.Properties, "npcType", "Trader");
        int count = Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "count", effect.Quantity > 0 ? effect.Quantity : 1));
        List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
        items.Add(Property("Visitor Count", count.ToString(CultureInfo.InvariantCulture), "Number of scripted visitor records queued."));
        AddWorldEventNpcTypeActions(items, actionIndex, npcType);
        int safeEffectIndex = Math.Max(0, effectIndex);
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.AdjustScheduledEffectQuantity, actionIndex, safeEffectIndex, 1), "Count +", "Increase scripted visitor count.", true, false, "+")));
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.AdjustScheduledEffectQuantity, actionIndex, safeEffectIndex, -1), "Count -", "Decrease scripted visitor count.", true, false, "-")));
        sections.Add(ActionSection("focused_world_event_npc", "NPC Visit", items));

        if (string.Equals(npcType, "Trader", StringComparison.OrdinalIgnoreCase))
        {
            List<ScenarioAuthoringInspectorItem> stockItems = new List<ScenarioAuthoringInspectorItem>();
            stockItems.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.AddWorldEventTradeItem, actionIndex), "Add Stock", "Add a trader stock row using a valid item id.", true, true, "I+")));
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "focused_world_event_trade_stock",
                Title = "Trader Stock",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.InventorySlotGrid,
                Items = stockItems.ToArray(),
                InventorySlotGrid = BuildWorldEventItemSpecSlotGrid(actionIndex, "trade", "tradeItems", effect, "STOCK", "Trader stock has no authored items yet.")
            });
        }
    }

    private static void AddRaidFocusedSections(List<ScenarioAuthoringInspectorSection> sections, int actionIndex, ScenarioEffectDefinition effect)
    {
        int count = Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "count", effect.Quantity > 0 ? effect.Quantity : 1));
        int minNpcs = Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "minNpcs", count));
        int maxNpcs = Math.Max(minNpcs, ScenarioPropertyBag.GetInt(effect.Properties, "maxNpcs", count));
        List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
        items.Add(Property("NPCs", minNpcs.ToString(CultureInfo.InvariantCulture) + "-" + maxNpcs.ToString(CultureInfo.InvariantCulture), "Runtime applies these to BreachMan difficulty before the raid starts."));
        items.Add(Property("Difficulty Override", "Breach difficulty fields", "Runtime supports min/max NPCs and loadout overrides; no named difficulty tier is exposed."));
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.AdjustWorldEventRaidMinimum, actionIndex, delta: 1), "Min +", "Increase minimum raider count.", true, false, "N+")));
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.AdjustWorldEventRaidMinimum, actionIndex, delta: -1), "Min -", "Decrease minimum raider count.", true, false, "N-")));
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.AdjustWorldEventRaidMaximum, actionIndex, delta: 1), "Max +", "Increase maximum raider count.", true, false, "N+")));
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.AdjustWorldEventRaidMaximum, actionIndex, delta: -1), "Max -", "Decrease maximum raider count.", true, false, "N-")));
        sections.Add(ActionSection("focused_world_event_raid", "Raid", items));

        sections.Add(BuildWorldEventSpecGridSection(actionIndex, "weapon", "weapons", effect, "Raid Weapons", "WEAPON"));
        sections.Add(BuildWorldEventSpecGridSection(actionIndex, "armor", "armor", effect, "Raid Gear", "GEAR"));
    }

    private static void AddBroadcastFocusedSections(List<ScenarioAuthoringInspectorSection> sections, int actionIndex, ScenarioEffectDefinition effect)
    {
        string outcome = ScenarioPropertyBag.GetString(effect.Properties, "outcome", "None");
        List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
        items.Add(Property("Forced Outcome", FormatBroadcastOutcome(outcome), "Runtime forces this radio scan result."));
        AddWorldEventOutcomeActions(items, actionIndex, outcome);
        items.Add(ActionItem(Action("scenario.world_event.broadcast.vanilla_weighted", "Vanilla-weighted", "C3 runtime exposes forced radio outcomes only. To use vanilla odds, do not schedule a Broadcast world event.", false, false, "VW", "Runtime unsupported", null, null, "Runtime supports Trader, Recruit, and None forced outcomes.")));
        sections.Add(ActionSection("focused_world_event_broadcast", "Broadcast / Radio", items));
    }

    private static void AddWorldEventTypeActions(List<ScenarioAuthoringInspectorItem> items, int actionIndex, string current)
    {
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventType, actionIndex, "NPC Visit", "Queue a scripted visitor.", "NpcVisit", "WEV", string.Equals(current, "NpcVisit", StringComparison.OrdinalIgnoreCase));
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventType, actionIndex, "Raid", "Start a scripted breach.", "Raid", "RD", string.Equals(current, "Raid", StringComparison.OrdinalIgnoreCase));
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventType, actionIndex, "Broadcast", "Force a radio outcome.", "Broadcast", "BC", string.Equals(current, "Broadcast", StringComparison.OrdinalIgnoreCase) || string.Equals(current, "RadioScan", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddWorldEventNpcTypeActions(List<ScenarioAuthoringInspectorItem> items, int actionIndex, string current)
    {
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventNpcType, actionIndex, "Trader", "Use a trader visitor with optional stock.", "Trader", "TR", string.Equals(current, "Trader", StringComparison.OrdinalIgnoreCase));
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventNpcType, actionIndex, "Joiner", "Use a recruit visitor.", "Joiner", "JN", string.Equals(current, "Joiner", StringComparison.OrdinalIgnoreCase) || string.Equals(current, "Recruit", StringComparison.OrdinalIgnoreCase));
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventNpcType, actionIndex, "Passerby", "Use a passerby visitor.", "Passerby", "PB", string.Equals(current, "Passerby", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddWorldEventOutcomeActions(List<ScenarioAuthoringInspectorItem> items, int actionIndex, string current)
    {
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventOutcome, actionIndex, "Trader", "Force a trader radio result.", "Trader", "TR", string.Equals(current, "Trader", StringComparison.OrdinalIgnoreCase));
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventOutcome, actionIndex, "Recruit", "Force a recruit radio result.", "Recruit", "RC", string.Equals(current, "Recruit", StringComparison.OrdinalIgnoreCase) || string.Equals(current, "Joiner", StringComparison.OrdinalIgnoreCase));
        AddIndexedTokenAction(items, EventAuthoringOperation.SetWorldEventOutcome, actionIndex, "None", "Force no visitor from this radio scan.", "None", "NO", string.Equals(current, "None", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddIndexedTokenAction(List<ScenarioAuthoringInspectorItem> items, EventAuthoringOperation operation, int index, string label, string hint, string token, string icon, bool emphasized)
    {
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(operation, index, value: token), label, hint, true, emphasized, icon)));
    }

    private static void AddWorldEventWhenControls(List<ScenarioAuthoringInspectorItem> items, ScenarioScheduledActionDefinition action, int actionIndex)
    {
        items.Add(Property("Timing", FormatWorldEventScheduleSummary(action), "Honest runtime schedule window."));
        AddEventScheduleActions(items, EventAuthoringOperation.AdjustScheduledActionDay, EventAuthoringOperation.AdjustScheduledActionHour, EventAuthoringOperation.AdjustScheduledActionMinute, actionIndex);
    }

    private static void AddWorldEventAdvancedControls(List<ScenarioAuthoringInspectorItem> items, ScenarioScheduledActionDefinition action, int actionIndex)
    {
        ScenarioSchedulePolicy policy = action != null && action.Policy != null ? action.Policy : new ScenarioSchedulePolicy();
        items.Add(Property("Runs", policy.Repeatable ? "Repeats" : "Once", "Optional cooldown, chance, jitter, window, and max-run policy."));
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(EventAuthoringOperation.ToggleScheduledActionRepeat, actionIndex), "Repeatable", "Switch this event between once-only and repeatable execution.", true, policy.Repeatable, "RP")));
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionCooldown, actionIndex, 1440, "Cooldown +1d", "Increase repeat cooldown by one day.", "C+");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionCooldown, actionIndex, -1440, "Cooldown -1d", "Decrease repeat cooldown by one day.", "C-");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionWindowEndDay, actionIndex, 1, "Window +1d", "Extend the event window by one day.", "W+");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionWindowEndDay, actionIndex, -1, "Window -1d", "Shorten the event window by one day.", "W-");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionChance, actionIndex, 5, "Chance +5%", "Increase schedule chance by five percent.", "%+");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionChance, actionIndex, -5, "Chance -5%", "Decrease schedule chance by five percent.", "%-");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionJitter, actionIndex, 30, "Jitter +30m", "Increase random schedule jitter by 30 minutes.", "J+");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionJitter, actionIndex, -30, "Jitter -30m", "Decrease random schedule jitter by 30 minutes.", "J-");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionMaxRuns, actionIndex, 1, "Max +1", "Increase maximum successful runs.", "M+");
        AddWorldEventPolicyStep(items, EventAuthoringOperation.AdjustScheduledActionMaxRuns, actionIndex, -1, "Max -1", "Decrease maximum successful runs.", "M-");
    }

    private static void AddWorldEventPolicyStep(List<ScenarioAuthoringInspectorItem> items, EventAuthoringOperation operation, int actionIndex, int delta, string label, string hint, string icon)
    {
        items.Add(ActionItem(Action(EventAuthoringCommand.Create(operation, actionIndex, delta: delta), label, hint, true, false, icon)));
    }

    private static ScenarioAuthoringInspectorSection BuildWorldEventSpecGridSection(int actionIndex, string listKey, string propertyKey, ScenarioEffectDefinition effect, string title, string badge)
    {
        List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
        items.Add(ActionItem(Action(CreateWorldEventItemCommand(listKey, WorldEventItemCommandKind.Add, actionIndex), "Add " + title, "Add a valid item row.", true, true, "I+")));
        return new ScenarioAuthoringInspectorSection
        {
            Id = "focused_world_event_" + listKey,
            Title = title,
            Expanded = true,
            Layout = ScenarioAuthoringInspectorSectionLayout.InventorySlotGrid,
            Items = items.ToArray(),
            InventorySlotGrid = BuildWorldEventItemSpecSlotGrid(actionIndex, listKey, propertyKey, effect, badge, title + " has no authored rows yet.")
        };
    }

    private static ScenarioInventorySlotGridViewModel BuildWorldEventItemSpecSlotGrid(int actionIndex, string listKey, string propertyKey, ScenarioEffectDefinition effect, string badge, string emptyMessage)
    {
        List<ScenarioInventorySlotViewModel> slots = new List<ScenarioInventorySlotViewModel>();
        List<WorldEventItemSpec> specs = ParseWorldEventItemSpec(ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, propertyKey, null));
        for (int i = 0; i < specs.Count; i++)
        {
            WorldEventItemSpec spec = specs[i];
            ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(spec.ItemId);
            string indexText = actionIndex.ToString(CultureInfo.InvariantCulture) + "." + i.ToString(CultureInfo.InvariantCulture);
            slots.Add(new ScenarioInventorySlotViewModel
            {
                Id = "world_event." + listKey + "." + indexText,
                ItemId = catalogEntry.ItemId,
                DisplayName = catalogEntry.DisplayName,
                Detail = catalogEntry.Detail,
                QuantityText = "x" + Math.Max(1, spec.Quantity).ToString(CultureInfo.InvariantCulture),
                Badge = badge,
                Emphasized = catalogEntry.ItemType != ItemManager.ItemType.Undefined,
                PreviewSprite = catalogEntry.PreviewSprite,
                PrimaryAction = Action(
                    EventAuthoringCommand.Create(EventAuthoringOperation.OpenWorldEventItemPicker, actionIndex, i, category: listKey),
                    "Choose " + catalogEntry.DisplayName,
                    "Open the valid item picker for this world event row.",
                    true,
                    true,
                    "IT",
                    catalogEntry.ItemId),
                QuantityIncreaseAction = Action(CreateWorldEventItemCommand(listKey, WorldEventItemCommandKind.AdjustQuantity, actionIndex, i, 1), "+", "Increase this row quantity.", true, false, "+"),
                QuantityDecreaseAction = Action(CreateWorldEventItemCommand(listKey, WorldEventItemCommandKind.AdjustQuantity, actionIndex, i, -1), "-", "Decrease this row quantity.", true, false, "-"),
                RemoveAction = Action(CreateWorldEventItemCommand(listKey, WorldEventItemCommandKind.Delete, actionIndex, i), "Remove", "Remove this world event item row.", true, false, "RM")
            });
        }

        int emptySlotCount = slots.Count == 0 ? 1 : Math.Max(0, 4 - (slots.Count % 4));
        ScenarioAuthoringInspectorAction addAction = Action(
            CreateWorldEventItemCommand(listKey, WorldEventItemCommandKind.Add, actionIndex),
            "Add Row",
            "Add a valid item row.",
            true,
            true,
            "I+");
        for (int i = 0; i < emptySlotCount; i++)
        {
            slots.Add(new ScenarioInventorySlotViewModel
            {
                Id = "empty." + slots.Count.ToString(CultureInfo.InvariantCulture),
                Empty = true,
                Badge = "Empty",
                DisplayName = addAction.Label,
                Detail = "No authored item in this slot.",
                PrimaryAction = addAction
            });
        }
        return new ScenarioInventorySlotGridViewModel
        {
            EmptyMessage = emptyMessage,
            Slots = slots.ToArray()
        };
    }

    private enum WorldEventItemCommandKind { Add, Delete, Set, AdjustQuantity }

    private static EventAuthoringCommand CreateWorldEventItemCommand(string listKey, WorldEventItemCommandKind kind, int actionIndex, int itemIndex = -1, int delta = 0, string value = null)
    {
        EventAuthoringOperation operation;
        if (string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase))
            operation = kind == WorldEventItemCommandKind.Add ? EventAuthoringOperation.AddWorldEventTradeItem : kind == WorldEventItemCommandKind.Delete ? EventAuthoringOperation.DeleteWorldEventTradeItem : kind == WorldEventItemCommandKind.Set ? EventAuthoringOperation.SetWorldEventTradeItem : EventAuthoringOperation.AdjustWorldEventTradeQuantity;
        else if (string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase))
            operation = kind == WorldEventItemCommandKind.Add ? EventAuthoringOperation.AddWorldEventWeapon : kind == WorldEventItemCommandKind.Delete ? EventAuthoringOperation.DeleteWorldEventWeapon : kind == WorldEventItemCommandKind.Set ? EventAuthoringOperation.SetWorldEventWeapon : EventAuthoringOperation.AdjustWorldEventWeaponQuantity;
        else
            operation = kind == WorldEventItemCommandKind.Add ? EventAuthoringOperation.AddWorldEventArmor : kind == WorldEventItemCommandKind.Delete ? EventAuthoringOperation.DeleteWorldEventArmor : kind == WorldEventItemCommandKind.Set ? EventAuthoringOperation.SetWorldEventArmor : EventAuthoringOperation.AdjustWorldEventArmorQuantity;
        return EventAuthoringCommand.Create(operation, actionIndex, itemIndex, delta, value);
    }

    }
}
