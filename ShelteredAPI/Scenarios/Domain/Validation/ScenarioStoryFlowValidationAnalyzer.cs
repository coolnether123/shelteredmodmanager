using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.Validation{
    internal sealed class ScenarioStoryFlowValidationAnalyzer
    {
        private static readonly string[] KnownNpcSlots = { "LeadNpc", "Npc2", "Npc3", "Npc4", "BackgroundNpc", "Player" };
        private static readonly string[] KnownIntercomTypes = { "Choice", "CheckItems", "CheckSubquestCompletion", "CheckMilestone", "Randomizer", "EndEncounter", "EnterCode" };
        private static readonly string[] KnownEndTypes = { "NothingHappens", "RewardItems", "EnterTrade", "EnterRecruit", "Combat", "AddVehicle", "CompleteQuest" };

        public ScenarioStoryFlowIssue[] Analyze(ScenarioDefinition definition)
        {
            List<ScenarioStoryFlowIssue> issues = new List<ScenarioStoryFlowIssue>();
            if (definition == null)
            {
                Add(issues, ScenarioIssueSeverity.Error, "story.definition.null", "Story validation could not run because the scenario definition is missing.", -1, null, -1);
                return issues.ToArray();
            }

            ValidateQuestLibraryCollision(definition, issues);

            ScenarioFlowDefinition flow = definition.ScenarioFlow;
            if (flow == null || flow.Stages == null || flow.Stages.Count == 0)
            {
                Add(issues, ScenarioIssueSeverity.Warning, "story.flow.no_starting_stage", "Story has no starting stage. Add a stage to start the scenario flow.", -1, null, -1);
                return issues.ToArray();
            }

            HashSet<string> stageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> characterIds = BuildScenarioCharacterIds(definition);
            for (int i = 0; i < flow.Stages.Count; i++)
                ValidateStageIdentity(flow.Stages[i], i, stageIds, characterIds, issues);

            if (flow.Stages[0] == null || TrimToNull(flow.Stages[0].Id) == null)
                Add(issues, ScenarioIssueSeverity.Error, "story.flow.no_starting_stage", "Story first stage needs an id because vanilla starts from the first authored stage.", 0, null, -1);

            for (int i = 0; i < flow.Stages.Count; i++)
                ValidateStageRoutes(flow, flow.Stages[i], i, stageIds, characterIds, issues);

            ValidateReachability(flow, stageIds, issues);
            return issues.ToArray();
        }

        private static void ValidateQuestLibraryCollision(ScenarioDefinition definition, List<ScenarioStoryFlowIssue> issues)
        {
            string id = TrimToNull(definition != null ? definition.Id : null);
            if (id == null || QuestLibrary.instance == null)
                return;

            try
            {
                if (QuestLibrary.instance.FindQuestDefinition(id) != null)
                    Add(issues, ScenarioIssueSeverity.Warning, "story.quest_library.id_collision", "Scenario id '" + id + "' is already present in QuestLibrary; vanilla keeps only one quest/scenario per id.", -1, null, -1);
            }
            catch
            {
            }
        }

        private static void ValidateStageIdentity(
            ScenarioFlowStageDefinition stage,
            int stageIndex,
            HashSet<string> stageIds,
            HashSet<string> characterIds,
            List<ScenarioStoryFlowIssue> issues)
        {
            if (stage == null)
            {
                Add(issues, ScenarioIssueSeverity.Error, "story.flow.stage_null", "Story stage #" + (stageIndex + 1).ToString(CultureInfo.InvariantCulture) + " is empty.", stageIndex, null, -1);
                return;
            }

            string id = TrimToNull(stage.Id);
            if (id == null)
                Add(issues, ScenarioIssueSeverity.Error, "story.flow.stage_id_required", "Story stage #" + (stageIndex + 1).ToString(CultureInfo.InvariantCulture) + " needs an internal id.", stageIndex, null, -1);
            else if (!stageIds.Add(id))
                Add(issues, ScenarioIssueSeverity.Error, "story.flow.duplicate_stage", "Story stage id is duplicated: " + id + ".", stageIndex, id, -1);

            if (stage.UnansweredNextDays < 0)
                Add(issues, ScenarioIssueSeverity.Error, "story.flow.invalid_unanswered_delay", "Stage '" + Label(stage, stageIndex) + "' has a negative ignored-call delay.", stageIndex, id, -1);

            for (int c = 0; stage.CharacterIds != null && c < stage.CharacterIds.Count; c++)
            {
                string characterId = TrimToNull(stage.CharacterIds[c]);
                if (characterId == null)
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.character_required", "Stage '" + Label(stage, stageIndex) + "' includes an empty character reference.", stageIndex, id, -1);
                else if (!characterIds.Contains(characterId))
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.unknown_character", "Stage '" + Label(stage, stageIndex) + "' references unknown character '" + characterId + "'.", stageIndex, id, -1);
            }
        }

        private static void ValidateStageRoutes(
            ScenarioFlowDefinition flow,
            ScenarioFlowStageDefinition stage,
            int stageIndex,
            HashSet<string> stageIds,
            HashSet<string> characterIds,
            List<ScenarioStoryFlowIssue> issues)
        {
            if (stage == null)
                return;

            string stageId = TrimToNull(stage.Id);
            if (TrimToNull(stage.UnansweredNextStage) != null && !stageIds.Contains(stage.UnansweredNextStage))
                Add(issues, ScenarioIssueSeverity.Error, "story.flow.missing_unanswered_stage", "Stage '" + Label(stage, stageIndex) + "' routes ignored calls to missing stage '" + stage.UnansweredNextStage + "'.", stageIndex, stageId, -1);

            if (flow != null && flow.Stages != null && flow.Stages.Count > 1 && !HasOutgoingStageRoute(stage) && !CompletesScenario(stage))
                Add(issues, ScenarioIssueSeverity.Warning, "story.flow.missing_next_stage", "Stage '" + Label(stage, stageIndex) + "' has no outgoing next-stage route or scenario completion outcome.", stageIndex, stageId, -1);

            if (TrimToNull(stage.UnansweredNextStage) == null)
                Add(issues, ScenarioIssueSeverity.Warning, "story.flow.unanswered_self_loop", "Stage '" + Label(stage, stageIndex) + "' has no ignored-call route; vanilla keeps the player in this stage unless punishment applies.", stageIndex, stageId, -1);

            ValidateIntercomStages(stage, stageIndex, stageIds, characterIds, issues);
        }

        private static void ValidateIntercomStages(
            ScenarioFlowStageDefinition stage,
            int stageIndex,
            HashSet<string> stageIds,
            HashSet<string> characterIds,
            List<ScenarioStoryFlowIssue> issues)
        {
            if (stage.IntercomStages == null || stage.IntercomStages.Count == 0)
            {
                Add(issues, ScenarioIssueSeverity.Warning, "story.flow.no_intercom_steps", "Stage '" + Label(stage, stageIndex) + "' has no encounter steps.", stageIndex, stage != null ? stage.Id : null, -1);
                return;
            }

            HashSet<string> intercomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                string id = TrimToNull(intercom != null ? intercom.Id : null);
                if (id == null)
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.intercom_id_required", "Stage '" + Label(stage, stageIndex) + "' encounter step #" + (i + 1).ToString(CultureInfo.InvariantCulture) + " needs an id.", stageIndex, stage.Id, i);
                else if (!intercomIds.Add(id))
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.duplicate_intercom", "Stage '" + Label(stage, stageIndex) + "' has duplicate encounter step id '" + id + "'.", stageIndex, stage.Id, i);
            }

            for (int i = 0; i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                if (intercom == null)
                    continue;

                string owner = Label(stage, stageIndex) + "/" + (TrimToNull(intercom.Id) ?? ("step #" + (i + 1).ToString(CultureInfo.InvariantCulture)));
                if (!IsKnownType(intercom.Type, KnownIntercomTypes))
                    Add(issues, ScenarioIssueSeverity.Warning, "story.flow.intercom_type_unknown", "Encounter step '" + owner + "' uses non-vanilla type '" + (intercom.Type ?? string.Empty) + "' and may fall back at runtime.", stageIndex, stage.Id, i);

                ValidateIntercomTarget(issues, intercomIds, intercom.NextId, owner, "next route", stageIndex, stage.Id, i);
                ValidateIntercomTarget(issues, intercomIds, intercom.AlternateNextId, owner, "alternate route", stageIndex, stage.Id, i);
                for (int r = 0; intercom.RandomizedNextIds != null && r < intercom.RandomizedNextIds.Count; r++)
                    ValidateIntercomTarget(issues, intercomIds, intercom.RandomizedNextIds[r], owner, "random route", stageIndex, stage.Id, i);

                ValidateDialogue(stage, stageIndex, intercom, i, intercomIds, characterIds, issues, owner);
                if (intercom.StageChange != null && TrimToNull(intercom.StageChange.Id) != null && !stageIds.Contains(intercom.StageChange.Id))
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.missing_stage_change", "Encounter step '" + owner + "' changes to missing stage '" + intercom.StageChange.Id + "'.", stageIndex, stage.Id, i);
                if (intercom.StageChange != null && intercom.StageChange.DelayDays < 0)
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.invalid_stage_change_delay", "Encounter step '" + owner + "' has a negative next-stage delay.", stageIndex, stage.Id, i);

                for (int c = 0; intercom.CharacterIdsToRecruit != null && c < intercom.CharacterIdsToRecruit.Count; c++)
                {
                    string characterId = TrimToNull(intercom.CharacterIdsToRecruit[c]);
                    if (characterId != null && !characterIds.Contains(characterId))
                        Add(issues, ScenarioIssueSeverity.Error, "story.flow.unknown_recruit", "Encounter step '" + owner + "' recruits unknown character '" + characterId + "'.", stageIndex, stage.Id, i);
                }

                ValidateItemEntries(issues, intercom.Items, "required/check item", owner, stageIndex, stage.Id, i);
                ValidateItemEntries(issues, intercom.ItemsToRemove, "swap/remove item", owner, stageIndex, stage.Id, i);
                if (intercom.EndOptions != null)
                {
                    if (!IsKnownType(intercom.EndOptions.Type, KnownEndTypes))
                        Add(issues, ScenarioIssueSeverity.Warning, "story.flow.end_type_unknown", "Encounter step '" + owner + "' uses non-vanilla end type '" + (intercom.EndOptions.Type ?? string.Empty) + "' and may fall back at runtime.", stageIndex, stage.Id, i);
                    ValidateItemEntries(issues, intercom.EndOptions.RewardItems, "end reward item", owner, stageIndex, stage.Id, i);
                    ValidateItemEntries(issues, intercom.EndOptions.TradeItems, "trade item", owner, stageIndex, stage.Id, i);
                }
            }
        }

        private static void ValidateDialogue(
            ScenarioFlowStageDefinition stage,
            int stageIndex,
            ScenarioIntercomStageDefinition intercom,
            int intercomIndex,
            HashSet<string> intercomIds,
            HashSet<string> characterIds,
            List<ScenarioStoryFlowIssue> issues,
            string owner)
        {
            for (int o = 0; intercom.Options != null && o < intercom.Options.Count; o++)
            {
                ScenarioDialogueOptionDefinition option = intercom.Options[o];
                if (TrimToNull(option != null ? option.TextKey : null) == null)
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.option_key_required", "Option #" + (o + 1).ToString(CultureInfo.InvariantCulture) + " in '" + owner + "' needs dialogue text.", stageIndex, stage.Id, intercomIndex);
                ValidateIntercomTarget(issues, intercomIds, option != null ? option.NextId : null, owner, "option route", stageIndex, stage.Id, intercomIndex);
            }

            for (int d = 0; intercom.Dialogue != null && d < intercom.Dialogue.Count; d++)
            {
                ScenarioDialogueLineDefinition line = intercom.Dialogue[d];
                if (TrimToNull(line != null ? line.TextKey : null) == null)
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.dialogue_key_required", "Dialogue line #" + (d + 1).ToString(CultureInfo.InvariantCulture) + " in '" + owner + "' needs text.", stageIndex, stage.Id, intercomIndex);
                string speaker = TrimToNull(line != null ? line.Character : null);
                if (speaker != null && !characterIds.Contains(speaker))
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.unknown_dialogue_speaker", "Dialogue line #" + (d + 1).ToString(CultureInfo.InvariantCulture) + " in '" + owner + "' uses unknown speaker '" + speaker + "'.", stageIndex, stage.Id, intercomIndex);
            }
        }

        private static void ValidateIntercomTarget(List<ScenarioStoryFlowIssue> issues, HashSet<string> intercomIds, string value, string owner, string field, int stageIndex, string stageId, int intercomIndex)
        {
            string id = TrimToNull(value);
            if (id != null && !intercomIds.Contains(id))
                Add(issues, ScenarioIssueSeverity.Error, "story.flow.missing_intercom_target", "Encounter step '" + owner + "' " + field + " points to missing step '" + id + "'.", stageIndex, stageId, intercomIndex);
        }

        private static void ValidateItemEntries(List<ScenarioStoryFlowIssue> issues, List<ItemEntry> items, string label, string owner, int stageIndex, string stageId, int intercomIndex)
        {
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ItemEntry item = items[i];
                string itemId = TrimToNull(item != null ? item.ItemId : null);
                if (itemId == null)
                {
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.item_id_required", "Story " + label + " #" + (i + 1).ToString(CultureInfo.InvariantCulture) + " in '" + owner + "' needs an item.", stageIndex, stageId, intercomIndex);
                    continue;
                }

                if (item.Quantity <= 0)
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.item_quantity_invalid", "Story " + label + " '" + itemId + "' in '" + owner + "' needs quantity greater than zero.", stageIndex, stageId, intercomIndex);

                ItemManager.ItemType itemType;
                if (!ContentInjector.ResolveItemType(itemId, out itemType))
                    Add(issues, ScenarioIssueSeverity.Error, "story.flow.item_unknown", "Story " + label + " '" + itemId + "' in '" + owner + "' is not a known item id.", stageIndex, stageId, intercomIndex);
            }
        }

        private static void ValidateReachability(ScenarioFlowDefinition flow, HashSet<string> stageIds, List<ScenarioStoryFlowIssue> issues)
        {
            if (flow == null || flow.Stages == null || flow.Stages.Count <= 1)
                return;

            string startId = TrimToNull(flow.Stages[0] != null ? flow.Stages[0].Id : null);
            if (startId == null)
                return;

            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Queue<string> queue = new Queue<string>();
            visited.Add(startId);
            queue.Enqueue(startId);
            while (queue.Count > 0)
            {
                ScenarioFlowStageDefinition stage = FindStage(flow, queue.Dequeue());
                AddReachable(stage != null ? stage.UnansweredNextStage : null, stageIds, visited, queue);
                for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                    AddReachable(stage.IntercomStages[i] != null && stage.IntercomStages[i].StageChange != null ? stage.IntercomStages[i].StageChange.Id : null, stageIds, visited, queue);
            }

            for (int i = 0; i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                string id = TrimToNull(stage != null ? stage.Id : null);
                if (id != null && !visited.Contains(id))
                    Add(issues, ScenarioIssueSeverity.Warning, "story.flow.unreachable_stage", "Stage '" + Label(stage, i) + "' is unreachable from the first authored stage.", i, id, -1);
            }
        }

        private static void AddReachable(string id, HashSet<string> stageIds, HashSet<string> visited, Queue<string> queue)
        {
            string stageId = TrimToNull(id);
            if (stageId == null || !stageIds.Contains(stageId) || visited.Contains(stageId))
                return;
            visited.Add(stageId);
            queue.Enqueue(stageId);
        }

        private static ScenarioFlowStageDefinition FindStage(ScenarioFlowDefinition flow, string id)
        {
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
                if (flow.Stages[i] != null && string.Equals(flow.Stages[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return flow.Stages[i];
            return null;
        }

        private static bool HasOutgoingStageRoute(ScenarioFlowStageDefinition stage)
        {
            if (stage == null)
                return false;
            if (TrimToNull(stage.UnansweredNextStage) != null)
                return true;
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                if (intercom != null && intercom.StageChange != null && TrimToNull(intercom.StageChange.Id) != null)
                    return true;
            }
            return false;
        }

        private static bool CompletesScenario(ScenarioFlowStageDefinition stage)
        {
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioEncounterEndOptionsDefinition end = stage.IntercomStages[i] != null ? stage.IntercomStages[i].EndOptions : null;
                if (end != null && (end.CompleteQuest || end.CompleteParentScenario || string.Equals(end.Type, "CompleteQuest", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private static HashSet<string> BuildScenarioCharacterIds(ScenarioDefinition definition)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < KnownNpcSlots.Length; i++)
                ids.Add(KnownNpcSlots[i]);
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                string id = TrimToNull(definition.ScenarioCharacters[i] != null ? definition.ScenarioCharacters[i].CharacterId : null);
                if (id != null)
                    ids.Add(id);
            }
            return ids;
        }

        private static bool IsKnownType(string value, string[] known)
        {
            string normalized = TrimToNull(value);
            if (normalized == null)
                return true;
            for (int i = 0; known != null && i < known.Length; i++)
                if (string.Equals(normalized, known[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string Label(ScenarioFlowStageDefinition stage, int stageIndex)
        {
            return stage != null && TrimToNull(stage.Id) != null
                ? stage.Id
                : "stage #" + (stageIndex + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static void Add(List<ScenarioStoryFlowIssue> issues, ScenarioIssueSeverity severity, string code, string message, int stageIndex, string stageId, int intercomIndex)
        {
            issues.Add(new ScenarioStoryFlowIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                StageIndex = stageIndex,
                StageId = stageId,
                IntercomIndex = intercomIndex
            });
        }
    }

    internal sealed class ScenarioStoryFlowIssue
    {
        public ScenarioIssueSeverity Severity { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public int StageIndex { get; set; }
        public string StageId { get; set; }
        public int IntercomIndex { get; set; }
    }
}
