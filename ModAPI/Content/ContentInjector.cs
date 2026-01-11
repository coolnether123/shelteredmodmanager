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
                // Wood is a safe base, but we must override crafting/stacking defaults
                var woodTemplate = ItemManager.Instance.GetItemDefinition(ItemManager.ItemType.Wood);
                var def = UnityEngine.Object.Instantiate(woodTemplate);

                def.gameObject.SetActive(false);
                def.name = "ModItem_" + kvp.Value.Definition.Id;
                UnityEngine.Object.DontDestroyOnLoad(def.gameObject);

                SetField(def, "m_Type", kvp.Key);
                SetField(def, "m_NameLocalizationKey", kvp.Value.Definition.DisplayName);
                SetField(def, "m_DescLocalizationKey", kvp.Value.Definition.Description);
                SetField(def, "m_Category", ItemManager.ItemCategory.Medicine);
                
                // Ensure the item can actually be given to the player
                SetField(def, "m_StackSize", 64);
                SetField(def, "m_CraftStackSize", 1); // CRITICAL: If 0, FinishCraft fails to add to inventory
                SetField(def, "m_TradeValue", 20);
                SetField(def, "m_BaseCraftTime", 10f);
                SetField(def, "m_ItemType", kvp.Key);

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
                // MMLog.Write($"[Patch_Icon] UpdateSprite for {__instance.name}, Type: {__instance.m_type}, Slot: {__instance.slotIndex}");

                if ((int)__instance.m_type >= CustomItemTypeStart)
                {
                    if (ResolvedByType.TryGetValue(__instance.m_type, out var res))
                    {
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