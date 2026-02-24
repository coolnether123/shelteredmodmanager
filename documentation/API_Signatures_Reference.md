# ModAPI v1.2 API Signatures Reference

This is the source-of-truth signature sheet for the current code in this repo.

## Plugin Lifecycle (`ModAPI.Core`)

```csharp
public interface IModPlugin
{
    void Initialize(IPluginContext ctx);
    void Start(IPluginContext ctx);
}

public interface IModUpdate { void Update(); }
public interface IModShutdown { void Shutdown(); }
public interface IModSceneEvents
{
    void OnSceneLoaded(string sceneName);
    void OnSceneUnloaded(string sceneName);
}
public interface IModSessionEvents
{
    void OnSessionStarted();
    void OnNewGame();
}
```

## Plugin Context (`ModAPI.Core`)

```csharp
public interface IPluginContext
{
    GameObject LoaderRoot { get; }
    GameObject PluginRoot { get; }
    ModEntry Mod { get; }
    ISettingsProvider Settings { get; }
    IModLogger Log { get; }
    IGameHelper Game { get; }
    string GameRoot { get; }
    string ModsRoot { get; }
    bool IsModernUnity { get; }
    ISaveSystem SaveSystem { get; }

    void RunNextFrame(Action action);
    Coroutine StartCoroutine(IEnumerator routine);
    GameObject FindPanel(string nameOrPath);
    T AddComponentToPanel<T>(string nameOrPath) where T : Component;
}
```

## Save + Game Helpers (`ModAPI.Core`)

```csharp
public interface IGameHelper
{
    int GetTotalOwned(string itemId);
    int GetInventoryCount(string itemId);
    FamilyMember FindMember(string characterId);
}

public interface ISaveSystem
{
    string GetCurrentSlotPath();
    int ActiveSlotIndex { get; }
    void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class;
}
```

## Transpiler Core (`ModAPI.Harmony`)

```csharp
public static FluentTranspiler For(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod = null, ILGenerator generator = null);
public static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator, Action<FluentTranspiler> transformer);

public FluentTranspiler FindCall(Type type, string methodName, SearchMode mode = SearchMode.Start, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true);
public FluentTranspiler ReplaceWithCall(Type type, string methodName, Type[] parameterTypes = null);
public FluentTranspiler ReplaceSequence(int removeCount, params CodeInstruction[] newInstructions);
public FluentTranspiler ReplaceAll(IEnumerable<CodeInstruction> newInstructions);
public FluentTranspiler ReplaceAllCalls(Type sourceType, string sourceMethod, Type targetType, string targetMethod, Type[] targetParams = null);
public FluentTranspiler ReplaceAllPatterns(Func<CodeInstruction, bool>[] patternPredicates, CodeInstruction[] replaceWith, bool preserveInstructionCount = false);
public FluentTranspiler WithTransaction(Action<FluentTranspiler> action);
public IEnumerable<CodeInstruction> Build(bool strict = true, bool validateStack = true);
```

## Intent API (`ModAPI.Harmony`)

```csharp
public static FluentTranspiler RedirectCall(this FluentTranspiler t, Type originalType, string originalMethod, Type replacementType, string replacementMethod, SearchMode mode = SearchMode.Start);
public static FluentTranspiler RedirectCallAll(this FluentTranspiler t, Type originalType, string originalMethod, Type replacementType, string replacementMethod);
public static FluentTranspiler ChangeConstant(this FluentTranspiler t, float oldValue, float newValue, SearchMode mode = SearchMode.Start);
public static FluentTranspiler ChangeConstantAll(this FluentTranspiler t, float oldValue, float newValue);
public static FluentTranspiler ChangeConstant(this FluentTranspiler t, int oldValue, int newValue, SearchMode mode = SearchMode.Start);
public static FluentTranspiler ChangeConstantAll(this FluentTranspiler t, int oldValue, int newValue);
public static FluentTranspiler RemoveCall(this FluentTranspiler t, Type type, string methodName, SearchMode mode = SearchMode.Start);
public static FluentTranspiler InjectBeforeCall(this FluentTranspiler t, Type targetType, string targetMethod, Type hookType, string hookMethod, SearchMode mode = SearchMode.Start);
```

## Cooperative Patching (`ModAPI.Harmony`)

```csharp
public static void RegisterTranspiler(MethodBase target, string anchorId, PatchPriority priority, Func<FluentTranspiler, FluentTranspiler> patchLogic, string[] dependsOn = null, string[] conflictsWith = null);
public static bool UnregisterTranspiler(MethodBase target, string anchorId, string ownerMod = null);
public static void UnregisterAll(string ownerMod = null);
public static IEnumerable<CodeInstruction> RunPipeline(MethodBase original, IEnumerable<CodeInstruction> instructions);
```

## Debugging (`ModAPI.Harmony`)

```csharp
public static IEnumerable<CodeInstruction> DumpWithDiff(string label, IEnumerable<CodeInstruction> before, IEnumerable<CodeInstruction> after, string modId = null, bool force = false, MethodBase originalMethod = null);
public static string ExplainOpCode(string opCodeName);
public static void RecordSnapshot(string modId, string stepName, IEnumerable<CodeInstruction> before, IEnumerable<CodeInstruction> after, double durationMs = 0, int warningsCount = 0, MethodBase method = null, string patchOrigin = null, IEnumerable<PatchEdit> patchEdits = null, IEnumerable<string> warnings = null);
```

## Content + Assets (`ModAPI.Content`)

```csharp
public static RegistrationResult RegisterItem(ItemDefinition def);
public static RegistrationResult RegisterItemWithFixedId(string modId, string itemId, ItemDefinition def);
public static void PatchItem(ItemPatch patch);
public static void RegisterRecipe(RecipeDefinition def);
public static void RegisterCookingRecipe(CookingRecipe recipe);
public static void PatchRecipe(RecipePatch patch);
public static void AddLoot(LootEntry entry);

public static Texture2D LoadTexture(Assembly asm, string relativePath);
public static Texture2D LoadTexture(string modRootPath, string relativePath);
public static Sprite LoadSprite(Assembly asm, string relativePath, float pixelsPerUnit = 100f);
public static Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit = 100f);
public static AssetBundle LoadBundle(Assembly asm, string relativePath);
public static AssetBundle LoadBundle(string modRootPath, string relativePath);
public static GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath);
```

## Event + Registry APIs

```csharp
// ModAPI.Events.GameEvents
public static event Action<int> OnNewDay;
public static event Action<SaveData> OnBeforeSave;
public static event Action<SaveData> OnAfterLoad;
public static event Action OnNewGame;
public static event Action OnSessionStarted;
public static event Action<EncounterCharacter, EncounterCharacter> OnCombatStarted;
public static event Action<ExplorationParty> OnPartyReturned;
public static event Action<TimeTriggerBatch> OnSixHourTick;
public static event Action<TimeTriggerBatch> OnStaggeredTick;

// ModAPI.Events.UIEvents
public static event Action<BasePanel> OnPanelOpened;
public static event Action<BasePanel> OnPanelClosed;
public static event Action<BasePanel> OnPanelResumed;
public static event Action<BasePanel> OnPanelPaused;
public static event Action<GameObject, string> OnButtonClicked;

// ModAPI.Events.ModEventBus
public static void Publish<T>(string eventName, T data);
public static void Subscribe<T>(string eventName, Action<T> handler);
public static void Unsubscribe<T>(string eventName, Action<T> handler);
public static bool HasSubscribers(string eventName);
public static int GetSubscriberCount(string eventName);
public static Dictionary<string, int> GetEventDiagnostics();

// ModAPI.Core.ModAPIRegistry
public static bool RegisterAPI<T>(string apiName, T implementation, string providerModId = null) where T : class;
public static T GetAPI<T>(string apiName) where T : class;
public static bool TryGetAPI<T>(string apiName, out T api) where T : class;
public static bool IsAPIRegistered(string apiName);
public static bool UnregisterAPI(string apiName, string providerModId = null);
public static List<string> GetRegisteredAPIs();

// ModAPI.Core.ModRegistry
public static bool Find(string modId);
public static ModEntry GetMod(string modId);
public static bool TryGetMod(string modId, out ModEntry entry);
public static List<string> GetLoadedModIds();
```

## Persistent Data (`ModAPI.Util`)

```csharp
public static void SaveData<T>(this IPluginContext ctx, string key, T data);
public static bool LoadData<T>(this IPluginContext ctx, string key, out T value);
```

Note: these are extension methods on `IPluginContext` (`ctx.SaveData(...)`, `ctx.LoadData(...)`).
