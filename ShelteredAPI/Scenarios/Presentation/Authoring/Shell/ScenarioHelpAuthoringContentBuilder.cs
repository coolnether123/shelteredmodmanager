using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioHelpAuthoringContentBuilder
    {
        public ScenarioAuthoringHelpViewModel Build(ScenarioAuthoringState state)
        {
            bool shortcutsView = state != null && state.HelpShortcutsView;
            ScenarioAuthoringInspectorAction[] viewTabs = BuildViewTabs(shortcutsView);
            ScenarioAuthoringInspectorAction[] headerActions = new[]
            {
                Item.Action(ScenarioAuthoringActionIds.ActionShellCloseHelp, "x", "Close help.", true, false, "CL")
            };

            if (shortcutsView)
            {
                ScenarioAuthoringShortcutOverlayViewModel shortcuts =
                    ScenarioAuthoringShortcutOverlayBuilder.Build(ScenarioAuthoringShortcutCatalog.ResolveActiveContext(state));
                return new ScenarioAuthoringHelpViewModel
                {
                    Title = "KEYBOARD SHORTCUTS",
                    Subtitle = "Active context: " + shortcuts.ActiveContextTitle,
                    PageTitle = "Keyboard Shortcuts",
                    HeaderActions = headerActions,
                    ViewTabs = viewTabs,
                    TopicActions = new ScenarioAuthoringInspectorAction[0],
                    Shortcuts = shortcuts
                };
            }

            ScenarioAuthoringHelpPage[] pages = TutorialContent.GetHelpPages();
            int pageCount = pages != null ? pages.Length : 0;
            int pageIndex = ClampPage(state != null && state.Settings != null ? state.Settings.GetInt(TutorialContent.HelpPageKey, 0) : 0, pageCount);
            ScenarioAuthoringHelpPage page = pageCount > 0 ? pages[pageIndex] : null;
            string topicId = page != null ? page.Id : null;

            return new ScenarioAuthoringHelpViewModel
            {
                Title = "WORKSHOP HELP",
                Subtitle = pageCount > 0 ? "Page " + (pageIndex + 1) + " of " + pageCount : "No pages",
                PageIndex = pageIndex,
                PageCount = pageCount,
                PageTitle = page != null ? page.Title : "Help",
                TopicId = topicId,
                Body = page != null ? page.Body : "No help content is available.",
                HeaderActions = headerActions,
                ViewTabs = viewTabs,
                TopicActions = BuildTopicActions(page),
                PreviousAction = Item.Action(ScenarioAuthoringActionIds.ActionHelpPagePrevious, "PREV", "Previous help page.", pageIndex > 0, false, "LT"),
                NextAction = Item.Action(ScenarioAuthoringActionIds.ActionHelpPageNext, "NEXT", "Next help page.", pageIndex + 1 < pageCount, false, "RT"),
                ReplayAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialReset, "Replay Tutorial", "Start the guided tour again.", true, true, "RP")
            };
        }

        private static ScenarioAuthoringInspectorAction[] BuildViewTabs(bool shortcutsView)
        {
            return new[]
            {
                Item.Action(ScenarioAuthoringActionIds.ActionShellHelpShowPages, "Help", "Show the workshop help pages.", true, !shortcutsView, "HP"),
                Item.Action(ScenarioAuthoringActionIds.ActionShellOpenShortcuts, "Shortcuts", "Show the keyboard shortcuts reference.", true, shortcutsView, "KB")
            };
        }

        private static ScenarioAuthoringInspectorAction[] BuildTopicActions(ScenarioAuthoringHelpPage page)
        {
            if (page == null)
                return new ScenarioAuthoringInspectorAction[0];

            System.Collections.Generic.List<ScenarioAuthoringInspectorAction> actions = new System.Collections.Generic.List<ScenarioAuthoringInspectorAction>();
            if (!string.IsNullOrEmpty(page.TourId))
            {
                actions.Add(Item.Action(
                    ScenarioAuthoringActionIds.ActionTourStartPrefix + page.TourId,
                    "Walk Me Through It",
                    "Start the spotlight tour for this topic.",
                    true,
                    true,
                    "TO"));
            }

            actions.Add(Item.Action(
                ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + page.Id,
                "Open Topic",
                "Open the related workspace for this help topic.",
                true,
                false,
                "GO"));
            return actions.ToArray();
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
