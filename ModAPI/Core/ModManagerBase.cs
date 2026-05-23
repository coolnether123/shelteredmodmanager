using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Harmony;
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
        public IModLogger Log
        {
            get
            {
                if (_scopedLog != null) return _scopedLog;
                if (Context != null && Context.Log != null)
                {
                    if (Context.Log is PrefixedLogger) return Context.Log;

                    string scope = (Context.Mod != null && !string.IsNullOrEmpty(Context.Mod.Id))
                        ? Context.Mod.Id
                        : GetType().Name;
                    _scopedLog = Context.Log.WithScope(scope);
                    return _scopedLog ?? Context.Log;
                }
                return null;
            }
        }
        protected ISaveSystem SaveSystem { get { return Context != null ? Context.SaveSystem : null; } }
        protected EventRegistry Events { get; private set; }

        /// <summary>The object holding the configuration. Defaults to 'this' if auto-detected.</summary>
        public object Config { get; protected set; }

        /// <summary>
        /// Deterministic random instance scoped to this mod.
        /// Uses the canonical ModRandom named-stream registry for isolation and save restoration.
        /// </summary>
        protected ModRandomStream Random { get; private set; }
        private HarmonyLib.Harmony _harmonyInstance;
        private IModLogger _scopedLog;

        /// <summary>
        /// Optional Unity lifecycle hook for derived manager types.
        /// </summary>
        protected virtual void Awake() { }

        /// <summary>
        /// Called by the ModAPI when the plugin is initialized.
        /// </summary>
        public virtual void Initialize(IPluginContext context)
        {
            Context = context;
            _scopedLog = null;

            if (Events != null)
            {
                try { Events.Dispose(); } catch { }
            }
            Events = new EventRegistry();

            // Initialize deterministic random stream for this mod
            ModRandom.OnSeedChanged += RefreshRandomStream;
            RefreshRandomStream();
            
            // 1. Auto-Detect "Inline" Settings if no Config was manually set
            if (Config == null && SpineSettingsDiscovery.HasSettings(this))
            {
                var controller = new SettingsController(context, this);
                Config = this;

                SettingsProviderLifecycle.RegisterFrameworkOwnedController(
                    context.Mod,
                    context,
                    controller,
                    Events,
                    "inline Spine settings",
                    "[ModManagerBase]");
            }

            if (Log != null) Log.Info(string.Format("{0} initialized. Settings Mode: {1}", GetType().Name, Config != null ? "Active" : "None"));

            SettingsProviderLifecycle.BindProviderSaveOnBeforeSave(context, Events, "[ModManagerBase]");
            
            ScanForPersistence();
        }

        protected void CreateSettings<T>() where T : class, new()
        {
            if (Context == null) throw new InvalidOperationException("CreateSettings must be called during or after Initialize.");
            
            var config = new T();
            Config = config;
            var controller = new SettingsController(Context, config);
            SettingsProviderLifecycle.RegisterFrameworkOwnedController(
                Context.Mod,
                Context,
                controller,
                Events,
                "manual Spine settings",
                "[ModManagerBase]");
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

        /// <summary>
        /// Runs work on a background thread and forwards the result to Unity's main thread.
        /// </summary>
        /// <typeparam name="TResult">Result type produced by the background calculation.</typeparam>
        /// <param name="work">Background calculation. Do not access Unity objects here.</param>
        /// <param name="onMainThread">Main-thread callback for applying results to gameplay/UI.</param>
        /// <param name="onError">Main-thread callback for background exceptions. Optional.</param>
        protected void RunInBackground<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError = null)
        {
            ModThreads.RunAsync(work, onMainThread, onError);
        }

        public virtual void OnSettingsLoaded() { }

        /// <summary>
        /// Applies all Harmony patches found in this mod assembly and tracks them for automatic cleanup.
        /// </summary>
        protected void ApplyPatches(HarmonyUtil.PatchOptions options = null)
        {
            if (_harmonyInstance != null) return;

            string harmonyId = (Context != null && Context.Mod != null && !string.IsNullOrEmpty(Context.Mod.Id))
                ? Context.Mod.Id
                : GetType().FullName;

            _harmonyInstance = new HarmonyLib.Harmony(harmonyId);
            var patchOptions = options ?? CreateDefaultPatchOptions();
            var registryOptions = new PatchRegistryOptions();
            registryOptions.PatchOptions = patchOptions;
            registryOptions.SourceName = GetType().Assembly.GetName().Name;
            PatchRegistry.ApplyAssembly(_harmonyInstance, GetType().Assembly, registryOptions);
        }

        private HarmonyUtil.PatchOptions CreateDefaultPatchOptions()
        {
            var opts = new HarmonyUtil.PatchOptions();

            if (Context != null && Context.Settings != null)
            {
                opts.AllowDebugPatches = Context.Settings.GetBool("enableDebugPatches", false);
                opts.AllowDangerousPatches = Context.Settings.GetBool("dangerousPatches", false);
                opts.AllowStructReturns = Context.Settings.GetBool("allowStructReturns", false);
            }

            opts.OnResult = delegate(object member, string reason)
            {
                try
                {
                    if (Log != null && Log.IsDebugEnabled)
                    {
                        var who = member != null ? member.ToString() : "<null>";
                        Log.Debug("Patch Result: " + who + " -> " + reason);
                    }
                }
                catch { }
            };

            return opts;
        }

        protected virtual void RefreshRandomStream()
        {
            if (Context == null) return;

            string modId = (Context.Mod != null && !string.IsNullOrEmpty(Context.Mod.Id))
                ? Context.Mod.Id
                : (GetType().FullName ?? GetType().Name);
            string featureId = "manager:" + (GetType().FullName ?? GetType().Name);
            this.Random = ModRandom.GetStream(modId, featureId);

            if (Log != null) Log.Debug(string.Format("Random stream refreshed. StreamId: {0}/{1}", modId, featureId));
        }

        protected virtual void OnDestroy()
        {
            ModRandom.OnSeedChanged -= RefreshRandomStream;

            if (_harmonyInstance != null)
            {
                try { _harmonyInstance.UnpatchAll(_harmonyInstance.Id); }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("ModManagerBase.UnpatchAll." + GetType().FullName, "Harmony cleanup failed: " + ex.Message);
                }
                _harmonyInstance = null;
            }

            if (Events != null)
            {
                try { Events.Dispose(); }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("ModManagerBase.Events.Dispose." + GetType().FullName, "Event cleanup failed: " + ex.Message);
                }
                Events = null;
            }

            _scopedLog = null;
        }
    }

    /// <summary>
    /// Utility for declarative event subscription that guarantees cleanup.
    /// </summary>
    public class EventRegistry : IDisposable
    {
        private readonly List<Action> _unsubscribers = new List<Action>();
        private bool _disposed;

        public void Bind(Action subscribe, Action unsubscribe)
        {
            if (_disposed) throw new ObjectDisposedException("EventRegistry");
            if (subscribe == null) throw new ArgumentNullException("subscribe");
            if (unsubscribe == null) throw new ArgumentNullException("unsubscribe");

            subscribe();
            _unsubscribers.Add(unsubscribe);
        }

        public void Dispose()
        {
            if (_disposed) return;

            for (int i = _unsubscribers.Count - 1; i >= 0; i--)
            {
                try { _unsubscribers[i](); }
                catch (Exception ex) { MMLog.WriteDebug("Event unbind error: " + ex.Message); }
            }

            _unsubscribers.Clear();
            _disposed = true;
        }
    }
}
