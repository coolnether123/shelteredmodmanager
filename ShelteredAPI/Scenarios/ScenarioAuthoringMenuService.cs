using ModAPI.Core;
using System.Text;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringMenuService
    {
        private const KeyCode ReopenShortcut = KeyCode.F6;
        private static readonly ScenarioAuthoringMenuService _instance = new ScenarioAuthoringMenuService();

        public static ScenarioAuthoringMenuService Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringMenuService()
        {
        }

        public void Update(ScenarioAuthoringSession session)
        {
            if (session == null)
                return;
        }

        public void Open(ScenarioAuthoringSession session, bool initial)
        {
            if (session == null)
                return;

            MMLog.WriteInfo("[ScenarioAuthoringMenu] " + BuildMessage(session, initial).Replace("\r", string.Empty).Replace("\n", " "));
        }

        private static string BuildMessage(ScenarioAuthoringSession session, bool initial)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(initial
                ? "Scenario authoring mode is now active."
                : "Scenario authoring help.");
            builder.Append("\n\n");
            builder.Append("This run started from a vanilla new game and is bound to a dedicated scenario draft save.\n");
            builder.Append("Simulation is frozen by the authoring runtime, not by Sheltered's pause menu.\n");
            builder.Append("Use the authoring shell for session details and editing tools.\n\n");
            builder.Append("Draft: ").Append(session.DraftId).Append("\n");
            builder.Append("Base Mode: ").Append(session.BaseMode).Append("\n\n");
            builder.Append("Controls:\n");
            builder.Append("- Hold Ctrl to enter selection mode.\n");
            builder.Append("- Left Click confirms the hovered target.\n");
            builder.Append("- Right Click clears the current selection.\n");
            builder.Append("- F5 saves the draft XML.\n");
            builder.Append("- F6 toggles the authoring shell.\n");
            builder.Append("- F7 toggles playtest mode.\n");
            return builder.ToString();
        }
    }
}
