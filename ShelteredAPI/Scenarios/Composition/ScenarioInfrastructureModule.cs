using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioInfrastructureModule
    {
        public static void AddScenarioInfrastructureModule(this ServiceCollection services)
        {
            services.AddSingleton<IScenarioStateManager>(delegate(IServiceResolver resolver) { return new ScenarioStateManager(); });
            services.AddScenarioInfrastructure();
            services.AddScenarioApplication();
        }
    }
}
