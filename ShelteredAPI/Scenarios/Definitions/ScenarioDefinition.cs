using System.Collections.Generic;
using System;

using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Domain.Assets;
using ShelteredAPI.Scenarios.Domain.Bunker;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Definitions{
    /// <summary>
    /// Base Sheltered mode a scenario is authored against.
    /// The mode controls default availability and runtime assumptions for scenario selection.
    /// </summary>
    public enum ScenarioBaseGameMode
    {
        Survival = 0,
        Surrounded = 1,
        Stasis = 2
    }

    /// <summary>Stable values accepted by <see cref="ScenarioDefinition.BaseFamilyChoice"/>.</summary>
    public static class ScenarioBaseFamilyChoices
    {
        /// <summary>Keep the cast already present in the selected base-mode world.</summary>
        public const string KeepCurrentCast = "KeepCurrentCast";
        /// <summary>Restore the default family supplied by the selected base mode.</summary>
        public const string UseBaseDefaultFamily = "UseBaseDefaultFamily";
    }

    /// <summary>
    /// Gender selector for authored family members or NPCs.
    /// Use <see cref="Any"/> when the runtime should keep or choose the default value.
    /// </summary>
    public enum ScenarioGender
    {
        Any = 0,
        Female = 1,
        Male = 2
    }

    /// <summary>
    /// Optional actor-system reference for scenario-authored people and actor-bound conditions or effects.
    /// Actor identity is tried first, then binding, then scenario-owned synthetic fallback.
    /// </summary>
    public sealed class ScenarioActorRef
    {
        public string Kind { get; set; }
        public int LocalId { get; set; }
        public string Domain { get; set; }
        public string BindingType { get; set; }
        public string BindingKey { get; set; }
        public string DisplayNameFallback { get; set; }
        public string RequiredModId { get; set; }
    }

    /// <summary>
    /// Persisted actor component payload envelope. Unknown component payloads stay as JSON text so
    /// scenarios can round-trip when the owning mod is temporarily unavailable.
    /// </summary>
    public sealed class ScenarioActorComponentDefinition
    {
        public string ComponentId { get; set; }
        public string OwnerModId { get; set; }
        public int Version { get; set; }
        public string PayloadJson { get; set; }
    }

    /// <summary>
    /// Persistent scenario definition. This type is deliberately a neutral data holder:
    /// it must not grow Sheltered or Unity references, because mod tools and the editor
    /// need to read scenario packs without booting a game scene.
    /// </summary>
    public class ScenarioDefinition
    {
        public ScenarioDefinition()
        {
            Tags = new List<string>();
            Dependencies = new List<string>();
            ModDependencies = new List<ScenarioModDependencyDefinition>();
            BaseGameMode = ScenarioBaseGameMode.Survival;
            SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(BaseGameMode);
            ScenarioCharacters = new List<ScenarioNpcDefinition>();
            ScenarioFlow = new ScenarioFlowDefinition();
            FamilySetup = new FamilySetupDefinition();
            LaunchSetup = ScenarioLaunchSetupDefinition.CreateDefault();
            StartingInventory = new StartingInventoryDefinition();
            BunkerEdits = new BunkerEditsDefinition();
            TriggersAndEvents = new TriggersAndEventsDefinition();
            Quests = new QuestAuthoringDefinition();
            Map = new MapAuthoringDefinition();
            WinLossConditions = new WinLossConditionsDefinition();
            Scoring = new ScenarioScoringDefinition();
            AssetReferences = new AssetReferencesDefinition();
            BunkerGrid = new ScenarioBunkerGridDefinition();
            BackendWorlds = new ScenarioBackendWorldsDefinition();
            Gates = new List<ScenarioGateDefinition>();
            ScheduledActions = new List<ScenarioScheduledActionDefinition>();
            Journal = new JournalDefinition();
            Conversations = new ScenarioConversationAuthoringDefinition();
            VanillaSuppression = new ScenarioVanillaSuppressionDefinition();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Goal { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        /// <summary>Optional attribution or licensing credits included with the scenario package.</summary>
        public string Credits { get; set; }
        /// <summary>Searchable author-defined labels serialized with the scenario.</summary>
        public List<string> Tags { get; private set; }
        public List<string> Dependencies { get; private set; }
        public List<ScenarioModDependencyDefinition> ModDependencies { get; private set; }
        public ScenarioBaseGameMode BaseGameMode { get; set; }
        /// <summary>Stable family-source choice used when starting or reloading the authored base mode.</summary>
        public string BaseFamilyChoice { get; set; }
        public long? SeedOverride { get; set; }
        public ScenarioSelectionRulesDefinition SelectionRules { get; set; }
        public List<ScenarioNpcDefinition> ScenarioCharacters { get; private set; }
        public ScenarioFlowDefinition ScenarioFlow { get; set; }
        public FamilySetupDefinition FamilySetup { get; set; }
        /// <summary>Controls the player-facing difficulty and family setup flow when this scenario is launched.</summary>
        public ScenarioLaunchSetupDefinition LaunchSetup { get; set; }
        public StartingInventoryDefinition StartingInventory { get; set; }
        public BunkerEditsDefinition BunkerEdits { get; set; }
        public TriggersAndEventsDefinition TriggersAndEvents { get; set; }
        public QuestAuthoringDefinition Quests { get; set; }
        public MapAuthoringDefinition Map { get; set; }
        public WinLossConditionsDefinition WinLossConditions { get; set; }
        public ScenarioScoringDefinition Scoring { get; set; }
        public AssetReferencesDefinition AssetReferences { get; set; }
        public ScenarioBunkerGridDefinition BunkerGrid { get; set; }
        /// <summary>Advanced authored state for alternate Sheltered world backends.</summary>
        public ScenarioBackendWorldsDefinition BackendWorlds { get; set; }
        public List<ScenarioGateDefinition> Gates { get; private set; }
        public List<ScenarioScheduledActionDefinition> ScheduledActions { get; private set; }
        public JournalDefinition Journal { get; set; }
        public ScenarioConversationAuthoringDefinition Conversations { get; set; }
        public ScenarioVanillaSuppressionDefinition VanillaSuppression { get; set; }
    }

    /// <summary>Player setup flow used when a published custom scenario starts.</summary>
    public enum ScenarioLaunchSetupMode
    {
        /// <summary>Keep Sheltered's complete difficulty and family customisation flow.</summary>
        FullSetup = 0,
        /// <summary>Skip setup and enter the authored scenario immediately.</summary>
        Direct = 1,
        /// <summary>Show setup while author-fixed difficulty categories remain locked.</summary>
        Guided = 2
    }

    /// <summary>Stable identifiers for Sheltered's vanilla difficulty categories.</summary>
    public static class ScenarioDifficultyCategoryIds
    {
        /// <summary>Rain frequency category (values 0-3).</summary>
        public const string Rain = "rain";
        /// <summary>Map resource abundance category (values 0-3).</summary>
        public const string Resources = "resources";
        /// <summary>Shelter breach frequency category (values 0-3).</summary>
        public const string Breach = "breach";
        /// <summary>Faction density category (values 0-3).</summary>
        public const string Faction = "faction";
        /// <summary>Populace mood category (values 0-3).</summary>
        public const string Mood = "mood";
        /// <summary>Expedition map size category (values 0-2).</summary>
        public const string MapSize = "map-size";
        /// <summary>Fog-of-war category (0 off, 1 on).</summary>
        public const string Fog = "fog";

        /// <summary>Returns whether an id names a vanilla difficulty category supported by this API version.</summary>
        public static bool IsKnown(string id)
        {
            return string.Equals(id, Rain, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, Resources, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, Breach, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, Faction, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, Mood, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, MapSize, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, Fog, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>An authored value and whether Guided mode lets the player change it.</summary>
    public sealed class ScenarioDifficultyCategoryDefinition
    {
        /// <summary>Stable id from <see cref="ScenarioDifficultyCategoryIds"/>.</summary>
        public string Id { get; set; }
        /// <summary>Vanilla integer setting selected by the author.</summary>
        public int AuthoredValue { get; set; }
        /// <summary>Whether Guided mode lets the player change this category.</summary>
        public bool PlayerSelectable { get; set; }
    }

    /// <summary>Scenario-authored policy for the vanilla launch setup experience.</summary>
    public sealed class ScenarioLaunchSetupDefinition
    {
        /// <summary>Creates an empty FullSetup policy; prefer <see cref="CreateDefault"/> for the complete vanilla category inventory.</summary>
        public ScenarioLaunchSetupDefinition()
        {
            Mode = ScenarioLaunchSetupMode.FullSetup;
            Categories = new List<ScenarioDifficultyCategoryDefinition>();
        }

        /// <summary>Setup flow used when a player chooses PLAY.</summary>
        public ScenarioLaunchSetupMode Mode { get; set; }
        /// <summary>Authored values and Guided-mode selection policy, keyed by stable category id.</summary>
        public List<ScenarioDifficultyCategoryDefinition> Categories { get; private set; }

        /// <summary>Creates a FullSetup policy containing all vanilla categories at Normal defaults.</summary>
        public static ScenarioLaunchSetupDefinition CreateDefault()
        {
            ScenarioLaunchSetupDefinition setup = new ScenarioLaunchSetupDefinition();
            setup.Categories.Add(Category(ScenarioDifficultyCategoryIds.Rain, 1));
            setup.Categories.Add(Category(ScenarioDifficultyCategoryIds.Resources, 1));
            setup.Categories.Add(Category(ScenarioDifficultyCategoryIds.Breach, 1));
            setup.Categories.Add(Category(ScenarioDifficultyCategoryIds.Faction, 1));
            setup.Categories.Add(Category(ScenarioDifficultyCategoryIds.Mood, 1));
            setup.Categories.Add(Category(ScenarioDifficultyCategoryIds.MapSize, 0));
            setup.Categories.Add(Category(ScenarioDifficultyCategoryIds.Fog, 0));
            return setup;
        }

        private static ScenarioDifficultyCategoryDefinition Category(string id, int value)
        {
            return new ScenarioDifficultyCategoryDefinition { Id = id, AuthoredValue = value, PlayerSelectable = true };
        }
    }

    public sealed class ScenarioVanillaSuppressionDefinition
    {
        public bool RandomVisitors { get; set; }
        public bool Binman { get; set; }
        public bool Raids { get; set; }
        public bool StasisVisitors { get; set; }
        public bool RadioBroadcastOdds { get; set; }
    }

    public enum ScenarioConversationTriggerSource
    {
        Random = 0,
        Event = 1,
        Timeline = 2
    }

    public enum ScenarioConversationParticipantFallback
    {
        None = 0,
        AnyFamily = 1,
        NearestIdleFamily = 2,
        Initiator = 3,
        Partner = 4
    }

    public sealed class ScenarioConversationAuthoringDefinition
    {
        public ScenarioConversationAuthoringDefinition()
        {
            Settings = new ScenarioConversationSuppressionDefinition();
            Conversations = new List<ScenarioConversationDefinition>();
        }

        public ScenarioConversationSuppressionDefinition Settings { get; set; }
        public List<ScenarioConversationDefinition> Conversations { get; private set; }
    }

    public sealed class ScenarioConversationSuppressionDefinition
    {
        public ScenarioConversationSuppressionDefinition()
        {
            SuppressedVanillaCategories = new List<string>();
            SuppressedVanillaTopicKeys = new List<string>();
        }

        public bool SuppressVanillaRandomChatter { get; set; }
        public List<string> SuppressedVanillaCategories { get; private set; }
        public List<string> SuppressedVanillaTopicKeys { get; private set; }
    }

    public sealed class ScenarioConversationDefinition
    {
        public ScenarioConversationDefinition()
        {
            Trigger = new ScenarioConversationTriggerDefinition();
            Participants = new List<ScenarioConversationParticipantDefinition>();
            Conditions = new List<ScenarioConditionRef>();
            Lines = new List<ScenarioConversationLineDefinition>();
            Tags = new List<string>();
        }

        public string Id { get; set; }
        public ScenarioConversationTriggerDefinition Trigger { get; set; }
        public List<ScenarioConversationParticipantDefinition> Participants { get; private set; }
        public List<ScenarioConditionRef> Conditions { get; private set; }
        public List<ScenarioConversationLineDefinition> Lines { get; private set; }
        public List<string> Tags { get; private set; }
    }

    public sealed class ScenarioConversationTriggerDefinition
    {
        public ScenarioConversationTriggerDefinition()
        {
            Source = ScenarioConversationTriggerSource.Random;
            Weight = 1f;
            Time = new ScenarioScheduleTime();
        }

        public ScenarioConversationTriggerSource Source { get; set; }
        public float Weight { get; set; }
        public string TriggerId { get; set; }
        public float CooldownDays { get; set; }
        public bool Once { get; set; }
        public ScenarioScheduleTime Time { get; set; }
    }

    public sealed class ScenarioConversationParticipantDefinition
    {
        public ScenarioConversationParticipantDefinition()
        {
            Required = true;
            Fallback = ScenarioConversationParticipantFallback.None;
        }

        public string Slot { get; set; }
        public string StoryCharacterId { get; set; }
        public ScenarioActorRef ActorRef { get; set; }
        public ScenarioConversationParticipantFallback Fallback { get; set; }
        public bool Required { get; set; }
    }

    public sealed class ScenarioConversationLineDefinition
    {
        public string SpeakerSlot { get; set; }
        public string TextKey { get; set; }
        public string RawText { get; set; }
        public float DelaySeconds { get; set; }
    }

    /// <summary>Authored bunker/world state keyed by Sheltered base mode.</summary>
    public sealed class ScenarioBackendWorldsDefinition
    {
        public ScenarioBackendWorldsDefinition()
        {
            Worlds = new List<ScenarioBackendWorldDefinition>();
        }

        public List<ScenarioBackendWorldDefinition> Worlds { get; private set; }

        public ScenarioBackendWorldDefinition GetOrCreate(ScenarioBaseGameMode baseMode)
        {
            ScenarioBackendWorldDefinition world = Find(baseMode);
            if (world != null)
                return world;

            world = new ScenarioBackendWorldDefinition();
            world.BaseMode = baseMode;
            Worlds.Add(world);
            return world;
        }

        public ScenarioBackendWorldDefinition Find(ScenarioBaseGameMode baseMode)
        {
            for (int i = 0; Worlds != null && i < Worlds.Count; i++)
            {
                ScenarioBackendWorldDefinition world = Worlds[i];
                if (world != null && world.BaseMode == baseMode)
                    return world;
            }

            return null;
        }
    }

    /// <summary>Authored bunker and scene-placement state for one Sheltered base mode.</summary>
    public sealed class ScenarioBackendWorldDefinition
    {
        public ScenarioBackendWorldDefinition()
        {
            BunkerEdits = new BunkerEditsDefinition();
            BunkerGrid = new ScenarioBunkerGridDefinition();
            SceneSpritePlacements = new List<SceneSpritePlacement>();
        }

        public ScenarioBaseGameMode BaseMode { get; set; }
        public BunkerEditsDefinition BunkerEdits { get; set; }
        public ScenarioBunkerGridDefinition BunkerGrid { get; set; }
        public List<SceneSpritePlacement> SceneSpritePlacements { get; private set; }
    }

    /// <summary>
    /// Selection availability flags by base game mode.
    /// Use this to restrict a scenario to Survival, Surrounded, Stasis, or a combination.
    /// </summary>
    public class ScenarioModeAvailabilityDefinition
    {
        public bool Survival { get; set; }
        public bool Surrounded { get; set; }
        public bool Stasis { get; set; }

        public void UseOnly(ScenarioBaseGameMode baseMode)
        {
            Survival = baseMode == ScenarioBaseGameMode.Survival;
            Surrounded = baseMode == ScenarioBaseGameMode.Surrounded;
            Stasis = baseMode == ScenarioBaseGameMode.Stasis;
        }
    }

    /// <summary>
    /// Rules that influence when and how often the scenario can appear in scenario selection.
    /// These values are authoring metadata; the runtime still owns final spawn timing.
    /// </summary>
    public class ScenarioSelectionRulesDefinition
    {
        public ScenarioSelectionRulesDefinition()
        {
            Weight = 1f;
            MaxSimultaneousInstances = 1;
            Availability = new ScenarioModeAvailabilityDefinition();
            PrerequisiteMilestones = new List<string>();
        }

        public float Weight { get; set; }
        public int StartDay { get; set; }
        public int TimeoutDays { get; set; }
        public int MaxSimultaneousInstances { get; set; }
        public bool OnceOnly { get; set; }
        public bool DiscoverByRadio { get; set; }
        public ScenarioModeAvailabilityDefinition Availability { get; set; }
        public List<string> PrerequisiteMilestones { get; private set; }

        public static ScenarioSelectionRulesDefinition ForBaseMode(ScenarioBaseGameMode baseMode)
        {
            ScenarioSelectionRulesDefinition rules = new ScenarioSelectionRulesDefinition();
            rules.Availability.UseOnly(baseMode);
            return rules;
        }
    }

    /// <summary>
    /// Authored NPC or scenario character definition.
    /// IDs in this object are scenario-local and are resolved when building vanilla scenario stages.
    /// </summary>
    public class ScenarioNpcDefinition
    {
        public ScenarioNpcDefinition()
        {
            WeaponItemId = "Weapon_Fists";
            EquippedItem1Id = "Undefined";
            EquippedItem2Id = "Undefined";
            StatSetting = "Random_Low";
            Stats = new ScenarioNpcStatsDefinition();
            CarriedItems = new List<ItemEntry>();
            ActorComponents = new List<ScenarioActorComponentDefinition>();
        }

        public string CharacterId { get; set; }
        public string DisplayName { get; set; }
        public ScenarioActorRef ActorRef { get; set; }
        public List<ScenarioActorComponentDefinition> ActorComponents { get; private set; }
        public string PresetId { get; set; }
        public string WeaponItemId { get; set; }
        public string EquippedItem1Id { get; set; }
        public string EquippedItem2Id { get; set; }
        public string Personality { get; set; }
        public int NumRandomItems { get; set; }
        public List<ItemEntry> CarriedItems { get; private set; }
        public string StatSetting { get; set; }
        public ScenarioNpcStatsDefinition Stats { get; set; }
        public bool BackgroundNpc { get; set; }
        public bool FlipMesh { get; set; }
        public string Species { get; set; }
        public string AvatarOverrideSpriteId { get; set; }
    }

    /// <summary>
    /// Explicit stat levels for an authored NPC.
    /// Leave values at zero when the selected preset or stat setting should decide them.
    /// </summary>
    public class ScenarioNpcStatsDefinition
    {
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Charisma { get; set; }
        public int Perception { get; set; }
        public int Intelligence { get; set; }
    }

    /// <summary>
    /// Ordered scenario conversation and encounter flow.
    /// Stages are authored as neutral data and later converted into Sheltered ScenarioDef stages.
    /// </summary>
    public class ScenarioFlowDefinition
    {
        public ScenarioFlowDefinition()
        {
            Stages = new List<ScenarioFlowStageDefinition>();
        }

        public List<ScenarioFlowStageDefinition> Stages { get; private set; }
    }

    /// <summary>
    /// One named stage in the scenario flow.
    /// It groups participating characters, intercom stages, and fallback behavior for unanswered calls.
    /// </summary>
    public class ScenarioFlowStageDefinition
    {
        public ScenarioFlowStageDefinition()
        {
            CharacterIds = new List<string>();
            IntercomStages = new List<ScenarioIntercomStageDefinition>();
            UnansweredNextDays = 1;
        }

        public string Id { get; set; }
        public List<string> CharacterIds { get; private set; }
        public List<ScenarioIntercomStageDefinition> IntercomStages { get; private set; }
        public string UnansweredNextStage { get; set; }
        public int UnansweredNextDays { get; set; }
        public bool PunishOnUnanswered { get; set; }
    }

    /// <summary>
    /// One intercom or encounter conversation step.
    /// Options, item rewards, milestones, and stage changes describe what happens when this step resolves.
    /// </summary>
    public class ScenarioIntercomStageDefinition
    {
        public ScenarioIntercomStageDefinition()
        {
            Type = "Standard";
            Dialogue = new List<ScenarioDialogueLineDefinition>();
            Options = new List<ScenarioDialogueOptionDefinition>();
            RandomizedNextIds = new List<string>();
            Items = new List<ItemEntry>();
            ItemsToRemove = new List<ItemEntry>();
            SubquestsToActivate = new List<string>();
            SetMilestones = new List<ScenarioMilestoneDefinition>();
            CheckMilestones = new List<ScenarioMilestoneCheckDefinition>();
            SubquestCheck = new ScenarioSubquestCheckDefinition();
            StageChange = new ScenarioStageChangeDefinition();
            EndOptions = new ScenarioEncounterEndOptionsDefinition();
            CharacterIdsToRecruit = new List<string>();
        }

        public string Id { get; set; }
        public string Type { get; set; }
        public string NextId { get; set; }
        public string AlternateNextId { get; set; }
        public List<ScenarioDialogueLineDefinition> Dialogue { get; private set; }
        public List<ScenarioDialogueOptionDefinition> Options { get; private set; }
        public List<string> RandomizedNextIds { get; private set; }
        public List<ItemEntry> Items { get; private set; }
        public List<ItemEntry> ItemsToRemove { get; private set; }
        public ScenarioEncounterEndOptionsDefinition EndOptions { get; set; }
        public List<string> SubquestsToActivate { get; private set; }
        public ScenarioSubquestCheckDefinition SubquestCheck { get; set; }
        public List<ScenarioMilestoneDefinition> SetMilestones { get; private set; }
        public List<ScenarioMilestoneCheckDefinition> CheckMilestones { get; private set; }
        public ScenarioStageChangeDefinition StageChange { get; set; }
        public string StageDescriptionKey { get; set; }
        public List<string> CharacterIdsToRecruit { get; private set; }
        public bool RecruitAsFamily { get; set; }
    }

    /// <summary>
    /// One authored dialogue line identified by speaker and localization key.
    /// </summary>
    public class ScenarioDialogueLineDefinition
    {
        public string Character { get; set; }
        public string TextKey { get; set; }
    }

    /// <summary>
    /// Player response option and the next flow stage it selects.
    /// </summary>
    public class ScenarioDialogueOptionDefinition
    {
        public string TextKey { get; set; }
        public string NextId { get; set; }
    }

    /// <summary>
    /// Delayed transition to another scenario flow stage.
    /// </summary>
    public class ScenarioStageChangeDefinition
    {
        public string Id { get; set; }
        public int DelayDays { get; set; }
    }

    /// <summary>
    /// Outcomes applied when an encounter or intercom stage ends.
    /// This covers rewards, trade overrides, milestone changes, spawned scenarios, and quest completion.
    /// </summary>
    public class ScenarioEncounterEndOptionsDefinition
    {
        public ScenarioEncounterEndOptionsDefinition()
        {
            Type = "NothingHappens";
            CombatResult = "Nothing";
            RewardItems = new List<ItemEntry>();
            TradeItems = new List<ItemEntry>();
            TriggerFloatingQuests = new List<ScenarioFloatingQuestTriggerDefinition>();
            SpawnScenarios = new List<ScenarioSpawnTriggerDefinition>();
        }

        public string Type { get; set; }
        public string CombatResult { get; set; }
        public string CombatWinMilestone { get; set; }
        public string CombatLossMilestone { get; set; }
        public bool AddVehicle { get; set; }
        public List<ItemEntry> RewardItems { get; private set; }
        public string MoralOutcome { get; set; }
        public string MoralOutcomeCombatWon { get; set; }
        public string MoralOutcomeCombatLost { get; set; }
        public string AddSurroundedCharacterOutcome { get; set; }
        public string RevealSurroundedMapRegionOption { get; set; }
        public bool OverrideTradeItems { get; set; }
        public List<ItemEntry> TradeItems { get; private set; }
        public int MinRandomTradeItems { get; set; }
        public int MaxRandomTradeItems { get; set; }
        public List<ScenarioFloatingQuestTriggerDefinition> TriggerFloatingQuests { get; private set; }
        public List<ScenarioSpawnTriggerDefinition> SpawnScenarios { get; private set; }
        public bool CompleteQuest { get; set; }
        public bool CompleteParentScenario { get; set; }
    }

    /// <summary>
    /// Request to trigger a floating quest after a delay.
    /// </summary>
    public class ScenarioFloatingQuestTriggerDefinition
    {
        public ScenarioFloatingQuestTriggerDefinition()
        {
            ActivationDelayDays = 2f;
            DurationDays = 5f;
        }

        public string Id { get; set; }
        public float ActivationDelayDays { get; set; }
        public float DurationDays { get; set; }
    }

    /// <summary>
    /// Request to spawn another scenario after a delay and chance roll.
    /// </summary>
    public class ScenarioSpawnTriggerDefinition
    {
        public ScenarioSpawnTriggerDefinition()
        {
            SpawnChance = 100f;
            DelayDays = 1;
        }

        public string Id { get; set; }
        public float SpawnChance { get; set; }
        public int DelayDays { get; set; }
    }

    /// <summary>
    /// Milestone mutation authored by a scenario stage.
    /// </summary>
    public class ScenarioMilestoneDefinition
    {
        public string Name { get; set; }
        public string Scope { get; set; }
        public string Action { get; set; }
    }

    /// <summary>
    /// Milestone prerequisite checked by a scenario stage.
    /// </summary>
    public class ScenarioMilestoneCheckDefinition
    {
        public string Name { get; set; }
        public string Scope { get; set; }
    }

    /// <summary>
    /// Condition that checks the state of one or more subquests before continuing.
    /// </summary>
    public class ScenarioSubquestCheckDefinition
    {
        public ScenarioSubquestCheckDefinition()
        {
            Check = "AllAreSuccessful";
            Subquests = new List<string>();
        }

        public string Check { get; set; }
        public List<string> Subquests { get; private set; }
    }

    /// <summary>
    /// Authored family setup for a scenario.
    /// Use this to replace the vanilla starting family or schedule future survivors.
    /// </summary>
    public class FamilySetupDefinition
    {
        public FamilySetupDefinition()
        {
            Members = new List<FamilyMemberConfig>();
            FutureSurvivors = new List<FutureSurvivorDefinition>();
        }

        public bool OverrideVanillaFamily { get; set; }
        public List<FamilyMemberConfig> Members { get; private set; }
        public List<FutureSurvivorDefinition> FutureSurvivors { get; private set; }
    }

    /// <summary>
    /// Authored family member configuration.
    /// Age, stats, traits, skills, and appearance are optional so scenarios can override only what matters.
    /// </summary>
    public class FamilyMemberConfig
    {
        public FamilyMemberConfig()
        {
            Gender = ScenarioGender.Any;
            Stats = new List<StatOverride>();
            Traits = new List<string>();
            Skills = new List<SkillOverride>();
            Appearance = new FamilyMemberAppearanceConfig();
            Conditions = new FamilyMemberConditionConfig();
            ActorComponents = new List<ScenarioActorComponentDefinition>();
        }

        public string Name { get; set; }
        public ScenarioActorRef ActorRef { get; set; }
        public List<ScenarioActorComponentDefinition> ActorComponents { get; private set; }
        public ScenarioGender Gender { get; set; }
        public int? ExactAge { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public List<StatOverride> Stats { get; private set; }
        public List<string> Traits { get; private set; }
        public List<SkillOverride> Skills { get; private set; }
        public FamilyMemberAppearanceConfig Appearance { get; set; }
        public FamilyMemberConditionConfig Conditions { get; set; }
    }

    /// <summary>
    /// Optional runtime-applied starting condition overrides for an authored survivor.
    /// Values match vanilla BehaviourStat values, where higher values mean a stronger need or problem.
    /// </summary>
    public class FamilyMemberConditionConfig
    {
        public int? Hunger { get; set; }
        public int? Thirst { get; set; }
        public int? Fatigue { get; set; }
        public int? Dirtiness { get; set; }
        public int? Toilet { get; set; }
        public int? Stress { get; set; }
    }

    /// <summary>
    /// Optional visual overrides for an authored family member.
    /// Texture IDs reference scenario assets; texture paths are mod-relative files.
    /// </summary>
    public class FamilyMemberAppearanceConfig
    {
        public string MeshId { get; set; }
        public bool? IsAdult { get; set; }
        public string HeadTextureId { get; set; }
        public string HeadTexturePath { get; set; }
        public string TorsoTextureId { get; set; }
        public string TorsoTexturePath { get; set; }
        public string LegTextureId { get; set; }
        public string LegTexturePath { get; set; }
        public string HairColorHex { get; set; }
        public string SkinColorHex { get; set; }
        public string ShirtColorHex { get; set; }
        public string PantsColorHex { get; set; }
    }

    /// <summary>
    /// Explicit stat override for an authored survivor.
    /// </summary>
    public class StatOverride
    {
        public string StatId { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// Explicit skill override for an authored survivor.
    /// </summary>
    public class SkillOverride
    {
        public string SkillId { get; set; }
        public int Level { get; set; }
    }

    /// <summary>
    /// Starting and scheduled inventory changes for a scenario.
    /// In authoring, <see cref="Items"/> mirrors native shelter storage and is the scenario's starting inventory.
    /// Use <see cref="OverrideRandomStart"/> only to suppress vanilla random-start item pools when the scenario is applied.
    /// </summary>
    public class StartingInventoryDefinition
    {
        public StartingInventoryDefinition()
        {
            Items = new List<ItemEntry>();
            ScheduledChanges = new List<TimedInventoryChangeDefinition>();
        }

        public bool OverrideRandomStart { get; set; }
        public List<ItemEntry> Items { get; private set; }
        public List<TimedInventoryChangeDefinition> ScheduledChanges { get; private set; }
    }

    /// <summary>
    /// Item ID and quantity pair used by inventory, loot, rewards, and trade definitions.
    /// </summary>
    public class ItemEntry
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Direction for a scheduled inventory change.
    /// </summary>
    public enum ScenarioInventoryChangeKind
    {
        Add = 0,
        Remove = 1
    }

    /// <summary>
    /// Survivor that can join or appear later in a scenario timeline.
    /// </summary>
    public class FutureSurvivorDefinition
    {
        public FutureSurvivorDefinition()
        {
            Id = string.Empty;
            Arrival = new ScenarioScheduleTime();
            Survivor = new FamilyMemberConfig();
            AskToJoin = true;
            ActorComponents = new List<ScenarioActorComponentDefinition>();
        }

        public string Id { get; set; }
        public ScenarioActorRef ActorRef { get; set; }
        public List<ScenarioActorComponentDefinition> ActorComponents { get; private set; }
        public ScenarioScheduleTime Arrival { get; set; }
        public bool AskToJoin { get; set; }
        public FamilyMemberConfig Survivor { get; set; }
    }

    /// <summary>
    /// Inventory mutation scheduled for a specific scenario time.
    /// </summary>
    public class TimedInventoryChangeDefinition
    {
        public TimedInventoryChangeDefinition()
        {
            Id = string.Empty;
            When = new ScenarioScheduleTime();
            Kind = ScenarioInventoryChangeKind.Add;
        }

        public string Id { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public ScenarioInventoryChangeKind Kind { get; set; }
        public ScenarioScheduleTime When { get; set; }
    }

    /// <summary>
    /// Authored changes to the starting bunker layout and placed objects.
    /// </summary>
    public class BunkerEditsDefinition
    {
        public BunkerEditsDefinition()
        {
            RoomChanges = new List<RoomEdit>();
            ObjectPlacements = new List<ObjectPlacement>();
        }

        public List<RoomEdit> RoomChanges { get; private set; }
        public List<ObjectPlacement> ObjectPlacements { get; private set; }
    }

    /// <summary>
    /// Visual edit for one bunker grid cell.
    /// Runtime sprite keys let scenario authoring reuse generated or imported sprite assets.
    /// </summary>
    public class RoomEdit
    {
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int? WallSpriteIndex { get; set; }
        public int? WireSpriteIndex { get; set; }
        public string WallRuntimeSpriteKey { get; set; }
        public string WireRuntimeSpriteKey { get; set; }
        public bool WallCleared { get; set; }
        public bool WireCleared { get; set; }
    }

    /// <summary>
    /// Authored object placed into the bunker or scene.
    /// Gates, schedule IDs, and required foundation IDs let the runtime delay or unlock placement.
    /// </summary>
    public class ObjectPlacement
    {
        public ObjectPlacement()
        {
            Position = new ScenarioVector3();
            Rotation = new ScenarioVector3();
            CustomProperties = new List<ScenarioProperty>();
            Tags = new List<string>();
            StartState = ScenarioObjectStartState.StartsEnabled;
            PlacementPhase = "Start";
        }

        public string ScenarioObjectId { get; set; }
        public string RuntimeBindingKey { get; set; }
        public string PrefabReference { get; set; }
        public string DefinitionReference { get; set; }
        public ScenarioVector3 Position { get; set; }
        public ScenarioVector3 Rotation { get; set; }
        public ScenarioObjectStartState StartState { get; set; }
        public string PlacementPhase { get; set; }
        public string RequiredFoundationId { get; set; }
        public string RequiredBunkerExpansionId { get; set; }
        public string UnlockGateId { get; set; }
        public string ScheduledActivationId { get; set; }
        public List<string> Tags { get; private set; }
        public List<ScenarioProperty> CustomProperties { get; private set; }
    }

    /// <summary>
    /// Serializable vector used by scenario DTOs without depending on UnityEngine serialization.
    /// </summary>
    public class ScenarioVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    /// <summary>
    /// Extensible key/value pair for scenario features that need mod-specific metadata.
    /// </summary>
    public class ScenarioProperty
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// Scenario triggers, dialogue chains, and weather events authored outside the main encounter flow.
    /// </summary>
    public class TriggersAndEventsDefinition
    {
        public TriggersAndEventsDefinition()
        {
            Triggers = new List<TriggerDef>();
            DialogueChains = new List<DialogueChain>();
            WeatherEvents = new List<WeatherEventDefinition>();
        }

        public List<TriggerDef> Triggers { get; private set; }
        public List<DialogueChain> DialogueChains { get; private set; }
        public List<WeatherEventDefinition> WeatherEvents { get; private set; }
    }

    /// <summary>
    /// Named trigger definition with extensible properties.
    /// Trigger IDs are referenced by gates, effects, and runtime calls.
    /// </summary>
    public class TriggerDef
    {
        public TriggerDef()
        {
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string Type { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Reusable dialogue line sequence identified by scenario-local ID.
    /// </summary>
    public class DialogueChain
    {
        public DialogueChain()
        {
            Lines = new List<string>();
        }

        public string Id { get; set; }
        public List<string> Lines { get; private set; }
    }

    /// <summary>
    /// Authored win and loss condition groups for a scenario.
    /// </summary>
    public class WinLossConditionsDefinition
    {
        public WinLossConditionsDefinition()
        {
            WinConditions = new List<ScenarioConditionRef>();
            LossConditions = new List<ScenarioConditionRef>();
        }

        public List<ScenarioConditionRef> WinConditions { get; private set; }
        public List<ScenarioConditionRef> LossConditions { get; private set; }
    }

    /// <summary>
    /// Authored scoring metadata for custom scenarios.
    /// Runtime score calculation is intentionally supplied by scenario code until vanilla hooks exist.
    /// </summary>
    public class ScenarioScoringDefinition
    {
        public ScenarioScoringDefinition()
        {
            ScoreLabel = "Score";
            HigherIsBetter = true;
            Categories = new List<ScenarioScoreCategoryDefinition>();
            Rules = new List<ScenarioScoreRuleDefinition>();
            Metadata = new List<ScenarioProperty>();
        }

        public bool Enabled { get; set; }
        public string ScoreLabel { get; set; }
        public bool HigherIsBetter { get; set; }
        public string LeaderboardKey { get; set; }
        public List<ScenarioScoreCategoryDefinition> Categories { get; private set; }
        public List<ScenarioScoreRuleDefinition> Rules { get; private set; }
        public List<ScenarioProperty> Metadata { get; private set; }
    }

    /// <summary>
    /// Author-visible grouping for one or more score rules.
    /// </summary>
    public class ScenarioScoreCategoryDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// A neutral scoring rule declaration. Source and properties describe the metric;
    /// scenario runtime code remains responsible for evaluating it.
    /// </summary>
    public class ScenarioScoreRuleDefinition
    {
        public ScenarioScoreRuleDefinition()
        {
            Operation = "Add";
            OutcomeFilter = "Any";
            Weight = 1f;
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string CategoryId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string Operation { get; set; }
        public string OutcomeFilter { get; set; }
        public float Weight { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    public enum ScenarioScoreCompletionState
    {
        Unknown = 0,
        InProgress = 1,
        Won = 2,
        Lost = 3,
        Completed = 4,
        Failed = 5,
        Abandoned = 6
    }

    /// <summary>
    /// Per-save score state for a custom scenario run.
    /// This is a snapshot supplied by scenario runtime code, not an implicit vanilla score.
    /// </summary>
    public class ScenarioScoreSnapshot
    {
        public ScenarioScoreSnapshot()
        {
            CompletionState = ScenarioScoreCompletionState.InProgress;
            Categories = new List<ScenarioScoreCategorySnapshot>();
            Rules = new List<ScenarioScoreRuleSnapshot>();
            Metadata = new List<ScenarioProperty>();
        }

        public string ScenarioId { get; set; }
        public string ScenarioVersion { get; set; }
        public string RuntimeBindingId { get; set; }
        public ScenarioScoreCompletionState CompletionState { get; set; }
        public string Outcome { get; set; }
        public string OutcomeConditionId { get; set; }
        public bool HasTotalScore { get; set; }
        public int TotalScore { get; set; }
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public List<ScenarioScoreCategorySnapshot> Categories { get; private set; }
        public List<ScenarioScoreRuleSnapshot> Rules { get; private set; }
        public List<ScenarioProperty> Metadata { get; private set; }
    }

    public class ScenarioScoreCategorySnapshot
    {
        public string CategoryId { get; set; }
        public string DisplayName { get; set; }
        public int Score { get; set; }
    }

    public class ScenarioScoreRuleSnapshot
    {
        public string RuleId { get; set; }
        public string CategoryId { get; set; }
        public string DisplayName { get; set; }
        public string Source { get; set; }
        public float Value { get; set; }
        public int Score { get; set; }
    }

    /// <summary>
    /// Collection of authored quest definitions attached to a scenario.
    /// </summary>
    public class QuestAuthoringDefinition
    {
        public QuestAuthoringDefinition()
        {
            Quests = new List<QuestDefinition>();
        }

        public List<QuestDefinition> Quests { get; private set; }
    }

    /// <summary>
    /// Neutral quest definition authored by a scenario.
    /// Game-specific runtime code translates this into Sheltered quest objects.
    /// </summary>
    public class QuestDefinition
    {
        public QuestDefinition()
        {
            Properties = new List<ScenarioProperty>();
            ScheduledStart = new ScenarioScheduleTime();
        }

        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string StartTriggerId { get; set; }
        public string CompletionConditionId { get; set; }
        public ScenarioScheduleTime ScheduledStart { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

    /// <summary>
    /// Weather override scheduled by a scenario timeline.
    /// </summary>
    public class WeatherEventDefinition
    {
        public WeatherEventDefinition()
        {
            Id = string.Empty;
            WeatherState = "None";
            When = new ScenarioScheduleTime();
        }

        public string Id { get; set; }
        public string WeatherState { get; set; }
        public ScenarioScheduleTime When { get; set; }
        public int DurationHours { get; set; }
    }

    /// <summary>
    /// Custom sprite, icon, patch, swap, and scene-sprite references used by a scenario.
    /// Paths are mod-relative unless a runtime sprite key is supplied.
    /// </summary>
    public class AssetReferencesDefinition
    {
        public AssetReferencesDefinition()
        {
            CustomSprites = new List<SpriteRef>();
            CustomIcons = new List<IconRef>();
            SpritePatches = new List<SpritePatchDefinition>();
            SpriteSwaps = new List<SpriteSwapRule>();
            SceneSpritePlacements = new List<SceneSpritePlacement>();
            AssetCredits = new List<ScenarioAssetCreditDefinition>();
        }

        public List<SpriteRef> CustomSprites { get; private set; }
        public List<IconRef> CustomIcons { get; private set; }
        public List<SpritePatchDefinition> SpritePatches { get; private set; }
        public List<SpriteSwapRule> SpriteSwaps { get; private set; }
        public List<SceneSpritePlacement> SceneSpritePlacements { get; private set; }
        /// <summary>Credits associated with authored scenario assets.</summary>
        public List<ScenarioAssetCreditDefinition> AssetCredits { get; private set; }
    }

    /// <summary>Attribution text associated with one scenario-relative asset path.</summary>
    public sealed class ScenarioAssetCreditDefinition
    {
        /// <summary>Scenario-package-relative asset path.</summary>
        public string RelativePath { get; set; }
        /// <summary>Human-readable creator, license, or attribution text.</summary>
        public string Credit { get; set; }
    }

    /// <summary>
    /// Runtime component type targeted by a sprite swap or scene sprite operation.
    /// Use <see cref="Auto"/> when the resolver should choose the best available component.
    /// </summary>
    public enum ScenarioSpriteTargetComponentKind
    {
        Auto = 0,
        SpriteRenderer = 1,
        UI2DSprite = 2,
        ParticleSystemRenderer = 3
    }

    /// <summary>
    /// Rule that replaces a runtime sprite when the target path and optional timing match.
    /// </summary>
    public class SpriteSwapRule
    {
        public string Id { get; set; }
        public string TargetPath { get; set; }
        public string SpriteId { get; set; }
        public string RelativePath { get; set; }
        public string RuntimeSpriteKey { get; set; }
        public int? AnimationFrameIndex { get; set; }
        public string AnimationFrameRuntimeSpriteKey { get; set; }
        public int? Day { get; set; }
        public ScenarioSpriteTargetComponentKind TargetComponent { get; set; }
    }

    /// <summary>
    /// Named custom sprite asset reference.
    /// Use either a mod-relative path, a patch ID, or a runtime-generated sprite key depending on source.
    /// </summary>
    public class SpriteRef
    {
        public string Id { get; set; }
        public string RelativePath { get; set; }
        public string PatchId { get; set; }
        public bool UserOwned { get; set; }
    }

    /// <summary>
    /// Authored standalone sprite placement in a scene.
    /// This is for visual scenario decorations or stateful scene assets, not inventory items.
    /// </summary>
    public class SceneSpritePlacement
    {
        public SceneSpritePlacement()
        {
            Position = new ScenarioVector3();
            Tags = new List<string>();
            StartState = ScenarioObjectStartState.StartsEnabled;
            PlacementPhase = "Start";
        }

        public string Id { get; set; }
        public string ScenarioObjectId { get; set; }
        public string RuntimeBindingKey { get; set; }
        public string SpriteId { get; set; }
        public string RelativePath { get; set; }
        public string RuntimeSpriteKey { get; set; }
        public ScenarioVector3 Position { get; set; }
        public bool SnapToGrid { get; set; }
        public int? GridX { get; set; }
        public int? GridY { get; set; }
        public ScenarioObjectStartState StartState { get; set; }
        public string PlacementPhase { get; set; }
        public string RequiredFoundationId { get; set; }
        public string RequiredBunkerExpansionId { get; set; }
        public string UnlockGateId { get; set; }
        public string ScheduledActivationId { get; set; }
        public List<string> Tags { get; private set; }
        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }
    }

    /// <summary>
    /// Named custom icon asset reference.
    /// </summary>
    public class IconRef
    {
        public string Id { get; set; }
        public string RelativePath { get; set; }
    }

}
