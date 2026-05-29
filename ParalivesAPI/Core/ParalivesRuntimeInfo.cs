namespace ParalivesAPI.Core
{
    public sealed class ParalivesRuntimeInfo
    {
        public const string RegistryId = "GameRuntime.Paralives";

        public static readonly ParalivesRuntimeInfo Current = new ParalivesRuntimeInfo();

        private ParalivesRuntimeInfo()
        {
            Localizations = new ParalivesLocalizationRegistry();
            Interactions = new ParalivesInteractionRegistry();
            Notifications = new ParalivesNotificationRegistry();
            InteractionSelections = new ParalivesInteractionSelectionDispatcher();
            ActionCompletions = new ParalivesActionCompletionDispatcher();
            Settings = new ParalivesSettingsFacade();
            Players = new ParalivesPlayerFacade();
            Characters = new ParalivesCharacterFacade(Players);
            Time = new ParalivesTimeFacade();
            Occupations = new ParalivesOccupationFacade(Characters);
            Skills = new ParalivesSkillFacade(Characters);
            Queues = new ParalivesInteractionQueueFacade(Characters);
            Wants = new ParalivesWantFacade(Characters, Settings);
            Needs = new ParalivesNeedsFacade(Characters, Settings);
            Status = new ParalivesStatusFacade(Characters, Settings);
            Statuses = Status;
            Relationships = new ParalivesRelationshipFacade(Characters, Settings);
            Personality = new ParalivesPersonalityFacade(Characters, Settings);
            Memories = new ParalivesMemoryFacade(Characters);
            Goals = new ParalivesGoalFacade(Characters, Settings);
            Social = new ParalivesSocialFacade();
            Together = new ParalivesTogetherFacade(Social);
            World = new ParalivesWorldFacade();
            Windows = new ParalivesUiFacade(Characters);
            AttendancePolicies = new ParalivesAttendancePolicyRegistry(Occupations);
            People = new ParalivesPeopleFacade(Characters, Players, Queues, Occupations);
            Game = new ParalivesGameFacade(this);
        }

        public string GameId
        {
            get { return "paralives"; }
        }

        public string DisplayName
        {
            get { return "Paralives"; }
        }

        public ParalivesInteractionRegistry Interactions { get; private set; }

        public ParalivesLocalizationRegistry Localizations { get; private set; }

        public ParalivesNotificationRegistry Notifications { get; private set; }

        public ParalivesInteractionSelectionDispatcher InteractionSelections { get; private set; }

        public ParalivesActionCompletionDispatcher ActionCompletions { get; private set; }

        public ParalivesGameFacade Game { get; private set; }

        public ParalivesSettingsFacade Settings { get; private set; }

        public ParalivesPlayerFacade Players { get; private set; }

        public ParalivesCharacterFacade Characters { get; private set; }

        public ParalivesTimeFacade Time { get; private set; }

        public ParalivesOccupationFacade Occupations { get; private set; }

        public ParalivesSkillFacade Skills { get; private set; }

        public ParalivesInteractionQueueFacade Queues { get; private set; }

        public ParalivesWantFacade Wants { get; private set; }

        public ParalivesNeedsFacade Needs { get; private set; }

        public ParalivesStatusFacade Status { get; private set; }

        public ParalivesStatusFacade Statuses { get; private set; }

        public ParalivesRelationshipFacade Relationships { get; private set; }

        public ParalivesPersonalityFacade Personality { get; private set; }

        public ParalivesMemoryFacade Memories { get; private set; }

        public ParalivesMemoryFacade Memory
        {
            get { return Memories; }
        }

        public ParalivesGoalFacade Goals { get; private set; }

        public ParalivesPeopleFacade People { get; private set; }

        public ParalivesSocialFacade Social { get; private set; }

        public ParalivesTogetherFacade Together { get; private set; }

        public ParalivesWorldFacade World { get; private set; }

        public ParalivesUiFacade Windows { get; private set; }

        public ParalivesAttendancePolicyRegistry AttendancePolicies { get; private set; }
    }
}
