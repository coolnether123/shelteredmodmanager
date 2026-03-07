# ModAPI + ShelteredAPI v1.3 API Signatures Reference

This is the source-of-truth signature sheet for the current code in this repo.

Related usage guide:
- `documentation/ShelteredAPI_Characters_Guide.md`

## Compatibility Matrix

| Surface | Assembly | Status |
|---------|----------|--------|
| Core loader/plugin/content/settings APIs | `ModAPI.dll` | Current |
| Backward-compat event/helper APIs used by v1.2 mods (`GameEvents`, `GameTimeTriggerHelper`, `UIEvents`, `FactionEvents`, `PartyHelper`, `InteractionRegistry`, `GameUtil`, `PersistentDataAPI`) | `ModAPI.dll` | Current (Deprecated for future major) |
| `IGameHelper` adapters and Sheltered-specific implementations | `ShelteredAPI.dll` | Current |
| Old v1.2 docs/snippets with conflicting signatures | mixed | Deprecated |

## v1.2 Compatibility (1.3 Line)

The v1.2 mod ecosystem was built against key gameplay helper/event types in `ModAPI.dll`.
For `1.3`, those surfaces remain in `ModAPI.dll` to preserve backward compatibility.
They are marked `[Obsolete(..., false)]` to signal migration planning without breaking builds.

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
    IActorSystem Actors { get; }
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

## Actor System (`ModAPI.Actors`, ShelteredAPI)

```csharp
public enum ActorKind { Player, Faction, Citizen, Visitor, NeutralShelter, Synthetic, Custom }
public enum ActorLifecycleState { Unknown, Registered, Active, Inactive, Unloaded, Destroyed }
public enum ActorPresenceState { Unknown, InShelter, Expedition, Encounter, Offscreen }
[Flags] public enum ActorFlags { None = 0, Persistent = 1, RuntimeOnly = 2, Synthetic = 4, Loaded = 8 }

public sealed class ActorId
{
    public ActorKind Kind;
    public int LocalId;
    public string Domain;
}

public interface IActorSystem : IActorRegistry, IActorComponentStore, IActorEvents, IActorSimulationScheduler, IActorSerializationService {}

public interface IActorRegistry
{
    IActorRecord Get(ActorId id);
    bool TryGet(ActorId id, out IActorRecord actor);
    IActorRecord Create(ActorCreateRequest request);
    IActorRecord Ensure(ActorCreateRequest request);
    bool Update(ActorId id, ActorRecordMutation mutation);
    bool Destroy(ActorId id, ActorDestroyReason reason);
    IReadOnlyList<IActorRecord> Enumerate(ActorQuery query);
    ActorQueryBuilder Query();
}

public interface IActorComponentStore
{
    ActorComponentWriteResult Set(ActorId actorId, IActorComponent component, string sourceModId);
    bool TryGet<TComponent>(ActorId actorId, out TComponent component) where TComponent : class, IActorComponent;
    bool Remove(ActorId actorId, string componentId, string sourceModId);
}

public sealed class ActorBinding
{
    public string BindingType;
    public string BindingKey;
    public string SourceModId;
    public bool Persistent;
}

public interface IActorBindingStore
{
    bool Bind(ActorId actorId, ActorBinding binding, bool replaceExisting);
    bool Unbind(string bindingType, string bindingKey);
    bool TryResolve(string bindingType, string bindingKey, out ActorId actorId);
    IReadOnlyList<ActorBinding> GetBindings(ActorId actorId);
    IReadOnlyList<ActorId> GetBoundActors(string bindingType);
}

public interface IActorAdapter
{
    string AdapterId { get; }
    int Priority { get; }
    void Synchronize(IActorSystem actors, long currentTick);
}

public interface IActorAdapterRegistry
{
    void RegisterAdapter(IActorAdapter adapter);
    bool UnregisterAdapter(string adapterId);
    IReadOnlyList<IActorAdapter> GetAdapters();
}
```

## Spine Settings (`ModAPI.Spine`, `ModAPI.Attributes`)

```csharp
// ModAPI.Attributes
[AttributeUsage(AttributeTargets.Class)]
public class ModConfigurationAttribute : Attribute
{
    public string Title { get; set; }
    public ModConfigurationAttribute(string title = null);
}

// ModAPI.Spine
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public class ModSettingAttribute : Attribute
{
    public string Label;
    public string Tooltip;
    public SettingMode Mode; // default: Advanced
    public float MinValue;
    public float MaxValue;
    public float StepSize;
    public string Category;
    public string DependsOnId;
    public bool ControlsChildVisibility;
    public string VisibilityMethod;
    public string OptionsSource;
    public string ValidateMethod;
    public string OnChanged;
}

public interface ISettingsProvider
{
    IEnumerable<SettingDefinition> GetSettings();
    object GetSettingsObject();
    void OnSettingsLoaded();
    void ResetToDefaults();
}

public static class SpineSettingsHelper
{
    public static List<SettingDefinition> Scan(object settingsObject);
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

Note on type collisions:
- Prefer aliasing `ModAPI.Content.ItemDefinition` in mod code:
  `using ContentItemDefinition = ModAPI.Content.ItemDefinition;`

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

`ItemDefinition` fluent localization APIs (ModAPI v1.3):

```csharp
public ItemDefinition WithDisplayName(string name);           // legacy key-or-text auto-detection
public ItemDefinition WithDescription(string desc);           // legacy key-or-text auto-detection
public ItemDefinition WithDisplayNameKey(string key);         // explicit key
public ItemDefinition WithDescriptionKey(string key);         // explicit key
public ItemDefinition WithDisplayNameText(string text);       // explicit literal text
public ItemDefinition WithDescriptionText(string text);       // explicit literal text
```

Localization behavior for content injection (ModAPI v1.3):
- `m_NameLocalizationKey` / `m_DescLocalizationKey` are always set to keys (never raw text).
- For `...Text(...)`, ModAPI auto-generates keys like `modapi.<modid>.<itemid>.name|desc` and registers values in its custom table.
- Legacy `WithDisplayName/WithDescription` values are interpreted as `key` if they look like keys (`.` and no spaces), otherwise as literal text.
- ModAPI patches `Localization.Get(string,bool)` and returns custom-table values directly (preserving original case for literal text).
- Injector logs localization mode diagnostics per item (`name=key|text`, `desc=key|text`, final keys).

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

## ShelteredAPI Trigger Scheduler (`ModAPI.Events`)

```csharp
public enum TimeTriggerCadence { SixHour = 1, Staggered = 2, Both = 3 }
public enum TimeTriggerKind { SixHour = 1, Staggered = 2 }

public static class GameTimeTriggerHelper
{
    public static event Action<TimeTriggerBatch> OnSixHourTick;
    public static event Action<TimeTriggerBatch> OnStaggeredTick;

    public static int StaggeredMinHours { get; }
    public static int StaggeredMaxHours { get; }

    public static void RegisterTrigger(string triggerId);
    public static void RegisterTrigger(string triggerId, int priority);
    public static void RegisterTrigger(string triggerId, int priority, TimeTriggerCadence cadence);
    public static void RegisterTrigger(string triggerId, int priority, TimeTriggerCadence cadence, Action<TimeTriggerBatch> callback);
    public static bool UnregisterTrigger(string triggerId);
    public static List<TimeTriggerInfo> GetPriorityList(TimeTriggerCadence cadence);
    public static void ConfigureStaggeredRange(int minInclusive, int maxInclusive);
}
```

## ShelteredAPI `IGameHelper` Adapter Extension (`ShelteredAPI.Adapters`)

```csharp
public static class GameHelperExtensions
{
    public static int GetTotalOwned(this IGameHelper helper, ItemManager.ItemType itemType);
}
```

## Background Processing (v1.3)

```csharp
// ModAPI.Core.ModThreads
public static void RunAsync(Action action);
public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread);
public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError);

// ModAPI.Core.ModManagerBase
protected void RunInBackground<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError = null);
```

## Persistent Data (`ModAPI.Util`)

```csharp
public static void SaveData<T>(this IPluginContext ctx, string key, T data);
public static bool LoadData<T>(this IPluginContext ctx, string key, out T value);
```

Note: these are extension methods on `IPluginContext` (`ctx.SaveData(...)`, `ctx.LoadData(...)`).
