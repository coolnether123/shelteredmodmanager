using System;
using System.Collections.Generic;

namespace ModAPI.Content
{
    /// <summary>
    /// Central registry for adding items, crafting recipes, loot entries, and tuning flags
    /// in a version-agnostic way (works with both Unity 5.3 and 5.6 builds).
    /// Use relative paths rooted at the mod directory (e.g., Assets/Textures/icon.png, Assets/Prefabs/tool.prefab).
    /// Integration code should read from these collections and apply changes when the game is ready.
    /// </summary>
    public static class ContentRegistry
    {
        /// <summary>Items to add to the game (IDs must be unique).</summary>
        public static readonly List<ItemDefinition> Items = new List<ItemDefinition>();

        /// <summary>Recipes to add (new crafting entries).</summary>
        public static readonly List<RecipeDefinition> Recipes = new List<RecipeDefinition>();

        /// <summary>Patches to adjust existing recipes (add/remove/replace ingredients or outputs).</summary>
        public static readonly List<RecipePatch> RecipePatches = new List<RecipePatch>();

        /// <summary>Loot entries to merge into expedition loot pools.</summary>
        public static readonly List<LootEntry> LootEntries = new List<LootEntry>();

        /// <summary>Game toggles developers can set to influence downstream systems.</summary>
        public static readonly GameToggles Toggles = new GameToggles();

        /// <summary>Register a new item definition.</summary>
        public static void RegisterItem(ItemDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (def.OwnerAssembly == null)
            {
                try { def.OwnerAssembly = System.Reflection.Assembly.GetCallingAssembly(); } catch { }
            }
            Items.Add(def);
        }

        /// <summary>Register a new recipe.</summary>
        public static void RegisterRecipe(RecipeDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            Recipes.Add(def);
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
    }

    /// <summary>Defines a new item and its assets.</summary>
    public class ItemDefinition
    {
        public string Id;                // unique key, e.g., com.mod.item.myhammer
        public string DisplayName;       // user-facing name
        public string Description;       // user-facing description
        public string IconPath;          // optional icon file path (relative to mod root)
        public string PrefabPath;        // optional prefab path for instantiation
        public string Category;          // optional in-game category
        public Dictionary<string, string> Metadata = new Dictionary<string, string>(); // extra key/value data
        internal System.Reflection.Assembly OwnerAssembly;

        // Fluent helpers for convenience
        public ItemDefinition WithId(string id) { Id = id; return this; }
        public ItemDefinition WithDisplayName(string name) { DisplayName = name; return this; }
        public ItemDefinition WithDescription(string desc) { Description = desc; return this; }
        public ItemDefinition WithIcon(string iconPath) { IconPath = iconPath; return this; }
        public ItemDefinition WithPrefab(string prefabPath) { PrefabPath = prefabPath; return this; }
        public ItemDefinition WithCategory(string category) { Category = category; return this; }
        public ItemDefinition WithMetadata(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) Metadata[key] = value;
            return this;
        }
        public ItemDefinition WithOwner(System.Reflection.Assembly asm) { OwnerAssembly = asm; return this; }
    }

    /// <summary>Defines a new crafting recipe.</summary>
    public class RecipeDefinition
    {
        public string Id;                        // unique recipe id
        public string ResultItemId;              // item produced
        public int ResultCount = 1;              // quantity produced
        public List<RecipeIngredient> Ingredients = new List<RecipeIngredient>();
        public string StationId;                 // crafting station identifier, if applicable
        public float CraftTimeSeconds = 1f;      // crafting duration
        public string UnlockFlag;                // optional unlock condition/id
    }

    /// <summary>Single ingredient entry.</summary>
    public class RecipeIngredient
    {
        public string ItemId;
        public int Count = 1;
    }

    /// <summary>Patches an existing recipe by adding/removing/replacing ingredients or outputs.</summary>
    public class RecipePatch
    {
        public string TargetRecipeId;                    // recipe to patch
        public List<RecipeIngredient> AddIngredients = new List<RecipeIngredient>();
        public List<string> RemoveIngredientIds = new List<string>();
        public List<RecipeIngredient> ReplaceIngredients = new List<RecipeIngredient>(); // replaces matching ids
        public bool ReplaceOutput;
        public string NewResultItemId;
        public int NewResultCount = 1;
    }

    /// <summary>Represents a loot entry for expeditions or containers.</summary>
    public class LootEntry
    {
        public string LootTableId;           // which loot table/pool to extend
        public string ItemId;                // item to drop
        public int MinQuantity = 1;
        public int MaxQuantity = 1;
        public float Weight = 1f;            // relative weight in the table
        public string BiomeId;               // optional biome/area filter
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
