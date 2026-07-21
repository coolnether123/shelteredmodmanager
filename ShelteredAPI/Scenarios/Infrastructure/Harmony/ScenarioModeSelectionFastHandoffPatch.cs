using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony
{
    /// <summary>
    /// Completes the vanilla game-mode book's scenario transition immediately.
    /// Sheltered otherwise waits for the entire reverse alpha tween before it
    /// pushes ScenarioSelectionPanel, which makes the book feel unresponsive.
    /// </summary>
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioModeSelectionFastHandoff",
        TargetBehavior = "Choosing Scenarios from the vanilla game-mode book opens scenario selection immediately.",
        FailureMode = "The game-mode book waits several seconds for its reverse tween before showing scenario selection.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the fast scenario handoff patch.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(GameModeSelectionPanel), "OnScenarioModeChosen")]
    internal static class ScenarioModeSelectionFastHandoffPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            ref bool ___m_inputEnabled,
            ref bool ___m_scenarioModeChosen,
            TweenAlpha ___m_tween,
            BasePanel ___m_slotSelectionPanel,
            BasePanel ___m_scenarioSelectionPanel)
        {
            if (!___m_inputEnabled)
                return false;

            if (___m_tween == null
                || ___m_slotSelectionPanel == null
                || ___m_scenarioSelectionPanel == null
                || UIPanelManager.instance == null)
            {
                return true;
            }

            UIPanelManager manager = UIPanelManager.Instance();
            MethodInfo pushDelayed = AccessTools.Method(typeof(UIPanelManager), "PushPanel_Delayed");
            MethodInfo updateTimeAndInput = AccessTools.Method(typeof(UIPanelManager), "UpdateTimeAndInput");
            if (manager == null || pushDelayed == null || updateTimeAndInput == null)
                return true;

            Stopwatch timer = Stopwatch.StartNew();
            ___m_inputEnabled = false;
            ___m_scenarioModeChosen = true;

            // Sample the reverse tween at its completed state so the outgoing
            // vanilla book does not flash behind the newly pushed panel.
            ___m_tween.enabled = false;
            ___m_tween.tweenFactor = 0f;

            // UIPanelManager.PushPanel defers the real push to a coroutine. On
            // the legacy 32-bit Steam player that coroutine can be throttled by
            // several seconds even though the click handler itself completed.
            // Execute the manager's own delayed implementation synchronously so
            // stack ownership, depth, OnHide/OnShow, and focus behavior remain
            // vanilla while the transition no longer waits for a later frame.
            try
            {
                ScenarioSelectionPanel scenarioPanel = ___m_scenarioSelectionPanel as ScenarioSelectionPanel;
                SlotSelectionPanel embeddedSlots = scenarioPanel != null ? scenarioPanel.selectionPanel : null;
                try
                {
                    // The vanilla OnShow eagerly scans and loads Standard save
                    // descriptions before it paints the two scenario choices.
                    // Those family descriptions do not belong under Surrounded
                    // or Stasis and are the 5+ second Steam stall measured by the
                    // harness. The real slot panel performs its own refresh when
                    // a stock scenario is subsequently opened.
                    if (scenarioPanel != null)
                        scenarioPanel.selectionPanel = null;
                    ___m_scenarioSelectionPanel.OnPushed();
                    pushDelayed.Invoke(manager, new object[] { ___m_scenarioSelectionPanel });
                }
                finally
                {
                    if (scenarioPanel != null)
                        scenarioPanel.selectionPanel = embeddedSlots;
                }
                updateTimeAndInput.Invoke(manager, null);
            }
            catch (System.Exception ex)
            {
                if (!manager.IsPanelOnStack(___m_scenarioSelectionPanel))
                    manager.PushPanel(___m_scenarioSelectionPanel);
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Synchronous scenario handoff fell back to the queued manager push: "
                    + ex.Message);
            }

            timer.Stop();
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Fast scenario-book handoff completed in "
                + timer.ElapsedMilliseconds + "ms.");
            return false;
        }
    }
}
