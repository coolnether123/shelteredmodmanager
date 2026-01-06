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
    /// <summary>
    /// Bridges registry entries into core game systems (items, crafting, loot).
    /// Runs after ItemManager/CraftingManager are alive and intercepts lookups for custom IDs (>= 10000).
    /// </summary>
    public static class ContentInjector
    {
        private const int CustomItemTypeStart = 10000;

        private static readonly Dictionary<string, ItemManager.ItemType> ItemKeyToType = new Dictionary<string, ItemManager.ItemType>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ItemManager.ItemType, ResolvedItem> ResolvedByType = new Dictionary<ItemManager.ItemType, ResolvedItem>();
        private static readonly Dictionary<ItemManager.ItemType, GameItemDefinition> RuntimeDefinitions = new Dictionary<ItemManager.ItemType, GameItemDefinition>();

        private static readonly object Sync = new object();

        private static bool _itemReady;
        private static bool _craftReady;
        private static bool _lootInjected;
        private static bool _bootstrapped;
        private static ItemManager.ItemType _fallbackType = ItemManager.ItemType.Wood;
        private static GameItemDefinition _fallbackDefinition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnUnityLoad()
        {
            // Scene ready means managers likely exist; defer actual work until we confirm below.
            TryBootstrap();
            RegisterSceneRetry();
        }

        internal static void NotifyManagerReady(string name)
        {
            lock (Sync)
            {
                if (string.Equals(name, "ItemManager", StringComparison.OrdinalIgnoreCase))
                    _itemReady = true;
                if (string.Equals(name, "CraftingManager", StringComparison.OrdinalIgnoreCase))
                    _craftReady = true;
            }
            TryBootstrap();
        }

        internal static bool TryResolve(ItemManager.ItemType type, ref GameItemDefinition definition)
        {
            if ((int)type < CustomItemTypeStart)
                return true;

            GameItemDefinition resolved;
            if (RuntimeDefinitions.TryGetValue(type, out resolved) && resolved != null)
            {
                definition = resolved;
                return false;
            }

            MMLog.WarnOnce("ContentInjector.MissingDef." + (int)type, "Custom item " + type + " missing; falling back to safe item.");
            definition = GetFallbackDefinition();
            return false;
        }

        private static void TryBootstrap()
        {
            if (_bootstrapped)
                return;

            if (!_itemReady || !_craftReady)
                return;

            lock (Sync)
            {
                if (_bootstrapped)
                    return;

                _fallbackType = ResolveFallbackType();

                BuildItemMap();
                if (ContentRegistry.Toggles.AutoRegisterItems)
                    BuildRuntimeDefinitions();
                if (ContentRegistry.Toggles.AutoInjectRecipes)
                    InjectRecipes();
                if (ContentRegistry.Toggles.AutoInjectLoot)
                    TryInjectLoot();

                _bootstrapped = true;
                LogSummary();
            }
        }

        private static void BuildItemMap()
        {
            ItemKeyToType.Clear();
            ResolvedByType.Clear();

            var resolvedItems = ContentResolver.ResolveItems();
            var next = CustomItemTypeStart;

            for (int i = 0; i < resolvedItems.Count; i++)
            {
                var item = resolvedItems[i];
                if (item == null || item.Definition == null || string.IsNullOrEmpty(item.Definition.Id))
                {
                    MMLog.WarnOnce("ContentInjector.ItemMissingId." + i, "Skipping item with missing id or definition.");
                    continue;
                }

                var type = (ItemManager.ItemType)next++;
                ItemKeyToType[item.Definition.Id] = type;
                ResolvedByType[type] = item;
            }
        }

        private static void BuildRuntimeDefinitions()
        {
            RuntimeDefinitions.Clear();

            foreach (var kvp in ResolvedByType)
            {
                var runtime = CreateRuntimeDefinition(kvp.Value, kvp.Key);
                RuntimeDefinitions[kvp.Key] = runtime;
            }
        }

        private static GameItemDefinition CreateRuntimeDefinition(ResolvedItem item, ItemManager.ItemType type)
        {
            try
            {
                var prefab = item != null ? item.Prefab : null;
                GameItemDefinition def = null;

                if (prefab != null)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    def = instance != null ? instance.GetComponent<GameItemDefinition>() : null;
                    if (def == null && instance != null)
                        def = instance.AddComponent<GameItemDefinition>();
                }

                if (def == null)
                {
                    def = CloneFallbackDefinition();
                }

                if (def != null)
                {
                    UnityEngine.Object.DontDestroyOnLoad(def.gameObject);
                    SetField(def, "m_Type", type);
                    if (!string.IsNullOrEmpty(item.Definition.DisplayName))
                        SetField(def, "m_NameLocalizationKey", item.Definition.DisplayName);
                    if (!string.IsNullOrEmpty(item.Definition.Description))
                        SetField(def, "m_DescLocalizationKey", item.Definition.Description);

                    var category = ResolveCategory(item.Definition.Category);
                    if (category.HasValue)
                        SetField(def, "m_Category", category.Value);

                    // Attach icon if a sprite was provided; best effort only.
                    if (item.Icon != null)
                        TryApplyIcon(def.gameObject, item.Icon);
                }

                return def ?? GetFallbackDefinition();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ContentInjector.RuntimeDef." + type, "Failed to build runtime item for " + type + ": " + ex.Message);
                return GetFallbackDefinition();
            }
        }

        private static void InjectRecipes()
        {
            var cm = CraftingManager.Instance;
            if (cm == null)
            {
                MMLog.WarnOnce("ContentInjector.Recipes.NoManager", "CraftingManager not available; recipes not injected.");
                return;
            }

            List<CraftingManager.Recipe> inspector;
            if (!Safe.TryGetField(cm, "m_RecipesInspector", out inspector) || inspector == null)
            {
                MMLog.WarnOnce("ContentInjector.Recipes.Field", "Could not access CraftingManager.m_RecipesInspector");
                return;
            }

            int injected = 0;
            for (int i = 0; i < ContentRegistry.Recipes.Count; i++)
            {
                var def = ContentRegistry.Recipes[i];
                var recipe = BuildRecipe(def);
                if (recipe == null)
                    continue;

                inspector.Add(recipe);
                injected++;
            }

            if (injected > 0)
                MMLog.Write($"[ContentInjector] Injected {injected} crafting recipe(s).");
        }

        private static CraftingManager.Recipe BuildRecipe(RecipeDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.ResultItemId))
                return null;

            var result = ResolveItemType(def.ResultItemId);
            if (result == ItemManager.ItemType.Undefined)
            {
                MMLog.WarnOnce("ContentInjector.Recipe.Result." + def.Id, "Recipe result missing for " + def.Id);
                return null;
            }

            var ingredients = new List<CraftingManager.Recipe.Ingredient>();
            for (int i = 0; i < def.Ingredients.Count; i++)
            {
                var ing = def.Ingredients[i];
                if (ing == null || string.IsNullOrEmpty(ing.ItemId))
                    continue;

                var ingType = ResolveItemType(ing.ItemId);
                if (ingType == ItemManager.ItemType.Undefined)
                {
                    MMLog.WarnOnce("ContentInjector.Recipe.Ingredient." + def.Id + "." + i, "Skipping ingredient " + ing.ItemId + " for " + def.Id);
                    continue;
                }

                ingredients.Add(new CraftingManager.Recipe.Ingredient
                {
                    Item = ingType,
                    Quantity = Math.Max(1, ing.Count)
                });
            }

            var recipe = new CraftingManager.Recipe(result, ingredients.ToArray())
            {
                ID = !string.IsNullOrEmpty(def.Id) ? def.Id : ("mod_" + result + "_" + ingredients.Count),
                level = 1,
                locked = !string.IsNullOrEmpty(def.UnlockFlag),
                unique = false,
                location = ResolveStation(def.StationId)
            };

            return recipe;
        }

        private static void TryInjectLoot()
        {
            if (_lootInjected)
                return;

            if (ContentRegistry.LootEntries.Count == 0)
            {
                _lootInjected = true;
                return;
            }

            var regions = UnityEngine.Object.FindObjectsOfType<MapRegion>();
            if (regions == null || regions.Length == 0)
                return;

            int injected = 0;
            for (int i = 0; i < ContentRegistry.LootEntries.Count; i++)
            {
                var entry = ContentRegistry.LootEntries[i];
                var type = ResolveItemType(entry.ItemId);
                if (type == ItemManager.ItemType.Undefined)
                {
                    MMLog.WarnOnce("ContentInjector.Loot.Item." + entry.ItemId, "Loot item missing for " + entry.ItemId);
                    continue;
                }

                for (int r = 0; r < regions.Length; r++)
                {
                    var region = regions[r];
                    if (region == null || !LootMatches(region, entry))
                        continue;

                    List<ItemBias> biasList;
                    if (!Safe.TryGetField(region, "m_locationSpecificItemTypes", out biasList) || biasList == null)
                        continue;

                    var bias = new ItemBias
                    {
                        itemType = type,
                        bias = Math.Max(1, Mathf.RoundToInt(entry.Weight <= 0 ? 1f : entry.Weight))
                    };
                    biasList.Add(bias);
                    injected++;
                }
            }

            if (injected > 0)
                MMLog.Write($"[ContentInjector] Injected {injected} loot entry/entries.");

            _lootInjected = true;
        }

        private static bool LootMatches(MapRegion region, LootEntry entry)
        {
            if (region == null || entry == null)
                return false;

            var id = entry.LootTableId;
            if (string.IsNullOrEmpty(id))
                return true;

            return string.Equals(region.regionName, id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(region.category, id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(region.topography.ToString(), id, StringComparison.OrdinalIgnoreCase);
        }

        private static ItemManager.ItemType ResolveItemType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return ResolveFallbackType();

            ItemManager.ItemType type;
            if (ItemKeyToType.TryGetValue(id, out type))
                return type;

            if (TryParseEnum<ItemManager.ItemType>(id, out type))
                return type;

            MMLog.WarnOnce("ContentInjector.ItemLookup." + id, "Unknown item id '" + id + "', using fallback.");
            return ResolveFallbackType();
        }

        private static ItemManager.ItemType ResolveFallbackType()
        {
            ItemManager.ItemType fallback;
            if (TryParseEnum<ItemManager.ItemType>("BrokenWood", out fallback))
                return fallback;
            return ItemManager.ItemType.Wood;
        }

        private static GameItemDefinition CloneFallbackDefinition()
        {
            if (_fallbackDefinition != null)
                return UnityEngine.Object.Instantiate(_fallbackDefinition);

            try
            {
                var baseDef = ItemManager.Instance != null ? ItemManager.Instance.GetItemDefinition(_fallbackType) : null;
                if (baseDef != null)
                {
                    _fallbackDefinition = baseDef;
                    return UnityEngine.Object.Instantiate(baseDef);
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ContentInjector.Fallback.Clone", "Failed to clone fallback item: " + ex.Message);
            }

            var go = new GameObject("ModItem_Fallback");
            return go.AddComponent<GameItemDefinition>();
        }

        private static void TryApplyIcon(GameObject host, Sprite sprite)
        {
            try
            {
                if (host == null || sprite == null)
                    return;

                var renderer = host.GetComponent<SpriteRenderer>() ?? host.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ContentInjector.Icon", "Failed to apply icon: " + ex.Message);
            }
        }

        private static ItemManager.ItemCategory? ResolveCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return null;

            try
            {
                ItemManager.ItemCategory parsed;
                if (TryParseEnum<ItemManager.ItemCategory>(category, out parsed))
                    return parsed;
            }
            catch { }
            return null;
        }

        private static CraftingManager.CraftLocation ResolveStation(string stationId)
        {
            if (string.IsNullOrEmpty(stationId))
                return CraftingManager.CraftLocation.Workbench;

            try
            {
                CraftingManager.CraftLocation location;
                if (TryParseEnum<CraftingManager.CraftLocation>(stationId, out location))
                    return location;
            }
            catch { }

            return CraftingManager.CraftLocation.Workbench;
        }

        private static bool TryParseEnum<T>(string raw, out T value) where T : struct
        {
            value = default(T);
            if (string.IsNullOrEmpty(raw))
                return false;
            try
            {
                value = (T)Enum.Parse(typeof(T), raw, true);
                return true;
            }
            catch { return false; }
        }

        private static GameItemDefinition GetFallbackDefinition()
        {
            try
            {
                return CloneFallbackDefinition();
            }
            catch
            {
                return null;
            }
        }

        private static void SetField<T>(GameItemDefinition def, string name, T value)
        {
            try
            {
                var f = typeof(GameItemDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null)
                    f.SetValue(def, value);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ContentInjector.SetField." + name, "Failed to set " + name + ": " + ex.Message);
            }
        }

        private static void LogSummary()
        {
            var items = ResolvedByType.Count;
            var recipes = ContentRegistry.Recipes.Count;
            var loot = ContentRegistry.LootEntries.Count;
            MMLog.Write($"[ContentInjector] Bootstrapped items={items}, recipes={recipes}, loot={loot}");
        }

        private static void RegisterSceneRetry()
        {
            try
            {
                if (!RuntimeCompat.IsModernSceneApi)
                    return;

                var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                var sceneLoaded = sceneManagerType != null ? sceneManagerType.GetEvent("sceneLoaded") : null;
                if (sceneLoaded == null)
                    return;

                var handler = Delegate.CreateDelegate(sceneLoaded.EventHandlerType, typeof(ContentInjector).GetMethod("OnSceneLoaded", BindingFlags.NonPublic | BindingFlags.Static));
                sceneLoaded.GetAddMethod()?.Invoke(null, new object[] { handler });
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ContentInjector.SceneHook", "Failed to hook sceneLoaded: " + ex.Message);
            }
        }

        private static void OnSceneLoaded(object scene, object mode)
        {
            TryBootstrap();
            TryInjectLoot();
        }

        // Harmony patches ---------------------------------------------------
        [HarmonyPatch(typeof(ItemManager), "GetItemDefinition")]
        private static class ItemManager_GetItemDefinition_Patch
        {
            private static bool Prefix(ItemManager.ItemType item, ref GameItemDefinition __result)
            {
                return ContentInjector.TryResolve(item, ref __result);
            }
        }

        [HarmonyPatch(typeof(ItemManager), "StartManager")]
        private static class ItemManager_StartManager_Patch
        {
            private static void Postfix()
            {
                ContentInjector.NotifyManagerReady("ItemManager");
            }
        }

        [HarmonyPatch(typeof(CraftingManager), "StartManager")]
        private static class CraftingManager_StartManager_Patch
        {
            private static void Postfix()
            {
                ContentInjector.NotifyManagerReady("CraftingManager");
            }
        }

        [HarmonyPatch(typeof(ExpeditionMap), "Awake")]
        private static class ExpeditionMap_Awake_Patch
        {
            private static void Postfix()
            {
                ContentInjector.TryInjectLoot();
            }
        }
    }
}
