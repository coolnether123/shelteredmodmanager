using System;
using System.Collections.Generic;
using ModAPI.Actors;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredScenarioEditor.Infrastructure.Assets;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
namespace ShelteredScenarioEditor.Application.Authoring{
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

        private readonly ScenarioEditorCharacterAppearanceService _appearanceService;
        private readonly ScenarioEditorActorReferenceService _actorResolver;
        private readonly ScenarioSurvivorAuthoringOperations _survivorOperations;
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;
        private readonly ScenarioAuthoringHistoryService _historyService;

        public ScenarioCharacterEditorAuthoringService(
            ScenarioEditorCharacterAppearanceService appearanceService,
            ScenarioEditorActorReferenceService actorResolver,
            ScenarioAuthoringRendererInteractionState rendererInteraction,
            ScenarioAuthoringHistoryService historyService)
        {
            _appearanceService = appearanceService;
            _actorResolver = actorResolver;
            _rendererInteraction = rendererInteraction;
            _historyService = historyService;
            _survivorOperations = new ScenarioSurvivorAuthoringOperations(appearanceService);
        }

        public bool Execute(ScenarioEditorSession session, ScenarioAuthoringState state, CharacterEditorCommand command, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (command == null)
                return false;
            if (command.Kind == CharacterEditorCommandKind.AddStarting)
                return AddStartingSurvivor(session, state, out message);
            if (command.Kind == CharacterEditorCommandKind.AddLiveStarting)
                return AddLiveSurvivorToStarting(session, state, command.ActorId, out message);
            if (command.Kind == CharacterEditorCommandKind.OpenColorPicker)
            {
                string channel = command.Key;
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

            FamilySetupDefinition editorFamily = EnsureFamily(session.WorkingDefinition);
            return command.Scope == CharacterMemberScope.Future
                ? HandleFutureMemberCommand(session, state, editorFamily.FutureSurvivors, command, out message)
                : HandleMemberCommand(session, state, editorFamily.Members, command, "starting survivor", out message);
        }

        private bool AddStartingSurvivor(ScenarioEditorSession session, ScenarioAuthoringState state, out string message)
        {
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            family.OverrideVanillaFamily = true;
            int next = family.Members.Count + 1;
            FamilyMemberConfig config = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.CreateDefaultConfig("Survivor " + next.ToString(), ScenarioGender.Any);
            if (_actorResolver != null)
                _actorResolver.EnsureStartingMemberRef(session.WorkingDefinition, config, family.Members.Count);
            family.Members.Add(config);
            MarkDirty(session);
            SelectStartingSurvivor(state, session.WorkingDefinition, family.Members.Count - 1);
            message = "Added and selected starting survivor " + next.ToString() + ".";
            return true;
        }

        private bool AddLiveSurvivorToStarting(ScenarioEditorSession session, ScenarioAuthoringState state, int actorLocalId, out string message)
        {
            if (actorLocalId <= 0)
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
            SelectStartingSurvivor(state, session.WorkingDefinition, family.Members.Count - 1);
            message = "Added and selected " + config.Name + " in the starting cast.";
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

        private void SelectStartingSurvivor(ScenarioAuthoringState state, ScenarioDefinition definition, int index)
        {
            if (index < 0)
                return;
            CloseColorPicker(state);
            ScenarioCastWorkspaceActions.SelectStartingDocument(definition, index, _rendererInteraction);
        }

        private void SelectFutureSurvivor(ScenarioAuthoringState state, ScenarioDefinition definition, int index)
        {
            if (index < 0)
                return;
            CloseColorPicker(state);
            ScenarioCastWorkspaceActions.SelectFutureDocument(definition, index, _rendererInteraction);
        }

        private static void CloseColorPicker(ScenarioAuthoringState state)
        {
            if (state == null)
                return;
            state.SurvivorColorPickerChannel = null;
        }

        private bool HandleFutureMemberCommand(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            List<FutureSurvivorDefinition> survivors,
            CharacterEditorCommand command,
            out string message)
        {
            message = null;
            int index = command.Index;
            if (survivors == null || index < 0 || index >= survivors.Count)
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
                survivor.Survivor = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.CreateDefaultConfig("Future Survivor " + (index + 1).ToString(), ScenarioGender.Any);
            if (_actorResolver != null)
                _actorResolver.EnsureFutureSurvivorRef(session.WorkingDefinition, survivor, index);

            if (command.Kind == CharacterEditorCommandKind.Duplicate)
            {
                RecordFamilyUndo(session, "Duplicate future survivor");
                FutureSurvivorDefinition duplicate = ScenarioSurvivorAuthoringOperations.DuplicateFutureSurvivor(survivor, survivors);
                int duplicateIndex = index + 1;
                survivors.Insert(duplicateIndex, duplicate);
                if (_actorResolver != null)
                    _actorResolver.EnsureFutureSurvivorRef(session.WorkingDefinition, duplicate, duplicateIndex);
                MarkDirty(session);
                SelectFutureSurvivor(state, session.WorkingDefinition, duplicateIndex);
                message = "Duplicated future survivor with a new name and actor identity.";
                return true;
            }

            return HandleSingleMemberCommand(session, state, survivor.Survivor, command, "future survivor", out message);
        }

        private bool HandleMemberCommand(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            List<FamilyMemberConfig> members,
            CharacterEditorCommand command,
            string label,
            out string message)
        {
            message = null;
            if (members == null)
            {
                message = "No survivor list is available.";
                return true;
            }

            if (command.Kind == CharacterEditorCommandKind.Remove)
            {
                int removeIndex = command.Index;
                if (removeIndex >= 0 && removeIndex < members.Count)
                {
                    FamilyMemberConfig selectedMember = ResolveSelectedStartingMember(session.WorkingDefinition);
                    members.RemoveAt(removeIndex);
                    MarkDirty(session);
                    ReconcileStartingSelection(session.WorkingDefinition, members, selectedMember);
                    message = "Removed " + label + ".";
                    return true;
                }
            }

            if (command.Kind == CharacterEditorCommandKind.Move)
            {
                int moveIndex = command.Index;
                int delta = command.Delta;
                if (moveIndex >= 0 && moveIndex < members.Count)
                {
                    int targetIndex = moveIndex + delta;
                    if (targetIndex < 0 || targetIndex >= members.Count)
                    {
                        message = "Cannot move " + label + " farther.";
                        return true;
                    }

                    FamilyMemberConfig selectedMember = ResolveSelectedStartingMember(session.WorkingDefinition);
                    FamilyMemberConfig member = members[moveIndex];
                    members.RemoveAt(moveIndex);
                    members.Insert(targetIndex, member);
                    MarkDirty(session);
                    ReconcileStartingSelection(session.WorkingDefinition, members, selectedMember);
                    message = "Moved " + label + " to slot " + (targetIndex + 1).ToString() + ".";
                    return true;
                }
            }

            int index = command.Index;
            if (index < 0 || index >= members.Count)
            {
                message = "Survivor editor action was out of range.";
                return true;
            }

            FamilyMemberConfig config = members[index];
            if (config == null)
            {
                config = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.CreateDefaultConfig("Survivor " + (index + 1).ToString(), ScenarioGender.Any);
                if (_actorResolver != null)
                    _actorResolver.EnsureStartingMemberRef(session.WorkingDefinition, config, index);
                members[index] = config;
            }

            if (command.Kind == CharacterEditorCommandKind.Duplicate)
            {
                RecordFamilyUndo(session, "Duplicate starting survivor");
                FamilyMemberConfig duplicate = ScenarioSurvivorAuthoringOperations.DuplicateMember(config);
                int duplicateIndex = index + 1;
                members.Insert(duplicateIndex, duplicate);
                if (_actorResolver != null)
                    _actorResolver.EnsureStartingMemberRef(session.WorkingDefinition, duplicate, duplicateIndex);
                MarkDirty(session);
                SelectStartingSurvivor(state, session.WorkingDefinition, duplicateIndex);
                message = "Duplicated starting survivor with a new name and actor identity.";
                return true;
            }

            return HandleSingleMemberCommand(session, state, config, command, label, out message);
        }

        private bool HandleSingleMemberCommand(
            ScenarioEditorSession session,
            ScenarioAuthoringState state,
            FamilyMemberConfig config,
            CharacterEditorCommand command,
            string label,
            out string message)
        {
            message = null;
            ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.EnsureCoreStats(config);

            if (command.Kind == CharacterEditorCommandKind.CycleName)
            {
                config.Name = NextName(config.Name);
                MarkDirty(session);
                message = "Changed " + label + " name to " + config.Name + ".";
                return true;
            }

            if (command.Kind == CharacterEditorCommandKind.CycleGender)
            {
                config.Gender = NextGender(config.Gender);
                UpdateVanillaMesh(config, false);
                SanitizeAppearanceTextures(config);
                MarkDirty(session);
                message = "Changed " + label + " gender to " + config.Gender + ".";
                return true;
            }

            if (command.Kind == CharacterEditorCommandKind.ToggleAdult)
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

            if (command.Kind == CharacterEditorCommandKind.StepStat)
            {
                int delta = command.Delta;
                StatOverride stat = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.EnsureStat(config, command.Key, 5);
                if (stat != null)
                {
                    stat.Value = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampStat(stat.Value + delta);
                    MarkDirty(session);
                    message = "Changed " + label + " " + stat.StatId + " to " + stat.Value.ToString() + ".";
                    return true;
                }
            }

            if (command.Kind == CharacterEditorCommandKind.SetStat)
            {
                int value;
                if (int.TryParse(command.Value, out value))
                {
                    StatOverride stat = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.EnsureStat(config, command.Key, 5);
                    if (stat != null)
                    {
                        stat.Value = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampStat(value);
                        MarkDirty(session);
                        message = "Changed " + label + " " + stat.StatId + " to " + stat.Value.ToString() + ".";
                        return true;
                    }
                }
            }

            if (command.Kind == CharacterEditorCommandKind.StepCondition)
            {
                int delta = command.Delta;
                    int current;
                    ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.TryGetConditionValue(config, command.Key, out current);
                    ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.SetConditionValue(config, command.Key, current + delta);
                    MarkDirty(session);
                    message = "Changed " + label + " " + command.Key + " condition.";
                    return true;
            }

            if (command.Kind == CharacterEditorCommandKind.SetCondition)
            {
                int value;
                if (int.TryParse(command.Value, out value))
                {
                    ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.SetConditionValue(config, command.Key, value);
                    MarkDirty(session);
                    message = "Changed " + label + " " + command.Key + " condition.";
                    return true;
                }
            }

            if (command.Kind == CharacterEditorCommandKind.SetTrait)
            {
                    bool strength = command.Flag;
                    if (SetTrait(config, strength, command.Value))
                    {
                        MarkDirty(session);
                        message = "Changed " + label + " " + command.Key + " trait.";
                        return true;
                    }

                    message = "That trait conflicts with the paired " + (strength ? "weakness" : "strength") + " trait.";
                    return true;
            }

            if (command.Kind == CharacterEditorCommandKind.CycleTrait)
            {
                CycleTrait(config, command.Flag, command.Delta);
                MarkDirty(session);
                message = "Changed " + label + " " + command.Key + " trait.";
                return true;
            }

            if (command.Kind == CharacterEditorCommandKind.RandomizePerson)
            {
                RecordFamilyUndo(session, "Randomize survivor");
                _survivorOperations.RandomizeDeclaredFields(config);
                MarkDirty(session);
                message = "Randomized " + label + ". Story links, arrival settings, conditions, skills, and actor identity were kept.";
                return true;
            }

            if (command.Kind == CharacterEditorCommandKind.RandomizeLook)
            {
                RecordFamilyUndo(session, "Randomize survivor appearance");
                _survivorOperations.RandomizeAppearance(config);
                MarkDirty(session);
                message = "Randomized " + label + " appearance.";
                return true;
            }

            if (command.Kind == CharacterEditorCommandKind.CycleTexture)
            {
                        CycleTexture(config, command.TexturePart, command.Delta);
                        MarkDirty(session);
                        message = "Changed " + label + " " + ScenarioEditorCharacterAppearanceService.BuildPartLabel(command.TexturePart).ToLowerInvariant() + " sprite.";
                        return true;
            }

            if (command.Kind == CharacterEditorCommandKind.CycleColor)
            {
                        CycleColor(config, command.ColorPart, command.Delta);
                        MarkDirty(session);
                        message = "Changed " + label + " " + ScenarioEditorCharacterAppearanceService.BuildColorLabel(command.ColorPart).ToLowerInvariant() + " color.";
                        return true;
            }

            if (command.Kind == CharacterEditorCommandKind.ApplyColor && command.HasColor)
            {
                        ScenarioEditorCharacterAppearanceService.UpsertColor(config, command.ColorPart, ScenarioEditorCharacterAppearanceService.ToColorHex(command.Color));
                        MarkDirty(session);
                        message = "Changed " + label + " " + ScenarioEditorCharacterAppearanceService.BuildColorLabel(command.ColorPart).ToLowerInvariant() + " color.";
                        return true;
            }

            if (command.Kind == CharacterEditorCommandKind.ToggleField
                || command.Kind == CharacterEditorCommandKind.StepField
                || command.Kind == CharacterEditorCommandKind.SetFieldText
                || command.Kind == CharacterEditorCommandKind.CycleFieldEnum
                || command.Kind == CharacterEditorCommandKind.OpenFieldColorPicker
                || command.Kind == CharacterEditorCommandKind.SetFieldColor)
                return HandleModFieldCommand(session, state, config, command, label, out message);

            if (command.Kind == CharacterEditorCommandKind.CopyLook)
                return CopyLookFromSelected(session, state, config, label, out message);

            if (command.Kind == CharacterEditorCommandKind.CopyIdentity)
                return CopyIdentityFromSelected(session, state, config, label, out message);

            if (command.Kind == CharacterEditorCommandKind.ClearLook)
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

            ScenarioEditorCharacterAppearanceService.ResolvedCharacterTarget target;
            if (!_appearanceService.TryResolve(state.SelectedTarget, out target, out message) || target == null || target.FamilyMember == null)
                return true;

            ScenarioEditorCharacterAppearanceService.CaptureAppearance(target.FamilyMember, config);
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

            ScenarioEditorCharacterAppearanceService.ResolvedCharacterTarget target;
            if (!_appearanceService.TryResolve(state.SelectedTarget, out target, out message) || target == null || target.FamilyMember == null)
                return true;

            CaptureLiveFamilyMember(target.FamilyMember, config);
            if (_actorResolver != null)
                config.ActorRef = _actorResolver.CreateLiveFamilyMemberRef(target.FamilyMember);
            MarkDirty(session);
            message = "Copied selected live character identity onto " + label + ".";
            return true;
        }

        private FamilyMemberConfig ResolveSelectedStartingMember(ScenarioDefinition definition)
        {
            string selected = _rendererInteraction.GetWorkspaceSelection(
                ScenarioCastWorkspaceActions.WorkspaceId,
                ScenarioCastWorkspaceActions.SubtabId);
            int index;
            return ScenarioCastWorkspaceActions.TryResolveStartingEntity(definition, selected, out index)
                && definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null
                && index >= 0 && index < definition.FamilySetup.Members.Count
                    ? definition.FamilySetup.Members[index]
                    : null;
        }

        private void ReconcileStartingSelection(
            ScenarioDefinition definition,
            List<FamilyMemberConfig> members,
            FamilyMemberConfig previouslySelected)
        {
            if (previouslySelected == null)
                return;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                if (object.ReferenceEquals(members[i], previouslySelected))
                {
                    ScenarioCastWorkspaceActions.SelectStartingDocument(definition, i, _rendererInteraction);
                    return;
                }
            }
            ScenarioCastWorkspaceActions.SelectOverview(_rendererInteraction);
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
            ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.EnsureCoreStats(config);

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

            ScenarioEditorCharacterAppearanceService.CaptureAppearance(member, config);
            CaptureConditions(member, config);
        }

        private static void CaptureConditions(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null || member.stats == null)
                return;

            if (config.Conditions == null)
                config.Conditions = new FamilyMemberConditionConfig();

            config.Conditions.Hunger = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampCondition((int)member.stats.hunger.Value);
            config.Conditions.Thirst = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampCondition((int)member.stats.thirst.Value);
            config.Conditions.Fatigue = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampCondition((int)member.stats.fatigue.Value);
            config.Conditions.Dirtiness = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampCondition((int)member.stats.dirtiness.Value);
            config.Conditions.Toilet = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampCondition((int)member.stats.toilet.Value);
            config.Conditions.Stress = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.ClampCondition((int)member.stats.stress.Value);
        }

        private void CycleTexture(FamilyMemberConfig config, ScenarioEditorCharacterTexturePart part, int delta)
        {
            EnsureAppearance(config);
            UpdateVanillaMesh(config, false);
            string current = GetTextureId(config.Appearance, part);
            string next = _appearanceService != null
                ? _appearanceService.CycleTextureId(config.Appearance.MeshId, part, current, delta)
                : current;
            ScenarioEditorCharacterAppearanceService.UpsertAppearance(config, part, next, null);
        }

        private void CycleColor(FamilyMemberConfig config, ScenarioEditorCharacterColorPart part, int delta)
        {
            EnsureAppearance(config);
            string current = GetColorHex(config.Appearance, part);
            string next = _appearanceService != null
                ? _appearanceService.CycleColorHex(part, current, delta)
                : current;
            ScenarioEditorCharacterAppearanceService.UpsertColor(config, part, next);
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

        private static string GetTextureId(FamilyMemberAppearanceConfig appearance, ScenarioEditorCharacterTexturePart part)
        {
            if (appearance == null)
                return "default";

            switch (part)
            {
                case ScenarioEditorCharacterTexturePart.Head: return appearance.HeadTextureId;
                case ScenarioEditorCharacterTexturePart.Torso: return appearance.TorsoTextureId;
                case ScenarioEditorCharacterTexturePart.Legs: return appearance.LegTextureId;
                default: return "default";
            }
        }

        private static string GetColorHex(FamilyMemberAppearanceConfig appearance, ScenarioEditorCharacterColorPart part)
        {
            if (appearance == null)
                return null;

            switch (part)
            {
                case ScenarioEditorCharacterColorPart.Hair: return appearance.HairColorHex;
                case ScenarioEditorCharacterColorPart.Skin: return appearance.SkinColorHex;
                case ScenarioEditorCharacterColorPart.Shirt: return appearance.ShirtColorHex;
                case ScenarioEditorCharacterColorPart.Pants: return appearance.PantsColorHex;
                default: return null;
            }
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

        private void RecordFamilyUndo(ScenarioEditorSession session, string description)
        {
            if (_historyService != null && session != null)
                _historyService.RecordAuthoringChange(session.WorkingDefinition, description, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
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
            CharacterEditorCommand command,
            string label,
            out string message)
        {
            message = null;
            ActorAuthoringFieldDefinition field;
            if (!ScenarioActorAuthoringFieldStore.TryFindField(config, command.Key, out field))
            {
                message = "The mod field provider for this actor field is not loaded.";
                return true;
            }

            string current = ScenarioActorAuthoringFieldStore.GetValue(config, field);
            if (command.Kind == CharacterEditorCommandKind.OpenFieldColorPicker)
            {
                state.SurvivorColorPickerChannel = "mod:" + ScenarioAutomationIdCodec.EncodeToken(command.Key);
                state.SurvivorColorPickerRequestId++;
                message = "Opened " + field.Label + " color picker.";
                return true;
            }
            string next = current;
            if (command.Kind == CharacterEditorCommandKind.ToggleField)
                next = string.Equals(ScenarioActorAuthoringFieldStore.NormalizeValue(field, current), "true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
            else if (command.Kind == CharacterEditorCommandKind.StepField)
                next = StepModField(field, current, command.Delta);
            else if (command.Kind == CharacterEditorCommandKind.CycleFieldEnum)
                next = ScenarioActorAuthoringFieldStore.NextEnumValue(field, current);
            else if (command.Kind == CharacterEditorCommandKind.SetFieldText)
                next = command.Value ?? string.Empty;
            else if (command.Kind == CharacterEditorCommandKind.SetFieldColor)
                next = command.Value != null && command.Value.StartsWith("#", StringComparison.Ordinal) ? command.Value : "#" + (command.Value ?? string.Empty);
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

        private static string StepModField(ActorAuthoringFieldDefinition field, string current, int direction)
        {
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

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }
    }
}
