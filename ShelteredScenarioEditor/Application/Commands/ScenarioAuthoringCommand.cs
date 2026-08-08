using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal abstract class ScenarioAuthoringCommand
    {
        protected ScenarioAuthoringCommand(string automationId, ScenarioAuthoringCommandPolicy policy)
        {
            AutomationId = automationId ?? string.Empty;
            Policy = policy ?? ScenarioAuthoringCommandPolicy.Default;
        }

        public string AutomationId { get; private set; }
        public ScenarioAuthoringCommandPolicy Policy { get; private set; }
    }

    internal sealed class ScenarioAuthoringCommandPolicy
    {
        private static readonly ScenarioAuthoringCommandPolicy DefaultPolicy = new ScenarioAuthoringCommandPolicy(false, false, false);
        private static readonly ScenarioAuthoringCommandPolicy WorldPolicy = new ScenarioAuthoringCommandPolicy(true, false, false);
        private static readonly ScenarioAuthoringCommandPolicy SafetySnapshotPolicy = new ScenarioAuthoringCommandPolicy(false, false, true);
        private static readonly ScenarioAuthoringCommandPolicy WorldSafetySnapshotPolicy = new ScenarioAuthoringCommandPolicy(true, false, true);
        private static readonly ScenarioAuthoringCommandPolicy ReloadPolicy = new ScenarioAuthoringCommandPolicy(false, true, false);
        private static readonly ScenarioAuthoringCommandPolicy ReloadSafetySnapshotPolicy = new ScenarioAuthoringCommandPolicy(false, true, true);

        public ScenarioAuthoringCommandPolicy(bool requiresWorld, bool allowedDuringReload, bool createsSafetySnapshot)
        {
            RequiresWorld = requiresWorld;
            AllowedDuringReload = allowedDuringReload;
            CreatesSafetySnapshot = createsSafetySnapshot;
        }

        public static ScenarioAuthoringCommandPolicy Default { get { return DefaultPolicy; } }
        public static ScenarioAuthoringCommandPolicy World { get { return WorldPolicy; } }
        public static ScenarioAuthoringCommandPolicy SafetySnapshot { get { return SafetySnapshotPolicy; } }
        public static ScenarioAuthoringCommandPolicy WorldSafetySnapshot { get { return WorldSafetySnapshotPolicy; } }
        public static ScenarioAuthoringCommandPolicy Reload { get { return ReloadPolicy; } }
        public static ScenarioAuthoringCommandPolicy ReloadSafetySnapshot { get { return ReloadSafetySnapshotPolicy; } }
        public bool RequiresWorld { get; private set; }
        public bool AllowedDuringReload { get; private set; }
        public bool CreatesSafetySnapshot { get; private set; }
    }

    internal interface IScenarioCommandHandler
    {
        bool CanHandle(ScenarioAuthoringCommand command);
        ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command);
    }

    internal interface IScenarioTextValueCommand
    {
        ScenarioAuthoringCommand WithTextValue(string value);
    }
}
