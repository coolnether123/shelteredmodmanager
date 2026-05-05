using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Presentation.Authoring.Imgui;
using ShelteredAPI.Scenarios.Presentation.Authoring.Ngui;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Presentation.Timeline;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioPresentationModule
    {
        public static void AddScenarioPresentationModule(this ServiceCollection services)
        {
            services.AddScenarioPresentation();
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioMapAuthoringContentBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioQuestAuthoringContentBuilder(); });
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
                    resolver.Get<ScenarioMapAuthoringContentBuilder>(),
                    resolver.Get<ScenarioQuestAuthoringContentBuilder>());
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
