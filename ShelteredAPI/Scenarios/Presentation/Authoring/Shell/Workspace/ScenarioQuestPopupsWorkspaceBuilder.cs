using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioQuestPopupsWorkspaceBuilder
    {
        private const string AuthoredGroupId = "authored";
        private const string LibraryGroupId = "library";
        private const string RuntimeGroupId = "runtime";
        private const string LibraryBrowseEntityId = "quest.library.browse";

        public ScenarioAuthoringWorkspaceViewModel Build(
            ScenarioAuthoringWindowContentContext context,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot =
                ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot.From(definition);
            List<QuestInstance> runtimeQuests =
                ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot.GetLiveQuests()
                ?? new List<QuestInstance>();
            ScenarioAuthoringRendererInteractionState state = ScenarioAuthoringRendererInteractionState.Instance;
            string selected = state.GetWorkspaceSelection(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId);

            int authoredIndex;
            int libraryIndex;
            int runtimeIndex;
            bool libraryBrowse = string.Equals(selected, LibraryBrowseEntityId, StringComparison.Ordinal);
            bool valid = string.IsNullOrEmpty(selected)
                || libraryBrowse
                || ScenarioStoryFocusedEditorActions.TryResolveQuestEntity(definition, selected, out authoredIndex)
                || TryResolveLibraryEntity(snapshot.Catalog, selected, out libraryIndex)
                || TryResolveRuntimeEntity(runtimeQuests, selected, out runtimeIndex);
            if (!valid)
            {
                selected = null;
                state.SetWorkspaceSelection(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    null);
            }

            ScenarioAuthoringWorkspaceViewModel workspace = factory.CreateWorkspace(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId);
            workspace.Navigator = BuildNavigator(snapshot, runtimeQuests, selected, state, factory);

            if (ScenarioStoryFocusedEditorActions.TryResolveQuestEntity(definition, selected, out authoredIndex))
                workspace.Document = BuildAuthoredDocument(snapshot, authoredIndex, factory);
            else if (TryResolveLibraryEntity(snapshot.Catalog, selected, out libraryIndex))
                workspace.Document = BuildLibraryDocument(snapshot, libraryIndex, factory);
            else if (TryResolveRuntimeEntity(runtimeQuests, selected, out runtimeIndex))
                workspace.Document = BuildRuntimeDocument(runtimeQuests[runtimeIndex], runtimeIndex, factory);
            else if (libraryBrowse)
                workspace.Document = BuildLibraryBrowseDocument(snapshot, factory);
            else
                workspace.Document = BuildOverview(snapshot, runtimeQuests, factory);

            return workspace;
        }

        private static ScenarioAuthoringNavigatorViewModel BuildNavigator(
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot,
            List<QuestInstance> runtimeQuests,
            string selected,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioAuthoringNavigatorViewModel navigator = factory.CreateNavigator("story.quest-popups.navigator");
            navigator.SearchControlId = "story.quest-popups.search";
            navigator.SearchText = state.GetWorkspaceSearch(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId);
            navigator.SearchPlaceholder = "Search quest popups";
            navigator.SelectedEntityId = selected;
            navigator.EmptyMessage = "No quest popups match this search.";
            navigator.Groups = new[]
            {
                BuildAuthoredGroup(snapshot, selected, navigator.SearchText, state, factory),
                BuildLibraryGroup(snapshot, selected, navigator.SearchText, state, factory),
                BuildRuntimeGroup(runtimeQuests, selected, navigator.SearchText, state, factory)
            };
            return navigator;
        }

        private static ScenarioAuthoringNavigatorGroupViewModel BuildAuthoredGroup(
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; snapshot != null && i < snapshot.AuthoredCount; i++)
            {
                QuestDefinition quest = snapshot.Authored[i];
                if (quest == null)
                    continue;
                QuestDef libraryQuest = ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot.FindQuestDef(quest.Id);
                string title = ScenarioQuestAuthoringContentBuilder.ResolveQuestName(quest, libraryQuest, i);
                string subtitle = string.IsNullOrEmpty(quest.StartTriggerId)
                    ? ScenarioQuestAuthoringContentBuilder.QuestAuthoringHelpers.FormatSchedule(quest.ScheduledStart)
                    : "Starts from an authored trigger";
                if (!MatchesSearch(search, title, subtitle))
                    continue;
                string entityId = ScenarioStoryFocusedEditorActions.QuestEntityId(snapshot.Definition, i);
                string validation = ScenarioQuestAuthoringContentBuilder.QuestAuthoringHelpers.FormatQuestValidation(quest, snapshot.Definition);
                string completion = CompletionStatus(quest, snapshot.Definition);
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entityId,
                    Title = title,
                    Subtitle = subtitle,
                    IconText = "QP",
                    Selected = string.Equals(selected, entityId, StringComparison.Ordinal),
                    StatusChips = new[]
                    {
                        StatusChip("quest.authored.validation." + i.ToString(CultureInfo.InvariantCulture), validation, ValidationTone(validation)),
                        StatusChip("quest.authored.completion." + i.ToString(CultureInfo.InvariantCulture), completion, completion == "Completion requirement missing" ? ScenarioAuthoringStatusTone.Error : ScenarioAuthoringStatusTone.Neutral)
                    },
                    SelectAction = factory.CreateEntityAction(
                        ScenarioStoryFocusedEditorActions.WorkspaceId,
                        ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                        entityId,
                        "Select " + title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }

            return new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = AuthoredGroupId,
                Label = "Authored",
                IconText = "QP",
                Expanded = state.GetWorkspaceExpanded(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    AuthoredGroupId,
                    true),
                ToggleAction = factory.CreateGroupToggleAction(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    AuthoredGroupId,
                    "Toggle Authored"),
                CreateAction = ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleAdd,
                    "Add Quest Popup",
                    "Create and select an authored quest popup.",
                    true,
                    snapshot != null && snapshot.AuthoredCount == 0,
                    "Q+"),
                StatusChips = new[]
                {
                    StatusChip(
                        "quest.authored.count",
                        CountLabel(snapshot != null ? snapshot.AuthoredCount : 0, "quest", "quests"),
                        ScenarioAuthoringStatusTone.Informational)
                },
                Rows = rows.ToArray()
            };
        }

        private static ScenarioAuthoringNavigatorGroupViewModel BuildLibraryGroup(
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; snapshot != null && snapshot.Catalog != null && i < snapshot.Catalog.Count; i++)
            {
                QuestDef quest = snapshot.Catalog[i];
                if (quest == null)
                    continue;
                string title = ScenarioQuestAuthoringContentBuilder.ResolveQuestDefName(quest, i);
                bool available = ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot.IsQuestAvailable(quest);
                string availability = available ? "Available to add now" : "Locked in the base game";
                if (!MatchesSearch(search, title, availability))
                    continue;
                string entityId = LibraryEntityId(snapshot.Catalog, quest, i);
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entityId,
                    Title = title,
                    Subtitle = availability,
                    IconText = "LB",
                    Selected = string.Equals(selected, entityId, StringComparison.Ordinal),
                    StatusChips = new[]
                    {
                        StatusChip(
                            "quest.library.availability." + i.ToString(CultureInfo.InvariantCulture),
                            available ? "Available" : "Locked",
                            available ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning)
                    },
                    SelectAction = factory.CreateEntityAction(
                        ScenarioStoryFocusedEditorActions.WorkspaceId,
                        ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                        entityId,
                        "Browse " + title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }

            return new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = LibraryGroupId,
                Label = "Library",
                IconText = "LB",
                Expanded = state.GetWorkspaceExpanded(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    LibraryGroupId,
                    true),
                ToggleAction = factory.CreateGroupToggleAction(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    LibraryGroupId,
                    "Toggle Library"),
                CreateAction = factory.CreateEntityAction(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    LibraryBrowseEntityId,
                    "Browse Library"),
                StatusChips = new[]
                {
                    StatusChip(
                        "quest.library.count",
                        snapshot != null && snapshot.CatalogReady
                            ? CountLabel(snapshot.CatalogCount, "quest", "quests")
                            : "Library unavailable",
                        snapshot != null && snapshot.CatalogReady ? ScenarioAuthoringStatusTone.Informational : ScenarioAuthoringStatusTone.Warning)
                },
                Rows = rows.ToArray()
            };
        }

        private static ScenarioAuthoringNavigatorGroupViewModel BuildRuntimeGroup(
            List<QuestInstance> runtimeQuests,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; runtimeQuests != null && i < runtimeQuests.Count; i++)
            {
                QuestInstance quest = runtimeQuests[i];
                if (quest == null || quest.definition == null)
                    continue;
                string title = ScenarioQuestAuthoringContentBuilder.ResolveQuestDefName(quest.definition, i);
                string status = RuntimeStatus(quest);
                if (!MatchesSearch(search, title, status))
                    continue;
                string entityId = RuntimeEntityId(runtimeQuests, quest, i);
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entityId,
                    Title = title,
                    Subtitle = "Read-only runtime status",
                    IconText = "RT",
                    Selected = string.Equals(selected, entityId, StringComparison.Ordinal),
                    StatusChips = new[]
                    {
                        StatusChip(
                            "quest.runtime.status." + i.ToString(CultureInfo.InvariantCulture),
                            status,
                            RuntimeTone(status))
                    },
                    SelectAction = factory.CreateEntityAction(
                        ScenarioStoryFocusedEditorActions.WorkspaceId,
                        ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                        entityId,
                        "View " + title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }

            return new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = RuntimeGroupId,
                Label = "Runtime",
                IconText = "RT",
                Expanded = state.GetWorkspaceExpanded(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    RuntimeGroupId,
                    true),
                ToggleAction = factory.CreateGroupToggleAction(
                    ScenarioStoryFocusedEditorActions.WorkspaceId,
                    ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId,
                    RuntimeGroupId,
                    "Toggle Runtime"),
                StatusChips = new[]
                {
                    StatusChip("quest.runtime.count", CountLabel(rows.Count, "running", "running"), rows.Count > 0 ? ScenarioAuthoringStatusTone.Informational : ScenarioAuthoringStatusTone.Neutral)
                },
                Rows = rows.ToArray()
            };
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildOverview(
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot,
            List<QuestInstance> runtimeQuests,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.quest-popups.overview", "Quest Popups");
            document.Subtitle = "Schedule authored popup quests, add from the library, and inspect live runtime status.";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, "Back to Navigator");
            document.StatusChips = new[]
            {
                StatusChip("quest.overview.authored", CountLabel(snapshot != null ? snapshot.AuthoredCount : 0, "authored", "authored"), ScenarioAuthoringStatusTone.Informational),
                StatusChip("quest.overview.runtime", CountLabel(CountRuntimeRows(runtimeQuests), "running", "running"), ScenarioAuthoringStatusTone.Neutral)
            };
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_popups_overview",
                    Title = "Quest Popups",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Authored popups", (snapshot != null ? snapshot.AuthoredCount : 0).ToString(CultureInfo.InvariantCulture)),
                        ScenarioInspectorItemFactory.Property("Available library entries", (snapshot != null ? snapshot.CatalogCount : 0).ToString(CultureInfo.InvariantCulture)),
                        ScenarioInspectorItemFactory.Property("Running quests", CountRuntimeRows(runtimeQuests).ToString(CultureInfo.InvariantCulture)),
                        ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionQuestScheduleAdd, "Add Quest Popup", "Create and select an authored quest popup.", true, snapshot != null && snapshot.AuthoredCount == 0, "Q+")),
                        ScenarioInspectorItemFactory.ActionItem(factory.CreateEntityAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, LibraryBrowseEntityId, "Browse Library"))
                    }
                }
            };
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildAuthoredDocument(
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot,
            int questIndex,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            QuestDefinition quest = snapshot.Authored[questIndex];
            QuestDef libraryQuest = ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot.FindQuestDef(quest != null ? quest.Id : null);
            string title = ScenarioQuestAuthoringContentBuilder.ResolveQuestName(quest, libraryQuest, questIndex);
            string validation = ScenarioQuestAuthoringContentBuilder.QuestAuthoringHelpers.FormatQuestValidation(quest, snapshot.Definition);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.quest-popups.authored." + questIndex.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = string.IsNullOrEmpty(quest.StartTriggerId)
                ? ScenarioQuestAuthoringContentBuilder.QuestAuthoringHelpers.FormatSchedule(quest.ScheduledStart)
                : "Starts from an authored trigger";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story" },
                new ScenarioAuthoringBreadcrumbViewModel
                {
                    Label = "Authored",
                    Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, string.Empty, "Authored")
                },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            document.StatusChips = new[]
            {
                StatusChip("quest.document.validation." + questIndex.ToString(CultureInfo.InvariantCulture), validation, ValidationTone(validation)),
                StatusChip("quest.document.completion." + questIndex.ToString(CultureInfo.InvariantCulture), CompletionStatus(quest, snapshot.Definition), CompletionStatus(quest, snapshot.Definition) == "Completion requirement missing" ? ScenarioAuthoringStatusTone.Error : ScenarioAuthoringStatusTone.Neutral)
            };
            document.Sections = ScenarioQuestAuthoringContentBuilder.BuildQuestWorkspaceDocumentSections(snapshot, questIndex);
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildLibraryBrowseDocument(
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.quest-popups.library", "Quest Library");
            document.Subtitle = "Browse a human-readable quest name, review availability, then add it with one click.";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story" },
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Quest Popups" },
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Library" }
            };
            document.StatusChips = new[]
            {
                StatusChip(
                    "quest.library.document.status",
                    snapshot != null && snapshot.CatalogReady ? CountLabel(snapshot.CatalogCount, "quest", "quests") : "Library unavailable",
                    snapshot != null && snapshot.CatalogReady ? ScenarioAuthoringStatusTone.Informational : ScenarioAuthoringStatusTone.Warning)
            };
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_library_browse",
                    Title = "Browse",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Text(snapshot != null && snapshot.CatalogReady
                            ? "Select a quest under Library to review its availability and add it to Authored."
                            : "The quest library is not available in this scene. Open a save or begin a playtest to load it.")
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_library_add",
                    Title = "Add",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    Items = new[] { ScenarioInspectorItemFactory.Text("Choose a library quest first; its one-click add action will appear here.") }
                }
            };
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildLibraryDocument(
            ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot snapshot,
            int libraryIndex,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            QuestDef quest = snapshot.Catalog[libraryIndex];
            string title = ScenarioQuestAuthoringContentBuilder.ResolveQuestDefName(quest, libraryIndex);
            bool available = ScenarioQuestAuthoringContentBuilder.QuestAuthoringSnapshot.IsQuestAvailable(quest);
            string availability = available ? "Available to add now" : "Locked in the base game; you can still author it for later playtesting";
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.quest-popups.library." + libraryIndex.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = availability;
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story" },
                new ScenarioAuthoringBreadcrumbViewModel
                {
                    Label = "Library",
                    Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, LibraryBrowseEntityId, "Library")
                },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            document.StatusChips = new[]
            {
                StatusChip("quest.library.document.availability." + libraryIndex.ToString(CultureInfo.InvariantCulture), available ? "Available" : "Locked", available ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning)
            };
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_library_browse_" + libraryIndex.ToString(CultureInfo.InvariantCulture),
                    Title = "Browse",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    StatusChips = new[] { StatusChip("quest.library.card.availability." + libraryIndex.ToString(CultureInfo.InvariantCulture), available ? "Available" : "Locked", available ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning) },
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Quest", title),
                        ScenarioInspectorItemFactory.Property("Availability", availability),
                        ScenarioInspectorItemFactory.Property(
                            "Description",
                            ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                                quest != null ? quest.descriptionKey : null,
                                quest != null ? quest.descriptionKey : null,
                                null,
                                "No library description is available.").Text)
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_library_add_" + libraryIndex.ToString(CultureInfo.InvariantCulture),
                    Title = "Add",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Text("Add this quest as a scheduled authored popup. The new popup will open under Authored."),
                        ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                            ScenarioAuthoringActionIds.ActionQuestCatalogAddPrefix + libraryIndex.ToString(CultureInfo.InvariantCulture),
                            "Add to Authored",
                            "Create this authored quest popup and select it.",
                            true,
                            false,
                            "Q+"))
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_library_advanced_" + libraryIndex.ToString(CultureInfo.InvariantCulture),
                    Title = "Advanced",
                    IsAdvanced = true,
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("QuestLibrary id", quest != null ? quest.id : string.Empty),
                        ScenarioInspectorItemFactory.Property("Name localization key", quest != null ? quest.nameKey : string.Empty),
                        ScenarioInspectorItemFactory.Property("Description localization key", quest != null ? quest.descriptionKey : string.Empty)
                    }
                }
            };
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildRuntimeDocument(
            QuestInstance quest,
            int runtimeIndex,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            string title = ScenarioQuestAuthoringContentBuilder.ResolveQuestDefName(quest != null ? quest.definition : null, runtimeIndex);
            string status = RuntimeStatus(quest);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.quest-popups.runtime." + runtimeIndex.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = "Read-only runtime status";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story" },
                new ScenarioAuthoringBreadcrumbViewModel
                {
                    Label = "Runtime",
                    Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, string.Empty, "Runtime")
                },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            document.StatusChips = new[] { StatusChip("quest.runtime.document.status." + runtimeIndex.ToString(CultureInfo.InvariantCulture), status, RuntimeTone(status)) };
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_runtime_status_" + runtimeIndex.ToString(CultureInfo.InvariantCulture),
                    Title = "Status",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    StatusChips = new[] { StatusChip("quest.runtime.card.status." + runtimeIndex.ToString(CultureInfo.InvariantCulture), status, RuntimeTone(status)) },
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Quest", title),
                        ScenarioInspectorItemFactory.Property("State", status),
                        ScenarioInspectorItemFactory.Property("Editing", "Read-only while the quest is running")
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_runtime_advanced_" + runtimeIndex.ToString(CultureInfo.InvariantCulture),
                    Title = "Advanced",
                    IsAdvanced = true,
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Runtime definition id", quest != null && quest.definition != null ? quest.definition.id : string.Empty),
                        ScenarioInspectorItemFactory.Property("Name localization key", quest != null && quest.definition != null ? quest.definition.nameKey : string.Empty),
                        ScenarioInspectorItemFactory.Property("Runtime description key", quest != null ? quest.descriptionKey : string.Empty)
                    }
                }
            };
            return document;
        }

        private static string LibraryEntityId(List<QuestDef> catalog, QuestDef quest, int index)
        {
            string id = quest != null ? quest.id : null;
            int matchingIds = 0;
            for (int i = 0; catalog != null && i < catalog.Count; i++)
            {
                QuestDef candidate = catalog[i];
                if (candidate != null && string.Equals(candidate.id, id, StringComparison.OrdinalIgnoreCase))
                    matchingIds++;
            }
            return !string.IsNullOrEmpty(id) && matchingIds == 1
                ? "quest.library.id." + ScenarioAuthoringActionCodec.EncodeToken(id)
                : "quest.library.index." + index.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryResolveLibraryEntity(List<QuestDef> catalog, string entityId, out int index)
        {
            index = -1;
            for (int i = 0; catalog != null && i < catalog.Count; i++)
            {
                if (string.Equals(LibraryEntityId(catalog, catalog[i], i), entityId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        private static string RuntimeEntityId(List<QuestInstance> quests, QuestInstance quest, int index)
        {
            string definitionId = quest != null && quest.definition != null ? quest.definition.id : string.Empty;
            int matchingDefinitions = 0;
            for (int i = 0; quests != null && i < quests.Count; i++)
            {
                QuestInstance candidate = quests[i];
                if (candidate != null && candidate.definition != null
                    && string.Equals(candidate.definition.id, definitionId, StringComparison.OrdinalIgnoreCase))
                    matchingDefinitions++;
            }
            return !string.IsNullOrEmpty(definitionId) && matchingDefinitions == 1
                ? "quest.runtime.id." + ScenarioAuthoringActionCodec.EncodeToken(definitionId)
                : "quest.runtime.index." + index.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryResolveRuntimeEntity(List<QuestInstance> quests, string entityId, out int index)
        {
            index = -1;
            for (int i = 0; quests != null && i < quests.Count; i++)
            {
                QuestInstance quest = quests[i];
                if (quest != null && quest.definition != null && string.Equals(RuntimeEntityId(quests, quest, i), entityId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        private static bool MatchesSearch(string search, string first, string second)
        {
            return string.IsNullOrEmpty(search)
                || (!string.IsNullOrEmpty(first) && first.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(second) && second.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string CompletionStatus(QuestDefinition quest, ScenarioDefinition definition)
        {
            if (quest == null || string.IsNullOrEmpty(quest.CompletionConditionId))
                return "No completion requirement";
            return string.Equals(
                ScenarioQuestAuthoringContentBuilder.QuestAuthoringHelpers.FormatQuestValidation(quest, definition),
                "Completion condition is missing",
                StringComparison.Ordinal)
                    ? "Completion requirement missing"
                    : "Completion requirement ready";
        }

        private static ScenarioAuthoringStatusTone ValidationTone(string validation)
        {
            if (string.Equals(validation, "Available in the base game", StringComparison.Ordinal))
                return ScenarioAuthoringStatusTone.Ready;
            if (!string.IsNullOrEmpty(validation) && validation.StartsWith("Locked", StringComparison.Ordinal))
                return ScenarioAuthoringStatusTone.Warning;
            return ScenarioAuthoringStatusTone.Error;
        }

        private static string RuntimeStatus(QuestInstance quest)
        {
            string state = quest != null ? quest.state.ToString() : string.Empty;
            if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
                return "Running";
            if (string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
                return "Completed";
            if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
                return "Failed";
            return "Waiting";
        }

        private static ScenarioAuthoringStatusTone RuntimeTone(string status)
        {
            if (string.Equals(status, "Completed", StringComparison.Ordinal))
                return ScenarioAuthoringStatusTone.Ready;
            if (string.Equals(status, "Failed", StringComparison.Ordinal))
                return ScenarioAuthoringStatusTone.Error;
            return ScenarioAuthoringStatusTone.Informational;
        }

        private static ScenarioAuthoringStatusChipViewModel StatusChip(
            string id,
            string text,
            ScenarioAuthoringStatusTone tone)
        {
            return new ScenarioAuthoringStatusChipViewModel
            {
                Id = id,
                Text = text,
                Tone = tone
            };
        }

        private static int CountRuntimeRows(List<QuestInstance> quests)
        {
            int count = 0;
            for (int i = 0; quests != null && i < quests.Count; i++)
                if (quests[i] != null && quests[i].definition != null) count++;
            return count;
        }

        private static string CountLabel(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }
    }
}
