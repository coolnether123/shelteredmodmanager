using System;
using System.Collections.Generic;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal sealed class ScenarioGlobalSearchRouteStep
    {
        private ScenarioGlobalSearchRouteStep(ScenarioAuthoringCommand command)
        {
            Command = command;
        }

        public ScenarioAuthoringCommand Command { get; private set; }
        public string AutomationId
        {
            get { return Command != null ? Command.AutomationId : string.Empty; }
        }

        public static ScenarioGlobalSearchRouteStep Typed(ScenarioAuthoringCommand command)
        {
            return command != null ? new ScenarioGlobalSearchRouteStep(command) : null;
        }
    }

    internal sealed class ScenarioGlobalSearchRouteCommand : ScenarioAuthoringCommand
    {
        public ScenarioGlobalSearchRouteCommand(ScenarioGlobalSearchRouteStep[] steps)
            : base(BuildAutomationId(steps), ScenarioAuthoringCommandPolicy.Default)
        {
            Steps = CopyValidSteps(steps);
        }

        public ScenarioGlobalSearchRouteStep[] Steps { get; private set; }

        private static string BuildAutomationId(ScenarioGlobalSearchRouteStep[] steps)
        {
            List<string> ids = new List<string>();
            for (int i = 0; steps != null && i < steps.Length; i++)
            {
                string id = steps[i] != null ? steps[i].AutomationId : null;
                if (!string.IsNullOrEmpty(id) && id.IndexOf('\n') < 0)
                    ids.Add(id);
            }
            return ids.Count == 0
                ? string.Empty
                : ScenarioAuthoringActionIds.ActionRendererGlobalSearchActivatePrefix
                    + ScenarioAutomationIdCodec.EncodeToken(string.Join("\n", ids.ToArray()));
        }

        private static ScenarioGlobalSearchRouteStep[] CopyValidSteps(ScenarioGlobalSearchRouteStep[] steps)
        {
            List<ScenarioGlobalSearchRouteStep> copy = new List<ScenarioGlobalSearchRouteStep>();
            for (int i = 0; steps != null && i < steps.Length; i++)
            {
                ScenarioGlobalSearchRouteStep step = steps[i];
                if (step != null
                    && !(step.Command is ScenarioGlobalSearchRouteCommand)
                    && !string.IsNullOrEmpty(step.AutomationId))
                {
                    copy.Add(step);
                }
            }
            return copy.ToArray();
        }
    }

}
