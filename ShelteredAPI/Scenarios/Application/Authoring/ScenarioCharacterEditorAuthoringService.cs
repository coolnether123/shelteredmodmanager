using System;
using System.Collections.Generic;
using ModAPI.Actors;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioCharacterEditorAuthoringService
    {
        private static readonly string[] NamePresets = new[]
        {
            "Alex",
            "Sam",
            "Jordan",
            "Morgan",
            "Casey",
            "Riley",
            "Taylor",
            "Quinn"
        };

        private readonly ScenarioCharacterAppearanceService _appearanceService;
        private readonly ScenarioActorResolver _actorResolver;
        private readonly ScenarioSurvivorAuthoringOperations _survivorOperations;

        public ScenarioCharacterEditorAuthoringService(
            ScenarioCharacterAppearanceService appearanceService,
            ScenarioActorResolver actorResolver)
        {
            _appearanceService = appearanceService;
            _actorResolver = actorResolver;
            _survivorOperations = new ScenarioSurvivorAuthoringOperations(appearanceService);
        }

        public bool TryHandleAction(ScenarioEditorSession session, ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionStartingSurvivorAdd, StringComparison.Ordinal))
                return AddStartingSurvivor(session, state, out message);

            if (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringActionIds.ActionLiveSurvivorAddToStartingPrefix, StringComparison.Ordinal))
                return AddLiveSurvivorToStarting(session, state, actionId.Substring(ScenarioAuthoringActionIds.ActionLiveSurvivorAddToStartingPrefix.Length), out message);

            if (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringLocalActionIds.ActionSurvivorOpenColorPickerPrefix, StringComparison.Ordinal))
            {
                string channel = actionId.Substring(ScenarioAuthoringLocalActionIds.ActionSurvivorOpenColorPickerPrefix.Length);
                if (!string.Equals(channel, "hair", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(channel, "skin", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(channel, "shirt", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(channel, "pants", StringComparison.OrdinalIgnoreCase))
                {
                    message = "Survivor color picker channel was not recognized.";
                    return true;
                }

                state.SurvivorColorPickerChannel = channel.ToLowerInvariant();
                state.SurvivorColorPickerRequestId++;
                message = "Opened survivor color picker.";
                return true;
            }

            if (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringLocalActionIds.ActionStartingSurvivorEditorOpenPrefix, StringComparison.Ordinal))
            {
                FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
                int index;
                if (!int.TryParse(actionId.Substring(ScenarioAuthoringLocalActionIds.ActionStartingSurvivorEditorOpenPrefix.Length), out index)
                    || index < 0
                    || index >= family.Members.Count)
                {
                    message = "Starting survivor editor action was out of range.";
                    return true;
                }

                FocusSurvivorEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindStartingSurvivor, index, false);
                state.FocusedSurvivorOriginal = ScenarioSurvivorAuthoringOperations.CloneMember(family.Members[index]);
                message = "Opened focused survivor editor.";
                return true;
            }

            if (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringLocalActionIds.ActionFutureSurvivorEditorOpenPrefix, StringComparison.Ordinal))
            {
                FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
                int index;
                if (!int.TryParse(actionId.Substring(ScenarioAuthoringLocalActionIds.ActionFutureSurvivorEditorOpenPrefix.Length), out index)
                    || index < 0
                    || index >= family.FutureSurvivors.Count)
                {
                    message = "Future survivor editor action was out of range.";
                    return true;
                }

                FocusSurvivorEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindFutureSurvivor, index, false);
                state.FocusedFutureSurvivorOriginal = ScenarioSurvivorAuthoringOperations.CloneFutureSurvivor(family.FutureSurvivors[index]);
                message = "Opened focused survivor editor.";
                return true;
            }

            if (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix, StringComparison.Ordinal))
            {
                FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
                string command = actionId.Substring(ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix.Length);
                return HandleMemberCommand(session, state, family.Members, command, "starting survivor", out message);
            }

            if (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringActionIds.ActionFutureSurvivorEditPrefix, StringComparison.Ordinal))
            {
                FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
                string command = actionId.Substring(ScenarioAuthoringActionIds.ActionFutureSurvivorEditPrefix.Length);
                return HandleFutureMemberCommand(session, state, family.FutureSurvivors, command, out message);
            }

            return false;
        }

        private bool AddStartingSurvivor(ScenarioEditorSession session, ScenarioAuthoringState state, out string message)
        {
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            family.OverrideVanillaFamily = true;
            int next = family.Members.Count + 1;
            FamilyMemberConfig config = ScenarioFamilyMemberFactory.CreateDefaultConfig("Survivor " + next.ToString(), ScenarioGender.Any);
            if (_actorResolver != null)
                _actorResolver.EnsureStartingMemberRef(session.WorkingDefinition, config, family.Members.Count);
            family.Members.Add(config);
            MarkDirty(session);
            FocusSurvivorEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindStartingSurvivor, family.Members.Count - 1, true);
            message = "Added starting survivor slot " + next.ToString() + ".";
            return true;
        }

        private bool AddLiveSurvivorToStarting(ScenarioEditorSession session, ScenarioAuthoringState state, string token, out string message)
        {
            int actorLocalId;
            if (!int.TryParse(token, out actorLocalId) || actorLocalId <= 0)
            {
                message = "Live survivor action was out of range.";
                return true;
            }

            FamilyManager manager = FamilyManager.Instance;
            List<FamilyMember> liveMembers = manager != null ? manager.GetAllFamilyMembers() : null;
            FamilyMember liveMember = FindLiveMemberByActorId(liveMembers, actorLocalId);
            if (liveMember == null)
            {
                message = "That live survivor is no longer available.";
                return true;
            }

            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            family.OverrideVanillaFamily = true;
            FamilyMemberConfig config = new FamilyMemberConfig();
            CaptureLiveFamilyMember(liveMember, config);
            if (_actorResolver != null)
                config.ActorRef = _actorResolver.CreateLiveFamilyMemberRef(liveMember);
            family.Members.Add(config);
            MarkDirty(session);
            FocusSurvivorEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindStartingSurvivor, family.Members.Count - 1, false);
            state.FocusedSurvivorOriginal = ScenarioSurvivorAuthoringOperations.CloneMember(config);
            message = "Added " + config.Name + " to the starting cast.";
            return true;
        }

        private static FamilyMember FindLiveMemberByActorId(List<FamilyMember> liveMembers, int actorLocalId)
        {
            for (int i = 0; liveMembers != null && i < liveMembers.Count; i++)
            {
                FamilyMember member = liveMembers[i];
                if (member == null)
                    continue;
                try
                {
                    if (member.GetId() == actorLocalId)
                        return member;
                }
                catch
                {
                }
            }

            return null;
        }

        private static void FocusSurvivorEditor(ScenarioAuthoringState state, string kind, int index, bool isNew)
        {
            if (state == null || index < 0)
                return;

            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = isNew;
            state.FocusedSurvivorOriginal = null;
            state.FocusedFutureSurvivorOriginal = null;
            state.SurvivorColorPickerChannel = null;
            state.SurvivorColorPickerRequestId = 0;
        }

        private bool HandleFutureMemberCommand(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            List<FutureSurvivorDefinition> survivors,
            string command,
            out string message)
        {
            message = null;
            int index;
            string memberCommand;
            if (!TrySplitIndexedCommand(command, out index, out memberCommand) || survivors == null || index < 0 || index >= survivors.Count)
            {
                message = "Future survivor editor action was out of range.";
                return true;
            }

            FutureSurvivorDefinition survivor = survivors[index];
            if (survivor == null)
            {
                survivor = new FutureSurvivorDefinition();
                survivors[index] = survivor;
            }
            if (survivor.Survivor == null)
                survivor.Survivor = ScenarioFamilyMemberFactory.CreateDefaultConfig("Future Survivor " + (index + 1).ToString(), ScenarioGender.Any);
            if (_actorResolver != null)
                _actorResolver.EnsureFutureSurvivorRef(session.WorkingDefinition, survivor, index);

            if (string.Equals(memberCommand, "duplicate_person", StringComparison.Ordinal))
            {
                RecordFamilyUndo(session, "Duplicate future survivor");
                FutureSurvivorDefinition duplicate = ScenarioSurvivorAuthoringOperations.DuplicateFutureSurvivor(survivor, survivors);
                int duplicateIndex = index + 1;
                survivors.Insert(duplicateIndex, duplicate);
                if (_actorResolver != null)
                    _actorResolver.EnsureFutureSurvivorRef(session.WorkingDefinition, duplicate, duplicateIndex);
                MarkDirty(session);
                FocusSurvivorEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindFutureSurvivor, duplicateIndex, true);
                message = "Duplicated future survivor with a new name and actor identity.";
                return true;
            }

            return HandleSingleMemberCommand(session, state, survivor.Survivor, memberCommand, "future survivor", out message);
        }

        private bool HandleMemberCommand(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            List<FamilyMemberConfig> members,
            string command,
            string label,
            out string message)
        {
            message = null;
            if (members == null)
            {
                message = "No survivor list is available.";
                return true;
            }

            if (command.StartsWith("remove.", StringComparison.Ordinal))
            {
                int removeIndex;
                if (TryParseIndex(command.Substring("remove.".Length), members.Count, out removeIndex))
                {
                    members.RemoveAt(removeIndex);
                    MarkDirty(session);
                    message = "Removed " + label + ".";
                    return true;
                }
            }

            if (command.StartsWith("move.", StringComparison.Ordinal))
            {
                int moveIndex;
                int delta;
                if (TryParseIndexDelta(command.Substring("move.".Length), members.Count, out moveIndex, out delta))
                {
                    int targetIndex = moveIndex + delta;
                    if (targetIndex < 0 || targetIndex >= members.Count)
                    {
                        message = "Cannot move " + label + " farther.";
                        return true;
                    }

                    FamilyMemberConfig member = members[moveIndex];
                    members.RemoveAt(moveIndex);
                    members.Insert(targetIndex, member);
                    MarkDirty(session);
                    message = "Moved " + label + " to slot " + (targetIndex + 1).ToString() + ".";
                    return true;
                }
            }

            int index;
            string memberCommand;
            if (!TrySplitIndexedCommand(command, out index, out memberCommand) || index < 0 || index >= members.Count)
            {
                message = "Survivor editor action was out of range.";
                return true;
            }

            FamilyMemberConfig config = members[index];
            if (config == null)
            {
                config = ScenarioFamilyMemberFactory.CreateDefaultConfig("Survivor " + (index + 1).ToString(), ScenarioGender.Any);
                if (_actorResolver != null)
                    _actorResolver.EnsureStartingMemberRef(session.WorkingDefinition, config, index);
                members[index] = config;
            }

            if (string.Equals(memberCommand, "duplicate_person", StringComparison.Ordinal))
            {
                RecordFamilyUndo(session, "Duplicate starting survivor");
                FamilyMemberConfig duplicate = ScenarioSurvivorAuthoringOperations.DuplicateMember(config);
                int duplicateIndex = index + 1;
                members.Insert(duplicateIndex, duplicate);
                if (_actorResolver != null)
                    _actorResolver.EnsureStartingMemberRef(session.WorkingDefinition, duplicate, duplicateIndex);
                MarkDirty(session);
                FocusSurvivorEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindStartingSurvivor, duplicateIndex, true);
                message = "Duplicated starting survivor with a new name and actor identity.";
                return true;
            }

            return HandleSingleMemberCommand(session, state, config, memberCommand, label, out message);
        }

        private bool HandleSingleMemberCommand(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            FamilyMemberConfig config,
            string command,
            string label,
            out string message)
        {
            message = null;
            ScenarioFamilyMemberFactory.EnsureCoreStats(config);

            if (string.Equals(command, "name", StringComparison.Ordinal))
            {
                config.Name = NextName(config.Name);
                MarkDirty(session);
                message = "Changed " + label + " name to " + config.Name + ".";
                return true;
            }

            if (string.Equals(command, "gender", StringComparison.Ordinal))
            {
                config.Gender = NextGender(config.Gender);
                UpdateVanillaMesh(config, false);
                SanitizeAppearanceTextures(config);
                MarkDirty(session);
                message = "Changed " + label + " gender to " + config.Gender + ".";
                return true;
            }

            if (string.Equals(command, "adult", StringComparison.Ordinal))
            {
                EnsureAppearance(config);
                bool current = !config.Appearance.IsAdult.HasValue || config.Appearance.IsAdult.Value;
                config.Appearance.IsAdult = !current;
                config.ExactAge = config.Appearance.IsAdult.Value ? 25 : 12;
                UpdateVanillaMesh(config, false);
                SanitizeAppearanceTextures(config);
                MarkDirty(session);
                message = "Changed " + label + " body type to " + (config.Appearance.IsAdult.Value ? "adult" : "child") + ".";
                return true;
            }

            if (command.StartsWith("age.", StringComparison.Ordinal))
            {
                int delta;
                if (int.TryParse(command.Substring("age.".Length), out delta))
                {
                    int current = config.ExactAge.HasValue ? config.ExactAge.Value : 25;
                    config.ExactAge = Clamp(current + delta, 1, 99);
                    EnsureAppearance(config);
                    config.Appearance.IsAdult = config.ExactAge.Value >= 18;
                    UpdateVanillaMesh(config, false);
                    SanitizeAppearanceTextures(config);
                    MarkDirty(session);
                    message = "Changed " + label + " age to " + config.ExactAge.Value.ToString() + ".";
                    return true;
                }
            }

            if (command.StartsWith("stat.", StringComparison.Ordinal))
            {
                string[] parts = command.Substring("stat.".Length).Split('.');
                int delta;
                if (parts.Length == 2 && int.TryParse(parts[1], out delta))
                {
                    StatOverride stat = ScenarioFamilyMemberFactory.EnsureStat(config, parts[0], 5);
                    if (stat != null)
                    {
                        stat.Value = ScenarioFamilyMemberFactory.ClampStat(stat.Value + delta);
                        MarkDirty(session);
                        message = "Changed " + label + " " + stat.StatId + " to " + stat.Value.ToString() + ".";
                        return true;
                    }
                }
            }

            if (command.StartsWith("stat_set.", StringComparison.Ordinal))
            {
                string[] parts = command.Substring("stat_set.".Length).Split(new[] { '.' }, 2);
                int value;
                if (parts.Length == 2 && int.TryParse(ScenarioAuthoringActionCodec.DecodeToken(parts[1]), out value))
                {
                    StatOverride stat = ScenarioFamilyMemberFactory.EnsureStat(config, parts[0], 5);
                    if (stat != null)
                    {
                        stat.Value = ScenarioFamilyMemberFactory.ClampStat(value);
                        MarkDirty(session);
                        message = "Changed " + label + " " + stat.StatId + " to " + stat.Value.ToString() + ".";
                        return true;
                    }
                }
            }

            if (command.StartsWith("condition.", StringComparison.Ordinal))
            {
                string[] parts = command.Substring("condition.".Length).Split('.');
                int delta;
                if (parts.Length == 2 && int.TryParse(parts[1], out delta))
                {
                    int current;
                    ScenarioFamilyMemberFactory.TryGetConditionValue(config, parts[0], out current);
                    ScenarioFamilyMemberFactory.SetConditionValue(config, parts[0], current + delta);
                    MarkDirty(session);
                    message = "Changed " + label + " " + parts[0] + " condition.";
                    return true;
                }
            }

            if (command.StartsWith("condition_set.", StringComparison.Ordinal))
            {
                string[] parts = command.Substring("condition_set.".Length).Split(new[] { '.' }, 2);
                int value;
                if (parts.Length == 2 && int.TryParse(ScenarioAuthoringActionCodec.DecodeToken(parts[1]), out value))
                {
                    ScenarioFamilyMemberFactory.SetConditionValue(config, parts[0], value);
                    MarkDirty(session);
                    message = "Changed " + label + " " + parts[0] + " condition.";
                    return true;
                }
            }

            if (command.StartsWith("trait.", StringComparison.Ordinal))
            {
                string[] parts = command.Substring("trait.".Length).Split('.');
                if (parts.Length == 2)
                {
                    bool strength = string.Equals(parts[0], "strength", StringComparison.OrdinalIgnoreCase);
                    bool weakness = string.Equals(parts[0], "weakness", StringComparison.OrdinalIgnoreCase);
                    if ((strength || weakness) && SetTrait(config, strength, parts[1]))
                    {
                        MarkDirty(session);
                        message = "Changed " + label + " " + parts[0] + " trait.";
                        return true;
                    }

                    message = "That trait conflicts with the paired " + (strength ? "weakness" : "strength") + " trait.";
                    return true;
                }
            }

            if (command.StartsWith("strength_trait", StringComparison.Ordinal))
            {
                CycleTrait(config, true, ParseTraitCycleDelta(command, "strength_trait"));
                MarkDirty(session);
                message = "Changed " + label + " strength trait.";
                return true;
            }

            if (command.StartsWith("weakness_trait", StringComparison.Ordinal))
            {
                CycleTrait(config, false, ParseTraitCycleDelta(command, "weakness_trait"));
                MarkDirty(session);
                message = "Changed " + label + " weakness trait.";
                return true;
            }

            if (string.Equals(command, "randomize_person", StringComparison.Ordinal))
            {
                RecordFamilyUndo(session, "Randomize survivor");
                _survivorOperations.RandomizeDeclaredFields(config);
                MarkDirty(session);
                message = "Randomized " + label + ". Story links, arrival settings, conditions, skills, and actor identity were kept.";
                return true;
            }

            if (string.Equals(command, "randomize_look", StringComparison.Ordinal))
            {
                RecordFamilyUndo(session, "Randomize survivor appearance");
                _survivorOperations.RandomizeAppearance(config);
                MarkDirty(session);
                message = "Randomized " + label + " appearance.";
                return true;
            }

            if (command.StartsWith("texture.", StringComparison.Ordinal))
            {
                string[] parts = command.Substring("texture.".Length).Split('.');
                int delta;
                if (parts.Length == 2 && int.TryParse(parts[1], out delta))
                {
                    ScenarioCharacterTexturePart part;
                    if (TryParseTexturePart(parts[0], out part))
                    {
                        CycleTexture(config, part, delta);
                        MarkDirty(session);
                        message = "Changed " + label + " " + ScenarioCharacterAppearanceService.BuildPartLabel(part).ToLowerInvariant() + " sprite.";
                        return true;
                    }
                }
            }

            if (command.StartsWith("color.", StringComparison.Ordinal))
            {
                string[] parts = command.Substring("color.".Length).Split('.');
                int delta;
                if (parts.Length == 2 && int.TryParse(parts[1], out delta))
                {
                    ScenarioCharacterColorPart part;
                    if (TryParseColorPart(parts[0], out part))
                    {
                        CycleColor(config, part, delta);
                        MarkDirty(session);
                        message = "Changed " + label + " " + ScenarioCharacterAppearanceService.BuildColorLabel(part).ToLowerInvariant() + " color.";
                        return true;
                    }
                }
            }

            if (command.StartsWith(ScenarioAuthoringLocalActionIds.ActionSurvivorApplyColorCommandPrefix, StringComparison.Ordinal))
            {
                string[] parts = command.Substring(ScenarioAuthoringLocalActionIds.ActionSurvivorApplyColorCommandPrefix.Length).Split('.');
                if (parts.Length == 2)
                {
                    ScenarioCharacterColorPart part;
                    Color parsed;
                    string colorHex = "#" + parts[1];
                    if (TryParseColorPart(parts[0], out part)
                        && ScenarioCharacterAppearanceService.TryParseColorHex(colorHex, out parsed))
                    {
                        ScenarioCharacterAppearanceService.UpsertColor(config, part, ScenarioCharacterAppearanceService.ToColorHex(parsed));
                        MarkDirty(session);
                        message = "Changed " + label + " " + ScenarioCharacterAppearanceService.BuildColorLabel(part).ToLowerInvariant() + " color.";
                        return true;
                    }
                }
            }

            if (command.StartsWith(ScenarioActorAuthoringFieldStore.FieldCommandPrefix, StringComparison.Ordinal))
                return HandleModFieldCommand(session, state, config, command.Substring(ScenarioActorAuthoringFieldStore.FieldCommandPrefix.Length), label, out message);

            if (string.Equals(command, "copy_look", StringComparison.Ordinal))
                return CopyLookFromSelected(session, state, config, label, out message);

            if (string.Equals(command, "copy_identity", StringComparison.Ordinal))
                return CopyIdentityFromSelected(session, state, config, label, out message);

            if (string.Equals(command, "clear_look", StringComparison.Ordinal))
            {
                config.Appearance = new FamilyMemberAppearanceConfig();
                MarkDirty(session);
                message = "Cleared " + label + " custom appearance.";
                return true;
            }

            return false;
        }

        private bool CopyLookFromSelected(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            FamilyMemberConfig config,
            string label,
            out string message)
        {
            message = null;
            if (_appearanceService == null || state == null || state.SelectedTarget == null)
            {
                message = "Select a live family member in the hierarchy before copying appearance.";
                return true;
            }

            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target;
            if (!_appearanceService.TryResolve(state.SelectedTarget, out target, out message) || target == null || target.FamilyMember == null)
                return true;

            ScenarioCharacterAppearanceService.CaptureAppearance(target.FamilyMember, config);
            MarkDirty(session);
            message = "Copied selected live character appearance onto " + label + ".";
            return true;
        }

        private bool CopyIdentityFromSelected(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            FamilyMemberConfig config,
            string label,
            out string message)
        {
            message = null;
            if (_appearanceService == null || state == null || state.SelectedTarget == null)
            {
                message = "Select a live family member in the hierarchy before copying identity.";
                return true;
            }

            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target;
            if (!_appearanceService.TryResolve(state.SelectedTarget, out target, out message) || target == null || target.FamilyMember == null)
                return true;

            CaptureLiveFamilyMember(target.FamilyMember, config);
            if (_actorResolver != null)
                config.ActorRef = _actorResolver.CreateLiveFamilyMemberRef(target.FamilyMember);
            MarkDirty(session);
            message = "Copied selected live character identity onto " + label + ".";
            return true;
        }

        private static void CaptureLiveFamilyMember(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null)
                return;

            config.Name = member.firstName;
            config.Gender = member.isMale ? ScenarioGender.Male : ScenarioGender.Female;
            config.Stats.Clear();
            if (member.BaseStats != null)
            {
                for (int statIndex = 0; statIndex < (int)BaseStats.StatType.Max; statIndex++)
                {
                    BaseStats.StatType statType = (BaseStats.StatType)statIndex;
                    BaseStat stat = member.BaseStats.GetStatByEnum(statType);
                    if (stat != null)
                        config.Stats.Add(new StatOverride { StatId = statType.ToString(), Value = stat.Level });
                }
            }
            ScenarioFamilyMemberFactory.EnsureCoreStats(config);

            config.Traits.Clear();
            if (member.traits != null)
            {
                List<Traits.Strength> strengths = member.traits.GetStrengths(false);
                for (int i = 0; strengths != null && i < strengths.Count; i++)
                    config.Traits.Add("Strength:" + strengths[i]);

                List<Traits.Weakness> weaknesses = member.traits.GetWeaknesses(false);
                for (int i = 0; weaknesses != null && i < weaknesses.Count; i++)
                    config.Traits.Add("Weakness:" + weaknesses[i]);
            }

            ScenarioCharacterAppearanceService.CaptureAppearance(member, config);
            CaptureConditions(member, config);
        }

        private static void CaptureConditions(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null || member.stats == null)
                return;

            if (config.Conditions == null)
                config.Conditions = new FamilyMemberConditionConfig();

            config.Conditions.Hunger = ScenarioFamilyMemberFactory.ClampCondition((int)member.stats.hunger.Value);
            config.Conditions.Thirst = ScenarioFamilyMemberFactory.ClampCondition((int)member.stats.thirst.Value);
            config.Conditions.Fatigue = ScenarioFamilyMemberFactory.ClampCondition((int)member.stats.fatigue.Value);
            config.Conditions.Dirtiness = ScenarioFamilyMemberFactory.ClampCondition((int)member.stats.dirtiness.Value);
            config.Conditions.Toilet = ScenarioFamilyMemberFactory.ClampCondition((int)member.stats.toilet.Value);
            config.Conditions.Stress = ScenarioFamilyMemberFactory.ClampCondition((int)member.stats.stress.Value);
        }

        private void CycleTexture(FamilyMemberConfig config, ScenarioCharacterTexturePart part, int delta)
        {
            EnsureAppearance(config);
            UpdateVanillaMesh(config, false);
            string current = GetTextureId(config.Appearance, part);
            string next = _appearanceService != null
                ? _appearanceService.CycleTextureId(config.Appearance.MeshId, part, current, delta)
                : current;
            ScenarioCharacterAppearanceService.UpsertAppearance(config, part, next, null);
        }

        private void CycleColor(FamilyMemberConfig config, ScenarioCharacterColorPart part, int delta)
        {
            EnsureAppearance(config);
            string current = GetColorHex(config.Appearance, part);
            string next = _appearanceService != null
                ? _appearanceService.CycleColorHex(part, current, delta)
                : current;
            ScenarioCharacterAppearanceService.UpsertColor(config, part, next);
        }

        private void SanitizeAppearanceTextures(FamilyMemberConfig config)
        {
            if (_appearanceService != null && config != null)
                _appearanceService.SanitizeAppearanceTextures(config.Appearance);
        }

        private static void UpdateVanillaMesh(FamilyMemberConfig config, bool resetTextures)
        {
            if (config == null)
                return;

            EnsureAppearance(config);
            if (!config.Appearance.IsAdult.HasValue)
                config.Appearance.IsAdult = !config.ExactAge.HasValue || config.ExactAge.Value >= 18;

            bool adult = config.Appearance.IsAdult.Value;
            config.Appearance.MeshId = ResolveVanillaMeshId(config.Gender, adult);
            if (!resetTextures)
                return;

            config.Appearance.HeadTextureId = "default";
            config.Appearance.HeadTexturePath = null;
            config.Appearance.TorsoTextureId = "default";
            config.Appearance.TorsoTexturePath = null;
            config.Appearance.LegTextureId = "default";
            config.Appearance.LegTexturePath = null;
        }

        private static string ResolveVanillaMeshId(ScenarioGender gender, bool adult)
        {
            if (gender == ScenarioGender.Female)
                return adult ? "woman" : "girl";
            return adult ? "man" : "boy";
        }

        private static void EnsureAppearance(FamilyMemberConfig config)
        {
            if (config != null && config.Appearance == null)
                config.Appearance = new FamilyMemberAppearanceConfig();
        }

        private static string GetTextureId(FamilyMemberAppearanceConfig appearance, ScenarioCharacterTexturePart part)
        {
            if (appearance == null)
                return "default";

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head: return appearance.HeadTextureId;
                case ScenarioCharacterTexturePart.Torso: return appearance.TorsoTextureId;
                case ScenarioCharacterTexturePart.Legs: return appearance.LegTextureId;
                default: return "default";
            }
        }

        private static string GetColorHex(FamilyMemberAppearanceConfig appearance, ScenarioCharacterColorPart part)
        {
            if (appearance == null)
                return null;

            switch (part)
            {
                case ScenarioCharacterColorPart.Hair: return appearance.HairColorHex;
                case ScenarioCharacterColorPart.Skin: return appearance.SkinColorHex;
                case ScenarioCharacterColorPart.Shirt: return appearance.ShirtColorHex;
                case ScenarioCharacterColorPart.Pants: return appearance.PantsColorHex;
                default: return null;
            }
        }

        private static bool TryParseTexturePart(string value, out ScenarioCharacterTexturePart part)
        {
            part = ScenarioCharacterTexturePart.Head;
            if (string.Equals(value, "head", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "torso", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "top", StringComparison.OrdinalIgnoreCase))
            {
                part = ScenarioCharacterTexturePart.Torso;
                return true;
            }
            if (string.Equals(value, "legs", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "bottom", StringComparison.OrdinalIgnoreCase))
            {
                part = ScenarioCharacterTexturePart.Legs;
                return true;
            }
            return false;
        }

        private static bool TryParseColorPart(string value, out ScenarioCharacterColorPart part)
        {
            part = ScenarioCharacterColorPart.Hair;
            if (string.Equals(value, "hair", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "skin", StringComparison.OrdinalIgnoreCase))
            {
                part = ScenarioCharacterColorPart.Skin;
                return true;
            }
            if (string.Equals(value, "shirt", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "top", StringComparison.OrdinalIgnoreCase))
            {
                part = ScenarioCharacterColorPart.Shirt;
                return true;
            }
            if (string.Equals(value, "pants", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "bottom", StringComparison.OrdinalIgnoreCase))
            {
                part = ScenarioCharacterColorPart.Pants;
                return true;
            }
            return false;
        }

        private static FamilySetupDefinition EnsureFamily(ScenarioDefinition definition)
        {
            if (definition.FamilySetup == null)
                definition.FamilySetup = new FamilySetupDefinition();
            return definition.FamilySetup;
        }

        private static void MarkDirty(ScenarioEditorSession session)
        {
            if (session == null)
                return;

            session.MarkDraftChanged(ScenarioDirtySection.Family, ScenarioEditCategory.Family);
        }

        private static void RecordFamilyUndo(ScenarioEditorSession session, string description)
        {
            ScenarioAuthoringHistoryService history = ScenarioAuthoringHistoryService.Instance;
            if (history != null && session != null)
                history.RecordAuthoringChange(session.WorkingDefinition, description, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
        }

        private static string NextName(string current)
        {
            int next = 0;
            for (int i = 0; i < NamePresets.Length; i++)
            {
                if (string.Equals(NamePresets[i], current, StringComparison.OrdinalIgnoreCase))
                {
                    next = (i + 1) % NamePresets.Length;
                    break;
                }
            }

            return NamePresets[next];
        }

        private static ScenarioGender NextGender(ScenarioGender current)
        {
            if (current == ScenarioGender.Any)
                return ScenarioGender.Female;
            if (current == ScenarioGender.Female)
                return ScenarioGender.Male;
            return ScenarioGender.Any;
        }

        private static int ParseTraitCycleDelta(string command, string prefix)
        {
            if (string.IsNullOrEmpty(command) || string.IsNullOrEmpty(prefix) || command.Length <= prefix.Length + 1)
                return 1;

            int delta;
            return int.TryParse(command.Substring(prefix.Length + 1), out delta) && delta < 0 ? -1 : 1;
        }

        private static void CycleTrait(FamilyMemberConfig config, bool strength, int delta)
        {
            if (config == null)
                return;

            string prefix = strength ? "Strength:" : "Weakness:";
            Array values = Enum.GetValues(strength ? typeof(Traits.Strength) : typeof(Traits.Weakness));
            string current = null;
            int currentIndex = -1;
            for (int i = 0; config.Traits != null && i < config.Traits.Count; i++)
            {
                string trait = config.Traits[i];
                if (trait != null && trait.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    current = trait.Substring(prefix.Length);
                    config.Traits.RemoveAt(i);
                    break;
                }
            }

            for (int i = 0; i < values.Length; i++)
            {
                object value = values.GetValue(i);
                if (value != null && string.Equals(value.ToString(), current, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            int direction = delta < 0 ? -1 : 1;
            for (int offset = 1; offset <= values.Length; offset++)
            {
                int nextIndex = Mod(currentIndex + (offset * direction), values.Length);
                object next = values.GetValue(nextIndex);
                if (next == null || string.Equals(next.ToString(), "Max", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasOppositeTrait(config, strength, next))
                    continue;

                config.Traits.Add(prefix + next.ToString());
                return;
            }
        }

        private static bool SetTrait(FamilyMemberConfig config, bool strength, string value)
        {
            if (config == null || string.IsNullOrEmpty(value))
                return false;

            string prefix = strength ? "Strength:" : "Weakness:";
            Array values = Enum.GetValues(strength ? typeof(Traits.Strength) : typeof(Traits.Weakness));
            object selected = null;
            for (int i = 0; values != null && i < values.Length; i++)
            {
                object candidate = values.GetValue(i);
                if (candidate != null && string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    selected = candidate;
                    break;
                }
            }

            if (selected == null || string.Equals(selected.ToString(), "Max", StringComparison.OrdinalIgnoreCase) || HasOppositeTrait(config, strength, selected))
                return false;

            for (int i = config.Traits.Count - 1; i >= 0; i--)
            {
                string trait = config.Traits[i];
                if (trait != null && trait.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    config.Traits.RemoveAt(i);
            }

            config.Traits.Add(prefix + selected.ToString());
            return true;
        }

        private static int Mod(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;

            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static bool HasOppositeTrait(FamilyMemberConfig config, bool strength, object value)
        {
            return ScenarioSurvivorTraitConflictRules.ConflictsWithSelection(config, strength, value);
        }

        private bool HandleModFieldCommand(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            FamilyMemberConfig config,
            string command,
            string label,
            out string message)
        {
            message = null;
            string verb;
            string token;
            string argument;
            if (!TrySplitModFieldCommand(command, out verb, out token, out argument))
            {
                message = "Mod field editor action was not recognized.";
                return true;
            }

            string fieldKey = ScenarioAuthoringActionCodec.DecodeToken(token);
            ActorAuthoringFieldDefinition field;
            if (!ScenarioActorAuthoringFieldStore.TryFindField(config, fieldKey, out field))
            {
                message = "The mod field provider for this actor field is not loaded.";
                return true;
            }

            string current = ScenarioActorAuthoringFieldStore.GetValue(config, field);
            if (string.Equals(verb, "open_color", StringComparison.OrdinalIgnoreCase))
            {
                state.SurvivorColorPickerChannel = "mod:" + token;
                state.SurvivorColorPickerRequestId++;
                message = "Opened " + field.Label + " color picker.";
                return true;
            }
            string next = current;
            if (string.Equals(verb, "toggle", StringComparison.OrdinalIgnoreCase))
                next = string.Equals(ScenarioActorAuthoringFieldStore.NormalizeValue(field, current), "true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
            else if (string.Equals(verb, "step", StringComparison.OrdinalIgnoreCase))
                next = StepModField(field, current, argument);
            else if (string.Equals(verb, "enum", StringComparison.OrdinalIgnoreCase))
                next = ScenarioActorAuthoringFieldStore.NextEnumValue(field, current);
            else if (string.Equals(verb, "text", StringComparison.OrdinalIgnoreCase))
                next = ScenarioAuthoringActionCodec.DecodeToken(argument) ?? string.Empty;
            else if (string.Equals(verb, "color", StringComparison.OrdinalIgnoreCase))
                next = "#" + (argument ?? string.Empty);
            else
            {
                message = "The mod field command was not supported.";
                return true;
            }

            if (!ScenarioActorAuthoringFieldStore.SetValue(config, field, next))
            {
                message = "Could not update the mod field payload.";
                return true;
            }

            MarkDirty(session);
            message = "Changed " + label + " mod field " + field.Label + ".";
            return true;
        }

        private static string StepModField(ActorAuthoringFieldDefinition field, string current, string argument)
        {
            int direction;
            if (!int.TryParse(argument ?? "1", out direction))
                direction = 1;
            direction = direction < 0 ? -1 : 1;

            if (field != null && field.ValueType == ActorAuthoringFieldValueType.Float)
            {
                float value;
                float.TryParse(current ?? field.DefaultValue ?? "0", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
                float step = field.FloatStep <= 0f ? 1f : field.FloatStep;
                return ScenarioActorAuthoringFieldStore.NormalizeValue(field, (value + (step * direction)).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }

            int intValue;
            int.TryParse(current ?? field.DefaultValue ?? "0", out intValue);
            int intStep = field != null && field.IntStep > 0 ? field.IntStep : 1;
            return ScenarioActorAuthoringFieldStore.NormalizeValue(field, (intValue + (intStep * direction)).ToString());
        }

        private static bool TrySplitModFieldCommand(string command, out string verb, out string token, out string argument)
        {
            verb = null;
            token = null;
            argument = null;
            if (string.IsNullOrEmpty(command))
                return false;

            string[] parts = command.Split(new[] { '.' }, 3);
            if (parts.Length < 2)
                return false;

            verb = parts[0];
            token = parts[1];
            argument = parts.Length > 2 ? parts[2] : null;
            return !string.IsNullOrEmpty(verb) && !string.IsNullOrEmpty(token);
        }

        private static bool TrySplitIndexedCommand(string value, out int index, out string command)
        {
            index = -1;
            command = null;
            if (string.IsNullOrEmpty(value))
                return false;

            int separator = value.IndexOf('.');
            if (separator <= 0 || separator >= value.Length - 1)
                return false;

            if (!int.TryParse(value.Substring(0, separator), out index))
                return false;

            command = value.Substring(separator + 1);
            return true;
        }

        private static bool TryParseIndex(string value, int count, out int index)
        {
            index = -1;
            return int.TryParse(value, out index) && index >= 0 && index < count;
        }

        private static bool TryParseIndexDelta(string value, int count, out int index, out int delta)
        {
            index = -1;
            delta = 0;
            string[] parts = (value ?? string.Empty).Split('.');
            return parts.Length == 2
                && int.TryParse(parts[0], out index)
                && int.TryParse(parts[1], out delta)
                && index >= 0
                && index < count
                && delta != 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }
    }
}
