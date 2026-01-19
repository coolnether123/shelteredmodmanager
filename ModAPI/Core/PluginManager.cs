using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using HarmonyLib;
using ModAPI.Harmony;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Discovers mods, loads assemblies, and wires plugin lifecycle callbacks.
    /// </summary>
    public class PluginManager
    {
        private static PluginManager instance;

        private readonly List<IModPlugin> _plugins;
        private readonly List<IModUpdate> _updates;
        private readonly List<IModShutdown> _shutdown;
        private readonly List<IModSceneEvents> _sceneEvents;
        private int _loadErrors;

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
            var stopwatch = Stopwatch.StartNew();
            _loadErrors = 0;

            InitializeLoader(doorstepGameObject);
            LogAssemblyResolution();
            LogSceneApiDetection();

            var orderedModIds = ReadLoadOrderFromFile(_modsRoot);
            DiscoverAndOrderMods(orderedModIds);

            AttachInspectorTools();
            LoadAndInitializePlugins(LoadedMods);

            MMLog.Write($"[loader] Loaded {_plugins.Count} plugin(s). Updates={_updates.Count}, Shutdown={_shutdown.Count}, SceneEvents={_sceneEvents.Count}");

            stopwatch.Stop();
            var ms = stopwatch.ElapsedMilliseconds;
            MMLog.Write($"[Loader] Startup complete in {ms}ms. Loaded {_plugins.Count} plugin(s), {_loadErrors} error(s).");
        }

        /// <summary>
        /// Creates the loader GameObject, attaches the runner, and applies core patches.
        /// </summary>
        private void InitializeLoader(GameObject doorstepGameObject)
        {
            _gameRoot = Directory.GetParent(Application.dataPath).FullName;
            _modsRoot = Path.Combine(_gameRoot, "mods");

            _loaderRoot = doorstepGameObject != null ? doorstepGameObject : new GameObject("ModAPI.Loader");
            UnityEngine.Object.DontDestroyOnLoad(_loaderRoot);

            var runner = _loaderRoot.GetComponent<PluginRunner>() ?? _loaderRoot.AddComponent<PluginRunner>();
            runner.Manager = this;

            HarmonyBootstrap.EnsurePatched();
            try
            {
                var harmony = new HarmonyLib.Harmony("ShelteredModManager.PluginManager");
                SaveProtectionPatches.ApplyPatches(harmony);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginManager.InitializeLoader", "Failed to apply save protection patches: " + ex.Message);
            }
        }

        private void LogAssemblyResolution()
        {
            MMLog.Write("[Assembly Resolution]");
            int failures = 0;

            failures += LogAssembly("ModAPI", Assembly.GetExecutingAssembly());
            failures += LogAssembly("0Harmony", ResolveAssemblyByType("HarmonyLib.Harmony, 0Harmony"));

            MMLog.Write($"[Assembly Resolution] Failed Assemblies: {failures}");
        }

        private int LogAssembly(string name, Assembly asm)
        {
            if (asm == null)
            {
                MMLog.Write($"[Assembly Resolution] {name}.dll: <missing> ✗");
                return 1;
            }

            var path = SafeAssemblyLocation(asm);
            MMLog.Write($"[Assembly Resolution] {name}.dll: {path} ✓");
            return 0;
        }

        private Assembly ResolveAssemblyByType(string typeName)
        {
            try
            {
                var t = Type.GetType(typeName, throwOnError: false);
                return t != null ? t.Assembly : null;
            }
            catch { return null; }
        }

        private string SafeAssemblyLocation(Assembly asm)
        {
            try { return asm.Location; } catch { return "<location unavailable>"; }
        }

        private void LogSceneApiDetection()
        {
            var modernAvailable = RuntimeCompat.IsModernSceneApi;
            var usingModern = PluginRunner.IsModernUnity;
            MMLog.Write($"[Loader] Assembly resolution and Scene API detection complete.");
        }

        private List<string> ReadLoadOrderFromFile(string modsRoot)
        {
            var orderedIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var path = Path.Combine(modsRoot ?? string.Empty, "loadorder.json");
                if (!File.Exists(path)) return null;

                var json = File.ReadAllText(path);
                var obj = JsonUtility.FromJson<SimpleLoadOrder>(json);
                if (obj != null && obj.order != null)
                {
                    foreach (var raw in obj.order)
                    {
                        if (string.IsNullOrEmpty(raw)) continue;
                        var id = raw.Trim().ToLowerInvariant();
                        if (seen.Add(id)) orderedIds.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Failed to read loadorder.json: " + ex.Message);
            }
            return orderedIds;
        }

        [Serializable]
        private class SimpleLoadOrder { public string[] order; }

        /// <summary>
        /// Discovers mods on disk and applies load order if present.
        /// </summary>
        private void DiscoverAndOrderMods(List<string> orderedModIds)
        {
            var discovered = ModDiscovery.DiscoverAllMods();
            MMLog.WriteDebug($"[PluginManager] DiscoverAndOrderMods: {discovered.Count} mods found on disk.");
            foreach (var m in discovered) MMLog.WriteDebug($"[PluginManager]   - On Disk: '{m.Id}' at '{m.RootPath}'");

            if (orderedModIds == null)
            {
                MMLog.WriteDebug("[PluginManager] No load order provided (loadorder.json missing). Enabling ALL discovered mods.");
                LoadedMods = discovered;
                return;
            }

            if (orderedModIds.Count == 0)
            {
                MMLog.Write("[PluginManager] Explicit empty load order found. Enabling NO mods.");
                LoadedMods = new List<ModEntry>();
                return;
            }

            MMLog.WriteDebug($"[PluginManager] Applying load order (contains {orderedModIds.Count} IDs).");
            var ordered = new List<ModEntry>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var id in orderedModIds)
            {
                MMLog.WriteDebug($"[PluginManager]   Looking for ordered ID: '{id}'");
                var mod = discovered.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
                if (mod != null)
                {
                    if (seenIds.Add(mod.Id))
                    {
                        ordered.Add(mod);
                        MMLog.WriteDebug($"[PluginManager]     FOUND and enabled: {mod.Id}");
                    }
                }
                else
                {
                    MMLog.WriteDebug($"[PluginManager]     NOT FOUND on disk: {id}");
                }
            }

            // Ensure LoadedMods only contains the successfully discovered and enabled mods.
            LoadedMods = ordered;
            MMLog.WriteDebug($"[PluginManager] Final LoadedMods count: {LoadedMods.Count}");
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
            MMLog.Write($"[loader] LoadAndInitializePlugins: Starting with {orderedMods.Count} mods");

            foreach (var entry in orderedMods)
            {
                MMLog.Write($"[loader] Processing mod: {entry.Id}");

                List<Assembly> modAssemblies = null;
                try
                {
                    MMLog.Write($"[loader] Loading assemblies for {entry.Id} from {entry.AssembliesPath}");
                    modAssemblies = ModDiscovery.LoadAssemblies(entry);
                    MMLog.Write($"[loader] Loaded {modAssemblies.Count} assemblies for {entry.Id}");
                }
                catch (Exception ex)
                {
                    MMLog.Write($"[loader] failed to load assemblies for '{entry.Id}': {ex.Message}");
                    _loadErrors++;
                    continue;
                }

                foreach (var asm in modAssemblies)
                {
                    Type[] types = new Type[0];
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types.Where(x => x != null).ToArray(); }

                    MMLog.Write($"[loader] Found {types.Length} types in assembly {asm.FullName}");

                    foreach (var type in types)
                    {
                        if (type == null || type.IsAbstract || !type.IsClass) continue;
                        if (!typeof(IModPlugin).IsAssignableFrom(type)) continue;

                        MMLog.Write($"[loader] Found IModPlugin: {type.FullName}");

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

                            MMLog.Write($"[loader] Initializing plugin: {type.FullName}");
                            plugin.Initialize(ctx);
                            MMLog.Write($"[loader] Starting plugin: {type.FullName}");
                            plugin.Start(ctx);
                            ctx.Log.Info("Started.");
                        }
                        catch (Exception ex)
                        {
                            MMLog.WriteError($"[loader] error starting plugin '{type.FullName}': {ex.Message}");
                            _loadErrors++;
                        }
                    }
                }
            }

            MMLog.Write($"[loader] LoadAndInitializePlugins complete. Total plugins loaded: {_plugins.Count}");
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
            try 
            { 
                byte[] assemblyBytes = File.ReadAllBytes(path);
                return Assembly.Load(assemblyBytes); 
            } 
            catch (Exception ex) 
            { 
                MMLog.WarnOnce("PluginManager.SafeLoadAssembly", "Error loading assembly: " + ex.Message); 
                return null; 
            }
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            try { return Type.GetType(typeName, throwOnError: false); }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.ResolveType", "Error resolving type: " + ex.Message); return null; }
        }
    }

    /// <summary>
    /// Hosts plugin lifecycle, update ticks, and scene events with 5.3/5.6 compatibility.
    /// </summary>
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
            _useModernApi = TryHookModernSceneEvents();
            IsModernUnity = _useModernApi;
            if (!_useModernApi)
            {
                ThrowLegacyFallback();
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

        private bool TryHookModernSceneEvents()
        {
            try
            {
                if (!RuntimeCompat.IsModernSceneApi)
                {
                    MMLog.WriteDebug("[PluginRunner] SceneManager modern API not detected (Unity 5.3?).");
                    return false;
                }

                var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                if (sceneManagerType == null)
                {
                    MMLog.WriteDebug("[PluginRunner] SceneManager type not found.");
                    return false;
                }

                var sceneLoadedEvent = sceneManagerType.GetEvent("sceneLoaded");
                if (sceneLoadedEvent == null)
                {
                    MMLog.WriteError("[PluginRunner] SceneManager.sceneLoaded event not found.");
                    return false;
                }

                var sceneUnloadedEvent = sceneManagerType.GetEvent("sceneUnloaded");
                var onLoadedMethod = GetType().GetMethod("OnSceneLoadedModern", BindingFlags.NonPublic | BindingFlags.Instance);
                var onUnloadedMethod = GetType().GetMethod("OnSceneUnloadedModern", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onLoadedMethod == null)
                {
                    MMLog.WriteError("[PluginRunner] OnSceneLoadedModern method missing.");
                    return false;
                }
                if (onUnloadedMethod == null)
                {
                    MMLog.WriteError("[PluginRunner] OnSceneUnloadedModern method missing.");
                    return false;
                }

                _sceneLoadedDelegate = Delegate.CreateDelegate(sceneLoadedEvent.EventHandlerType, this, onLoadedMethod);
                sceneLoadedEvent.GetAddMethod().Invoke(null, new object[] { _sceneLoadedDelegate });

                if (sceneUnloadedEvent != null)
                {
                    _sceneUnloadedDelegate = Delegate.CreateDelegate(sceneUnloadedEvent.EventHandlerType, this, onUnloadedMethod);
                    sceneUnloadedEvent.GetAddMethod().Invoke(null, new object[] { _sceneUnloadedDelegate });
                }

                IsModernUnity = true;
                MMLog.WriteDebug("[PluginRunner] Modern scene events hooked successfully.");

                try
                {
                    var activeSceneProp = sceneManagerType.GetProperty("activeScene");
                    var activeScene = activeSceneProp != null ? activeSceneProp.GetValue(null, null) : null;
                    var isLoadedProp = activeScene != null ? activeScene.GetType().GetProperty("isLoaded") : null;
                    if (activeScene != null && isLoadedProp != null && (bool)isLoadedProp.GetValue(activeScene, null))
                    {
                        OnSceneLoadedModern(activeScene, null);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("PluginRunner.ActiveScene", "Failed to read activeScene: " + ex.Message);
                }

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[PluginRunner] Failed to hook modern scene events: " + ex.Message);
                return false;
            }
        }

        private void ThrowLegacyFallback()
        {
            if (_useModernApi) return;
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
