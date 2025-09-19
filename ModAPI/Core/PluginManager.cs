using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

using static ModAPI.Core.LoadOrderResolver;

namespace ModAPI.Core
{
    public class PluginManager
    {
        private static PluginManager instance;

        private readonly List<IModPlugin> _plugins;
        private readonly List<IModUpdate> _updates;
        private readonly List<IModShutdown> _shutdown;
        private readonly List<IModSceneEvents> _sceneEvents;

        private GameObject _loaderRoot;
        private string _gameRoot;
        private string _modsRoot;

        public static List<ModEntry> LoadedMods { get; private set; }


        private PluginManager()
        {
            _plugins = new List<IModPlugin>();
            _updates = new List<IModUpdate>();
            _shutdown = new List<IModShutdown>();
            _sceneEvents = new List<IModSceneEvents>();
            LoadedMods = new List<ModEntry>();
        }

        public static PluginManager getInstance()
        {
            if (instance == null)
            {
                instance = new PluginManager();
            }
            return instance;
        }

        public IEnumerable<IModPlugin> GetPlugins()
        {
            return _plugins;
        }

        public void loadAssemblies(GameObject doorstepGameObject)
        {

            // 1. Initialize core loader components and Harmony
            InitializeLoader(doorstepGameObject);
            // 2. Discover and order mods based on loadorder.json and dependencies
            var orderedMods = DiscoverAndOrderMods();

            // 3. Attach inspector tools for debugging (if not already present)
            AttachInspectorTools();

            // 4. Load mod assemblies and initialize plugins
            LoadAndInitializePlugins(orderedMods);

            MMLog.Write($"[loader] Loaded {_plugins.Count} plugin(s). Updates={_updates.Count}, Shutdown={_shutdown.Count}, SceneEvents={_sceneEvents.Count}");
        }

        private void InitializeLoader(GameObject doorstepGameObject)
        {
            try { ModAPI.Harmony.HarmonyBootstrap.EnsurePatched(); }
            catch (Exception ex) { MMLog.Write("PluginManager: HarmonyBootstrap.EnsurePatched failed: " + ex.Message); }

            // Apply save protection patches
            SaveProtectionPatches.ApplyPatches(new HarmonyLib.Harmony("ShelteredModManager.SaveProtection"));

            _gameRoot = Directory.GetParent(Application.dataPath).FullName;
            _modsRoot = Path.Combine(_gameRoot, "mods");
            _loaderRoot = doorstepGameObject;

            var runner = _loaderRoot.GetComponent<PluginRunner>();
            if (runner == null) runner = _loaderRoot.AddComponent<PluginRunner>();
            runner.Manager = this;
        }

        private List<ModEntry> DiscoverAndOrderMods()
        {
            var processedLof = LoadOrderResolver.ReadLoadOrderFile(_modsRoot);
            var discovered = ModDiscovery.DiscoverAllMods();

            try
            {
                var lofPath = Path.Combine(_modsRoot, "loadorder.json");
                var hasLoadOrder = File.Exists(lofPath);
                if (hasLoadOrder)
                {
                    var allowed = new HashSet<string>(processedLof.Order ?? new string[0], StringComparer.OrdinalIgnoreCase);
                    discovered = discovered.Where(m => allowed.Contains(m.Id)).ToList();
                    if (processedLof.Mods != null && processedLof.Mods.Count > 0)
                    {
                        discovered = discovered.Where(m => {
                            ModStatusEntry st;
                            return !processedLof.Mods.TryGetValue(m.Id, out st) || st.enabled;
                        }).ToList();
                    }
                }
                else
                {
                    MMLog.Write("[loader] No loadorder.json found. Skipping all mods until one is created by the Manager.");
                    discovered = new List<ModEntry>();
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.DiscoverAndOrderMods.Filter", "Error filtering mods: " + ex.Message); }

            var resolution = LoadOrderResolver.Resolve(discovered, processedLof.Order ?? new string[0]);
            LoadedMods = resolution.Mods; // Update static property
            if (resolution.MissingHardDependencies != null && resolution.MissingHardDependencies.Count > 0)
            {
                foreach (var e in resolution.MissingHardDependencies)
                    MMLog.Write("[loader] dependency error: " + e);
            }
            return LoadedMods;
        }

        private void AttachInspectorTools()
        {
            try
            {
                if (_loaderRoot.GetComponent<ModAPI.Inspector.RuntimeInspector>() == null)
                    _loaderRoot.AddComponent<ModAPI.Inspector.RuntimeInspector>();
                if (_loaderRoot.GetComponent<ModAPI.Inspector.BoundsHighlighter>() == null)
                    _loaderRoot.AddComponent<ModAPI.Inspector.BoundsHighlighter>();
            }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.AttachInspectorTools", "Error attaching inspector: " + ex.Message); }
        }

        private void LoadAndInitializePlugins(List<ModEntry> orderedMods)
        {
            foreach (var entry in orderedMods)
            {
                List<Assembly> modAssemblies = null;
                try
                {
                    modAssemblies = ModDiscovery.LoadAssemblies(entry);
                }
                catch (Exception ex)
                {
                    MMLog.Write($"[loader] failed to load assemblies for '{entry.Id}': {ex.Message}");
                    continue;
                }

                foreach (var asm in modAssemblies)
                {
                    Type[] types = new Type[0];
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types.Where(x => x != null).ToArray(); }

                    foreach (var type in types)
                    {
                        if (type == null || type.IsAbstract || !type.IsClass) continue;
                        if (!typeof(IModPlugin).IsAssignableFrom(type)) continue;

                        MMLog.WriteDebug($"[loader] Found potential plugin: {type.FullName}");

                        try
                        {
                            var plugin = (IModPlugin)Activator.CreateInstance(type);
                            var pluginRoot = new GameObject($"Mod-{SafeModIdFor(type)}");
                            pluginRoot.transform.SetParent(_loaderRoot.transform, false);

                            var ctx = BuildContextFor(type, pluginRoot);
                            _plugins.Add(plugin);

                            var u = plugin as IModUpdate; if (u != null) _updates.Add(u);
                            var s = plugin as IModShutdown; if (s != null) _shutdown.Add(s);
                            var se = plugin as IModSceneEvents; if (se != null) _sceneEvents.Add(se);

                            MMLog.WriteDebug($"[loader] Initializing plugin: {type.FullName}");
                            plugin.Initialize(ctx);
                            MMLog.WriteDebug($"[loader] Starting plugin: {type.FullName}");
                            plugin.Start(ctx);
                            ctx.Log.Info("Started.");
                        }
                        catch (Exception ex)
                        {
                            MMLog.WriteError($"[loader] error starting plugin '{type.FullName}': {ex.Message}");
                        }
                    }
                }
            }
        }

        internal void EnqueueNextFrame(Action a)
        {
            var runner = _loaderRoot != null ? _loaderRoot.GetComponent<PluginRunner>() : null;
            if (runner != null)
            {
                MMLog.Write("Runner type: " + runner.GetType().FullName);
                runner.Enqueue(a);
            }
        }

        internal void OnUnityUpdate()
        {
            for (int i = 0; i < _updates.Count; i++)
            {
                try { _updates[i].Update(); }
                catch (Exception ex) { MMLog.Write($"[loader] Update() failed: {ex.Message}"); }
            }
        }

        internal void OnSceneLoaded(string name)
        {
            for (int i = 0; i < _sceneEvents.Count; i++)
            {
                try { _sceneEvents[i].OnSceneLoaded(name); }
                catch (Exception ex) { MMLog.Write($"[loader] OnSceneLoaded failed: {ex.Message}"); }
            }
        }

        internal void OnSceneUnloaded(string name)
        {
            for (int i = 0; i < _sceneEvents.Count; i++)
            {
                try { _sceneEvents[i].OnSceneUnloaded(name); }
                catch (Exception ex) { MMLog.Write($"[loader] OnSceneUnloaded failed: {ex.Message}"); }
            }
        }

        public void ShutdownAll()
        {
            for (int i = _shutdown.Count - 1; i >= 0; i--)
            {
                try { _shutdown[i].Shutdown(); }
                catch (Exception ex) { MMLog.Write($"[loader] Shutdown() failed: {ex.Message}"); }
            }
        }

        private string SafeModIdFor(Type type)
        {
            ModEntry entry;
            if (ModRegistry.TryGetModByAssembly(type.Assembly, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
                return entry.Id;
            return type.Namespace ?? type.Name;
        }

        private IPluginContext BuildContextFor(Type type, GameObject pluginRoot)
        {
            ModEntry entry = null;
            ModRegistry.TryGetModByAssembly(type.Assembly, out entry);

            string asmPath = SafeAssemblyPath(type.Assembly);
            string modRoot = entry != null ? entry.RootPath : ProbeModRootFromAssembly(asmPath);

            string modId = entry != null && !string.IsNullOrEmpty(entry.Id) ? entry.Id : (type.Namespace ?? type.Name);
            var log = new PrefixedLogger(modId);
            ModSettings settings = null;
            try { settings = ModSettings.ForAssembly(type.Assembly); } catch (Exception ex) { MMLog.WarnOnce("PluginManager.BuildContextFor", "Error creating settings: " + ex.Message); settings = null; }

            return new PluginContextImpl
            {
                LoaderRoot = _loaderRoot,
                PluginRoot = pluginRoot,
                Mod = entry,
                Settings = settings,
                Log = log,
                GameRoot = _gameRoot,
                ModsRoot = _modsRoot,
                Scheduler = (Action a) => EnqueueNextFrame(a)
            };
        }

        private static string SafeAssemblyPath(Assembly asm)
        {
            try { return asm != null ? asm.Location : null; }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.SafeAssemblyPath", "Error getting assembly path: " + ex.Message); return null; }
        }

        private static string ProbeModRootFromAssembly(string asmPath)
        {
            if (string.IsNullOrEmpty(asmPath)) return null;
            try
            {
                var dir = new DirectoryInfo(Path.GetDirectoryName(asmPath));
                for (var cursor = dir; cursor != null; cursor = cursor.Parent)
                {
                    var aboutDir = Path.Combine(cursor.FullName, "About");
                    if (Directory.Exists(aboutDir)) return cursor.FullName;
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.ProbeModRoot", "Error probing for mod root: " + ex.Message); }
            return null;
        }

        private static IEnumerable<string> SafeEnumerateAssemblies(ModEntry entry)
        {
            var list = new List<string>();
            try
            {
                var asmDir = Path.Combine(entry.RootPath, "Assemblies");
                if (Directory.Exists(asmDir))
                {
                    foreach (var dll in Directory.GetFiles(asmDir, "*.dll", SearchOption.AllDirectories))
                    {
                        list.Add(dll);
                    }
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.SafeEnumerateAssemblies", "Error enumerating assemblies: " + ex.Message); }
            return list;
        }

        private static Assembly SafeLoadAssembly(string path)
        {
            try { return Assembly.LoadFrom(path); } catch (Exception ex) { MMLog.WarnOnce("PluginManager.SafeLoadAssembly", "Error loading assembly: " + ex.Message); return null; }
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            try { return Type.GetType(typeName, throwOnError: false); }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.ResolveType", "Error resolving type: " + ex.Message); return null; }
        }
    }

    public class PluginRunner : MonoBehaviour
    {
        public static bool IsModernUnity { get; private set; }
        private readonly Queue<Action> _nextFrame = new Queue<Action>();
        public PluginManager Manager;
        private bool _useModernApi = false;
        private string _currentSceneName;

        public event Action<string> SceneLoaded;
        public event Action<string> SceneUnloaded;

        private object _sceneLoadedDelegate;
        private object _sceneUnloadedDelegate;

        public void Enqueue(Action action)
        {
            lock (_nextFrame)
            {
                _nextFrame.Enqueue(action);
            }
        }

        private void Awake()
        {
            try
            {
                var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                if (sceneManagerType == null) throw new Exception("Modern SceneManager not found.");

                var sceneLoadedEvent = sceneManagerType.GetEvent("sceneLoaded");
                if (sceneLoadedEvent == null) throw new Exception("sceneLoaded event not found.");

                _sceneLoadedDelegate = Delegate.CreateDelegate(sceneLoadedEvent.EventHandlerType, this, GetType().GetMethod("OnSceneLoadedModern", BindingFlags.NonPublic | BindingFlags.Instance));
                sceneLoadedEvent.GetAddMethod().Invoke(null, new object[] { _sceneLoadedDelegate });

                var sceneUnloadedEvent = sceneManagerType.GetEvent("sceneUnloaded");
                if (sceneUnloadedEvent != null)
                {
                    _sceneUnloadedDelegate = Delegate.CreateDelegate(sceneUnloadedEvent.EventHandlerType, this, GetType().GetMethod("OnSceneUnloadedModern", BindingFlags.NonPublic | BindingFlags.Instance));
                    sceneUnloadedEvent.GetAddMethod().Invoke(null, new object[] { _sceneUnloadedDelegate });
                }

                IsModernUnity = true;
                _useModernApi = true;
                MMLog.Write("[PluginRunner] Using modern SceneManager events (Unity 5.4+).");

                var activeScene = sceneManagerType.GetProperty("activeScene").GetValue(null, null);
                var isLoadedProp = activeScene.GetType().GetProperty("isLoaded");
                if ((bool)isLoadedProp.GetValue(activeScene, null))
                {
                    OnSceneLoadedModern(activeScene, null);
                }
            }
            catch (Exception)
            {
                IsModernUnity = false;
                MMLog.Write("[PluginRunner] Modern SceneManager not found. Using legacy OnLevelWasLoaded (Unity 5.3).");

                _currentSceneName = Application.loadedLevelName;
                if (Manager != null && !string.IsNullOrEmpty(_currentSceneName))
                {
                    Manager.OnSceneLoaded(_currentSceneName);
                    SceneLoaded?.Invoke(_currentSceneName);
                }
            }
        }

        private void OnDestroy()
        {
            if (_useModernApi && _sceneLoadedDelegate != null)
            {
                try
                {
                    var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                    var sceneLoadedEvent = sceneManagerType.GetEvent("sceneLoaded");
                    sceneLoadedEvent.GetRemoveMethod().Invoke(null, new object[] { _sceneLoadedDelegate });

                    if (_sceneUnloadedDelegate != null)
                    {
                        var sceneUnloadedEvent = sceneManagerType.GetEvent("sceneUnloaded");
                        sceneUnloadedEvent.GetRemoveMethod().Invoke(null, new object[] { _sceneUnloadedDelegate });
                    }
                }
                catch (Exception ex) { MMLog.WarnOnce("PluginRunner.OnDestroy", "Error unsubscribing from scene events: " + ex.Message); }
            }
        }

        private void OnSceneLoadedModern(object scene, object mode)
        {
            if (Manager != null)
            {
                var nameProp = scene.GetType().GetProperty("name");
                string sceneName = (string)nameProp.GetValue(scene, null);
                Manager.OnSceneLoaded(sceneName);
                SceneLoaded?.Invoke(sceneName);
            }
        }

        private void OnSceneUnloadedModern(object scene)
        {
            if (Manager != null)
            {
                var nameProp = scene.GetType().GetProperty("name");
                string sceneName = (string)nameProp.GetValue(scene, null);
                Manager.OnSceneUnloaded(sceneName);
                SceneUnloaded?.Invoke(sceneName);
            }
        }

        void OnLevelWasLoaded(int level)
        {
            if (!_useModernApi)
            {
                var newSceneName = Application.loadedLevelName;
                if (Manager != null && _currentSceneName != newSceneName)
                {
                    if (!string.IsNullOrEmpty(_currentSceneName))
                    {
                        Manager.OnSceneUnloaded(_currentSceneName);
                        SceneUnloaded?.Invoke(_currentSceneName);
                    }
                    Manager.OnSceneLoaded(newSceneName);
                    SceneLoaded?.Invoke(newSceneName);
                    _currentSceneName = newSceneName;
                }
            }
        }

        private void Update()
        {
            lock (_nextFrame)
            {
                while (_nextFrame.Count > 0)
                {
                    var a = _nextFrame.Dequeue();
                    try { a(); } catch (Exception ex) { MMLog.Write($"[loader] next-frame action failed: {ex.Message}"); }
                }
            }
            if (Manager != null) Manager.OnUnityUpdate();
        }
    }

    internal class PrefixedLogger : IModLogger
    {
        private readonly string _prefix;
        public PrefixedLogger(string modId) { _prefix = string.IsNullOrEmpty(modId) ? "mod" : modId; }

        public void Info(string message) { MMLog.Write($"[{_prefix}] {message}"); }
        public void Warn(string message) { MMLog.Write($"[{_prefix}] WARN: {message}"); }
        public void Error(string message) { MMLog.Write($"[{_prefix}] ERROR: {message}"); }
    }

    internal class PluginContextImpl : IPluginContext
    {
        public GameObject LoaderRoot { get; set; }
        public GameObject PluginRoot { get; set; }
        public ModEntry Mod { get; set; }
        public ModSettings Settings { get; set; }
        public IModLogger Log { get; set; }
        public string GameRoot { get; set; }
        public string ModsRoot { get; set; }
        public bool IsModernUnity { get { return PluginRunner.IsModernUnity; } }

        public Action<Action> Scheduler;

        public void RunNextFrame(Action action) { Scheduler?.Invoke(action); }
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            return LoaderRoot != null ? LoaderRoot.GetComponent<PluginRunner>().StartCoroutine(routine) : null;
        }

        public GameObject FindPanel(string nameOrPath)
        {
            try { return ModAPI.SceneUtil.Find(nameOrPath); }
            catch (Exception ex) { MMLog.WarnOnce("PluginContextImpl.FindPanel", "Error finding panel: " + ex.Message); return null; }
        }

        public T AddComponentToPanel<T>(string nameOrPath) where T : Component
        {
            var go = FindPanel(nameOrPath);
            if (go == null)
            {
                Log?.Warn("FindPanel failed for '" + nameOrPath + "'");
                return null;
            }
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            try { return go.AddComponent<T>(); }
            catch (Exception ex)
            {
                Log?.Error("AddComponentToPanel<" + typeof(T).Name + "> failed: " + ex.Message);
                return null;
            }
        }
    }
}