using System;
using ModAPI.Spine;

namespace ModAPI.Core
{
    /// <summary>
    /// Wires framework-owned settings providers into game/session lifecycle events.
    /// Registration, persistence, and lifecycle subscription stay separate so every
    /// settings discovery path gets the same behavior without duplicating handlers.
    /// </summary>
    internal static class SettingsProviderLifecycle
    {
        public static void RegisterFrameworkOwnedController(
            ModEntry entry,
            IPluginContext context,
            SettingsController controller,
            EventRegistry events,
            string source,
            string logPrefix)
        {
            if (controller == null)
                return;

            SettingsProviderRegistration.Register(entry, context, controller);
            MMLog.WriteDebug("[Settings] Registered settings provider for " + ResolveModId(entry, context) + " via " + source + ".");

            LoadController(controller, logPrefix);
            BindControllerReloadOnSessionStart(context, events, logPrefix);
        }

        public static void BindProviderSaveOnBeforeSave(IPluginContext context, EventRegistry events, string logPrefix)
        {
            if (context == null || events == null)
                return;

            Action<object> beforeSaveHandler = delegate(object _)
            {
                try
                {
                    ISettingsProvider2 provider = ResolveProvider(context) as ISettingsProvider2;
                    if (provider != null)
                        provider.Save();
                }
                catch (Exception ex)
                {
                    MMLog.WriteError(logPrefix + " pre-save settings flush failed: " + ex.Message);
                }
            };

            events.Bind(
                delegate { GameLifecycleSources.AddBeforeSave(beforeSaveHandler); },
                delegate { GameLifecycleSources.RemoveBeforeSave(beforeSaveHandler); });
        }

        private static void BindControllerReloadOnSessionStart(
            IPluginContext context,
            EventRegistry events,
            string logPrefix)
        {
            if (events == null)
                return;

            Action sessionStartedHandler = delegate
            {
                try
                {
                    SettingsController activeController = ResolveProvider(context) as SettingsController;
                    if (activeController != null)
                        activeController.Load();
                }
                catch (Exception ex)
                {
                    MMLog.WriteError(logPrefix + " session-started settings reload failed: " + ex.Message);
                }
            };

            events.Bind(
                delegate { PluginManager.OnSessionStartedEvent += sessionStartedHandler; },
                delegate { PluginManager.OnSessionStartedEvent -= sessionStartedHandler; });
        }

        private static void LoadController(SettingsController controller, string logPrefix)
        {
            try
            {
                controller.Load();
            }
            catch (Exception ex)
            {
                MMLog.WriteError(logPrefix + " settings load failed: " + ex.Message);
            }
        }

        private static ISettingsProvider ResolveProvider(IPluginContext context)
        {
            if (context == null)
                return null;

            if (context.Mod != null && context.Mod.SettingsProvider != null)
                return context.Mod.SettingsProvider;

            return context.Settings;
        }

        private static string ResolveModId(ModEntry entry, IPluginContext context)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Id))
                return entry.Id;

            if (context != null && context.Mod != null && !string.IsNullOrEmpty(context.Mod.Id))
                return context.Mod.Id;

            return "<unknown>";
        }
    }
}
