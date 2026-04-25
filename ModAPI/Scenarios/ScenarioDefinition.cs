using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public enum ScenarioBaseGameMode
    {
        Survival = 0,
        Surrounded = 1,
        Stasis = 2
    }

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

    public class FamilyMemberAppearanceConfig
    {
        public string HeadTextureId { get; set; }
        public string HeadTexturePath { get; set; }
        public string TorsoTextureId { get; set; }
        public string TorsoTexturePath { get; set; }
        public string LegTextureId { get; set; }
        public string LegTexturePath { get; set; }
    }

    public class StatOverride
    {
        public string StatId { get; set; }
        public int Value { get; set; }
    }

    public class SkillOverride
    {
        public string SkillId { get; set; }
        public int Level { get; set; }
    }

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

    public class ItemEntry
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
    }

    public enum ScenarioInventoryChangeKind
    {
        Add = 0,
        Remove = 1
    }

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

    public class RoomEdit
    {
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int? WallSpriteIndex { get; set; }
        public int? WireSpriteIndex { get; set; }
    }

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

    public class ScenarioVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class ScenarioProperty
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

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

    public class DialogueChain
    {
        public DialogueChain()
        {
            Lines = new List<string>();
        }

        public string Id { get; set; }
        public List<string> Lines { get; private set; }
    }

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

    public class QuestAuthoringDefinition
    {
        public QuestAuthoringDefinition()
        {
            Quests = new List<QuestDefinition>();
        }

        public List<QuestDefinition> Quests { get; private set; }
    }

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

    public class MapAuthoringDefinition
    {
        public MapAuthoringDefinition()
        {
            Locations = new List<MapLocationDefinition>();
        }

        public string StartLocationId { get; set; }
        public List<MapLocationDefinition> Locations { get; private set; }
    }

    public class MapLocationDefinition
    {
        public MapLocationDefinition()
        {
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }

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

    public enum ScenarioSpriteTargetComponentKind
    {
        Auto = 0,
        SpriteRenderer = 1,
        UI2DSprite = 2
    }

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

    public class SpriteRef
    {
        public string Id { get; set; }
        public string RelativePath { get; set; }
        public string PatchId { get; set; }
    }

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

    public class IconRef
    {
        public string Id { get; set; }
        public string RelativePath { get; set; }
    }

}
