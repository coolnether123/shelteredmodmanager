using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony
{
    /// <summary>
    /// Moves ScenarioSelectionPanel's expensive first NGUI widget initialization
    /// into main-menu idle time and prevents BasePanel.Initialise from repeating it.
    /// </summary>
    internal static class ScenarioSelectionPanelPrewarmCache
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<int> InitializedPanels = new HashSet<int>();
        private static readonly HashSet<int> ActivatedPanels = new HashSet<int>();

        internal static bool TryPrepare()
        {
            GameObject uiRoot = GameObject.Find("UI Root");
            if (uiRoot == null)
                return false;

            ScenarioSelectionPanel[] panels = uiRoot.GetComponentsInChildren<ScenarioSelectionPanel>(true);
            if (panels == null || panels.Length == 0 || panels[0] == null)
                return false;

            ScenarioSelectionPanel panel = panels[0];
            if (IsPrepared(panel))
                return true;

            Stopwatch timer = Stopwatch.StartNew();
            GameObject panelObject = panel.gameObject;
            UIPanel uiPanel = panel.GetComponent<UIPanel>();
            float previousAlpha = uiPanel != null ? uiPanel.alpha : 1f;
            List<GameObject> inactiveAncestors = new List<GameObject>();
            Transform current = panelObject.transform;
            while (current != null && current.gameObject != uiRoot)
            {
                if (!current.gameObject.activeSelf)
                    inactiveAncestors.Add(current.gameObject);
                current = current.parent;
            }
            try
            {
                if (uiPanel != null)
                    uiPanel.alpha = 0f;
                for (int i = inactiveAncestors.Count - 1; i >= 0; i--)
                    inactiveAncestors[i].SetActive(true);
                panel.Initialise();
                panel.OnShow();
                panel.OnHide(false);
            }
            finally
            {
                for (int i = 0; i < inactiveAncestors.Count; i++)
                    inactiveAncestors[i].SetActive(false);
                if (uiPanel != null)
                    uiPanel.alpha = previousAlpha;
            }
            lock (Sync)
                ActivatedPanels.Add(panel.GetInstanceID());
            timer.Stop();
            MMLog.WriteInfo("[ScenarioSelectionPrewarm] Activated " + inactiveAncestors.Count
                + " inactive ancestor(s) and prepared vanilla scenario selection widgets in "
                + timer.ElapsedMilliseconds + "ms.");
            return IsPrepared(panel);
        }

        internal static bool IsPrepared(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return false;

            lock (Sync)
                return ActivatedPanels.Contains(panel.GetInstanceID());
        }

        internal static bool IsInitialized(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return false;

            lock (Sync)
                return InitializedPanels.Contains(panel.GetInstanceID());
        }

        internal static void RememberInitialized(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return;

            lock (Sync)
                InitializedPanels.Add(panel.GetInstanceID());
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioSelectionPanelPrewarmCache",
        TargetBehavior = "The vanilla scenario-selection panel initializes once during menu idle and opens without repeating its expensive NGUI pass.",
        FailureMode = "Opening scenario selection blocks for several seconds while every child widget is synchronously initialized.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario-selection prewarm cache patch.",
        StartupTiming = PatchStartupTiming.MenuCritical)]
    [HarmonyPatch(typeof(BasePanel), "Initialise")]
    internal static class ScenarioSelectionPanelInitialiseCachePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BasePanel __instance, ref UIPanel ___m_ui_panel, out bool __state)
        {
            __state = false;
            ScenarioSelectionPanel panel = __instance as ScenarioSelectionPanel;
            if (panel == null)
                return true;

            if (!ScenarioSelectionPanelPrewarmCache.IsInitialized(panel))
            {
                __state = true;
                return true;
            }

            ___m_ui_panel = panel.GetComponent<UIPanel>();
            return false;
        }

        [HarmonyPostfix]
        private static void Postfix(BasePanel __instance, bool __state)
        {
            if (__state)
                ScenarioSelectionPanelPrewarmCache.RememberInitialized(__instance as ScenarioSelectionPanel);
        }
    }

}
