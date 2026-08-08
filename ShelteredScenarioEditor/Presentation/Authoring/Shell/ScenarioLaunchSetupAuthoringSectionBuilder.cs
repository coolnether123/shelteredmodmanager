using System.Collections.Generic;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    internal static class ScenarioLaunchSetupAuthoringSectionBuilder
    {
        private static readonly string[] Ids = { ScenarioDifficultyCategoryIds.Rain, ScenarioDifficultyCategoryIds.Resources,
            ScenarioDifficultyCategoryIds.Breach, ScenarioDifficultyCategoryIds.Faction, ScenarioDifficultyCategoryIds.Mood,
            ScenarioDifficultyCategoryIds.MapSize, ScenarioDifficultyCategoryIds.Fog };
        private static readonly string[] Labels = { "Rain", "Map resources", "Breach frequency", "Faction density",
            "Populace mood", "Map size", "Fog of war" };

        public static ScenarioAuthoringInspectorSection Build(ScenarioDefinition definition)
        {
            ScenarioLaunchSetupDefinition setup = definition != null ? definition.LaunchSetup : null;
            if (setup == null)
                setup = ScenarioLaunchSetupDefinition.CreateDefault();
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Text("Choose what players see after PLAY. Full Setup preserves vanilla; Direct uses the authored family and difficulty immediately; Guided locks selected difficulty rows."));
            AddMode(items, setup.Mode, ScenarioLaunchSetupMode.FullSetup, "Full Setup", "Keep every vanilla setup screen.");
            AddMode(items, setup.Mode, ScenarioLaunchSetupMode.Direct, "Direct / No setup", "Enter the authored run without setup screens.");
            AddMode(items, setup.Mode, ScenarioLaunchSetupMode.Guided, "Guided", "Let players change only categories you leave unlocked.");

            if (setup.Mode != ScenarioLaunchSetupMode.FullSetup)
            {
                for (int i = 0; i < Ids.Length; i++)
                {
                    ScenarioDifficultyCategoryDefinition category = Find(setup, Ids[i]);
                    int value = category != null ? category.AuthoredValue : (Ids[i] == ScenarioDifficultyCategoryIds.MapSize || Ids[i] == ScenarioDifficultyCategoryIds.Fog ? 0 : 1);
                    string state = FormatValue(Ids[i], value);
                    if (setup.Mode == ScenarioLaunchSetupMode.Guided)
                        state += category == null || category.PlayerSelectable ? " · player choice" : " · authored / locked";
                    items.Add(Item.Property(Labels[i], state));
                    items.Add(Item.ActionItem(Item.Action(ShellUxCommand.LaunchValue(Ids[i], -1), "-", "Use the previous authored value.", true, false, "-")));
                    items.Add(Item.ActionItem(Item.Action(ShellUxCommand.LaunchValue(Ids[i], 1), "+", "Use the next authored value.", true, false, "+")));
                    if (setup.Mode == ScenarioLaunchSetupMode.Guided)
                    {
                        bool selectable = category == null || category.PlayerSelectable;
                        items.Add(Item.ActionItem(Item.Action(ShellUxCommand.LaunchSelectable(Ids[i]), selectable ? "Player choice" : "Authored lock",
                            selectable ? "Lock this value to the scenario." : "Let the player change this value.", true, !selectable, "LK")));
                    }
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "publish_play_experience",
                Title = "Play Experience",
                Expanded = false,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static void AddMode(List<ScenarioAuthoringInspectorItem> items, ScenarioLaunchSetupMode current, ScenarioLaunchSetupMode mode, string label, string hint)
        {
            items.Add(Item.ActionItem(Item.Action(ShellUxCommand.SetLaunchMode(mode), label, hint, true, current == mode, "PLAY")));
        }

        private static ScenarioDifficultyCategoryDefinition Find(ScenarioLaunchSetupDefinition setup, string id)
        {
            for (int i = 0; setup != null && setup.Categories != null && i < setup.Categories.Count; i++)
                if (setup.Categories[i] != null && string.Equals(setup.Categories[i].Id, id, System.StringComparison.OrdinalIgnoreCase)) return setup.Categories[i];
            return null;
        }

        private static string FormatValue(string id, int value)
        {
            if (id == ScenarioDifficultyCategoryIds.Fog) return value == 0 ? "Off" : "On";
            if (id == ScenarioDifficultyCategoryIds.MapSize) return new[] { "Normal", "Hard", "Hardcore" }[Clamp(value, 0, 2)];
            return new[] { "Easy", "Normal", "Hard", "Hardcore" }[Clamp(value, 0, 3)];
        }

        private static int Clamp(int value, int min, int max) { return value < min ? min : value > max ? max : value; }
    }
}
