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

            LegacySettingsProviderAdapter adapter = null;
            if (!(provider is ISettingsProvider2))
            {
                adapter = new LegacySettingsProviderAdapter(context, provider);
                provider = adapter;
            }

            if (entry != null)
                entry.SettingsProvider = provider;

            SetContextSettings(context, provider);
            if (adapter != null) adapter.Load();
        }

        public static void SetContextSettings(IPluginContext context, ISettingsProvider provider)
        {
            PluginContextImpl impl = context as PluginContextImpl;
            if (impl != null)
                impl.Settings = provider;
        }
    }
    internal sealed class LegacySettingsProviderAdapter : ISettingsProvider2
    {
        private readonly ISettingsProvider _provider;
        private readonly SettingsController _controller;

        internal LegacySettingsProviderAdapter(IPluginContext context, ISettingsProvider provider)
        {
            _provider = provider;
            _controller = new SettingsController(context, provider.GetSettingsObject(), provider, provider.GetSettings());
        }

        internal void Load() { _controller.Load(); }
        public System.Collections.Generic.IEnumerable<SettingDefinition> GetSettings() { return _provider.GetSettings(); }
        public object GetSettingsObject() { return _provider.GetSettingsObject(); }
        public void OnSettingsLoaded() { _provider.OnSettingsLoaded(); }
        public void ResetToDefaults() { _provider.ResetToDefaults(); }
        public void Save() { _controller.Save(); }
        public bool IsReady { get { return _controller.IsReady; } }
        public string SerializeToJson() { return _controller.SerializeToJson(); }
    }

}
