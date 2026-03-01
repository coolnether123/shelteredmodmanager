using System;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Events
{
    /// <summary>
    /// UI panel lifecycle events. Provides panel open/close tracking
    /// without requiring individual Harmony patches.
    /// </summary>
    public static class UIEvents
    {
        public static event Action<BasePanel> OnPanelOpened;
        public static event Action<BasePanel> OnPanelClosed;
        public static event Action<BasePanel> OnPanelResumed;
        public static event Action<BasePanel> OnPanelPaused;
        public static event Action<GameObject, string> OnButtonClicked;

        internal static void RaisePanelOpened(BasePanel panel)
        {
            if (panel == null) return;
            SafeInvoke(() => OnPanelOpened?.Invoke(panel), nameof(OnPanelOpened), panel);
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            if (panel == null) return;
            SafeInvoke(() => OnPanelClosed?.Invoke(panel), nameof(OnPanelClosed), panel);
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            if (panel == null) return;
            SafeInvoke(() => OnPanelResumed?.Invoke(panel), nameof(OnPanelResumed), panel);
        }

        internal static void RaisePanelPaused(BasePanel panel)
        {
            if (panel == null) return;
            SafeInvoke(() => OnPanelPaused?.Invoke(panel), nameof(OnPanelPaused), panel);
        }

        internal static void RaiseButtonClicked(GameObject button, string buttonName)
        {
            if (button == null) return;
            SafeInvoke(() => OnButtonClicked?.Invoke(button, buttonName), nameof(OnButtonClicked), $"Button: {buttonName}");
        }

        private static void SafeInvoke(Action action, string eventName, object context = null)
        {
            // Note: PluginRunner reference should be safe if it's in ModAPI (Generic)
            try
            {
                action.Invoke();
                MMLog.WriteDebug($"[UIEvents.{eventName}] Event fired");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce($"UIEvents.{eventName}.Error", $"[UIEvents] {eventName} handler error: {ex.Message}");
            }
        }
    }
}
