using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
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

        public ScenarioCharacterEditorAuthoringService(ScenarioCharacterAppearanceService appearanceService)
        {
            _appearanceService = appearanceService;
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

        private static bool AddStartingSurvivor(ScenarioEditorSession session, ScenarioAuthoringState state, out string message)
        {
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            family.OverrideVanillaFamily = true;
            int next = family.Members.Count + 1;
            family.Members.Add(ScenarioFamilyMemberFactory.CreateDefaultConfig("Survivor " + next.ToString(), ScenarioGender.Any));
            MarkDirty(session);
            FocusSurvivorEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindStartingSurvivor, family.Members.Count - 1, true);
            message = "Added starting survivor slot " + next.ToString() + ".";
            return true;
        }

        private static void FocusSurvivorEditor(ScenarioAuthoringState state, string kind, int index, bool isNew)
        {
            if (state == null || index < 0)
                return;

            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = isNew;
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
                members[index] = config;
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

            if (string.Equals(command, "strength_trait", StringComparison.Ordinal))
            {
                CycleTrait(config, true);
                MarkDirty(session);
                message = "Changed " + label + " strength trait.";
                return true;
            }

            if (string.Equals(command, "weakness_trait", StringComparison.Ordinal))
            {
                CycleTrait(config, false);
                MarkDirty(session);
                message = "Changed " + label + " weakness trait.";
                return true;
            }

            if (string.Equals(command, "randomize_person", StringComparison.Ordinal))
            {
                RandomizePerson(config);
                MarkDirty(session);
                message = "Randomized " + label + " like vanilla character creation.";
                return true;
            }

            if (string.Equals(command, "randomize_look", StringComparison.Ordinal))
            {
                RandomizeLook(config);
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

        private void RandomizePerson(FamilyMemberConfig config)
        {
            if (config == null)
                return;

            config.Gender = UnityEngine.Random.Range(0, 2) == 0 ? ScenarioGender.Male : ScenarioGender.Female;
            EnsureAppearance(config);
            config.Appearance.IsAdult = UnityEngine.Random.Range(0, 2) == 0;
            config.ExactAge = config.Appearance.IsAdult.Value ? UnityEngine.Random.Range(18, 61) : UnityEngine.Random.Range(6, 18);
            config.Name = NameGenerator.GetFirstName(config.Gender == ScenarioGender.Female ? NameGenerator.Gender.Female : NameGenerator.Gender.Male);

            ScenarioFamilyMemberFactory.EnsureCoreStats(config);
            for (int i = 0; config.Stats != null && i < config.Stats.Count; i++)
            {
                if (config.Stats[i] != null)
                    config.Stats[i].Value = UnityEngine.Random.Range(10, 21);
            }

            config.Traits.Clear();
            AddRandomTrait(config, true);
            AddRandomTrait(config, false);
            RandomizeLook(config);
        }

        private void RandomizeLook(FamilyMemberConfig config)
        {
            if (config == null)
                return;

            EnsureAppearance(config);
            UpdateVanillaMesh(config, false);

            if (_appearanceService != null)
            {
                config.Appearance.HeadTextureId = _appearanceService.RandomTextureId(config.Appearance.MeshId, ScenarioCharacterTexturePart.Head);
                config.Appearance.TorsoTextureId = _appearanceService.RandomTextureId(config.Appearance.MeshId, ScenarioCharacterTexturePart.Torso);
                config.Appearance.LegTextureId = _appearanceService.RandomTextureId(config.Appearance.MeshId, ScenarioCharacterTexturePart.Legs);
                config.Appearance.HairColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Hair);
                config.Appearance.SkinColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Skin);
                config.Appearance.ShirtColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Shirt);
                config.Appearance.PantsColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Pants);
            }

            config.Appearance.HeadTexturePath = null;
            config.Appearance.TorsoTexturePath = null;
            config.Appearance.LegTexturePath = null;
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

        private static void AddRandomTrait(FamilyMemberConfig config, bool strength)
        {
            Array values = Enum.GetValues(strength ? typeof(Traits.Strength) : typeof(Traits.Weakness));
            if (values == null || values.Length == 0)
                return;

            for (int attempts = 0; attempts < values.Length * 2; attempts++)
            {
                object value = values.GetValue(UnityEngine.Random.Range(0, values.Length));
                if (value == null || string.Equals(value.ToString(), "Max", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasOppositeTrait(config, strength, value))
                    continue;

                config.Traits.Add((strength ? "Strength:" : "Weakness:") + value.ToString());
                return;
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

        private static void CycleTrait(FamilyMemberConfig config, bool strength)
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

            for (int offset = 1; offset <= values.Length; offset++)
            {
                int nextIndex = (currentIndex + offset) % values.Length;
                object next = values.GetValue(nextIndex);
                if (next == null || string.Equals(next.ToString(), "Max", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasOppositeTrait(config, strength, next))
                    continue;

                config.Traits.Add(prefix + next.ToString());
                return;
            }
        }

        private static bool HasOppositeTrait(FamilyMemberConfig config, bool strength, object value)
        {
            if (config == null || config.Traits == null || value == null)
                return false;

            if (strength)
            {
                Traits.Strength strengthValue = (Traits.Strength)value;
                Traits.Weakness pairedWeakness;
                if (!ScenarioFamilyMemberFactory.TryGetPairedWeakness(strengthValue, out pairedWeakness))
                    return false;

                for (int i = 0; i < config.Traits.Count; i++)
                {
                    Traits.Weakness weakness;
                    if (ScenarioFamilyMemberFactory.TryParseWeaknessTrait(config.Traits[i], out weakness) && weakness == pairedWeakness)
                        return true;
                }
            }
            else
            {
                Traits.Weakness weaknessValue = (Traits.Weakness)value;
                Traits.Strength pairedStrength;
                if (!ScenarioFamilyMemberFactory.TryGetPairedStrength(weaknessValue, out pairedStrength))
                    return false;

                for (int i = 0; i < config.Traits.Count; i++)
                {
                    Traits.Strength existingStrength;
                    if (ScenarioFamilyMemberFactory.TryParseStrengthTrait(config.Traits[i], out existingStrength) && existingStrength == pairedStrength)
                        return true;
                }
            }

            return false;
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
