using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class VanillaScenarioRuntimeGateway : IVanillaScenarioRuntime
    {
        private static readonly MethodInfo AddRemoveQuestInstancesMethod = typeof(QuestManager).GetMethod(
            "AddRemoveQuestInstances_Recursive",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PendingStagesField = typeof(QuestManager).GetField(
            "m_pendingStages",
            BindingFlags.Instance | BindingFlags.NonPublic);
        public bool IsWorldReady(out string blockingReason)
        {
            return ScenarioWorldReady.Evaluate(out blockingReason);
        }

        public bool TrySpawnScenario(ScenarioDef definition, out QuestInstance instance, out string reason)
        {
            instance = null;
            reason = null;

            if (definition == null)
            {
                reason = "ScenarioDef is null.";
                return false;
            }

            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            try
            {
                instance = QuestManager.instance.SpawnQuestOrScenario(definition);
            }
            catch (Exception ex)
            {
                reason = "QuestManager failed while spawning scenario '" + (definition.id ?? string.Empty) + "': " + ex.Message;
                return false;
            }

            if (instance == null)
            {
                reason = "QuestManager rejected scenario '" + (definition.id ?? string.Empty) + "'.";
                return false;
            }

            return true;
        }

        public bool TryStartQuest(string questId, out string reason)
        {
            reason = null;
            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            if (string.IsNullOrEmpty(questId))
            {
                reason = "Quest id is missing.";
                return false;
            }

            try
            {
                if (QuestManager.instance.SpawnQuestWithId(questId))
                    return true;

                reason = "QuestManager rejected quest '" + questId + "'.";
                return false;
            }
            catch (Exception ex)
            {
                reason = "QuestManager failed while starting quest '" + questId + "': " + ex.Message;
                return false;
            }
        }

        public bool TryGetQuestInstance(int instanceId, out QuestInstance instance, out string reason)
        {
            instance = null;
            reason = null;

            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            instance = QuestManager.instance.GetQuestInstance(instanceId);
            if (instance == null)
            {
                reason = "QuestInstance was not found: " + instanceId.ToString();
                return false;
            }

            return true;
        }

        public List<QuestInstance> GetCurrentQuests()
        {
            if (QuestManager.instance == null)
                return new List<QuestInstance>();

            List<QuestInstance> quests = QuestManager.instance.GetCurrentQuests(true, true, true);
            return quests ?? new List<QuestInstance>();
        }

        public bool TryFinishQuest(QuestInstance instance, bool success, out string reason)
        {
            reason = null;

            if (instance == null)
            {
                reason = "QuestInstance is null.";
                return false;
            }

            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            try
            {
                QuestManager.instance.FinishQuest(instance.id, success);
                return true;
            }
            catch (Exception ex)
            {
                reason = "QuestManager failed while finishing QuestInstance " + instance.id.ToString() + ": " + ex.Message;
                return false;
            }
        }

        public bool TryAbandonQuest(QuestInstance instance, out string reason)
        {
            reason = null;
            if (instance == null)
            {
                reason = "QuestInstance is null.";
                return false;
            }
            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }
            if (AddRemoveQuestInstancesMethod == null)
            {
                reason = "QuestManager removal seam was not found.";
                return false;
            }

            try
            {
                instance.CleanUp();
                AddRemoveQuestInstancesMethod.Invoke(
                    QuestManager.instance,
                    new object[] { instance, false });
                RemovePendingStagesRecursive(instance);
                return true;
            }
            catch (Exception ex)
            {
                reason = "QuestManager failed while abandoning QuestInstance "
                    + instance.id.ToString() + ": " + ex.Message;
                return false;
            }
        }

        private static void RemovePendingStagesRecursive(QuestInstance instance)
        {
            if (instance == null)
                return;
            RemovePendingStage(instance.id);
            for (int i = 0; instance.subquests != null && i < instance.subquests.Count; i++)
                RemovePendingStagesRecursive(instance.subquests[i]);
        }

        private static void RemovePendingStage(int instanceId)
        {
            IList pending = PendingStagesField != null && QuestManager.instance != null
                ? PendingStagesField.GetValue(QuestManager.instance) as IList
                : null;
            for (int i = pending != null ? pending.Count - 1 : -1; i >= 0; i--)
            {
                object entry = pending[i];
                FieldInfo instanceIdField = entry != null
                    ? entry.GetType().GetField("instanceId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    : null;
                object value = instanceIdField != null ? instanceIdField.GetValue(entry) : null;
                if (value is int && (int)value == instanceId)
                    pending.RemoveAt(i);
            }
        }
    }
}
