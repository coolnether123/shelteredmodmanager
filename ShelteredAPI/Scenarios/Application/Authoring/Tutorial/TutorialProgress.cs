namespace ShelteredAPI.Scenarios.Application.Authoring.Tutorial{
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
            ShelteredAPI.Scenarios.Domain.Stages.ScenarioStageKind stage,
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
        public ShelteredAPI.Scenarios.Domain.Stages.ScenarioStageKind Stage { get; private set; }
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
        public ScenarioAuthoringTourStep(string targetId, string title, string body, string openAction)
        {
            TargetId = targetId;
            Title = title;
            Body = body;
            OpenAction = openAction;
        }

        public string TargetId { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public string OpenAction { get; private set; }
    }
}
