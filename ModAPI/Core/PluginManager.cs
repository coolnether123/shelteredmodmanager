using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using ModAPI.Harmony;
using ModAPI.Loading;
using ModAPI.Persistence;
using ModAPI.Spine;
using ModAPI.Actors;
using Smm.Shared;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Discovers mods, loads assemblies, and wires plugin lifecycle callbacks.
    /// </summary>
    internal class PluginManager
    {
        private static PluginManager instance;

        private readonly List<IModPlugin> _plugins;
        private readonly List<IModUpdate> _updates;
        private readonly List<IModShutdown> _shutdown;
        private readonly List<IModSceneEvents> _sceneEvents;
        private readonly List<IModSessionEvents> _sessionEvents;
        private readonly List<PluginContextImpl> _pluginContexts;
        private readonly HashSet<string> _initializedGameRuntimeBootstraps;
        private int _loadErrors;
        private Stopwatch _startupStopwatch;

        private GameObject _loaderRoot;
        private string _gameRoot;
        private string _modsRoot;
        private const int ActivationBatchMinMods = 2;
        private const int ActivationBatchMaxMods = 4;
        private const int ActivationBatchBudgetMs = 18;

        private sealed class PreparedPluginAssembly
        {
            public string Path;
            public Assembly Assembly;
            public Type[] Types;
        }

        private sealed class PreparedModLoad
        {
            public ModEntry Entry;
            public List<PreparedPluginAssembly> Assemblies = new List<PreparedPluginAssembly>();
            public bool HasAssemblyLoadErrors;
        }

        private sealed class PreparedModActivationState
        {
            public List<PreparedModLoad> PreparedMods;
            public Action OnComplete;
            public int NextModIndex;
        }

        /// <summary>
        /// Mods that were discovered and accepted by load-order filtering for this session.
        /// </summary>
        public static List<ModEntry> LoadedMods { get; private set; }
        internal static readonly List<ModEntry> SuccessfullyLoadedMods = new List<ModEntry>();
        public static bool PluginsActivated { get; private set; }

        public static event Action OnSessionStartedEvent;
        public static event Action OnNewGameEvent;

        private PluginManager()
        {
            _plugins = new List<IModPlugin>();
            _updates = new List<IModUpdate>();
            _shutdown = new List<IModShutdown>();
            _sceneEvents = new List<IModSceneEvents>();
            _sessionEvents = new List<IModSessionEvents>();
            _pluginContexts = new List<PluginContextImpl>();
            _initializedGameRuntimeBootstraps = new HashSet<string>(StringComparer.Ordinal);
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
            _startupStopwatch = Stopwatch.StartNew();
            _loadErrors = 0;
            PluginsActivated = false;
            lock (SuccessfullyLoadedMods)
                SuccessfullyLoadedMods.Clear();

            MeasureStartupPhase("InitializeLoader", delegate { InitializeLoader(doorstepGameObject); });
            MeasureStartupPhase("LogAssemblyResolution", LogAssemblyResolution);
            MeasureStartupPhase("LogSceneApiDetection", LogSceneApiDetection);

            List<string> orderedModIds = null;
            MeasureStartupPhase("ReadLoadOrder", delegate { orderedModIds = ModLoadOrderReader.Read(_modsRoot); });

            if (orderedModIds != null && orderedModIds.Count == 0)
            {
                MMLog.Write("Explicit empty load order found. Enabling NO mods (core runtime remains active).");
                LoadedMods = new List<ModEntry>();
                AttachInspectorTools();
                CompleteStartupLog();
                return;
            }

            MeasureStartupPhase("DiscoverAndOrderMods", delegate { DiscoverAndOrderMods(orderedModIds); });

            MeasureStartupPhase("AttachInspectorTools", AttachInspectorTools);

            if (LoadedMods == null || LoadedMods.Count == 0)
            {
                CompleteStartupLog();
                return;
            }

            StartBackgroundPluginActivation(new List<ModEntry>(LoadedMods));
        }

        private void CompleteStartupLog()
        {
            if (_startupStopwatch == null)
            {
                MMLog.Write(string.Format("Startup complete. Loaded {0} plugin(s), {1} error(s).", _plugins.Count, _loadErrors));
                return;
            }

            _startupStopwatch.Stop();
            var ms = _startupStopwatch.ElapsedMilliseconds;
            MMLog.Write(string.Format("Startup complete in {0}ms. Loaded {1} plugin(s), {2} error(s).", ms, _plugins.Count, _loadErrors));
        }

        private static void MeasureStartupPhase(string phaseName, Action action)
        {
            if (action == null)
                return;

            var timer = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                LogStartupTiming(phaseName, timer);
            }
        }

        private static void LogStartupTiming(string phaseName, Stopwatch timer)
        {
            if (timer == null)
                return;

            timer.Stop();
            MMLog.WriteWithSource(
                MMLog.LogLevel.Info,
                MMLog.LogCategory.General,
                "StartupTiming",
                phaseName + " took " + timer.ElapsedMilliseconds + "ms.");
        }

        private static string SafeEntryId(ModEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id))
                return "<unknown>";

            return entry.Id;
        }

        private void StartBackgroundPluginActivation(List<ModEntry> orderedMods)
        {
            var runner = _loaderRoot != null ? _loaderRoot.GetComponent<PluginRunner>() : null;
            if (runner == null)
            {
                MMLog.WriteWarning("PluginRunner was unavailable for async startup preload. Falling back to synchronous activation.");
                LoadAndInitializePlugins(orderedMods);
                CompleteStartupLog();
                return;
            }

            MMLog.WriteDebug("Background startup preload beginning for " + orderedMods.Count + " mod(s).");
            var prepareTimer = Stopwatch.StartNew();
            ModThreads.RunAsync<List<PreparedModLoad>>(
                delegate
                {
                    return PrepareModLoads(orderedMods);
                },
                delegate(List<PreparedModLoad> preparedMods)
                {
                    LogStartupTiming("PrepareModLoads background", prepareTimer);
                    try
                    {
                        StartPreparedModActivation(preparedMods, CompleteStartupLog);
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("Async startup activation failed on main thread. Falling back to synchronous activation: " + ex.Message);
                        MeasureStartupPhase("LoadAndInitializePlugins fallback", delegate { LoadAndInitializePlugins(orderedMods); });
                        CompleteStartupLog();
                    }
                },
                delegate(Exception ex)
                {
                    LogStartupTiming("PrepareModLoads background failed", prepareTimer);
                    MMLog.WriteWarning("Background startup preload failed. Falling back to synchronous activation: " + ex.Message);
                    MeasureStartupPhase("LoadAndInitializePlugins fallback", delegate { LoadAndInitializePlugins(orderedMods); });
                    CompleteStartupLog();
                });
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
            // It also keeps shared runtime assemblies deterministic when mods reference the framework.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name;
                var sharedAssembly = SharedAssemblyResolver.ResolveSharedAssembly(name);
                if (sharedAssembly != null) return sharedAssembly;
                return null;
            };

            _gameRoot = Directory.GetParent(Application.dataPath).FullName;
            _modsRoot = Path.Combine(_gameRoot, "mods");

            _loaderRoot = doorstepGameObject != null ? doorstepGameObject : new GameObject("ModAPI.Loader");
            UnityEngine.Object.DontDestroyOnLoad(_loaderRoot);

            var runner = _loaderRoot.GetComponent<PluginRunner>() ?? _loaderRoot.AddComponent<PluginRunner>();
            runner.Manager = this;

            MeasureStartupPhase("SharedAssemblyResolver.LoadAvailableSharedRuntimeAssemblies", delegate
            {
                SharedAssemblyResolver.LoadAvailableSharedRuntimeAssemblies();
            });
            MeasureStartupPhase("HarmonyBootstrap.EnsurePatched", HarmonyBootstrap.EnsurePatched);

            try
            {
                MeasureStartupPhase("InitializeLoadedGameRuntimeBootstraps", InitializeLoadedGameRuntimeBootstraps);
                MeasureStartupPhase("SaveRuntimeAdapters.EnsureRuntimeReady", SaveRuntimeAdapters.EnsureRuntimeReady);

                // Initialize Core Systems
                GameLifecycleSources.AddAfterLoad(ModRandomState.Load);
                GameLifecycleSources.AddBeforeSave(ModRandomState.Save);
                GameLifecycleSources.AddSessionStarted(OnSessionStarted);
                GameLifecycleSources.AddNewGame(OnNewGame);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginManager.InitializeLoader", "Failed to initialize runtime bootstrap hooks: " + ex.Message);
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

            Assembly[] sharedRuntimeAssemblies = SharedAssemblyResolver.LoadAvailableSharedRuntimeAssemblies();
            MMLog.WriteDebug("Assembly Resolution: Shared runtime assemblies: " + sharedRuntimeAssemblies.Length);
            for (int i = 0; i < sharedRuntimeAssemblies.Length; i++)
            {
                var assembly = sharedRuntimeAssemblies[i];
                if (assembly == null)
                    continue;

                string name = null;
                try { name = assembly.GetName().Name; }
                catch { name = "<unknown>"; }

                MMLog.WriteDebug("shared runtime " + name + ".dll: " + SafeAssemblyPath(assembly));
            }

            MMLog.WriteDebug($"Assembly Resolution: Failed Assemblies: {failures}");
        }

        private int LogAssembly(string name, Assembly asm)
        {
            if (asm == null)
            {
                MMLog.Write($"{name}.dll: <missing> ?");
                return 1;
            }

            var path = SafeAssemblyPath(asm);
            MMLog.WriteDebug($"{name}.dll: {path} ?");
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

        /// <summary>
        /// Records whether the runtime is using modern SceneManager callbacks or legacy fallback.
        /// </summary>
        private void LogSceneApiDetection()
        {
            var modernAvailable = RuntimeCompat.IsModernSceneApi;
            var usingModern = PluginRunner.IsModernUnity;
            MMLog.WriteDebug($"Scene API Detection: ModernAvailable={modernAvailable}, UsingModern={usingModern}");
        }

        private List<PreparedModLoad> PrepareModLoads(List<ModEntry> orderedMods)
        {
            var timer = Stopwatch.StartNew();
            var prepared = new List<PreparedModLoad>();
            if (orderedMods == null || orderedMods.Count == 0)
            {
                LogStartupTiming("PrepareModLoads total", timer);
                return prepared;
            }

            for (int i = 0; i < orderedMods.Count; i++)
            {
                var entry = orderedMods[i];
                if (entry == null)
                    continue;

                string compatibilityReason;
                if (!IsRuntimeApiCompatible(entry, out compatibilityReason))
                {
                    MMLog.WriteError("PrepareModLoads: blocked incompatible mod '" + SafeEntryId(entry) + "' before assembly load: " + compatibilityReason);
                    _loadErrors++;
                    continue;
                }

                var modLoad = new PreparedModLoad();
                modLoad.Entry = entry;
                if (!TryPrepareAssemblies(modLoad, out compatibilityReason))
                {
                    MMLog.WriteError("PrepareModLoads: blocked mod '" + SafeEntryId(entry) + "': " + compatibilityReason);
                    _loadErrors++;
                    continue;
                }
                prepared.Add(modLoad);
            }

            LogStartupTiming("PrepareModLoads total", timer);
            return prepared;
        }

        private static bool TryPrepareAssemblies(PreparedModLoad prepared, out string reason)
        {
            var timer = Stopwatch.StartNew();
            var entry = prepared.Entry;
            // Inspect the whole package before loading any of its assemblies.
            // Keep the checked bytes so a changed file can't bypass the check.
            var images = new List<KeyValuePair<string, byte[]>>();
            if (!TryInspectAssemblies(entry, images, out reason))
                return false;

            foreach (var image in images)
            {
                try
                {
                    var asm = Assembly.Load(image.Value);
                    var types = GetLoadableTypes(asm, entry);
                    prepared.HasAssemblyLoadErrors |= types == null || Array.IndexOf(types, null) >= 0;
                    prepared.Assemblies.Add(new PreparedPluginAssembly
                    {
                        Path = image.Key,
                        Assembly = asm,
                        Types = types
                    });
                }
                catch (Exception ex)
                {
                    prepared.HasAssemblyLoadErrors = true;
                    MMLog.WriteError("PrepareAssemblies failed to load assembly '" + image.Key + "' for mod '" + entry.Id + "': " + ex);
                }
            }

            LogStartupTiming("PrepareAssemblies " + SafeEntryId(entry), timer);
            return true;
        }

        internal static bool CanPrepareMod(ModEntry entry)
        {
            string reason;
            return IsRuntimeApiCompatible(entry, out reason) && TryInspectAssemblies(entry, null, out reason);
        }

        private static bool TryInspectAssemblies(ModEntry entry, List<KeyValuePair<string, byte[]>> images, out string reason)
        {
            reason = null;
            string path = entry.AssembliesPath;
            try
            {
                string[] dllFiles;
                try { dllFiles = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories); }
                catch (DirectoryNotFoundException) { return true; } // Data-only mod.

                foreach (string dllPath in dllFiles)
                {
                    path = dllPath;
                    if (SharedAssemblyResolver.ShouldSkipModAssembly(dllPath))
                    {
                        if (images != null)
                            MMLog.WriteInfo("PrepareAssemblies: skipping shared runtime assembly '" + dllPath + "'.");
                        continue;
                    }

                    byte[] image = File.ReadAllBytes(dllPath);
                    var references = new List<AssemblyName>();
                    if (!ModAssemblyMetadata.ReadReferences(image, references))
                        continue; // Native dependency; the CLR does not load it as a managed assembly.
                    foreach (AssemblyName reference in references)
                    {
                        if (!ModAssemblyMetadata.IsLegacyReference(reference.Name, reference.Version.Major))
                            continue;
                        reason = ModAssemblyMetadata.LegacyUpdateMessage + " (" + Path.GetFileName(dllPath)
                            + " references " + reference.Name + " " + reference.Version + ").";
                        return false;
                    }
                    if (images != null)
                        images.Add(new KeyValuePair<string, byte[]>(dllPath, image));
                }
            }
            catch (Exception ex)
            {
                reason = "Could not inspect assembly metadata at '" + path + "': " + ex.Message;
                return false;
            }

            return true;
        }

        private void DiscoverAndOrderMods(List<string> orderedModIds)
        {
            LoadedMods = ModLoadPlanBuilder.DiscoverAndOrder(orderedModIds);
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
            ActivatePreparedMods(PrepareModLoads(orderedMods));
        }

        private void ActivatePreparedMods(List<PreparedModLoad> preparedMods)
        {
            if (preparedMods == null)
                preparedMods = new List<PreparedModLoad>();

            MeasureStartupPhase("RegisterPreparedModAssemblies", delegate { RegisterPreparedModAssemblies(preparedMods); });
            MeasureStartupPhase("InitializePreparedGameRuntimeBootstraps", delegate { InitializeGameRuntimeBootstraps(preparedMods); });

            for (int i = 0; i < preparedMods.Count; i++)
            {
                ActivatePreparedMod(preparedMods[i]);
            }

            LogPluginActivationComplete();
        }

        private void StartPreparedModActivation(List<PreparedModLoad> preparedMods, Action onComplete)
        {
            if (preparedMods == null)
                preparedMods = new List<PreparedModLoad>();

            MeasureStartupPhase("RegisterPreparedModAssemblies", delegate { RegisterPreparedModAssemblies(preparedMods); });
            MeasureStartupPhase("InitializePreparedGameRuntimeBootstraps", delegate { InitializeGameRuntimeBootstraps(preparedMods); });

            var state = new PreparedModActivationState
            {
                PreparedMods = preparedMods,
                OnComplete = onComplete,
                NextModIndex = 0
            };

            var runner = _loaderRoot != null ? _loaderRoot.GetComponent<PluginRunner>() : null;
            if (runner == null)
            {
                MMLog.WriteWarning("PluginRunner became unavailable during sliced activation. Activating remaining mods synchronously.");
                ActivateRemainingPreparedMods(state);
                return;
            }

            // This callback is already executing on PluginRunner.Update.  Run the
            // first batch now rather than requiring a second Unity frame: some
            // installs can pause immediately after startup while unfocused, which
            // otherwise leaves every plugin prepared but never activated.
            MMLog.WriteInfo("Sliced plugin activation starting for " + preparedMods.Count + " mod(s).");
            ActivateNextPreparedModBatch(state);
        }

        private void ActivateNextPreparedModBatch(PreparedModActivationState state)
        {
            if (state == null)
                return;

            if (state.PreparedMods == null)
                state.PreparedMods = new List<PreparedModLoad>();

            if (state.NextModIndex >= state.PreparedMods.Count)
            {
                CompletePreparedModActivation(state);
                return;
            }

            var batchTimer = Stopwatch.StartNew();
            int activated = 0;
            while (state.NextModIndex < state.PreparedMods.Count && activated < ActivationBatchMaxMods)
            {
                var prepared = state.PreparedMods[state.NextModIndex];
                state.NextModIndex++;
                activated++;

                var timer = Stopwatch.StartNew();
                try
                {
                    ActivatePreparedMod(prepared);
                }
                catch (Exception ex)
                {
                    var entry = prepared != null ? prepared.Entry : null;
                    MMLog.WriteError("error activating mod '" + SafeEntryId(entry) + "': " + ex.Message);
                    _loadErrors++;
                }
                finally
                {
                    var entry = prepared != null ? prepared.Entry : null;
                    LogStartupTiming("ActivateMod " + SafeEntryId(entry), timer);
                }

                if (activated >= ActivationBatchMinMods && batchTimer.ElapsedMilliseconds >= ActivationBatchBudgetMs)
                    break;
            }

            if (state.NextModIndex >= state.PreparedMods.Count)
            {
                CompletePreparedModActivation(state);
                return;
            }

            var runner = _loaderRoot != null ? _loaderRoot.GetComponent<PluginRunner>() : null;
            if (runner == null)
            {
                MMLog.WriteWarning("PluginRunner became unavailable during sliced activation. Activating remaining mods synchronously.");
                ActivateRemainingPreparedMods(state);
                return;
            }

            runner.Enqueue(delegate { ActivateNextPreparedModBatch(state); });
        }

        private void ActivateRemainingPreparedMods(PreparedModActivationState state)
        {
            if (state == null)
                return;

            if (state.PreparedMods == null)
                state.PreparedMods = new List<PreparedModLoad>();

            while (state.NextModIndex < state.PreparedMods.Count)
            {
                var prepared = state.PreparedMods[state.NextModIndex];
                state.NextModIndex++;
                try
                {
                    ActivatePreparedMod(prepared);
                }
                catch (Exception ex)
                {
                    var entry = prepared != null ? prepared.Entry : null;
                    MMLog.WriteError("error activating mod '" + SafeEntryId(entry) + "': " + ex.Message);
                    _loadErrors++;
                }
            }

            CompletePreparedModActivation(state);
        }

        private void CompletePreparedModActivation(PreparedModActivationState state)
        {
            LogPluginActivationComplete();
            if (state != null && state.OnComplete != null)
                state.OnComplete();
        }

        private void ActivatePreparedMod(PreparedModLoad prepared)
        {
            var entry = prepared != null ? prepared.Entry : null;
            if (prepared == null || entry == null)
                return;

            string compatibilityReason;
            if (!IsRuntimeApiCompatible(entry, out compatibilityReason))
            {
                MMLog.WriteError("Blocked incompatible mod '" + SafeEntryId(entry) + "' before plugin activation: " + compatibilityReason);
                _loadErrors++;
                return;
            }

            ModRuntime.NotifyModActivating(entry);

            bool isLibrary = prepared.Assemblies.Count > 0 && !prepared.HasAssemblyLoadErrors;
            for (int j = 0; j < prepared.Assemblies.Count; j++)
            {
                var preparedAssembly = prepared.Assemblies[j];
                if (preparedAssembly == null || preparedAssembly.Assembly == null)
                    continue;

                if (isLibrary)
                    isLibrary = !Array.Exists(preparedAssembly.Types, type => IsPluginType(type) || IsGameRuntimeBootstrapType(type));
                ActivatePluginTypes(entry, preparedAssembly.Types);
            }
            if (isLibrary || (prepared.Assemblies.Count == 0 && !prepared.HasAssemblyLoadErrors
                && string.Equals(entry.Id, "com.harmony.0harmony", StringComparison.OrdinalIgnoreCase)
                && FindLoadedRuntimeAssembly("0Harmony") != null))
                ModRuntime.RecordSuccessfulLoad(entry);
        }

        private void LogPluginActivationComplete()
        {
            if (!PluginsActivated)
            {
                PluginsActivated = true;
                ModRuntime.NotifyPluginsActivated();
            }

            MMLog.Write(string.Format("LoadAndInitializePlugins complete. Total plugins loaded: {0}", _plugins.Count));
        }

        private void RegisterPreparedModAssemblies(List<PreparedModLoad> preparedMods)
        {
            for (int i = 0; i < preparedMods.Count; i++)
            {
                var prepared = preparedMods[i];
                var entry = prepared != null ? prepared.Entry : null;
                if (prepared == null || entry == null)
                    continue;

                ModRegistry.Register(entry);

                for (int j = 0; j < prepared.Assemblies.Count; j++)
                {
                    var preparedAssembly = prepared.Assemblies[j];
                    if (preparedAssembly == null || preparedAssembly.Assembly == null)
                        continue;

                    ModRegistry.RegisterAssemblyForMod(preparedAssembly.Assembly, entry);
                }
            }
        }

        private void InitializeGameRuntimeBootstraps(List<PreparedModLoad> preparedMods)
        {
            for (int i = 0; i < preparedMods.Count; i++)
            {
                var prepared = preparedMods[i];
                if (prepared == null)
                    continue;

                for (int j = 0; j < prepared.Assemblies.Count; j++)
                {
                    var preparedAssembly = prepared.Assemblies[j];
                    if (preparedAssembly == null)
                        continue;

                    InitializeGameRuntimeBootstraps(preparedAssembly.Types, prepared.Entry);
                }
            }
        }

        private void InitializeLoadedGameRuntimeBootstraps()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (!IsGameRuntimeAssemblyCandidate(assemblies[i]))
                    continue;

                InitializeGameRuntimeBootstraps(GetLoadableTypes(assemblies[i], null), null);
            }
        }

        private void InitializeGameRuntimeBootstraps(Type[] types, ModEntry entry)
        {
            if (types == null)
                return;

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (!IsGameRuntimeBootstrapType(type))
                    continue;

                var key = type.AssemblyQualifiedName ?? type.FullName;
                if (string.IsNullOrEmpty(key) || !_initializedGameRuntimeBootstraps.Add(key))
                    continue;

                var timer = Stopwatch.StartNew();
                try
                {
                    var bootstrap = (IGameRuntimeBootstrap)Activator.CreateInstance(type);
                    bootstrap.Initialize();
                    ModRuntime.RecordSuccessfulLoad(entry);
                    MMLog.WriteInfo("Initialized game runtime bootstrap: " + type.FullName);
                }
                catch (Exception ex)
                {
                    MMLog.WritePluginError(type.FullName, "game runtime bootstrap", ex);
                    MMLog.WriteError("Failed to initialize game runtime bootstrap '" + type.FullName + "': " + ex.Message);
                    _loadErrors++;
                }
                finally
                {
                    LogStartupTiming("RuntimeBootstrap " + type.FullName, timer);
                }
            }
        }

        private static bool IsGameRuntimeBootstrapType(Type type)
        {
            if (type == null || !type.IsClass || type.IsAbstract)
                return false;

            try
            {
                return typeof(IGameRuntimeBootstrap).IsAssignableFrom(type);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsGameRuntimeAssemblyCandidate(Assembly assembly)
        {
            if (assembly == null)
                return false;

            string name = null;
            try { name = assembly.GetName().Name; }
            catch { return false; }

            if (string.IsNullOrEmpty(name))
                return false;

            if (name == "ModAPI" || name == "0Harmony")
                return false;

            if (name.StartsWith("System", StringComparison.Ordinal)
                || name.StartsWith("mscorlib", StringComparison.Ordinal)
                || name.StartsWith("Microsoft", StringComparison.Ordinal)
                || name.StartsWith("Mono", StringComparison.Ordinal)
                || name.StartsWith("Unity", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static Type[] GetLoadableTypes(Assembly assembly, ModEntry entry)
        {
            if (assembly == null)
                return null;

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException rtle)
            {
                MMLog.WritePluginError(entry != null ? entry.Id : assembly.FullName, "type discovery", rtle);
                return rtle.Types;
            }
        }

        private void ActivatePluginTypes(ModEntry entry, Type[] types)
        {
            if (types == null)
                return;

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (!IsPluginType(type))
                    continue;

                MMLog.WriteDebug("Found IModPlugin: " + type.FullName);

                try
                {
                    IModPlugin plugin = null;
                    MeasureStartupPhase("Plugin " + type.FullName + " constructor", delegate
                    {
                        plugin = (IModPlugin)Activator.CreateInstance(type);
                    });

                    var pluginRoot = new GameObject("Mod-" + SafeModIdFor(type));
                    pluginRoot.transform.SetParent(_loaderRoot.transform, false);

                    var ctx = BuildContextFor(type, pluginRoot);
                    MMLog.WriteDebug("Initializing plugin: " + type.FullName);
                    MeasureStartupPhase("Plugin " + type.FullName + " Initialize", delegate { plugin.Initialize(ctx); });

                    _plugins.Add(plugin);

                    var u = plugin as IModUpdate; if (u != null) _updates.Add(u);
                    var s = plugin as IModShutdown; if (s != null) _shutdown.Add(s);
                    var se = plugin as IModSceneEvents; if (se != null) _sceneEvents.Add(se);
                    var ss = plugin as IModSessionEvents; if (ss != null) _sessionEvents.Add(ss);



                    var settingsProvider = plugin as ISettingsProvider;
                    if (entry != null && entry.SettingsProvider == null && settingsProvider != null)
                    {
                        RegisterSettingsProvider(entry, ctx, settingsProvider, "ISettingsProvider");
                        if (entry.SettingsProvider is ISettingsProvider2 && !(plugin is ModManagerBase))
                            SettingsProviderLifecycle.BindProviderSaveOnBeforeSave(ctx, GetFrameworkEvents(ctx), "[PluginManager]");
                    }
                    else if (entry != null && entry.SettingsProvider != null)
                    {
                        bool providerWasRegisteredByThisPlugin = object.ReferenceEquals(ctx.Settings, entry.SettingsProvider);
                        SettingsProviderRegistration.SetContextSettings(ctx, entry.SettingsProvider);
                        if (providerWasRegisteredByThisPlugin && entry.SettingsProvider is ISettingsProvider2 && !(plugin is ModManagerBase))
                            SettingsProviderLifecycle.BindProviderSaveOnBeforeSave(ctx, GetFrameworkEvents(ctx), "[PluginManager]");
                    }

                    if (entry != null && entry.SettingsProvider == null)
                        TryRegisterDiscoveredSettingsProvider(entry, ctx, plugin);

                    MMLog.WriteDebug("Starting plugin: " + type.FullName);
                    MeasureStartupPhase("Plugin " + type.FullName + " Start", delegate { plugin.Start(ctx); });
                    ModRuntime.RecordSuccessfulLoad(entry);
                    ctx.Log.Info("Started.");
                }
                catch (Exception ex)
                {
                    MMLog.WritePluginError(type.FullName, "startup", ex);
                    MMLog.WriteError("error starting plugin '" + type.FullName + "': " + ex.Message);
                    _loadErrors++;
                }
            }
        }

        private static bool TryRegisterDiscoveredSettingsProvider(ModEntry entry, IPluginContext context, IModPlugin plugin)
        {
            object settingsObject;
            string sourceName;
            if (!SpineSettingsDiscovery.TryFindSettingsObject(plugin, out settingsObject, out sourceName))
                return false;

            try
            {
                SettingsController controller = new SettingsController(context, settingsObject, plugin);
                SettingsProviderLifecycle.RegisterFrameworkOwnedController(
                    entry,
                    context,
                    controller,
                    GetFrameworkEvents(context),
                    "auto-discovered " + sourceName,
                    "[PluginManager]");
                SettingsProviderLifecycle.BindProviderSaveOnBeforeSave(context, GetFrameworkEvents(context), "[PluginManager]");

                MMLog.WriteInfo("[PluginManager] Auto-registered Spine settings for " + entry.Id + " from " + plugin.GetType().Name + "." + sourceName + ".");
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[PluginManager] Auto-discovered settings registration failed for " + entry.Id + ": " + ex.Message);
                return false;
            }
        }

        private static void RegisterSettingsProvider(ModEntry entry, IPluginContext context, ISettingsProvider provider, string source)
        {
            if (entry == null || provider == null)
                return;

            SettingsProviderRegistration.Register(entry, context, provider);
            MMLog.WriteDebug("[PluginManager] Registered settings provider for " + entry.Id + " via " + source + ".");
        }

        private static bool IsPluginType(Type type)
        {
            if (type == null || !type.IsClass || type.IsAbstract)
                return false;

            try
            {
                return typeof(IModPlugin).IsAssignableFrom(type);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRuntimeApiCompatible(ModEntry entry, out string reason)
        {
            return RuntimeApiCompatibility.IsRuntimeApiCompatible(
                entry != null ? entry.About : null,
                TryGetRuntimeApiVersion,
                out reason);
        }

        private static bool TryGetRuntimeApiVersion(string apiName, out string version, out string failureReason)
        {
            version = null;
            failureReason = null;

            Assembly assembly = null;
            if (string.Equals(apiName, RuntimeApiCompatibility.ModApiName, StringComparison.OrdinalIgnoreCase))
                assembly = Assembly.GetExecutingAssembly();
            else
                assembly = FindLoadedRuntimeAssembly(apiName);

            if (assembly == null)
            {
                failureReason = "not loaded";
                return false;
            }

            string location = SafeAssemblyPath(assembly);
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                try
                {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(location);
                    if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                    {
                        version = versionInfo.FileVersion;
                        return true;
                    }

                    if (!string.IsNullOrEmpty(versionInfo.ProductVersion))
                    {
                        version = versionInfo.ProductVersion;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    failureReason = "unreadable: " + ex.Message;
                    return false;
                }
            }

            try
            {
                Version assemblyVersion = assembly.GetName().Version;
                if (assemblyVersion == null)
                {
                    failureReason = "missing";
                    return false;
                }

                version = assemblyVersion.ToString();
                return true;
            }
            catch (Exception ex)
            {
                failureReason = "unreadable: " + ex.Message;
                return false;
            }
        }

        private static Assembly FindLoadedRuntimeAssembly(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
                return null;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; assemblies != null && i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                    continue;

                try
                {
                    AssemblyName name = assembly.GetName();
                    if (name != null && string.Equals(name.Name, apiName, StringComparison.OrdinalIgnoreCase))
                        return assembly;
                }
                catch
                {
                }
            }

            return null;
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
        public void OnSessionStarted()
        {
            InvokeStaticLifecycleSubscribers("OnSessionStarted", OnSessionStartedEvent);
            for (int i = 0; i < _sessionEvents.Count; i++)
            {
                IModSessionEvents listener = _sessionEvents[i];
                InvokePluginLifecycleCallback("OnSessionStarted", listener, delegate { listener.OnSessionStarted(); });
            }
        }

        /// <summary>
        /// Handles New Game lifecycle fanout and reseeds session-scoped ModRandom state.
        /// </summary>
        public void OnNewGame()
        {
            SaveSystemImpl.ResetForNewGame();
            InvokeStaticLifecycleSubscribers("OnNewGame", OnNewGameEvent);
            // Initialize ModRandom for the new world
            ModRandom.Initialize(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            ModRandom.NotifySeedChanged();

            for (int i = 0; i < _sessionEvents.Count; i++)
            {
                IModSessionEvents listener = _sessionEvents[i];
                InvokePluginLifecycleCallback("OnNewGame", listener, delegate { listener.OnNewGame(); });
            }
        }

        private static void InvokeStaticLifecycleSubscribers(string eventName, Action subscribers)
        {
            SubscriberDispatch.Invoke(subscribers, eventName);
        }

        private static void InvokePluginLifecycleCallback(string eventName, object plugin, Action callback)
        {
            InvokeLifecycleCallback(eventName, SafePluginLabel(plugin), callback);
        }

        private static void InvokeLifecycleCallback(string eventName, string callbackOwner, Action callback)
        {
            if (callback == null)
                return;

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                string owner = string.IsNullOrEmpty(callbackOwner) ? "<unknown>" : callbackOwner;
                MMLog.WritePluginError(owner, eventName, ex);
            }
            finally
            {
                timer.Stop();
                if (timer.ElapsedMilliseconds >= 100)
                {
                    MMLog.WriteDebug(
                        "Lifecycle callback " + eventName + " for " +
                        (string.IsNullOrEmpty(callbackOwner) ? "<unknown>" : callbackOwner) +
                        " took " + timer.ElapsedMilliseconds + "ms.");
                }
            }
        }

        private static string SafePluginLabel(object plugin)
        {
            if (plugin == null)
                return "<unknown>";

            Type type = plugin.GetType();
            ModEntry entry;
            if (ModRegistry.TryGetModByAssembly(type.Assembly, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
                return entry.Id + " (" + type.FullName + ")";

            return type.FullName ?? type.Name;
        }

        private static string SafeDelegateLabel(Delegate callback)
        {
            if (callback == null)
                return "<unknown>";

            MethodInfo method = callback.Method;
            Type declaringType = method != null ? method.DeclaringType : null;
            Type ownerType = declaringType ?? (callback.Target != null ? callback.Target.GetType() : null);

            if (ownerType == null)
                return "<unknown>";

            ModEntry entry;
            if (ModRegistry.TryGetModByAssembly(ownerType.Assembly, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
                return entry.Id + " (" + ownerType.FullName + "." + (method != null ? method.Name : "<unknown>") + ")";

            return ownerType.FullName + "." + (method != null ? method.Name : "<unknown>");
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
            for (int i = _pluginContexts.Count - 1; i >= 0; i--)
            {
                PluginContextImpl context = _pluginContexts[i];
                if (context != null)
                    context.DisposeFrameworkEvents();
            }

            _pluginContexts.Clear();
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

            string modId = entry != null && !string.IsNullOrEmpty(entry.Id) ? entry.Id : (type.Namespace ?? type.Name);
            var log = new PrefixedLogger(modId);
            var gameHelper = ResolveGameHelper();
            var actors = ResolveActorSystem();
            if (entry != null && entry.About != null)
            {
                log.IsDebugEnabled = entry.About.debugLogging;
            }
            ISettingsProvider settings = null;
            // ModManagerBase now discovers settings. Keep the context empty for legacy plugins.

            PluginContextImpl context = new PluginContextImpl
            {
                LoaderRoot = _loaderRoot,
                PluginRoot = pluginRoot,
                Mod = entry,
                Settings = settings,
                Log = log,
                Game = gameHelper,
                Actors = actors,
                SaveSystem = SaveSystemImpl.GetForMod(modId),
                GameRoot = _gameRoot,
                ModsRoot = _modsRoot,
                Scheduler = (Action a) => EnqueueNextFrame(a)
            };

            _pluginContexts.Add(context);
            return context;
        }

        private static EventRegistry GetFrameworkEvents(IPluginContext context)
        {
            PluginContextImpl impl = context as PluginContextImpl;
            return impl != null ? impl.FrameworkEvents : null;
        }

        private static IGameHelper ResolveGameHelper()
        {
            if (ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.GameHelper))
                return ModAPIRegistry.GetAPI<IGameHelper>(GameRuntimeApiIds.GameHelper);

            return null;
        }

        private static IActorSystem ResolveActorSystem()
        {
            if (ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.Actors))
                return ModAPIRegistry.GetAPI<IActorSystem>(GameRuntimeApiIds.Actors);

            return null;
        }

        private static string SafeAssemblyPath(Assembly asm)
        {
            try { return asm != null ? asm.Location : null; }
            catch (Exception ex) { MMLog.WarnOnce("PluginManager.SafeAssemblyPath", "Error getting assembly path: " + ex.Message); return null; }
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

