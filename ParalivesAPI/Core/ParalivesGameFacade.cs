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

        public ParalivesSettingsFacade Settings
        {
            get { return _runtime.Settings; }
        }

        public ParalivesPlayerFacade Players
        {
            get { return _runtime.Players; }
        }

        public ParalivesCharacterFacade Characters
        {
            get { return _runtime.Characters; }
        }

        public ParalivesTimeFacade Time
        {
            get { return _runtime.Time; }
        }

        public ParalivesOccupationFacade Occupations
        {
            get { return _runtime.Occupations; }
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
