using HarmonyLib;
using ModAPI.Core;
using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Shows another Harmony example set inside Sheltered.
/// </summary>
public class HarmonyPlugin : IModPlugin
{
    private IModLogger _log;
    private Harmony _harmony;

    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("[HarmonyPlugin] Initialize called.");
    }

    public void Start(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("[HarmonyPlugin] Start called. Registering patches...");

        HarmonyPluginLogger.Log = _log;

        _harmony = new Harmony("com.shelteredmodmanager.harmonyplugin");

        _harmony.Patch(
            original: AccessTools.Method(typeof(ExpeditionMainPanelNew), "Update"),
            postfix: new HarmonyMethod(typeof(ExpeditionPanelUpdatePatch), nameof(ExpeditionPanelUpdatePatch.Postfix))
        );

        _harmony.Patch(
            original: AccessTools.Method(typeof(InventoryManager), "AddNewItem", new Type[] { typeof(ItemManager.ItemType) }),
            prefix: new HarmonyMethod(typeof(InventoryAddNewItemPatch), nameof(InventoryAddNewItemPatch.Prefix))
        );

        _log.Info("[HarmonyPlugin] Patches applied.");
    }
}

[HarmonyPatch]
public static class ExpeditionPanelUpdatePatch
{
    private static float _nextLog;
    private static int _lastCount;

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(ExpeditionMainPanelNew), "Update");
    }

    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null)
            return;

        if (Time.time < _nextLog)
            return;

        int count = __instance.route.Count;
            if (count != _lastCount)
            {
                _lastCount = count;
                HarmonyPluginLogger.Log?.Info($"Expedition route length is now {count}");
            }

        _nextLog = Time.time + 2f;
    }
}

[HarmonyPatch]
public static class InventoryAddNewItemPatch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(InventoryManager), "AddNewItem", new Type[] { typeof(ItemManager.ItemType) });
    }

    public static void Prefix(ItemManager.ItemType itemType)
    {
        HarmonyPluginLogger.Log?.Info($"InventoryManager.AddNewItem called with {itemType}");
    }
}

public static class HarmonyPluginLogger
{
    public static IModLogger Log;
}
