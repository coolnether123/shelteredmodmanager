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

        public ScenarioAuthoringWorkspaceComposer()
        {
            _storyBuilder = new ScenarioStoryWorkspaceViewModelBuilder(
                new ScenarioQuestPopupsWorkspaceBuilder());
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
                default:
                    return null;
            }
        }
    }
}
