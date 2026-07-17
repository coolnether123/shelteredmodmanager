using ShelteredAPI.Scenarios.Application.Authoring;

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

        public ScenarioAuthoringWorkspaceComposer()
        {
            _storyBuilder = new ScenarioStoryWorkspaceViewModelBuilder(
                new ScenarioQuestPopupsWorkspaceBuilder());
            _castBuilder = new ScenarioCastWorkspaceViewModelBuilder();
            _suppliesBuilder = new ScenarioSuppliesWorkspaceViewModelBuilder();
            _mapBuilder = new ScenarioMapWorkspaceViewModelBuilder();
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
                    return _mapBuilder.Build(context);
                default:
                    return null;
            }
        }
    }
}
