using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioHelpAuthoringContentBuilder
    {
        public ScenarioAuthoringHelpViewModel Build(ScenarioAuthoringState state)
        {
            ScenarioAuthoringHelpPage[] pages = TutorialContent.GetHelpPages();
            int pageCount = pages != null ? pages.Length : 0;
            int pageIndex = ClampPage(state != null && state.Settings != null ? state.Settings.GetInt(TutorialContent.HelpPageKey, 0) : 0, pageCount);
            ScenarioAuthoringHelpPage page = pageCount > 0 ? pages[pageIndex] : null;

            return new ScenarioAuthoringHelpViewModel
            {
                Title = "WORKSHOP HELP",
                Subtitle = pageCount > 0 ? "Page " + (pageIndex + 1) + " of " + pageCount : "No pages",
                PageIndex = pageIndex,
                PageCount = pageCount,
                PageTitle = page != null ? page.Title : "Help",
                Body = page != null ? page.Body : "No help content is available.",
                HeaderActions = new[]
                {
                    Item.Action(ScenarioAuthoringActionIds.ActionShellCloseHelp, "x", "Close help.", true, false, "CL")
                },
                PreviousAction = Item.Action(ScenarioAuthoringActionIds.ActionHelpPagePrevious, "PREV", "Previous help page.", pageIndex > 0, false, "LT"),
                NextAction = Item.Action(ScenarioAuthoringActionIds.ActionHelpPageNext, "NEXT", "Next help page.", pageIndex + 1 < pageCount, false, "RT"),
                ReplayAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialReset, "Replay Tutorial", "Start the guided tour again.", true, true, "RP")
            };
        }

        private static int ClampPage(int page, int pageCount)
        {
            if (pageCount <= 0)
                return 0;
            if (page < 0)
                return 0;
            if (page >= pageCount)
                return pageCount - 1;
            return page;
        }
    }
}
