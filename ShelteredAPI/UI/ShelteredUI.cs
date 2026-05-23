using System;
using ShelteredAPI.UI.Internal;
using UnityEngine;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Stable mod-facing UI facade for focused NGUI extension operations, panel takeovers,
    /// and Sheltered keybinding UI.
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

        /// <summary>Clones an NGUI visual template with safe listener/button defaults.</summary>
        public static UICloneResult CloneElement(GameObject template, Transform parent)
        {
            return UIExtensionService.CloneElement(template, parent, null);
        }

        /// <summary>Clones an NGUI visual template using focused inherited-behavior options.</summary>
        public static UICloneResult CloneElement(GameObject template, Transform parent, UICloneOptions options)
        {
            return UIExtensionService.CloneElement(template, parent, options);
        }

        /// <summary>Clears inherited UIEventListener delegates from a UI root and its descendants.</summary>
        public static UIOperationResult StripInheritedEventListeners(GameObject root)
        {
            return UIExtensionService.StripInheritedEventListeners(root, true);
        }

        /// <summary>Clears inherited UIEventListener delegates from a UI root with explicit hierarchy scope.</summary>
        public static UIOperationResult StripInheritedEventListeners(GameObject root, bool includeChildren)
        {
            return UIExtensionService.StripInheritedEventListeners(root, includeChildren);
        }

        /// <summary>Binds a button click with explicit replacement or append semantics.</summary>
        public static UIOperationResult BindButtonClick(UIButton button, Action onClick, UIButtonBindingMode mode)
        {
            return UIExtensionService.BindButtonClick(button, onClick, mode);
        }

        /// <summary>Binds a button click to an item/context value captured for this specific button.</summary>
        public static UIOperationResult BindButtonClick<TContext>(
            UIButton button,
            TContext context,
            Action<TContext> onClick,
            UIButtonBindingMode mode)
        {
            if (onClick == null)
                return UIExtensionService.BindButtonClick(button, null, mode);

            return UIExtensionService.BindButtonClick(button, delegate { onClick(context); }, mode);
        }

        /// <summary>Captures widget, label, and color-tween visual state under a UI root.</summary>
        public static UIColorSnapshot SnapshotColors(GameObject root)
        {
            return UIExtensionService.SnapshotColors(root, true);
        }

        /// <summary>Captures widget, label, and color-tween visual state with explicit hierarchy scope.</summary>
        public static UIColorSnapshot SnapshotColors(GameObject root, bool includeChildren)
        {
            return UIExtensionService.SnapshotColors(root, includeChildren);
        }

        /// <summary>Restores an earlier color snapshot where captured components are still alive.</summary>
        public static UIOperationResult RestoreColors(UIColorSnapshot snapshot)
        {
            return UIExtensionService.RestoreColors(snapshot);
        }

        /// <summary>Subscribes typed open, close, and resume callbacks and returns a disposable registration.</summary>
        public static IDisposable SubscribePanelLifecycle<TPanel>(
            Action<TPanel> onOpened,
            Action<TPanel> onClosed,
            Action<TPanel> onResumed)
            where TPanel : BasePanel
        {
            return UIExtensionService.SubscribePanelLifecycle(onOpened, onClosed, onResumed);
        }

        public static void ShowShelteredKeybinds()
        {
            ShelteredKeybindsUIV2.Show();
        }
    }
}
