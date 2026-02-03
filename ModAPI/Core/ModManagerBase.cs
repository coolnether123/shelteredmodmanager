using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ModAPI.Core;

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
        protected ModSettings Settings { get { return Context != null ? Context.Settings : null; } }

        /// <summary>
        /// Called by the ModAPI when the plugin is initialized.
        /// </summary>
        public virtual void Initialize(IPluginContext context)
        {
            Context = context;
            
            // Auto-bind settings
            if (Settings != null)
            {
                Settings.AutoBind(this);

                // Also try to find a field named 'config' or 'Configuration' and bind that
                var configField = GetType().GetField("config", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                               ?? GetType().GetField("Configuration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (configField != null)
                {
                    var configObj = configField.GetValue(this);
                    if (configObj != null) Settings.AutoBind(configObj);
                }

                OnSettingsLoaded();
            }

            if (Log != null) Log.Info(string.Format("{0} initialized.", GetType().Name));
            
            ScanForPersistence();
        }

        protected virtual void ScanForPersistence()
        {
            if (Context == null || SaveSystem == null) return;
            
            try
            {
                var assembly = GetType().Assembly;
                // Only scan types in the same assembly as the plugin
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    
                    var attr = Attribute.GetCustomAttribute(type, typeof(Attributes.ModPersistentDataAttribute)) as Attributes.ModPersistentDataAttribute;
                    if (attr != null)
                    {
                        try
                        {
                            // 1. Instantiate
                            var instance = Activator.CreateInstance(type);

                            // 2. Register
                            SaveSystem.RegisterModData(attr.Key, instance);
                            if (Log != null) Log.Debug($"[Persistence] Registered auto-persistent type {type.Name} with key '{attr.Key}'");

                            // 3. Inject into Plugin
                            // Find a field in 'this' (the ModManagerBase plugin) that matches the type
                            var bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                            var fields = GetType().GetFields(bindFlags);
                            
                            foreach (var field in fields)
                            {
                                if (field.FieldType == type)
                                {
                                    field.SetValue(this, instance);
                                    if (Log != null) Log.Info($"[Persistence] Injected {type.Name} into {GetType().Name}.{field.Name}");
                                    break; // Only inject into the first matching field
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (Log != null) Log.Error($"[Persistence] Failed to process {type.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Log != null) Log.Error($"[Persistence] Scanner failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Hook for when settings are loaded or changed.
        /// </summary>
        public virtual void OnSettingsLoaded() { }

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
    }
}
