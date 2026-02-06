using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq; // Added for .Any() extension method
using UnityEngine;
using ModAPI.Core;
using ModAPI.Spine;

namespace ModAPI.Core
{
    /// <summary>
    /// Recommended base class for complex mods. 
    /// Manages its own Lifecycle and provides pre-wired access to ModAPI systems.
    /// </summary>
    public abstract class ModManagerBase : MonoBehaviour
    {
        protected IPluginContext Context { get; private set; }
        public IModLogger Log { get { return Context != null ? Context.Log : null; } }
        protected ISaveSystem SaveSystem { get { return Context != null ? Context.SaveSystem : null; } }
        
        /// <summary>The object holding the configuration. Defaults to 'this' if auto-detected.</summary>
        public object Config { get; protected set; }

        /// <summary>
        /// Deterministic random instance scoped to this mod.
        /// Uses the master seed combined with the mod ID for isolation.
        /// </summary>
        protected ModRandomStream Random { get; private set; }

        /// <summary>
        /// Called by the ModAPI when the plugin is initialized.
        /// </summary>
        public virtual void Initialize(IPluginContext context)
        {
            Context = context;

            // Initialize deterministic random stream for this mod
            ModRandom.OnSeedChanged += RefreshRandomStream;
            RefreshRandomStream();
            
            // 1. Auto-Detect "Inline" Settings if no Config was manually set
            if (Config == null && HasModSettings(this))
            {
                var controller = new SettingsController(context, this);
                Config = this;
                
                // IMMEDIATE ASSIGNMENT (Prevents UI Null Checks)
                context.Mod.SettingsProvider = controller;
                
                // Load (Safe to happen now)
                controller.Load();

                // 2. SUBSCRIBE: Per-Save re-load catch
                ModAPI.Events.GameEvents.OnSessionStarted += delegate {
                    try {
                        SettingsController sc = context.Mod.SettingsProvider as SettingsController;
                        if (sc != null) sc.Load();
                    } catch (Exception ex) {
                        MMLog.WriteError("[ModManagerBase] Session-started settings re-load failed: " + ex.Message);
                    }
                };
            }

            if (Log != null) Log.Info(string.Format("{0} initialized. Settings Mode: {1}", GetType().Name, Config != null ? "Active" : "None"));
            
            ScanForPersistence();
        }

        private bool HasModSettings(object target)
        {
            if (target == null) return false;
            var type = target.GetType();
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Any(f => f.IsDefined(typeof(ModSettingAttribute), true)) ||
                   type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Any(p => p.IsDefined(typeof(ModSettingAttribute), true)) ||
                   type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Any(m => m.IsDefined(typeof(ModSettingAttribute), true));
        }

        protected void CreateSettings<T>() where T : class, new()
        {
            if (Context == null) throw new InvalidOperationException("CreateSettings must be called during or after Initialize.");
            
            var config = new T();
            Config = config;
            var controller = new SettingsController(Context, config);
            Context.Mod.SettingsProvider = controller;
            controller.Load();

            // SUBSCRIBE: Per-Save re-load catch for manual Config
            ModAPI.Events.GameEvents.OnSessionStarted += delegate {
                try {
                    SettingsController sc = Context.Mod.SettingsProvider as SettingsController;
                    if (sc != null) sc.Load();
                } catch (Exception ex) {
                    MMLog.WriteError("[ModManagerBase] Manual config session-started re-load failed: " + ex.Message);
                }
            };
        }

        protected virtual void ScanForPersistence()
        {
            if (Context == null || SaveSystem == null) return;
            
            try
            {
                var assembly = GetType().Assembly;
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    
                    // We also support scanning for any field with [ModSetting(Scope=PerSave)] etc, 
                    // but legacy persistence still uses ModPersistentDataAttribute (to be deprecated).
                    // For now keeping simpler scan logic.
                }
            }
            catch (Exception ex)
            {
                if (Log != null) Log.Error($"[Persistence] Scanner failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Convenience method to register data for persistence.
        /// </summary>
        protected void RegisterPersistentData<T>(string key, T data, Action<T> migrationCallback = null) where T : class
        {
            if (SaveSystem != null) SaveSystem.RegisterModData(key, data, migrationCallback);
        }

        /// <summary>
        /// Convenience method for starting coroutines without referencing Context directly.
        /// </summary>
        public new Coroutine StartCoroutine(IEnumerator routine)
        {
            return Context != null ? Context.StartCoroutine(routine) : base.StartCoroutine(routine);
        }

        public virtual void OnSettingsLoaded() { }

        protected virtual void RefreshRandomStream()
        {
            if (Context == null) return;
            
            // Each mod gets its own RNG stream derived from master seed.
            // Branching via Mod ID ensures Mod A and Mod B don't "steal" from each other.
            int modHash = Context.Mod != null ? Context.Mod.Id.GetHashCode() : GetType().FullName.GetHashCode();
            this.Random = new ModRandomStream(ModRandom.CurrentSeed ^ modHash);
            
            if (Log != null) Log.Debug(string.Format("Random stream refreshed. Seed Hash: {0}", modHash));
        }

        protected virtual void OnDestroy()
        {
            ModRandom.OnSeedChanged -= RefreshRandomStream;
        }
    }
}
