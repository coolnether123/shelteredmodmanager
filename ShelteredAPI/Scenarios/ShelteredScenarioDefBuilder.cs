using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ShelteredAPI.Scenarios
{
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
            if (!HasSelectionField) failures.Add("missing QuestDefBase.m_selectionProperties");
            if (!HasStagesField) failures.Add("missing ScenarioDef.m_stages");
            if (!HasStageIdField) failures.Add("missing ScenarioStage.m_id");
            return failures.Count == 0 ? "compatible" : string.Join("; ", failures.ToArray());
        }
    }

    /// <summary>
    /// Helper for constructing Sheltered ScenarioDef and ScenarioStage objects whose serialized fields are private.
    /// </summary>
    public sealed class ShelteredScenarioDefBuilder
    {
        private static readonly FieldInfo QuestIdField = typeof(QuestDefBase).GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestNameKeyField = typeof(QuestDefBase).GetField("m_nameKey", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestDescriptionKeyField = typeof(QuestDefBase).GetField("m_descriptionKey", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo QuestSelectionField = typeof(QuestDefBase).GetField("m_selectionProperties", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ScenarioStagesField = typeof(ScenarioDef).GetField("m_stages", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StageIdField = typeof(ScenarioStage).GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Type QuestSelectionType = typeof(QuestDefBase).GetNestedType("QuestSelection", BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo SelectionUseSurvivalField = GetSelectionField("m_useInSurvival");
        private static readonly FieldInfo SelectionUseSurroundedField = GetSelectionField("m_useInSurrounded");
        private static readonly FieldInfo SelectionUseStasisField = GetSelectionField("m_useInStasis");
        private static readonly FieldInfo SelectionOnceOnlyField = GetSelectionField("m_onceOnly");

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

        public ShelteredScenarioDefBuilder AddSimpleStage(string stageId)
        {
            ScenarioStage stage = CreateStage(stageId);
            if (stage != null)
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
    }
}
