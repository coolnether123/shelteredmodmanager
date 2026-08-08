using System;
using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Domain.Validation{
    /// <summary>
    /// The kind of authored element a reference points at. Story/Timeline centric.
    /// Add a new kind here plus one collector block in <see cref="ScenarioReferenceIndex"/>
    /// to teach the whole editor about a new reference shape.
    /// </summary>
    internal enum ScenarioReferenceTargetKind
    {
        Stage = 0,
        IntercomStep = 1,
        StoryCharacter = 2,
        Milestone = 3
    }

    /// <summary>
    /// One place a scenario id is referenced. Carries plain-language labels, a navigation
    /// target compatible with the focused-editor "open stage" seam, and a writer that
    /// re-points this exact slot (used by safe rename and reference redirection).
    /// </summary>
    internal sealed class ScenarioReferenceUsage
    {
        private readonly Action<string> _setter;

        public ScenarioReferenceUsage(
            ScenarioReferenceTargetKind targetKind,
            string referencedId,
            int ownerStageIndex,
            int ownerIntercomIndex,
            string ownerLabel,
            string displayLabel,
            int navStageIndex,
            Action<string> setter)
        {
            TargetKind = targetKind;
            ReferencedId = referencedId;
            OwnerStageIndex = ownerStageIndex;
            OwnerIntercomIndex = ownerIntercomIndex;
            OwnerLabel = ownerLabel ?? string.Empty;
            DisplayLabel = displayLabel ?? string.Empty;
            NavStageIndex = navStageIndex;
            _setter = setter;
        }

        public ScenarioReferenceTargetKind TargetKind { get; private set; }

        /// <summary>The id value currently stored at this reference site.</summary>
        public string ReferencedId { get; private set; }

        /// <summary>Owning stage index (-1 when the reference is not stage-scoped).</summary>
        public int OwnerStageIndex { get; private set; }

        /// <summary>Owning intercom step index within the stage (-1 when not applicable).</summary>
        public int OwnerIntercomIndex { get; private set; }

        /// <summary>Plain-language path to the owning element, e.g. "Stage 'Radio Call 2' / step 'ask'".</summary>
        public string OwnerLabel { get; private set; }

        /// <summary>Plain-language description of the reference, e.g. "response option 3 route".</summary>
        public string DisplayLabel { get; private set; }

        /// <summary>Stage index to open in the focused editor to reveal this usage (-1 when none).</summary>
        public int NavStageIndex { get; private set; }

        public bool CanRedirect { get { return _setter != null; } }

        /// <summary>Re-point this reference to a new id (pass null to clear it).</summary>
        public void Redirect(string newId)
        {
            if (_setter != null)
                _setter(newId);
        }
    }

    /// <summary>
    /// Single-pass reference index over a <see cref="ScenarioDefinition"/>. It is the one
    /// authority for "where is this id used?" so Find Usages, safe rename, and delete guards
    /// all agree. Traversal mirrors <see cref="ShelteredAPI.Scenarios.Public.ShelteredScenarioAuthoring.AnalyzeStoryFlow"/>; the two
    /// walk the same story-flow graph shape.
    /// </summary>
    internal static class ScenarioReferenceIndex
    {
        /// <summary>Walk the definition once and collect every id reference site.</summary>
        public static List<ScenarioReferenceUsage> Collect(ScenarioDefinition definition)
        {
            List<ScenarioReferenceUsage> usages = new List<ScenarioReferenceUsage>();
            if (definition == null)
                return usages;

            CollectFlowReferences(definition, usages);
            CollectConversationReferences(definition, usages);
            CollectSelectionReferences(definition, usages);
            return usages;
        }

        public static List<ScenarioReferenceUsage> FindUsages(ScenarioDefinition definition, ScenarioReferenceTargetKind kind, string id)
        {
            return FindUsages(definition, kind, id, -1);
        }

        /// <summary>
        /// Usages of a specific id. <paramref name="ownerStageScope"/> restricts to a single
        /// owning stage (needed for intercom-step ids, which are only unique within their stage).
        /// </summary>
        public static List<ScenarioReferenceUsage> FindUsages(ScenarioDefinition definition, ScenarioReferenceTargetKind kind, string id, int ownerStageScope)
        {
            List<ScenarioReferenceUsage> matches = new List<ScenarioReferenceUsage>();
            string wanted = TrimToNull(id);
            if (wanted == null)
                return matches;

            List<ScenarioReferenceUsage> all = Collect(definition);
            for (int i = 0; i < all.Count; i++)
            {
                ScenarioReferenceUsage usage = all[i];
                if (usage.TargetKind != kind)
                    continue;
                if (ownerStageScope >= 0 && usage.OwnerStageIndex != ownerStageScope)
                    continue;
                if (string.Equals(TrimToNull(usage.ReferencedId), wanted, StringComparison.OrdinalIgnoreCase))
                    matches.Add(usage);
            }
            return matches;
        }

        public static int CountUsages(ScenarioDefinition definition, ScenarioReferenceTargetKind kind, string id)
        {
            return FindUsages(definition, kind, id, -1).Count;
        }

        public static int CountUsages(ScenarioDefinition definition, ScenarioReferenceTargetKind kind, string id, int ownerStageScope)
        {
            return FindUsages(definition, kind, id, ownerStageScope).Count;
        }

        /// <summary>
        /// Re-point every reference from <paramref name="oldId"/> to <paramref name="newId"/>.
        /// Does not touch the declaring element's own id (the caller owns that so the declaration
        /// and its references move together atomically). Returns the number of references updated.
        /// </summary>
        public static int RedirectReferences(ScenarioDefinition definition, ScenarioReferenceTargetKind kind, string oldId, string newId, int ownerStageScope)
        {
            List<ScenarioReferenceUsage> matches = FindUsages(definition, kind, oldId, ownerStageScope);
            for (int i = 0; i < matches.Count; i++)
                matches[i].Redirect(newId);
            return matches.Count;
        }

        /// <summary>Plain-language "Used in N places" summary.</summary>
        public static string Summarize(int count)
        {
            if (count <= 0)
                return "Not used yet";
            if (count == 1)
                return "Used in 1 place";
            return "Used in " + count.ToString(CultureInfo.InvariantCulture) + " places";
        }

        // === Collectors ===================================================================
        // Each block below registers one family of reference shapes. Adding a reference shape
        // is a new Emit(...) in the right block, never a new traversal.

        private static void CollectFlowReferences(ScenarioDefinition definition, List<ScenarioReferenceUsage> usages)
        {
            ScenarioFlowDefinition flow = definition.ScenarioFlow;
            if (flow == null || flow.Stages == null)
                return;

            for (int i = 0; i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                if (stage == null)
                    continue;

                int stageIndex = i;
                string stageLabel = StageLabel(stage, stageIndex);

                // Stage-target: unanswered-call route.
                ScenarioFlowStageDefinition unansweredOwner = stage;
                usages.Add(new ScenarioReferenceUsage(
                    ScenarioReferenceTargetKind.Stage,
                    stage.UnansweredNextStage,
                    stageIndex,
                    -1,
                    stageLabel,
                    "unanswered-call route",
                    stageIndex,
                    delegate(string v) { unansweredOwner.UnansweredNextStage = v; }));

                // Story-character-target: stage cast list.
                if (stage.CharacterIds != null)
                {
                    List<string> cast = stage.CharacterIds;
                    for (int c = 0; c < cast.Count; c++)
                    {
                        int castIndex = c;
                        usages.Add(new ScenarioReferenceUsage(
                            ScenarioReferenceTargetKind.StoryCharacter,
                            cast[castIndex],
                            stageIndex,
                            -1,
                            stageLabel,
                            "stage cast",
                            stageIndex,
                            delegate(string v) { cast[castIndex] = v; }));
                    }
                }

                if (stage.IntercomStages == null)
                    continue;

                for (int s = 0; s < stage.IntercomStages.Count; s++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[s];
                    if (intercom == null)
                        continue;

                    int intercomIndex = s;
                    string ownerLabel = stageLabel + " / " + IntercomLabel(intercom, intercomIndex);

                    // Intercom-step-target routes (scoped to this stage).
                    ScenarioIntercomStageDefinition nextOwner = intercom;
                    usages.Add(new ScenarioReferenceUsage(
                        ScenarioReferenceTargetKind.IntercomStep,
                        intercom.NextId,
                        stageIndex,
                        intercomIndex,
                        ownerLabel,
                        "next-step route",
                        stageIndex,
                        delegate(string v) { nextOwner.NextId = v; }));
                    usages.Add(new ScenarioReferenceUsage(
                        ScenarioReferenceTargetKind.IntercomStep,
                        intercom.AlternateNextId,
                        stageIndex,
                        intercomIndex,
                        ownerLabel,
                        "alternate-step route",
                        stageIndex,
                        delegate(string v) { nextOwner.AlternateNextId = v; }));

                    if (intercom.RandomizedNextIds != null)
                    {
                        List<string> random = intercom.RandomizedNextIds;
                        for (int r = 0; r < random.Count; r++)
                        {
                            int routeIndex = r;
                            usages.Add(new ScenarioReferenceUsage(
                                ScenarioReferenceTargetKind.IntercomStep,
                                random[routeIndex],
                                stageIndex,
                                intercomIndex,
                                ownerLabel,
                                "random route " + (routeIndex + 1).ToString(CultureInfo.InvariantCulture),
                                stageIndex,
                                delegate(string v) { random[routeIndex] = v; }));
                        }
                    }

                    if (intercom.Options != null)
                    {
                        for (int o = 0; o < intercom.Options.Count; o++)
                        {
                            ScenarioDialogueOptionDefinition option = intercom.Options[o];
                            if (option == null)
                                continue;
                            int optionIndex = o;
                            ScenarioDialogueOptionDefinition optionOwner = option;
                            usages.Add(new ScenarioReferenceUsage(
                                ScenarioReferenceTargetKind.IntercomStep,
                                option.NextId,
                                stageIndex,
                                intercomIndex,
                                ownerLabel,
                                "response option " + (optionIndex + 1).ToString(CultureInfo.InvariantCulture) + " route",
                                stageIndex,
                                delegate(string v) { optionOwner.NextId = v; }));
                        }
                    }

                    // Stage-target: delayed stage change.
                    if (intercom.StageChange != null)
                    {
                        ScenarioStageChangeDefinition change = intercom.StageChange;
                        usages.Add(new ScenarioReferenceUsage(
                            ScenarioReferenceTargetKind.Stage,
                            change.Id,
                            stageIndex,
                            intercomIndex,
                            ownerLabel,
                            "next-stage change",
                            stageIndex,
                            delegate(string v) { change.Id = v; }));
                    }

                    // Story-character-target: dialogue speakers.
                    if (intercom.Dialogue != null)
                    {
                        for (int d = 0; d < intercom.Dialogue.Count; d++)
                        {
                            ScenarioDialogueLineDefinition line = intercom.Dialogue[d];
                            if (line == null)
                                continue;
                            int lineIndex = d;
                            ScenarioDialogueLineDefinition lineOwner = line;
                            usages.Add(new ScenarioReferenceUsage(
                                ScenarioReferenceTargetKind.StoryCharacter,
                                line.Character,
                                stageIndex,
                                intercomIndex,
                                ownerLabel,
                                "dialogue line " + (lineIndex + 1).ToString(CultureInfo.InvariantCulture) + " speaker",
                                stageIndex,
                                delegate(string v) { lineOwner.Character = v; }));
                        }
                    }

                    // Story-character-target: recruit list.
                    if (intercom.CharacterIdsToRecruit != null)
                    {
                        List<string> recruit = intercom.CharacterIdsToRecruit;
                        for (int c = 0; c < recruit.Count; c++)
                        {
                            int recruitIndex = c;
                            usages.Add(new ScenarioReferenceUsage(
                                ScenarioReferenceTargetKind.StoryCharacter,
                                recruit[recruitIndex],
                                stageIndex,
                                intercomIndex,
                                ownerLabel,
                                "recruit list",
                                stageIndex,
                                delegate(string v) { recruit[recruitIndex] = v; }));
                        }
                    }

                    // Milestone-target: checks and combat outcome milestones.
                    if (intercom.CheckMilestones != null)
                    {
                        for (int m = 0; m < intercom.CheckMilestones.Count; m++)
                        {
                            ScenarioMilestoneCheckDefinition check = intercom.CheckMilestones[m];
                            if (check == null)
                                continue;
                            ScenarioMilestoneCheckDefinition checkOwner = check;
                            usages.Add(new ScenarioReferenceUsage(
                                ScenarioReferenceTargetKind.Milestone,
                                check.Name,
                                stageIndex,
                                intercomIndex,
                                ownerLabel,
                                "milestone check",
                                stageIndex,
                                delegate(string v) { checkOwner.Name = v; }));
                        }
                    }

                    if (intercom.EndOptions != null)
                    {
                        ScenarioEncounterEndOptionsDefinition end = intercom.EndOptions;
                        usages.Add(new ScenarioReferenceUsage(
                            ScenarioReferenceTargetKind.Milestone,
                            end.CombatWinMilestone,
                            stageIndex,
                            intercomIndex,
                            ownerLabel,
                            "combat-win milestone",
                            stageIndex,
                            delegate(string v) { end.CombatWinMilestone = v; }));
                        usages.Add(new ScenarioReferenceUsage(
                            ScenarioReferenceTargetKind.Milestone,
                            end.CombatLossMilestone,
                            stageIndex,
                            intercomIndex,
                            ownerLabel,
                            "combat-loss milestone",
                            stageIndex,
                            delegate(string v) { end.CombatLossMilestone = v; }));
                    }
                }
            }
        }

        private static void CollectConversationReferences(ScenarioDefinition definition, List<ScenarioReferenceUsage> usages)
        {
            ScenarioConversationAuthoringDefinition conversations = definition.Conversations;
            if (conversations == null || conversations.Conversations == null)
                return;

            for (int i = 0; i < conversations.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = conversations.Conversations[i];
                if (conversation == null || conversation.Participants == null)
                    continue;

                string conversationLabel = "Conversation '" + (TrimToNull(conversation.Id) ?? ("#" + (i + 1).ToString(CultureInfo.InvariantCulture))) + "'";
                for (int p = 0; p < conversation.Participants.Count; p++)
                {
                    ScenarioConversationParticipantDefinition participant = conversation.Participants[p];
                    if (participant == null)
                        continue;

                    ScenarioConversationParticipantDefinition owner = participant;
                    string slot = TrimToNull(participant.Slot) ?? ("#" + (p + 1).ToString(CultureInfo.InvariantCulture));
                    usages.Add(new ScenarioReferenceUsage(
                        ScenarioReferenceTargetKind.StoryCharacter,
                        participant.StoryCharacterId,
                        -1,
                        -1,
                        conversationLabel,
                        "participant slot " + slot,
                        -1,
                        delegate(string v) { owner.StoryCharacterId = v; }));
                }
            }
        }

        private static void CollectSelectionReferences(ScenarioDefinition definition, List<ScenarioReferenceUsage> usages)
        {
            ScenarioSelectionRulesDefinition rules = definition.SelectionRules;
            if (rules == null || rules.PrerequisiteMilestones == null)
                return;

            List<string> prerequisites = rules.PrerequisiteMilestones;
            for (int i = 0; i < prerequisites.Count; i++)
            {
                int prereqIndex = i;
                usages.Add(new ScenarioReferenceUsage(
                    ScenarioReferenceTargetKind.Milestone,
                    prerequisites[prereqIndex],
                    -1,
                    -1,
                    "Scenario selection rules",
                    "prerequisite milestone",
                    -1,
                    delegate(string v) { prerequisites[prereqIndex] = v; }));
            }
        }

        // === Labels =======================================================================

        private static string StageLabel(ScenarioFlowStageDefinition stage, int stageIndex)
        {
            string id = TrimToNull(stage != null ? stage.Id : null);
            return "Stage '" + (id ?? ("#" + (stageIndex + 1).ToString(CultureInfo.InvariantCulture))) + "'";
        }

        private static string IntercomLabel(ScenarioIntercomStageDefinition intercom, int intercomIndex)
        {
            string id = TrimToNull(intercom != null ? intercom.Id : null);
            return "step '" + (id ?? ("#" + (intercomIndex + 1).ToString(CultureInfo.InvariantCulture))) + "'";
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
