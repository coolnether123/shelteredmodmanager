using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.UI.Internal.Settings;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    /// <summary>
    /// Compatibility report for the reflection fields required by <see cref="ShelteredScenarioDefBuilder"/>.
    /// Use this before relying on builder APIs against an unknown Sheltered build.
    /// </summary>
    public sealed class ShelteredScenarioDefBuilderCompatibility
    {
        public bool HasQuestIdField { get; set; }
        public bool HasNameKeyField { get; set; }
        public bool HasDescriptionKeyField { get; set; }
        public bool HasSelectionField { get; set; }
        public bool HasStagesField { get; set; }
        public bool HasStageIdField { get; set; }

        public bool IsUsable
        {
            get
            {
                return HasQuestIdField
                    && HasNameKeyField
                    && HasDescriptionKeyField
                    && HasStagesField
                    && HasStageIdField;
            }
        }

        public string DescribeFailures()
        {
            List<string> failures = new List<string>();
            if (!HasQuestIdField) failures.Add("missing QuestDefBase.m_id");
            if (!HasNameKeyField) failures.Add("missing QuestDefBase.m_nameKey");
            if (!HasDescriptionKeyField) failures.Add("missing QuestDefBase.m_descriptionKey");
            if (!HasStagesField) failures.Add("missing ScenarioDef.m_stages");
            if (!HasStageIdField) failures.Add("missing ScenarioStage.m_id");
            if (!HasSelectionField) failures.Add("optional selection field missing: QuestDefBase.m_selectionProperties");
            return failures.Count == 0 ? "compatible" : string.Join("; ", failures.ToArray());
        }
    }

    /// <summary>
    /// Helper for constructing Sheltered ScenarioDef and ScenarioStage objects whose serialized fields are private.
    /// Use this when a class-based scenario needs to produce vanilla runtime objects without duplicating reflection code.
    /// </summary>
    public sealed class ShelteredScenarioDefBuilder
    {
        private static readonly FieldInfo QuestIdField = typeof(QuestDefBase).GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestNameKeyField = typeof(QuestDefBase).GetField("m_nameKey", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestDescriptionKeyField = typeof(QuestDefBase).GetField("m_descriptionKey", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestSelectionField = typeof(QuestDefBase).GetField("m_selectionProperties", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterSetupField = typeof(QuestDefBase).GetField("m_characterSetup", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ScenarioStagesField = typeof(ScenarioDef).GetField("m_stages", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StageIdField = typeof(ScenarioStage).GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StageCharacterIdsField = typeof(ScenarioStage).GetField("m_characterIds", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StageIntercomStagesField = typeof(ScenarioStage).GetField("m_intercomStages", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StageUnansweredNextStageField = typeof(ScenarioStage).GetField("m_unansweredNextStage", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StageUnansweredNextDaysField = typeof(ScenarioStage).GetField("m_unansweredNextDays", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StagePunishOnUnansweredField = typeof(ScenarioStage).GetField("m_punishOnUnanswered", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Type QuestSelectionType = typeof(QuestDefBase).GetNestedType("QuestSelection", BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo SelectionUseSurvivalField = GetSelectionField("m_useInSurvival");
        private static readonly FieldInfo SelectionUseSurroundedField = GetSelectionField("m_useInSurrounded");
        private static readonly FieldInfo SelectionUseStasisField = GetSelectionField("m_useInStasis");
        private static readonly FieldInfo SelectionOnceOnlyField = GetSelectionField("m_onceOnly");
        private static readonly FieldInfo SelectionWeightField = GetSelectionField("m_weight");
        private static readonly FieldInfo SelectionStartDateField = GetSelectionField("m_startDate");
        private static readonly FieldInfo SelectionTimeoutDaysField = GetSelectionField("m_timeoutDays");
        private static readonly FieldInfo SelectionMaxSimultaneousInstancesField = GetSelectionField("m_maxSimultaneousInstances");
        private static readonly FieldInfo SelectionDiscoverByRadioField = GetSelectionField("m_discoverByRadio");
        private static readonly FieldInfo SelectionPrerequisiteMilestonesField = GetSelectionField("m_prerequisiteMilestones");

        private static readonly Type QuestCharacterType = typeof(QuestDefBase.QuestCharacter);
        private static readonly FieldInfo QuestCharacterIdField = QuestCharacterType.GetField("m_characterId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterPresetIdField = QuestCharacterType.GetField("m_presetId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterWeaponField = QuestCharacterType.GetField("m_weapon", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterEquippedItem1Field = QuestCharacterType.GetField("m_equippedItem1", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterEquippedItem2Field = QuestCharacterType.GetField("m_equippedItem2", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterPersonalityField = QuestCharacterType.GetField("m_personality", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterNumRandomItemsField = QuestCharacterType.GetField("m_numRandomItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterCarriedItemsField = QuestCharacterType.GetField("m_carriedItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterStatSettingField = QuestCharacterType.GetField("m_statSetting", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterStatsField = QuestCharacterType.GetField("m_stats", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterBackgroundNpcField = QuestCharacterType.GetField("m_backgroundNPC", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterFlipMeshField = QuestCharacterType.GetField("m_flipMesh", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestCharacterSpeciesField = QuestCharacterType.GetField("m_species", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Type IntercomStageType = typeof(QuestEncounterStage);
        private static readonly FieldInfo IntercomIdField = IntercomStageType.GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomTypeField = IntercomStageType.GetField("m_type", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomNextIdField = IntercomStageType.GetField("m_nextId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomAlternateNextIdField = IntercomStageType.GetField("m_alternateNextId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomDialogueField = IntercomStageType.GetField("m_dialogue", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomOptionsField = IntercomStageType.GetField("m_options", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomRandomizedNextIdsField = IntercomStageType.GetField("m_randomizedNextIds", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomItemsField = IntercomStageType.GetField("m_items", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomItemsToRemoveField = IntercomStageType.GetField("m_itemsToRemove", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomEndOptionsField = IntercomStageType.GetField("m_endOptions", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomSubquestsToActivateField = IntercomStageType.GetField("m_subquestsToActivate", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomSubquestCheckField = IntercomStageType.GetField("m_subquestCheck", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomSetMilestonesField = IntercomStageType.GetField("m_setMilestones", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomCheckMilestonesField = IntercomStageType.GetField("m_checkMilestones", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomStageChangeField = IntercomStageType.GetField("m_stageChange", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomStageDescriptionKeyField = IntercomStageType.GetField("m_stageDescriptionKey", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomCharacterIdsField = IntercomStageType.GetField("m_characterIds", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo IntercomRecruitAsFamilyField = IntercomStageType.GetField("m_recruitAsFamily", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly ScenarioDef _definition = new ScenarioDef();
        private readonly List<ScenarioStage> _stages = new List<ScenarioStage>();
        private bool _selectionRequested;
        private bool _onceOnlyRequested;

        public static ShelteredScenarioDefBuilderCompatibility CheckCompatibility()
        {
            return new ShelteredScenarioDefBuilderCompatibility
            {
                HasQuestIdField = QuestIdField != null,
                HasNameKeyField = QuestNameKeyField != null,
                HasDescriptionKeyField = QuestDescriptionKeyField != null,
                HasSelectionField = QuestSelectionField != null,
                HasStagesField = ScenarioStagesField != null,
                HasStageIdField = StageIdField != null
            };
        }

        public ShelteredScenarioDefBuilder SetId(string id)
        {
            SetStringFieldRequired(_definition, QuestIdField, id, "QuestDefBase.m_id");
            return this;
        }

        public ShelteredScenarioDefBuilder SetNameKey(string nameKey)
        {
            SetStringFieldRequired(_definition, QuestNameKeyField, nameKey, "QuestDefBase.m_nameKey");
            return this;
        }

        public ShelteredScenarioDefBuilder SetDescriptionKey(string descriptionKey)
        {
            SetStringFieldRequired(_definition, QuestDescriptionKeyField, descriptionKey, "QuestDefBase.m_descriptionKey");
            return this;
        }

        public ShelteredScenarioDefBuilder UseInModes(bool survival, bool surrounded, bool stasis)
        {
            _selectionRequested = true;
            object selection = GetRequiredSelection("UseInModes");

            SetBoolFieldRequired(selection, SelectionUseSurvivalField, survival, "QuestSelection.m_useInSurvival");
            SetBoolFieldRequired(selection, SelectionUseSurroundedField, surrounded, "QuestSelection.m_useInSurrounded");
            SetBoolFieldRequired(selection, SelectionUseStasisField, stasis, "QuestSelection.m_useInStasis");
            return this;
        }

        public ShelteredScenarioDefBuilder OnceOnly(bool onceOnly)
        {
            _onceOnlyRequested = true;
            object selection = GetRequiredSelection("OnceOnly");
            SetBoolFieldRequired(selection, SelectionOnceOnlyField, onceOnly, "QuestSelection.m_onceOnly");
            return this;
        }

        public ShelteredScenarioDefBuilder ApplySelectionRules(ScenarioSelectionRulesDefinition rules, ScenarioBaseGameMode baseMode)
        {
            if (rules == null)
                rules = ScenarioSelectionRulesDefinition.ForBaseMode(baseMode);
            if (rules.Availability == null)
                rules.Availability = new ScenarioModeAvailabilityDefinition();

            _selectionRequested = true;
            _onceOnlyRequested = true;
            object selection = GetRequiredSelection("ApplySelectionRules");
            SetFieldRequired(selection, SelectionWeightField, rules.Weight, "QuestSelection.m_weight");
            SetFieldRequired(selection, SelectionStartDateField, rules.StartDay, "QuestSelection.m_startDate");
            SetFieldRequired(selection, SelectionTimeoutDaysField, rules.TimeoutDays, "QuestSelection.m_timeoutDays");
            SetFieldRequired(selection, SelectionMaxSimultaneousInstancesField, rules.MaxSimultaneousInstances, "QuestSelection.m_maxSimultaneousInstances");
            SetBoolFieldRequired(selection, SelectionOnceOnlyField, rules.OnceOnly, "QuestSelection.m_onceOnly");
            SetBoolFieldRequired(selection, SelectionDiscoverByRadioField, rules.DiscoverByRadio, "QuestSelection.m_discoverByRadio");
            SetBoolFieldRequired(selection, SelectionUseSurvivalField, rules.Availability.Survival, "QuestSelection.m_useInSurvival");
            SetBoolFieldRequired(selection, SelectionUseSurroundedField, rules.Availability.Surrounded, "QuestSelection.m_useInSurrounded");
            SetBoolFieldRequired(selection, SelectionUseStasisField, rules.Availability.Stasis, "QuestSelection.m_useInStasis");
            ReplaceStringList(selection, SelectionPrerequisiteMilestonesField, rules.PrerequisiteMilestones, "QuestSelection.m_prerequisiteMilestones");
            return this;
        }

        public ShelteredScenarioDefBuilder AddScenarioCharacter(ScenarioNpcDefinition character)
        {
            if (character == null)
                return this;

            IList characters = GetRequiredList(_definition, QuestCharacterSetupField, "QuestDefBase.m_characterSetup");
            QuestDefBase.QuestCharacter runtimeCharacter = new QuestDefBase.QuestCharacter();
            SetStringFieldRequired(runtimeCharacter, QuestCharacterIdField, character.CharacterId, "QuestCharacter.m_characterId");
            SetStringFieldRequired(runtimeCharacter, QuestCharacterPresetIdField, character.PresetId, "QuestCharacter.m_presetId");
            SetEnumField(runtimeCharacter, QuestCharacterWeaponField, typeof(ItemManager.ItemType), character.WeaponItemId, ItemManager.ItemType.Weapon_Fists);
            SetEnumField(runtimeCharacter, QuestCharacterEquippedItem1Field, typeof(ItemManager.ItemType), character.EquippedItem1Id, ItemManager.ItemType.Undefined);
            SetEnumField(runtimeCharacter, QuestCharacterEquippedItem2Field, typeof(ItemManager.ItemType), character.EquippedItem2Id, ItemManager.ItemType.Undefined);
            SetEnumField(runtimeCharacter, QuestCharacterPersonalityField, typeof(EncounterCharacter.PersonalityType), character.Personality, default(EncounterCharacter.PersonalityType));
            SetFieldRequired(runtimeCharacter, QuestCharacterNumRandomItemsField, character.NumRandomItems, "QuestCharacter.m_numRandomItems");
            SetEnumField(runtimeCharacter, QuestCharacterStatSettingField, typeof(QuestDefBase.QuestCharacter.StatSetting), character.StatSetting, QuestDefBase.QuestCharacter.StatSetting.Random_Low);
            ApplyNpcStats(runtimeCharacter, character.Stats);
            SetBoolFieldRequired(runtimeCharacter, QuestCharacterBackgroundNpcField, character.BackgroundNpc, "QuestCharacter.m_backgroundNPC");
            SetBoolFieldRequired(runtimeCharacter, QuestCharacterFlipMeshField, character.FlipMesh, "QuestCharacter.m_flipMesh");
            SetEnumField(runtimeCharacter, QuestCharacterSpeciesField, typeof(EncounterCharacter.SpeciesEnum), character.Species, default(EncounterCharacter.SpeciesEnum));
            ReplaceItemList(runtimeCharacter, QuestCharacterCarriedItemsField, character.CarriedItems, "QuestCharacter.m_carriedItems");
            characters.Add(runtimeCharacter);
            return this;
        }

        public ShelteredScenarioDefBuilder AddSimpleStage(string stageId)
        {
            ScenarioStage stage = CreateStage(stageId);
            if (stage != null)
                _stages.Add(stage);
            return this;
        }

        public ShelteredScenarioDefBuilder AddFlowStage(ScenarioFlowStageDefinition definition)
        {
            if (definition == null)
                return this;

            ScenarioStage stage = CreateStage(definition.Id);
            ReplaceStringList(stage, StageCharacterIdsField, definition.CharacterIds, "ScenarioStage.m_characterIds");
            ReplaceIntercomStageList(stage, definition.IntercomStages);
            SetStringFieldRequired(stage, StageUnansweredNextStageField, definition.UnansweredNextStage, "ScenarioStage.m_unansweredNextStage");
            SetFieldRequired(stage, StageUnansweredNextDaysField, definition.UnansweredNextDays, "ScenarioStage.m_unansweredNextDays");
            SetBoolFieldRequired(stage, StagePunishOnUnansweredField, definition.PunishOnUnanswered, "ScenarioStage.m_punishOnUnanswered");
            _stages.Add(stage);
            return this;
        }

        public ShelteredScenarioDefBuilder AddStage(ScenarioStage stage)
        {
            if (stage != null)
                _stages.Add(stage);
            return this;
        }

        public ScenarioDef Build()
        {
            if (ScenarioStagesField == null)
                throw new InvalidOperationException("Cannot build ScenarioDef because ScenarioDef.m_stages was not found.");

            if (_selectionRequested)
                EnsureSelectionFieldsForModes();
            if (_onceOnlyRequested)
                EnsureSelectionField(SelectionOnceOnlyField, "QuestSelection.m_onceOnly");

            IList runtimeStages = ScenarioStagesField.GetValue(_definition) as IList;
            if (runtimeStages == null)
                throw new InvalidOperationException("Cannot build ScenarioDef because ScenarioDef.m_stages is not an IList.");

            try
            {
                runtimeStages.Clear();
                for (int i = 0; i < _stages.Count; i++)
                    runtimeStages.Add(_stages[i]);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Cannot build ScenarioDef because ScenarioDef.m_stages could not be written: " + ex.Message, ex);
            }

            return _definition;
        }

        public static ScenarioStage CreateStage(string stageId)
        {
            ScenarioStage stage = new ScenarioStage();
            SetStringFieldRequired(stage, StageIdField, stageId, "ScenarioStage.m_id");
            return stage;
        }

        private static void ApplyNpcStats(QuestDefBase.QuestCharacter runtimeCharacter, ScenarioNpcStatsDefinition stats)
        {
            if (runtimeCharacter == null || QuestCharacterStatsField == null)
                return;

            object runtimeStats = QuestCharacterStatsField.GetValue(runtimeCharacter);
            if (runtimeStats == null || stats == null)
                return;

            SetIntFieldIfFound(runtimeStats, "m_strength", stats.Strength);
            SetIntFieldIfFound(runtimeStats, "m_dexterity", stats.Dexterity);
            SetIntFieldIfFound(runtimeStats, "m_charisma", stats.Charisma);
            SetIntFieldIfFound(runtimeStats, "m_perception", stats.Perception);
            SetIntFieldIfFound(runtimeStats, "m_intelligence", stats.Intelligence);
        }

        private static void ReplaceIntercomStageList(ScenarioStage stage, List<ScenarioIntercomStageDefinition> definitions)
        {
            IList stages = GetRequiredList(stage, StageIntercomStagesField, "ScenarioStage.m_intercomStages");
            stages.Clear();
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                QuestEncounterStage intercomStage = CreateIntercomStage(definitions[i]);
                if (intercomStage != null)
                    stages.Add(intercomStage);
            }
        }

        private static QuestEncounterStage CreateIntercomStage(ScenarioIntercomStageDefinition definition)
        {
            if (definition == null)
                return null;

            QuestEncounterStage stage = new QuestEncounterStage();
            SetStringFieldRequired(stage, IntercomIdField, definition.Id, "QuestEncounterStage.m_id");
            SetEnumField(stage, IntercomTypeField, typeof(QuestEncounterStage.EncounterStageType), definition.Type, QuestEncounterStage.EncounterStageType.Standard);
            SetStringFieldRequired(stage, IntercomNextIdField, definition.NextId, "QuestEncounterStage.m_nextId");
            SetStringFieldRequired(stage, IntercomAlternateNextIdField, definition.AlternateNextId, "QuestEncounterStage.m_alternateNextId");
            ReplaceDialogueList(stage, definition.Dialogue);
            ReplaceDialogueOptionList(stage, definition.Options);
            ReplaceStringList(stage, IntercomRandomizedNextIdsField, definition.RandomizedNextIds, "QuestEncounterStage.m_randomizedNextIds");
            ReplaceItemList(stage, IntercomItemsField, definition.Items, "QuestEncounterStage.m_items");
            ReplaceItemList(stage, IntercomItemsToRemoveField, definition.ItemsToRemove, "QuestEncounterStage.m_itemsToRemove");
            ApplyEndOptions(stage, definition.EndOptions);
            ReplaceStringList(stage, IntercomSubquestsToActivateField, definition.SubquestsToActivate, "QuestEncounterStage.m_subquestsToActivate");
            ApplySubquestCheck(stage, definition.SubquestCheck);
            ReplaceMilestoneList(stage, IntercomSetMilestonesField, definition.SetMilestones);
            ReplaceMilestoneCheckList(stage, IntercomCheckMilestonesField, definition.CheckMilestones);
            ApplyStageChange(stage, definition.StageChange);
            ApplyDescriptionOverride(stage, definition.StageDescriptionKey);
            ReplaceStringList(stage, IntercomCharacterIdsField, definition.CharacterIdsToRecruit, "QuestEncounterStage.m_characterIds");
            SetBoolFieldRequired(stage, IntercomRecruitAsFamilyField, definition.RecruitAsFamily, "QuestEncounterStage.m_recruitAsFamily");
            return stage;
        }

        private static void ReplaceDialogueList(QuestEncounterStage stage, List<ScenarioDialogueLineDefinition> definitions)
        {
            IList dialogue = GetRequiredList(stage, IntercomDialogueField, "QuestEncounterStage.m_dialogue");
            dialogue.Clear();
            Type type = typeof(QuestEncounterStage.DialogueString);
            FieldInfo characterField = type.GetField("m_character", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo textKeyField = type.GetField("m_textKey", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                ScenarioDialogueLineDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                object line = Activator.CreateInstance(type);
                SetEnumField(line, characterField, typeof(QuestEncounterStage.QuestEncounterCharacter), definition.Character, QuestEncounterStage.QuestEncounterCharacter.LeadNpc);
                SetStringFieldRequired(line, textKeyField, definition.TextKey, "DialogueString.m_textKey");
                dialogue.Add(line);
            }
        }

        private static void ReplaceDialogueOptionList(QuestEncounterStage stage, List<ScenarioDialogueOptionDefinition> definitions)
        {
            IList options = GetRequiredList(stage, IntercomOptionsField, "QuestEncounterStage.m_options");
            options.Clear();
            Type type = typeof(QuestEncounterStage.DialogueOption);
            FieldInfo textKeyField = type.GetField("m_textKey", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo nextIdField = type.GetField("m_nextId", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                ScenarioDialogueOptionDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                object option = Activator.CreateInstance(type);
                SetStringFieldRequired(option, textKeyField, definition.TextKey, "DialogueOption.m_textKey");
                SetStringFieldRequired(option, nextIdField, definition.NextId, "DialogueOption.m_nextId");
                options.Add(option);
            }
        }

        private static void ApplyEndOptions(QuestEncounterStage stage, ScenarioEncounterEndOptionsDefinition definition)
        {
            object endOptions = GetRequiredObject(stage, IntercomEndOptionsField, "QuestEncounterStage.m_endOptions");
            if (definition == null)
                definition = new ScenarioEncounterEndOptionsDefinition();

            Type type = endOptions.GetType();
            SetEnumField(endOptions, type.GetField("m_type", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.EncounterEndType), definition.Type, QuestEncounterStage.EncounterEndType.NothingHappens);
            SetEnumField(endOptions, type.GetField("m_combatResult", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.QuestCombatResult), definition.CombatResult, QuestEncounterStage.QuestCombatResult.Nothing);
            SetStringFieldRequired(endOptions, type.GetField("m_combatWinMilestone", BindingFlags.NonPublic | BindingFlags.Instance), definition.CombatWinMilestone, "EndOptions.m_combatWinMilestone");
            SetStringFieldRequired(endOptions, type.GetField("m_combatLossMilestone", BindingFlags.NonPublic | BindingFlags.Instance), definition.CombatLossMilestone, "EndOptions.m_combatLossMilestone");
            SetBoolFieldRequired(endOptions, type.GetField("m_addVehicle", BindingFlags.NonPublic | BindingFlags.Instance), definition.AddVehicle, "EndOptions.m_addVehicle");
            ReplaceItemList(endOptions, type.GetField("m_rewardItems", BindingFlags.NonPublic | BindingFlags.Instance), definition.RewardItems, "EndOptions.m_rewardItems");
            SetEnumField(endOptions, type.GetField("m_moralOutcome", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.MoralResult), definition.MoralOutcome, QuestEncounterStage.MoralResult.None);
            SetEnumField(endOptions, type.GetField("m_moralOutcomeCombatWon", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.MoralResult), definition.MoralOutcomeCombatWon, QuestEncounterStage.MoralResult.None);
            SetEnumField(endOptions, type.GetField("m_moralOutcomeCombatLost", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.MoralResult), definition.MoralOutcomeCombatLost, QuestEncounterStage.MoralResult.None);
            SetEnumField(endOptions, type.GetField("m_addSurroundedCharacterOutcome", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.AddSurroundedCharacter), definition.AddSurroundedCharacterOutcome, QuestEncounterStage.AddSurroundedCharacter.None);
            SetEnumField(endOptions, type.GetField("m_revealSurroundedMapRegionOption", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.RevealHiddenSurroundedMapRegion), definition.RevealSurroundedMapRegionOption, QuestEncounterStage.RevealHiddenSurroundedMapRegion.None);
            SetBoolFieldRequired(endOptions, type.GetField("m_overrideTradeItems", BindingFlags.NonPublic | BindingFlags.Instance), definition.OverrideTradeItems, "EndOptions.m_overrideTradeItems");
            ReplaceItemList(endOptions, type.GetField("m_tradeItems", BindingFlags.NonPublic | BindingFlags.Instance), definition.TradeItems, "EndOptions.m_tradeItems");
            SetFieldRequired(endOptions, type.GetField("m_minRandomTradeItems", BindingFlags.NonPublic | BindingFlags.Instance), definition.MinRandomTradeItems, "EndOptions.m_minRandomTradeItems");
            SetFieldRequired(endOptions, type.GetField("m_maxRandomTradeItems", BindingFlags.NonPublic | BindingFlags.Instance), definition.MaxRandomTradeItems, "EndOptions.m_maxRandomTradeItems");
            ReplaceFloatingQuestTriggers(endOptions, type.GetField("m_triggerFloatingQuests", BindingFlags.NonPublic | BindingFlags.Instance), definition.TriggerFloatingQuests);
            ReplaceScenarioTriggers(endOptions, type.GetField("m_spawnScenarios", BindingFlags.NonPublic | BindingFlags.Instance), definition.SpawnScenarios);
            SetBoolFieldRequired(endOptions, type.GetField("m_completeQuest", BindingFlags.NonPublic | BindingFlags.Instance), definition.CompleteQuest, "EndOptions.m_completeQuest");
            SetBoolFieldRequired(endOptions, type.GetField("m_completeParentScenario", BindingFlags.NonPublic | BindingFlags.Instance), definition.CompleteParentScenario, "EndOptions.m_completeParentScenario");
        }

        private static void ApplySubquestCheck(QuestEncounterStage stage, ScenarioSubquestCheckDefinition definition)
        {
            object check = GetRequiredObject(stage, IntercomSubquestCheckField, "QuestEncounterStage.m_subquestCheck");
            if (definition == null)
                definition = new ScenarioSubquestCheckDefinition();

            Type type = check.GetType();
            SetEnumField(check, type.GetField("m_check", BindingFlags.NonPublic | BindingFlags.Instance), typeof(QuestEncounterStage.SubquestCheckOp), definition.Check, QuestEncounterStage.SubquestCheckOp.AllAreSuccessful);
            ReplaceStringList(check, type.GetField("m_subquests", BindingFlags.NonPublic | BindingFlags.Instance), definition.Subquests, "SubquestCheck.m_subquests");
        }

        private static void ApplyStageChange(QuestEncounterStage stage, ScenarioStageChangeDefinition definition)
        {
            object change = GetRequiredObject(stage, IntercomStageChangeField, "QuestEncounterStage.m_stageChange");
            if (definition == null)
                definition = new ScenarioStageChangeDefinition();

            Type type = change.GetType();
            SetStringFieldRequired(change, type.GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance), definition.Id, "ScenarioStageChange.m_id");
            SetFieldRequired(change, type.GetField("m_delay", BindingFlags.NonPublic | BindingFlags.Instance), definition.DelayDays, "ScenarioStageChange.m_delay");
        }

        private static void ApplyDescriptionOverride(QuestEncounterStage stage, string key)
        {
            object description = GetRequiredObject(stage, IntercomStageDescriptionKeyField, "QuestEncounterStage.m_stageDescriptionKey");
            Type type = description.GetType();
            SetStringFieldRequired(description, type.GetField("m_key", BindingFlags.NonPublic | BindingFlags.Instance), key, "DescriptionOverride.m_key");
        }

        private static void ReplaceMilestoneList(QuestEncounterStage stage, FieldInfo field, List<ScenarioMilestoneDefinition> definitions)
        {
            IList milestones = GetRequiredList(stage, field, "QuestEncounterStage milestone list");
            milestones.Clear();
            Type type = typeof(QuestEncounterStage.QuestMilestone);
            FieldInfo nameField = type.GetField("m_name", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo scopeField = type.GetField("m_scope", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo actionField = type.GetField("m_action", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                ScenarioMilestoneDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                object milestone = Activator.CreateInstance(type);
                SetStringFieldRequired(milestone, nameField, definition.Name, "QuestMilestone.m_name");
                SetEnumField(milestone, scopeField, typeof(QuestEncounterStage.MilestoneScope), definition.Scope, QuestEncounterStage.MilestoneScope.ParentQuest);
                SetEnumField(milestone, actionField, typeof(QuestEncounterStage.MilestoneAction), definition.Action, QuestEncounterStage.MilestoneAction.SetMilestone);
                milestones.Add(milestone);
            }
        }

        private static void ReplaceMilestoneCheckList(QuestEncounterStage stage, FieldInfo field, List<ScenarioMilestoneCheckDefinition> definitions)
        {
            IList milestones = GetRequiredList(stage, field, "QuestEncounterStage milestone check list");
            milestones.Clear();
            Type type = typeof(QuestEncounterStage.QuestMilestoneCheck);
            FieldInfo nameField = type.GetField("m_name", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo scopeField = type.GetField("m_scope", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                ScenarioMilestoneCheckDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                object milestone = Activator.CreateInstance(type);
                SetStringFieldRequired(milestone, nameField, definition.Name, "QuestMilestoneCheck.m_name");
                SetEnumField(milestone, scopeField, typeof(QuestEncounterStage.MilestoneScope), definition.Scope, QuestEncounterStage.MilestoneScope.ParentQuest);
                milestones.Add(milestone);
            }
        }

        private static void ReplaceFloatingQuestTriggers(object target, FieldInfo field, List<ScenarioFloatingQuestTriggerDefinition> definitions)
        {
            IList triggers = GetRequiredList(target, field, "EndOptions.m_triggerFloatingQuests");
            triggers.Clear();
            Type type = typeof(QuestEncounterStage.FloatingQuestTrigger);
            FieldInfo idField = type.GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo delayField = type.GetField("m_activationDelayDays", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo durationField = type.GetField("m_durationDays", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                ScenarioFloatingQuestTriggerDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                object trigger = Activator.CreateInstance(type);
                SetStringFieldRequired(trigger, idField, definition.Id, "FloatingQuestTrigger.m_id");
                SetFieldRequired(trigger, delayField, definition.ActivationDelayDays, "FloatingQuestTrigger.m_activationDelayDays");
                SetFieldRequired(trigger, durationField, definition.DurationDays, "FloatingQuestTrigger.m_durationDays");
                triggers.Add(trigger);
            }
        }

        private static void ReplaceScenarioTriggers(object target, FieldInfo field, List<ScenarioSpawnTriggerDefinition> definitions)
        {
            IList triggers = GetRequiredList(target, field, "EndOptions.m_spawnScenarios");
            triggers.Clear();
            Type type = typeof(QuestEncounterStage.ScenarioTrigger);
            FieldInfo idField = type.GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo chanceField = type.GetField("m_spawnChance", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo delayField = type.GetField("m_delayDays", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                ScenarioSpawnTriggerDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                object trigger = Activator.CreateInstance(type);
                SetStringFieldRequired(trigger, idField, definition.Id, "ScenarioTrigger.m_id");
                SetFieldRequired(trigger, chanceField, definition.SpawnChance, "ScenarioTrigger.m_spawnChance");
                SetFieldRequired(trigger, delayField, definition.DelayDays, "ScenarioTrigger.m_delayDays");
                triggers.Add(trigger);
            }
        }

        private object GetRequiredSelection(string operation)
        {
            if (QuestSelectionField == null)
                throw new InvalidOperationException("Cannot apply " + operation + " because QuestDefBase.m_selectionProperties was not found.");

            object selection = QuestSelectionField.GetValue(_definition);
            if (selection == null)
                throw new InvalidOperationException("Cannot apply " + operation + " because QuestDefBase.m_selectionProperties was null.");

            return selection;
        }

        private static void EnsureSelectionFieldsForModes()
        {
            EnsureSelectionField(SelectionUseSurvivalField, "QuestSelection.m_useInSurvival");
            EnsureSelectionField(SelectionUseSurroundedField, "QuestSelection.m_useInSurrounded");
            EnsureSelectionField(SelectionUseStasisField, "QuestSelection.m_useInStasis");
        }

        private static void EnsureSelectionField(FieldInfo field, string fieldName)
        {
            if (field == null)
                throw new InvalidOperationException("Cannot build ScenarioDef because " + fieldName + " was not found.");
        }

        private static FieldInfo GetSelectionField(string fieldName)
        {
            return QuestSelectionType != null ? QuestSelectionType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance) : null;
        }

        private static void SetStringFieldRequired(object target, FieldInfo field, string value, string fieldName)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot set " + fieldName + " because the target object was null.");
            if (field == null)
                throw new InvalidOperationException("Cannot set " + fieldName + " because the field was not found.");

            field.SetValue(target, value ?? string.Empty);
        }

        private static void SetBoolFieldRequired(object target, FieldInfo field, bool value, string fieldName)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot set " + fieldName + " because the target object was null.");
            if (field == null)
                throw new InvalidOperationException("Cannot set " + fieldName + " because the field was not found.");

            field.SetValue(target, value);
        }

        private static void SetFieldRequired(object target, FieldInfo field, object value, string fieldName)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot set " + fieldName + " because the target object was null.");
            if (field == null)
                throw new InvalidOperationException("Cannot set " + fieldName + " because the field was not found.");

            field.SetValue(target, value);
        }

        private static void SetEnumField(object target, FieldInfo field, Type enumType, string value, object fallback)
        {
            if (target == null || field == null || enumType == null)
                return;

            object parsed = fallback;
            if (!string.IsNullOrEmpty(value))
            {
                try { parsed = Enum.Parse(enumType, value, true); }
                catch { parsed = fallback; }
            }

            field.SetValue(target, parsed);
        }

        private static void SetIntFieldIfFound(object target, string fieldName, int value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
                return;

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
        }

        private static object GetRequiredObject(object target, FieldInfo field, string fieldName)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot read " + fieldName + " because the target object was null.");
            if (field == null)
                throw new InvalidOperationException("Cannot read " + fieldName + " because the field was not found.");

            object value = field.GetValue(target);
            if (value == null)
                throw new InvalidOperationException("Cannot read " + fieldName + " because it was null.");
            return value;
        }

        private static IList GetRequiredList(object target, FieldInfo field, string fieldName)
        {
            IList list = GetRequiredObject(target, field, fieldName) as IList;
            if (list == null)
                throw new InvalidOperationException("Cannot write " + fieldName + " because it is not an IList.");
            return list;
        }

        private static void ReplaceStringList(object target, FieldInfo field, List<string> values, string fieldName)
        {
            IList list = GetRequiredList(target, field, fieldName);
            list.Clear();
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    list.Add(values[i]);
            }
        }

        private static void ReplaceItemList(object target, FieldInfo field, List<ItemEntry> values, string fieldName)
        {
            IList list = GetRequiredList(target, field, fieldName);
            list.Clear();
            for (int i = 0; values != null && i < values.Count; i++)
            {
                ItemEntry item = values[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId))
                    continue;

                ItemManager.ItemType itemType;
                try { itemType = (ItemManager.ItemType)Enum.Parse(typeof(ItemManager.ItemType), item.ItemId, true); }
                catch { itemType = ItemManager.ItemType.Undefined; }
                if (itemType == ItemManager.ItemType.Undefined)
                    continue;

                list.Add(new ItemStack(itemType, item.Quantity));
            }
        }
    }
}
