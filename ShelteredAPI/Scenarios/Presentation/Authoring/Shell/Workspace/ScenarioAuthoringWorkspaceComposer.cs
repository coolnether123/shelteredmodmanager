using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>
    /// The exclusive content-kind switch for navigator/document workspace migrations.
    /// Unmigrated content kinds deliberately return null and continue through the
    /// existing section builders.
    /// </summary>
    internal sealed class ScenarioAuthoringWorkspaceComposer
    {
        private readonly ScenarioStoryWorkspaceViewModelBuilder _storyBuilder;
        private readonly ScenarioCastWorkspaceViewModelBuilder _castBuilder;
        private readonly ScenarioSuppliesWorkspaceViewModelBuilder _suppliesBuilder;
        private readonly ScenarioMapWorkspaceViewModelBuilder _mapBuilder;
        private readonly ScenarioTestStatusFlowWorkspaceBuilder _testBuilder;
        private readonly ScenarioPublishStatusFlowWorkspaceBuilder _publishBuilder;

        public ScenarioAuthoringWorkspaceComposer()
            : this(
                new ScenarioRuntimeTestAuthoringContentBuilder(
                    new ScenarioTimelineBuilder(),
                    new ScenarioModDependencyDetector(),
                    new ScenarioModCompatibilityViewModelBuilder()),
                new ScenarioPublishAuthoringContentBuilder(
                    new ScenarioTimelineBuilder(),
                    new ScenarioModDependencyDetector(),
                    new ScenarioModCompatibilityViewModelBuilder()))
        {
        }

        public ScenarioAuthoringWorkspaceComposer(
            ScenarioRuntimeTestAuthoringContentBuilder runtimeTestBuilder,
            ScenarioPublishAuthoringContentBuilder publishBuilder)
        {
            _storyBuilder = new ScenarioStoryWorkspaceViewModelBuilder(
                new ScenarioQuestPopupsWorkspaceBuilder());
            _castBuilder = new ScenarioCastWorkspaceViewModelBuilder();
            _suppliesBuilder = new ScenarioSuppliesWorkspaceViewModelBuilder();
            _mapBuilder = new ScenarioMapWorkspaceViewModelBuilder();
            _testBuilder = new ScenarioTestStatusFlowWorkspaceBuilder(runtimeTestBuilder);
            _publishBuilder = new ScenarioPublishStatusFlowWorkspaceBuilder(publishBuilder);
        }

        public ScenarioAuthoringWorkspaceViewModel Build(
            ScenarioAuthoringWindowContentKind contentKind,
            ScenarioAuthoringWindowContentContext context)
        {
            if (context == null)
                return null;

            switch (contentKind)
            {
                case ScenarioAuthoringWindowContentKind.Quests:
                    return _storyBuilder.Build(context);
                case ScenarioAuthoringWindowContentKind.Survivors:
                    return _castBuilder.Build(context);
                case ScenarioAuthoringWindowContentKind.Stockpile:
                    return _suppliesBuilder.Build(context);
                case ScenarioAuthoringWindowContentKind.Map:
                    return context.State != null && context.State.WorldLoading
                        ? null
                        : _mapBuilder.Build(context);
                case ScenarioAuthoringWindowContentKind.Scenario:
                    return context.State != null
                        && context.State.ActiveStage == ScenarioStageKind.Test
                        && !context.State.WorldLoading
                            ? _testBuilder.Build(context)
                            : null;
                case ScenarioAuthoringWindowContentKind.Publish:
                    return _publishBuilder.Build(context);
                default:
                    return null;
            }
        }
    }
}
