using System;
using System.Collections;
using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Application.Runtime;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    /// <summary>
    /// Restores the vanilla HUD and family-selection state when the authoring
    /// shell hands control back to the game for an in-place playtest.
    /// </summary>
    internal sealed class ScenarioPlaytestVanillaUiService : IScenarioPlaytestUiService
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo CutscenePanelsField = typeof(CutsceneManager).GetField("panelsToDisable", InstancePrivate);
        private static readonly FieldInfo PanelInputActiveField = typeof(UIPanelManager).GetField("m_bInputActive", InstancePrivate);
        private static readonly FieldInfo PanelNextFrameInputActiveField = typeof(UIPanelManager).GetField("m_bNextFrameInputActive", InstancePrivate);
        private static readonly FieldInfo PanelIgnoreInputField = typeof(UIPanelManager).GetField("m_bIgnoreInput", InstancePrivate);
        private static readonly FieldInfo PanelTimePausedField = typeof(UIPanelManager).GetField("m_bTimePaused", InstancePrivate);

        public void RestoreForPlaytest()
        {
            int restoredPanels = RestoreCutsceneManagedPanels();
            restoredPanels += RestoreHudPanel(UI_PanelContainer.Instance != null ? UI_PanelContainer.Instance.AvatarPanel : null);
            restoredPanels += RestoreHudPanel(UI_PanelContainer.Instance != null ? UI_PanelContainer.Instance.TimePanel : null);

            RestorePanelInput();
            RestoreFamilySelection();
            AudioListener.pause = false;
            MMLog.WriteInfo("[ScenarioPlaytestVanillaUi] Restored vanilla playtest controls and "
                + restoredPanels + " hidden HUD panel(s).");
        }

        private static int RestoreCutsceneManagedPanels()
        {
            CutsceneManager manager = CutsceneManager.Instance;
            if (manager == null || manager.CutSceneActive || CutscenePanelsField == null)
                return 0;

            IList panels = CutscenePanelsField.GetValue(manager) as IList;
            int restored = 0;
            for (int i = 0; panels != null && i < panels.Count; i++)
            {
                UIPanel panel = panels[i] as UIPanel;
                if (panel == null || panel.gameObject == null || panel.gameObject.activeSelf)
                    continue;

                panel.gameObject.SetActive(true);
                restored++;
            }

            return restored;
        }

        private static int RestoreHudPanel(Component panel)
        {
            if (panel == null || panel.gameObject == null || panel.gameObject.activeSelf)
                return 0;

            panel.gameObject.SetActive(true);
            return 1;
        }

        private static void RestorePanelInput()
        {
            UIPanelManager manager = UIPanelManager.instance;
            if (manager == null)
                return;

            SetBool(manager, PanelInputActiveField, true);
            SetBool(manager, PanelNextFrameInputActiveField, true);
            SetBool(manager, PanelIgnoreInputField, false);
            SetBool(manager, PanelTimePausedField, false);
        }

        private static void RestoreFamilySelection()
        {
            InteractionManager interaction = InteractionManager.Instance;
            if (interaction == null)
                return;

            interaction.SetAllowFamilySelection(true);
            if (interaction.GetSelectedFamilyMember() != null)
                return;

            FamilyManager family = FamilyManager.Instance;
            if (family != null && family.GetAllFamilyMembers().Count > 0)
                interaction.SelectNextFamilyMember();
        }

        private static void SetBool(object target, FieldInfo field, bool value)
        {
            if (target != null && field != null)
                field.SetValue(target, value);
        }
    }
}
