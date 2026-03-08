using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.Internal.UI;
using ModAPI.Reflection;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Supported menus for UI injection.
    /// </summary>
    public enum TargetMenu
    {
        Radio,
        Intercom,
        Settings,
        Inventory,
        Crafting
    }

    /// <summary>
    /// Standardized way to inject UI elements into existing game panels.
    /// </summary>
    [PatchPolicy(PatchDomain.UI, "ModUIHooks",
        TargetBehavior = "Shared button injection into supported runtime panels",
        FailureMode = "Registered ModUIHooks buttons do not appear when panels open.",
        RollbackStrategy = "Disable the UI patch domain or remove the ModUIHooks patch host.")]
    public static class ModUIHooks
    {
        /// <summary>
        /// Registers a button to be injected into a specific game menu when it opens.
        /// </summary>
        public static void RegisterButton(TargetMenu menu, string buttonText, Action onClick)
        {
            ModUIHookRegistry.Register(menu, buttonText, onClick);
        }

        internal static void ProcessPanel(BasePanel panel)
        {
            ModUIHookRuntimeService.ProcessPanel(panel);
        }

        [HarmonyPatch(typeof(BasePanel), "OnShow")]
        private static class BasePanel_OnShow_Patch
        {
            private static void Postfix(BasePanel __instance)
            {
                ProcessPanel(__instance);
            }
        }
    }
}
