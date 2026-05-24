using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Events;
using UnityEngine;

namespace ShelteredAPI.UI.Internal
{
    /// <summary>
    /// Owns direct NGUI operations exposed through the focused ShelteredUI facade.
    /// </summary>
    internal static class UIExtensionService
    {
        private sealed class WidgetColorRecord
        {
            public UIWidget Widget;
            public Color Color;
        }

        private sealed class TweenColorRecord
        {
            public TweenColor Tween;
            public Color From;
            public Color To;
            public Color Value;
        }

        private sealed class ColorSnapshotState
        {
            public readonly List<WidgetColorRecord> Widgets = new List<WidgetColorRecord>();
            public readonly List<TweenColorRecord> Tweens = new List<TweenColorRecord>();
        }

        private sealed class PanelLifecycleSubscription<TPanel> : IDisposable where TPanel : BasePanel
        {
            private readonly Action<TPanel> _onOpened;
            private readonly Action<TPanel> _onClosed;
            private readonly Action<TPanel> _onResumed;
            private bool _disposed;

            public PanelLifecycleSubscription(Action<TPanel> onOpened, Action<TPanel> onClosed, Action<TPanel> onResumed)
            {
                _onOpened = onOpened;
                _onClosed = onClosed;
                _onResumed = onResumed;
                UIEvents.OnPanelOpened += OnOpened;
                UIEvents.OnPanelClosed += OnClosed;
                UIEvents.OnPanelResumed += OnResumed;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                UIEvents.OnPanelOpened -= OnOpened;
                UIEvents.OnPanelClosed -= OnClosed;
                UIEvents.OnPanelResumed -= OnResumed;
            }

            private void OnOpened(BasePanel panel)
            {
                Invoke(_onOpened, panel, "opened");
            }

            private void OnClosed(BasePanel panel)
            {
                Invoke(_onClosed, panel, "closed");
            }

            private void OnResumed(BasePanel panel)
            {
                Invoke(_onResumed, panel, "resumed");
            }

            private void Invoke(Action<TPanel> callback, BasePanel panel, string lifecycleName)
            {
                if (_disposed || callback == null) return;
                TPanel typed = panel as TPanel;
                if (typed == null) return;

                try
                {
                    callback(typed);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("[ShelteredUI] Panel " + lifecycleName + " callback failed: " + ex.Message);
                }
            }
        }

        internal static UICloneResult CloneElement(GameObject template, Transform parent, UICloneOptions options)
        {
            List<string> warnings = new List<string>();
            if (template == null)
            {
                warnings.Add("A clone template is required.");
                return new UICloneResult(null, 0, warnings);
            }

            if (options == null)
                options = new UICloneOptions();

            Transform targetParent = parent != null ? parent : template.transform.parent;
            if (targetParent == null)
                warnings.Add("The cloned object has no parent; UI layer alignment could not be inherited.");

            bool active = template.activeSelf;
            Vector3 localPosition = template.transform.localPosition;
            Quaternion localRotation = template.transform.localRotation;
            Vector3 localScale = template.transform.localScale;
            GameObject clone;
            try
            {
                if (active)
                    template.SetActive(false);
                clone = UnityEngine.Object.Instantiate(template) as GameObject;
            }
            catch (Exception ex)
            {
                warnings.Add("Clone failed: " + ex.Message);
                return new UICloneResult(null, 0, warnings);
            }
            finally
            {
                if (active && template != null)
                    template.SetActive(true);
            }

            if (clone == null)
            {
                warnings.Add("Clone failed because Unity did not return a GameObject.");
                return new UICloneResult(null, 0, warnings);
            }

            clone.SetActive(false);
            clone.name = string.IsNullOrEmpty(options.CloneName) ? template.name + "_Clone" : options.CloneName;

            if (targetParent != null)
            {
                clone.transform.SetParent(targetParent, false);
                NGUITools.SetLayer(clone, targetParent.gameObject.layer);
            }

            clone.transform.localPosition = localPosition;
            clone.transform.localRotation = localRotation;
            clone.transform.localScale = localScale;

            int affectedCount = 0;
            if (options.StripInheritedEventListeners)
                affectedCount += StripEventListeners(clone, options.IncludeChildren, warnings);
            if (options.ClearButtonClickHandlers)
                affectedCount += ClearButtonHandlers(clone, options.IncludeChildren, warnings);

            clone.SetActive(active);
            return new UICloneResult(clone, affectedCount, warnings);
        }

        internal static UIOperationResult StripInheritedEventListeners(GameObject root, bool includeChildren)
        {
            List<string> warnings = new List<string>();
            if (root == null)
            {
                warnings.Add("A UI root is required to strip inherited event listeners.");
                return new UIOperationResult(false, 0, warnings);
            }

            int cleared = StripEventListeners(root, includeChildren, warnings);
            return new UIOperationResult(true, cleared, warnings);
        }

        internal static UIOperationResult BindButtonClick(UIButton button, Action onClick, UIButtonBindingMode mode)
        {
            List<string> warnings = new List<string>();
            if (button == null)
            {
                warnings.Add("A UIButton is required for click binding.");
                return new UIOperationResult(false, 0, warnings);
            }
            if (onClick == null)
            {
                warnings.Add("A click callback is required.");
                return new UIOperationResult(false, 0, warnings);
            }

            if (button.onClick == null)
            {
                button.onClick = new List<EventDelegate>();
                warnings.Add("The button had no onClick list; an empty list was created before binding.");
            }

            EventDelegate.Callback callback = delegate { onClick(); };
            if (mode == UIButtonBindingMode.Replace)
                EventDelegate.Set(button.onClick, callback);
            else
                EventDelegate.Add(button.onClick, callback);

            return new UIOperationResult(true, 1, warnings);
        }

        internal static UIColorSnapshot SnapshotColors(GameObject root, bool includeChildren)
        {
            List<string> warnings = new List<string>();
            if (root == null)
            {
                warnings.Add("A UI root is required to snapshot colors.");
                return new UIColorSnapshot(null, 0, 0, 0, warnings);
            }

            ColorSnapshotState state = new ColorSnapshotState();
            UIWidget[] widgets = includeChildren
                ? root.GetComponentsInChildren<UIWidget>(true)
                : root.GetComponents<UIWidget>();
            int labelCount = 0;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null) continue;
                if (widget is UILabel) labelCount++;
                state.Widgets.Add(new WidgetColorRecord { Widget = widget, Color = widget.color });
            }

            TweenColor[] tweens = includeChildren
                ? root.GetComponentsInChildren<TweenColor>(true)
                : root.GetComponents<TweenColor>();
            for (int i = 0; i < tweens.Length; i++)
            {
                TweenColor tween = tweens[i];
                if (tween == null) continue;
                try
                {
                    state.Tweens.Add(new TweenColorRecord
                    {
                        Tween = tween,
                        From = tween.from,
                        To = tween.to,
                        Value = tween.value
                    });
                }
                catch (Exception ex)
                {
                    warnings.Add("A TweenColor state could not be captured: " + ex.Message);
                }
            }

            if (state.Widgets.Count == 0 && state.Tweens.Count == 0)
                warnings.Add("No UIWidget or TweenColor components were found under the requested root.");

            return new UIColorSnapshot(state, labelCount, state.Widgets.Count, state.Tweens.Count, warnings);
        }

        internal static UIOperationResult RestoreColors(UIColorSnapshot snapshot)
        {
            List<string> warnings = new List<string>();
            if (snapshot == null || snapshot.State == null)
            {
                warnings.Add("A valid color snapshot is required for restore.");
                return new UIOperationResult(false, 0, warnings);
            }

            ColorSnapshotState state = snapshot.State as ColorSnapshotState;
            if (state == null)
            {
                warnings.Add("The color snapshot state is not recognized by this ShelteredAPI version.");
                return new UIOperationResult(false, 0, warnings);
            }

            int restored = 0;
            for (int i = 0; i < state.Tweens.Count; i++)
            {
                TweenColorRecord record = state.Tweens[i];
                if (record.Tween == null)
                {
                    warnings.Add("A captured TweenColor no longer exists and was skipped.");
                    continue;
                }

                try
                {
                    record.Tween.from = record.From;
                    record.Tween.to = record.To;
                    record.Tween.value = record.Value;
                    restored++;
                }
                catch (Exception ex)
                {
                    warnings.Add("A TweenColor state could not be restored: " + ex.Message);
                }
            }

            for (int i = 0; i < state.Widgets.Count; i++)
            {
                WidgetColorRecord record = state.Widgets[i];
                if (record.Widget == null)
                {
                    warnings.Add("A captured UIWidget no longer exists and was skipped.");
                    continue;
                }

                record.Widget.color = record.Color;
                restored++;
            }

            return new UIOperationResult(true, restored, warnings);
        }

        internal static IDisposable SubscribePanelLifecycle<TPanel>(
            Action<TPanel> onOpened,
            Action<TPanel> onClosed,
            Action<TPanel> onResumed)
            where TPanel : BasePanel
        {
            if (onOpened == null && onClosed == null && onResumed == null)
                throw new ArgumentException("At least one panel lifecycle callback is required.");

            return new PanelLifecycleSubscription<TPanel>(onOpened, onClosed, onResumed);
        }

        private static int StripEventListeners(GameObject root, bool includeChildren, IList<string> warnings)
        {
            UIEventListener[] listeners = includeChildren
                ? root.GetComponentsInChildren<UIEventListener>(true)
                : root.GetComponents<UIEventListener>();
            int cleared = 0;

            for (int i = 0; i < listeners.Length; i++)
            {
                UIEventListener listener = listeners[i];
                if (listener == null) continue;
                try
                {
                    listener.onSubmit = null;
                    listener.onClick = null;
                    listener.onDoubleClick = null;
                    listener.onHover = null;
                    listener.onPress = null;
                    listener.onSelect = null;
                    listener.onScroll = null;
                    listener.onDrag = null;
                    listener.onDrop = null;
                    listener.onKey = null;
                    cleared++;
                }
                catch (Exception ex)
                {
                    warnings.Add("A UIEventListener could not be cleared: " + ex.Message);
                }
            }

            return cleared;
        }

        private static int ClearButtonHandlers(GameObject root, bool includeChildren, IList<string> warnings)
        {
            UIButton[] buttons = includeChildren
                ? root.GetComponentsInChildren<UIButton>(true)
                : root.GetComponents<UIButton>();
            int cleared = 0;

            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                if (button == null) continue;
                try
                {
                    if (button.onClick != null)
                        button.onClick.Clear();
                    cleared++;
                }
                catch (Exception ex)
                {
                    warnings.Add("A UIButton onClick list could not be cleared: " + ex.Message);
                }
            }

            return cleared;
        }
    }
}
