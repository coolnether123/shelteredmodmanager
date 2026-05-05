using ShelteredAPI.Core;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioDomainModule
    {
        public static void AddScenarioDomainModule(this ServiceCollection services)
        {
            services.AddScenarioDomain();
        }
    }
}
