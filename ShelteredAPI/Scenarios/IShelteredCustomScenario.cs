using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Sheltered-specific scenario authoring contract for mods that prefer a class-based scenario definition.
    /// </summary>
    public interface IShelteredCustomScenario
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        string Version { get; }
        int Order { get; }
        object UserData { get; }

        ScenarioDef BuildDefinition(CustomScenarioBuildContext context);
        void OnSelected(CustomScenarioEventArgs args);
        void OnSpawned(CustomScenarioEventArgs args);
    }

    /// <summary>
    /// Default overridable implementation for custom Sheltered scenarios.
    /// Override only the members your scenario needs.
    /// </summary>
    public abstract class ShelteredCustomScenarioBase : IShelteredCustomScenario
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }

        public virtual string Description
        {
            get { return string.Empty; }
        }

        public virtual string Version
        {
            get { return "1.0"; }
        }

        public virtual int Order
        {
            get { return 0; }
        }

        public virtual object UserData
        {
            get { return null; }
        }

        public abstract ScenarioDef BuildDefinition(CustomScenarioBuildContext context);

        public virtual void OnSelected(CustomScenarioEventArgs args)
        {
        }

        public virtual void OnSpawned(CustomScenarioEventArgs args)
        {
        }

        public CustomScenarioRegistration ToRegistration()
        {
            return ShelteredScenarioRegistration.FromScenario(this);
        }

        public CustomScenarioRegistrationResult Register()
        {
            return ShelteredCustomScenarioService.Instance.Register(ToRegistration());
        }

        protected ShelteredScenarioDefBuilder CreateDefinition()
        {
            return new ShelteredScenarioDefBuilder()
                .SetId(Id)
                .SetNameKey(DisplayName)
                .SetDescriptionKey(Description);
        }
    }
}
