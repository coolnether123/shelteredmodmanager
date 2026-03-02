using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Events;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Registers reusable panel takeover handlers that run on panel open/resume.
    /// </summary>
    public static class UIPanelTakeover
    {
        private sealed class Registration : IDisposable
        {
            private readonly string _key;
            private readonly bool _applyOnOpened;
            private readonly bool _applyOnResumed;
            private readonly Action<BasePanel> _apply;
            private bool _disposed;

            public Registration(string key, bool applyOnOpened, bool applyOnResumed, Action<BasePanel> apply)
            {
                _key = key;
                _applyOnOpened = applyOnOpened;
                _applyOnResumed = applyOnResumed;
                _apply = apply;
            }

            public void Bind()
            {
                UIEvents.OnPanelOpened += HandleOpened;
                UIEvents.OnPanelResumed += HandleResumed;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                UIEvents.OnPanelOpened -= HandleOpened;
                UIEvents.OnPanelResumed -= HandleResumed;
            }

            private void HandleOpened(BasePanel panel)
            {
                if (_disposed || !_applyOnOpened) return;
                SafeApply(panel);
            }

            private void HandleResumed(BasePanel panel)
            {
                if (_disposed || !_applyOnResumed) return;
                SafeApply(panel);
            }

            private void SafeApply(BasePanel panel)
            {
                try
                {
                    if (_apply != null) _apply(panel);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("[UIPanelTakeover] Apply failed for '" + _key + "': " + ex.Message);
                }
            }
        }

        private static readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>();
        private static readonly object Sync = new object();

        public static IDisposable Register<TPanel>(string key, Action<TPanel, UITakeoverSession> apply)
            where TPanel : BasePanel
        {
            return Register(key, apply, true, true);
        }

        public static IDisposable Register<TPanel>(
            string key,
            Action<TPanel, UITakeoverSession> apply,
            bool applyOnOpened,
            bool applyOnResumed)
            where TPanel : BasePanel
        {
            if (apply == null) throw new ArgumentNullException("apply");

            string resolvedKey = string.IsNullOrEmpty(key)
                ? typeof(TPanel).FullName + ".DefaultTakeover"
                : key;

            Registration registration = new Registration(
                resolvedKey,
                applyOnOpened,
                applyOnResumed,
                delegate(BasePanel panel)
                {
                    TPanel typed = panel as TPanel;
                    if (typed == null) return;
                    UITakeoverSession session = UITakeover.For(typed);
                    apply(typed, session);
                });

            lock (Sync)
            {
                Registration old;
                if (Registrations.TryGetValue(resolvedKey, out old) && old != null)
                {
                    old.Dispose();
                }

                Registrations[resolvedKey] = registration;
            }

            registration.Bind();
            return registration;
        }

        public static void Unregister(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            lock (Sync)
            {
                Registration old;
                if (!Registrations.TryGetValue(key, out old)) return;
                Registrations.Remove(key);
                if (old != null) old.Dispose();
            }
        }
    }
}
