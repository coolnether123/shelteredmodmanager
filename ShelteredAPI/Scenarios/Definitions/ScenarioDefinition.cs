using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
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
    /// Persistent scenario definition. This type is deliberately a neutral data holder:
    /// it must not grow Sheltered or Unity references, because mod tools and the editor
    /// need to read scenario packs without booting a game scene.
    /// </summary>
    public class ScenarioDefinition
    {
        public ScenarioDefinition()
        {
            Dependencies = new List<string>();
            ModDependencies = new List<ScenarioModDependencyDefinition>();
            BaseGameMode = ScenarioBaseGameMode.Survival;
            SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(BaseGameMode);
            ScenarioCharacters = new List<ScenarioNpcDefinition>();
            ScenarioFlow = new ScenarioFlowDefinition();
            FamilySetup = new FamilySetupDefinition();
            StartingInventory = new StartingInventoryDefinition();
            BunkerEdits = new BunkerEditsDefinition();
            TriggersAndEvents = new TriggersAndEventsDefinition();
            Quests = new QuestAuthoringDefinition();
            Map = new MapAuthoringDefinition();
            WinLossConditions = new WinLossConditionsDefinition();
            AssetReferences = new AssetReferencesDefinition();
            BunkerGrid = new ScenarioBunkerGridDefinition();
            Gates = new List<ScenarioGateDefinition>();
            ScheduledActions = new List<ScenarioScheduledActionDefinition>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public List<string> Dependencies { get; private set; }
        public List<ScenarioModDependencyDefinition> ModDependencies { get; private set; }
        public ScenarioBaseGameMode BaseGameMode { get; set; }
        public long? SeedOverride { get; set; }
        public ScenarioSelectionRulesDefinition SelectionRules { get; set; }
        public List<ScenarioNpcDefinition> ScenarioCharacters { get; private set; }
        public ScenarioFlowDefinition ScenarioFlow { get; set; }
        public FamilySetupDefinition FamilySetup { get; set; }
        public StartingInventoryDefinition StartingInventory { get; set; }
        public BunkerEditsDefinition BunkerEdits { get; set; }
        public TriggersAndEventsDefinition TriggersAndEvents { get; set; }
        public QuestAuthoringDefinition Quests { get; set; }
        public MapAuthoringDefinition Map { get; set; }
        public WinLossConditionsDefinition WinLossConditions { get; set; }
        public AssetReferencesDefinition AssetReferences { get; set; }
        public ScenarioBunkerGridDefinition BunkerGrid { get; set; }
        public List<ScenarioGateDefinition> Gates { get; private set; }
        public List<ScenarioScheduledActionDefinition> ScheduledActions { get; private set; }
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
        }

        public string CharacterId { get; set; }
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
        }

        public string Name { get; set; }
        public ScenarioGender Gender { get; set; }
        public int? ExactAge { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public List<StatOverride> Stats { get; private set; }
        public List<string> Traits { get; private set; }
        public List<SkillOverride> Skills { get; private set; }
        public FamilyMemberAppearanceConfig Appearance { get; set; }
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
    /// Use <see cref="OverrideRandomStart"/> to replace vanilla random starting supplies.
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
        }

        public string Id { get; set; }
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
            WinConditions = new List<ConditionDef>();
            LossConditions = new List<ConditionDef>();
        }

        public List<ConditionDef> WinConditions { get; private set; }
        public List<ConditionDef> LossConditions { get; private set; }
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
    /// Legacy/extensible condition definition used by win/loss and quest authoring.
    /// Newer scenario gates should prefer <see cref="ScenarioConditionRef"/>.
    /// </summary>
    public class ConditionDef
    {
        public ConditionDef()
        {
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string Type { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
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
        }

        public List<SpriteRef> CustomSprites { get; private set; }
        public List<IconRef> CustomIcons { get; private set; }
        public List<SpritePatchDefinition> SpritePatches { get; private set; }
        public List<SpriteSwapRule> SpriteSwaps { get; private set; }
        public List<SceneSpritePlacement> SceneSpritePlacements { get; private set; }
    }

    /// <summary>
    /// Runtime component type targeted by a sprite swap or scene sprite operation.
    /// Use <see cref="Auto"/> when the resolver should choose the best available component.
    /// </summary>
    public enum ScenarioSpriteTargetComponentKind
    {
        Auto = 0,
        SpriteRenderer = 1,
        UI2DSprite = 2
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
