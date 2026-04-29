using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioPresentationModule
    {
        public static void AddScenarioPresentationModule(this ServiceCollection services)
        {
            services.AddScenarioPresentation();
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioMapAuthoringContentBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringShellImguiRenderModule(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringImguiRenderModule(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringNguiRenderModule(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringPresentationBuilder(
                    resolver.Get<ScenarioAuthoringCaptureService>(),
                    resolver.Get<IScenarioAuthoringSectionHub>(),
                    resolver.Get<ScenarioAuthoringWindowRegistry>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<ScenarioSpriteRuntimeResolver>(),
                    resolver.Get<ShellChromeViewModelBuilder>(),
                    resolver.Get<StageNavigationViewModelBuilder>(),
                    resolver.Get<InspectorViewModelBuilder>(),
                    resolver.Get<StatusBarViewModelBuilder>(),
                    resolver.Get<ScenarioTimelineBuilder>(),
                    resolver.Get<ScenarioTimelineViewModelBuilder>(),
                    resolver.Get<ScenarioModDependencyDetector>(),
                    resolver.Get<ScenarioModCompatibilityViewModelBuilder>(),
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioTargetClassifier>(),
                    resolver.Get<ScenarioAssetAuthoringContentBuilder>(),
                    resolver.Get<ScenarioMapAuthoringContentBuilder>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringPresentationService(
                    resolver.Get<IScenarioAuthoringBackend>(),
                    new IScenarioAuthoringRenderModule[]
                    {
                        resolver.Get<ScenarioAuthoringShellImguiRenderModule>(),
                        resolver.Get<ScenarioAuthoringImguiRenderModule>(),
                        resolver.Get<ScenarioAuthoringNguiRenderModule>()
                    });
            });
        }
    }
}
