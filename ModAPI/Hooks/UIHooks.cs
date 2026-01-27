using UnityEngine;
using ModAPI.Reflection;

namespace ModAPI.Hooks
{
    /// <summary>
    /// High-level hooks for common UI elements in Sheltered.
    /// Provides safe access to NGUI elements and their controllers.
    /// </summary>
    public static class UIHooks
    {
        /// <summary>
        /// Gets the main UI Root object.
        /// </summary>
        public static GameObject GetUIRoot() { return SceneUtil.Find("UI Root"); }

        /// <summary>
        /// Gets the Expedition Map panel.
        /// </summary>
        public static GameObject GetExpeditionMapPanel()
        {
            var p = SceneUtil.Find("UI Root/Panels/ExpeditionMap");
            if (p == null) p = SceneUtil.Find("UI Root/ExpeditionMap");
            return p;
        }

        /// <summary>
        /// Gets the Camera used to render the Expedition Map.
        /// Useful for converting world coordinates to map-pixel coordinates for custom icons.
        /// </summary>
        public static Camera GetMapCamera()
        {
            var panel = GetExpeditionMapPanel();
            if (panel == null) return null;
            
            var uiMap = panel.GetComponent<UI_ExpeditionMap>();
            if (uiMap != null)
            {
                return Safe.GetField<Camera>(uiMap, "m_mapCamera");
            }
            return null;
        }

        /// <summary>
        /// Gets the Bunker/Shelter icon on the expedition map.
        /// </summary>
        public static GameObject GetBunkerIcon()
        {
            var panel = GetExpeditionMapPanel();
            if (panel == null) return null;
            
            // Try common paths
            var t = panel.transform.Find("expeditionMap/MapImage/BunkerIcon") 
                 ?? panel.transform.Find("MapImage/BunkerIcon")
                 ?? panel.transform.Find("BunkerIcon");
            
            return t != null ? t.gameObject : null;
        }

        /// <summary>
        /// Gets the main HUD panel (bottom bar with characters).
        /// </summary>
        public static GameObject GetHUD() { return SceneUtil.Find("UI Root/Panels/HUD"); }

        /// <summary>
        /// Gets the radio dialog panel.
        /// </summary>
        public static GameObject GetRadioPanel() { return SceneUtil.Find("UI Root/Panels/RadioDialogPanel"); }

        /// <summary>
        /// Gets the current active panel in the UI stack.
        /// </summary>
        public static GameObject GetActivePanel()
        {
            try
            {
                var go = SceneUtil.Find("UI Root");
                var mgr = go != null ? go.GetComponent<UIPanelManager>() : null;
                if (mgr != null)
                {
                    return Safe.GetField<GameObject>(mgr, "m_currentPanel");
                }
            }
            catch { }
            return null;
        }
    }
}
