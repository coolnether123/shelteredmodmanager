using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    /// <summary>
    /// Kind of a global-search result. Ordering here doubles as the default group
    /// order used when the query is empty (commands first, help last).
    /// </summary>
    internal enum ScenarioGlobalSearchKind
    {
        Command = 0,
        WorkspaceSubtab = 1,
        NavigatorEntity = 2,
        StoryStage = 3,
        IntercomStep = 4,
        Character = 5,
        Conversation = 6,
        TimelineEntry = 7,
        CastMember = 8,
        MapLocation = 9,
        Version = 10,
        Help = 11
    }

    /// <summary>
    /// One ranked, activatable entry in the creator command palette. Activation runs
    /// <see cref="ActionIds"/> in order through the existing action dispatch, so a story
    /// element can first select its stage tab and then open the focused editor for it.
    /// </summary>
    internal sealed class ScenarioGlobalSearchEntry
    {
        public ScenarioGlobalSearchEntry(
            ScenarioGlobalSearchKind kind,
            string kindLabel,
            string name,
            string context,
            bool enabled,
            string[] actionIds)
        {
            Kind = kind;
            KindLabel = kindLabel ?? string.Empty;
            Name = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
            Context = context ?? string.Empty;
            Enabled = enabled;
            ActionIds = actionIds ?? new string[0];
        }

        public ScenarioGlobalSearchKind Kind { get; private set; }
        public string KindLabel { get; private set; }
        public string Name { get; private set; }
        public string Context { get; private set; }
        public bool Enabled { get; private set; }
        public string[] ActionIds { get; private set; }
    }

    /// <summary>
    /// Builds and ranks the creator command palette index. The index is assembled once
    /// per open (not per keystroke): editor commands come from the live shell view model,
    /// authored elements are walked off the loaded <see cref="ScenarioDefinition"/>, and
    /// help topics come from <see cref="TutorialContent"/>. Ranking is plain
    /// subsequence/prefix matching so it stays net35-friendly with no external libraries.
    /// </summary>
    internal static class ScenarioGlobalSearchService
    {
        private const int MaxResults = 50;

        /// <summary>
        /// Assemble the full palette index. Cheap enough to run on open; callers should
        /// cache the result and only re-filter with <see cref="Rank"/> per keystroke.
        /// </summary>
        public static List<ScenarioGlobalSearchEntry> BuildEntries(
            ScenarioAuthoringShellViewModel shell,
            ScenarioDefinition definition,
            IList<string> namedVersions)
        {
            List<ScenarioGlobalSearchEntry> entries = new List<ScenarioGlobalSearchEntry>();
            CollectCommands(shell, entries);
            CollectWorkspaceEntries(shell, entries);
            CollectElements(definition, entries);
            CollectVersions(namedVersions, entries);
            CollectHelp(entries);
            DeduplicateExactRoutes(entries);
            return entries;
        }

        private static void DeduplicateExactRoutes(List<ScenarioGlobalSearchEntry> entries)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ScenarioGlobalSearchEntry entry = entries[i];
                if (entry == null || entry.ActionIds == null || entry.ActionIds.Length < 2)
                    continue;
                string route = string.Join("\n", entry.ActionIds);
                if (!seen.Add(route))
                {
                    entries.RemoveAt(i);
                    i--;
                }
            }
        }

        private static void CollectWorkspaceEntries(ScenarioAuthoringShellViewModel shell, List<ScenarioGlobalSearchEntry> entries)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int windowIndex = 0; shell != null && shell.Windows != null && windowIndex < shell.Windows.Length; windowIndex++)
            {
                ScenarioAuthoringShellWindowViewModel window = shell.Windows[windowIndex];
                ScenarioAuthoringWorkspaceViewModel workspace = window != null ? window.WorkspaceBody : null;
                if (workspace == null)
                    continue;

                string openAction = WorkspaceOpenAction(workspace.Id);
                for (int i = 0; workspace.Subtabs != null && i < workspace.Subtabs.Length; i++)
                {
                    ScenarioAuthoringWorkspaceSubtabViewModel subtab = workspace.Subtabs[i];
                    if (subtab == null || subtab.SelectAction == null || string.IsNullOrEmpty(subtab.Label))
                        continue;
                    AddWorkspaceEntry(entries, seen, ScenarioGlobalSearchKind.WorkspaceSubtab, "Workspace", subtab.Label, window.Title, openAction, subtab.SelectAction.Id);
                }

                ScenarioAuthoringNavigatorViewModel navigator = workspace.Navigator;
                for (int i = 0; navigator != null && navigator.Groups != null && i < navigator.Groups.Length; i++)
                {
                    ScenarioAuthoringNavigatorGroupViewModel group = navigator.Groups[i];
                    CollectWorkspaceRows(entries, seen, group != null ? group.Rows : null, window.Title, group != null ? group.Label : null, openAction);
                }
            }
        }

        private static void CollectWorkspaceRows(
            List<ScenarioGlobalSearchEntry> entries,
            HashSet<string> seen,
            ScenarioAuthoringNavigatorRowViewModel[] rows,
            string workspaceLabel,
            string groupLabel,
            string openAction)
        {
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                ScenarioAuthoringNavigatorRowViewModel row = rows[i];
                if (row == null)
                    continue;
                if (row.SelectAction != null && !string.IsNullOrEmpty(row.Title))
                    AddWorkspaceEntry(entries, seen, ScenarioGlobalSearchKind.NavigatorEntity, "Navigator", row.Title, (workspaceLabel ?? "Workspace") + " / " + (groupLabel ?? "Items"), openAction, row.SelectAction.Id);
                CollectWorkspaceRows(entries, seen, row.Children, workspaceLabel, row.Title, openAction);
            }
        }

        private static void AddWorkspaceEntry(
            List<ScenarioGlobalSearchEntry> entries,
            HashSet<string> seen,
            ScenarioGlobalSearchKind kind,
            string kindLabel,
            string name,
            string context,
            string openAction,
            string selectAction)
        {
            string key = (openAction ?? string.Empty) + "\n" + (selectAction ?? string.Empty);
            if (string.IsNullOrEmpty(selectAction) || !seen.Add(key))
                return;
            entries.Add(new ScenarioGlobalSearchEntry(
                kind,
                kindLabel,
                name,
                context,
                true,
                string.IsNullOrEmpty(openAction) ? new[] { selectAction } : new[] { openAction, selectAction }));
        }

        private static string WorkspaceOpenAction(string workspaceId)
        {
            if (string.Equals(workspaceId, ScenarioStoryFocusedEditorActions.WorkspaceId, StringComparison.Ordinal))
                return ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests;
            if (string.Equals(workspaceId, ScenarioCastWorkspaceActions.WorkspaceId, StringComparison.Ordinal))
                return ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.People;
            if (string.Equals(workspaceId, ScenarioSuppliesWorkspaceActions.WorkspaceId, StringComparison.Ordinal))
                return ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.InventoryStorage;
            if (string.Equals(workspaceId, ScenarioMapWorkspaceSelection.WorkspaceId, StringComparison.Ordinal))
                return ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Map;
            return null;
        }

        /// <summary>
        /// Filter and rank the index for a query. An empty query returns everything grouped
        /// by kind; a non-empty query returns the best matches first (exact &gt; prefix &gt;
        /// substring &gt; subsequence), capped for a scrollable list.
        /// </summary>
        public static List<ScenarioGlobalSearchEntry> Rank(
            IList<ScenarioGlobalSearchEntry> entries,
            string query,
            int max)
        {
            int limit = max > 0 ? max : MaxResults;
            List<ScenarioGlobalSearchEntry> results = new List<ScenarioGlobalSearchEntry>();
            if (entries == null || entries.Count == 0)
                return results;

            string trimmed = query != null ? query.Trim() : string.Empty;
            if (trimmed.Length == 0)
            {
                for (int i = 0; i < entries.Count; i++)
                    results.Add(entries[i]);
                results.Sort(CompareGrouped);
                if (results.Count > limit)
                    results.RemoveRange(limit, results.Count - limit);
                return results;
            }

            string needle = trimmed.ToLowerInvariant();
            List<Scored> scored = new List<Scored>();
            for (int i = 0; i < entries.Count; i++)
            {
                ScenarioGlobalSearchEntry entry = entries[i];
                if (entry == null)
                    continue;

                int score = Score(entry, needle);
                if (score <= 0)
                    continue;

                scored.Add(new Scored(entry, score, i));
            }

            scored.Sort(CompareScored);
            for (int i = 0; i < scored.Count && results.Count < limit; i++)
                results.Add(scored[i].Entry);
            return results;
        }

        // === Command collection ===========================================================

        private static void CollectCommands(ScenarioAuthoringShellViewModel shell, List<ScenarioGlobalSearchEntry> entries)
        {
            if (shell == null)
                return;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddCommandActions(shell.Tabs, entries, seen);
            AddCommandActions(shell.ToolbarActions, entries, seen);
            AddCommandActions(shell.WorldSubstageActions, entries, seen);
            AddCommandActions(shell.LayoutActions, entries, seen);
            AddCommandActions(shell.WindowMenuActions, entries, seen);
        }

        private static void AddCommandActions(
            ScenarioAuthoringInspectorAction[] actions,
            List<ScenarioGlobalSearchEntry> entries,
            HashSet<string> seen)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null || string.IsNullOrEmpty(action.Id) || string.IsNullOrEmpty(action.Label))
                    continue;
                if (string.Equals(action.Badge, "GROUP", StringComparison.OrdinalIgnoreCase))
                    continue;
                // The "More >" overflow control is presentation-only, not a real command.
                if (string.Equals(action.Id, "shell.stage.more", StringComparison.Ordinal))
                    continue;
                if (!seen.Add(action.Id))
                    continue;

                string context = !string.IsNullOrEmpty(action.Hint) ? action.Hint : action.Detail;
                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.Command,
                    "Command",
                    action.Label.Trim(),
                    !string.IsNullOrEmpty(context) ? context : "Editor command",
                    action.Enabled,
                    new[] { action.Id }));
            }
        }

        // === Element collection ===========================================================

        private static void CollectElements(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            if (definition == null)
                return;

            CollectStoryStages(definition, entries);
            CollectStoryCharacters(definition, entries);
            CollectConversations(definition, entries);
            CollectQuests(definition, entries);
            CollectCast(definition, entries);
            CollectTimeline(definition, entries);
            CollectMapLocations(definition, entries);
        }

        private static void CollectStoryStages(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            ScenarioFlowDefinition flow = definition.ScenarioFlow;
            if (flow == null || flow.Stages == null)
                return;

            string storyTab = ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests;
            for (int i = 0; i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                if (stage == null)
                    continue;

                int stepCount = stage.IntercomStages != null ? stage.IntercomStages.Count : 0;
                string name = "Stage " + (i + 1).ToString(CultureInfo.InvariantCulture);
                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.StoryStage,
                    "Story stage",
                    name,
                    "Story / Flow / " + Plural(stepCount, "scene"),
                    true,
                    new[] { storyTab, WorkspaceSelect(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, ScenarioStoryFocusedEditorActions.StageEntityId(definition, i)) }));

                for (int s = 0; s < stepCount; s++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[s];
                    if (intercom == null)
                        continue;

                    int lineCount = intercom.Dialogue != null ? intercom.Dialogue.Count : 0;
                    string stepName = "Scene " + (s + 1).ToString(CultureInfo.InvariantCulture);
                    entries.Add(new ScenarioGlobalSearchEntry(
                        ScenarioGlobalSearchKind.IntercomStep,
                        "Scene",
                        stepName,
                        "In " + name + " — " + Plural(lineCount, "dialogue line"),
                        true,
                        new[] { storyTab, WorkspaceSelect(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, ScenarioStoryFocusedEditorActions.SceneEntityId(definition, i, s)) }));
                }
            }
        }

        private static void CollectStoryCharacters(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            List<ScenarioNpcDefinition> characters = definition.ScenarioCharacters;
            if (characters == null)
                return;

            string storyTab = ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests;
            for (int i = 0; i < characters.Count; i++)
            {
                ScenarioNpcDefinition character = characters[i];
                if (character == null)
                    continue;

                string name = PrimaryName(character.DisplayName, "Character " + (i + 1).ToString(CultureInfo.InvariantCulture));
                string id = null;
                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.Character,
                    "Character",
                    name,
                    id != null ? "Story character — id '" + id + "'" : "Story character",
                    true,
                    new[] { storyTab, WorkspaceSelect(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, ScenarioStoryFocusedEditorActions.CharacterEntityId(definition, i)) }));
            }
        }

        private static void CollectConversations(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            ScenarioConversationAuthoringDefinition conversations = definition.Conversations;
            if (conversations == null || conversations.Conversations == null)
                return;

            string storyTab = ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests;
            for (int i = 0; i < conversations.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = conversations.Conversations[i];
                if (conversation == null)
                    continue;

                int lineCount = conversation.Lines != null ? conversation.Lines.Count : 0;
                string name = "Conversation " + (i + 1).ToString(CultureInfo.InvariantCulture);
                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.Conversation,
                    "Conversation",
                    name,
                    "Conversation — " + Plural(lineCount, "line"),
                    true,
                    new[] { storyTab, WorkspaceSelect(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, ScenarioStoryFocusedEditorActions.ConversationEntityId(definition, i)) }));
            }
        }

        private static void CollectCast(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            FamilySetupDefinition family = definition.FamilySetup;
            if (family == null)
                return;

            string peopleTab = ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.People;
            if (family.Members != null)
            {
                for (int i = 0; i < family.Members.Count; i++)
                {
                    FamilyMemberConfig member = family.Members[i];
                    if (member == null)
                        continue;

                    string name = PrimaryName(member.Name, "Survivor " + (i + 1).ToString(CultureInfo.InvariantCulture));
                    entries.Add(new ScenarioGlobalSearchEntry(
                        ScenarioGlobalSearchKind.CastMember,
                        "Cast member",
                        name,
                        "Starting survivor",
                        true,
                        new[] { peopleTab, WorkspaceSelect(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, ScenarioCastWorkspaceActions.StartingEntityId(definition, i)) }));
                }
            }

            if (family.FutureSurvivors != null)
            {
                for (int i = 0; i < family.FutureSurvivors.Count; i++)
                {
                    FutureSurvivorDefinition future = family.FutureSurvivors[i];
                    if (future == null)
                        continue;

                    string name = future.Survivor != null ? PrimaryName(future.Survivor.Name, null) : null;
                    if (name == null)
                        name = "Future survivor " + (i + 1).ToString(CultureInfo.InvariantCulture);
                    entries.Add(new ScenarioGlobalSearchEntry(
                        ScenarioGlobalSearchKind.CastMember,
                        "Cast member",
                        name,
                        "Future survivor",
                        true,
                        new[] { peopleTab, WorkspaceSelect(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, ScenarioCastWorkspaceActions.FutureEntityId(definition, i)) }));
                }
            }
        }

        private static void CollectQuests(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            if (definition.Quests == null || definition.Quests.Quests == null)
                return;

            string storyTab = ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests;
            for (int i = 0; i < definition.Quests.Quests.Count; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                if (quest == null)
                    continue;
                string name = PrimaryName(quest.Title, "Quest " + (i + 1).ToString(CultureInfo.InvariantCulture));
                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.NavigatorEntity,
                    "Navigator",
                    name,
                    "Story / Quest Popups / Authored",
                    true,
                    new[] { storyTab, WorkspaceSelect(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, ScenarioStoryFocusedEditorActions.QuestEntityId(definition, i)) }));
            }
        }

        private static void CollectTimeline(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            string eventsTab = ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Events;
            TriggersAndEventsDefinition triggers = definition.TriggersAndEvents;
            if (triggers != null)
            {
                for (int i = 0; triggers.Triggers != null && i < triggers.Triggers.Count; i++)
                {
                    TriggerDef trigger = triggers.Triggers[i];
                    if (trigger == null)
                        continue;

                    string name = "Trigger " + (i + 1).ToString(CultureInfo.InvariantCulture);
                    entries.Add(new ScenarioGlobalSearchEntry(
                        ScenarioGlobalSearchKind.TimelineEntry,
                        "Timeline",
                        name,
                        "Timeline / Triggers",
                        true,
                        new[] { eventsTab }));
                }

                for (int i = 0; triggers.WeatherEvents != null && i < triggers.WeatherEvents.Count; i++)
                {
                    WeatherEventDefinition weather = triggers.WeatherEvents[i];
                    if (weather == null)
                        continue;

                    string name = "Weather " + (i + 1).ToString(CultureInfo.InvariantCulture);
                    entries.Add(new ScenarioGlobalSearchEntry(
                        ScenarioGlobalSearchKind.TimelineEntry,
                        "Timeline",
                        name,
                        "Timeline / Weather",
                        true,
                        new[] { eventsTab }));
                }
            }

            if (definition.ScheduledActions != null)
            {
                for (int i = 0; i < definition.ScheduledActions.Count; i++)
                {
                    ShelteredAPI.Scenarios.Domain.Scheduling.ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                    if (action == null)
                        continue;

                    string name = "Scheduled action " + (i + 1).ToString(CultureInfo.InvariantCulture);
                    entries.Add(new ScenarioGlobalSearchEntry(
                        ScenarioGlobalSearchKind.TimelineEntry,
                        "Timeline",
                        name,
                        "Scheduled action",
                        true,
                        new[] { eventsTab }));
                }
            }
        }

        private static void CollectMapLocations(ScenarioDefinition definition, List<ScenarioGlobalSearchEntry> entries)
        {
            MapAuthoringDefinition map = definition.Map;
            if (map == null || map.Locations == null)
                return;

            string mapTab = ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Map;
            for (int i = 0; i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location == null)
                    continue;

                string name = PrimaryName(location.DisplayName, "Location " + (i + 1).ToString(CultureInfo.InvariantCulture));
                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.MapLocation,
                    "Map location",
                    name,
                    "Map / Locations",
                    true,
                    new[] { mapTab, WorkspaceSelect(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, ScenarioMapWorkspaceSelection.LocationEntityId(map, i)) }));
            }
        }

        private static string WorkspaceSelect(string workspaceId, string subtabId, string entityId)
        {
            return ScenarioAuthoringWorkspaceViewModelFactory.BuildWorkspaceActionId(
                ScenarioAuthoringActionIds.ActionRendererWorkspaceEntitySelectPrefix,
                workspaceId,
                subtabId,
                entityId);
        }

        private static void CollectVersions(IList<string> namedVersions, List<ScenarioGlobalSearchEntry> entries)
        {
            if (namedVersions == null)
                return;

            for (int i = 0; i < namedVersions.Count; i++)
            {
                string name = TrimToNull(namedVersions[i]);
                if (name == null)
                    continue;

                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.Version,
                    "Version",
                    name,
                    "Named version — open history",
                    true,
                    new[] { ScenarioAuthoringActionIds.ActionHistoryShow }));
            }
        }

        private static void CollectHelp(List<ScenarioGlobalSearchEntry> entries)
        {
            ScenarioAuthoringHelpPage[] pages = TutorialContent.GetHelpPages();
            for (int i = 0; pages != null && i < pages.Length; i++)
            {
                ScenarioAuthoringHelpPage page = pages[i];
                if (page == null || string.IsNullOrEmpty(page.Id))
                    continue;

                entries.Add(new ScenarioGlobalSearchEntry(
                    ScenarioGlobalSearchKind.Help,
                    "Help",
                    TrimToNull(page.Title) ?? page.Id,
                    "Help topic",
                    true,
                    new[] { ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + page.Id }));
            }
        }

        // === Ranking ======================================================================

        private static int Score(ScenarioGlobalSearchEntry entry, string needle)
        {
            string name = (entry.Name ?? string.Empty).ToLowerInvariant();
            string context = (entry.Context ?? string.Empty).ToLowerInvariant();

            if (string.Equals(name, needle, StringComparison.Ordinal))
                return 1000;
            if (name.StartsWith(needle, StringComparison.Ordinal))
                return 800;
            if (name.IndexOf(needle, StringComparison.Ordinal) >= 0)
                return 600;
            if (IsSubsequence(name, needle))
                return 400;
            if (context.IndexOf(needle, StringComparison.Ordinal) >= 0)
                return 200;
            if (IsSubsequence(context, needle))
                return 100;

            return 0;
        }

        private static bool IsSubsequence(string text, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;
            if (string.IsNullOrEmpty(text))
                return false;

            int qi = 0;
            for (int i = 0; i < text.Length && qi < query.Length; i++)
            {
                if (text[i] == query[qi])
                    qi++;
            }

            return qi == query.Length;
        }

        private static int CompareScored(Scored left, Scored right)
        {
            if (left.Score != right.Score)
                return right.Score.CompareTo(left.Score);

            int byGroup = ((int)left.Entry.Kind).CompareTo((int)right.Entry.Kind);
            if (byGroup != 0)
                return byGroup;

            int byLength = left.Entry.Name.Length.CompareTo(right.Entry.Name.Length);
            if (byLength != 0)
                return byLength;

            int byName = string.Compare(left.Entry.Name, right.Entry.Name, StringComparison.OrdinalIgnoreCase);
            if (byName != 0)
                return byName;

            return left.Order.CompareTo(right.Order);
        }

        private static int CompareGrouped(ScenarioGlobalSearchEntry left, ScenarioGlobalSearchEntry right)
        {
            int byGroup = ((int)left.Kind).CompareTo((int)right.Kind);
            if (byGroup != 0)
                return byGroup;

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static string Plural(int count, string noun)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? string.Empty : "s");
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string PrimaryName(string value, string fallback)
        {
            string candidate = TrimToNull(value);
            if (candidate == null
                || candidate.IndexOf('.') >= 0
                || candidate.IndexOf('_') >= 0
                || candidate.IndexOf('/') >= 0
                || candidate.IndexOf('\\') >= 0
                || candidate.IndexOf(':') >= 0)
            {
                return fallback;
            }
            return candidate;
        }

        private sealed class Scored
        {
            public Scored(ScenarioGlobalSearchEntry entry, int score, int order)
            {
                Entry = entry;
                Score = score;
                Order = order;
            }

            public ScenarioGlobalSearchEntry Entry { get; private set; }
            public int Score { get; private set; }
            public int Order { get; private set; }
        }
    }
}
