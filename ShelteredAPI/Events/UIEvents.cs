using System;
using ModAPI.Core;
using UnityEngine;


using ShelteredAPI.Saves;
namespace ShelteredAPI.Events
{
    /// <summary>
    /// UI panel lifecycle events. Provides panel open/close tracking
    /// without requiring individual Harmony patches.
    ///
    /// Usage:
    ///   UIEvents.OnPanelOpened += (panel) => {
    ///       if (panel.GetType().Name == "CraftingPanel") {
    ///           // React to crafting panel opening
    ///       }
    ///   };
    /// </summary>
    internal static class UIEvents
    {
        // Panel lifecycle events
        /// <summary>
        /// Fired when a panel is pushed onto the UI stack and shown.
        /// </summary>
        public static event Action<BasePanel> OnPanelOpened;

        /// <summary>
        /// Fired when a panel is popped from the UI stack and closed.
        /// </summary>
        public static event Action<BasePanel> OnPanelClosed;

        /// <summary>
        /// Fired when a panel is resumed (returns to top of stack).
        /// </summary>
        public static event Action<BasePanel> OnPanelResumed;

        /// <summary>
        /// Fired when a panel is paused (another panel pushed above it).
        /// </summary>
        public static event Action<BasePanel> OnPanelPaused;

        /// <summary>
        /// Fired when any <see cref="UIButton"/> is clicked.
        /// </summary>
        /// <remarks>Subscribers must filter events by button name or panel.</remarks>
        public static event Action<GameObject, string> OnButtonClicked;

        // Internal event raisers (called by Harmony patches)

        internal static void RaisePanelOpened(BasePanel panel)
        {
            if (panel == null)
                return;

            SafeInvoke(() => OnPanelOpened?.Invoke(panel), nameof(OnPanelOpened), panel);
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            if (panel == null)
                return;

            SafeInvoke(() => OnPanelClosed?.Invoke(panel), nameof(OnPanelClosed), panel);
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            if (panel == null)
                return;

            SafeInvoke(() => OnPanelResumed?.Invoke(panel), nameof(OnPanelResumed), panel);
        }

        internal static void RaisePanelPaused(BasePanel panel)
        {
            if (panel == null)
                return;

            SafeInvoke(() => OnPanelPaused?.Invoke(panel), nameof(OnPanelPaused), panel);
        }

        internal static void RaiseButtonClicked(GameObject button, string buttonName)
        {
            if (button == null)
                return;

            SafeInvoke(() => OnButtonClicked?.Invoke(button, buttonName),
                      nameof(OnButtonClicked),
                      $"Button: {buttonName}");
        }

        private static void SafeInvoke(Action action, string eventName, object context = null)
        {
            if (action == null || ModRuntime.IsQuitting)
                return;

            try
            {
                action.Invoke();

                string contextStr = context != null ? $" (Context: {context})" : "";
                MMLog.WriteDebug($"[UIEvents.{eventName}] Event fired{contextStr}");
            }
            catch (Exception ex)
            {
                string contextStr = context != null ? $" for {context}" : "";
                MMLog.WarnOnce($"UIEvents.{eventName}.Error",
                    $"[UIEvents] {eventName} handler error{contextStr}: {ex.Message}");
            }
        }

        /// <summary>
        /// Compatibility stub for panel-stack inspection. Always returns <see langword="false"/>.
        /// </summary>
        /// <typeparam name="T">Panel type to check.</typeparam>
        /// <returns><see langword="false"/> because panel-stack inspection is not implemented.</returns>
        public static bool IsPanelOpen<T>() where T : BasePanel
        {
            if (UIPanelManager.instance == null)
                return false;

            try
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns subscription counts for UI event diagnostics.
        /// </summary>
        /// <returns>String with subscription counts</returns>
        public static string GetDiagnostics()
        {
            int openedCount = OnPanelOpened != null ? OnPanelOpened.GetInvocationList().Length : 0;
            int closedCount = OnPanelClosed != null ? OnPanelClosed.GetInvocationList().Length : 0;
            int resumedCount = OnPanelResumed != null ? OnPanelResumed.GetInvocationList().Length : 0;
            int pausedCount = OnPanelPaused != null ? OnPanelPaused.GetInvocationList().Length : 0;
            int buttonCount = OnButtonClicked != null ? OnButtonClicked.GetInvocationList().Length : 0;

            return $"UIEvents Subscriptions: Opened={openedCount}, Closed={closedCount}, " +
                   $"Resumed={resumedCount}, Paused={pausedCount}, ButtonClicked={buttonCount}";
        }
    }
}
