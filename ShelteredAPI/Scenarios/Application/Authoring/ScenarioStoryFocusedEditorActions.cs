using System;
using System.Globalization;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioStoryFocusedEditorActions
    {
        public const string FocusedEditorKind = "story_stage";
        public const string FocusedEntryPrefix = "story_stage:";
        public const string ActionStageOpenPrefix = "scenario.story.focused_editor.stage.open.";
        public const string ActionStageOpenNew = "scenario.story.focused_editor.stage.open_new";
        public const string ActionSave = "scenario.story.focused_editor.save";
        public const string ActionCancel = "scenario.story.focused_editor.cancel";
        public const string ActionStageTitlePrefix = "scenario.story.focused_editor.stage.title.";
        public const string ActionUnansweredNewStagePrefix = "scenario.story.focused_editor.stage.unanswered_new.";
        public const string ActionStageChangeNewStagePrefix = "scenario.story.focused_editor.stage_change.new.";
        public const string ActionEndRewardAddPrefix = "scenario.story.focused_editor.end_reward.add.";
        public const string ActionEndRewardDeletePrefix = "scenario.story.focused_editor.end_reward.delete.";
        public const string ActionEndRewardItemPrefix = "scenario.story.focused_editor.end_reward.item.";
        public const string ActionEndRewardQuantityPrefix = "scenario.story.focused_editor.end_reward.quantity.";
        public const string ActionTradeOverridePrefix = "scenario.story.focused_editor.trade.override.";
        public const string ActionTradeAddPrefix = "scenario.story.focused_editor.trade.add.";
        public const string ActionTradeDeletePrefix = "scenario.story.focused_editor.trade.delete.";
        public const string ActionTradeItemPrefix = "scenario.story.focused_editor.trade.item.";
        public const string ActionTradeQuantityPrefix = "scenario.story.focused_editor.trade.quantity.";

        public static bool CanHandle(string actionId)
        {
            return !string.IsNullOrEmpty(actionId)
                && actionId.StartsWith("scenario.story.focused_editor.", StringComparison.Ordinal);
        }

        public static string StageOpen(int stageIndex)
        {
            return ActionStageOpenPrefix + stageIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static string StageTitle(int stageIndex, string title)
        {
            return ActionStageTitlePrefix + stageIndex.ToString(CultureInfo.InvariantCulture) + "." + ScenarioStoryAuthoringActions.EncodeToken(title);
        }

        public static string UnansweredNewStage(int stageIndex)
        {
            return ActionUnansweredNewStagePrefix + stageIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static string StageChangeNewStage(int stageIndex, int intercomIndex)
        {
            return Pair(ActionStageChangeNewStagePrefix, stageIndex, intercomIndex);
        }

        public static string FocusedEntryId(int stageIndex)
        {
            return FocusedEntryPrefix + stageIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static string EndRewardAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ActionEndRewardAddPrefix, stageIndex, intercomIndex);
        }

        public static string EndRewardDelete(int stageIndex, int intercomIndex, int itemIndex)
        {
            return Triple(ActionEndRewardDeletePrefix, stageIndex, intercomIndex, itemIndex);
        }

        public static string EndRewardItem(int stageIndex, int intercomIndex, int itemIndex, string itemId)
        {
            return TripleToken(ActionEndRewardItemPrefix, stageIndex, intercomIndex, itemIndex, ScenarioStoryAuthoringActions.EncodeToken(itemId));
        }

        public static string EndRewardQuantity(int stageIndex, int intercomIndex, int itemIndex, int delta)
        {
            return TripleToken(ActionEndRewardQuantityPrefix, stageIndex, intercomIndex, itemIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static string TradeOverride(int stageIndex, int intercomIndex)
        {
            return Pair(ActionTradeOverridePrefix, stageIndex, intercomIndex);
        }

        public static string TradeAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ActionTradeAddPrefix, stageIndex, intercomIndex);
        }

        public static string TradeDelete(int stageIndex, int intercomIndex, int itemIndex)
        {
            return Triple(ActionTradeDeletePrefix, stageIndex, intercomIndex, itemIndex);
        }

        public static string TradeItem(int stageIndex, int intercomIndex, int itemIndex, string itemId)
        {
            return TripleToken(ActionTradeItemPrefix, stageIndex, intercomIndex, itemIndex, ScenarioStoryAuthoringActions.EncodeToken(itemId));
        }

        public static string TradeQuantity(int stageIndex, int intercomIndex, int itemIndex, int delta)
        {
            return TripleToken(ActionTradeQuantityPrefix, stageIndex, intercomIndex, itemIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static bool TryTripleToken(string actionId, string prefix, out int first, out int second, out int third, out string token)
        {
            first = -1;
            second = -1;
            third = -1;
            token = null;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string[] parts = actionId.Substring(prefix.Length).Split(new[] { '.' }, 4);
            if (parts.Length != 4 || !int.TryParse(parts[0], out first) || !int.TryParse(parts[1], out second) || !int.TryParse(parts[2], out third))
                return false;
            token = parts[3];
            return true;
        }

        public static bool TryTriple(string actionId, string prefix, out int first, out int second, out int third)
        {
            first = -1;
            second = -1;
            third = -1;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string[] parts = actionId.Substring(prefix.Length).Split('.');
            return parts.Length == 3 && int.TryParse(parts[0], out first) && int.TryParse(parts[1], out second) && int.TryParse(parts[2], out third);
        }

        private static string Pair(string prefix, int first, int second)
        {
            return prefix + first.ToString(CultureInfo.InvariantCulture) + "." + second.ToString(CultureInfo.InvariantCulture);
        }

        private static string Triple(string prefix, int first, int second, int third)
        {
            return Pair(prefix, first, second) + "." + third.ToString(CultureInfo.InvariantCulture);
        }

        private static string TripleToken(string prefix, int first, int second, int third, string token)
        {
            return Triple(prefix, first, second, third) + "." + (token ?? string.Empty);
        }
    }
}
