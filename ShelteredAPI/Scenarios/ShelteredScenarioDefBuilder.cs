using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ShelteredAPI.Scenarios
{
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

        public ShelteredScenarioDefBuilder SetId(string id)
        {
            SetStringField(_definition, QuestIdField, id);
            return this;
        }

        public ShelteredScenarioDefBuilder SetNameKey(string nameKey)
        {
            SetStringField(_definition, QuestNameKeyField, nameKey);
            return this;
        }

        public ShelteredScenarioDefBuilder SetDescriptionKey(string descriptionKey)
        {
            SetStringField(_definition, QuestDescriptionKeyField, descriptionKey);
            return this;
        }

        public ShelteredScenarioDefBuilder UseInModes(bool survival, bool surrounded, bool stasis)
        {
            object selection = GetSelection();
            if (selection == null)
                return this;

            SetBoolField(selection, SelectionUseSurvivalField, survival);
            SetBoolField(selection, SelectionUseSurroundedField, surrounded);
            SetBoolField(selection, SelectionUseStasisField, stasis);
            return this;
        }

        public ShelteredScenarioDefBuilder OnceOnly(bool onceOnly)
        {
            object selection = GetSelection();
            if (selection != null)
                SetBoolField(selection, SelectionOnceOnlyField, onceOnly);
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
            IList runtimeStages = ScenarioStagesField != null ? ScenarioStagesField.GetValue(_definition) as IList : null;
            if (runtimeStages != null)
            {
                runtimeStages.Clear();
                for (int i = 0; i < _stages.Count; i++)
                    runtimeStages.Add(_stages[i]);
            }

            return _definition;
        }

        public static ScenarioStage CreateStage(string stageId)
        {
            try
            {
                ScenarioStage stage = new ScenarioStage();
                SetStringField(stage, StageIdField, stageId);
                return stage;
            }
            catch
            {
                return null;
            }
        }

        private object GetSelection()
        {
            if (QuestSelectionField == null)
                return null;

            return QuestSelectionField.GetValue(_definition);
        }

        private static FieldInfo GetSelectionField(string fieldName)
        {
            return QuestSelectionType != null ? QuestSelectionType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance) : null;
        }

        private static void SetStringField(object target, FieldInfo field, string value)
        {
            if (target != null && field != null)
                field.SetValue(target, value ?? string.Empty);
        }

        private static void SetBoolField(object target, FieldInfo field, bool value)
        {
            if (target != null && field != null)
                field.SetValue(target, value);
        }
    }
}
