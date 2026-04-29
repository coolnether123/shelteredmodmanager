using System;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Stable mod-facing UI facade for panel takeovers and Sheltered keybinding UI.
    /// </summary>
    public static class ShelteredUI
    {
        public static UITakeoverSession For(BasePanel panel)
        {
            return UITakeover.For(panel);
        }

        public static UITakeoverSession For(UnityEngine.GameObject root)
        {
            return UITakeover.For(root);
        }

        public static UITakeoverSession For(UnityEngine.Transform root)
        {
            return UITakeover.For(root);
        }

        public static IDisposable RegisterPanelTakeover<TPanel>(string key, Action<TPanel, UITakeoverSession> apply)
            where TPanel : BasePanel
        {
            return UIPanelTakeover.Register(key, apply);
        }

        public static IDisposable RegisterPanelTakeover<TPanel>(
            string key,
            Action<TPanel, UITakeoverSession> apply,
            bool applyOnOpened,
            bool applyOnResumed)
            where TPanel : BasePanel
        {
            return UIPanelTakeover.Register(key, apply, applyOnOpened, applyOnResumed);
        }

        public static void UnregisterPanelTakeover(string key)
        {
            UIPanelTakeover.Unregister(key);
        }

        public static void ShowShelteredKeybinds()
        {
            ShelteredKeybindsUI.Show();
        }
    }
}
