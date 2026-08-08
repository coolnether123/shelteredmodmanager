using System;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal static class ScenarioStoryFocusedEditorActions
    {
        public const string WorkspaceId = "story";
        public const string FlowSubtabId = "flow";
        public const string CharactersSubtabId = "characters";
        public const string ConversationsSubtabId = "conversations";
        public const string QuestPopupsSubtabId = "quest-popups";
        public const string FocusedEntryPrefix = "story_stage:";

        public static string StageEntityId(ScenarioDefinition definition, int stageIndex)
        {
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            ScenarioFlowStageDefinition stage = flow != null && flow.Stages != null && stageIndex >= 0 && stageIndex < flow.Stages.Count
                ? flow.Stages[stageIndex]
                : null;
            string id = TrimToNull(stage != null ? stage.Id : null);
            return id != null && CountStageIds(flow, id) == 1
                ? "stage.id." + ScenarioAutomationIdCodec.EncodeToken(id)
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
                ? "id." + ScenarioAutomationIdCodec.EncodeToken(id)
                : "index." + sceneIndex.ToString(CultureInfo.InvariantCulture);
            return "scene." + ScenarioAutomationIdCodec.EncodeToken(StageEntityId(definition, stageIndex)) + "." + sceneToken;
        }

        public static string CharacterEntityId(ScenarioDefinition definition, int characterIndex)
        {
            ScenarioNpcDefinition character = definition != null && definition.ScenarioCharacters != null && characterIndex >= 0 && characterIndex < definition.ScenarioCharacters.Count
                ? definition.ScenarioCharacters[characterIndex]
                : null;
            string id = TrimToNull(character != null ? character.CharacterId : null);
            return id != null && CountCharacterIds(definition, id) == 1
                ? "character.id." + ScenarioAutomationIdCodec.EncodeToken(id)
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
                ? "conversation.id." + ScenarioAutomationIdCodec.EncodeToken(id)
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
                ? "quest.authored.id." + ScenarioAutomationIdCodec.EncodeToken(id)
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
            stageEntityId = ScenarioAutomationIdCodec.DecodeToken(sceneEntityId.Substring("scene.".Length, separator - "scene.".Length));
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

        public static void SelectStageDocument(ScenarioDefinition definition, int stageIndex, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(FlowSubtabId, StageEntityId(definition, stageIndex), rendererInteraction);
        }

        public static void SelectSceneDocument(ScenarioDefinition definition, int stageIndex, int sceneIndex, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(FlowSubtabId, SceneEntityId(definition, stageIndex, sceneIndex), rendererInteraction);
        }

        public static void SelectCharacterDocument(ScenarioDefinition definition, int characterIndex, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(CharactersSubtabId, CharacterEntityId(definition, characterIndex), rendererInteraction);
        }

        public static void SelectConversationDocument(ScenarioDefinition definition, int conversationIndex, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(ConversationsSubtabId, ConversationEntityId(definition, conversationIndex), rendererInteraction);
        }

        public static void SelectQuestDocument(ScenarioDefinition definition, int questIndex, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(QuestPopupsSubtabId, QuestEntityId(definition, questIndex), rendererInteraction);
        }

        public static string FocusedEntryId(int stageIndex)
        {
            return FocusedEntryPrefix + stageIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static void SelectDocument(string subtabId, string entityId, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            rendererInteraction.SetWorkspaceSubtab(WorkspaceId, subtabId);
            rendererInteraction.SetWorkspaceSelection(WorkspaceId, subtabId, entityId);
            rendererInteraction.SetWorkspaceNarrowPane(WorkspaceId, subtabId, true);
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
