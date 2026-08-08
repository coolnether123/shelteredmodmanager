using System;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    /// <summary>
    /// Progressive-disclosure rules for the story stage editor. Advanced routing (alternate
    /// routes, random targets, and their weights) stays hidden until a stage actually has
    /// basic dialogue content, so a fresh stage reads as "write your scene first" rather than
    /// a wall of routing steppers. Kept as a tiny pure helper so the rule is testable in one
    /// place and both the main page and the focused editor agree.
    /// </summary>
    internal static class ScenarioStoryStageDisclosure
    {
        /// <summary>True when at least one scene in the stage has a written dialogue line.</summary>
        public static bool HasBasicDialogue(ScenarioFlowStageDefinition stage)
        {
            if (stage == null || stage.IntercomStages == null)
                return false;
            for (int s = 0; s < stage.IntercomStages.Count; s++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[s];
                if (step == null || step.Dialogue == null)
                    continue;
                for (int d = 0; d < step.Dialogue.Count; d++)
                {
                    ScenarioDialogueLineDefinition line = step.Dialogue[d];
                    if (line != null && !string.IsNullOrEmpty(line.TextKey))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether advanced routing controls should be revealed for this stage. They stay
        /// collapsed until the stage has basic dialogue content.
        /// </summary>
        public static bool ShouldRevealAdvancedRouting(ScenarioFlowStageDefinition stage)
        {
            return HasBasicDialogue(stage);
        }
    }
}
