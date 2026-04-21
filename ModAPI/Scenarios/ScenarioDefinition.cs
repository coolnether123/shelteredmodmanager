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
            BaseGameMode = ScenarioBaseGameMode.Survival;
            FamilySetup = new FamilySetupDefinition();
            StartingInventory = new StartingInventoryDefinition();
            BunkerEdits = new BunkerEditsDefinition();
            TriggersAndEvents = new TriggersAndEventsDefinition();
            WinLossConditions = new WinLossConditionsDefinition();
            AssetReferences = new AssetReferencesDefinition();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public List<string> Dependencies { get; private set; }
        public ScenarioBaseGameMode BaseGameMode { get; set; }
        public long? SeedOverride { get; set; }
        public FamilySetupDefinition FamilySetup { get; set; }
        public StartingInventoryDefinition StartingInventory { get; set; }
        public BunkerEditsDefinition BunkerEdits { get; set; }
        public TriggersAndEventsDefinition TriggersAndEvents { get; set; }
        public WinLossConditionsDefinition WinLossConditions { get; set; }
        public AssetReferencesDefinition AssetReferences { get; set; }
    }

    public class FamilySetupDefinition
    {
        public FamilySetupDefinition()
        {
            Members = new List<FamilyMemberConfig>();
        }

        public bool OverrideVanillaFamily { get; set; }
        public List<FamilyMemberConfig> Members { get; private set; }
    }

    public class FamilyMemberConfig
    {
        public FamilyMemberConfig()
        {
            Gender = ScenarioGender.Any;
            Stats = new List<StatOverride>();
            Traits = new List<string>();
            Skills = new List<SkillOverride>();
        }

        public string Name { get; set; }
        public ScenarioGender Gender { get; set; }
        public int? ExactAge { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public List<StatOverride> Stats { get; private set; }
        public List<string> Traits { get; private set; }
        public List<SkillOverride> Skills { get; private set; }
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
        }

        public bool OverrideRandomStart { get; set; }
        public List<ItemEntry> Items { get; private set; }
    }

    public class ItemEntry
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
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
        }

        public string PrefabReference { get; set; }
        public string DefinitionReference { get; set; }
        public ScenarioVector3 Position { get; set; }
        public ScenarioVector3 Rotation { get; set; }
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
        }

        public List<TriggerDef> Triggers { get; private set; }
        public List<DialogueChain> DialogueChains { get; private set; }
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
            SpriteSwaps = new List<SpriteSwapRule>();
        }

        public List<SpriteRef> CustomSprites { get; private set; }
        public List<IconRef> CustomIcons { get; private set; }
        public List<SpriteSwapRule> SpriteSwaps { get; private set; }
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
        public int? Day { get; set; }
        public ScenarioSpriteTargetComponentKind TargetComponent { get; set; }
    }

    public class SpriteRef
    {
        public string Id { get; set; }
        public string RelativePath { get; set; }
    }

    public class IconRef
    {
        public string Id { get; set; }
        public string RelativePath { get; set; }
    }

}
