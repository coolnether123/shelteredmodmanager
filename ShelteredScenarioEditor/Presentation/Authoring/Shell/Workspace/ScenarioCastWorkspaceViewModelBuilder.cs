using ShelteredScenarioEditor.Application.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    internal static class ScenarioCastWorkspaceActions
    {
        public const string WorkspaceId = "cast";
        public const string SubtabId = "survivors";

        public static string StartingEntityId(ScenarioDefinition definition, int index)
        {
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            FamilyMemberConfig member = family != null && family.Members != null && index >= 0 && index < family.Members.Count
                ? family.Members[index]
                : null;
            string actorKey = ActorKey(member != null ? member.ActorRef : null);
            return actorKey != null && CountStartingActorKeys(family, actorKey) == 1
                ? "starting.actor." + ScenarioAutomationIdCodec.EncodeToken(actorKey)
                : "starting.index." + index.ToString(CultureInfo.InvariantCulture);
        }

        public static string FutureEntityId(ScenarioDefinition definition, int index)
        {
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            FutureSurvivorDefinition survivor = family != null && family.FutureSurvivors != null && index >= 0 && index < family.FutureSurvivors.Count
                ? family.FutureSurvivors[index]
                : null;
            string id = TrimToNull(survivor != null ? survivor.Id : null);
            return id != null && CountFutureIds(family, id) == 1
                ? "future.id." + ScenarioAutomationIdCodec.EncodeToken(id)
                : "future.index." + index.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryResolveStartingEntity(ScenarioDefinition definition, string entityId, out int index)
        {
            index = -1;
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            for (int i = 0; family != null && family.Members != null && i < family.Members.Count; i++)
            {
                if (string.Equals(StartingEntityId(definition, i), entityId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolveFutureEntity(ScenarioDefinition definition, string entityId, out int index)
        {
            index = -1;
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            for (int i = 0; family != null && family.FutureSurvivors != null && i < family.FutureSurvivors.Count; i++)
            {
                if (string.Equals(FutureEntityId(definition, i), entityId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static void SelectStartingDocument(ScenarioDefinition definition, int index, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(StartingEntityId(definition, index), rendererInteraction);
        }

        public static void SelectFutureDocument(ScenarioDefinition definition, int index, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(FutureEntityId(definition, index), rendererInteraction);
        }

        public static void SelectOverview(ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            SelectDocument(null, rendererInteraction);
        }

        private static void SelectDocument(string entityId, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            rendererInteraction.SetWorkspaceSelection(WorkspaceId, SubtabId, entityId);
            rendererInteraction.SetWorkspaceNarrowPane(WorkspaceId, SubtabId, true);
        }

        private static int CountStartingActorKeys(FamilySetupDefinition family, string actorKey)
        {
            int count = 0;
            for (int i = 0; family != null && family.Members != null && i < family.Members.Count; i++)
            {
                FamilyMemberConfig member = family.Members[i];
                if (string.Equals(ActorKey(member != null ? member.ActorRef : null), actorKey, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        private static int CountFutureIds(FamilySetupDefinition family, string id)
        {
            int count = 0;
            for (int i = 0; family != null && family.FutureSurvivors != null && i < family.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = family.FutureSurvivors[i];
                if (string.Equals(TrimToNull(survivor != null ? survivor.Id : null), id, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        private static string ActorKey(ScenarioActorRef actorRef)
        {
            if (actorRef == null)
                return null;
            string bindingType = TrimToNull(actorRef.BindingType);
            string bindingKey = TrimToNull(actorRef.BindingKey);
            if (bindingType != null && bindingKey != null)
                return bindingType + "\n" + bindingKey;
            string kind = TrimToNull(actorRef.Kind);
            return kind != null && actorRef.LocalId > 0
                ? kind + "\n" + actorRef.LocalId.ToString(CultureInfo.InvariantCulture) + "\n" + (actorRef.Domain ?? string.Empty)
                : null;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length > 0 ? trimmed : null;
        }
    }

    internal sealed class ScenarioCastWorkspaceViewModelBuilder
    {
        private readonly ScenarioAuthoringWorkspaceViewModelFactory _factory;
        private readonly ScenarioSurvivorWorkspaceDocumentBuilder _documentBuilder;

        public ScenarioCastWorkspaceViewModelBuilder()
        {
            _factory = new ScenarioAuthoringWorkspaceViewModelFactory();
            _documentBuilder = new ScenarioSurvivorWorkspaceDocumentBuilder();
        }

        public ScenarioAuthoringWorkspaceViewModel Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioAuthoringRendererInteractionState state = context.RendererInteraction;
            string selected = state.GetWorkspaceSelection(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId);
            int selectedIndex;
            bool starting = ScenarioCastWorkspaceActions.TryResolveStartingEntity(definition, selected, out selectedIndex);
            bool future = !starting && ScenarioCastWorkspaceActions.TryResolveFutureEntity(definition, selected, out selectedIndex);
            if (!string.IsNullOrEmpty(selected) && !starting && !future)
            {
                selected = null;
                state.SetWorkspaceSelection(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, null);
            }

            ScenarioAuthoringWorkspaceViewModel workspace = _factory.CreateWorkspace(
                ScenarioCastWorkspaceActions.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                ScenarioCastWorkspaceActions.SubtabId);
            workspace.Navigator = BuildNavigator(definition, selected, state);
            if (starting)
                workspace.Document = _documentBuilder.BuildStartingDocument(context, selectedIndex, _factory);
            else if (future)
                workspace.Document = _documentBuilder.BuildFutureDocument(context, selectedIndex, _factory);
            else
                workspace.Document = BuildOverview(definition);
            return workspace;
        }

        private ScenarioAuthoringNavigatorViewModel BuildNavigator(
            ScenarioDefinition definition,
            string selected,
            ScenarioAuthoringRendererInteractionState state)
        {
            ScenarioAuthoringNavigatorViewModel navigator = _factory.CreateNavigator("cast.navigator");
            navigator.SearchControlId = "cast.search";
            navigator.SearchText = state.GetWorkspaceSearch(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId);
            navigator.SearchPlaceholder = "Search survivors";
            navigator.SelectedEntityId = selected;
            navigator.EmptyMessage = "No authored survivors match this search.";
            navigator.Groups = new[]
            {
                BuildStartingGroup(definition, selected, navigator.SearchText, state),
                BuildFutureGroup(definition, selected, navigator.SearchText, state)
            };
            return navigator;
        }

        private ScenarioAuthoringNavigatorGroupViewModel BuildStartingGroup(
            ScenarioDefinition definition,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state)
        {
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; family != null && family.Members != null && i < family.Members.Count; i++)
            {
                FamilyMemberConfig member = family.Members[i];
                string title = ScenarioSurvivorWorkspaceDocumentBuilder.ResolveStartingName(member, i);
                if (!MatchesSearch(title, search))
                    continue;
                string entity = ScenarioCastWorkspaceActions.StartingEntityId(definition, i);
                bool warning = ScenarioSurvivorWorkspaceDocumentBuilder.NeedsAttention(member);
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entity,
                    Title = title,
                    Subtitle = "Starts in the shelter",
                    IconText = "ST",
                    Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                    StatusChips = new[]
                    {
                        Chip(
                            "cast.starting.status." + i.ToString(CultureInfo.InvariantCulture),
                            warning ? "Needs attention" : "Ready",
                            warning ? ScenarioAuthoringStatusTone.Warning : ScenarioAuthoringStatusTone.Ready,
                            warning ? _factory.CreateWarningAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, entity, "Open " + title) : null)
                    },
                    SelectAction = _factory.CreateEntityAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, entity, "Select " + title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }

            bool missing = family == null || family.Members == null || family.Members.Count == 0;
            return new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = "starting-survivors",
                Label = "Starting Survivors",
                IconText = "ST",
                Expanded = state.GetWorkspaceExpanded(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, "starting-survivors", true),
                ToggleAction = _factory.CreateGroupToggleAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, "starting-survivors", "Toggle Starting Survivors"),
                CreateAction = ScenarioInspectorItemFactory.Action(CharacterEditorCommand.AddStarting(), "Add Survivor", "Create and select a starting survivor.", true, missing, "S+"),
                StatusChips = new[]
                {
                    Chip(
                        "cast.starting.group.status",
                        missing ? "Required" : "Ready",
                        missing ? ScenarioAuthoringStatusTone.Error : ScenarioAuthoringStatusTone.Ready,
                        missing ? _factory.CreateWarningAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, string.Empty, "Starting survivor required") : null)
                },
                Rows = rows.ToArray()
            };
        }

        private ScenarioAuthoringNavigatorGroupViewModel BuildFutureGroup(
            ScenarioDefinition definition,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state)
        {
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            bool hasFutureArrivals = family != null
                && family.FutureSurvivors != null
                && family.FutureSurvivors.Count > 0;
            for (int i = 0; family != null && family.FutureSurvivors != null && i < family.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = family.FutureSurvivors[i];
                FamilyMemberConfig member = survivor != null ? survivor.Survivor : null;
                string title = ScenarioSurvivorWorkspaceDocumentBuilder.ResolveFutureName(survivor, i);
                if (!MatchesSearch(title, search))
                    continue;
                string entity = ScenarioCastWorkspaceActions.FutureEntityId(definition, i);
                bool warning = survivor == null || survivor.Arrival == null || ScenarioSurvivorWorkspaceDocumentBuilder.NeedsAttention(member);
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entity,
                    Title = title,
                    Subtitle = survivor != null
                        ? (survivor.AskToJoin ? "Asks to join · " : "Joins automatically · ") + ScenarioScheduleFormatter.Format(survivor.Arrival)
                        : "Arrival needs setup",
                    IconText = "FA",
                    Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                    StatusChips = new[]
                    {
                        Chip(
                            "cast.future.status." + i.ToString(CultureInfo.InvariantCulture),
                            warning ? "Needs attention" : "Scheduled",
                            warning ? ScenarioAuthoringStatusTone.Warning : ScenarioAuthoringStatusTone.Informational,
                            warning ? _factory.CreateWarningAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, entity, "Open " + title) : null)
                    },
                    SelectAction = _factory.CreateEntityAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, entity, "Select " + title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }

            return new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = "future-arrivals",
                Label = "Future Arrivals",
                IconText = "FA",
                Expanded = state.GetWorkspaceExpanded(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, "future-arrivals", true),
                ToggleAction = _factory.CreateGroupToggleAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, "future-arrivals", "Toggle Future Arrivals"),
                CreateAction = ScenarioInspectorItemFactory.Action(GameplayScheduleCommands.AddFutureSurvivor(), "Add Arrival", "Create and select a scheduled survivor arrival.", true, !hasFutureArrivals, "F+"),
                StatusChips = new[] { Chip("cast.future.group.status", "Optional", ScenarioAuthoringStatusTone.Neutral, null) },
                Rows = rows.ToArray()
            };
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildOverview(ScenarioDefinition definition)
        {
            bool hasStarting = ShelteredScenarioAuthoring.HasStartingSurvivor(definition);
            ScenarioAuthoringWorkspaceDocumentViewModel document = _factory.CreateDocument("cast.overview", "Cast");
            document.Subtitle = "Choose who starts in the shelter and schedule survivors who arrive later.";
            document.BackAction = _factory.CreateBackAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, "Back to Navigator");
            document.StatusChips = new[]
            {
                Chip(
                    "cast.overview.readiness",
                    hasStarting ? "Ready to playtest" : "Starting survivor required",
                    hasStarting ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Error,
                    hasStarting ? null : _factory.CreateWarningAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, string.Empty, "Starting survivor required"))
            };
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "cast_get_started",
                    Title = "CAST SETUP",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Text(hasStarting
                            ? "Select a survivor in the navigator to edit their portrait, identity, stats, traits, and starting conditions."
                            : "Add at least one starting survivor before playtest. Future arrivals are optional."),
                        ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(CharacterEditorCommand.AddStarting(), "Add Starting Survivor", "Create a new starting survivor.", true, !hasStarting, "S+")),
                        ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(GameplayScheduleCommands.AddFutureSurvivor(), "Add Future Arrival", "Create a scheduled survivor arrival.", true, false, "F+"))
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "live_world_reference",
                    Title = "Live World Reference",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.CastCardGrid,
                    StatusChips = new[] { Chip("cast.live.reference", "Read only", ScenarioAuthoringStatusTone.Informational, null) },
                    Items = BuildLiveSurvivorItems(definition)
                }
            };
            return document;
        }

        private static ScenarioAuthoringInspectorItem[] BuildLiveSurvivorItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            FamilyManager manager = FamilyManager.Instance;
            List<FamilyMember> members = manager != null ? manager.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member == null)
                    continue;
                items.Add(new ScenarioAuthoringInspectorItem
                {
                    Kind = ScenarioAuthoringInspectorItemKind.Property,
                    CastCard = BuildLiveSurvivorCard(member, IsLiveMemberInStartingCast(member, definition))
                });
            }
            if (items.Count == 0)
                items.Add(ScenarioInspectorItemFactory.Text("No live survivors are available from the current world."));
            return items.ToArray();
        }

        private static ScenarioCastCardViewModel BuildLiveSurvivorCard(FamilyMember member, bool inStartingCast)
        {
            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ScenarioCastPortraitResolver.ResolveColors(member, out hair, out skin, out shirt, out pants);
            int actorLocalId = TryGetFamilyMemberId(member);
            ScenarioAuthoringInspectorAction addAction = inStartingCast
                ? null
                : ScenarioInspectorItemFactory.Action(
                    CharacterEditorCommand.AddLiveStarting(actorLocalId),
                    "Add to cast",
                    "Copy this live survivor into the authored starting cast.",
                    actorLocalId > 0,
                    true,
                    "A+");
            string name = ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                member != null ? member.firstName : null,
                null,
                null,
                "Live Survivor").Text;
            return new ScenarioCastCardViewModel
            {
                Name = name,
                RoleLine = member != null ? (member.isChild ? "Child " : "Adult ") + (member.isMale ? "Male" : "Female") : "Live family unavailable",
                Status = (member != null && member.isAway ? "Away" : "Active") + (inStartingCast ? " / in starting cast" : string.Empty),
                CompactReference = true,
                PortraitSprite = ScenarioCastPortraitResolver.Resolve(member),
                PortraitTexture = ScenarioCastPortraitResolver.ResolveTexture(member),
                HairColor = hair,
                SkinColor = skin,
                ShirtColor = shirt,
                PantsColor = pants,
                Stats = new ScenarioCastStatViewModel[0],
                Traits = new string[0],
                PrimaryAction = addAction,
                SecondaryActions = new ScenarioAuthoringInspectorAction[0]
            };
        }

        private static bool IsLiveMemberInStartingCast(FamilyMember member, ScenarioDefinition definition)
        {
            if (member == null || definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null)
                return false;
            int actorLocalId = TryGetFamilyMemberId(member);
            string liveName = string.IsNullOrEmpty(member.firstName) ? string.Empty : member.firstName.Trim();
            if (liveName.Length == 0)
                return false;
            bool liveAdult = !member.isChild;
            ScenarioGender liveGender = member.isMale ? ScenarioGender.Male : ScenarioGender.Female;
            for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
            {
                FamilyMemberConfig authored = definition.FamilySetup.Members[i];
                ScenarioActorRef actorRef = authored != null ? authored.ActorRef : null;
                if (actorLocalId > 0 && actorRef != null
                    && ((string.Equals(actorRef.BindingType, "core.family", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(actorRef.BindingKey, actorLocalId.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
                        || (string.Equals(actorRef.Kind, "Player", StringComparison.OrdinalIgnoreCase) && actorRef.LocalId == actorLocalId && string.IsNullOrEmpty(actorRef.Domain))))
                    return true;
                if (authored == null || !string.Equals((authored.Name ?? string.Empty).Trim(), liveName, StringComparison.OrdinalIgnoreCase))
                    continue;
                FamilyMemberAppearanceConfig appearance = authored.Appearance;
                bool authoredAdult = appearance == null || !appearance.IsAdult.HasValue || appearance.IsAdult.Value;
                if ((authored.Gender == ScenarioGender.Any || authored.Gender == liveGender) && authoredAdult == liveAdult)
                    return true;
            }
            return false;
        }

        private static int TryGetFamilyMemberId(FamilyMember member)
        {
            if (member == null)
                return 0;
            try { return member.GetId(); }
            catch { return 0; }
        }

        private static bool MatchesSearch(string title, string search)
        {
            return string.IsNullOrEmpty(search) || (title ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ScenarioAuthoringStatusChipViewModel Chip(
            string id,
            string text,
            ScenarioAuthoringStatusTone tone,
            ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringStatusChipViewModel
            {
                Id = id,
                Text = text,
                Tone = tone,
                Action = action
            };
        }
    }
}
