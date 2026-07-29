using ParalivesAPI.Stable;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesRuntimeInfo
    {
        public const string RegistryId = "GameRuntime.Paralives";

        public static readonly ParalivesRuntimeInfo Current = new ParalivesRuntimeInfo();

        private ParalivesRuntimeInfo()
        {
            Version = ParalivesApiVersion.Current;
            Capabilities = ParalivesCapabilityRegistry.CreateDefault();
            Compatibility = new ParalivesCompatibilityFacade();
            Safe = Compatibility;
            Localizations = new ParalivesLocalizationRegistry();
            Interactions = new ParalivesInteractionRegistry();
            Notifications = new ParalivesNotificationRegistry();
            InteractionSelections = new ParalivesInteractionSelectionDispatcher();
            ActionCompletions = new ParalivesActionCompletionDispatcher();
            ActionLifecycle = new ParalivesActionLifecycleFacade(ActionCompletions);
            Settings = new ParalivesSettingsFacade();
            Players = new ParalivesPlayerFacade();
            Characters = new ParalivesCharacterFacade(Players);
            Content = Settings.Content;
            Requirements = Characters.Requirements;
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
            Occupations.AttachRuntimeServices(AttendancePolicies, Windows);
            People = new ParalivesPeopleFacade(Characters, Players, Queues, Occupations);
            Game = new ParalivesGameFacade(this);
        }

        public string GameId
        {
            get { return Version.GameId; }
        }

        public string DisplayName
        {
            get { return Version.DisplayName; }
        }

        public ParalivesApiVersion Version { get; private set; }

        public string ApiVersion
        {
            get { return Version.ApiVersion; }
        }

        public string AdapterVersion
        {
            get { return Version.AdapterVersion; }
        }

        public ParalivesCapabilityRegistry Capabilities { get; private set; }

        public ParalivesCompatibilityFacade Compatibility { get; private set; }

        public ParalivesCompatibilityFacade Safe { get; private set; }

        public string[] CapabilityStrings
        {
            get { return Capabilities.CapabilityStrings; }
        }

        public bool HasCapability(string capability)
        {
            return Capabilities.HasCapability(capability);
        }

        public ParalivesInteractionRegistry Interactions { get; private set; }

        public ParalivesLocalizationRegistry Localizations { get; private set; }

        public ParalivesNotificationRegistry Notifications { get; private set; }

        public ParalivesInteractionSelectionDispatcher InteractionSelections { get; private set; }

        public ParalivesActionCompletionDispatcher ActionCompletions { get; private set; }

        public ParalivesActionLifecycleFacade ActionLifecycle { get; private set; }

        public ParalivesGameFacade Game { get; private set; }

        public ParalivesSettingsFacade Settings { get; private set; }

        public ParalivesContentFacade Content { get; private set; }

        public ParalivesPlayerFacade Players { get; private set; }

        public ParalivesCharacterFacade Characters { get; private set; }

        public ParalivesRequirementFacade Requirements { get; private set; }

        public ParalivesTimeFacade Time { get; private set; }

        public ParalivesOccupationFacade Occupations { get; private set; }

        public ParalivesOccupationRegistry OccupationRegistry
        {
            get { return Occupations.Registry; }
        }

        public ParalivesOccupationEnrollmentFacade OccupationEnrollment
        {
            get { return Occupations.Enrollment; }
        }

        public ParalivesOccupationScheduleFacade OccupationSchedules
        {
            get { return Occupations.Schedules; }
        }

        public ParalivesOccupationTaskFacade OccupationTasks
        {
            get { return Occupations.Tasks; }
        }

        public ParalivesOccupationUnlockableFacade OccupationUnlockables
        {
            get { return Occupations.Unlockables; }
        }

        public IParalivesOccupationPanelProviders OccupationPanelProviders
        {
            get { return Occupations.PanelProviders; }
        }

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
