using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Domain.Stages;

namespace ShelteredScenarioEditor.Application.Authoring.Tutorial{
    internal sealed class TutorialProgress
    {
        public bool Completed { get; set; }
        public bool Skipped { get; set; }
        public int Step { get; set; }
        public int HelpPage { get; set; }
        public string HelpTopicId { get; set; }

        public TutorialProgress Copy()
        {
            return new TutorialProgress
            {
                Completed = Completed,
                Skipped = Skipped,
                Step = Step,
                HelpPage = HelpPage,
                HelpTopicId = HelpTopicId
            };
        }
    }

    internal sealed class ScenarioAuthoringHelpPage
    {
        public ScenarioAuthoringHelpPage(
            string id,
            string title,
            string body,
            ShelteredScenarioEditor.Domain.Stages.ScenarioStageKind stage,
            string windowId,
            string tourId)
        {
            Id = id;
            Title = title;
            Body = body;
            Stage = stage;
            WindowId = windowId;
            TourId = tourId;
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public ShelteredScenarioEditor.Domain.Stages.ScenarioStageKind Stage { get; private set; }
        public string WindowId { get; private set; }
        public string TourId { get; private set; }
    }

    internal sealed class ScenarioAuthoringTourDefinition
    {
        public ScenarioAuthoringTourDefinition(string id, string title, ScenarioAuthoringTourStep[] steps)
        {
            Id = id;
            Title = title;
            Steps = steps ?? new ScenarioAuthoringTourStep[0];
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public ScenarioAuthoringTourStep[] Steps { get; private set; }
    }

    internal sealed class ScenarioAuthoringTourStep
    {
        public ScenarioAuthoringTourStep(string targetId, string title, string body)
            : this(targetId, title, body, null, null)
        {
        }

        public ScenarioAuthoringTourStep(string targetId, string title, string body, ShellUxCommand openCommand)
            : this(targetId, title, body, openCommand, null)
        {
        }

        public ScenarioAuthoringTourStep(string targetId, string title, string body, ScenarioAuthoringTool openTool)
            : this(targetId, title, body, null, openTool)
        {
        }

        public ScenarioAuthoringTourStep(string targetId, string title, string body, ScenarioAuthoringCommand openCommand)
            : this(targetId, title, body, openCommand, null)
        {
        }

        private ScenarioAuthoringTourStep(
            string targetId,
            string title,
            string body,
            ScenarioAuthoringCommand openCommand,
            ScenarioAuthoringTool? openTool)
        {
            TargetId = targetId;
            Title = title;
            Body = body;
            OpenCommand = openCommand;
            OpenTool = openTool;
        }

        public string TargetId { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public ScenarioAuthoringCommand OpenCommand { get; private set; }
        public ScenarioAuthoringTool? OpenTool { get; private set; }
    }
}
