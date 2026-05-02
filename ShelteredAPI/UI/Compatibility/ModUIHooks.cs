using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using ModAPI.Harmony;
using ShelteredAPI.UI.Internal;
using ModAPI.Reflection;
using UnityEngine;

namespace ShelteredAPI.UI.Compatibility
{
    /// <summary>
    /// Supported menus for UI injection.
    /// </summary>
    internal enum TargetMenu
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
    internal static class ModUIHooks
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
