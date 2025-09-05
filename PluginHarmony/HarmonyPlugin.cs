using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

/**
 * Maintainer: coolnether123
 * 
 *  Updated to IModPlugin with context-first lifecycle.
 */
public class HarmonyPlugin : IModPlugin 
{
    public HarmonyPlugin() { }

    // Context-first initialize
    public void Initialize(IPluginContext ctx)
    {
        // no-op; keep style consistent. Could pre-load state here.
    }

    // Context-first start
    public void Start(IPluginContext ctx)
    {
        var harmony = new Harmony("com.coolnether123.shelteredmodmanager");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // attach label to this plugin's parent root so it cleans up with the plugin
        LabelComponent label = ctx.PluginRoot.AddComponent<LabelComponent>(); 
        label.setTop(30);
        if (harmony == null) return;

        // Use settings from the context (bound to this mod)
        try
        {
            var settings = ctx.Settings; 
            if (settings != null)
            {
                bool enabled = settings.GetBool("enabled", true);
                int maxCount = settings.GetInt("maxCount", 10);
                // Persist (only if changed); mirrors previous example
                settings.SetBool("enabled", enabled);
                settings.SetInt("maxCount", maxCount);
                settings.SaveUser();
            }
        }
        catch { }

        // Prefer ctx.Log; MMLog still works but ctx.Log prefixes with mod id. (Coolnether123) WIP Plan on making MMLog more robust as a tool)
        ctx.Log.Info(
            "Harmony-Plugin initialized. HasAnyPatches=" + Harmony.HasAnyPatches(harmony.Id)
        );

        label.setText(
            "Harmony-Plugin loaded: " + (harmony != null) + "\n"
            + "Harmony-Id: " + (harmony.Id) + "\n"
            + "Harmony.hasPatches: " + Harmony.HasAnyPatches(harmony.Id)
        );
    }

    [HarmonyPatch(typeof(CraftingPanel), "GetRecipes")]
    public static class CraftingPanel_GetRecipes_Patch
    {
        public static void Postfix(CraftingPanel __instance)
        {
            using (TextWriter tw = File.CreateText(@"harmony_works.log"))
            {
                tw.WriteLine("HarmonyPlugin and Patches initialized!");
                tw.Flush();
            }
        }
    }

    [HarmonyPatch(typeof(Obj_Base), "IsRelocationEnabled")]
    public static class Obj_Base_IsRelocationEnabled_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Obj_Integrity), "IsInteractionAllowed")]
    public static class Obj_Integrity_IsInteractionAllowed_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Obj_Base), "Update")]
    public static class Obj_Base_Update_Patch
    {
        public static bool Prefix(Obj_Base __instance)
        {
            Traverse.Create(__instance).Field("m_Movable").SetValue(true);
            return true;
        }
    }

    /// <summary>
    /// Test patch to add a new recipe to the crafting panel.
    /// </summary>
    [HarmonyPatch(typeof(CraftingPanel), "GetRecipes")]
    public static class AddHingeRecipe_TestPatch
    {
        public static void Postfix(ref List<CraftingManager.Recipe> __result)
        {
            MMLog.Write("[HingeTestPatch] Postfix started.");
            MMLog.Write("[HingeTestPatch] Original recipe count: " + __result.Count);

            // Define the ingredients for our new recipe using verified item types.
            var ingredients = new CraftingManager.Recipe.Ingredient[]
            {
                new CraftingManager.Recipe.Ingredient() { Item = ItemManager.ItemType.Valve, Quantity = 2 },
                new CraftingManager.Recipe.Ingredient() { Item = ItemManager.ItemType.Cement, Quantity = 1 }
            };

            // Create the new recipe object using the correct constructor.
            var newRecipe = new CraftingManager.Recipe(ItemManager.ItemType.Hinge, ingredients);

            // Set other public properties after creation.
            newRecipe.level = 1;
            newRecipe.location = CraftingManager.CraftLocation.Workbench;

            // Add our new recipe to the list that the game will use.
            __result.Add(newRecipe);
            MMLog.Write("[HingeTestPatch] Recipe for Hinge added successfully.");
            MMLog.Write("[HingeTestPatch] New recipe count: " + __result.Count);
        }
    }
}
