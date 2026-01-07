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
        private static readonly Dictionary<ItemManager.ItemType, GameItemDefinition> RuntimeDefinitions = new Dictionary<ItemManager.ItemType, GameItemDefinition>();

        private static bool _bootstrapped;
        private static readonly object Sync = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnUnityLoad() => TryBootstrap();

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

                // 3. Inject Recipes
                InjectRecipes();

                _bootstrapped = true;
                MMLog.Write("[ContentInjector] Injection complete.");

                // Final Check
                PluginManager.getInstance().EnqueueNextFrame(VerifyExistence);
            }
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
                var woodTemplate = ItemManager.Instance.GetItemDefinition(ItemManager.ItemType.Wood);
                var def = UnityEngine.Object.Instantiate(woodTemplate);

                def.gameObject.SetActive(false);
                def.name = "ModItem_" + kvp.Value.Definition.Id;
                UnityEngine.Object.DontDestroyOnLoad(def.gameObject);

                SetField(def, "m_Type", kvp.Key);
                SetField(def, "m_NameLocalizationKey", kvp.Value.Definition.DisplayName);
                SetField(def, "m_DescLocalizationKey", kvp.Value.Definition.Description);
                SetField(def, "m_Category", ItemManager.ItemCategory.Medicine);

                RuntimeDefinitions[kvp.Key] = def;
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
                // Find our custom ID from the name map
                if (!ItemKeyToType.TryGetValue(def.ResultItemId, out var typeId))
                {
                    MMLog.Write($"[ContentInjector] Recipe '{def.Id}' skipped - item '{def.ResultItemId}' not found in ItemKeyToType map");
                    failed++;
                    continue;
                }

                var ing = new CraftingManager.Recipe.Ingredient { Item = ItemManager.ItemType.Wood, Quantity = 1 };
                var recipe = new CraftingManager.Recipe(typeId, new[] { ing })
                {
                    ID = def.Id,
                    level = 1,
                    location = CraftingManager.CraftLocation.Workbench
                };

                // Use the official AddRecipe method which properly updates internal dictionaries
                if (cm.AddRecipe(recipe))
                {
                    added++;
                    MMLog.Write($"[ContentInjector] Added recipe '{def.Id}' for item {typeId} ({(int)typeId}) at Workbench level 1");
                    
                    // Also add to inspector list if not already there
                    if (Safe.TryGetField(cm, "m_RecipesInspector", out List<CraftingManager.Recipe> inspector))
                    {
                        if (!inspector.Contains(recipe))
                        {
                            inspector.Add(recipe);
                        }
                    }
                }
                else
                {
                    failed++;
                    MMLog.Write($"[ContentInjector] Failed to add recipe '{def.Id}' - AddRecipe returned false");
                }
            }

            MMLog.Write($"[ContentInjector] Recipe injection complete: {added} added, {failed} failed");
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
                if ((int)__instance.m_type >= CustomItemTypeStart && ResolvedByType.TryGetValue(__instance.m_type, out var res))
                {
                    var uiSprite = __instance.GetComponentInChildren<UISprite>();
                    if (uiSprite != null) uiSprite.alpha = 0f;

                    var ui2d = __instance.GetComponent<UI2DSprite>() ?? __instance.gameObject.AddComponent<UI2DSprite>();
                    ui2d.sprite2D = res.Icon;
                    ui2d.depth = 100;
                    ui2d.width = 48; // Force scale up for 16x16 icons
                    ui2d.height = 48;
                }
            }
        }
    }
}