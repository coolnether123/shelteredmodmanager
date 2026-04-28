# ModAPI + ShelteredAPI v1.3 API Signatures Reference

This is the source-of-truth signature sheet for the current code in this repo.

Related usage guide:
- `documentation/ShelteredAPI_Characters_Guide.md`

## Compatibility Matrix

| Surface | Assembly | Status |
|---------|----------|--------|
| Core loader/plugin/settings APIs and neutral runtime ports | `ModAPI.dll` | Current |
| Neutral event bus (`ModEventBus`) | `ModAPI.dll` | Current |
| Sheltered event/helper APIs used by v1.2 mods (`GameEvents`, `GameTimeTriggerHelper`, `UIEvents`, `FactionEvents`, `PartyHelper`, `InteractionRegistry`) | `ShelteredAPI.dll` | Current 1.3 migration aliases using old `ModAPI.*` namespaces |
| Sheltered save compatibility helpers (`GameUtil`, `PersistentDataAPI`, `ModList`, `ModDictionary`, custom-save APIs) | `ShelteredAPI.dll` | Current 1.3 migration aliases using old `ModAPI.*` namespaces |
| Sheltered UI/content compatibility helpers (`InventoryHelper`, `UIHooks`, `ContextMenuHelper`, `ModUIHooks`, `ModSettingsPanel`, NGUI helpers, Spine settings UI renderers) | `ShelteredAPI.dll` | Current 1.3 migration aliases using old `ModAPI.*` namespaces |
| `IGameHelper` adapters and Sheltered-specific implementations | `ShelteredAPI.dll` | Current |
| Old v1.2 docs/snippets with conflicting signatures | mixed | Deprecated |

## v1.2 Compatibility (1.3 Line)

The v1.2 mod ecosystem was built against key gameplay helper/event types in `ModAPI.*` namespaces.
For `1.3`, Sheltered-backed event, party, interaction, and manager-state helpers are hosted by `ShelteredAPI.dll`.
Some types intentionally keep old `ModAPI.*` namespaces as source migration aliases, but mod authors must reference `ShelteredAPI.dll` when using those Sheltered hooks.

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
    object FindCharacter(string characterId);
}

public interface ISaveSystem
{
    string GetCurrentSlotPath();
    int ActiveSlotIndex { get; }
    void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class;
}

public interface IContentResolutionService
{
    bool TryResolveRuntimeItemKey(string itemId, out object runtimeItemKey);
    IEnumerable<object> GetRegisteredRuntimeItemKeys();
}

public interface IModSaveContext
{
    string SlotPath { get; }
    int SlotIndex { get; }
    string SaveScopeId { get; }
    string SaveId { get; }
    object HostSaveDescriptor { get; }
}

public interface ISaveRuntimeAdapter
{
    string GetCurrentSlotPath();
    int ActiveSlotIndex { get; }
    IModSaveContext GetCurrentSaveContext();
    void EnsureRuntimeReady();
    void ResetRuntimeState();
    string GetQuitHeartbeatDetail();
}

public static class GameRuntimeApiIds
{
    public const string ContentResolution = "GameRuntime.ContentResolution";
    public const string SaveRuntime = "GameRuntime.SaveRuntime";
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

public interface IActorSystem :
    IActorRegistry,
    IActorComponentStore,
    IActorBindingStore,
    IActorEvents,
    IActorAdapterRegistry,
    IActorSimulationScheduler,
    IActorSerializationService {}

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
    ActorComponentWriteResult Set<TComponent>(ActorId actorId, TComponent component, string sourceModId)
        where TComponent : class, IActorComponent;
    bool TryGet<TComponent>(ActorId actorId, out TComponent component)
        where TComponent : class, IActorComponent;
    bool TryGet(ActorId actorId, string componentId, out IActorComponent component);
    IActorComponent GetByComponentId(ActorId actorId, string componentId);
    bool HasComponent(ActorId actorId, string componentId);
    bool Remove(ActorId actorId, string componentId, string sourceModId);
    IReadOnlyList<IActorComponent> GetAllComponents(ActorId actorId);
    IReadOnlyList<string> GetComponentIds(ActorId actorId);
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

public interface IActorSimulationSystem
{
    string SystemId { get; }
    int Priority { get; }
    void Tick(ActorSimulationContext context, int tickStep);
}

public sealed class ActorSimulationContext
{
    public IActorRegistry Registry { get; }
    public IActorComponentStore Components { get; }
    public IActorEvents Events { get; }
    public ModRandomStream Random { get; }
    public long CurrentTick { get; }
}

public interface IActorEvents
{
    event Action<ActorEventEnvelope> EventPublished;

    IDisposable Subscribe(Action<ActorEventEnvelope> handler);
    IDisposable Subscribe(Predicate<ActorEventEnvelope> filter, Action<ActorEventEnvelope> handler);
    IReadOnlyList<ActorEventEnvelope> GetRecentEvents();
}

public interface IActorSimulationScheduler
{
    long CurrentTick { get; }

    void RegisterSystem(IActorSimulationSystem system);
    bool UnregisterSystem(string systemId);
    IReadOnlyList<IActorSimulationSystem> GetSystems();
    void Tick(int tickStep, string streamName);
}

public interface IActorSerializationService
{
    int CurrentSchemaVersion { get; }

    void RegisterSerializer(IActorComponentSerializer serializer);
    bool TryGetSerializer(string componentId, out IActorComponentSerializer serializer);
    string ExportJson();
    bool ImportJson(string json);
}
```

Built-in actor API registration names used by `ModAPI`:
- `GameRuntime.Actors`
- `GameRuntime.ActorRegistry`
- `GameRuntime.ActorComponents`
- `GameRuntime.ActorBindings`
- `GameRuntime.ActorAdapters`
- `GameRuntime.ActorSimulation`
- `GameRuntime.ActorEvents`
- `GameRuntime.ActorSerialization`

ShelteredAPI also registers `ShelteredAPI.*` aliases for 1.3 source migration.

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

## Content + Assets (`ShelteredAPI.Content`)

Note on type collisions:
- Prefer aliasing `ShelteredAPI.Content.ItemDefinition` in mod code:
  `using ContentItemDefinition = ShelteredAPI.Content.ItemDefinition;`

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

Sheltered inventory helper compatibility API:

```csharp
// ShelteredAPI-owned 1.3 source alias: ModAPI.Items.InventoryHelper
public static bool ResolveItemType(string itemId, out ItemManager.ItemType type);
public static ItemInstance CreateItem(string itemId);
public static bool TryAddToInventory(ItemInstance item);
public static bool TryAddToInventory(string itemId, int quantity = 1);
public static bool TryRemoveFromInventory(string itemId, int quantity = 1);
public static int GetItemCount(string itemId, bool includeParties = false);
public static ReadOnlyCollection<ItemStack> GetAllItems();
public static int GetStorageCapacity();
public static int GetUsedStorage();
```

Sheltered UI compatibility APIs:

```csharp
// ShelteredAPI-owned 1.3 source aliases under ModAPI.UI / ModAPI.Hooks
public static void ModUIHooks.RegisterButton(TargetMenu menu, string buttonText, Action onClick);
public static void ContextMenuHelper.RegisterAddon(string optionName, string displayText, Action onSelected, Func<Obj_Base, bool> predicate = null);
public static GameObject UIHooks.GetUIRoot();
public static GameObject UIHooks.GetExpeditionMapPanel();
public static GameObject UIHooks.GetHUD();
public static GameObject UIHooks.GetRadioPanel();
public static GameObject UIHooks.GetActivePanel();
public static Camera UIHooks.GetMapCamera();
```

`ItemDefinition` fluent localization APIs (ShelteredAPI v1.3):

```csharp
public ItemDefinition WithDisplayName(string name);           // legacy key-or-text auto-detection
public ItemDefinition WithDescription(string desc);           // legacy key-or-text auto-detection
public ItemDefinition WithDisplayNameKey(string key);         // explicit key
public ItemDefinition WithDescriptionKey(string key);         // explicit key
public ItemDefinition WithDisplayNameText(string text);       // explicit literal text
public ItemDefinition WithDescriptionText(string text);       // explicit literal text
```

Localization behavior for content injection (ShelteredAPI v1.3):
- `m_NameLocalizationKey` / `m_DescLocalizationKey` are always set to keys (never raw text).
- For `...Text(...)`, ShelteredAPI auto-generates keys like `shelteredapi.<modid>.<itemid>.name|desc` and registers values in its custom table.
- Legacy `WithDisplayName/WithDescription` values are interpreted as `key` if they look like keys (`.` and no spaces), otherwise as literal text.
- ShelteredAPI patches `Localization.Get(string,bool)` and returns custom-table values directly (preserving original case for literal text).
- Injector logs localization mode diagnostics per item (`name=key|text`, `desc=key|text`, final keys).

## Event + Registry APIs

```csharp
// ShelteredAPI-owned 1.3 source alias: ModAPI.Events.GameEvents
public static event Action<int> OnNewDay;
public static event Action<SaveData> OnBeforeSave;
public static event Action<SaveData> OnAfterLoad;
public static event Action OnNewGame;
public static event Action OnSessionStarted;
public static event Action<EncounterCharacter, EncounterCharacter> OnCombatStarted;
public static event Action<ExplorationParty> OnPartyReturned;
public static event Action<TimeTriggerBatch> OnSixHourTick;
public static event Action<TimeTriggerBatch> OnStaggeredTick;

// ShelteredAPI-owned 1.3 source alias: ModAPI.Events.UIEvents
public static event Action<BasePanel> OnPanelOpened;
public static event Action<BasePanel> OnPanelClosed;
public static event Action<BasePanel> OnPanelResumed;
public static event Action<BasePanel> OnPanelPaused;
public static event Action<GameObject, string> OnButtonClicked;

// ModAPI.Events.ModEventBus, neutral and hosted by ModAPI.dll
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

// ModAPI.Core neutral runtime ports, implemented by ShelteredAPI at runtime
public interface IGameLifecycleSource
{
    event Action<object> BeforeSave;
    event Action<object> BeforeLoadSceneContents;
    event Action<object> AfterLoad;
    event Action SessionStarted;
    event Action NewGame;
}

public interface IUiLifecycleEventSink
{
    void RaisePanelOpened(object panel);
    void RaisePanelClosed(object panel);
    void RaisePanelResumed(object panel);
    void RaisePanelPaused(object panel);
    void RaiseButtonClicked(object button, string buttonName);
}

public interface ISaveRuntimeAdapter
{
    string GetCurrentSlotPath();
    int ActiveSlotIndex { get; }
    IModSaveContext GetCurrentSaveContext();
    void EnsureRuntimeReady();
    void ResetRuntimeState();
    string GetQuitHeartbeatDetail();
}

// ModAPI.Scenarios neutral scenario registration/lifecycle contracts
public static class GameRuntimeApiIds
{
    public const string SaveRuntime = "GameRuntime.SaveRuntime";
    public const string CustomScenarios = "GameRuntime.CustomScenarios";
}

public interface ICustomScenarioService
{
    event Action<CustomScenarioEventArgs> ScenarioRegistered;
    event Action<CustomScenarioEventArgs> ScenarioUnregistered;
    event Action<CustomScenarioEventArgs> ScenarioSelected;
    event Action<CustomScenarioEventArgs> ScenarioSpawned;
    event Action<CustomScenarioEventArgs> StateChanged;

    CustomScenarioState CurrentState { get; }
    CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration);
    bool Unregister(string scenarioId);
    bool TryGet(string scenarioId, out CustomScenarioInfo scenario);
    CustomScenarioInfo[] List();
    bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage);
}

public class CustomScenarioRegistration
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public int Order { get; set; }
    public string OwnerModId { get; set; }
    public Assembly OwnerAssembly { get; set; }
    public ScenarioModDependency[] RequiredMods { get; set; }
    public object Definition { get; set; }
    public CustomScenarioDefinitionFactory DefinitionFactory { get; set; }
    public Action<CustomScenarioEventArgs> OnSelected { get; set; }
    public Action<CustomScenarioEventArgs> OnSpawned { get; set; }
    public object UserData { get; set; }
}

public sealed class ScenarioInfo
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Author { get; }
    public string Version { get; }
    public string FilePath { get; }
    public string OwnerModId { get; }
}

public static class ScenarioDependencyManifest
{
    public static ScenarioDependencyManifestData Create(string scenarioName, ScenarioModDependency[] requiredMods);
    public static ScenarioModDependency[] FromDependencyStrings(IList<string> dependencies);
    public static ScenarioModDependency ParseDependency(string dependency);
    public static ScenarioModDependency[] Merge(ScenarioModDependency[] first, ScenarioModDependency[] second);
    public static ScenarioModDependency[] CloneRequiredMods(ScenarioModDependency[] requiredMods);
}

public sealed class ScenarioValidationResult
{
    public bool IsValid { get; }
    public ScenarioValidationIssue[] Issues { get; }
    public void AddError(string message);
    public void AddWarning(string message);
}

// ShelteredAPI.Scenarios Sheltered scenario authoring/runtime pack
public interface IShelteredCustomScenario
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    string Version { get; }
    int Order { get; }
    object UserData { get; }
    ScenarioDef BuildDefinition(CustomScenarioBuildContext context);
    void OnSelected(CustomScenarioEventArgs args);
    void OnSpawned(CustomScenarioEventArgs args);
}

public abstract class ShelteredCustomScenarioBase : IShelteredCustomScenario
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string Description { get; }
    public virtual string Version { get; }
    public virtual int Order { get; }
    public virtual object UserData { get; }
    public abstract ScenarioDef BuildDefinition(CustomScenarioBuildContext context);
    public virtual void OnSelected(CustomScenarioEventArgs args);
    public virtual void OnSpawned(CustomScenarioEventArgs args);
    public CustomScenarioRegistration ToRegistration();
    public CustomScenarioRegistrationResult Register();
}

public class ScenarioDefinition
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public FamilySetupDefinition FamilySetup { get; set; }
    public StartingInventoryDefinition StartingInventory { get; set; }
    public BunkerEditsDefinition BunkerEdits { get; set; }
    public AssetReferencesDefinition AssetReferences { get; set; }
}

public class ScenarioDefinitionSerializer
{
    public const string DefaultFileName = "scenario.xml";
    public ScenarioDefinition Load(string filePath);
    public ScenarioDefinition FromXml(string xml);
    public void Save(ScenarioDefinition definition, string filePath);
    public string ToXml(ScenarioDefinition definition);
    public ScenarioInfo LoadInfo(string filePath, string ownerModId);
}

public sealed class ScenarioValidator
{
    public ScenarioValidator();
    public ScenarioValidator(IScenarioDependencyResolver dependencyResolver);
    public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath);
}

public interface IScenarioDependencyResolver
{
    bool IsLoaded(string modId);
}

public interface IScenarioDependencyVersionResolver : IScenarioDependencyResolver
{
    string GetLoadedVersion(string modId);
}

public static class ScenarioFrameworkVerification
{
    public static ScenarioValidationResult Run();
}

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
    public static FamilyMember FindFamilyMember(this IGameHelper helper, string characterId);
    public static bool IsAwayOnExpedition(this IGameHelper helper, FamilyMember member);
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

## Persistent Data (`ModAPI.Core`, ShelteredAPI)

```csharp
// ModAPI.dll
public interface ISaveSystem
{
    string GetCurrentSlotPath();
    int ActiveSlotIndex { get; }
    void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class;
}

// ShelteredAPI.dll, namespace retained for 1.3 source migration
public static void SaveData<T>(this IPluginContext ctx, string key, T data);
public static bool LoadData<T>(this IPluginContext ctx, string key, out T value);
```

Note: these are extension methods on `IPluginContext` (`ctx.SaveData(...)`, `ctx.LoadData(...)`).
