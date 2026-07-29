using ParalivesAPI.Stable;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesGameFacade
    {
        private readonly ParalivesRuntimeInfo _runtime;

        internal ParalivesGameFacade(ParalivesRuntimeInfo runtime)
        {
            _runtime = runtime;
        }

        public bool IsReady
        {
            get { return global::Settings.Instance != null; }
        }

        public ParalivesApiVersion Version
        {
            get { return _runtime.Version; }
        }

        public string ApiVersion
        {
            get { return _runtime.ApiVersion; }
        }

        public string AdapterVersion
        {
            get { return _runtime.AdapterVersion; }
        }

        public string GameId
        {
            get { return _runtime.GameId; }
        }

        public string DisplayName
        {
            get { return _runtime.DisplayName; }
        }

        public ParalivesCapabilityRegistry Capabilities
        {
            get { return _runtime.Capabilities; }
        }

        public ParalivesCompatibilityFacade Compatibility
        {
            get { return _runtime.Compatibility; }
        }

        public ParalivesCompatibilityFacade Safe
        {
            get { return _runtime.Safe; }
        }

        public string[] CapabilityStrings
        {
            get { return _runtime.CapabilityStrings; }
        }

        public bool HasCapability(string capability)
        {
            return _runtime.HasCapability(capability);
        }

        public ParalivesSettingsFacade Settings
        {
            get { return _runtime.Settings; }
        }

        public ParalivesContentFacade Content
        {
            get { return _runtime.Content; }
        }

        public ParalivesPlayerFacade Players
        {
            get { return _runtime.Players; }
        }

        public ParalivesCharacterFacade Characters
        {
            get { return _runtime.Characters; }
        }

        public ParalivesRequirementFacade Requirements
        {
            get { return _runtime.Requirements; }
        }

        public ParalivesTimeFacade Time
        {
            get { return _runtime.Time; }
        }

        public ParalivesOccupationFacade Occupations
        {
            get { return _runtime.Occupations; }
        }

        public ParalivesOccupationRegistry OccupationRegistry
        {
            get { return _runtime.OccupationRegistry; }
        }

        public ParalivesOccupationEnrollmentFacade OccupationEnrollment
        {
            get { return _runtime.OccupationEnrollment; }
        }

        public ParalivesOccupationScheduleFacade OccupationSchedules
        {
            get { return _runtime.OccupationSchedules; }
        }

        public ParalivesOccupationTaskFacade OccupationTasks
        {
            get { return _runtime.OccupationTasks; }
        }

        public ParalivesOccupationUnlockableFacade OccupationUnlockables
        {
            get { return _runtime.OccupationUnlockables; }
        }

        public IParalivesOccupationPanelProviders OccupationPanelProviders
        {
            get { return _runtime.OccupationPanelProviders; }
        }

        public ParalivesSkillFacade Skills
        {
            get { return _runtime.Skills; }
        }

        public ParalivesInteractionQueueFacade Queues
        {
            get { return _runtime.Queues; }
        }

        public ParalivesWantFacade Wants
        {
            get { return _runtime.Wants; }
        }

        public ParalivesNeedsFacade Needs
        {
            get { return _runtime.Needs; }
        }

        public ParalivesStatusFacade Status
        {
            get { return _runtime.Status; }
        }

        public ParalivesRelationshipFacade Relationships
        {
            get { return _runtime.Relationships; }
        }

        public ParalivesPersonalityFacade Personality
        {
            get { return _runtime.Personality; }
        }

        public ParalivesPeopleFacade People
        {
            get { return _runtime.People; }
        }

        public ParalivesWorldFacade World
        {
            get { return _runtime.World; }
        }

        public ParalivesUiFacade Windows
        {
            get { return _runtime.Windows; }
        }
    }
}
