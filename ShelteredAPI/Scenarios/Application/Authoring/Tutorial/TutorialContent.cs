using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;

namespace ShelteredAPI.Scenarios.Application.Authoring.Tutorial{
    internal static class TutorialContent
    {
        public const string CompletedKey = "tutorial.completed";
        public const string SkippedKey = "tutorial.skipped";
        public const string StepKey = "tutorial.step";
        public const string HelpPageKey = "tutorial.help_page";
        public const string HelpTopicKey = "tutorial.help_topic";

        public const string TopicSetup = "setup";
        public const string TopicWorldCamera = "world-camera";
        public const string TopicPlacementSnap = "placement-snap";
        public const string TopicSelectionCycling = "selection-cycling";
        public const string TopicCast = "cast";
        public const string TopicSupplies = "supplies";
        public const string TopicTimelineConditions = "timeline-conditions";
        public const string TopicStory = "story";
        public const string TopicArtPixelEditor = "art-pixel-editor";
        public const string TopicTest = "test";
        public const string TopicPublish = "publish";
        public const string TopicBaseModes = "base-modes";

        public const string TourEditorBasics = "editor-basics";
        public const string TourPlaceFirstObject = "place-first-object";
        public const string TourTimelineEvent = "timeline-event";
        public const string TourEditSprite = "edit-sprite";

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
            new ScenarioAuthoringHelpPage(TopicSetup, "Home / Setup", "Home shows the draft title, selected base, save state, progress cards, and quick actions. New authoring drafts also get a local setup checklist stored beside the draft XML.", ScenarioStageKind.Bunker, ScenarioAuthoringWindowIds.Scenario, TourEditorBasics),
            new ScenarioAuthoringHelpPage(TopicWorldCamera, "World & Camera", "The editor shell keeps the shelter visible while authoring. Camera input is guarded while the shell owns pointer focus, and the World tabs open the bunker, surface, backdrop, and map workspaces.", ScenarioStageKind.Bunker, ScenarioAuthoringWindowIds.Scenario, TourEditorBasics),
            new ScenarioAuthoringHelpPage(TopicPlacementSnap, "Placement & Snap", "Object placement and scene-sprite placement are separate authoring tools. Snap-to-grid is a persistent editor setting, and placement tools cancel when switching tools or starting playtest.", ScenarioStageKind.BunkerInside, ScenarioAuthoringWindowIds.TilesPalette, TourPlaceFirstObject),
            new ScenarioAuthoringHelpPage(TopicSelectionCycling, "Selection & Cycling", "Selection uses live scene targets. When multiple targets are available, the selection stack can cycle or select a specific row; clearing selection drops selected and multi-selected targets.", ScenarioStageKind.BunkerInside, ScenarioAuthoringWindowIds.Hierarchy, TourEditorBasics),
            new ScenarioAuthoringHelpPage(TopicCast, "Cast", "The Cast workspace edits starting family members and future survivors. Starting survivors live in FamilySetup.Members; future arrivals live in FamilySetup.FutureSurvivors.", ScenarioStageKind.People, ScenarioAuthoringWindowIds.Survivors, null),
            new ScenarioAuthoringHelpPage(TopicSupplies, "Supplies", "Supplies are authored as starting inventory entries, an override-random-start flag, and scheduled inventory changes. The editor can also capture the current live inventory into the draft.", ScenarioStageKind.InventoryStorage, ScenarioAuthoringWindowIds.Stockpile, null),
            new ScenarioAuthoringHelpPage(TopicTimelineConditions, "Timeline & Conditions", "Timeline authoring combines triggers, weather events, gates, and scheduled actions. Gates group condition references; scheduled actions can apply effects at scenario time.", ScenarioStageKind.Events, ScenarioAuthoringWindowIds.Triggers, TourTimelineEvent),
            new ScenarioAuthoringHelpPage(TopicStory, "Story", "Story authoring stores quest definitions and scenario flow stages. Dialogue, options, rewards, removals, milestones, recruitment, and end outcomes are explicit draft data.", ScenarioStageKind.Quests, ScenarioAuthoringWindowIds.Quests, null),
            new ScenarioAuthoringHelpPage(TopicArtPixelEditor, "Art & Pixel Editor", "Art tools can replace existing target sprites, place scene sprites, import PNG assets, and edit custom sprite drafts before applying them to the scenario.", ScenarioStageKind.BunkerInside, ScenarioAuthoringWindowIds.PixelEditor, TourEditSprite),
            new ScenarioAuthoringHelpPage(TopicTest, "Test", "Playtest applies the current draft to the live world. If the draft changes after a playtest starts, saving reports that the running playtest predates recent edits.", ScenarioStageKind.Test, ScenarioAuthoringWindowIds.Scenario, null),
            new ScenarioAuthoringHelpPage(TopicPublish, "Publish", "Publish validates and exports the active draft. The export path uses the active scenario definition serializer and validator before writing a package.", ScenarioStageKind.Publish, ScenarioAuthoringWindowIds.Publish, null),
            new ScenarioAuthoringHelpPage(TopicBaseModes, "Base Modes", "Base mode changes update scenario metadata and selection availability. The current world is not transformed in place; reopen the draft to load the matching base scene.", ScenarioStageKind.Bunker, ScenarioAuthoringWindowIds.Scenario, null)
        };

        private static readonly ScenarioAuthoringTourDefinition[] Tours =
        {
            new ScenarioAuthoringTourDefinition(TourEditorBasics, "Editor Basics", new[]
            {
                new ScenarioAuthoringTourStep("window:" + ScenarioAuthoringWindowIds.Scenario, "Home", "Home is the workshop starting point for identity, base mode, progress, setup, and common actions.", "help.open." + TopicSetup),
                new ScenarioAuthoringTourStep("stage:" + ScenarioStageKind.Bunker, "World Tabs", "World tabs move between bunker, surface, backdrop, and map authoring without leaving the editor shell.", "stage.select." + ScenarioStageKind.Bunker),
                new ScenarioAuthoringTourStep("tool:" + ScenarioAuthoringTool.Select, "Selection Tool", "Selection focuses live scene targets and keeps authored changes scoped to the current workspace.", ScenarioAuthoringActionIds.ActionToolSelect),
                new ScenarioAuthoringTourStep("action:" + ScenarioAuthoringActionIds.ActionSave, "Save", "Save validates and writes the current draft XML from inside the shell.", null)
            }),
            new ScenarioAuthoringTourDefinition(TourPlaceFirstObject, "Place Your First Object", new[]
            {
                new ScenarioAuthoringTourStep("stage:" + ScenarioStageKind.BunkerInside, "Interior", "Use the inside bunker workspace for rooms, shelter objects, wiring, and interior placements.", "stage.select." + ScenarioStageKind.BunkerInside),
                new ScenarioAuthoringTourStep("tool:" + ScenarioAuthoringTool.Objects, "Objects Tool", "The Objects tool opens object placement authoring for the current bunker workspace.", ScenarioAuthoringActionIds.ActionToolObjects),
                new ScenarioAuthoringTourStep("window:" + ScenarioAuthoringWindowIds.TilesPalette, "Placement Palette", "The placement palette lists build and object authoring choices for the selected workspace.", "shell.window.toggle." + ScenarioAuthoringWindowIds.TilesPalette),
                new ScenarioAuthoringTourStep("action:" + ScenarioAuthoringActionIds.ActionSave, "Save Placement", "Save after the draft contains the placement changes you want to keep.", null)
            }),
            new ScenarioAuthoringTourDefinition(TourTimelineEvent, "Make A Timeline Event", new[]
            {
                new ScenarioAuthoringTourStep("stage:" + ScenarioStageKind.Events, "Events", "The Events workspace is where triggers, weather, gates, and scheduled actions are authored.", "stage.select." + ScenarioStageKind.Events),
                new ScenarioAuthoringTourStep("window:" + ScenarioAuthoringWindowIds.Triggers, "Timeline", "The timeline panel presents scheduled and trigger-driven draft entries.", "shell.window.toggle." + ScenarioAuthoringWindowIds.Triggers),
                new ScenarioAuthoringTourStep("action:" + ScenarioAuthoringActionIds.ActionTriggerAddScheduled, "Add Entry", "Scheduled triggers and actions become explicit draft data with scenario time fields.", null),
                new ScenarioAuthoringTourStep("action:" + ScenarioAuthoringActionIds.ActionSave, "Save Timeline", "Save after adding or editing timeline rules.", null)
            }),
            new ScenarioAuthoringTourDefinition(TourEditSprite, "Edit A Sprite", new[]
            {
                new ScenarioAuthoringTourStep("tool:" + ScenarioAuthoringTool.Assets, "Art Tool", "The Art tool switches to visual authoring for replacements, placements, imports, and custom sprite edits.", ScenarioAuthoringActionIds.ActionToolAssets),
                new ScenarioAuthoringTourStep("window:" + ScenarioAuthoringWindowIds.PixelEditor, "Pixel Editor", "The pixel editor window is used for custom sprite drafts when a supported target is being edited.", "shell.window.toggle." + ScenarioAuthoringWindowIds.PixelEditor),
                new ScenarioAuthoringTourStep("action:" + ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditStart, "Pixel Editor", "Custom sprite editing opens for supported selected targets before the edit is applied to the draft.", null),
                new ScenarioAuthoringTourStep("action:" + ScenarioAuthoringActionIds.ActionSave, "Save Art", "Save writes sprite references and generated asset metadata into the scenario draft.", null)
            })
        };

        public static TutorialStep[] GetSteps()
        {
            return Steps;
        }

        public static ScenarioAuthoringHelpPage[] GetHelpPages()
        {
            return HelpPages;
        }

        public static ScenarioAuthoringTourDefinition[] GetTours()
        {
            return Tours;
        }

        public static ScenarioAuthoringHelpPage FindHelpPage(string topicId)
        {
            for (int i = 0; HelpPages != null && i < HelpPages.Length; i++)
            {
                ScenarioAuthoringHelpPage page = HelpPages[i];
                if (page != null && string.Equals(page.Id, topicId, System.StringComparison.OrdinalIgnoreCase))
                    return page;
            }

            return null;
        }

        public static int FindHelpPageIndex(string topicId)
        {
            for (int i = 0; HelpPages != null && i < HelpPages.Length; i++)
            {
                ScenarioAuthoringHelpPage page = HelpPages[i];
                if (page != null && string.Equals(page.Id, topicId, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        public static ScenarioAuthoringTourDefinition FindTour(string tourId)
        {
            for (int i = 0; Tours != null && i < Tours.Length; i++)
            {
                ScenarioAuthoringTourDefinition tour = Tours[i];
                if (tour != null && string.Equals(tour.Id, tourId, System.StringComparison.OrdinalIgnoreCase))
                    return tour;
            }

            return null;
        }
    }
}
