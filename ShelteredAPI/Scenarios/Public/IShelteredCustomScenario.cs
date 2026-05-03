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
    /// Optional dependency metadata for class-based custom scenarios.
    /// </summary>
    public interface IShelteredCustomScenarioDependencies
    {
        ScenarioModDependency[] RequiredMods { get; }
    }

    /// <summary>
    /// Default overridable implementation for custom Sheltered scenarios.
    /// Override only the members your scenario needs.
    /// </summary>
    public abstract class ShelteredCustomScenarioBase : IShelteredCustomScenario, IShelteredCustomScenarioDependencies
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }

        public virtual string Description
        {
            get { return string.Empty; }
        }

        /// <summary>
        /// Localization key used by the in-game ScenarioDef. Defaults to DisplayName for backward compatibility.
        /// </summary>
        public virtual string DisplayNameKey
        {
            get { return DisplayName; }
        }

        /// <summary>
        /// Localization key used by the in-game ScenarioDef. Defaults to Description for backward compatibility.
        /// </summary>
        public virtual string DescriptionKey
        {
            get { return Description; }
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

        public virtual ScenarioModDependency[] RequiredMods
        {
            get { return new ScenarioModDependency[0]; }
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
                .SetNameKey(DisplayNameKey)
                .SetDescriptionKey(DescriptionKey);
        }
    }
}
