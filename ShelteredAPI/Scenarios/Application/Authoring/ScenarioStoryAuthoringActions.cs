using System;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioStoryAuthoringActions
    {
        public const string NoneToken = "none";

        private static readonly string[] IntercomPairAddressPrefixes =
        {
            ScenarioAuthoringActionIds.ActionStoryIntercomDeletePrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomDuplicatePrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomMovePrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomIdPrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomTypePrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomNextPrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomAlternatePrefix,
            ScenarioAuthoringActionIds.ActionStoryStageChangeTargetPrefix,
            ScenarioAuthoringActionIds.ActionStoryStageChangeDelayPrefix,
            ScenarioAuthoringActionIds.ActionStoryRecruitTogglePrefix,
            ScenarioAuthoringActionIds.ActionStoryRecruitFamilyPrefix,
            ScenarioAuthoringActionIds.ActionStoryEndTypePrefix,
            ScenarioAuthoringActionIds.ActionStoryEndCompleteQuestPrefix,
            ScenarioAuthoringActionIds.ActionStoryEndCompleteScenarioPrefix,
            ScenarioAuthoringActionIds.ActionStoryDialogueAddPrefix,
            ScenarioAuthoringActionIds.ActionStoryOptionAddPrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomRandomAddPrefix,
            ScenarioAuthoringActionIds.ActionStoryRewardAddPrefix,
            ScenarioAuthoringActionIds.ActionStoryRemovalAddPrefix,
            ScenarioAuthoringActionIds.ActionStoryMilestoneAddPrefix
        };

        private static readonly string[] IntercomChildAddressPrefixes =
        {
            ScenarioAuthoringActionIds.ActionStoryDialogueDeletePrefix,
            ScenarioAuthoringActionIds.ActionStoryDialogueSpeakerPrefix,
            ScenarioAuthoringActionIds.ActionStoryDialogueKeyPrefix,
            ScenarioAuthoringActionIds.ActionStoryOptionDeletePrefix,
            ScenarioAuthoringActionIds.ActionStoryOptionKeyPrefix,
            ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomRandomDeletePrefix,
            ScenarioAuthoringActionIds.ActionStoryIntercomRandomTargetPrefix,
            ScenarioAuthoringActionIds.ActionStoryRewardDeletePrefix,
            ScenarioAuthoringActionIds.ActionStoryRewardItemPrefix,
            ScenarioAuthoringActionIds.ActionStoryRewardQuantityPrefix,
            ScenarioAuthoringActionIds.ActionStoryRemovalDeletePrefix,
            ScenarioAuthoringActionIds.ActionStoryRemovalItemPrefix,
            ScenarioAuthoringActionIds.ActionStoryRemovalQuantityPrefix,
            ScenarioAuthoringActionIds.ActionStoryMilestoneDeletePrefix,
            ScenarioAuthoringActionIds.ActionStoryMilestoneNamePrefix
        };

        public static bool CanHandle(string actionId)
        {
            return !string.IsNullOrEmpty(actionId)
                && (actionId.StartsWith("scenario.story.", StringComparison.Ordinal)
                    || actionId.StartsWith("scenario.flow.", StringComparison.Ordinal));
        }

        public static bool IsAddStage(string actionId)
        {
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionStoryStageAdd, StringComparison.Ordinal);
        }

        public static bool IsAddCharacter(string actionId)
        {
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionStoryCharacterAdd, StringComparison.Ordinal);
        }

        public static string CharacterEditPrefix(int characterIndex, string field)
        {
            return ScenarioAuthoringActionIds.ActionStoryCharacterEditPrefix
                + (field ?? string.Empty)
                + "."
                + characterIndex.ToString(CultureInfo.InvariantCulture)
                + ".";
        }

        public static string CharacterDelete(int characterIndex)
        {
            return Index(ScenarioAuthoringActionIds.ActionStoryCharacterDeletePrefix, characterIndex);
        }

        public static string StageDelete(int stageIndex)
        {
            return Index(ScenarioAuthoringActionIds.ActionStoryStageDeletePrefix, stageIndex);
        }

        public static string StageDuplicate(int stageIndex)
        {
            return Index(ScenarioAuthoringActionIds.ActionStoryStageDuplicatePrefix, stageIndex);
        }

        public static string StageMove(int stageIndex, int delta)
        {
            return IndexToken(ScenarioAuthoringActionIds.ActionStoryStageMovePrefix, stageIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static string StageId(int stageIndex, string id)
        {
            return IndexToken(ScenarioAuthoringActionIds.ActionStoryStageIdPrefix, stageIndex, EncodeToken(id));
        }

        public static string StageCharacterToggle(int stageIndex, string characterId)
        {
            return IndexToken(ScenarioAuthoringActionIds.ActionStoryStageCharacterTogglePrefix, stageIndex, EncodeToken(characterId));
        }

        public static string StageUnanswered(int stageIndex, string stageId)
        {
            return IndexToken(ScenarioAuthoringActionIds.ActionStoryStageUnansweredPrefix, stageIndex, EncodeTokenOrNone(stageId));
        }

        public static string StageUnansweredDelay(int stageIndex, int delta)
        {
            return IndexToken(ScenarioAuthoringActionIds.ActionStoryStageUnansweredDelayPrefix, stageIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static string StagePunish(int stageIndex)
        {
            return Index(ScenarioAuthoringActionIds.ActionStoryStagePunishPrefix, stageIndex);
        }

        public static string IntercomAdd(int stageIndex)
        {
            return Index(ScenarioAuthoringActionIds.ActionStoryIntercomAddPrefix, stageIndex);
        }

        public static string IntercomDelete(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryIntercomDeletePrefix, stageIndex, intercomIndex);
        }

        public static string IntercomDuplicate(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryIntercomDuplicatePrefix, stageIndex, intercomIndex);
        }

        public static string IntercomMove(int stageIndex, int intercomIndex, int delta)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryIntercomMovePrefix, stageIndex, intercomIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static string IntercomId(int stageIndex, int intercomIndex, string id)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryIntercomIdPrefix, stageIndex, intercomIndex, EncodeToken(id));
        }

        public static string IntercomType(int stageIndex, int intercomIndex, string type)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryIntercomTypePrefix, stageIndex, intercomIndex, EncodeToken(type));
        }

        public static string IntercomNext(int stageIndex, int intercomIndex, string targetId)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryIntercomNextPrefix, stageIndex, intercomIndex, EncodeTokenOrNone(targetId));
        }

        public static string IntercomAlternate(int stageIndex, int intercomIndex, string targetId)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryIntercomAlternatePrefix, stageIndex, intercomIndex, EncodeTokenOrNone(targetId));
        }

        public static string StageChangeTarget(int stageIndex, int intercomIndex, string stageId)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryStageChangeTargetPrefix, stageIndex, intercomIndex, EncodeTokenOrNone(stageId));
        }

        public static string StageChangeDelay(int stageIndex, int intercomIndex, int delta)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryStageChangeDelayPrefix, stageIndex, intercomIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static string RecruitToggle(int stageIndex, int intercomIndex, string characterId)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryRecruitTogglePrefix, stageIndex, intercomIndex, EncodeToken(characterId));
        }

        public static string RecruitFamily(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryRecruitFamilyPrefix, stageIndex, intercomIndex);
        }

        public static string EndType(int stageIndex, int intercomIndex, string type)
        {
            return PairToken(ScenarioAuthoringActionIds.ActionStoryEndTypePrefix, stageIndex, intercomIndex, EncodeToken(type));
        }

        public static string EndCompleteQuest(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryEndCompleteQuestPrefix, stageIndex, intercomIndex);
        }

        public static string EndCompleteScenario(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryEndCompleteScenarioPrefix, stageIndex, intercomIndex);
        }

        public static string DialogueAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryDialogueAddPrefix, stageIndex, intercomIndex);
        }

        public static string DialogueDelete(int stageIndex, int intercomIndex, int dialogueIndex)
        {
            return Triple(ScenarioAuthoringActionIds.ActionStoryDialogueDeletePrefix, stageIndex, intercomIndex, dialogueIndex);
        }

        public static string DialogueSpeaker(int stageIndex, int intercomIndex, int dialogueIndex, string speaker)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryDialogueSpeakerPrefix, stageIndex, intercomIndex, dialogueIndex, EncodeToken(speaker));
        }

        public static string DialogueKey(int stageIndex, int intercomIndex, int dialogueIndex, string key)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryDialogueKeyPrefix, stageIndex, intercomIndex, dialogueIndex, EncodeToken(key));
        }

        public static string OptionAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryOptionAddPrefix, stageIndex, intercomIndex);
        }

        public static string OptionDelete(int stageIndex, int intercomIndex, int optionIndex)
        {
            return Triple(ScenarioAuthoringActionIds.ActionStoryOptionDeletePrefix, stageIndex, intercomIndex, optionIndex);
        }

        public static string OptionKey(int stageIndex, int intercomIndex, int optionIndex, string key)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryOptionKeyPrefix, stageIndex, intercomIndex, optionIndex, EncodeToken(key));
        }

        public static string OptionNext(int stageIndex, int intercomIndex, int optionIndex, string targetId)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix, stageIndex, intercomIndex, optionIndex, EncodeTokenOrNone(targetId));
        }

        public static string RandomRouteAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryIntercomRandomAddPrefix, stageIndex, intercomIndex);
        }

        public static string RandomRouteDelete(int stageIndex, int intercomIndex, int routeIndex)
        {
            return Triple(ScenarioAuthoringActionIds.ActionStoryIntercomRandomDeletePrefix, stageIndex, intercomIndex, routeIndex);
        }

        public static string RandomRouteTarget(int stageIndex, int intercomIndex, int routeIndex, string targetId)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryIntercomRandomTargetPrefix, stageIndex, intercomIndex, routeIndex, EncodeTokenOrNone(targetId));
        }

        public static string RewardAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryRewardAddPrefix, stageIndex, intercomIndex);
        }

        public static string RewardDelete(int stageIndex, int intercomIndex, int itemIndex)
        {
            return Triple(ScenarioAuthoringActionIds.ActionStoryRewardDeletePrefix, stageIndex, intercomIndex, itemIndex);
        }

        public static string RewardItem(int stageIndex, int intercomIndex, int itemIndex, string itemId)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryRewardItemPrefix, stageIndex, intercomIndex, itemIndex, EncodeToken(itemId));
        }

        public static string RewardQuantity(int stageIndex, int intercomIndex, int itemIndex, int delta)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryRewardQuantityPrefix, stageIndex, intercomIndex, itemIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static string RemovalAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryRemovalAddPrefix, stageIndex, intercomIndex);
        }

        public static string RemovalDelete(int stageIndex, int intercomIndex, int itemIndex)
        {
            return Triple(ScenarioAuthoringActionIds.ActionStoryRemovalDeletePrefix, stageIndex, intercomIndex, itemIndex);
        }

        public static string RemovalItem(int stageIndex, int intercomIndex, int itemIndex, string itemId)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryRemovalItemPrefix, stageIndex, intercomIndex, itemIndex, EncodeToken(itemId));
        }

        public static string RemovalQuantity(int stageIndex, int intercomIndex, int itemIndex, int delta)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryRemovalQuantityPrefix, stageIndex, intercomIndex, itemIndex, delta.ToString(CultureInfo.InvariantCulture));
        }

        public static string MilestoneAdd(int stageIndex, int intercomIndex)
        {
            return Pair(ScenarioAuthoringActionIds.ActionStoryMilestoneAddPrefix, stageIndex, intercomIndex);
        }

        public static string MilestoneDelete(int stageIndex, int intercomIndex, int milestoneIndex)
        {
            return Triple(ScenarioAuthoringActionIds.ActionStoryMilestoneDeletePrefix, stageIndex, intercomIndex, milestoneIndex);
        }

        public static string MilestoneName(int stageIndex, int intercomIndex, int milestoneIndex, string name)
        {
            return TripleToken(ScenarioAuthoringActionIds.ActionStoryMilestoneNamePrefix, stageIndex, intercomIndex, milestoneIndex, EncodeToken(name));
        }

        public static string IntercomKey(int stageIndex, int intercomIndex)
        {
            return stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static string ChildKey(int stageIndex, int intercomIndex, int childIndex)
        {
            return IntercomKey(stageIndex, intercomIndex) + "." + childIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryResolveIntercom(string actionId, ScenarioFlowDefinition flow, out int stageIndex, out int intercomIndex, out ScenarioIntercomStageDefinition intercom)
        {
            stageIndex = -1;
            intercomIndex = -1;
            intercom = null;
            if (flow == null || flow.Stages == null)
                return false;

            for (int i = 0; i < IntercomPairAddressPrefixes.Length; i++)
            {
                string prefix = IntercomPairAddressPrefixes[i];
                string ignored;
                if (ScenarioAuthoringActionParser.TryPairIndex(actionId, prefix, flow.Stages.Count, out stageIndex, out intercomIndex)
                    || ScenarioAuthoringActionParser.TryPairToken(actionId, prefix, flow.Stages.Count, out stageIndex, out intercomIndex, out ignored))
                    return TryGetIntercom(flow, stageIndex, intercomIndex, out intercom);
            }

            for (int i = 0; i < IntercomChildAddressPrefixes.Length; i++)
            {
                string prefix = IntercomChildAddressPrefixes[i];
                int childIndex;
                string ignored;
                if (TryTriple(actionId, prefix, out stageIndex, out intercomIndex, out childIndex)
                    || TryTripleToken(actionId, prefix, out stageIndex, out intercomIndex, out childIndex, out ignored))
                    return stageIndex >= 0 && stageIndex < flow.Stages.Count && TryGetIntercom(flow, stageIndex, intercomIndex, out intercom);
            }

            return false;
        }

        public static bool TryChild(string actionId, string prefix, int stageIndex, int intercomIndex, int childCount, out int childIndex)
        {
            int first;
            int second;
            int child;
            if (!TryTriple(actionId, prefix, out first, out second, out child) || first != stageIndex || second != intercomIndex || child < 0 || child >= childCount)
            {
                childIndex = -1;
                return false;
            }

            childIndex = child;
            return true;
        }

        public static bool TryChildToken(string actionId, string prefix, int stageIndex, int intercomIndex, int childCount, out int childIndex, out string token)
        {
            int first;
            int second;
            int child;
            if (!TryTripleToken(actionId, prefix, out first, out second, out child, out token) || first != stageIndex || second != intercomIndex || child < 0 || child >= childCount)
            {
                childIndex = -1;
                token = null;
                return false;
            }

            childIndex = child;
            return true;
        }

        public static string EncodeToken(string value)
        {
            // Editable shell controls append their value through
            // ScenarioAuthoringActionCodec.  Keep story actions on that same
            // transport so an encoded route value is never persisted as text.
            return ScenarioAuthoringActionCodec.EncodeToken(value);
        }

        public static string DecodeToken(string token)
        {
            return ScenarioAuthoringActionCodec.DecodeToken(token) ?? string.Empty;
        }

        private static string EncodeTokenOrNone(string value)
        {
            return string.IsNullOrEmpty(value) ? NoneToken : EncodeToken(value);
        }

        private static bool TryGetIntercom(ScenarioFlowDefinition flow, int stageIndex, int intercomIndex, out ScenarioIntercomStageDefinition intercom)
        {
            intercom = null;
            if (stageIndex < 0 || flow == null || flow.Stages == null || stageIndex >= flow.Stages.Count)
                return false;
            ScenarioFlowStageDefinition stage = flow.Stages[stageIndex];
            if (stage == null || stage.IntercomStages == null || intercomIndex < 0 || intercomIndex >= stage.IntercomStages.Count)
                return false;
            intercom = stage.IntercomStages[intercomIndex];
            return intercom != null;
        }

        private static string Index(string prefix, int index)
        {
            return prefix + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string IndexToken(string prefix, int index, string token)
        {
            return Index(prefix, index) + "." + (token ?? string.Empty);
        }

        private static string Pair(string prefix, int first, int second)
        {
            return prefix + IntercomKey(first, second);
        }

        private static string PairToken(string prefix, int first, int second, string token)
        {
            return Pair(prefix, first, second) + "." + (token ?? string.Empty);
        }

        private static string Triple(string prefix, int first, int second, int third)
        {
            return prefix + ChildKey(first, second, third);
        }

        private static string TripleToken(string prefix, int first, int second, int third, string token)
        {
            return Triple(prefix, first, second, third) + "." + (token ?? string.Empty);
        }

        private static bool TryTriple(string actionId, string prefix, out int first, out int second, out int third)
        {
            first = -1;
            second = -1;
            third = -1;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string[] parts = actionId.Substring(prefix.Length).Split('.');
            return parts.Length == 3 && int.TryParse(parts[0], out first) && int.TryParse(parts[1], out second) && int.TryParse(parts[2], out third);
        }

        private static bool TryTripleToken(string actionId, string prefix, out int first, out int second, out int third, out string token)
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
    }
}
