using System;
using System.Reflection;
using ModAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public delegate ScenarioDef ShelteredScenarioDefinitionFactory(CustomScenarioBuildContext context);

    /// <summary>
    /// Typed Sheltered helpers for creating neutral ModAPI custom scenario registrations.
    /// </summary>
    public static class ShelteredScenarioRegistration
    {
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
