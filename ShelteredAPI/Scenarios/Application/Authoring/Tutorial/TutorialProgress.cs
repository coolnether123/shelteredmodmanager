namespace ShelteredAPI.Scenarios.Application.Authoring.Tutorial{
    internal sealed class TutorialProgress
    {
        public bool Completed { get; set; }
        public bool Skipped { get; set; }
        public int Step { get; set; }
        public int HelpPage { get; set; }

        public TutorialProgress Copy()
        {
            return new TutorialProgress
            {
                Completed = Completed,
                Skipped = Skipped,
                Step = Step,
                HelpPage = HelpPage
            };
        }
    }

    internal sealed class ScenarioAuthoringHelpPage
    {
        public ScenarioAuthoringHelpPage(string title, string body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; private set; }
        public string Body { get; private set; }
    }
}
