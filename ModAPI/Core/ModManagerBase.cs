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
        protected IModLogger Log { get { return Context != null ? Context.Log : null; } }
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
        }

        /// <summary>
        /// Hook for when settings are loaded or changed.
        /// </summary>
        protected virtual void OnSettingsLoaded() { }

        /// <summary>
        /// Convenience method to register data for persistence.
        /// </summary>
        protected void RegisterPersistentData<T>(string key, T data) where T : class
        {
            if (SaveSystem != null) SaveSystem.RegisterModData(key, data);
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
