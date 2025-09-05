using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LoadOrderResolver;

/**
* Original Author: benjaminfoo
* Maintainer: coolnether123
* See: https://github.com/benjaminfoo/shelteredmodmanager
* See: https://code.msdn.microsoft.com/windowsdesktop/Creating-a-simple-plugin-b6174b62
* 
* 
* This class handles the overall plugin-management (like loading and executing them)
* 
* BREAKING CHANGE (WIP): Updated to the new context-first plugin API (IModPlugin et al.).
*  - Each plugin now gets a dedicated parent GameObject for clean teardown.
*  - Safe per-plugin try/catch so one bad plugin won't break others.
*  - Optional Update/Shutdown/Scene event hooks via IModUpdate/IModShutdown/IModSceneEvents.
*  - Honors loadorder.json 'order' and 'enabled' flags before loading.
*  - Emits clearer diagnostics via a per-mod prefixed logger.
*  (Coolnether123)
*/
public class PluginManager
{
    private static PluginManager instance;

    private readonly List<IModPlugin> _plugins;          // all loaded plugin instances
    private readonly List<IModUpdate> _updates;          // plugins that implement IModUpdate
    private readonly List<IModShutdown> _shutdown;       // plugins that implement IModShutdown
    private readonly List<IModSceneEvents> _sceneEvents; // plugins that implement IModSceneEvents

    private GameObject _loaderRoot;                      // global host object
    private string _gameRoot;                            // <GameRoot>
    private string _modsRoot;                            // <GameRoot>/mods

    public static List<ModEntry> LoadedMods { get; private set; } // Expose loaded mods

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
        // Build absolute path to the mods directory (from game's data folder)
        _gameRoot = Directory.GetParent(Application.dataPath).FullName;    // same as before
        _modsRoot = Path.Combine(_gameRoot, "mods");                       // consistent naming
        _loaderRoot = doorstepGameObject;                                  // keep a handle for context

        var assemblies = new List<Assembly>();                             // collect candidate assemblies
        var activatedTypes = new HashSet<Type>(); // track explicit entry types  Coolnether123

        // Read load order file (unchanged input contract)
        var processedLof = LoadOrderResolver.ReadLoadOrderFile(_modsRoot);

        // Discover all mods (About.json-driven)
        var discovered = ModDiscovery.DiscoverAllMods();

        // Enabled mods filtering:
        // Behavior change: If loadorder.json exists, treat its 'order' as a strict allow-list (even if empty).
        // If no loadorder.json is present, load no mods by default to avoid surprising global enable.
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
        catch { }

        // Resolve dependency-aware load order
        var resolution = LoadOrderResolver.Resolve(discovered, processedLof.Order ?? new string[0]);
        var orderedMods = resolution.Mods; // already sorted
        LoadedMods = orderedMods; // Populate the new static property
        if (resolution.MissingHardDependencies != null && resolution.MissingHardDependencies.Count > 0)
        {
            foreach (var e in resolution.MissingHardDependencies)
                MMLog.Write("[loader] dependency error: " + e);
        }

        // Attach a small runner to drive updates and scene events (once)
        var runner = _loaderRoot.GetComponent<PluginRunner>();
        if (runner == null) runner = _loaderRoot.AddComponent<PluginRunner>();
        runner.Manager = this; // set back-reference // Coolnether123

        // Load and start plugins mod-by-mod in resolved order
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

                    try
                    {
                        var plugin = (IModPlugin)Activator.CreateInstance(type);
                        var pluginRoot = new GameObject($"Mod-{SafeModIdFor(type)}");
                        pluginRoot.transform.SetParent(_loaderRoot.transform, false);

                        var ctx = BuildContextFor(type, pluginRoot);
                        _plugins.Add(plugin);

                        // Optional capability caches
                        var u = plugin as IModUpdate; if (u != null) _updates.Add(u);
                        var s = plugin as IModShutdown; if (s != null) _shutdown.Add(s);
                        var se = plugin as IModSceneEvents; if (se != null) _sceneEvents.Add(se);

                        // Call lifecycle with isolation
                        plugin.Initialize(ctx);
                        plugin.Start(ctx);
                        ctx.Log.Info("Started.");
                    }
                    catch (Exception ex)
                    {
                        MMLog.Write($"[loader] error starting plugin '{type.FullName}': {ex.Message}");
                    }
                }
            }
        }

        // Final load summary (simple)
        MMLog.Write($"[loader] Loaded {_plugins.Count} plugin(s). Updates={_updates.Count}, Shutdown={_shutdown.Count}, SceneEvents={_sceneEvents.Count}");
    }

    // Schedules an action on the next frame via the runner
    internal void EnqueueNextFrame(Action a)
    {
        var runner = _loaderRoot != null ? _loaderRoot.GetComponent<PluginRunner>() : null;
        if (runner != null) runner.Enqueue(a);
    }

    // Dispatch called by PluginRunner on Unity's Update()
    internal void OnUnityUpdate()
    {
        for (int i = 0; i < _updates.Count; i++)
        {
            try { _updates[i].Update(); }
            catch (Exception ex) { MMLog.Write($"[loader] Update() failed: {ex.Message}"); }
        }
    }

    // Scene events forwarded by PluginRunner
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

    // Graceful shutdown hook you can call from the host on exit if desired.
    public void ShutdownAll()
    {
        for (int i = _shutdown.Count - 1; i >= 0; i--)
        {
            try { _shutdown[i].Shutdown(); }
            catch (Exception ex) { MMLog.Write($"[loader] Shutdown() failed: {ex.Message}"); }
        }
    }

    // --- Helpers ---------------------------------------------------------

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
        ModRegistry.TryGetModByAssembly(type.Assembly, out entry);  // prefer registry

        // Fallback best-effort root (walk up to find About/)
        string asmPath = SafeAssemblyPath(type.Assembly);
        string modRoot = entry != null ? entry.RootPath : ProbeModRootFromAssembly(asmPath);

        // Compose logger and settings
        string modId = entry != null && !string.IsNullOrEmpty(entry.Id) ? entry.Id : (type.Namespace ?? type.Name);
        var log = new PrefixedLogger(modId);
        ModSettings settings = null;
        try { settings = ModSettings.ForAssembly(type.Assembly); } catch { settings = null; }

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
        try { return asm != null ? asm.Location : null; } catch { return null; }
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
        catch { }
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
        catch { }
        return list;
    }

    private static Assembly SafeLoadAssembly(string path)
    {
        try { return Assembly.LoadFrom(path); } catch { return null; }
    }

    // Resolves an explicitly declared entryType (About.json) safely
    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        try { return Type.GetType(typeName, throwOnError: false); } catch { return null; }
    }
}

// Small runner to drive Update() and scene events for plugins.
// Attached once to the loader's root GameObject. (New) (Coolnether123)
public class PluginRunner : MonoBehaviour
{
    private readonly Queue<Action> _nextFrame = new Queue<Action>();
    public PluginManager Manager; // set by PluginManager

    public void Enqueue(Action a)
    {
        if (a != null) _nextFrame.Enqueue(a);
    }

    private void Awake()
    {
        // subscribe to scene events once
        SceneManager.sceneLoaded += OnSceneLoadedInternal;
        SceneManager.sceneUnloaded += OnSceneUnloadedInternal;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedInternal;
        SceneManager.sceneUnloaded -= OnSceneUnloadedInternal;
    }

    private void Update()
    {
        // pump next-frame actions
        while (_nextFrame.Count > 0)
        {
            var a = _nextFrame.Dequeue();
            try { a(); } catch (Exception ex) { MMLog.Write($"[loader] next-frame action failed: {ex.Message}"); }
        }

        if (Manager != null) Manager.OnUnityUpdate();
    }

    private void OnSceneLoadedInternal(Scene scene, LoadSceneMode mode)
    {
        if (Manager != null) Manager.OnSceneLoaded(scene.name);
    }

    private void OnSceneUnloadedInternal(Scene scene)
    {
        if (Manager != null) Manager.OnSceneUnloaded(scene.name);
    }
}

// Adapter that prefixes log lines with the mod id. (New) (Coolnether123)
internal class PrefixedLogger : IModLogger
{
    private readonly string _prefix;
    public PrefixedLogger(string modId) { _prefix = string.IsNullOrEmpty(modId) ? "mod" : modId; }

    public void Info(string message) { MMLog.Write($"[{_prefix}] {message}"); }
    public void Warn(string message) { MMLog.Write($"[{_prefix}] WARN: {message}"); }
    public void Error(string message) { MMLog.Write($"[{_prefix}] ERROR: {message}"); }
}

// Private concrete context passed to plugins. (New) (Coolnether123)
internal class PluginContextImpl : IPluginContext
{
    public GameObject LoaderRoot { get; set; }
    public GameObject PluginRoot { get; set; }
    public ModEntry Mod { get; set; }
    public ModSettings Settings { get; set; }
    public IModLogger Log { get; set; }
    public string GameRoot { get; set; }
    public string ModsRoot { get; set; }

    public Action<Action> Scheduler; // set by PluginManager

    public void RunNextFrame(Action action) { Scheduler?.Invoke(action); }
    public Coroutine StartCoroutine(IEnumerator routine)
    {
        return LoaderRoot != null ? LoaderRoot.GetComponent<PluginRunner>().StartCoroutine(routine) : null;
    }
}
