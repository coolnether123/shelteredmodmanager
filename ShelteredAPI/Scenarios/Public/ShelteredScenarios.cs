using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Registration;
using ShelteredAPI.UI.FieldManual.Textures;
namespace ShelteredAPI.Scenarios.Public{
    /// <summary>
    /// Stable Sheltered scenario registration and catalog facade.
    /// </summary>
    public static class ShelteredScenarios
    {
        public static ICustomScenarioService Service
        {
            get
            {
                return ShelteredCustomScenarioService.Instance;
            }
        }

        public static CustomScenarioRegistrationResult Register(IShelteredCustomScenario scenario)
        {
            CustomScenarioRegistration registration = ShelteredScenarioRegistration.FromScenario(scenario);
            return Register(registration);
        }

        public static CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration)
        {
            return ShelteredCustomScenarioService.Instance.Register(registration);
        }

        public static bool Unregister(string scenarioId)
        {
            return ShelteredCustomScenarioService.Instance.Unregister(scenarioId);
        }

        public static bool TryGet(string scenarioId, out CustomScenarioInfo scenario)
        {
            return ShelteredCustomScenarioService.Instance.TryGet(scenarioId, out scenario);
        }

        public static CustomScenarioInfo[] List()
        {
            return ShelteredCustomScenarioService.Instance.List();
        }

        public static CustomScenarioRegistration FromDefinition(string id, string displayName, ScenarioDef definition)
        {
            return ShelteredScenarioRegistration.FromDefinition(id, displayName, definition);
        }

        public static CustomScenarioRegistration FromScenario(IShelteredCustomScenario scenario)
        {
            return ShelteredScenarioRegistration.FromScenario(scenario);
        }

        public static CustomScenarioRegistration FromFactory(string id, string displayName, ShelteredScenarioDefinitionFactory factory)
        {
            return ShelteredScenarioRegistration.FromFactory(id, displayName, factory);
        }

        public static ShelteredScenarioDefBuilder CreateScenarioDefBuilder()
        {
            return new ShelteredScenarioDefBuilder();
        }

        public static ShelteredScenarioDefBuilderCompatibility CheckScenarioDefBuilderCompatibility()
        {
            return ShelteredScenarioDefBuilder.CheckCompatibility();
        }

        public static ScenarioInfo[] ListXmlDefinitions()
        {
            return ShelteredCustomScenarioService.Instance.ListDefinitions();
        }

        public static void RefreshXmlDefinitions()
        {
            ShelteredCustomScenarioService.Instance.RefreshDefinitionCatalog();
        }
    }
}
