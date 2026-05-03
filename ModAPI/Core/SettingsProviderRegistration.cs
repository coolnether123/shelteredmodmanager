using ModAPI.Spine;

namespace ModAPI.Core
{
    /// <summary>
    /// Keeps ModEntry and IPluginContext settings provider references in sync.
    /// </summary>
    internal static class SettingsProviderRegistration
    {
        public static void Register(ModEntry entry, IPluginContext context, ISettingsProvider provider)
        {
            if (provider == null)
                return;

            if (entry != null)
                entry.SettingsProvider = provider;

            SetContextSettings(context, provider);
        }

        public static void SetContextSettings(IPluginContext context, ISettingsProvider provider)
        {
            PluginContextImpl impl = context as PluginContextImpl;
            if (impl != null)
                impl.Settings = provider;
        }
    }
}
