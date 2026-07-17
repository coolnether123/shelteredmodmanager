using System;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioStoryFocusedEditorActions
    {
        public const string WorkspaceId = "story";
        public const string FlowSubtabId = "flow";
        public const string CharactersSubtabId = "characters";
        public const string ConversationsSubtabId = "conversations";
        public const string QuestPopupsSubtabId = "quest-popups";
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

        public static string StageEntityId(ScenarioDefinition definition, int stageIndex)
        {
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            ScenarioFlowStageDefinition stage = flow != null && flow.Stages != null && stageIndex >= 0 && stageIndex < flow.Stages.Count
                ? flow.Stages[stageIndex]
                : null;
            string id = TrimToNull(stage != null ? stage.Id : null);
            return id != null && CountStageIds(flow, id) == 1
                ? "stage.id." + ScenarioAuthoringActionCodec.EncodeToken(id)
                : "stage.index." + stageIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static string SceneEntityId(ScenarioDefinition definition, int stageIndex, int sceneIndex)
        {
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            ScenarioFlowStageDefinition stage = flow != null && flow.Stages != null && stageIndex >= 0 && stageIndex < flow.Stages.Count
                ? flow.Stages[stageIndex]
                : null;
            ScenarioIntercomStageDefinition scene = stage != null && stage.IntercomStages != null && sceneIndex >= 0 && sceneIndex < stage.IntercomStages.Count
                ? stage.IntercomStages[sceneIndex]
                : null;
            string id = TrimToNull(scene != null ? scene.Id : null);
            string sceneToken = id != null && CountSceneIds(stage, id) == 1
                ? "id." + ScenarioAuthoringActionCodec.EncodeToken(id)
                : "index." + sceneIndex.ToString(CultureInfo.InvariantCulture);
            return "scene." + ScenarioAuthoringActionCodec.EncodeToken(StageEntityId(definition, stageIndex)) + "." + sceneToken;
        }

        public static string CharacterEntityId(ScenarioDefinition definition, int characterIndex)
        {
            ScenarioNpcDefinition character = definition != null && definition.ScenarioCharacters != null && characterIndex >= 0 && characterIndex < definition.ScenarioCharacters.Count
                ? definition.ScenarioCharacters[characterIndex]
                : null;
            string id = TrimToNull(character != null ? character.CharacterId : null);
            return id != null && CountCharacterIds(definition, id) == 1
                ? "character.id." + ScenarioAuthoringActionCodec.EncodeToken(id)
                : "character.index." + characterIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static string ConversationEntityId(ScenarioDefinition definition, int conversationIndex)
        {
            ScenarioConversationAuthoringDefinition authored = definition != null ? definition.Conversations : null;
            ScenarioConversationDefinition conversation = authored != null && authored.Conversations != null && conversationIndex >= 0 && conversationIndex < authored.Conversations.Count
                ? authored.Conversations[conversationIndex]
                : null;
            string id = TrimToNull(conversation != null ? conversation.Id : null);
            return id != null && CountConversationIds(authored, id) == 1
                ? "conversation.id." + ScenarioAuthoringActionCodec.EncodeToken(id)
                : "conversation.index." + conversationIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static string QuestEntityId(ScenarioDefinition definition, int questIndex)
        {
            QuestDefinition quest = definition != null && definition.Quests != null && definition.Quests.Quests != null
                && questIndex >= 0 && questIndex < definition.Quests.Quests.Count
                    ? definition.Quests.Quests[questIndex]
                    : null;
            string id = TrimToNull(quest != null ? quest.Id : null);
            return id != null && CountQuestIds(definition, id) == 1
                ? "quest.authored.id." + ScenarioAuthoringActionCodec.EncodeToken(id)
                : "quest.authored.index." + questIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryResolveStageEntity(ScenarioDefinition definition, string entityId, out int stageIndex)
        {
            stageIndex = -1;
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                if (string.Equals(StageEntityId(definition, i), entityId, StringComparison.Ordinal))
                {
                    stageIndex = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolveSceneEntity(ScenarioDefinition definition, string entityId, out int stageIndex, out int sceneIndex)
        {
            stageIndex = -1;
            sceneIndex = -1;
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                for (int j = 0; stage != null && stage.IntercomStages != null && j < stage.IntercomStages.Count; j++)
                {
                    if (string.Equals(SceneEntityId(definition, i, j), entityId, StringComparison.Ordinal))
                    {
                        stageIndex = i;
                        sceneIndex = j;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool TryGetSceneParentEntityId(string sceneEntityId, out string stageEntityId)
        {
            stageEntityId = null;
            if (string.IsNullOrEmpty(sceneEntityId) || !sceneEntityId.StartsWith("scene.", StringComparison.Ordinal))
                return false;
            int separator = sceneEntityId.IndexOf('.', "scene.".Length);
            if (separator <= "scene.".Length)
                return false;
            stageEntityId = ScenarioAuthoringActionCodec.DecodeToken(sceneEntityId.Substring("scene.".Length, separator - "scene.".Length));
            return !string.IsNullOrEmpty(stageEntityId);
        }

        public static bool TryResolveCharacterEntity(ScenarioDefinition definition, string entityId, out int characterIndex)
        {
            characterIndex = -1;
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                if (string.Equals(CharacterEntityId(definition, i), entityId, StringComparison.Ordinal))
                {
                    characterIndex = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolveConversationEntity(ScenarioDefinition definition, string entityId, out int conversationIndex)
        {
            conversationIndex = -1;
            ScenarioConversationAuthoringDefinition authored = definition != null ? definition.Conversations : null;
            for (int i = 0; authored != null && authored.Conversations != null && i < authored.Conversations.Count; i++)
            {
                if (string.Equals(ConversationEntityId(definition, i), entityId, StringComparison.Ordinal))
                {
                    conversationIndex = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolveQuestEntity(ScenarioDefinition definition, string entityId, out int questIndex)
        {
            questIndex = -1;
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
            {
                if (string.Equals(QuestEntityId(definition, i), entityId, StringComparison.Ordinal))
                {
                    questIndex = i;
                    return true;
                }
            }
            return false;
        }

        public static void SelectStageDocument(ScenarioDefinition definition, int stageIndex)
        {
            SelectDocument(FlowSubtabId, StageEntityId(definition, stageIndex));
        }

        public static void SelectSceneDocument(ScenarioDefinition definition, int stageIndex, int sceneIndex)
        {
            SelectDocument(FlowSubtabId, SceneEntityId(definition, stageIndex, sceneIndex));
        }

        public static void SelectCharacterDocument(ScenarioDefinition definition, int characterIndex)
        {
            SelectDocument(CharactersSubtabId, CharacterEntityId(definition, characterIndex));
        }

        public static void SelectConversationDocument(ScenarioDefinition definition, int conversationIndex)
        {
            SelectDocument(ConversationsSubtabId, ConversationEntityId(definition, conversationIndex));
        }

        public static void SelectQuestDocument(ScenarioDefinition definition, int questIndex)
        {
            SelectDocument(QuestPopupsSubtabId, QuestEntityId(definition, questIndex));
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

        private static void SelectDocument(string subtabId, string entityId)
        {
            ScenarioAuthoringRendererInteractionState rendererState = ScenarioAuthoringRendererInteractionState.Instance;
            rendererState.SetWorkspaceSubtab(WorkspaceId, subtabId);
            rendererState.SetWorkspaceSelection(WorkspaceId, subtabId, entityId);
            rendererState.SetWorkspaceNarrowPane(WorkspaceId, subtabId, true);
        }

        private static int CountStageIds(ScenarioFlowDefinition flow, string id)
        {
            int count = 0;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
                if (string.Equals(TrimToNull(flow.Stages[i] != null ? flow.Stages[i].Id : null), id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static int CountSceneIds(ScenarioFlowStageDefinition stage, string id)
        {
            int count = 0;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (string.Equals(TrimToNull(stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null), id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static int CountCharacterIds(ScenarioDefinition definition, string id)
        {
            int count = 0;
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
                if (string.Equals(TrimToNull(definition.ScenarioCharacters[i] != null ? definition.ScenarioCharacters[i].CharacterId : null), id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static int CountConversationIds(ScenarioConversationAuthoringDefinition authored, string id)
        {
            int count = 0;
            for (int i = 0; authored != null && authored.Conversations != null && i < authored.Conversations.Count; i++)
                if (string.Equals(TrimToNull(authored.Conversations[i] != null ? authored.Conversations[i].Id : null), id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static int CountQuestIds(ScenarioDefinition definition, string id)
        {
            int count = 0;
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
                if (string.Equals(TrimToNull(definition.Quests.Quests[i] != null ? definition.Quests.Quests[i].Id : null), id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string trimmed = value.Trim();
            return trimmed.Length > 0 ? trimmed : null;
        }
    }
}
