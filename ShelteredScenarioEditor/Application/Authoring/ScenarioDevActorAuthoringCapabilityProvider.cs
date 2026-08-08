using System.Collections.Generic;
using ModAPI.Actors;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed class ScenarioDevActorAuthoringCapabilityProvider : IActorAuthoringCapabilityProvider
    {
        public const string Provider = "shelteredapi.dev.actor_authoring";
        public const string ProviderMod = "ShelteredAPI.DevActorFields";
        public const string Component = "ShelteredAPI.DevActorFields.authoring";

        public string ProviderId { get { return Provider; } }
        public string ProviderModId { get { return ProviderMod; } }
        public int Priority { get { return 9000; } }

        public IList<ActorAuthoringFieldDefinition> GetFields()
        {
            ActorKind[] people = new[] { ActorKind.Player, ActorKind.Synthetic, ActorKind.Visitor, ActorKind.Citizen };
            return new List<ActorAuthoringFieldDefinition>
            {
                Field("dev_ready", "Dev Ready", ActorAuthoringFieldValueType.Bool, people, "false", "Toggle used by contract tests for bool persistence."),
                Field("dev_rank", "Dev Rank", ActorAuthoringFieldValueType.Int, people, "0", "Integer field used by contract tests for stepper persistence.", 0, 10),
                Field("dev_focus", "Dev Focus", ActorAuthoringFieldValueType.Float, people, "0", "Float field used by contract tests for stepper persistence."),
                Field("dev_codename", "Dev Codename", ActorAuthoringFieldValueType.String, people, string.Empty, "String field used by contract tests for text persistence."),
                EnumField("dev_stance", "Dev Stance", people, "neutral", new[] { "neutral", "brave", "cautious" }),
                Field("dev_tint", "Dev Tint", ActorAuthoringFieldValueType.Color, people, "#FFFFFFFF", "Color field used by contract tests for color picker persistence.")
            };
        }

        private static ActorAuthoringFieldDefinition Field(
            string id,
            string label,
            ActorAuthoringFieldValueType valueType,
            ActorKind[] kinds,
            string defaultValue,
            string helpText)
        {
            return Field(id, label, valueType, kinds, defaultValue, helpText, null, null);
        }

        private static ActorAuthoringFieldDefinition Field(
            string id,
            string label,
            ActorAuthoringFieldValueType valueType,
            ActorKind[] kinds,
            string defaultValue,
            string helpText,
            int? min,
            int? max)
        {
            return new ActorAuthoringFieldDefinition
            {
                Id = id,
                Label = label,
                ValueType = valueType,
                ComponentId = Component,
                ComponentVersion = 1,
                ApplicableActorKinds = kinds,
                RequiredModId = ProviderMod,
                HelpText = helpText,
                DefaultValue = defaultValue,
                MinInt = min,
                MaxInt = max,
                IntStep = 1,
                MinFloat = 0f,
                MaxFloat = 1f,
                FloatStep = 0.1f
            };
        }

        private static ActorAuthoringFieldDefinition EnumField(string id, string label, ActorKind[] kinds, string defaultValue, string[] values)
        {
            ActorAuthoringFieldDefinition field = Field(id, label, ActorAuthoringFieldValueType.StringEnum, kinds, defaultValue, "Enum field used by contract tests for option persistence.");
            field.EnumValues = values;
            return field;
        }
    }
}
