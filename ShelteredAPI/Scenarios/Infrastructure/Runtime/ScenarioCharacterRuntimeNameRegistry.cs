using System;
using System.Collections.Generic;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    internal static class ScenarioCharacterRuntimeNameRegistry
    {
        private static readonly Dictionary<string, Dictionary<string, string>> NamesByScenario =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static void Register(ScenarioDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return;

            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character == null || string.IsNullOrEmpty(character.CharacterId) || string.IsNullOrEmpty(character.DisplayName))
                    continue;
                names[character.CharacterId] = character.DisplayName;
            }
            NamesByScenario[definition.Id] = names;
        }

        public static int ApplyToPendingStage(
            int instanceId,
            IList<QuestManager.QuestCharacterInfo> visitorCharacterInfo)
        {
            QuestManager manager = QuestManager.instance;
            QuestInstance instance = manager != null ? manager.GetQuestInstance(instanceId) : null;
            string scenarioId = instance != null && instance.definition != null ? instance.definition.id : null;
            Dictionary<string, string> names;
            if (instance == null || string.IsNullOrEmpty(scenarioId) || !NamesByScenario.TryGetValue(scenarioId, out names))
                return -1;
            if (instance.stage == null)
                return 0;

            int visitorIndex = 0;
            int appliedCount = 0;
            for (int i = 0; i < instance.stage.characterIds.Count; i++)
            {
                string characterId = instance.stage.characterIds[i];
                QuestManager.QuestCharacterInfo info = instance.GetCharacterInfo(characterId);
                if (info == null)
                    continue;

                QuestManager.QuestCharacterInfo visitorInfo = visitorCharacterInfo != null
                    && visitorIndex < visitorCharacterInfo.Count
                        ? visitorCharacterInfo[visitorIndex]
                        : info;
                visitorIndex++;

                string displayName;
                if (string.IsNullOrEmpty(characterId) || !names.TryGetValue(characterId, out displayName))
                    continue;

                if (ApplyDisplayName(info, displayName))
                    appliedCount++;
                if (!object.ReferenceEquals(visitorInfo, info) && ApplyDisplayName(visitorInfo, displayName))
                    appliedCount++;
            }

            return appliedCount;
        }

        private static bool ApplyDisplayName(QuestManager.QuestCharacterInfo info, string displayName)
        {
            if (info == null || info.m_preset == null)
                return false;

            info.m_preset.m_firstName = displayName;
            info.m_preset.m_lastName = string.Empty;
            info.m_preset.m_randomizeName = false;
            return true;
        }
    }
}
