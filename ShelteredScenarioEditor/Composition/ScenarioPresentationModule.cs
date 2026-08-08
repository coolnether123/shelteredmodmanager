using ShelteredScenarioEditor.Core;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Tutorial;
using ShelteredScenarioEditor.Application.Compatibility;
using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Application.Stages;
using ShelteredScenarioEditor.Application.Timeline;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Infrastructure.Assets;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
using ShelteredScenarioEditor.Presentation.UiKit.Animation;
using ShelteredScenarioEditor.Presentation.Inspector;
namespace ShelteredScenarioEditor.Composition{
    internal static class ScenarioPresentationModule
    {
        public static void AddScenarioPresentationModule(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ShellChromeViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioModCompatibilityViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new StageNavigationViewModelBuilder(
                    resolver.Get<ScenarioStageRegistry>(),
                    resolver.Get<ScenarioStageCoordinator>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new InspectorViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new StatusBarViewModelBuilder(
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioBuildPlacementAuthoringService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioTimelineNavigationService(
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringRendererInteractionState>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAssetAuthoringContentBuilder(
                    resolver.Get<IScenarioAuthoringSectionHub>(),
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioEditorSpriteRuntimeResolver>(),
                    resolver.Get<ScenarioWeatherEffectSpriteCatalogService>(),
                    resolver.Get<ScenarioAssetInventoryService>(),
                    resolver.Get<IScenarioEditorSessionStore>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringShellAnimationService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringTourTargetRegistry(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioMapAuthoringContentBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioQuestAuthoringContentBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioHelpAuthoringContentBuilder(
                    resolver.Get<ScenarioBuildPlacementAuthoringService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringShellImguiRenderModule(
                    resolver.Get<ScenarioAuthoringShellAnimationService>(),
                    resolver.Get<ScenarioAuthoringRendererInteractionState>(),
                    resolver.Get<IScenarioAuthoringBackend>(),
                    resolver.Get<ScenarioAuthoringInputCaptureService>(),
                    resolver.Get<ScenarioAuthoringTourTargetRegistry>(),
                    resolver.Get<ScenarioAuthoringVanillaPanelVisibilityService>(),
                    resolver.Get<ScenarioAuthoringContextMenuService>(),
                    resolver.Get<IScenarioEditorSessionStore>(),
                    resolver.Get<ScenarioDraftSnapshotService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioBuildPlacementAuthoringService>(),
                    resolver.Get<ScenarioSceneSpritePlacementAuthoringService>(),
                    resolver.Get<ScenarioSpriteSwapAuthoringService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringPresentationBuilder(
                    resolver.Get<ScenarioAuthoringCaptureService>(),
                    resolver.Get<IScenarioAuthoringSectionHub>(),
                    resolver.Get<ScenarioAuthoringWindowRegistry>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<ScenarioEditorSpriteRuntimeResolver>(),
                    resolver.Get<ShellChromeViewModelBuilder>(),
                    resolver.Get<StageNavigationViewModelBuilder>(),
                    resolver.Get<InspectorViewModelBuilder>(),
                    resolver.Get<StatusBarViewModelBuilder>(),
                    resolver.Get<ScenarioTimelineBuilder>(),
                    resolver.Get<ScenarioModDependencyDetector>(),
                    resolver.Get<ScenarioModCompatibilityViewModelBuilder>(),
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioAuthoringSelectionService>(),
                    resolver.Get<ScenarioTargetClassifier>(),
                    resolver.Get<ScenarioAssetAuthoringContentBuilder>(),
                    resolver.Get<ScenarioMapAuthoringContentBuilder>(),
                    resolver.Get<ScenarioQuestAuthoringContentBuilder>(),
                    resolver.Get<ScenarioAuthoringTutorialService>(),
                    resolver.Get<ScenarioHelpAuthoringContentBuilder>(),
                    resolver.Get<ScenarioBackdropTargetCatalogService>(),
                    resolver.Get<ScenarioDraftSnapshotService>(),
                    resolver.Get<ScenarioAuthoringRendererInteractionState>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    resolver.Get<ScenarioPublishExportService>(),
                    resolver.Get<ScenarioTestConsoleService>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringPresentationService(
                    resolver.Get<IScenarioAuthoringBackend>(),
                    resolver.Get<ScenarioAuthoringShellImguiRenderModule>(),
                    resolver.Get<ScenarioAuthoringInputCaptureService>());
            });
        }
    }
}
