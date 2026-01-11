using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;
using UnityEngine;
using GameItemDefinition = global::ItemDefinition;

namespace ModAPI.Content
{
    public static class ContentInjector
    {
        private const int CustomItemTypeStart = 10000;
        private static readonly Dictionary<string, ItemManager.ItemType> ItemKeyToType = new Dictionary<string, ItemManager.ItemType>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ItemManager.ItemType, ResolvedItem> ResolvedByType = new Dictionary<ItemManager.ItemType, ResolvedItem>();
        public static IEnumerable<ItemManager.ItemType> RegisteredTypes => ResolvedByType.Keys;
        private static readonly Dictionary<ItemManager.ItemType, GameItemDefinition> RuntimeDefinitions = new Dictionary<ItemManager.ItemType, GameItemDefinition>();
        private static readonly Dictionary<ItemManager.ItemType, CookingRecipe> _cookingRecipes = new Dictionary<ItemManager.ItemType, CookingRecipe>();
        private static readonly HashSet<ItemManager.ItemType> _rawFoodTypes = new HashSet<ItemManager.ItemType>();

        private static bool _bootstrapped;
        private static readonly object Sync = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnUnityLoad() => TryBootstrap();

        /// <summary>
        /// Query cooking recipes injected by mods.
        /// Use this when writing Harmony patches to hook into FamilyAI cooking.
        /// </summary>
        /// <param name="rawItemType">The raw food item type</param>
        /// <param name="recipe">The cooking recipe if found</param>
        /// <returns>True if a cooking recipe exists for this item</returns>
        public static bool TryGetCookingRecipe(ItemManager.ItemType rawItemType, out CookingRecipe recipe)
        {
            return _cookingRecipes.TryGetValue(rawItemType, out recipe);
        }

        /// <summary>
        /// Get all cooking recipes registered by mods.
        /// Use this when you need to enumerate all cooking transformations.
        /// </summary>
        /// <returns>Read-only dictionary of raw item types to cooking recipes</returns>
        public static IReadOnlyDictionary<ItemManager.ItemType, CookingRecipe> GetCookingRecipes()
        {
            return _cookingRecipes;
        }

        /// <summary>
        /// Check if an item is marked as raw food (can be cooked).
        /// </summary>
        /// <param name="itemType">The item type to check</param>
        /// <returns>True if the item is raw food</returns>
        public static bool IsRawFood(ItemManager.ItemType itemType)
        {
            return _rawFoodTypes.Contains(itemType);
        }

        /// <summary>
        /// Get all item types marked as raw food.
        /// </summary>
        /// <returns>Read-only collection of raw food item types</returns>
        public static IReadOnlyCollection<ItemManager.ItemType> GetRawFoodTypes()
        {
            return _rawFoodTypes;
        }

        public static void NotifyManagerReady(string name) => TryBootstrap();

        private static void TryBootstrap()
        {
            MMLog.Write($"[ContentInjector] TryBootstrap called. Bootstrapped={_bootstrapped}, ItemManager={(ItemManager.Instance != null)}, CraftingManager={(CraftingManager.Instance != null)}");
            if (_bootstrapped || ItemManager.Instance == null || CraftingManager.Instance == null) return;

            lock (Sync)
            {
                if (_bootstrapped) return;
                MMLog.Write("[ContentInjector] Starting injection...");

                // 1. Setup Maps
                BuildItemMap();
                BuildRuntimeDefinitions();

                // 2. Inject Items
                InjectIntoItemManager();
                ApplyItemPatches();

                // 3. Inject Recipes
                InjectRecipes();
                InjectCookingRecipes();

                _bootstrapped = true;
                MMLog.Write("[ContentInjector] Injection complete.");

                // Final Check
                PluginManager.getInstance().EnqueueNextFrame(VerifyExistence);
            }
        }

        private static void ApplyItemPatches()
        {
            var im = ItemManager.Instance;
            if (ContentRegistry.ItemPatches.Count == 0) return;

            MMLog.Write($"[ContentInjector] Applying {ContentRegistry.ItemPatches.Count} item patches...");

            foreach (var patch in ContentRegistry.ItemPatches)
            {
                if (!ResolveItemType(patch.TargetItemId, out var type))
                {
                    MMLog.Write($"[ContentInjector] Item patch failed: Target item '{patch.TargetItemId}' not found.");
                    continue;
                }

                var def = im.GetItemDefinition(type);
                if (def == null) continue;

                if (patch.NewStackSize.HasValue) SetField(def, "m_StackSize", patch.NewStackSize.Value);
                if (patch.NewTradeValue.HasValue) SetField(def, "m_TradeValue", patch.NewTradeValue.Value);
                if (patch.NewBaseCraftTime.HasValue) SetField(def, "m_BaseCraftTime", patch.NewBaseCraftTime.Value);
                if (patch.NewBurnValue.HasValue) SetField(def, "m_BurnValue", patch.NewBurnValue.Value);
                if (patch.NewScrapValue.HasValue) SetField(def, "m_ScrapValue", patch.NewScrapValue.Value);
                if (patch.NewFabricationCost.HasValue) SetField(def, "m_FabricationCost", patch.NewFabricationCost.Value);
                if (patch.NewBaseFabricationTime.HasValue) SetField(def, "m_BaseFabricationTime", patch.NewBaseFabricationTime.Value);
                if (patch.NewRationValue.HasValue) SetField(def, "m_RationValue", patch.NewRationValue.Value);
                if (patch.NewContamination.HasValue) SetField(def, "m_Contamination", patch.NewContamination.Value);
                if (patch.NewLoadCarrySlots.HasValue) SetField(def, "m_LoadCarrySlots", patch.NewLoadCarrySlots.Value);
                if (patch.NewRecyclingIngredients != null) SetRecyclingIngredients(def, patch.NewRecyclingIngredients);
                if (patch.NewCategory.HasValue) SetField(def, "m_Category", (ItemManager.ItemCategory)patch.NewCategory.Value);

                MMLog.Write($"[ContentInjector] Patched item '{patch.TargetItemId}' successfully.");
            }
        }

        private static void SetRecyclingIngredients(GameItemDefinition def, List<RecipeIngredient> ingredients)
        {
            var parts = new List<GameItemDefinition.BasePart>();
            foreach (var ing in ingredients)
            {
                if (ResolveItemType(ing.ItemId, out var type))
                {
                    parts.Add(new GameItemDefinition.BasePart { item = type, count = ing.Count });
                }
            }
            SetField(def, "m_itemBasePart", parts);
        }

        private static void BuildItemMap()
        {
            ItemKeyToType.Clear();
            ResolvedByType.Clear();
            var items = ContentResolver.ResolveItems();
            MMLog.Write($"[ContentInjector] BuildItemMap: ContentResolver returned {items.Count} items");
            foreach (var item in items)
            {
                if (item?.Definition == null) continue;
                var typeId = (ItemManager.ItemType)ContentRegistry.EnsureCustomTypeId(item.Definition);
                ItemKeyToType[item.Definition.Id] = typeId;
                ResolvedByType[typeId] = item;
                MMLog.Write($"[ContentInjector] Mapped item: {item.Definition.Id} -> {typeId} ({(int)typeId})");
            }
            MMLog.Write($"[ContentInjector] BuildItemMap complete: {ResolvedByType.Count} items mapped");
        }

        private static void BuildRuntimeDefinitions()
        {
            RuntimeDefinitions.Clear();
            foreach (var kvp in ResolvedByType)
            {
                var definition = kvp.Value.Definition;
                var itemType = kvp.Key;

                // Use Wood as a base template for physical properties
                var sourceTemplate = ItemManager.Instance.GetItemDefinition(ItemManager.ItemType.Wood);
                var def = UnityEngine.Object.Instantiate(sourceTemplate);

                def.gameObject.SetActive(false);
                def.name = "ModItem_" + definition.Id;
                UnityEngine.Object.DontDestroyOnLoad(def.gameObject);

                SetField(def, "m_Type", itemType);
                SetField(def, "m_NameLocalizationKey", definition.DisplayName);
                SetField(def, "m_DescLocalizationKey", definition.Description);
                
                // Map Category
                var category = (ItemManager.ItemCategory)definition.Category;
                SetField(def, "m_Category", category);
                
                // Apply specific item properties
                SetField(def, "m_StackSize", definition.StackSize);
                SetField(def, "m_TradeValue", definition.TradeValue);
                SetField(def, "m_BaseCraftTime", definition.BaseCraftTime);
                SetField(def, "m_BurnValue", definition.BurnValue);
                SetField(def, "m_ScrapValue", definition.ScrapValue);
                SetField(def, "m_FabricationCost", definition.FabricationCost);
                SetField(def, "m_BaseFabricationTime", definition.BaseFabricationTime);
                SetField(def, "m_RationValue", definition.RationValue);
                SetField(def, "m_Contamination", definition.Contamination);
                SetField(def, "m_LoadCarrySlots", definition.LoadCarrySlots);
                
                
                // Validation: ObjectType requires Object category
                if (definition.ObjectType != ObjectManager.ObjectType.Undefined && 
                    definition.Category != ItemCategory.Object)
                {
                    MMLog.Write($"[ContentInjector] WARNING: Item '{definition.Id}' has ObjectType " +
                                $"but Category is {definition.Category}, not Object. " +
                                $"Item may not spawn correctly.");
                }
                
                // Object Properties
                SetField(def, "m_ObjectType", definition.ObjectType);
                SetField(def, "m_ObjectLevel", definition.ObjectLevel);

                if (definition.RecyclingIngredients.Count > 0)
                    SetRecyclingIngredients(def, definition.RecyclingIngredients);

                SetField(def, "m_CraftStackSize", definition.CraftStackSize);
                SetField(def, "m_ItemType", itemType);

                // Track raw food types for cooking system
                if (definition.IsRawFood) _rawFoodTypes.Add(itemType);

                RuntimeDefinitions[itemType] = def;
                MMLog.Write($"[ContentInjector] Built definition for {definition.Id} (Category: {category}, Stack: {definition.StackSize})");
            }
        }


        private static void InjectIntoItemManager()
        {
            var im = ItemManager.Instance;
            if (Safe.TryGetField(im, "m_ItemDefinitions", out Dictionary<ItemManager.ItemType, GameItemDefinition> dict))
            {
                foreach (var kvp in RuntimeDefinitions) dict[kvp.Key] = kvp.Value;
                MMLog.Write($"[ContentInjector] Injected {RuntimeDefinitions.Count} items into ItemManager.");
            }
        }

        private static void InjectRecipes()
        {
            var cm = CraftingManager.Instance;
            MMLog.Write($"[ContentInjector] InjectRecipes: Processing {ContentRegistry.Recipes.Count} recipe definitions");

            int added = 0;
            int failed = 0;

            foreach (var def in ContentRegistry.Recipes)
            {
                if (!ItemKeyToType.TryGetValue(def.ResultItemId, out var resultType))
                {
                    // Maybe it's a vanilla item ID?
                    if (!TryParseEnum<ItemManager.ItemType>(def.ResultItemId, out resultType))
                    {
                        MMLog.Write($"[ContentInjector] Recipe '{def.Id}' skipped - result item '{def.ResultItemId}' not found");
                        failed++;
                        continue;
                    }
                }

                // Resolve Ingredients
                var ingredients = new List<CraftingManager.Recipe.Ingredient>();
                foreach (var ingDef in def.Ingredients)
                {
                    if (ResolveItemType(ingDef.ItemId, out var ingType))
                    {
                        ingredients.Add(new CraftingManager.Recipe.Ingredient { Item = ingType, Quantity = ingDef.Count });
                    }
                    else
                    {
                        MMLog.Write($"[ContentInjector] Warning: Recipe '{def.Id}' ingredient '{ingDef.ItemId}' could not be resolved. Skipping.");
                    }
                }

                if (ingredients.Count == 0)
                {
                    MMLog.Write($"[ContentInjector] Recipe '{def.Id}' has no valid ingredients. Skipping.");
                    failed++;
                    continue;
                }

                var recipe = new CraftingManager.Recipe(resultType, ingredients.ToArray())
                {
                    ID = def.Id,
                    level = def.Level,
                    unique = def.Unique,
                    locked = def.Locked
                };
                
                recipe.location = MapStation(def.Station);

                // Set unlockFlag via reflection if it exists and is specified
                if (!string.IsNullOrEmpty(def.UnlockFlag))
                {
                    // Note: Game's Recipe class doesn't appear to have unlockFlag field
                    // Unlocking is handled via CraftingManager.UnlockRecipe(id) method
                    // This field is kept for future compatibility
                }

                // ResultCount is now handled by the item's m_CraftStackSize
                // which was already set in BuildRuntimeDefinitions for custom items.

                // Validate the mapped station
                if (!IsValidCraftLocation(recipe.location))
                {
                    MMLog.Write($"[ContentInjector] ERROR: Recipe '{def.Id}' has invalid station '{def.Station}'. Valid stations: Workbench, Laboratory, AmmoPress");
                    failed++;
                    continue;
                }

                if (cm.AddRecipe(recipe))
                {
                    added++;
                    MMLog.Write($"[ContentInjector] Added recipe '{def.Id}' producing {resultType} @ {recipe.location} Lv{recipe.level} (Unique: {def.Unique}, Locked: {def.Locked})");
                    
                    if (Safe.TryGetField(cm, "m_RecipesInspector", out List<CraftingManager.Recipe> inspector))
                    {
                        if (!inspector.Contains(recipe)) inspector.Add(recipe);
                    }
                }
                else
                {
                    failed++;
                }
            }

            MMLog.Write($"[ContentInjector] Recipe injection complete: {added} added, {failed} failed");
            
            ApplyRecipePatches();
        }

        private static void ApplyRecipePatches()
        {
            var cm = CraftingManager.Instance;
            if (ContentRegistry.RecipePatches.Count == 0) return;

            MMLog.Write($"[ContentInjector] Applying {ContentRegistry.RecipePatches.Count} recipe patches...");
            
            foreach (var patch in ContentRegistry.RecipePatches)
            {
                var recipe = cm.GetRecipeByID(patch.TargetRecipeId);
                if (recipe == null)
                {
                    MMLog.Write($"[ContentInjector] Recipe patch failed: Target recipe '{patch.TargetRecipeId}' not found.");
                    continue;
                }

                // Handle Removals
                if (patch.RemoveIngredientIds.Count > 0)
                {
                    var current = new List<CraftingManager.Recipe.Ingredient>(recipe.Input);
                    int removed = 0;
                    foreach (var id in patch.RemoveIngredientIds)
                    {
                        if (ResolveItemType(id, out var type))
                        {
                            removed += current.RemoveAll(i => i.Item == type);
                        }
                    }
                    recipe.Input = current.ToArray();
                    if (removed > 0) MMLog.Write($"[ContentInjector] Removed {removed} ingredients from recipe '{patch.TargetRecipeId}'");
                }

                // Handle Additions
                if (patch.AddIngredients.Count > 0)
                {
                    var current = new List<CraftingManager.Recipe.Ingredient>(recipe.Input);
                    foreach (var add in patch.AddIngredients)
                    {
                        if (ResolveItemType(add.ItemId, out var type))
                            current.Add(new CraftingManager.Recipe.Ingredient { Item = type, Quantity = add.Count });
                    }
                    recipe.Input = current.ToArray();
                }

                // Handle Replacements
                if (patch.ReplaceIngredients.Count > 0)
                {
                    var newIngredients = new List<CraftingManager.Recipe.Ingredient>(recipe.Input);
                    foreach (var repl in patch.ReplaceIngredients)
                    {
                        if (ResolveItemType(repl.ItemId, out var type))
                        {
                            int idx = newIngredients.FindIndex(i => i.Item == type);
                            if (idx >= 0) newIngredients[idx] = new CraftingManager.Recipe.Ingredient { Item = type, Quantity = repl.Count };
                            else newIngredients.Add(new CraftingManager.Recipe.Ingredient { Item = type, Quantity = repl.Count });
                        }
                    }
                    recipe.Input = newIngredients.ToArray();
                }

                if (patch.ReplaceOutput && ResolveItemType(patch.NewResultItemId, out var outType))
                {
                    recipe.Result = outType;
                }

                if (patch.SetUnique.HasValue) recipe.unique = patch.SetUnique.Value;
                if (patch.SetLocked.HasValue) recipe.locked = patch.SetLocked.Value;
                
                MMLog.Write($"[ContentInjector] Patched recipe '{patch.TargetRecipeId}' successfully.");
            }
        }

        private static bool ResolveItemType(string id, out ItemManager.ItemType type)
        {
            if (ItemKeyToType.TryGetValue(id, out type)) return true;
            if (TryParseEnum<ItemManager.ItemType>(id, out type)) return true;
            type = ItemManager.ItemType.Undefined;
            return false;
        }

        private static bool IsValidCraftLocation(CraftingManager.CraftLocation loc)
        {
            return loc == CraftingManager.CraftLocation.Workbench ||
                   loc == CraftingManager.CraftLocation.Lab ||
                   loc == CraftingManager.CraftLocation.AmmoPress;
        }

        private static CraftingManager.CraftLocation MapStation(CraftStation station)
        {
            switch (station)
            {
                case CraftStation.Laboratory: return CraftingManager.CraftLocation.Lab;
                case CraftStation.AmmoPress: return CraftingManager.CraftLocation.AmmoPress;
                case CraftStation.Workbench:
                default: return CraftingManager.CraftLocation.Workbench;
            }
        }

        private static bool TryParseEnum<T>(string value, out T result) where T : struct
        {
            try
            {
                if (Enum.IsDefined(typeof(T), value))
                {
                    result = (T)Enum.Parse(typeof(T), value, true);
                    return true;
                }
            }
            catch { }

            // Manual check for case-insensitive
            foreach (string name in Enum.GetNames(typeof(T)))
            {
                if (name.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    result = (T)Enum.Parse(typeof(T), name);
                    return true;
                }
            }

            result = default(T);
            return false;
        }

        [HarmonyPatch(typeof(ExpeditionMap), "LoadRegionPrefabs")]
        static class Patch_LootInjection
        {
            static void Postfix(ExpeditionMap __instance)
            {
                if (ContentRegistry.LootEntries.Count == 0) return;
                
                MMLog.Write($"[LootInjection] Injecting {ContentRegistry.LootEntries.Count} loot entries into MapRegion prefabs...");
                
                var prefabs = (Dictionary<string, UnityEngine.Object>)AccessTools.Field(typeof(ExpeditionMap), "m_regionPrefabs").GetValue(__instance);
                if (prefabs == null) return;

                foreach (var entry in ContentRegistry.LootEntries)
                {
                    if (!ResolveItemType(entry.ItemId, out var itemType)) continue;

                    if (entry.LootTableId.Equals("Common", StringComparison.OrdinalIgnoreCase))
                    {
                        var commonItems = (List<ItemBias>)AccessTools.Field(typeof(ExpeditionMap), "m_commonItems").GetValue(__instance);
                        if (commonItems != null)
                        {
                            commonItems.Add(new ItemBias { itemType = itemType, bias = (int)entry.Weight });
                            MMLog.Write($"[LootInjection] Added {entry.ItemId} to Common loot pool (Weight: {entry.Weight})");
                        }
                        continue;
                    }

                    foreach (var prefabKvp in prefabs)
                    {
                        var go = prefabKvp.Value as GameObject;
                        if (go == null) continue;
                        
                        var region = go.GetComponent<MapRegion>();
                        if (region == null) continue;

                        bool matches = region.topography.ToString().Equals(entry.LootTableId, StringComparison.OrdinalIgnoreCase) ||
                                       region.name.Contains(entry.LootTableId);

                        if (matches)
                        {
                            var locItems = (List<ItemBias>)AccessTools.Field(typeof(MapRegion), "m_locationSpecificItemTypes").GetValue(region);
                            if (locItems != null)
                            {
                                locItems.Add(new ItemBias { itemType = itemType, bias = (int)entry.Weight });
                                MMLog.Write($"[LootInjection] Added {entry.ItemId} to {prefabKvp.Key} prefab (Topography: {region.topography})");
                            }
                        }
                    }
                }
            }
        }

        private static void VerifyExistence()
        {
            foreach (var kvp in RuntimeDefinitions)
            {
                bool found = ItemManager.Instance.HasBeenDefined(kvp.Key);
                MMLog.Write($"[ContentInjector] VERIFY: {kvp.Key} ({(int)kvp.Key}) exists: {found}");
            }
        }

        private static void SetField<T>(object obj, string name, T val)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) f.SetValue(obj, val);
        }

        [HarmonyPatch(typeof(ItemButtonBase), "UpdateSprite")]
        static class Patch_Icon
        {
            static void Postfix(ItemButtonBase __instance)
            {
                // MMLog.Write($"[Patch_Icon] UpdateSprite for {__instance.name}, Type: {__instance.m_type}, Slot: {__instance.slotIndex}");

                if ((int)__instance.m_type >= CustomItemTypeStart)
                {
                    if (ResolvedByType.TryGetValue(__instance.m_type, out var res))
                    {
                        if (res.Icon == null)
                        {
                            MMLog.Write($"[Patch_Icon] WARNING: Custom item {__instance.m_type} has no icon. " +
                                $"Define IconPath (relative to Assets/) in ItemDefinition to fix this.");
                            return;
                        }

                        // 1. Find or create the custom icon holder attached to the BUTTON, not the sprite
                        //    This prevents weird inheritance issues if the sprite is disabled/modified.
                        Transform t = __instance.transform.Find("CustomModIcon");
                        UI2DSprite ui2d = null;

                        if (t == null)
                        {
                            GameObject go = new GameObject("CustomModIcon");
                            t = go.transform;
                            t.parent = __instance.transform;
                            t.localPosition = Vector3.zero;
                            t.localRotation = Quaternion.identity;
                            t.localScale = Vector3.one;
                            go.layer = __instance.gameObject.layer;
                            MMLog.Write($"[Patch_Icon] Created CustomModIcon for custom item {__instance.m_type}");
                        }
                        
                        ui2d = t.GetComponent<UI2DSprite>() ?? t.gameObject.AddComponent<UI2DSprite>();

                        // 2. Configure the custom sprite using the vanilla sprite as a layout template
                        if (__instance.m_sprite != null)
                        {
                            // Copy layout from m_sprite
                            if (ui2d.sprite2D != res.Icon) 
                            { 
                                ui2d.sprite2D = res.Icon;
                                // MMLog.Write($"[Patch_Icon] Set sprite2D to {res.Icon?.name}");
                            }

                            // Match the vanilla sprite's geometry
                            ui2d.width = __instance.m_sprite.width;
                            ui2d.height = __instance.m_sprite.height;
                            ui2d.pivot = __instance.m_sprite.pivot;
                            ui2d.depth = __instance.m_sprite.depth; // Use exact depth to respect overlay widgets (ticks/crosses)
                            
                            // Match position (important if the sprite is offset)
                            t.localPosition = __instance.m_sprite.transform.localPosition;

                            // 3. Handle Visibility/State
                            bool lockedOrHidden = __instance.IsLocked || __instance.IsHidden;
                            
                            // Show custom only if visible
                            ui2d.alpha = lockedOrHidden ? 0f : 1f;
                            t.gameObject.SetActive(!lockedOrHidden);

                            // Hide vanilla only if visible
                            __instance.m_sprite.alpha = lockedOrHidden ? 1f : 0f;

                             // MMLog.Write($"[Patch_Icon] Applied. Visible: {!lockedOrHidden}, Depth: {ui2d.depth}, Pos: {t.localPosition}");
                        }
                        else
                        {
                             // Fallback if no vanilla sprite exists to copy from (unlikely)
                             ui2d.sprite2D = res.Icon;
                             ui2d.depth = 100;
                             // MMLog.Write("[Patch_Icon] Warning: No vanilla m_sprite found to copy layout from.");
                        }
                    }
                }
                else
                {
                    // Clean up custom icon if this slot is now specific to a vanilla item
                    Transform t = __instance.transform.Find("CustomModIcon");
                    if (t != null)
                    {
                        var ui2d = t.GetComponent<UI2DSprite>();
                        if (ui2d != null) ui2d.sprite2D = null;
                        t.gameObject.SetActive(false);
                    }
                    
                    // Restore vanilla sprite visibility
                    if (__instance.m_sprite != null && __instance.m_sprite.alpha < 0.1f)
                    {
                        __instance.m_sprite.alpha = 1f;
                        // MMLog.Write($"[Patch_Icon] Restored vanilla sprite for type {__instance.m_type}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ItemButtonBase), "SetLocked")]
        static class Patch_Locked
        {
            static void Postfix(ItemButtonBase __instance, bool locked)
            {
                if ((int)__instance.m_type >= CustomItemTypeStart)
                {
                    Transform t = __instance.transform.Find("CustomModIcon");
                    if (t != null)
                    {
                        t.gameObject.SetActive(!locked);
                        var ui2d = t.GetComponent<UI2DSprite>();
                        if (ui2d != null) ui2d.alpha = locked ? 0f : 1f;
                    }
                    
                    if (__instance.m_sprite != null)
                        __instance.m_sprite.alpha = locked ? 1f : 0f;
                }
            }
        }

        private static void InjectCookingRecipes()
        {
            _cookingRecipes.Clear();
            foreach (var recipe in ContentRegistry.CookingRecipes)
            {
                if (ResolveItemType(recipe.RawItemId, out var rawType))
                {
                    _cookingRecipes[rawType] = recipe;
                    MMLog.Write($"[ContentInjector] Injected cooking recipe: {rawType} -> {recipe.CookedItemId ?? "self"} (x{recipe.HungerMultiplier})");
                }
                else
                {
                    MMLog.Write($"[ContentInjector] WARNING: Cooking recipe raw item '{recipe.RawItemId}' could not be resolved.");
                }
            }
        }

        [HarmonyPatch(typeof(ItemButtonBase), "SetHidden")]
        static class Patch_Hidden
        {
            static void Postfix(ItemButtonBase __instance, bool hidden)
            {
                if ((int)__instance.m_type >= CustomItemTypeStart)
                {
                    Transform t = __instance.transform.Find("CustomModIcon");
                    if (t != null)
                    {
                        t.gameObject.SetActive(!hidden);
                        var ui2d = t.GetComponent<UI2DSprite>();
                        if (ui2d != null) ui2d.alpha = hidden ? 0f : 1f;
                    }

                    if (__instance.m_sprite != null)
                        __instance.m_sprite.alpha = hidden ? 1f : 0f;
                }
            }
        }
    }
}