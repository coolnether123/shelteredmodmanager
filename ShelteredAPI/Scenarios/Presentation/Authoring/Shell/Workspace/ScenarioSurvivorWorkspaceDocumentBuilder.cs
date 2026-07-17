using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Actors;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Actors;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioSurvivorWorkspaceDocumentBuilder
    {
        public ScenarioAuthoringWorkspaceDocumentViewModel BuildStartingDocument(
            ScenarioAuthoringWindowContentContext context,
            int index,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            if (family == null || family.Members == null || index < 0 || index >= family.Members.Count)
                return factory.CreateDocument("cast.starting.missing", "Starting Survivor");

            FamilyMemberConfig member = family.Members[index];
            string title = ResolveStartingName(member, index);
            string entity = ScenarioCastWorkspaceActions.StartingEntityId(definition, index);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("cast.starting." + index.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = "Starting survivor";
            document.BackAction = factory.CreateBackAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, "Back to Navigator");
            document.Breadcrumbs = BuildBreadcrumbs("Starting Survivors", title, factory);
            document.StatusChips = BuildDocumentStatus(member, entity, factory);
            document.HeaderActions = new[]
            {
                Action(ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix + "move." + index.ToString(CultureInfo.InvariantCulture) + ".-1", "Move Up", "Move this survivor earlier in the starting order.", index > 0, false, "UP", index > 0 ? null : "Already first."),
                Action(ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix + "move." + index.ToString(CultureInfo.InvariantCulture) + ".1", "Move Down", "Move this survivor later in the starting order.", index + 1 < family.Members.Count, false, "DN", index + 1 < family.Members.Count ? null : "Already last."),
                Action(ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix + "remove." + index.ToString(CultureInfo.InvariantCulture), "Remove", "Remove this survivor from the starting cast.", true, false, "RM", null)
            };
            document.Sections = BuildEditorSections(context != null ? context.State : null, member, index, ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix, null, "Starting", null);
            return document;
        }

        public ScenarioAuthoringWorkspaceDocumentViewModel BuildFutureDocument(
            ScenarioAuthoringWindowContentContext context,
            int index,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            FamilySetupDefinition family = definition != null ? definition.FamilySetup : null;
            if (family == null || family.FutureSurvivors == null || index < 0 || index >= family.FutureSurvivors.Count)
                return factory.CreateDocument("cast.future.missing", "Future Arrival");

            FutureSurvivorDefinition survivor = family.FutureSurvivors[index];
            FamilyMemberConfig member = survivor != null ? survivor.Survivor : null;
            string title = ResolveFutureName(survivor, index);
            string entity = ScenarioCastWorkspaceActions.FutureEntityId(definition, index);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("cast.future." + index.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = "Future arrival";
            document.BackAction = factory.CreateBackAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, "Back to Navigator");
            document.Breadcrumbs = BuildBreadcrumbs("Future Arrivals", title, factory);
            document.StatusChips = BuildFutureDocumentStatus(survivor, entity, factory);
            document.HeaderActions = new[]
            {
                Action(ScenarioAuthoringActionIds.ActionFutureSurvivorRemovePrefix + index.ToString(CultureInfo.InvariantCulture), "Remove", "Remove this future arrival.", true, false, "RM", null)
            };
            string arrivalSummary = survivor != null
                ? (survivor.AskToJoin ? "Ask to join · " : "Auto join · ") + ScenarioScheduleFormatter.Format(survivor.Arrival)
                : "Arrival needs setup";
            document.Sections = BuildEditorSections(context != null ? context.State : null, member, index, ScenarioAuthoringActionIds.ActionFutureSurvivorEditPrefix, arrivalSummary, "Future", survivor);
            return document;
        }

        internal static string ResolveStartingName(FamilyMemberConfig member, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                member != null ? member.Name : null,
                null,
                null,
                "Starting Survivor " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        internal static string ResolveFutureName(FutureSurvivorDefinition survivor, int index)
        {
            FamilyMemberConfig member = survivor != null ? survivor.Survivor : null;
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                member != null ? member.Name : null,
                null,
                survivor != null ? survivor.Id : null,
                "Future Survivor " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        internal static bool NeedsAttention(FamilyMemberConfig member)
        {
            if (member == null || string.IsNullOrEmpty(member.Name) || member.Name.Trim().Length == 0)
                return true;
            for (int i = 0; member.Stats != null && i < member.Stats.Count; i++)
            {
                StatOverride stat = member.Stats[i];
                if (stat != null && (stat.Value < ScenarioFamilyMemberFactory.StatMin || stat.Value > ScenarioFamilyMemberFactory.StatMax))
                    return true;
            }
            return false;
        }

        private static ScenarioAuthoringBreadcrumbViewModel[] BuildBreadcrumbs(
            string groupLabel,
            string title,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            return new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel
                {
                    Label = "Cast",
                    Action = factory.CreateBreadcrumbAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, string.Empty, "Cast")
                },
                new ScenarioAuthoringBreadcrumbViewModel { Label = groupLabel },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
        }

        private static ScenarioAuthoringStatusChipViewModel[] BuildDocumentStatus(
            FamilyMemberConfig member,
            string entity,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            bool warning = NeedsAttention(member);
            return new[]
            {
                Chip(
                    "cast.document.starting.status",
                    warning ? "Needs attention" : "Ready",
                    warning ? ScenarioAuthoringStatusTone.Warning : ScenarioAuthoringStatusTone.Ready,
                    warning ? factory.CreateWarningAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, entity, "Open survivor warnings") : null)
            };
        }

        private static ScenarioAuthoringStatusChipViewModel[] BuildFutureDocumentStatus(
            FutureSurvivorDefinition survivor,
            string entity,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            bool warning = survivor == null || survivor.Arrival == null || NeedsAttention(survivor.Survivor);
            return new[]
            {
                Chip(
                    "cast.document.future.status",
                    warning ? "Needs attention" : "Scheduled",
                    warning ? ScenarioAuthoringStatusTone.Warning : ScenarioAuthoringStatusTone.Informational,
                    warning ? factory.CreateWarningAction(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId, entity, "Open arrival warnings") : null)
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildEditorSections(
            ScenarioAuthoringState state,
            FamilyMemberConfig member,
            int index,
            string actionPrefix,
            string arrivalSummary,
            string status,
            FutureSurvivorDefinition futureSurvivor)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "survivor_editor_layout",
                Title = string.Empty,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.SurvivorEditor,
                SurvivorEditor = BuildSurvivorEditorViewModel(state, member, index, actionPrefix, arrivalSummary, status)
            });

            if (futureSurvivor != null)
                sections.Add(BuildArrivalSection(futureSurvivor, index));

            ScenarioAuthoringInspectorSection modFields = BuildSurvivorModFieldsSection(member, actionPrefix, index);
            if (modFields != null)
                sections.Add(modFields);
            return sections.ToArray();
        }

        private static ScenarioAuthoringInspectorSection BuildArrivalSection(FutureSurvivorDefinition survivor, int index)
        {
            string indexText = index.ToString(CultureInfo.InvariantCulture);
            return new ScenarioAuthoringInspectorSection
            {
                Id = "survivor_editor_schedule",
                Title = "Arrival",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Property("Schedule", ScenarioScheduleFormatter.Format(survivor.Arrival), "When this survivor arrives or asks to join."),
                    ScenarioInspectorItemFactory.Property("Join mode", survivor.AskToJoin ? "Ask to join" : "Join automatically"),
                    ScenarioInspectorItemFactory.ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorDayPrefix + indexText + ".1", "Day +", "Move this arrival one day later.", true, false, "D+", null)),
                    ScenarioInspectorItemFactory.ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorDayPrefix + indexText + ".-1", "Day -", "Move this arrival one day earlier.", true, false, "D-", null)),
                    ScenarioInspectorItemFactory.ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorHourPrefix + indexText + ".1", "Hour +", "Move this arrival one hour later.", true, false, "H+", null)),
                    ScenarioInspectorItemFactory.ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorHourPrefix + indexText + ".-1", "Hour -", "Move this arrival one hour earlier.", true, false, "H-", null)),
                    ScenarioInspectorItemFactory.ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorToggleAskPrefix + indexText, "Change Join Mode", "Switch between asking to join and joining automatically.", true, survivor.AskToJoin, "AJ", null))
                }
            };
        }

        private static ScenarioSurvivorEditorViewModel BuildSurvivorEditorViewModel(
            ScenarioAuthoringState state,
            FamilyMemberConfig member,
            int index,
            string actionPrefix,
            string arrivalSummary,
            string status)
        {
            if (member == null)
                member = new FamilyMemberConfig();
            string indexedPrefix = actionPrefix + index.ToString(CultureInfo.InvariantCulture) + ".";
            string copyReason;
            bool canCopySelected = CanCopySelectedFamilyMember(state, out copyReason);
            string strengthTrait = FindTrait(member, "Strength:");
            string weaknessTrait = FindTrait(member, "Weakness:");
            string displayName = ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(member.Name, null, null, "Unnamed Survivor").Text;
            return new ScenarioSurvivorEditorViewModel
            {
                Portrait = BuildAuthoredSurvivorCard(member, index, arrivalSummary, status),
                NameAction = Action(indexedPrefix + "name", displayName, "Cycle this survivor's name preset.", true, false, "NM", displayName),
                GenderAction = Action(indexedPrefix + "gender", "Gender: " + member.Gender.ToString(), "Cycle Any, Female, and Male.", true, false, "GN", member.Gender.ToString()),
                BodyAction = Action(indexedPrefix + "adult", FormatAgeBand(member), "Toggle the vanilla adult or child body mesh.", true, false, "BD", FormatBody(member, true)),
                TextureRows = BuildSurvivorTextureRows(member, indexedPrefix),
                ColorRows = BuildSurvivorColorRows(member, indexedPrefix),
                StatRows = BuildSurvivorStatRows(member, indexedPrefix),
                SkillsLimitationText = "Skills can't be authored yet - the game doesn't expose a stable way to save them. Strengths and weaknesses below DO work.",
                TraitRows = BuildSurvivorTraitRows(member, indexedPrefix, strengthTrait, weaknessTrait),
                ConditionRows = BuildSurvivorConditionRows(member, indexedPrefix),
                UtilityDisclosureLines = new[]
                {
                    ScenarioSurvivorAuthoringOperations.RandomizeDisclosure,
                    ScenarioSurvivorAuthoringOperations.DuplicateDisclosure
                },
                UtilityActions = new[]
                {
                    Action(indexedPrefix + "randomize_person", "Randomize", ScenarioSurvivorAuthoringOperations.RandomizeDisclosure, true, false, "RND", null),
                    Action(indexedPrefix + "duplicate_person", "Duplicate Person", ScenarioSurvivorAuthoringOperations.DuplicateDisclosure, true, false, "DUP", null),
                    Action(indexedPrefix + "randomize_look", "Randomize Look", "Randomize head, top, bottom, and color choices.", true, false, "RLK", FormatAppearance(member)),
                    Action(indexedPrefix + "copy_identity", "Copy Selected Identity", "Copy identity and appearance from the selected live family member.", canCopySelected, false, "ID", canCopySelected ? "Selected live family member" : null, copyReason),
                    Action(indexedPrefix + "copy_look", "Copy Selected Look", "Copy appearance from the selected live family member.", canCopySelected, false, "LK", FormatAppearance(member), copyReason),
                    Action(indexedPrefix + "clear_look", "Clear Look", "Clear stored mesh, texture, and color overrides.", true, false, "CL", FormatAppearance(member))
                },
                CloseActions = new ScenarioAuthoringInspectorAction[0]
            };
        }

        private static ScenarioCastCardViewModel BuildAuthoredSurvivorCard(
            FamilyMemberConfig member,
            int index,
            string arrivalSummary,
            string status)
        {
            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ActorProfileComponent profile = ResolveActorProfile(member != null ? member.ActorRef : null);
            ScenarioCastPortraitResolver.ResolveColors(member, out hair, out skin, out shirt, out pants);
            if (profile != null && (member == null || member.Appearance == null || string.IsNullOrEmpty(member.Appearance.MeshId)))
                ScenarioCastPortraitResolver.ResolveColors(profile, out hair, out skin, out shirt, out pants);
            Sprite portraitSprite = ScenarioCastPortraitResolver.Resolve(member);
            Texture2D portraitTexture = ScenarioCastPortraitResolver.ResolveTexture(member);
            if (portraitSprite == null && profile != null)
                portraitSprite = ScenarioCastPortraitResolver.Resolve(profile);
            if (portraitTexture == null && profile != null)
                portraitTexture = ScenarioCastPortraitResolver.ResolveTexture(profile);
            return new ScenarioCastCardViewModel
            {
                Name = ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(member != null ? member.Name : null, null, null, "Survivor " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text,
                RoleLine = FormatAgeBand(member) + " " + (member != null ? member.Gender.ToString() : ScenarioGender.Any.ToString()),
                Status = BuildActorStatus(member != null ? member.ActorRef : null, status),
                ArrivalSummary = arrivalSummary,
                PortraitSprite = portraitSprite,
                PortraitTexture = portraitTexture,
                HairColor = hair,
                SkinColor = skin,
                ShirtColor = shirt,
                PantsColor = pants,
                Stats = BuildAuthoredStats(member),
                Traits = BuildAuthoredTraits(member),
                SecondaryActions = new ScenarioAuthoringInspectorAction[0]
            };
        }

        private static ScenarioCastStatViewModel[] BuildAuthoredStats(FamilyMemberConfig member)
        {
            string[] statIds = ScenarioFamilyMemberFactory.StatIds;
            ScenarioCastStatViewModel[] stats = new ScenarioCastStatViewModel[statIds.Length];
            for (int i = 0; i < statIds.Length; i++)
            {
                string statId = statIds[i];
                stats[i] = new ScenarioCastStatViewModel
                {
                    Id = statId,
                    Label = statId.Substring(0, 3),
                    Value = ClampStatDisplay(FindStatValue(member, statId, 5)),
                    Max = 20
                };
            }
            return stats;
        }

        private static string[] BuildAuthoredTraits(FamilyMemberConfig member)
        {
            List<string> traits = new List<string>();
            for (int i = 0; member != null && member.Traits != null && i < member.Traits.Count; i++)
            {
                string trait = member.Traits[i];
                if (string.IsNullOrEmpty(trait))
                    continue;
                int separator = trait.IndexOf(':');
                traits.Add(separator >= 0 && separator < trait.Length - 1 ? FormatTraitName(trait.Substring(separator + 1)) : FormatTraitName(trait));
            }
            return traits.Count == 0 ? new[] { "No traits selected" } : traits.ToArray();
        }

        private static ScenarioSurvivorTextureRowViewModel[] BuildSurvivorTextureRows(FamilyMemberConfig member, string indexedPrefix)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            return new[]
            {
                SurvivorTextureRow("Head", indexedPrefix, "head", ScenarioCharacterTexturePart.Head, appearance),
                SurvivorTextureRow("Top", indexedPrefix, "torso", ScenarioCharacterTexturePart.Torso, appearance),
                SurvivorTextureRow("Bottom", indexedPrefix, "legs", ScenarioCharacterTexturePart.Legs, appearance)
            };
        }

        private static ScenarioSurvivorTextureRowViewModel SurvivorTextureRow(
            string label,
            string indexedPrefix,
            string commandPart,
            ScenarioCharacterTexturePart part,
            FamilyMemberAppearanceConfig appearance)
        {
            return new ScenarioSurvivorTextureRowViewModel
            {
                Label = label,
                Detail = FormatTexture(appearance, part),
                PreviousAction = Action(indexedPrefix + "texture." + commandPart + ".-1", "<", "Switch to the previous vanilla " + label.ToLowerInvariant() + " sprite.", true, false, "<", null),
                NextAction = Action(indexedPrefix + "texture." + commandPart + ".1", ">", "Switch to the next vanilla " + label.ToLowerInvariant() + " sprite.", true, false, ">", null)
            };
        }

        private static ScenarioSurvivorColorRowViewModel[] BuildSurvivorColorRows(FamilyMemberConfig member, string indexedPrefix)
        {
            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ScenarioCastPortraitResolver.ResolveColors(member, out hair, out skin, out shirt, out pants);
            return new[]
            {
                SurvivorColorRow("Hair", "hair", indexedPrefix, hair),
                SurvivorColorRow("Skin", "skin", indexedPrefix, skin),
                SurvivorColorRow("Shirt", "shirt", indexedPrefix, shirt),
                SurvivorColorRow("Pants", "pants", indexedPrefix, pants)
            };
        }

        private static ScenarioSurvivorColorRowViewModel SurvivorColorRow(string label, string commandPart, string indexedPrefix, Color color)
        {
            return new ScenarioSurvivorColorRowViewModel
            {
                Channel = commandPart,
                Label = label,
                Hex = ScenarioCharacterAppearanceService.ToColorHex(color),
                Color = color,
                PreviousAction = Action(indexedPrefix + "color." + commandPart + ".-1", "<", "Switch to the previous vanilla " + label.ToLowerInvariant() + " color.", true, false, "<", null),
                NextAction = Action(indexedPrefix + "color." + commandPart + ".1", ">", "Switch to the next vanilla " + label.ToLowerInvariant() + " color.", true, false, ">", null),
                OpenColorPickerActionId = ScenarioAuthoringLocalActionIds.ActionSurvivorOpenColorPickerPrefix + commandPart,
                ApplyColorActionPrefix = indexedPrefix + ScenarioAuthoringLocalActionIds.ActionSurvivorApplyColorCommandPrefix + commandPart + "."
            };
        }

        private static ScenarioSurvivorStatRowViewModel[] BuildSurvivorStatRows(FamilyMemberConfig member, string indexedPrefix)
        {
            string[] statIds = ScenarioFamilyMemberFactory.StatIds;
            ScenarioSurvivorStatRowViewModel[] rows = new ScenarioSurvivorStatRowViewModel[statIds.Length];
            for (int i = 0; i < statIds.Length; i++)
            {
                string statId = statIds[i];
                int rawValue = FindStatValue(member, statId, 5);
                int displayValue = ClampStatDisplay(rawValue);
                string detail = rawValue == displayValue ? null : "Stored value is outside 1-20; showing " + displayValue.ToString(CultureInfo.InvariantCulture) + ".";
                bool canIncrease = displayValue < ScenarioFamilyMemberFactory.StatMax;
                bool canDecrease = displayValue > ScenarioFamilyMemberFactory.StatMin;
                rows[i] = new ScenarioSurvivorStatRowViewModel
                {
                    Id = statId,
                    Label = statId,
                    Value = displayValue,
                    Min = ScenarioFamilyMemberFactory.StatMin,
                    Max = ScenarioFamilyMemberFactory.StatMax,
                    RangeText = "1-20",
                    DecreaseAction = Action(indexedPrefix + "stat." + statId + ".-1", "-", "Decrease " + statId + ".", canDecrease, false, "-", displayValue.ToString(CultureInfo.InvariantCulture), canDecrease ? detail : "Stats are limited to 1-20."),
                    IncreaseAction = Action(indexedPrefix + "stat." + statId + ".1", "+", "Increase " + statId + ".", canIncrease, false, "+", displayValue.ToString(CultureInfo.InvariantCulture), canIncrease ? detail : "Stats are limited to 1-20."),
                    TextAction = Action(indexedPrefix + "stat_set." + statId + ".", displayValue.ToString(CultureInfo.InvariantCulture), "Enter a " + statId + " value from 1 to 20.", true, false, "TX", displayValue.ToString(CultureInfo.InvariantCulture))
                };
            }
            return rows;
        }

        private static ScenarioSurvivorTraitRowViewModel[] BuildSurvivorTraitRows(FamilyMemberConfig member, string indexedPrefix, string strengthTrait, string weaknessTrait)
        {
            return new[]
            {
                SurvivorTraitRow(member, indexedPrefix, true, strengthTrait),
                SurvivorTraitRow(member, indexedPrefix, false, weaknessTrait)
            };
        }

        private static ScenarioSurvivorTraitRowViewModel SurvivorTraitRow(FamilyMemberConfig member, string indexedPrefix, bool strength, string value)
        {
            string kind = strength ? "strength" : "weakness";
            return new ScenarioSurvivorTraitRowViewModel
            {
                Kind = kind,
                Label = strength ? "Strength Trait" : "Weakness Trait",
                Value = value,
                PickerKey = indexedPrefix + "trait." + kind,
                PreviousAction = Action(indexedPrefix + kind + "_trait.-1", "<", "Switch to the previous valid " + kind + " trait.", true, false, "<", null),
                NextAction = Action(indexedPrefix + kind + "_trait.1", ">", "Switch to the next valid " + kind + " trait.", true, false, ">", null),
                PickerAction = Action(indexedPrefix + "trait_picker." + kind, value == "<none>" ? "Choose trait" : FormatTraitName(value), "Pick a " + kind + " trait with its vanilla effect.", true, false, strength ? "ST" : "WT", value),
                Options = BuildTraitOptions(member, indexedPrefix, strength)
            };
        }

        private static ScenarioSurvivorTraitOptionViewModel[] BuildTraitOptions(FamilyMemberConfig member, string indexedPrefix, bool strength)
        {
            Array values = Enum.GetValues(strength ? typeof(Traits.Strength) : typeof(Traits.Weakness));
            List<ScenarioSurvivorTraitOptionViewModel> options = new List<ScenarioSurvivorTraitOptionViewModel>();
            for (int i = 0; values != null && i < values.Length; i++)
            {
                object value = values.GetValue(i);
                if (value == null || string.Equals(value.ToString(), "Max", StringComparison.OrdinalIgnoreCase))
                    continue;
                bool conflicts = ScenarioSurvivorTraitConflictRules.ConflictsWithSelection(member, strength, value);
                string id = value.ToString();
                string label = FormatTraitName(id);
                string description = GetTraitDescription(strength, id);
                options.Add(new ScenarioSurvivorTraitOptionViewModel
                {
                    Id = id,
                    Label = label,
                    Description = description,
                    SelectAction = Action(indexedPrefix + "trait." + (strength ? "strength" : "weakness") + "." + id, label, description, !conflicts, false, strength ? "ST" : "WT", id, conflicts ? "Blocked by the paired trait." : null)
                });
            }
            return options.ToArray();
        }

        private static ScenarioSurvivorConditionRowViewModel[] BuildSurvivorConditionRows(FamilyMemberConfig member, string indexedPrefix)
        {
            string[] conditionIds = ScenarioFamilyMemberFactory.ConditionIds;
            ScenarioSurvivorConditionRowViewModel[] rows = new ScenarioSurvivorConditionRowViewModel[conditionIds.Length];
            for (int i = 0; i < conditionIds.Length; i++)
            {
                string id = conditionIds[i];
                int rawValue;
                int value = ScenarioFamilyMemberFactory.TryGetConditionValue(member, id, out rawValue) ? rawValue : 0;
                bool canDecrease = value > ScenarioFamilyMemberFactory.ConditionMin;
                bool canIncrease = value < ScenarioFamilyMemberFactory.ConditionMax;
                rows[i] = new ScenarioSurvivorConditionRowViewModel
                {
                    Id = id,
                    Label = FormatConditionLabel(id),
                    Value = value,
                    Min = ScenarioFamilyMemberFactory.ConditionMin,
                    Max = ScenarioFamilyMemberFactory.ConditionMax,
                    RangeText = "0-100",
                    HelpText = GetConditionHelp(id),
                    DecreaseAction = Action(indexedPrefix + "condition." + id + ".-5", "-", "Decrease starting " + id.ToLowerInvariant() + ".", canDecrease, false, "-", value.ToString(CultureInfo.InvariantCulture), canDecrease ? null : "Conditions are limited to 0-100."),
                    IncreaseAction = Action(indexedPrefix + "condition." + id + ".5", "+", "Increase starting " + id.ToLowerInvariant() + ".", canIncrease, false, "+", value.ToString(CultureInfo.InvariantCulture), canIncrease ? null : "Conditions are limited to 0-100."),
                    TextAction = Action(indexedPrefix + "condition_set." + id + ".", value.ToString(CultureInfo.InvariantCulture), "Enter a starting " + id.ToLowerInvariant() + " value from 0 to 100.", true, false, "TX", value.ToString(CultureInfo.InvariantCulture))
                };
            }
            return rows;
        }

        private static ScenarioAuthoringInspectorSection BuildSurvivorModFieldsSection(FamilyMemberConfig member, string actionPrefix, int index)
        {
            List<ScenarioSurvivorModFieldRowViewModel> rows = new List<ScenarioSurvivorModFieldRowViewModel>();
            string indexedPrefix = actionPrefix + index.ToString(CultureInfo.InvariantCulture) + ".";
            IList<ActorAuthoringFieldDefinition> fields = ScenarioActorAuthoringFieldStore.GetApplicableFields(member);
            for (int i = 0; fields != null && i < fields.Count; i++)
            {
                ScenarioSurvivorModFieldRowViewModel row = BuildSurvivorModFieldRow(member, fields[i], indexedPrefix);
                if (row != null)
                    rows.Add(row);
            }
            AddMissingModFieldNotices(rows, member, fields);
            if (rows.Count == 0)
                return null;
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (HasMissingModFieldNotice(rows))
                items.Add(ScenarioInspectorItemFactory.ActionItem(Action(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicModGating, "Resolve Mod Gating", "Open guidance for missing actor-authoring providers.", true, false, "MOD", null)));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "survivor_mod_fields",
                Title = "Mod Fields",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ModFieldList,
                ModFieldRows = rows.ToArray(),
                Items = items.ToArray()
            };
        }

        private static ScenarioSurvivorModFieldRowViewModel BuildSurvivorModFieldRow(FamilyMemberConfig member, ActorAuthoringFieldDefinition field, string indexedPrefix)
        {
            if (field == null)
                return null;
            string value = ScenarioActorAuthoringFieldStore.NormalizeValue(field, ScenarioActorAuthoringFieldStore.GetValue(member, field));
            string token = ScenarioAuthoringActionCodec.EncodeToken(ScenarioActorAuthoringFieldStore.BuildFieldToken(field));
            string commandPrefix = indexedPrefix + ScenarioActorAuthoringFieldStore.FieldCommandPrefix;
            string help = !string.IsNullOrEmpty(field.HelpText) ? field.HelpText : "Provided by " + field.RequiredModId + ".";
            if (field.ValueType == ActorAuthoringFieldValueType.Bool)
            {
                bool enabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return new ScenarioSurvivorModFieldRowViewModel { Kind = ScenarioSurvivorModFieldControlKind.Toggle, Label = field.Label, ValueText = enabled ? "On" : "Off", HelpText = help, ToggleAction = Action(commandPrefix + "toggle." + token, enabled ? "On" : "Off", help, true, enabled, enabled ? "ON" : "OFF", null) };
            }
            if (field.ValueType == ActorAuthoringFieldValueType.Int || field.ValueType == ActorAuthoringFieldValueType.Float)
                return new ScenarioSurvivorModFieldRowViewModel { Kind = ScenarioSurvivorModFieldControlKind.Stepper, Label = field.Label, ValueText = value, HelpText = help, DecreaseAction = Action(commandPrefix + "step." + token + ".-1", "-", "Decrease " + field.Label + ".", true, false, "-", value), IncreaseAction = Action(commandPrefix + "step." + token + ".1", "+", "Increase " + field.Label + ".", true, false, "+", value) };
            if (field.ValueType == ActorAuthoringFieldValueType.String)
                return new ScenarioSurvivorModFieldRowViewModel { Kind = ScenarioSurvivorModFieldControlKind.Text, Label = field.Label, ValueText = value, HelpText = help, TextAction = Action(commandPrefix + "text." + token + ".", field.Label, help, true, false, "TXT", null) };
            if (field.ValueType == ActorAuthoringFieldValueType.StringEnum)
                return new ScenarioSurvivorModFieldRowViewModel { Kind = ScenarioSurvivorModFieldControlKind.Enum, Label = field.Label, ValueText = value, HelpText = help, CycleAction = Action(commandPrefix + "enum." + token, value, help, true, false, "EN", null) };
            if (field.ValueType == ActorAuthoringFieldValueType.Color)
            {
                Color color;
                if (!ScenarioCharacterAppearanceService.TryParseColorHex(value, out color))
                    color = Color.white;
                return new ScenarioSurvivorModFieldRowViewModel
                {
                    Kind = ScenarioSurvivorModFieldControlKind.Color,
                    Label = field.Label,
                    ValueText = value,
                    HelpText = help,
                    ColorRow = new ScenarioSurvivorColorRowViewModel
                    {
                        Channel = "mod:" + token,
                        Label = field.Label,
                        Hex = value,
                        Color = color,
                        OpenColorPickerActionId = commandPrefix + "open_color." + token,
                        ApplyColorActionPrefix = commandPrefix + "color." + token + "."
                    }
                };
            }
            return null;
        }

        private static void AddMissingModFieldNotices(List<ScenarioSurvivorModFieldRowViewModel> rows, FamilyMemberConfig member, IList<ActorAuthoringFieldDefinition> registeredFields)
        {
            for (int i = 0; member != null && member.ActorComponents != null && i < member.ActorComponents.Count; i++)
            {
                ScenarioActorComponentDefinition component = member.ActorComponents[i];
                if (component == null || string.IsNullOrEmpty(component.ComponentId) || string.IsNullOrEmpty(component.OwnerModId)
                    || ScenarioActorAuthoringFieldStore.IsProviderModLoaded(component.OwnerModId) || HasRegisteredFieldForComponent(registeredFields, component.ComponentId))
                    continue;
                rows.Add(new ScenarioSurvivorModFieldRowViewModel
                {
                    Kind = ScenarioSurvivorModFieldControlKind.Notice,
                    Label = "Missing provider: " + component.OwnerModId,
                    HelpText = "Payload for " + component.ComponentId + " is preserved but hidden until that mod/API is registered.",
                    Badge = "MOD",
                    Emphasized = true
                });
            }
        }

        private static bool HasRegisteredFieldForComponent(IList<ActorAuthoringFieldDefinition> fields, string componentId)
        {
            for (int i = 0; fields != null && i < fields.Count; i++)
                if (fields[i] != null && string.Equals(fields[i].ComponentId, componentId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasMissingModFieldNotice(List<ScenarioSurvivorModFieldRowViewModel> rows)
        {
            for (int i = 0; rows != null && i < rows.Count; i++)
                if (rows[i] != null && rows[i].Kind == ScenarioSurvivorModFieldControlKind.Notice) return true;
            return false;
        }

        private static bool CanCopySelectedFamilyMember(ScenarioAuthoringState state, out string reason)
        {
            reason = null;
            ScenarioAuthoringTarget target = state != null ? state.SelectedTarget : null;
            if (target == null || target.RuntimeObject == null)
            {
                reason = "No live family member is selected.";
                return false;
            }
            GameObject gameObject = target.RuntimeObject as GameObject;
            Component component = target.RuntimeObject as Component;
            if (gameObject == null && component != null)
                gameObject = component.gameObject;
            if (gameObject == null || gameObject.GetComponentInParent<FamilyMember>() == null)
            {
                reason = "Selected target is not a live family member.";
                return false;
            }
            return true;
        }

        private static ActorProfileComponent ResolveActorProfile(ScenarioActorRef actorRef)
        {
            if (actorRef == null || ShelteredActors.Instance == null)
                return null;
            IActorRecord record = null;
            ActorId boundId;
            if (!string.IsNullOrEmpty(actorRef.BindingType) && !string.IsNullOrEmpty(actorRef.BindingKey)
                && ShelteredActors.Instance.TryResolve(actorRef.BindingType, actorRef.BindingKey, out boundId) && boundId != null)
                ShelteredActors.Instance.TryGet(boundId, out record);
            if (record == null && !string.IsNullOrEmpty(actorRef.Kind))
            {
                try
                {
                    ActorId exactId = new ActorId((ActorKind)Enum.Parse(typeof(ActorKind), actorRef.Kind, true), actorRef.LocalId, actorRef.Domain ?? string.Empty);
                    ShelteredActors.Instance.TryGet(exactId, out record);
                }
                catch { }
            }
            if (record == null || record.Id == null)
                return null;
            ActorProfileComponent profile;
            return ShelteredActors.Instance.TryGet<ActorProfileComponent>(record.Id, out profile) ? profile : null;
        }

        private static string BuildActorStatus(ScenarioActorRef actorRef, string fallback)
        {
            if (actorRef == null || ShelteredActors.Instance == null)
                return fallback;
            IActorRecord record = null;
            ActorId boundId;
            if (!string.IsNullOrEmpty(actorRef.BindingType) && !string.IsNullOrEmpty(actorRef.BindingKey)
                && ShelteredActors.Instance.TryResolve(actorRef.BindingType, actorRef.BindingKey, out boundId) && boundId != null)
                ShelteredActors.Instance.TryGet(boundId, out record);
            if (record == null)
                return fallback;
            if (record.PresenceState == ActorPresenceState.InShelter) return "Active";
            if (record.PresenceState == ActorPresenceState.Expedition) return "Away";
            if (record.PresenceState == ActorPresenceState.Offscreen) return string.Equals(fallback, "Future", StringComparison.OrdinalIgnoreCase) ? "Future" : "Offscreen";
            return fallback;
        }

        private static int FindStatValue(FamilyMemberConfig member, string statId, int fallback)
        {
            for (int i = 0; member != null && member.Stats != null && i < member.Stats.Count; i++)
                if (member.Stats[i] != null && string.Equals(member.Stats[i].StatId, statId, StringComparison.OrdinalIgnoreCase)) return member.Stats[i].Value;
            return fallback;
        }

        private static int ClampStatDisplay(int value)
        {
            return Mathf.Clamp(value, ScenarioFamilyMemberFactory.StatMin, ScenarioFamilyMemberFactory.StatMax);
        }

        private static string FindTrait(FamilyMemberConfig member, string prefix)
        {
            for (int i = 0; member != null && member.Traits != null && i < member.Traits.Count; i++)
            {
                string trait = member.Traits[i];
                if (!string.IsNullOrEmpty(trait) && trait.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return trait.Substring(prefix.Length);
            }
            return "<none>";
        }

        private static string FormatTraitName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1])) builder.Append(' ');
                builder.Append(value[i]);
            }
            return builder.ToString();
        }

        private static string GetTraitDescription(bool strength, string id)
        {
            if (strength)
            {
                if (string.Equals(id, "SmallEater", StringComparison.OrdinalIgnoreCase)) return "Food restores more hunger.";
                if (string.Equals(id, "Courageous", StringComparison.OrdinalIgnoreCase)) return "Improves combat reliability and subdue chance.";
                if (string.Equals(id, "DeepSleeper", StringComparison.OrdinalIgnoreCase)) return "Recovers fatigue faster while sleeping.";
                if (string.Equals(id, "Proactive", StringComparison.OrdinalIgnoreCase)) return "Moves faster in shelter and on expeditions.";
                if (string.Equals(id, "HandsOn", StringComparison.OrdinalIgnoreCase)) return "Crafts and repairs faster.";
            }
            else
            {
                if (string.Equals(id, "BigEater", StringComparison.OrdinalIgnoreCase)) return "Food restores less hunger.";
                if (string.Equals(id, "Cowardice", StringComparison.OrdinalIgnoreCase)) return "Can cower or skip turns under combat pressure.";
                if (string.Equals(id, "LightSleeper", StringComparison.OrdinalIgnoreCase)) return "Recovers fatigue more slowly while sleeping.";
                if (string.Equals(id, "Lazy", StringComparison.OrdinalIgnoreCase)) return "Moves slower in shelter and on expeditions.";
                if (string.Equals(id, "HandsOff", StringComparison.OrdinalIgnoreCase)) return "Crafts and repairs more slowly.";
            }
            return "Vanilla trait effect.";
        }

        private static string FormatConditionLabel(string id)
        {
            if (string.Equals(id, "Fatigue", StringComparison.OrdinalIgnoreCase)) return "Tiredness";
            if (string.Equals(id, "Dirtiness", StringComparison.OrdinalIgnoreCase)) return "Hygiene";
            return FormatTraitName(id);
        }

        private static string GetConditionHelp(string id)
        {
            if (string.Equals(id, "Hunger", StringComparison.OrdinalIgnoreCase)) return "0 is fed; 100 is starving.";
            if (string.Equals(id, "Thirst", StringComparison.OrdinalIgnoreCase)) return "0 is hydrated; 100 is dehydrated.";
            if (string.Equals(id, "Fatigue", StringComparison.OrdinalIgnoreCase)) return "0 is rested; 100 is exhausted.";
            if (string.Equals(id, "Dirtiness", StringComparison.OrdinalIgnoreCase)) return "0 is clean; 100 is filthy.";
            if (string.Equals(id, "Toilet", StringComparison.OrdinalIgnoreCase)) return "0 is relieved; 100 urgently needs the toilet.";
            if (string.Equals(id, "Stress", StringComparison.OrdinalIgnoreCase)) return "0 is calm; 100 is maximum stress.";
            return "Vanilla starting condition.";
        }

        private static string FormatBody(FamilyMemberConfig member, bool advanced)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            bool adult = appearance == null || !appearance.IsAdult.HasValue || appearance.IsAdult.Value;
            string mesh = appearance != null && !string.IsNullOrEmpty(appearance.MeshId) ? appearance.MeshId : "<auto>";
            string label = adult ? "Adult Body" : "Child Body";
            if (string.Equals(mesh, "man", StringComparison.OrdinalIgnoreCase)) label = "Adult Male";
            else if (string.Equals(mesh, "woman", StringComparison.OrdinalIgnoreCase)) label = "Adult Female";
            else if (string.Equals(mesh, "boy", StringComparison.OrdinalIgnoreCase)) label = "Child Male";
            else if (string.Equals(mesh, "girl", StringComparison.OrdinalIgnoreCase)) label = "Child Female";
            return advanced ? label + " (" + mesh + ")" : label;
        }

        private static string FormatAgeBand(FamilyMemberConfig member)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            return appearance == null || !appearance.IsAdult.HasValue || appearance.IsAdult.Value ? "Adult" : "Child";
        }

        private static string FormatAppearance(FamilyMemberConfig member)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            if (appearance == null)
                return "default";
            int count = 0;
            if (!string.IsNullOrEmpty(appearance.MeshId) || appearance.IsAdult.HasValue) count++;
            if (!string.IsNullOrEmpty(appearance.HeadTextureId) || !string.IsNullOrEmpty(appearance.HeadTexturePath)) count++;
            if (!string.IsNullOrEmpty(appearance.TorsoTextureId) || !string.IsNullOrEmpty(appearance.TorsoTexturePath)) count++;
            if (!string.IsNullOrEmpty(appearance.LegTextureId) || !string.IsNullOrEmpty(appearance.LegTexturePath)) count++;
            if (!string.IsNullOrEmpty(appearance.HairColorHex)) count++;
            if (!string.IsNullOrEmpty(appearance.SkinColorHex)) count++;
            if (!string.IsNullOrEmpty(appearance.ShirtColorHex)) count++;
            if (!string.IsNullOrEmpty(appearance.PantsColorHex)) count++;
            return count == 0 ? "default" : count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " custom choice" : " custom choices");
        }

        private static string FormatTexture(FamilyMemberAppearanceConfig appearance, ScenarioCharacterTexturePart part)
        {
            if (appearance == null)
                return "default";
            if (part == ScenarioCharacterTexturePart.Head) return !string.IsNullOrEmpty(appearance.HeadTextureId) ? appearance.HeadTextureId : (!string.IsNullOrEmpty(appearance.HeadTexturePath) ? appearance.HeadTexturePath : "default");
            if (part == ScenarioCharacterTexturePart.Torso) return !string.IsNullOrEmpty(appearance.TorsoTextureId) ? appearance.TorsoTextureId : (!string.IsNullOrEmpty(appearance.TorsoTexturePath) ? appearance.TorsoTexturePath : "default");
            return !string.IsNullOrEmpty(appearance.LegTextureId) ? appearance.LegTextureId : (!string.IsNullOrEmpty(appearance.LegTexturePath) ? appearance.LegTexturePath : "default");
        }

        private static ScenarioAuthoringStatusChipViewModel Chip(string id, string text, ScenarioAuthoringStatusTone tone, ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringStatusChipViewModel { Id = id, Text = text, Tone = tone, Action = action };
        }

        private static ScenarioAuthoringInspectorAction Action(
            string id,
            string label,
            string hint,
            bool enabled,
            bool emphasized,
            string icon,
            string detail,
            string disabledReason = null)
        {
            return ScenarioInspectorItemFactory.Action(id, label, hint, enabled, emphasized, icon, detail, null, null, disabledReason);
        }
    }
}
