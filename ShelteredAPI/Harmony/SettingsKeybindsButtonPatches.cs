using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.UI;
using ShelteredAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Takes over vanilla PC controls entry points and routes to ModAPI keybind UI.
    /// </summary>
    [PatchPolicy(PatchDomain.Input, "ShelteredSettingsKeybindsTakeover",
        TargetBehavior = "Vanilla controls-entry takeover into ModAPI keybind UI",
        FailureMode = "Sheltered controls open the vanilla path instead of the managed ModAPI keybind UI.",
        RollbackStrategy = "Disable the Input patch domain or remove the Sheltered settings takeover host.")]
    internal static class SettingsKeybindsButtonPatches
    {
        private static readonly HashSet<string> LoggedPanelTypes = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<GameObject> TemporarilyHiddenObjects = new List<GameObject>();
        private static bool _restoreHooked;

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
                MMLog.WriteDebug("[SettingsKeybindsButtonPatches] UIPanelManager.PushPanel saw panel type: " + typeName);
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
                EnsureRestoreHook();
                RestoreTemporarilyHiddenObjects();

                if (sourcePanel != null && sourcePanel.gameObject != null)
                    UIFontCache.SeedFromGameObject(sourcePanel.gameObject, source);

                HidePanelsForKeybindOpen(sourcePanel, source);
                MMLog.WriteDebug("[SettingsKeybindsButtonPatches] Opening ModAPI keybinds from " + source + ".");
                ShelteredKeybindsUI.Show();
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SettingsKeybindsButtonPatches] Failed to open keybinds from " + source + ": " + ex.Message);
                return true;
            }
        }

        private static void EnsureRestoreHook()
        {
            if (_restoreHooked) return;
            _restoreHooked = true;
            ModSettingsPanel.Closed += RestoreTemporarilyHiddenObjects;
        }

        private static void HidePanelsForKeybindOpen(BasePanel sourcePanel, string source)
        {
            TrackSourceAncestorsForTemporaryHide(sourcePanel, source);

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
                    TrackAndHide(panel.gameObject, source);

                MMLog.WriteDebug("[SettingsKeybindsButtonPatches] Hid panel " + panel.GetType().Name + " before " + source + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SettingsKeybindsButtonPatches] Could not hide panel for " + source + ": " + ex.Message);
            }
        }

        private static void TrackSourceAncestorsForTemporaryHide(BasePanel sourcePanel, string source)
        {
            if (sourcePanel == null || sourcePanel.transform == null) return;

            // SettingsPCPanel can be nested; hide nearest visual parent containers too.
            Transform current = sourcePanel.transform.parent;
            int levels = 0;
            while (current != null && levels < 2)
            {
                TrackAndHide(current.gameObject, source);
                current = current.parent;
                levels++;
            }
        }

        private static void TrackAndHide(GameObject obj, string source)
        {
            if (obj == null) return;
            if (!obj.activeSelf) return;
            if (obj.GetComponent<UIRoot>() != null) return;
            if (obj.name.StartsWith("ModAPI_", StringComparison.OrdinalIgnoreCase)) return;

            if (!TemporarilyHiddenObjects.Contains(obj))
                TemporarilyHiddenObjects.Add(obj);

            obj.SetActive(false);
            MMLog.WriteDebug("[SettingsKeybindsButtonPatches] Temporarily hid object " + obj.name + " for " + source + ".");
        }

        private static void RestoreTemporarilyHiddenObjects()
        {
            if (TemporarilyHiddenObjects.Count == 0) return;

            for (int i = 0; i < TemporarilyHiddenObjects.Count; i++)
            {
                var obj = TemporarilyHiddenObjects[i];
                if (obj == null) continue;

                try
                {
                    obj.SetActive(true);
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[SettingsKeybindsButtonPatches] Failed to restore hidden object: " + ex.Message);
                }
            }

            MMLog.WriteDebug("[SettingsKeybindsButtonPatches] Restored " + TemporarilyHiddenObjects.Count + " temporarily hidden settings object(s).");
            TemporarilyHiddenObjects.Clear();
        }
    }
}
