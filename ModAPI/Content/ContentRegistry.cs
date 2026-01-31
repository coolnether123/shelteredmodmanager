using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Content
{
    /// <summary>Result of a content registration attempt.</summary>
    public class RegistrationResult
    {
        public bool Success;
        public int AssignedTypeId;
        public string ErrorMessage;
        public string[] Warnings = new string[0];

        public static RegistrationResult Failed(string error) => new RegistrationResult { Success = false, ErrorMessage = error };
        public static RegistrationResult Ok(int id, string[] warnings = null) => new RegistrationResult { Success = true, AssignedTypeId = id, Warnings = warnings ?? new string[0] };
    }

    /// <summary>
    /// Central registry for adding items, crafting recipes, loot entries, and tuning flags.
    /// Use relative paths rooted at the mod directory (e.g., Assets/Textures/icon.png).
    /// </summary>
    public static class ContentRegistry
    {
        private const int CustomItemTypeStart = 10000;
        private const int CustomItemTypeRange = 900000;

        private static readonly HashSet<int> _claimedIds = new HashSet<int>();

        /// <summary>Items to add to the game (IDs must be unique).</summary>
        public static readonly List<ItemDefinition> Items = new List<ItemDefinition>();

        /// <summary>Recipes to add (new crafting entries).</summary>
        public static readonly List<RecipeDefinition> Recipes = new List<RecipeDefinition>();

        /// <summary>Cooking recipes for the stove (Raw -> Cooked mapping).</summary>
        public static readonly List<CookingRecipe> CookingRecipes = new List<CookingRecipe>();

        /// <summary>Patches to adjust existing recipes.</summary>
        public static readonly List<RecipePatch> RecipePatches = new List<RecipePatch>();

        /// <summary>Loot entries to merge into expedition loot pools.</summary>
        public static readonly List<LootEntry> LootEntries = new List<LootEntry>();

        /// <summary>Patches to adjust existing items.</summary>
        public static readonly List<ItemPatch> ItemPatches = new List<ItemPatch>();

        /// <summary>Game toggles developers can set to influence downstream systems.</summary>
        public static readonly GameToggles Toggles = new GameToggles();

        /// <summary>
        /// Register a new item. ID is generated based on Mod ID and Item ID.
        /// Use this for new items defined by your mod.
        /// 
        /// Example:
        /// <code>
        /// var result = ContentRegistry.RegisterItem(new ItemDefinition()
        ///     .WithId("mymod.item.super_mre")
        ///     .WithDisplayName("Super MRE")
        ///     .WithCategory(ItemCategory.Food)
        ///     .WithStackSize(10)
        ///     .WithRation(80, 0.05f)
        ///     .WithIcon("Assets/Icons/mre.png"));
        /// 
        /// if (!result.Success) {
        ///     Debug.LogError($"Failed to register item: {result.ErrorMessage}");
        /// }
        /// </code>
        /// </summary>
        public static RegistrationResult RegisterItem(ItemDefinition def)
        {
            if (def == null) return RegistrationResult.Failed("ItemDefinition cannot be null");
            if (string.IsNullOrEmpty(def.Id)) return RegistrationResult.Failed("Item ID is required");
            if (string.IsNullOrEmpty(def.DisplayName)) return RegistrationResult.Failed("DisplayName is required");

            if (def.OwnerAssembly == null)
            {
                try { def.OwnerAssembly = System.Reflection.Assembly.GetCallingAssembly(); } catch { }
            }

            try
            {
                var typeId = EnsureCustomTypeId(def);
                Items.Add(def);
                return RegistrationResult.Ok(typeId);
            }
            catch (Exception ex)
            {
                return RegistrationResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Register a new item with a deterministic fixed ID. 
        /// Ensures the same internal integer ID is assigned every session.
        /// </summary>
        public static RegistrationResult RegisterItemWithFixedId(string modId, string itemId, ItemDefinition def)
        {
            if (def == null) return RegistrationResult.Failed("ItemDefinition cannot be null");
            if (string.IsNullOrEmpty(modId)) return RegistrationResult.Failed("Mod ID is required");
            if (string.IsNullOrEmpty(itemId)) return RegistrationResult.Failed("Item ID is required");

            def.Id = string.IsNullOrEmpty(def.Id) ? itemId : def.Id;
            if (def.OwnerAssembly == null)
            {
                try { def.OwnerAssembly = System.Reflection.Assembly.GetCallingAssembly(); } catch { }
            }

            try
            {
                var id = ClaimCustomItemId(modId, itemId);
                def.CustomTypeId = id;
                Items.Add(def);
                return RegistrationResult.Ok(id);
            }
            catch (Exception ex)
            {
                return RegistrationResult.Failed(ex.Message);
            }
        }

        /// <summary>Patch an existing item's properties.</summary>
        public static void PatchItem(ItemPatch patch)
        {
            if (patch == null) throw new ArgumentNullException(nameof(patch));
            ItemPatches.Add(patch);
        }

        /// <summary>
        /// Register a new crafting recipe.
        /// 
        /// IMPORTANT: Output quantity is set on the ItemDefinition.CraftStackSize, NOT here.
        /// 
        /// Example:
        /// <code>
        /// ContentRegistry.RegisterRecipe(new RecipeDefinition()
        ///     .WithId("recipe.super_mre")
        ///     .WithResultItem("mymod.item.super_mre")
        ///     .WithStation(CraftStation.Laboratory)
        ///     .WithLevel(2)
        ///     .WithIngredient(VanillaItems.Meat, 2)
        ///     .WithIngredient(VanillaItems.Water, 1));
        /// </code>
        /// </summary>
        public static void RegisterRecipe(RecipeDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            
            if (string.IsNullOrEmpty(def.Id))
                throw new ArgumentException("Recipe ID is required", nameof(def));
            if (string.IsNullOrEmpty(def.ResultItemId))
                throw new ArgumentException("ResultItemId is required", nameof(def));
            if (def.Ingredients.Count == 0)
                throw new ArgumentException("Recipe must have at least one ingredient", nameof(def));
            
            // Clamp level between 1 and 5
            def.Level = UnityEngine.Mathf.Clamp(def.Level, 1, 5);
                
            Recipes.Add(def);
        }

        /// <summary>
        /// Register a cooking recipe for the stove.
        /// </summary>
        public static void RegisterCookingRecipe(CookingRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (string.IsNullOrEmpty(recipe.RawItemId)) 
                throw new ArgumentException("RawItemId is required", nameof(recipe));
            if (recipe.CookTimeSeconds <= 0)
                throw new ArgumentException("CookTimeSeconds must be > 0", nameof(recipe));
            
            CookingRecipes.Add(recipe);
        }

        /// <summary>Patch an existing recipe by ID.</summary>
        public static void PatchRecipe(RecipePatch patch)
        {
            if (patch == null) throw new ArgumentNullException(nameof(patch));
            RecipePatches.Add(patch);
        }

        /// <summary>Add an item to a loot pool.</summary>
        public static void AddLoot(LootEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            LootEntries.Add(entry);
        }

        internal static int EnsureCustomTypeId(ItemDefinition def)
        {
            if (def.OwnerAssembly == null)
            {
                try { def.OwnerAssembly = System.Reflection.Assembly.GetCallingAssembly(); } catch { }
            }
            if (def.CustomTypeId.HasValue) return def.CustomTypeId.Value;

            var modId = ResolveModId(def.OwnerAssembly);
            var itemKey = !string.IsNullOrEmpty(def.Id) ? def.Id : def.DisplayName ?? Guid.NewGuid().ToString("N");
            var id = ClaimCustomItemId(modId, itemKey);
            def.CustomTypeId = id;
            return id;
        }

        private static int ClaimCustomItemId(string modId, string itemId)
        {
            var seed = (modId ?? "mod") + "|" + (itemId ?? "item");
            var hash = seed.GetHashCode();
            var positive = hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
            var id = CustomItemTypeStart + (positive % CustomItemTypeRange);

            if (!_claimedIds.Add(id))
            {
                var baseId = id;
                for (int attempt = 1; attempt < 100; attempt++)
                {
                    var candidate = baseId + attempt;
                    if (candidate >= CustomItemTypeStart + CustomItemTypeRange)
                        candidate = CustomItemTypeStart + (candidate % CustomItemTypeRange);
                    if (_claimedIds.Add(candidate))
                    {
                        id = candidate;
                        break;
                    }
                }
                if (!_claimedIds.Contains(id))
                    throw new InvalidOperationException($"No available custom item ID for {modId}.{itemId}");
            }

            return id;
        }

        private static string ResolveModId(System.Reflection.Assembly asm)
        {
            try
            {
                Core.ModEntry entry;
                if (Core.ModRegistry.TryGetModByAssembly(asm, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
                    return entry.Id;
            }
            catch { }
            try { return asm != null ? asm.GetName().Name : "mod"; } catch { return "mod"; }
        }
    }

    /// <summary>
    /// Crafting stations where recipes can be performed.
    /// 
    /// Valid stations:
    /// - Workbench: General crafting (most items, tools, weapons)
    /// - Laboratory: Science-based recipes (explosives, medicine)
    /// - AmmoPress: Ammunition pressing
    /// 
    /// NOTE: Stove is NOT a crafting station. Cooking food is handled via
    /// FamilyAI interactions (Int_CookFood) and does not use CraftingManager.
    /// </summary>
    public enum CraftStation { Workbench, Laboratory, AmmoPress }

    /// <summary>Known world topography types for loot placement.</summary>
    public enum TopographyType { City, Forest, Desert, Mountain, Scrapyard, Highway, Farm, Church, School, Hospital, PoliceStation, PetrolStation, Barracks, Landfill }

    /// <summary>Standard item categories matching the game's internal ItemCategory enum.</summary>
    public enum ItemCategory
    {
        Normal = 0,
        Medicine = 1,
        Entertainment = 2,
        Object = 3,
        Tool = 4,
        Food = 5,
        Water = 6,
        Weapon = 7,
        Ammo = 9,
        Armour = 10,
        LoadCarrying = 11,
        Equipment = 12,
        Schematic = 14,
        Shelter = 20,
        ShelterPaint = 21,
        Meat = 22,
        Embryo = 23,
        GasMask = 24
    }

    /// <summary>Helper for referencing vanilla item IDs safely.</summary>
    public static class VanillaItems
    {
        // Raw Materials
        public const string Water = "Water";
        public const string DirtyWater = "DirtyWater";
        public const string Petrol = "Petrol";
        public const string Metal = "Metal";
        public const string Wood = "Wood";
        public const string Plastic = "Plastic";
        public const string Rubber = "Rubber";
        public const string Leather = "Leather";
        public const string Wool = "Wool";
        public const string Sand = "Sand";
        public const string Limestone = "Limestone";
        
        // Components
        public const string Nails = "Nails";
        public const string Spring = "Spring";
        public const string Piping = "Piping";
        public const string Valve = "Valve";
        public const string Hinge = "Hinge";
        public const string Rope = "Rope";
        public const string Wiring = "Wiring";
        public const string Battery = "Battery";
        public const string Fuse = "Fuse";
        public const string CircuitBoard = "CircuitBoard";
        public const string Bulb = "Bulb";
        public const string Motor = "Motor";
        public const string Transistor = "Transistor";
        public const string Lens = "Lens";
        
        // Tools & Equipment
        public const string DuctTape = "DuctTape";
        public const string Bandages = "Bandages";
        public const string FirstAid = "FirstAid";
        public const string GasMask = "GasMask";
        public const string AdvancedGasMask = "AdvancedGasMask";
        public const string Lighter = "Lighter";
        public const string Matches = "Matches";
        public const string LockpickSet = "LockpickSet";
        
        // Food & Consumables
        public const string Meat = "Meat";
        public const string DesperateMeat = "DesperateMeat";
        public const string Ration = "Ration";
        public const string CoffeeBeans = "CoffeeBeans";
        
        // Medicine
        public const string AntiRadMedicine = "AntiRadMedicine";
        public const string Antibiotics = "Antibiotics";
        public const string Valium = "Valium";
        
        // Other
        public const string GlassJar = "GlassJar";
        public const string Cement = "Cement";
    }

    /// <summary>Helper for formatting asset paths (icons/prefabs).</summary>
    public static class AssetPath
    {
        public static string FromAssets(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return relativePath;
            if (!relativePath.StartsWith("Assets/"))
            {
                MMLog.Write($"[AssetPath] WARNING: Path '{relativePath}' does not start with 'Assets/'. " +
                          "Paths should be relative to the mod root (e.g., 'Assets/Icons/item.png').");
            }
            return relativePath;
        }
        
        public static string FromBundle(string bundlePath, string assetName) => $"{bundlePath}|{assetName}";
    }

    /// <summary>Defines a new item and its assets.</summary>
    public class ItemDefinition
    {
        public string Id;                // unique key, e.g., com.mod.item.myhammer
        public string DisplayName;       // user-facing name
        public string Description;       // user-facing description
        public string IconPath;          // optional icon file path (use AssetPath helpers)
        public string PrefabPath;        // optional prefab path for instantiation
        public ItemCategory Category = ItemCategory.Normal;
        public int StackSize = 64;
        
        /// <summary>Selling price to NPCs / trade value.</summary>
        public int TradeValue = 20;

        /// <summary>Heat output when burned in fire (non-zero = burnable).</summary>
        public float BurnValue = 0f;

        /// <summary>Salvage value when recycled or fabricated (cost to break down).</summary>
        public float ScrapValue = 0f;

        /// <summary>
        /// Cost to fabricate this item in the Item Fabricator (Obj_ItemFabricator).
        /// Requires Category=Normal and FabricationCost > 0.
        /// </summary>
        public float FabricationCost;

        /// <summary>Time in seconds to fabricate (BaseFabricationTime).</summary>
        public float BaseFabricationTime;
        
        /// <summary>Time in seconds to craft via Workbench (BaseCraftTime).</summary>
        public float BaseCraftTime = 10f;
        
        /// <summary>
        /// How many items are produced per craft operation.
        /// Example: CraftStackSize=5 means one craft makes 5 items.
        /// This is fixed per-item, not per-recipe.
        /// </summary>
        public int CraftStackSize = 1;

        public int RationValue;
        public float Contamination;
        public int LoadCarrySlots;

        /// <summary>If true, this food item causes illness if consumed without cooking (Harmony patch suggested).</summary>
        public bool IsRawFood;
        /// <summary>Multiplier applied to RationValue when this food is cooked via a Stove.</summary>
        public float CookedHungerMultiplier = 1.1f;

        /// <summary>Type of placeable object this item creates (requires matching level).</summary>
        public ObjectManager.ObjectType ObjectType = ObjectManager.ObjectType.Undefined;
        /// <summary>Level of the placeable object (1-5, default 1).</summary>
        public int ObjectLevel = 1;

        public List<RecipeIngredient> RecyclingIngredients = new List<RecipeIngredient>();
        public Dictionary<string, string> Metadata = new Dictionary<string, string>();
        internal System.Reflection.Assembly OwnerAssembly;
        internal int? CustomTypeId;

        // Fluent helpers
        public ItemDefinition WithId(string id) { Id = id; return this; }
        public ItemDefinition WithDisplayName(string name) { DisplayName = name; return this; }
        public ItemDefinition WithDescription(string desc) { Description = desc; return this; }
        public ItemDefinition WithIcon(string path) { IconPath = AssetPath.FromAssets(path); return this; }
        public ItemDefinition WithPrefab(string path) { PrefabPath = AssetPath.FromAssets(path); return this; }
        public ItemDefinition WithCategory(ItemCategory cat) { Category = cat; return this; }

        /// <summary>Backward compatibility overload for vanilla ItemManager.ItemCategory.</summary>
        public ItemDefinition WithCategory(ItemManager.ItemCategory cat)
        {
            Category = (ItemCategory)(int)cat;
            return this;
        }

        public ItemDefinition WithStackSize(int size) { StackSize = size; return this; }
        public ItemDefinition WithTradeValue(int val) { TradeValue = val; return this; }
        public ItemDefinition WithBurnValue(float val) { BurnValue = val; return this; }
        public ItemDefinition WithScrapValue(float val) { ScrapValue = val; return this; }
        public ItemDefinition WithFabrication(float cost, float time) { FabricationCost = cost; BaseFabricationTime = time; return this; }
        public ItemDefinition WithBaseCraftTime(float time) { BaseCraftTime = time; return this; }
        public ItemDefinition WithCraftStackSize(int count) { CraftStackSize = count; return this; }
        public ItemDefinition WithRation(int value, float contamination = 0f) { RationValue = value; Contamination = contamination; return this; }
        public ItemDefinition WithRawFood(float cookedMultiplier = 1.1f) { IsRawFood = true; CookedHungerMultiplier = cookedMultiplier; return this; }
        public ItemDefinition WithLoadCarrySlots(int slots) { LoadCarrySlots = slots; return this; }
        public ItemDefinition WithObjectType(ObjectManager.ObjectType type, int level = 1)
        {
            ObjectType = type;
            ObjectLevel = level;
            return this;
        }
        public ItemDefinition WithRecycling(string itemId, int count)
        {
            RecyclingIngredients.Add(new RecipeIngredient { ItemId = itemId, Count = count });
            return this;
        }
        public ItemDefinition WithMetadata(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) Metadata[key] = value;
            return this;
        }
        public ItemDefinition WithOwner(System.Reflection.Assembly asm) { OwnerAssembly = asm; return this; }
    }

    /// <summary>Templates for common item patterns to reduce boilerplate.</summary>
    public static class ItemTemplates
    {
        public static ItemDefinition Food(string id, string name, int nutrition, float contamination = 0f)
        {
            return new ItemDefinition()
                .WithId(id)
                .WithDisplayName(name)
                .WithCategory(ItemCategory.Food)
                .WithStackSize(10)
                .WithRation(nutrition, contamination);
        }

        public static ItemDefinition Tool(string id, string name, int tradeValue = 100)
        {
            return new ItemDefinition()
                .WithId(id)
                .WithDisplayName(name)
                .WithCategory(ItemCategory.Tool)
                .WithStackSize(1)
                .WithTradeValue(tradeValue);
        }
    }

    /// <summary>Defines a new crafting recipe.</summary>
    public class RecipeDefinition
    {
        public string Id;
        public string ResultItemId;
        public List<RecipeIngredient> Ingredients = new List<RecipeIngredient>();
        public CraftStation Station = CraftStation.Workbench;
        public int Level = 1;
        public float CraftTimeSeconds = 1f;
        public bool Unique = false;   // Only craftable once
        public bool Locked = false;   // Must be unlocked
        public string UnlockFlag;     // ACHIEVEMENT or Quest ID
        public Dictionary<string, string> Metadata = new Dictionary<string, string>();

        public RecipeDefinition WithId(string id) { Id = id; return this; }
        public RecipeDefinition WithResultItem(string itemId) 
        { 
            ResultItemId = itemId; 
            return this; 
        }
        public RecipeDefinition WithStation(CraftStation station) { Station = station; return this; }
        public RecipeDefinition WithLevel(int level) { Level = level; return this; }
        public RecipeDefinition WithCraftTime(float seconds) { CraftTimeSeconds = seconds; return this; }
        public RecipeDefinition AsUnique() { Unique = true; return this; }
        public RecipeDefinition AsLocked(string unlockFlag = null) 
        { 
            Locked = true; 
            UnlockFlag = unlockFlag;
            return this; 
        }
        public RecipeDefinition WithIngredient(string itemId, int count)
        {
            Ingredients.Add(new RecipeIngredient { ItemId = itemId, Count = count });
            return this;
        }
        public RecipeDefinition WithMetadata(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) Metadata[key] = value;
            return this;
        }
    }

    /// <summary>Single ingredient entry.</summary>
    public class RecipeIngredient
    {
        public string ItemId;
        public int Count = 1;
    }

    /// <summary>Patches an existing recipe.</summary>
    public class RecipePatch
    {
        public string TargetRecipeId;
        public List<RecipeIngredient> AddIngredients = new List<RecipeIngredient>();
        public List<string> RemoveIngredientIds = new List<string>();
        public List<RecipeIngredient> ReplaceIngredients = new List<RecipeIngredient>();
        public bool? SetUnique;  // null = no change
        public bool? SetLocked;
        public bool ReplaceOutput;
        public string NewResultItemId;

        public static RecipePatch For(string recipeId) => new RecipePatch { TargetRecipeId = recipeId };
        
        public RecipePatch AddIngredient(string itemId, int count)
        {
            AddIngredients.Add(new RecipeIngredient { ItemId = itemId, Count = count });
            return this;
        }

        public RecipePatch RemoveIngredient(string itemId)
        {
            RemoveIngredientIds.Add(itemId);
            return this;
        }

        public RecipePatch ReplaceIngredient(string itemId, int count)
        {
            ReplaceIngredients.Add(new RecipeIngredient { ItemId = itemId, Count = count });
            return this;
        }

        public RecipePatch ReplaceResult(string itemId)
        {
            ReplaceOutput = true;
            NewResultItemId = itemId;
            return this;
        }

        public RecipePatch AsUnique(bool unique = true) { SetUnique = unique; return this; }
        public RecipePatch AsLocked(bool locked = true) { SetLocked = locked; return this; }
    }

    /// <summary>Patches an existing item's properties.</summary>
    public class ItemPatch
    {
        public string TargetItemId;
        public int? NewStackSize;
        public int? NewTradeValue;
        public float? NewBaseCraftTime;
        public float? NewBurnValue;
        public float? NewScrapValue;
        public float? NewFabricationCost;
        public float? NewBaseFabricationTime;
        public int? NewRationValue;
        public float? NewContamination;
        public int? NewLoadCarrySlots;
        public List<RecipeIngredient> NewRecyclingIngredients;
        public ItemCategory? NewCategory;
        public Dictionary<string, string> Metadata = new Dictionary<string, string>();

        public static ItemPatch For(string itemId) => new ItemPatch { TargetItemId = itemId };

        public ItemPatch WithStackSize(int size) { NewStackSize = size; return this; }
        public ItemPatch WithTradeValue(int val) { NewTradeValue = val; return this; }
        public ItemPatch WithBaseCraftTime(float seconds) { NewBaseCraftTime = seconds; return this; }
        public ItemPatch WithBurnValue(float val) { NewBurnValue = val; return this; }
        public ItemPatch WithScrapValue(float val) { NewScrapValue = val; return this; }
        public ItemPatch WithFabrication(float cost, float time) { NewFabricationCost = cost; NewBaseFabricationTime = time; return this; }
        public ItemPatch WithRation(int value, float contamination = 0f) { NewRationValue = value; NewContamination = contamination; return this; }
        public ItemPatch WithLoadCarrySlots(int slots) { NewLoadCarrySlots = slots; return this; }
        public ItemPatch WithRecycling(string itemId, int count)
        {
            if (NewRecyclingIngredients == null) NewRecyclingIngredients = new List<RecipeIngredient>();
            NewRecyclingIngredients.Add(new RecipeIngredient { ItemId = itemId, Count = count });
            return this;
        }
        public ItemPatch WithCategory(ItemCategory cat) { NewCategory = cat; return this; }
    }

    /// <summary>Defines a mapping for cooking food on a stove.</summary>
    public class CookingRecipe
    {
        /// <summary>The raw item to be cooked.</summary>
        public string RawItemId;
        /// <summary>
        /// The item produced after cooking. 
        /// If null or same as RawItemId, the item is cooked "in place"
        /// (like vanilla Meat which stays Meat but gains a multiplier).
        /// </summary>
        public string CookedItemId;
        /// <summary>Bonus hunger reduction applied when cooked.</summary>
        public float HungerMultiplier = 1.1f;
        /// <summary>Time in seconds required to cook this item.</summary>
        public float CookTimeSeconds = 30f;

        public CookingRecipe WithRawItem(string itemId) { RawItemId = itemId; return this; }
        public CookingRecipe WithCookedItem(string itemId) { CookedItemId = itemId; return this; }
        public CookingRecipe WithHungerMultiplier(float mult) { HungerMultiplier = mult; return this; }
        public CookingRecipe WithCookTime(float seconds) { CookTimeSeconds = seconds; return this; }
    }

    /// <summary>Represents a loot entry for expeditions or containers.</summary>
    public class LootEntry
    {
        public string ItemId;                // item to drop
        /// <summary>
        /// Relative weight in the loot table (integer value 1-10 recommended).
        /// Note: This is cast to an integer internally by the game's ItemBias system.
        /// </summary>
        public float Weight = 1f;
        public int MinQuantity = 1;
        public int MaxQuantity = 1;
        /// <summary>
        /// Common = all locations.
        /// Otherwise use TopographyType.Forest.ToString(), etc.
        /// </summary>
        public string LootTableId = "Common";
        public string BiomeId;               // optional biome filter
    }

    /// <summary>Optional knobs developers can set to influence integration code.</summary>
    public class GameToggles
    {
        public bool EnableContentLogging = true;
        public bool AutoInjectRecipes = true;
        public bool AutoInjectLoot = true;
        public bool AutoRegisterItems = true;
    }
}
