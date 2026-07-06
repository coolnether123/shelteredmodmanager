using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioAuthoringVanillaPanelVisibilityService
    {
        private static readonly FieldInfo PanelStackField = typeof(UIPanelManager).GetField("m_panel_stack", BindingFlags.NonPublic | BindingFlags.Instance);

        public bool HasBlockingPanelOpen()
        {
            UIPanelManager manager = UIPanelManager.instance;
            if (manager == null)
                return false;

            try
            {
                if (manager.GetStackCount() <= 0)
                    return false;

                BasePanel topPanel = manager.GetTopPanel();
                return topPanel != null && topPanel.gameObject != null && topPanel.gameObject.activeInHierarchy;
            }
            catch
            {
                return HasBlockingPanelOpenByReflection(manager);
            }
        }

        private static bool HasBlockingPanelOpenByReflection(UIPanelManager manager)
        {
            if (manager == null || PanelStackField == null)
                return false;

            try
            {
                List<BasePanel> stack = PanelStackField.GetValue(manager) as List<BasePanel>;
                if (stack == null || stack.Count == 0)
                    return false;

                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    BasePanel panel = stack[i];
                    if (panel != null && panel.gameObject != null && panel.gameObject.activeInHierarchy)
                        return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }
    }
}
