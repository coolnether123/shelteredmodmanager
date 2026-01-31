using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
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
    public static class ModUIHooks
    {
        private class ButtonHook
        {
            public TargetMenu Menu;
            public string Text;
            public Action OnClick;
        }

        private static List<ButtonHook> _hooks = new List<ButtonHook>();

        /// <summary>
        /// Registers a button to be injected into a specific game menu when it opens.
        /// </summary>
        public static void RegisterButton(TargetMenu menu, string buttonText, Action onClick)
        {
            _hooks.Add(new ButtonHook { Menu = menu, Text = buttonText, OnClick = onClick });
            ModLog.Debug($"Registered UI hook for {menu}: {buttonText}");
        }

        internal static void ProcessPanel(BasePanel panel)
        {
            TargetMenu? menuType = GetMenuType(panel);
            if (menuType == null) return;

            foreach (var hook in _hooks)
            {
                if (hook.Menu == menuType.Value)
                {
                    InjectButton(panel, hook);
                }
            }
        }

        private static TargetMenu? GetMenuType(BasePanel panel)
        {
            if (panel is RadioDialogPanel) return TargetMenu.Radio;
            // Add other mappings here as needed
            // if (panel is IntercomPanel) return TargetMenu.Intercom;
            return null;
        }

        private static void InjectButton(BasePanel panel, ButtonHook hook)
        {
            try
            {
                // Create a new button. For simplicity, we clone an existing button if possible,
                // or create a basic NGUI button structure.
                
                // For RadioDialogPanel, we'll try to find a suitable parent.
                GameObject parent = panel.gameObject;
                
                // Create GameObject
                GameObject btnObj = new GameObject("ModButton_" + hook.Text);
                btnObj.transform.parent = parent.transform;
                btnObj.transform.localPosition = Vector3.zero; // Should be adjusted
                btnObj.layer = parent.layer;

                // Add NGUI components
                var sprite = btnObj.AddComponent<UISprite>();
                sprite.type = UISprite.Type.Sliced;
                // Try to find a common sprite name like "Button_Normal"
                sprite.spriteName = "button_dark_thin_64"; 
                sprite.width = 200;
                sprite.height = 50;

                var button = btnObj.AddComponent<UIButton>();
                button.tweenTarget = btnObj;

                var labelObj = new GameObject("Label");
                labelObj.transform.parent = btnObj.transform;
                labelObj.transform.localPosition = Vector3.zero;
                labelObj.layer = btnObj.layer;

                var label = labelObj.AddComponent<UILabel>();
                // Try to find a common font
                label.trueTypeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.text = hook.Text;
                label.fontSize = 25;
                label.alignment = NGUIText.Alignment.Center;
                
                // Add click event
                EventDelegate.Add(button.onClick, () => hook.OnClick?.Invoke());

                // Set Depth safely
                NGUIHelper.SetToTopDepth(sprite);
                label.depth = sprite.depth + 1;

                ModLog.Debug($"Injected button '{hook.Text}' into {panel.name}");
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to inject button {hook.Text}: {ex.Message}");
            }
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
