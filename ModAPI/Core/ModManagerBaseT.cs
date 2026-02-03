using System;
using System.Linq;
using ModAPI.Attributes;
using ModAPI.Events;

namespace ModAPI.Core
{
    public abstract class ModManagerBase<T> : ModManagerBase where T : class, new()
    {
        public T Config { get; private set; }

        public override void Initialize(IPluginContext ctx)
        {
            // 1. Legacy setup FIRST
            // (This handles Context setup, Logging, and ScanForPersistence)
            base.Initialize(ctx);

            // 2. Scanner Logic
            var assembly = GetType().Assembly;
            var duplicateConfigs = assembly.GetTypes()
                .Where(t => t.IsDefined(typeof(ModConfigurationAttribute), false))
                .ToList();

            if (duplicateConfigs.Count > 1)
            {
                // Fatal error as per spec
                throw new Exception("FatalInitializationException: Multiple [ModConfiguration] classes found in assembly " + assembly.FullName);
            }

            // 3. Instantiation
            Config = new T();

            // 4. Binding & Loading
            var provider = new AutoSettingsProvider(typeof(T), Config, ctx);
            provider.LoadInto(Config);

            // Link to ModEntry for Spine UI
            if (ctx.Mod != null)
            {
                ctx.Mod.SettingsProvider = provider;
            }

            // 5. Structural Validation
            (Config as IModSettingsValidator)?.Validate();

            // 6. Runtime Validation
            if (typeof(IModSettingsValidator).IsAssignableFrom(typeof(T)))
            {
                GameEvents.OnSessionStarted += () => 
                {
                    try {
                        (Config as IModSettingsValidator).ValidateRuntime();
                    } catch (Exception ex) {
                        if (Log != null) Log.Error("[ModConfiguration] Runtime validation failed: " + ex);
                    }
                };
            }
        }
    }
}
