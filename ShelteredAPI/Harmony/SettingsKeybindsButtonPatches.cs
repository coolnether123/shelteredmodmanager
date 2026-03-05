using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.UI;
using ShelteredAPI.UI;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Takes over vanilla PC controls entry points and routes to ModAPI keybind UI.
    /// </summary>
    internal static class SettingsKeybindsButtonPatches
    {
        private static readonly HashSet<string> LoggedPanelTypes = new HashSet<string>(StringComparer.Ordinal);

        [HarmonyPatch(typeof(SettingsPCPanel), "OnControlsButtonPressed")]
        [HarmonyPrefix]
        private static bool ControlsPrefix(SettingsPCPanel __instance)
        {
            return TryOpenKeybinds("OnControlsButtonPressed", __instance);
        }

        [HarmonyPatch(typeof(SettingsPCPanel), "OnControlsButtonPressed_PAD")]
        [HarmonyPrefix]
        private static bool ControlsPadPrefix(SettingsPCPanel __instance)
        {
            return TryOpenKeybinds("OnControlsButtonPressed_PAD", __instance);
        }

        [HarmonyPatch(typeof(SettingsConsolePanel), "OnControlsButtonPressed")]
        [HarmonyPrefix]
        private static bool ConsoleControlsPrefix(SettingsConsolePanel __instance)
        {
            return TryOpenKeybinds("SettingsConsolePanel.OnControlsButtonPressed", __instance);
        }

        [HarmonyPatch(typeof(UIPanelManager), "PushPanel", new[] { typeof(BasePanel) })]
        [HarmonyPrefix]
        private static bool PushPanelPrefix(BasePanel panel)
        {
            if (panel == null) return true;

            string typeName = panel.GetType().FullName ?? panel.GetType().Name;
            if (!LoggedPanelTypes.Contains(typeName))
            {
                LoggedPanelTypes.Add(typeName);
                MMLog.WriteInfo("[SettingsKeybindsButtonPatches] UIPanelManager.PushPanel saw panel type: " + typeName);
            }

            if (!(panel is ControllerPanel)) return true;

            BasePanel sourcePanel = null;
            if (UIPanelManager.instance != null)
                sourcePanel = UIPanelManager.instance.GetTopPanel();

            return TryOpenKeybinds("UIPanelManager.PushPanel(ControllerPanel)", sourcePanel);
        }

        private static bool TryOpenKeybinds(string source, BasePanel sourcePanel)
        {
            try
            {
                if (sourcePanel != null && sourcePanel.gameObject != null)
                    UIFontCache.SeedFromGameObject(sourcePanel.gameObject, source);

                HidePanelsForKeybindOpen(sourcePanel, source);
                MMLog.WriteInfo("[SettingsKeybindsButtonPatches] Opening ModAPI keybinds from " + source + ".");
                ShelteredKeybindsUI.Show();
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SettingsKeybindsButtonPatches] Failed to open keybinds from " + source + ": " + ex.Message);
                return true;
            }
        }

        private static void HidePanelsForKeybindOpen(BasePanel sourcePanel, string source)
        {
            var panels = CollectPanelsToHide(sourcePanel);
            if (panels.Count == 0) return;

            // Pop/hide top-most panels first to keep panel stack transitions valid.
            panels.Sort((a, b) => GetTransformDepth(b).CompareTo(GetTransformDepth(a)));

            for (int i = 0; i < panels.Count; i++)
                HidePanelInstance(panels[i], source);
        }

        private static List<BasePanel> CollectPanelsToHide(BasePanel sourcePanel)
        {
            var result = new List<BasePanel>();
            AddUniquePanel(result, sourcePanel);

            if (UIPanelManager.instance != null)
                AddUniquePanel(result, UIPanelManager.instance.GetTopPanel());

            AddPanelAndParents(result, sourcePanel);

            if (UIPanelManager.instance != null)
                AddPanelAndParents(result, UIPanelManager.instance.GetTopPanel());

            return result;
        }

        private static void AddPanelAndParents(List<BasePanel> target, BasePanel panel)
        {
            if (panel == null || panel.transform == null) return;

            var parents = panel.GetComponentsInParent<BasePanel>(true);
            if (parents == null) return;

            for (int i = 0; i < parents.Length; i++)
                AddUniquePanel(target, parents[i]);
        }

        private static void AddUniquePanel(List<BasePanel> target, BasePanel panel)
        {
            if (panel == null) return;
            if (!target.Contains(panel))
                target.Add(panel);
        }

        private static int GetTransformDepth(BasePanel panel)
        {
            if (panel == null || panel.transform == null) return 0;

            int depth = 0;
            var current = panel.transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static void HidePanelInstance(BasePanel panel, string source)
        {
            if (panel == null) return;

            try
            {
                // Hide immediately so the vanilla settings panel does not visually overlap the ModAPI panel.
                if (panel.gameObject != null)
                    panel.gameObject.SetActive(false);

                if (UIPanelManager.instance != null)
                    UIPanelManager.instance.PopPanel(panel);

                MMLog.WriteInfo("[SettingsKeybindsButtonPatches] Hid panel " + panel.GetType().Name + " before " + source + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SettingsKeybindsButtonPatches] Could not hide panel for " + source + ": " + ex.Message);
            }
        }
    }
}
