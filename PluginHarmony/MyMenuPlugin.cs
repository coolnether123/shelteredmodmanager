using HarmonyLib;
using ModAPI.Core;
using ModAPI.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Demonstrates Harmony patching inside Sheltered.
/// </summary>
public class MyMenuPlugin : IModPlugin
{
    private IModLogger _log;
    private Harmony _harmony;

    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("[MyMenuPlugin] Initialize called.");
    }

    public void Start(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("[MyMenuPlugin] Start called. Applying patches...");

        HarmonyDemoLogger.Log = _log;

        _harmony = new Harmony("com.shelteredmodmanager.harmonydemo");

        _harmony.Patch(
            original: AccessTools.Method(typeof(MainMenu), "OnShow"),
            postfix: new HarmonyMethod(typeof(MainMenu_OnShow_Patch), nameof(MainMenu_OnShow_Patch.Postfix))
        );

        _harmony.Patch(
            original: AccessTools.Method(typeof(Obj_Integrity), "IsInteractionAllowed", new Type[] { typeof(FamilyMember), typeof(string) }),
            prefix: new HarmonyMethod(typeof(AllowInteractionWithDamagedObjects), nameof(AllowInteractionWithDamagedObjects.Prefix))
        );

        _harmony.Patch(
            original: AccessTools.Method(typeof(CraftingPanel), "GetRecipes"),
            postfix: new HarmonyMethod(typeof(AddCustomRecipeExample), nameof(AddCustomRecipeExample.Postfix))
        );

        _log.Info("[MyMenuPlugin] Harmony patches applied.");
    }
}

public static class HarmonyDemoLogger
{
    public static IModLogger Log;
}

[HarmonyPatch(typeof(MainMenu), "OnShow")]
public static class MainMenu_OnShow_Patch
{
    private const string LabelText = "HarmonyDemo: patches running";

    public static void Postfix(MainMenu __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;

        if (__instance.GetComponent<HarmonyDemoMenuMarker>() != null)
            return;

        var opts = new UIUtil.UILabelOptions
        {
            text = LabelText,
            color = Color.cyan,
            fontSize = 18,
            alignment = NGUIText.Alignment.Left,
            effect = UILabel.Effect.Outline,
            effectColor = Color.black,
            anchor = UIUtil.AnchorCorner.BottomLeft,
            relativeDepth = 50,
            pixelOffset = new Vector2(10, 10)
        };

        UIUtil.CreateLabel(__instance.gameObject, opts, out _);
        __instance.gameObject.AddComponent<HarmonyDemoMenuMarker>();
    }
}

public class HarmonyDemoMenuMarker : MonoBehaviour { }

public static class AddCustomRecipeExample
{
    private static bool _recipeAdded;

    public static void Postfix(ref List<CraftingManager.Recipe> __result)
    {
        if (_recipeAdded || __result == null)
            return;

        try
        {
            var ingredients = new CraftingManager.Recipe.Ingredient[]
            {
                new CraftingManager.Recipe.Ingredient { Item = ItemManager.ItemType.Valve, Quantity = 2 },
                new CraftingManager.Recipe.Ingredient { Item = ItemManager.ItemType.Cement, Quantity = 1 }
            };

            var hingeRecipe = new CraftingManager.Recipe(ItemManager.ItemType.Hinge, ingredients)
            {
                level = 1,
                location = CraftingManager.CraftLocation.Workbench
            };

            __result.Add(hingeRecipe);
            _recipeAdded = true;
        }
        catch (Exception ex)
        {
            HarmonyDemoLogger.Log?.Error($"Inject recipe failed: {ex.Message}");
        }
    }
}

public static class AllowInteractionWithDamagedObjects
{
    public static bool Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
