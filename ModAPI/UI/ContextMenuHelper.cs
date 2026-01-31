using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Simplifies adding custom options to the right-click context menu.
    /// </summary>
    public static class ContextMenuHelper
    {
        private struct ContextMenuAddon
        {
            public string OptionName;
            public string DisplayText;
            public Action OnSelected;
            public Func<Obj_Base, bool> Predicate;
        }

        private static List<ContextMenuAddon> _addons = new List<ContextMenuAddon>();

        /// <summary>
        /// Registers a global addon for the context menu. 
        /// Use the predicate to only show it for specific objects.
        /// </summary>
        public static void RegisterAddon(string optionName, string displayText, Action onSelected, Func<Obj_Base, bool> predicate = null)
        {
            _addons.Add(new ContextMenuAddon
            {
                OptionName = optionName,
                DisplayText = displayText,
                OnSelected = onSelected,
                Predicate = predicate
            });
        }

        [HarmonyPatch(typeof(ContextMenuPanel), "PushNewLayer", typeof(string), typeof(List<string>), typeof(List<string>), typeof(ContextMenuPanel.OptionSelectionCallback))]
        private static class ContextMenuPanel_PushNewLayer_Patch
        {
            private static void Prefix(ref List<string> optionsList, ref List<string> optionsTextList)
            {
                // Find what object is currently selected
                Obj_Base selected = InteractionManager.Instance?.SelectedObject;
                
                foreach (var addon in _addons)
                {
                    if (addon.Predicate == null || (selected != null && addon.Predicate(selected)))
                    {
                        optionsList.Add("MOD_ADDON_" + addon.OptionName);
                        optionsTextList.Add(addon.DisplayText);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ContextMenuPanel), "ButtonPressed")]
        private static class ContextMenuPanel_ButtonPressed_Patch
        {
            private static bool Prefix(object button)
            {
                // ContextMenuButton is a private class or component
                string option = Safe.GetField<string>(button, "m_option");
                if (option != null && option.StartsWith("MOD_ADDON_"))
                {
                    string addonName = option.Substring("MOD_ADDON_".Length);
                    foreach (var addon in _addons)
                    {
                        if (addon.OptionName == addonName)
                        {
                            addon.OnSelected?.Invoke();
                            return false; // Consume click
                        }
                    }
                }
                return true;
            }
        }
    }
}
