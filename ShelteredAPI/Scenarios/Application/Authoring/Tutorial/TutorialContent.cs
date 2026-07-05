using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;

namespace ShelteredAPI.Scenarios.Application.Authoring.Tutorial{
    internal static class TutorialContent
    {
        public const string CompletedKey = "tutorial.completed";
        public const string SkippedKey = "tutorial.skipped";
        public const string StepKey = "tutorial.step";
        public const string HelpPageKey = "tutorial.help_page";

        private static readonly TutorialStep[] Steps =
        {
            new TutorialStep(0, "overview", "SCENARIO OVERVIEW", "Start with the shape of the crisis. Name it, set the terms, and keep the brief short. A confused plan gets people killed.", "OPEN PANEL", ScenarioAuthoringWindowIds.Scenario, ScenarioStageKind.None, null),
            new TutorialStep(1, "build", "BUILD THE SHELTER", "The bunker is the board. Rooms, ladders, walls, and wiring decide what the survivors can reach when things go bad.", "OPEN PANEL", ScenarioAuthoringWindowIds.TilesPalette, ScenarioStageKind.BunkerInside, null),
            new TutorialStep(2, "cast", "CHOOSE THE CAST", "People are not decoration. Set the family, their limits, and what they bring into the hole with them.", "OPEN PANEL", ScenarioAuthoringWindowIds.Survivors, ScenarioStageKind.People, null),
            new TutorialStep(3, "supplies", "COUNT THE SUPPLIES", "Food, water, fuel, medicine. Too much and there is no pressure. Too little and there is no story.", "OPEN PANEL", ScenarioAuthoringWindowIds.Stockpile, ScenarioStageKind.InventoryStorage, null),
            new TutorialStep(4, "timeline", "SET THE CLOCK", "Trouble needs timing. Use the timeline for events, delays, and the moments when the shelter stops feeling safe.", "OPEN PANEL", ScenarioAuthoringWindowIds.Triggers, ScenarioStageKind.Events, null),
            new TutorialStep(5, "story", "WRITE THE ORDERS", "Quests give the scenario a spine. Keep objectives plain. The wasteland does not reward speeches.", "OPEN PANEL", ScenarioAuthoringWindowIds.Quests, ScenarioStageKind.Quests, null),
            new TutorialStep(6, "test", "TEST THE DAMAGE", "Run it before you trust it. If the first day breaks, the rest of the plan is scrap.", "WAITING FOR ACTION", null, ScenarioStageKind.Test, "playtest"),
            new TutorialStep(7, "publish", "PACKAGE IT", "When it holds together, publish the draft. Check warnings first. A bad export wastes everyone\u0027s daylight.", "OPEN PANEL", ScenarioAuthoringWindowIds.Publish, ScenarioStageKind.Publish, null)
        };

        private static readonly ScenarioAuthoringHelpPage[] HelpPages =
        {
            new ScenarioAuthoringHelpPage("Overview", "Set the scenario identity, summary, rules, and validation status. If the warnings are loud here, fix them before adding more."),
            new ScenarioAuthoringHelpPage("Build", "Use Interior for rooms and shelter objects. Use Backdrop and Surface for scene dressing. Keep layers locked when you are not editing them."),
            new ScenarioAuthoringHelpPage("Cast", "Set survivors, starting condition, and roles. The cast should match the pressure you are building."),
            new ScenarioAuthoringHelpPage("Supplies", "Tune storage, food, water, fuel, and medicine. Scarcity is a tool; use it with intent."),
            new ScenarioAuthoringHelpPage("Timeline", "Schedule events and triggers. Prefer a few readable beats over a pile of invisible rules."),
            new ScenarioAuthoringHelpPage("Story", "Quests should tell the player what matters now. Short instructions survive panic better than long ones."),
            new ScenarioAuthoringHelpPage("Test", "Playtest from the editor. Watch the first minutes, then the first day. Save after clean passes."),
            new ScenarioAuthoringHelpPage("Publish", "Validate, export, and check the package. Do not ship warnings you have not read.")
        };

        public static TutorialStep[] GetSteps()
        {
            return Steps;
        }

        public static ScenarioAuthoringHelpPage[] GetHelpPages()
        {
            return HelpPages;
        }
    }
}
