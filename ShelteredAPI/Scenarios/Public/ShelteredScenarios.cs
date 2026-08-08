using System;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Registration;
using ShelteredAPI.UI.FieldManual.Textures;
namespace ShelteredAPI.Scenarios.Public{
    /// <summary>
    /// Sheltered-specific factory delegate that builds a vanilla ScenarioDef from neutral ModAPI context.
    /// </summary>
    public delegate ScenarioDef ShelteredScenarioDefinitionFactory(CustomScenarioBuildContext context);

    /// <summary>
    /// Stable Sheltered scenario registration and catalog facade.
    /// </summary>
    public static class ShelteredScenarios
    {
        public static CustomScenarioRegistrationResult Register(IShelteredCustomScenario scenario)
        {
            CustomScenarioRegistration registration = FromScenario(scenario);
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
            return new CustomScenarioRegistration
            {
                Id = id,
                DisplayName = displayName,
                Definition = definition
            };
        }

        public static CustomScenarioRegistration FromScenario(IShelteredCustomScenario scenario)
        {
            if (scenario == null)
                return null;

            IShelteredCustomScenarioDependencies dependencySource = scenario as IShelteredCustomScenarioDependencies;
            return new CustomScenarioRegistration
            {
                Id = scenario.Id,
                DisplayName = scenario.DisplayName,
                Description = scenario.Description,
                Version = scenario.Version,
                Order = scenario.Order,
                RequiredMods = dependencySource != null
                    ? ScenarioDependencyManifest.CloneRequiredMods(dependencySource.RequiredMods)
                    : null,
                DefinitionFactory = new CustomScenarioDefinitionFactory(
                    delegate(CustomScenarioBuildContext context) { return scenario.BuildDefinition(context); }),
                OnSelected = scenario.OnSelected,
                OnSpawned = scenario.OnSpawned,
                UserData = scenario.UserData,
                OwnerAssembly = scenario.GetType().Assembly
            };
        }

        public static CustomScenarioRegistration FromFactory(string id, string displayName, ShelteredScenarioDefinitionFactory factory)
        {
            return new CustomScenarioRegistration
            {
                Id = id,
                DisplayName = displayName,
                DefinitionFactory = factory != null
                    ? new CustomScenarioDefinitionFactory(delegate(CustomScenarioBuildContext context) { return factory(context); })
                    : null,
                OwnerAssembly = ResolveFactoryAssembly(factory)
            };
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

        private static Assembly ResolveFactoryAssembly(ShelteredScenarioDefinitionFactory factory)
        {
            try
            {
                Type declaringType = factory != null && factory.Method != null ? factory.Method.DeclaringType : null;
                return declaringType != null ? declaringType.Assembly : null;
            }
            catch { return null; }
        }
    }
}
