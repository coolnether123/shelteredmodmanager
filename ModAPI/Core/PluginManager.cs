using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using HarmonyLib;
using ModAPI.Harmony;
using ModAPI.Hooks;
using ModAPI.Spine;
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
        private readonly List<IModSessionEvents> _sessionEvents;
        private int _loadErrors;

        private GameObject _loaderRoot;
        private string _gameRoot;
        private string _modsRoot;

        /// <summary>
        /// Mods that were discovered and accepted by load-order filtering for this session.
        /// </summary>
        public static List<ModEntry> LoadedMods { get; private set; }


        private PluginManager()
        {
            _plugins = new List<IModPlugin>();
            _updates = new List<IModUpdate>();
            _shutdown = new List<IModShutdown>();
            _sceneEvents = new List<IModSceneEvents>();
            _sessionEvents = new List<IModSessionEvents>();
            LoadedMods = new List<ModEntry>();
        }

        /// <summary>
        /// Returns the singleton loader coordinator used by Doorstop startup code.
        /// </summary>
        public static PluginManager getInstance()
        {
            if (instance == null)
            {
                instance = new PluginManager();
            }
            return instance;
        }

        /// <summary>
        /// Exposes active plugin instances for diagnostics and debug UI.
        /// </summary>
        public IEnumerable<IModPlugin> GetPlugins()
        {
            return _plugins;
        }

        /// <summary>
        /// Main startup entry point. Initializes loader infrastructure and bootstraps all mods.
        /// </summary>
        /// <param name="doorstepGameObject">
        /// Optional pre-created root object. If null, a persistent root is created.
        /// </param>
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

            MMLog.WriteDebug(string.Format("Loaded {0} plugin(s). Updates={1}, Shutdown={2}, SceneEvents={3}", _plugins.Count, _updates.Count, _shutdown.Count, _sceneEvents.Count));

            stopwatch.Stop();
            var ms = stopwatch.ElapsedMilliseconds;
            MMLog.Write(string.Format("Startup complete in {0}ms. Loaded {1} plugin(s), {2} error(s).", ms, _plugins.Count, _loadErrors));
        }

        /// <summary>
        /// Creates the loader GameObject, attaches the runner, and applies core patches.
        /// </summary>
        private void InitializeLoader(GameObject doorstepGameObject)
        {
            // --- FIX: Force Link the ModAPI assembly ---
            // Because plugins are loaded via bytes (no file lock), they live in an anonymous context.
            // This resolver ensures they link back to the ALREADY LOADED ModAPI instance,
            // preventing duplicate assembly loads and fixing IsAssignableFrom failures.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name;
                if (name == "ModAPI" || name == "ModAPI.Core") return Assembly.GetExecutingAssembly();
                return null;
            };

            _gameRoot = Directory.GetParent(Application.dataPath).FullName;
            _modsRoot = Path.Combine(_gameRoot, "mods");

            _loaderRoot = doorstepGameObject != null ? doorstepGameObject : new GameObject("ModAPI.Loader");
            UnityEngine.Object.DontDestroyOnLoad(_loaderRoot);

            var runner = _loaderRoot.GetComponent<PluginRunner>() ?? _loaderRoot.AddComponent<PluginRunner>();
            runner.Manager = this;

            HarmonyBootstrap.EnsurePatched();
            
            // Force injection in case SaveManager.Awake already ran before we could patch it
            try { SaveManager_Injection_Patch.Inject(SaveManager.instance); } catch { }

            try
            {
                var harmony = new HarmonyLib.Harmony("ShelteredModManager.PluginManager");
                SaveProtectionPatches.ApplyPatches(harmony);
                
                // Initialize Core Systems
                ModAPI.Saves.Events.OnAfterLoad += ModRandomState.Load;
                ModAPI.Saves.Events.OnBeforeSave += ModRandomState.Save;

                // Track lifecycle events for plugins
                ModAPI.Events.GameEvents.OnSessionStarted += OnSessionStarted;
                ModAPI.Events.GameEvents.OnNewGame += OnNewGame;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginManager.InitializeLoader", "Failed to apply save protection patches: " + ex.Message);
            }
        }

        /// <summary>
        /// Emits a compact dependency-resolution snapshot to help diagnose bootstrap failures.
        /// </summary>
        private void LogAssemblyResolution()
        {
            MMLog.WriteDebug("Assembly Resolution");
            int failures = 0;

            failures += LogAssembly("ModAPI", Assembly.GetExecutingAssembly());
            failures += LogAssembly("0Harmony", ResolveAssemblyByType("HarmonyLib.Harmony, 0Harmony"));

            MMLog.WriteDebug($"Assembly Resolution: Failed Assemblies: {failures}");
        }

        private int LogAssembly(string name, Assembly asm)
        {
            if (asm == null)
            {
                MMLog.Write($"{name}.dll: <missing> ✗");
                return 1;
            }

            var path = SafeAssemblyPath(asm);
            MMLog.WriteDebug($"{name}.dll: {path} ✓");
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

        /// <summary>
        /// Records whether the runtime is using modern SceneManager callbacks or legacy fallback.
        /// </summary>
        private void LogSceneApiDetection()
        {
            var modernAvailable = RuntimeCompat.IsModernSceneApi;
            var usingModern = PluginRunner.IsModernUnity;
            MMLog.WriteDebug($"Scene API Detection: ModernAvailable={modernAvailable}, UsingModern={usingModern}");
        }

        /// <summary>
        /// Reads and normalizes <c>loadorder.json</c> into a unique lowercase ID list.
        /// </summary>
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
                string[] order = null;
                if (obj != null && obj.order != null)
                {
                    order = obj.order;
                }
                else
                {
                    // Robust fallback parser for loadorder.json formats that JsonUtility can fail on.
                    order = TryExtractOrderArray(json);
                }

                if (order == null)
                {
                    MMLog.Write("loadorder.json exists but no readable 'order' array was found. Treating as explicit empty load order.");
                    return new List<string>();
                }

                foreach (var raw in order)
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    var id = raw.Trim().ToLowerInvariant();
                    if (seen.Add(id)) orderedIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Failed to read loadorder.json: " + ex.Message);
                return null;
            }
            return orderedIds;
        }

        private static string[] TryExtractOrderArray(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                int keyPos = json.IndexOf("\"order\"", StringComparison.OrdinalIgnoreCase);
                if (keyPos < 0) return null;

                int arrayStart = json.IndexOf('[', keyPos);
                if (arrayStart < 0) return null;

                int depth = 0;
                int arrayEnd = -1;
                for (int i = arrayStart; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '[') depth++;
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            arrayEnd = i;
                            break;
                        }
                    }
                }
                if (arrayEnd < 0) return null;

                string arrayBody = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    arrayBody,
                    "\"((?:\\\\.|[^\"\\\\])*)\"",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                var result = new List<string>();
                for (int i = 0; i < matches.Count; i++)
                {
                    var raw = matches[i].Groups[1].Value;
                    if (!string.IsNullOrEmpty(raw))
                        result.Add(raw.Replace("\\\"", "\"").Replace("\\\\", "\\"));
                }
                return result.ToArray();
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        private class SimpleLoadOrder { public string[] order; }

        /// <summary>
        /// Discovers mods on disk and applies load order if present.
        /// </summary>
        private void DiscoverAndOrderMods(List<string> orderedModIds)
        {
            var discovered = ModDiscovery.DiscoverAllMods();
            MMLog.WriteDebug($"DiscoverAndOrderMods: {discovered.Count} mods found on disk.");
            foreach (var m in discovered) MMLog.WriteDebug($"  - On Disk: '{m.Id}' at '{m.RootPath}'");

            if (orderedModIds == null)
            {
                MMLog.WriteDebug("No load order provided (loadorder.json missing). Enabling ALL discovered mods.");
                LoadedMods = discovered;
                return;
            }

            if (orderedModIds.Count == 0)
            {
                MMLog.Write("Explicit empty load order found. Enabling NO mods.");
                LoadedMods = new List<ModEntry>();
                return;
            }

            MMLog.WriteDebug($"Applying load order (contains {orderedModIds.Count} IDs).");
            var ordered = new List<ModEntry>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var id in orderedModIds)
            {
                MMLog.WriteDebug($"  Looking for ordered ID: '{id}'");
                var mod = discovered.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
                if (mod != null)
                {
                    if (seenIds.Add(mod.Id))
                    {
                        ordered.Add(mod);
                        MMLog.WriteDebug($"    FOUND and enabled: {mod.Id}");
                    }
                }
                else
                {
                    MMLog.WriteDebug($"    NOT FOUND on disk: {id}");
                }
            }

            // Ensure LoadedMods only contains the successfully discovered and enabled mods.
            LoadedMods = ordered;
            MMLog.WriteDebug($"Final LoadedMods count: {LoadedMods.Count}");
        }

        /// <summary>
        /// Attaches always-on runtime inspection tooling to the loader root.
        /// </summary>
        private void AttachInspectorTools()
        {
            try
            {
                // Core inspection (Safe for production/diagnostic use)
                if (_loaderRoot.GetComponent<ModAPI.Inspector.RuntimeInspector>() == null)
                    _loaderRoot.AddComponent<ModAPI.Inspector.RuntimeInspector>();
                if (_loaderRoot.GetComponent<ModAPI.Inspector.BoundsHighlighter>() == null)
                    _loaderRoot.AddComponent<ModAPI.Inspector.BoundsHighlighter>();
                if (_loaderRoot.GetComponent<ModAPI.UI.UIDebugInspector>() == null)
                    _loaderRoot.AddComponent<ModAPI.UI.UIDebugInspector>();

                // Advanced developer tools (Disabled if decompiler is missing)
                // This ensures F10 and F12 tools are not accessible in production builds.
                if (File.Exists(ModAPI.Inspector.SourceCacheManager.ResolveDecompilerPath()))
                {
                    if (_loaderRoot.GetComponent<ModAPI.Inspector.RuntimeILInspector>() == null)
                        _loaderRoot.AddComponent<ModAPI.Inspector.RuntimeILInspector>();
                    if (_loaderRoot.GetComponent<ModAPI.Inspector.ExecutionTracer>() == null)
                        _loaderRoot.AddComponent<ModAPI.Inspector.ExecutionTracer>();
                    if (_loaderRoot.GetComponent<ModAPI.Inspector.RuntimeDebuggerUI>() == null)
                        _loaderRoot.AddComponent<ModAPI.Inspector.RuntimeDebuggerUI>();
                    
                    MMLog.WriteDebug("Advanced developer tools (F10/F12) enabled.");
                }
                else
                {
                    MMLog.WriteDebug("Decompiler not found. Advanced developer tools (F10/F12) disabled for production.");
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.AttachInspectorTools", "Error attaching inspector: " + ex.Message); }
        }

        /// <summary>
        /// Loads mod assemblies, discovers <see cref="IModPlugin"/> implementations, and runs
        /// Initialize/Start in load-order sequence.
        /// </summary>
        private void LoadAndInitializePlugins(List<ModEntry> orderedMods)
        {
            MMLog.WriteDebug(string.Format("LoadAndInitializePlugins: Starting with {0} mods", orderedMods.Count));

            foreach (var entry in orderedMods)
            {
                MMLog.WriteDebug($"Processing mod: {entry.Id}");

                List<Assembly> modAssemblies = null;
                try
                {
                    MMLog.WriteDebug($"Loading assemblies for {entry.Id} from {entry.AssembliesPath}");
                    modAssemblies = ModDiscovery.LoadAssemblies(entry);
                    MMLog.WriteDebug($"Loaded {modAssemblies.Count} assemblies for {entry.Id}");
                }
                catch (Exception ex)
                {
                    MMLog.Write($"failed to load assemblies for '{entry.Id}': {ex.Message}");
                    _loadErrors++;
                    continue;
                }

                // Register mod and its assemblies in the global registry for cross-mod access and lookup
                ModRegistry.Register(entry);
                foreach (var asm in modAssemblies)
                {
                    ModRegistry.RegisterAssemblyForMod(asm, entry);

                    Type[] types = null;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }

                    if (types == null) continue;

                    foreach (var type in types)
                    {
                        if (type == null) continue;
                        if (!type.IsClass || type.IsAbstract) continue;
                        
                        try
                        {
                            if (!typeof(IModPlugin).IsAssignableFrom(type)) continue;
                        }
                        catch { continue; } // Handle cases where IsAssignableFrom might throw due to missing deps

                        MMLog.WriteDebug($"Found IModPlugin: {type.FullName}");

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
                            var ss = plugin as IModSessionEvents; if (ss != null) _sessionEvents.Add(ss);
                            
                            // Register the settings provider if the plugin implements it
                            MMLog.WriteDebug($"Initializing plugin: {type.FullName}");
                            plugin.Initialize(ctx);

                            // If the plugin didn't set a provider during Initialize, check if it implements it directly
                            ModAPI.Spine.ISettingsProvider sp = plugin as ModAPI.Spine.ISettingsProvider;
                            if (entry != null && entry.SettingsProvider == null && sp != null)
                            {
                                entry.SettingsProvider = sp;
                            }

                            MMLog.WriteDebug($"Starting plugin: {type.FullName}");
                            plugin.Start(ctx);
                            ctx.Log.Info("Started.");
                        }
                        catch (Exception ex)
                        {
                            MMLog.WriteError($"error starting plugin '{type.FullName}': {ex.Message}");
                            _loadErrors++;
                        }
                    }
                }
            }

            MMLog.Write(string.Format("LoadAndInitializePlugins complete. Total plugins loaded: {0}", _plugins.Count));
        }

        /// <summary>
        /// Schedules work onto the main Unity thread in the next update tick.
        /// </summary>
        internal void EnqueueNextFrame(Action a)
        {
            var runner = _loaderRoot != null ? _loaderRoot.GetComponent<PluginRunner>() : null;
            if (runner != null)
            {
                MMLog.WriteDebug("Runner type: " + runner.GetType().FullName);
                runner.Enqueue(a);
            }
        }

        /// <summary>
        /// Forwards Unity's update tick to plugins that opted into <see cref="IModUpdate"/>.
        /// </summary>
        internal void OnUnityUpdate()
        {
            for (int i = 0; i < _updates.Count; i++)
            {
                try { _updates[i].Update(); }
                catch (Exception ex) { MMLog.Write($"Update() failed: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Broadcasts scene-loaded events to plugins that implement scene lifecycle hooks.
        /// </summary>
        internal void OnSceneLoaded(string name)
        {
            for (int i = 0; i < _sceneEvents.Count; i++)
            {
                try { _sceneEvents[i].OnSceneLoaded(name); }
                catch (Exception ex) { MMLog.Write($"OnSceneLoaded failed: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Broadcasts scene-unloaded events to plugins that implement scene lifecycle hooks.
        /// </summary>
        internal void OnSceneUnloaded(string name)
        {
            for (int i = 0; i < _sceneEvents.Count; i++)
            {
                try { _sceneEvents[i].OnSceneUnloaded(name); }
                catch (Exception ex) { MMLog.Write($"OnSceneUnloaded failed: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Broadcasts session-start events after game state is considered live.
        /// </summary>
        internal void OnSessionStarted()
        {
            for (int i = 0; i < _sessionEvents.Count; i++)
            {
                try { _sessionEvents[i].OnSessionStarted(); }
                catch (Exception ex) { MMLog.Write($"OnSessionStarted failed: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Handles New Game lifecycle fanout and reseeds session-scoped ModRandom state.
        /// </summary>
        internal void OnNewGame()
        {
            // Initialize ModRandom for the new world
            ModRandom.Initialize(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            ModRandom.NotifySeedChanged();

            for (int i = 0; i < _sessionEvents.Count; i++)
            {
                try { _sessionEvents[i].OnNewGame(); }
                catch (Exception ex) { MMLog.Write($"OnNewGame failed: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Calls shutdown handlers in reverse registration order.
        /// </summary>
        public void ShutdownAll()
        {
            MMLog.WriteInfo($"ShutdownAll started for {_plugins.Count} plugins.");
            for (int i = _shutdown.Count - 1; i >= 0; i--)
            {
                var s = _shutdown[i];
                try 
                { 
                    MMLog.WriteDebug($"Shutting down: {s.GetType().FullName}");
                    s.Shutdown(); 
                }
                catch (Exception ex) { MMLog.Write($"Shutdown() failed for {s.GetType().FullName}: {ex.Message}"); }
            }
            MMLog.WriteInfo("ShutdownAll complete.");
        }

        private string SafeModIdFor(Type type)
        {
            ModEntry entry;
            if (ModRegistry.TryGetModByAssembly(type.Assembly, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
                return entry.Id;
            return type.Namespace ?? type.Name;
        }

        /// <summary>
        /// Builds a per-plugin context object with logging, save access, and scheduler bindings.
        /// </summary>
        private IPluginContext BuildContextFor(Type type, GameObject pluginRoot)
        {
            ModEntry entry = null;
            ModRegistry.TryGetModByAssembly(type.Assembly, out entry);

            string asmPath = SafeAssemblyPath(type.Assembly);
            string modRoot = entry != null ? entry.RootPath : ProbeModRootFromAssembly(asmPath);

            string modId = entry != null && !string.IsNullOrEmpty(entry.Id) ? entry.Id : (type.Namespace ?? type.Name);
            var log = new PrefixedLogger(modId);
            if (entry != null && entry.About != null)
            {
                log.IsDebugEnabled = entry.About.debugLogging;
            }
            ISettingsProvider settings = null;
            // Legacy AutoSettings support? Replaced by newer auto-scan in ModManagerBase

            return new PluginContextImpl
            {
                LoaderRoot = _loaderRoot,
                PluginRoot = pluginRoot,
                Mod = entry,
                Settings = settings,
                Log = log,
                Game = new GameHelperImpl(),
                SaveSystem = new SaveSystemImpl(modId),
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
                return Assembly.LoadFrom(path);
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

}
