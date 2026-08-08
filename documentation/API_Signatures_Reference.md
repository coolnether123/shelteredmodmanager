# ModAPI + ShelteredAPI v2.0 API Signatures Reference

This is the signature lookup sheet for public APIs present in the current repo. The 2.0 line is a breaking clean API line.

Use this file for type names and method shapes only. For setup, assembly selection, stability rules, and task workflows, start with the [Documentation Index](README.md) and its canonical [assembly boundary](README.md#assembly-boundary-canonical).

## Manager runtime options

```csharp
namespace ModAPI.Core
{
    public static class ManagerBooleanOptions
    {
        public static void RegisterBooleanOption(ManagerBooleanOptionDefinition definition);
        public static bool GetBool(string id, bool fallback);
        public static void SetBool(string id, bool value);
    }

    public sealed class ManagerBooleanOptionDefinition
    {
        public string Id;
        public string Owner;
        public string Label;
        public string Description;
        public bool DefaultValue;
        public bool RequiresRestart;
        public int SortOrder;
    }
}
```

The persisted JSON file and record DTOs are internal. Mods use `ManagerBooleanOptions`; they do not edit
`manager_options.json` directly.

## Signature Map

| Area | Section |
|------|---------|
| Plugin lifecycle and context | [Plugin Lifecycle](#plugin-lifecycle-modapicore), [Plugin Context](#plugin-context-modapicore) |
| Save and game runtime services | [Save + Game Helpers](#save--game-helpers-modapicore), [Persistence And Sheltered Saves](#persistence-and-sheltered-saves-modapicore-shelteredapisaves) |
| Input actions and Sheltered controls | [Input Actions](#input-actions-modapiinputactions), [Sheltered Input Facade](#sheltered-input-facade-shelteredapiinput) |
| Actors and characters | [Actor System](#actor-system-modapiactors-shelteredapi), [Sheltered Actors And Characters](#sheltered-actors-and-characters-shelteredapiactors-shelteredapicharacters) |
| Settings UI | [Spine Settings](#spine-settings-modapispine-modapiattributes) |
| Harmony and transpilers | [Patch Diagnostics](#patch-diagnostics-modapiharmony), [Transpiler Core](#transpiler-core-modapiharmony), [Intent API](#intent-api-modapiharmony), [Cooperative Patching](#cooperative-patching-modapiharmony) |
| Content and assets | [Content + Assets](#content--assets-shelteredapicontent), [Runtime UI + Stores](#runtime-ui--stores-shelteredapiuiruntime-shelteredapistorage-shelteredapiworkstations) |
| Events and registries | [Event + Registry APIs](#event--registry-apis), [ShelteredAPI Trigger Scheduler](#shelteredapi-trigger-scheduler-shelteredapievents), [Mod Registry](#mod-registry-modapicore) |
| Custom scenarios | [Custom Scenarios](#custom-scenarios-modapiscenarios-shelteredapiscenarios) |
| Save lifecycle expansion | [Save Lifecycle](#save-lifecycle-smm-20) |
| Deterministic random streams | [ModRandom Deterministic Streams](#modrandom-deterministic-streams-modapicore) |
| Expedition map context | [Expedition Map Context](#expedition-map-context-smm-20) |
| Home-shelter placement providers | [Expedition Map Context](#expedition-map-context-smm-20) |
| Player queues | [Player Queues](#player-queues-smm-20) |
| Background work | [Background Work](#background-work-smm-20) |
| UI extension service | [UI Extensions](#ui-extensions-smm-20) |
| Patch reports and conflicts | [Patch Diagnostics](#patch-diagnostics-modapiharmony) |
| Expedition markers | [Map Markers](#map-markers-smm-20) |
| Compatibility exports | [Save Manifest / Support Bundle](#save-manifest--support-bundle-smm-20) |
| Documentation contract | [Documentation Model](#documentation-model-smm-20) |

> Status note: runtime UI/store/cooking signatures are preview surfaces. Custom scenario registration, XML/code authoring, playback, runtime bindings, and scoring snapshots are supported ShelteredAPI 2.0 surfaces. The advanced in-game editor is a separate optional preview assembly and is not a mod API dependency. See the linked task guides before publishing against preview areas.
- Shared SMM 2.0 naming and DTO rules are defined in [ShelteredAPI Guide: Shared Facade Conventions](ShelteredAPI_Guide.md#shared-facade-conventions).

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

## Input Actions (`ModAPI.InputActions`)

```csharp
public class ModInputAction
{
    public string Id { get; }
    public string Label { get; }
    public string Category { get; }
    public string Description { get; }
    public InputBinding DefaultBinding { get; }

    public ModInputAction(string id, string label, string category, InputBinding defaultBinding, string description = null);
}

public struct InputBinding
{
    public KeyCode Primary;
    public KeyCode Secondary;

    public InputBinding(KeyCode primary, KeyCode secondary);
    public bool IsUnbound { get; }
    public bool ContainsKey(KeyCode key);
    public bool Overlaps(InputBinding other);
    public bool IsDown();
    public bool IsHeld();
    public bool IsUp();
}

public static class InputActionRegistry
{
    public static event Action<ModInputAction> OnActionRegistered;
    public static event Action<string, InputBinding> OnBindingChanged;

    public static bool Register(ModInputAction action);
    public static bool IsRegistered(string actionId);
    public static List<ModInputAction> GetAllActions();
    public static bool TryGetAction(string actionId, out ModInputAction action);
    public static InputBinding GetBinding(string actionId);
    public static bool TryGetBinding(string actionId, out InputBinding binding);
    public static bool SetBinding(string actionId, InputBinding binding);
    public static bool ResetBinding(string actionId);
    public static void ResetAllBindings();
    public static List<ModInputAction> FindConflicts(string actionId, InputBinding candidate);
    public static bool IsDown(string actionId);
    public static bool IsHeld(string actionId);
    public static bool IsUp(string actionId);
}
```

## Sheltered Input Facade (`ShelteredAPI.Input`)

```csharp
public enum InputContext { Unknown = 0, Gameplay = 1, Menu = 2, System = 3 }

public static class ShelteredInputActions
{
    public const string IdPrefix = "sheltered.";
    public static bool IsShelteredAction(string actionId);
}

public static class ShelteredInput
{
    public static void EnsureReady();
    public static void RegisterVanillaActions();
    public static bool IsShelteredAction(string actionId);
    public static InputContext GetContextForActionId(string actionId);

    public static float ZoomSpeed { get; set; }
    public static float TouchpadMovementSpeed { get; set; }
    public static float MouseScrollSpeed { get; set; }
    public static float NormalizeSpeedScale(float value, float fallback);

    public static float DefaultZoomSpeed { get; }
    public static float DefaultTouchpadMovementSpeed { get; }
    public static float DefaultMouseScrollSpeed { get; }
    public static float MinSpeedScale { get; }
    public static float MaxSpeedScale { get; }
    public static float SpeedStep { get; }
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

ShelteredAPI supplies the default runtime implementation and Sheltered-facing facade helpers.

## Sheltered Actors And Characters (`ShelteredAPI.Actors`, `ShelteredAPI.Characters`)

```csharp
public static class ShelteredActors
{
    public static IActorSystem Instance { get; }
    public static ActorId FamilyMemberActorId(int uniqueMemberId);
    public static ActorId VisitorActorId(int uniqueVisitorId);
    public static ActorId SyntheticCharacterActorId(int uniqueCharacterId, string sourceModId);
    public static bool TryGetCharacter(ActorId actorId, out ICharacterProxy character);
}

public static class ShelteredCharacters
{
    public static event Action<ICharacterProxy, EffectInstance> EffectApplied;
    public static event Action<ICharacterProxy, EffectInstance, RemovalReason> EffectRemoved;
    public static event Action<ICharacterProxy, string, object> DataChanged;
    public static event Action<ICharacterProxy> SyntheticCharacterCreated;
    public static event Action<ICharacterProxy> SyntheticCharacterUnloaded;

    public static void RegisterEffectType<T>(string effectId) where T : ICharacterEffect, new();
    public static ICharacterProxy GetByUniqueId(int uniqueMemberId);
    public static CharacterQuery Query();
    public static IReadOnlyList<ICharacterProxy> ListAll();
    public static IReadOnlyList<ICharacterProxy> ListPersistent();
    public static IReadOnlyList<ICharacterProxy> ListTemporary();
    public static ICharacterProxy CreateSyntheticCharacter(string firstName, string lastName, string persistenceKey, string sourceModId, bool isPersistent = true);
    public static ICharacterProxy CreateTemporaryCharacter(string firstName, string lastName, string sourceModId);
    public static ICharacterProxy FindSyntheticCharacter(string persistenceKey);
    public static void Unregister(ICharacterProxy character);
    public static int UnloadTemporaryCharacters(string sourceModId);

    // Explicit Sheltered runtime escape hatches.
    public static ICharacterProxy FromFamilyMember(FamilyMember member);
    public static ICharacterProxy FromNpcVisitor(NpcVisitor npc);
    public static FamilyMember FindFamilyMember(ICharacterProxy character);
    public static NpcVisitor FindNpcVisitor(ICharacterProxy character);
    public static void SwapEncounterCharacter(EncounterCharacter encounterActor, ICharacterProxy newCharacter, Action<EncounterCharacter> onSwapComplete = null);
}

public interface ICharacterProxy : ICharacterDefinition
{
    CharacterState State { get; }
    CharacterLocation Location { get; }
    bool IsActive { get; }
    ICharacterEffects Effects { get; }
    ICharacterAttributes Attributes { get; }
    ICharacterData Data { get; }
    event Action<ICharacterProxy> OnUnregistered;
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
    public string LabelKey;
    public string Tooltip;
    public string TooltipKey;
    public SettingMode Mode; // default: Advanced
    public float MinValue;
    public float MaxValue;
    public float StepSize;
    public SliderStepMode SliderStepMode; // default: Granular
    public string ValueFormat;
    public string UnitSuffix;
    public string TrueLabel;
    public string FalseLabel;
    public string ActionLabel;
    public string Placeholder;
    public bool ShowValueInput;
    public bool ShowStepperButtons;
    public float FineStepSize;
    public float LargeStepSize;
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
public static class ShelteredContent
{
    public static RegistrationResult RegisterItem(ItemDefinition definition);
    public static RegistrationResult RegisterItem(string modId, string itemId, ItemDefinition definition);
    public static void PatchItem(ItemPatch patch);
    public static void RegisterRecipe(RecipeDefinition definition);
    public static void RegisterCookingRecipe(CookingRecipe recipe);
    public static void PatchRecipe(RecipePatch patch);
    public static void AddLoot(LootEntry entry);
    public static void SetLocalization(string key, string value);
    public static bool TryGetLocalization(string key, out string value);

    public static Texture2D LoadTexture(Assembly assembly, string relativePath);
    public static Texture2D LoadTexture(string modRootPath, string relativePath);
    public static Sprite LoadSprite(Assembly assembly, string relativePath);
    public static Sprite LoadSprite(Assembly assembly, string relativePath, float pixelsPerUnit);
    public static Sprite LoadSprite(string modRootPath, string relativePath);
    public static Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit);
    public static AssetBundle LoadBundle(Assembly assembly, string relativePath);
    public static AssetBundle LoadBundle(string modRootPath, string relativePath);
    public static GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath);

    public static bool ResolveItemType(string itemId, out ItemManager.ItemType type);
    public static bool TryGetCookingRecipe(ItemManager.ItemType rawItemType, out CookingRecipe recipe);
    public static bool IsRawFood(ItemManager.ItemType itemType);
    public static ItemInstance CreateItem(string itemId);
    public static bool TryAddToInventory(ItemInstance item);
    public static bool TryAddToInventory(string itemId, int quantity);
    public static bool TryRemoveFromInventory(string itemId, int quantity);
    public static int GetItemCount(string itemId);
    public static int GetItemCount(string itemId, bool includeParties);
    public static ReadOnlyCollection<ItemStack> GetAllInventoryItems();
    public static int GetStorageCapacity();
    public static int GetUsedStorage();
}
```

Sheltered UI facade:

```csharp
public static class ShelteredUI
{
    public static UITakeoverSession For(BasePanel panel);
    public static UITakeoverSession For(GameObject root);
    public static UITakeoverSession For(Transform root);
    public static IDisposable RegisterPanelTakeover<TPanel>(string key, Action<TPanel, UITakeoverSession> apply)
        where TPanel : BasePanel;
    public static IDisposable RegisterPanelTakeover<TPanel>(string key, Action<TPanel, UITakeoverSession> apply, bool applyOnOpened, bool applyOnResumed)
        where TPanel : BasePanel;
    public static void UnregisterPanelTakeover(string key);
    public static UICloneResult CloneElement(GameObject template, Transform parent);
    public static UICloneResult CloneElement(GameObject template, Transform parent, UICloneOptions options);
    public static UIOperationResult StripInheritedEventListeners(GameObject root);
    public static UIOperationResult StripInheritedEventListeners(GameObject root, bool includeChildren);
    public static UIOperationResult BindButtonClick(UIButton button, Action onClick, UIButtonBindingMode mode);
    public static UIOperationResult BindButtonClick<TContext>(UIButton button, TContext context, Action<TContext> onClick, UIButtonBindingMode mode);
    public static UIColorSnapshot SnapshotColors(GameObject root);
    public static UIColorSnapshot SnapshotColors(GameObject root, bool includeChildren);
    public static UIOperationResult RestoreColors(UIColorSnapshot snapshot);
    public static IDisposable SubscribePanelLifecycle<TPanel>(Action<TPanel> onOpened, Action<TPanel> onClosed, Action<TPanel> onResumed)
        where TPanel : BasePanel;
    public static void ShowShelteredKeybinds();
}

public enum UIButtonBindingMode { Replace, Append }

public sealed class UICloneOptions
{
    public bool StripInheritedEventListeners { get; set; }
    public bool ClearButtonClickHandlers { get; set; }
    public bool IncludeChildren { get; set; }
    public string CloneName { get; set; }
}

public sealed class UICloneResult
{
    public GameObject Clone { get; }
    public bool Success { get; }
    public int AffectedCount { get; }
    public ReadOnlyCollection<string> Warnings { get; }
    public bool HasWarnings { get; }
}

public sealed class UIOperationResult
{
    public bool Success { get; }
    public int AffectedCount { get; }
    public ReadOnlyCollection<string> Warnings { get; }
    public bool HasWarnings { get; }
}

public sealed class UIColorSnapshot
{
    public bool Success { get; }
    public int LabelCount { get; }
    public int WidgetCount { get; }
    public int TweenCount { get; }
    public ReadOnlyCollection<string> Warnings { get; }
    public bool HasWarnings { get; }
    public UIOperationResult Restore();
}
```

Runtime UI facade:

```csharp
public static class ShelteredRuntimeUI
{
    public static RuntimeUiHandle OpenContainer(ContainerUiRequest request);
    public static RuntimeUiHandle OpenCrafting(CraftingUiRequest request);
    public static IDisposable RegisterObjectPanel(ObjectPanelRegistration registration);
    public static bool IsOpen(string panelId);
    public static void Refresh(string panelId);
    public static void Close(string panelId);
    public static void CloseOwner(string ownerId);
    public static void CloseAll();
}

public sealed class RuntimeUiHandle : IDisposable
{
    public string PanelId { get; }
    public bool IsOpen { get; }
    public void Refresh();
    public void Close();
    public void Dispose();
}

public sealed class ContainerUiRequest
{
    public string PanelId { get; set; }
    public string Title { get; set; }
    public string OwnerId { get; set; }
    public RuntimePanelOptions PanelOptions { get; set; }
    public IList<ContainerUiItem> Items { get; set; }
    public Func<IList<ContainerUiItem>> ItemSource { get; set; }
    public ItemCategory[] Categories { get; set; }
    public ItemCategory? InitialCategory { get; set; }
    public string[] AllowedItemIds { get; set; }
    public string EmptyText { get; set; }
    public int TransferQuantity { get; set; }
    public ContainerUiTransferDirection TransferDirection { get; set; }
    public bool CloseOnTransfer { get; set; }
    public bool RefreshEveryFrame { get; set; }
    public Obj_Base AttachedObject { get; set; }
    public Comparison<ContainerUiItem> SortComparison { get; set; }
    public Func<ContainerUiItem, bool> CanSelect { get; set; }
    public Func<ContainerUiItem, bool> CanTransfer { get; set; }
    public Func<ContainerUiItem, string> FormatCount { get; set; }
    public IList<ContainerUiAction> Actions { get; set; }
    public Action<ContainerUiItem> OnItemSelected { get; set; }
    public Action<ContainerUiTransferContext> OnTransferRequested { get; set; }
    public Action<RuntimeUiHandle> OnRefreshed { get; set; }
    public Action OnClosed { get; set; }
}

public sealed class ContainerUiItem
{
    public string ItemId { get; set; }
    public string DisplayName { get; set; }
    public string Subtitle { get; set; }
    public ItemCategory Category { get; set; }
    public int Count { get; set; }
    public string CountText { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsTransferEnabled { get; set; }
    public object Tag { get; set; }
}

public sealed class ContainerUiAction
{
    public string Id { get; set; }
    public string Text { get; set; }
    public Func<bool> IsEnabled { get; set; }
    public Action<RuntimeUiHandle> Execute { get; set; }
}

public sealed class ObjectPanelRegistration
{
    public string ObjectId { get; set; }
    public ObjectManager.ObjectType ObjectType { get; set; }
    public string InteractionId { get; set; }
    public string InteractionText { get; set; }
    public int Priority { get; set; }
    public Func<ObjectPanelContext, bool> CanOpen { get; set; }
    public Func<ObjectPanelContext, RuntimeUiHandle> Open { get; set; }
}

public sealed class ObjectPanelContext
{
    public string ObjectId { get; }
    public ObjectManager.ObjectType ObjectType { get; }
    public Obj_Base TargetObject { get; }
    public FamilyMember SelectedMember { get; }
}

public sealed class CraftingUiRequest
{
    public string PanelId { get; set; }
    public string Title { get; set; }
    public string OwnerId { get; set; }
    public RuntimePanelOptions PanelOptions { get; set; }
    public IList<CraftingUiRecipe> Recipes { get; set; }
    public Func<IList<CraftingUiRecipe>> RecipeSource { get; set; }
    public string EmptyText { get; set; }
    public string CraftButtonText { get; set; }
    public bool RefreshEveryFrame { get; set; }
    public Func<CraftingUiRecipe, bool> IsAvailable { get; set; }
    public Action<CraftingUiRecipe> OnCraft { get; set; }
    public Action<CraftingUiCraftContext> OnCraftRequested { get; set; }
    public Func<CraftingUiRecipe, string> GetUnavailableReason { get; set; }
    public Action<RuntimeUiHandle> OnRefreshed { get; set; }
    public Action OnClosed { get; set; }
}

public sealed class CraftingUiRecipe
{
    public string RecipeId { get; set; }
    public string DisplayName { get; set; }
    public string Subtitle { get; set; }
    public string OutputItemId { get; set; }
    public int OutputCount { get; set; }
    public string OutputCountText { get; set; }
    public Sprite Icon { get; set; }
    public string CraftButtonText { get; set; }
    public string UnavailableText { get; set; }
    public IList<CraftingUiIngredient> Ingredients { get; set; }
    public object Tag { get; set; }
}

public sealed class CraftingUiIngredient
{
    public string ItemId { get; set; }
    public int Count { get; set; }
}

public sealed class CraftingUiCraftContext
{
    public CraftingUiRecipe Recipe { get; }
    public RuntimeUiHandle Panel { get; }
}

public sealed class RuntimePanelOptions
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int HeaderHeight { get; set; }
    public int TitleFontSize { get; set; }
    public string CloseText { get; set; }
    public bool ShowCloseButton { get; set; }
    public Sprite Icon { get; set; }
    public Sprite HeaderIcon { get; set; }
    public string HeaderIconText { get; set; }
    public int HeaderIconSize { get; set; }
    public bool ShowHeaderIcon { get; set; }
    public string Subtitle { get; set; }
    public RuntimePanelStyle Style { get; set; }
}

public sealed class RuntimePanelStyle
{
    public Color? FrameColor { get; set; }
    public Color? HeaderColor { get; set; }
    public Color? AccentColor { get; set; }
    public Color? TextColor { get; set; }
    public Color? ButtonColor { get; set; }
    public Color? DisabledButtonColor { get; set; }
}
```

## Runtime UI + Stores (`ShelteredAPI.UI.Runtime`, `ShelteredAPI.Storage`, `ShelteredAPI.Workstations`)

Task guide: [Runtime UI, Stores, and Cooking Stations](ShelteredAPI_Runtime_UI_Stores_Guide.md).

```csharp
public enum ItemStoreKind { Unknown, Inventory, Freezer, Mod }
public enum CharacterItemAssignmentKind { Assigned, Reserved, Equipped, Carried, Quest }
public enum CharacterItemSlot { None, MainHand, OffHand, Backpack, Medicine, Food, Tool }

public interface IItemStore
{
    string StoreId { get; }
    string DisplayName { get; }
    ItemStoreKind Kind { get; }
    int Capacity { get; }
    int Used { get; }
    bool IsReadOnly { get; }

    ItemStoreSnapshot Snapshot();
    int GetCount(string itemId);
    bool CanAdd(string itemId, int quantity);
    bool CanRemove(string itemId, int quantity);
    ItemTransferResult Add(string itemId, int quantity);
    ItemTransferResult Remove(string itemId, int quantity);
}

public sealed class ItemStoreItem
{
    public string ItemId { get; set; }
    public string DisplayName { get; set; }
    public string Subtitle { get; set; }
    public ItemCategory Category { get; set; }
    public int Count { get; set; }
}

public sealed class ItemStoreSnapshot
{
    public string StoreId { get; set; }
    public string DisplayName { get; set; }
    public ItemStoreKind Kind { get; set; }
    public int Capacity { get; set; }
    public int Used { get; set; }
    public bool IsReadOnly { get; set; }
    public IList<ItemStoreItem> Items { get; set; }
}

public sealed class ItemTransferResult
{
    public bool Success { get; }
    public string ItemId { get; }
    public int Requested { get; }
    public int Moved { get; }
    public string ErrorMessage { get; }
}

public sealed class ItemReservationResult
{
    public bool Success { get; }
    public string ReservationId { get; }
    public string ItemId { get; }
    public int Requested { get; }
    public int Reserved { get; }
    public string OwnerToken { get; }
    public string ErrorMessage { get; }
}

public interface IReservableItemStore
{
    ItemReservationResult Reserve(string itemId, int quantity, string ownerToken);
    ItemTransferResult CommitReservation(string reservationId);
    ItemTransferResult CancelReservation(string reservationId);
    int GetAvailableCount(string itemId);
}

public sealed class CharacterItemAssignment
{
    public string AssignmentId { get; set; }
    public ActorId ActorId { get; set; }
    public string MemberDisplayName { get; set; }
    public string SourceStoreId { get; set; }
    public string SourceStoreName { get; set; }
    public ItemStoreKind SourceStoreKind { get; set; }
    public string ItemId { get; set; }
    public int Quantity { get; set; }
    public string ReservationId { get; set; }
    public CharacterItemAssignmentKind Kind { get; set; }
    public CharacterItemSlot Slot { get; set; }
}

public interface ICharacterItemAssignmentService
{
    CharacterItemAssignment Assign(ActorId actorId, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot);
    CharacterItemAssignment Assign(FamilyMember member, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot);
    bool Unassign(string assignmentId);
    IList<CharacterItemAssignment> GetAssignments(ActorId actorId);
    IList<CharacterItemAssignment> GetAssignments(FamilyMember member);
    IList<CharacterItemAssignment> GetAvailableAssignments(ActorId actorId);
    IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member);
    int GetAssignedCount(ActorId actorId, string itemId);
    int GetAssignedCount(FamilyMember member, string itemId);
    int ReleaseAssignmentsForActor(ActorId actorId);
    int ReleaseAssignmentsForMember(FamilyMember member);
}

public static class ShelteredCharacterItems
{
    public static ICharacterItemAssignmentService Service { get; }
    public static CharacterItemAssignment Assign(ActorId actorId, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot);
    public static CharacterItemAssignment Assign(FamilyMember member, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot);
    public static bool Unassign(string assignmentId);
    public static IList<CharacterItemAssignment> GetAssignments(ActorId actorId);
    public static IList<CharacterItemAssignment> GetAssignments(FamilyMember member);
    public static IList<CharacterItemAssignment> GetAvailableAssignments(ActorId actorId);
    public static IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member);
    public static int GetAssignedCount(ActorId actorId, string itemId);
    public static int GetAssignedCount(FamilyMember member, string itemId);
    public static int ReleaseAssignmentsForActor(ActorId actorId);
    public static int ReleaseAssignmentsForMember(FamilyMember member);
}

public static class ShelteredStores
{
    public static IItemStore ForInventory();
    public static IItemStore ForFreezer(Obj_Freezer freezer);
    public static IItemStore ForMod(string ownerId, string storeId, string displayName);
    public static IItemStore ForMod(string ownerId, string storeId, string displayName, int capacity);
    public static IItemStore ForObject(string ownerId, Obj_Base targetObject, string displayName);
    public static IItemStore ForObject(string ownerId, Obj_Base targetObject, string displayName, int capacity);
    public static IItemStore FindNearestObjectStore(string ownerId, ObjectManager.ObjectType objectType, Vector3 position, string displayName);
    public static IItemStore FindNearestObjectStore(string ownerId, ObjectManager.ObjectType objectType, Vector3 position, string displayName, int capacity);
    public static Obj_Base FindNearestObject(ObjectManager.ObjectType objectType, Vector3 position);
    public static IItemStore FindNearestFreezer(Vector3 position);
    public static Obj_Freezer FindNearestFreezerObject(Vector3 position);
    public static IList<IItemStore> GetFreezers();
    public static ItemTransferResult Transfer(IItemStore source, IItemStore target, string itemId, int quantity);
    public static IList<ContainerUiItem> ToContainerItems(IItemStore store);
    public static ContainerUiRequest CreateContainerRequest(IItemStore store, string ownerId, string panelId, string title);
    public static ContainerUiRequest CreateContainerRequest(IItemStore store, IItemStore transferStore, string ownerId, string panelId, string title);
    public static string BuildObjectStoreId(Obj_Base targetObject);
}

public sealed class CookingStationRecipe
{
    public string RecipeId { get; set; }
    public string DisplayName { get; set; }
    public string Subtitle { get; set; }
    public IList<RecipeIngredient> Ingredients { get; set; }
    public string OutputItemId { get; set; }
    public int OutputCount { get; set; }
    public string OutputCountText { get; set; }
    public float DurationSeconds { get; set; }
    public Sprite Icon { get; set; }
}

public sealed class CookingStationRequest
{
    public string OwnerId { get; set; }
    public string PanelId { get; set; }
    public string Title { get; set; }
    public RuntimePanelOptions PanelOptions { get; set; }
    public IItemStore IngredientStore { get; set; }
    public IItemStore OutputStore { get; set; }
    public FamilyMember Worker { get; set; }
    public Obj_Base WorkstationObject { get; set; }
    public CookingStationJobOptions JobOptions { get; set; }
    public IList<CookingStationRecipe> Recipes { get; set; }
    public Func<IList<CookingStationRecipe>> RecipeSource { get; set; }
    public bool ConsumeIngredients { get; set; }
    public bool RefreshEveryFrame { get; set; }
    public Func<CookingStationRecipe, string> GetUnavailableReason { get; set; }
    public Action<CookingCraftContext> OnCraftQueued { get; set; }
    public Action<CookingCraftContext> OnCrafted { get; set; }
    public Action<CookingCraftContext> OnCraftFailed { get; set; }
    public Action OnClosed { get; set; }
}

public sealed class CookingStationJobOptions
{
    public bool Enabled { get; set; }
    public float DurationSeconds { get; set; }
    public string JobType { get; set; }
    public string AnimationTrigger { get; set; }
    public string CompleteAnimationTrigger { get; set; }
    public bool QueueAsPlayerJob { get; set; }
    public bool ClosePanelOnQueue { get; set; }
    public int TargetIntegrityCost { get; set; }
}

public sealed class CookingStationRegistration
{
    public string OwnerId { get; set; }
    public string ObjectId { get; set; }
    public ObjectManager.ObjectType ObjectType { get; set; }
    public string InteractionId { get; set; }
    public string InteractionText { get; set; }
    public int Priority { get; set; }
    public string PanelId { get; set; }
    public string Title { get; set; }
    public RuntimePanelOptions PanelOptions { get; set; }
    public bool ConsumeIngredients { get; set; }
    public bool RefreshEveryFrame { get; set; }
    public Func<CookingStationContext, bool> CanOpen { get; set; }
    public Func<CookingStationContext, IItemStore> IngredientStore { get; set; }
    public Func<CookingStationContext, IItemStore> OutputStore { get; set; }
    public Func<CookingStationContext, FamilyMember> Worker { get; set; }
    public Func<CookingStationContext, Obj_Base> WorkstationObject { get; set; }
    public CookingStationJobOptions JobOptions { get; set; }
    public IList<CookingStationRecipe> Recipes { get; set; }
    public Func<CookingStationContext, IList<CookingStationRecipe>> RecipeSource { get; set; }
    public Func<CookingStationRecipe, string> GetUnavailableReason { get; set; }
    public Action<CookingCraftContext> OnCraftQueued { get; set; }
    public Action<CookingCraftContext> OnCrafted { get; set; }
    public Action<CookingCraftContext> OnCraftFailed { get; set; }
    public Action OnClosed { get; set; }
}

public sealed class CookingStationContext
{
    public string ObjectId { get; }
    public ObjectManager.ObjectType ObjectType { get; }
    public Obj_Base TargetObject { get; }
    public FamilyMember SelectedMember { get; }
}

public sealed class CookingCraftContext
{
    public CookingStationRecipe Recipe { get; }
    public IItemStore IngredientStore { get; }
    public IItemStore OutputStore { get; }
    public RuntimeUiHandle Panel { get; }
    public FamilyMember Worker { get; }
    public Obj_Base WorkstationObject { get; }
    public bool Queued { get; }
    public ItemTransferResult Result { get; }
}

public static class ShelteredCooking
{
    public static RuntimeUiHandle Open(CookingStationRequest request);
    public static IDisposable RegisterStation(CookingStationRegistration registration);
    public static FamilyMember FindIdleWorker();
}
```

Minimal fridge-backed cooking station flow:

```csharp
Obj_Base fridgeObject = ShelteredStores.FindNearestObject(
    ObjectManager.ObjectType.Freezer,
    stove.transform.position);
if (fridgeObject == null)
    return;

IItemStore fridgeStore = ShelteredStores.ForObject(
    ownerId: "com.example.cooking",
    targetObject: fridgeObject,
    displayName: "Fridge Storage",
    capacity: 24);

ShelteredRuntimeUI.OpenContainer(
    ShelteredStores.CreateContainerRequest(
        store: fridgeStore,
        ownerId: "com.example.cooking",
        panelId: "com.example.cooking.fridge." + fridgeObject.objectId,
        title: "Fridge Storage"));

ShelteredCooking.RegisterStation(new CookingStationRegistration
{
    OwnerId = "com.example.cooking",
    ObjectType = ObjectManager.ObjectType.Stove,
    InteractionId = "com.example.cooking.stove.cook",
    InteractionText = "Cook",
    CanOpen = context =>
        ShelteredStores.FindNearestObject(
            ObjectManager.ObjectType.Freezer,
            context.TargetObject.transform.position) != null,
    IngredientStore = context =>
        ShelteredStores.FindNearestObjectStore(
            "com.example.cooking",
            ObjectManager.ObjectType.Freezer,
            context.TargetObject.transform.position,
            "Fridge Storage",
            24),
    OutputStore = context => ShelteredStores.ForInventory(),
    JobOptions = new CookingStationJobOptions
    {
        JobType = "cook_food",
        AnimationTrigger = "Rummage",
        DurationSeconds = 3f
    },
    Recipes = new[]
    {
        new CookingStationRecipe
        {
            RecipeId = "com.example.cooking.meat_to_ration",
            DisplayName = "Cook Ration",
            OutputItemId = VanillaItems.Ration,
            OutputCount = 1,
            Ingredients = new[]
            {
                new RecipeIngredient { ItemId = VanillaItems.Meat, Count = 1 }
            }
        }
    }
});
```

`ForFreezer(...)` and `FindNearestFreezer(...)` are adapters over vanilla `Obj_Freezer` data and preserve vanilla limits: meat and desperate meat only. `ForObject(...)` and `FindNearestObjectStore(...)` create mod-owned stores keyed to world objects, which is the supported path for fridge-like custom storage. ShelteredAPI avoids patching `Obj_Freezer` to accept custom item types.

`ItemDefinition` fluent localization APIs (ShelteredAPI v2.0):

```csharp
public ItemDefinition WithDisplayName(string name);           // legacy key-or-text auto-detection
public ItemDefinition WithDescription(string desc);           // legacy key-or-text auto-detection
public ItemDefinition WithDisplayNameKey(string key);         // explicit key
public ItemDefinition WithDescriptionKey(string key);         // explicit key
public ItemDefinition WithDisplayNameText(string text);       // explicit literal text
public ItemDefinition WithDescriptionText(string text);       // explicit literal text
```

Localization behavior for content injection (ShelteredAPI v2.0):
- `m_NameLocalizationKey` / `m_DescLocalizationKey` are always set to keys (never raw text).
- For `...Text(...)`, ShelteredAPI auto-generates keys like `shelteredapi.<modid>.<itemid>.name|desc` and registers values in its custom table.
- Legacy `WithDisplayName/WithDescription` values are interpreted as `key` if they look like keys (`.` and no spaces), otherwise as literal text.
- ShelteredAPI patches `Localization.Get(string,bool)` and returns custom-table values directly (preserving original case for literal text).
- Injector logs localization mode diagnostics per item (`name=key|text`, `desc=key|text`, final keys).

## Event + Registry APIs

```csharp
public static class ShelteredEvents
{
    public static event Action<int> NewDay;
    public static event Action<SaveData> BeforeSave;
    public static event Action<SaveData> BeforeLoadSceneContents;
    public static event Action<SaveData> AfterLoad;
    public static event Action NewGame;
    public static event Action SessionStarted;
    public static event Action<EncounterCharacter, EncounterCharacter> CombatStarted;
    public static event Action<ExplorationParty> PartyReturned;

    public static event Action<BasePanel> PanelOpened;
    public static event Action<BasePanel> PanelClosed;
    public static event Action<BasePanel> PanelResumed;
    public static event Action<BasePanel> PanelPaused;
    public static event Action<GameObject, string> ButtonClicked;

    public static event Action<int> FactionSpawned;
    public static event Action<int, int> FactionZoneGrew;
    public static event Action<int, int> FactionTerritoryChanged;

    public static event Action<TimeTriggerBatch> SixHourTick;
    public static event Action<TimeTriggerBatch> StaggeredTick;
    public static void RegisterTimeTrigger(string triggerId, int priority, TimeTriggerCadence cadence, Action<TimeTriggerBatch> callback);
    public static bool UnregisterTimeTrigger(string triggerId);
    public static List<TimeTriggerInfo> GetTimeTriggerPriorityList(TimeTriggerCadence cadence);
    public static void ConfigureStaggeredTimeRange(int minInclusiveHours, int maxInclusiveHours);
}

public static class ShelteredSaveEvents
{
    public static event SaveEvent BeforeSave;
    public static event SaveEvent AfterSave;
    public static event LoadEvent BeforeLoad;
    public static event LoadEvent AfterLoad;
    public static event PageChangedEvent PageChanged;
    public static event ReservationChangedEvent ReservationChanged;
}

public static class ShelteredSaves
{
    public static SaveEntry[] ListStandard();
    public static SaveEntry[] ListStandard(int page, int pageSize);
    public static int CountStandard();
    public static int GetMaxStandardSlot();
    public static SaveEntry GetStandard(string saveId);
    public static SaveEntry GetStandardSlot(int absoluteSlot);
    public static SaveEntry CreateStandard(SaveCreateOptions options);
    public static SaveEntry OverwriteStandard(string saveId, SaveOverwriteOptions options, byte[] xmlBytes);
    public static bool DeleteStandard(string saveId);
    public static bool DeleteStandardSlot(int absoluteSlot);

    public static SaveEntry[] ListScenario(string scenarioId, int page, int pageSize);
    public static SaveEntry GetScenario(string scenarioId, string saveId);
    public static SaveEntry CreateScenario(string scenarioId, SaveCreateOptions options);
    public static SaveEntry CreateNextScenario(string scenarioId, SaveCreateOptions options);
    public static int GetNextScenarioSlot(string scenarioId);
    public static SaveEntry OverwriteScenario(string scenarioId, string saveId, SaveOverwriteOptions options, byte[] xmlBytes);
    public static bool DeleteScenario(string scenarioId, string saveId);
}

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
```

## Custom Scenarios (`ModAPI.Scenarios`, `ShelteredAPI.Scenarios`)

```csharp
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

// ShelteredAPI.Scenarios public XML authoring and scenario runtime API
public static class ShelteredScenarios
{
    public static CustomScenarioRegistrationResult Register(IShelteredCustomScenario scenario);
    public static CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration);
    public static bool Unregister(string scenarioId);
    public static bool TryGet(string scenarioId, out CustomScenarioInfo scenario);
    public static CustomScenarioInfo[] List();
    public static CustomScenarioRegistration FromDefinition(string id, string displayName, ScenarioDef definition);
    public static CustomScenarioRegistration FromScenario(IShelteredCustomScenario scenario);
    public static CustomScenarioRegistration FromFactory(string id, string displayName, ShelteredScenarioDefinitionFactory factory);
    public static ShelteredScenarioDefBuilder CreateScenarioDefBuilder();
    public static ShelteredScenarioDefBuilderCompatibility CheckScenarioDefBuilderCompatibility();
    public static ScenarioInfo[] ListXmlDefinitions();
    public static void RefreshXmlDefinitions();
}

// XML scenario authoring facade.
// CreateDefinition creates an in-memory DTO.
// LoadDefinition/SaveDefinition read and write scenario.xml files.
// FromXml/ToXml edit XML text without disk IO.
// ValidateDefinition validates a DTO against an optional scenario file path for pack-relative assets.
// TryLoadXmlDefinition loads a catalog entry by scenario id and returns validation details on failure.

public static class ShelteredScenarioAuthoring
{
    public const string DefaultFileName = "scenario.xml";
    public const string DefaultTitle = "Untitled Scenario";
    public const string DefaultAuthor = "unknown";
    public const string DefaultVersion = "0.1.0";
    public const string GeneratedBlendTerrainId = "GeneratedBlend";
    public const string EmptyStartingCastWarning = "No starting survivors.";
    public const string EmptyStartingCastDisabledReason = "No starting survivors. Add a starting survivor in Cast before playtest can begin.";
    public const string UnsavedDraftPlayDisabledReason = "Save draft before testing.";
    public const string ValidationUnavailablePlayDisabledReason = "Validation is unavailable. Open Publish and refresh checks before playtest.";
    public static ScenarioDefinition CreateDefinition();
    public static ScenarioDefinition CreateDefinition(ScenarioBaseGameMode baseGameMode);
    public static string BumpVersion(string version, bool minor);
    public static string[] GetKnownMapIconIds();
    public static bool IsKnownMapIconId(string iconId);
    public static ScenarioActorRef ResolveFutureSurvivorActorReference(FutureSurvivorDefinition survivor);
    public static ScenarioDefinition LoadDefinition(string filePath);
    public static bool TryLoadDefinitionWithRecovery(string filePath, out ScenarioDefinition definition, out string recoveryMessage, out bool recovered);
    public static ScenarioInfo LoadDefinitionInfo(string filePath, string ownerModId);
    public static ScenarioDefinition FromXml(string xml);
    public static void SaveDefinition(ScenarioDefinition definition, string filePath);
    public static string ToXml(ScenarioDefinition definition);
    public static ScenarioValidationResult ValidateDefinition(ScenarioDefinition definition, string scenarioFilePath);
    public static ScenarioDefinitionReferenceIndex IndexDefinition(ScenarioDefinition definition);
    public static ScenarioDefinitionReferenceIndex IndexDefinition(TriggersAndEventsDefinition triggersAndEvents);
    public static ScenarioStoryFlowIssue[] AnalyzeStoryFlow(ScenarioDefinition definition);
    public static bool HasStartingSurvivor(ScenarioDefinition definition);
    public static bool CanStartPlay(ScenarioDefinition definition, out string reason);
    public static ScenarioMapProjectionField[] GetMapEncounterProjectionFields();
    public static bool TryCompileTrigger(TriggerDef trigger, int index, out ScenarioScheduledActionDefinition action, out string reason);
    public static bool IsManualTrigger(TriggerDef trigger);
    public static ScenarioValidationResult ValidateXmlDefinition(string scenarioId);
    public static bool TryLoadXmlDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation);
    public static int AssignMissingActorReferences(ScenarioDefinition definition);
    public static ScenarioActorRef EnsureStartingMemberActorReference(ScenarioDefinition definition, FamilyMemberConfig member, int memberIndex);
    public static ScenarioActorRef EnsureFutureSurvivorActorReference(ScenarioDefinition definition, FutureSurvivorDefinition survivor, int survivorIndex);
    public static ScenarioActorRef CreateLiveFamilyMemberActorReference(FamilyMember member);
}

public sealed class ScenarioDefinitionReferenceIndex
{
    public bool HasGate(string id);
    public bool HasTrigger(string id);
    public bool HasQuest(string id);
    public bool HasCondition(string id);
    public bool HasExpansion(string id);
    public bool HasObject(string id);
    public bool HasFutureSurvivor(string id);
    public bool HasFamilySurvivor(string id);
}

public sealed class ScenarioStoryFlowIssue
{
    public ScenarioIssueSeverity Severity { get; set; }
    public string Code { get; set; }
    public string Message { get; set; }
    public int StageIndex { get; set; }
    public string StageId { get; set; }
    public int IntercomIndex { get; set; }
}

public sealed class ScenarioMapProjectionField
{
    public string Group { get; }
    public string Field { get; }
    public bool AppliesInGame { get; }
    public string StatusText { get; }
}

public static class ShelteredScenarioRuntime
{
    public static string GetTransformPath(Transform transform);
    public static bool TryResolveRuntimeSpriteTarget(string targetPath, ScenarioSpriteTargetComponentKind preferredKind, out ScenarioRuntimeSpriteTarget target);
    public static bool TryResolveRuntimeSpriteTarget(Transform transform, ScenarioSpriteTargetComponentKind preferredKind, out ScenarioRuntimeSpriteTarget target);
    public static IScenarioPreviewSession BeginPreview(ScenarioDefinition definition, string scenarioFilePath);
    public static bool IsWorldReady(out string blockingReason);
    public static bool IsShelterSceneActive();
    public static bool TryGetShelterGridCell(Vector3 worldPosition, out int gridX, out int gridY);
    public static Vector3 GetShelterGridCellCenter(int gridX, int gridY);
    public static string CreateRuntimeSpriteKey(Sprite sprite);
    public static string CreateRuntimeSpriteKey(Texture2D texture, string spriteName);
    public static ScenarioMapLootEntrySnapshot[] PlanMapLoot(ScenarioDefinition definition, MapLocationDefinition location, MapLootTableDefinition table);
    public static ScenarioMapLootEntrySnapshot[] PlanMapLoot(ScenarioDefinition definition, MapLocationDefinition location, MapLootTableDefinition table, int masterSeed);
    public static Sprite ResolveSpriteAsset(ScenarioDefinition definition, string packRoot, string spriteId, string relativePath, string runtimeSpriteKey, string contextLabel);
    public static void InvalidateSpriteAssets();
    public static void RegisterRuntimeSprite(string runtimeSpriteKey, Sprite sprite);
    public static bool TryFindRuntimeSprite(string runtimeSpriteKey, out Sprite sprite);
    public static Sprite CreateAndRegisterRuntimeSprite(Texture2D texture, string spriteName);
    public static bool TryApplyRuntimeSprite(string targetPath, ScenarioSpriteTargetComponentKind targetKind, Sprite sprite);
    public static bool ApplyConfiguredAppearance(ScenarioDefinition definition, string scenarioFilePath, FamilyMemberConfig config, FamilyMember member, out string message);
    public static void ResolveConfiguredAppearanceColors(FamilyMemberAppearanceConfig appearance, out Color hair, out Color skin, out Color shirt, out Color pants);
    public static bool TryLaunchScenarioWorld(ScenarioWorldLaunchRequest request, out string message);
    public static bool TryCompleteScenarioWorldLaunch(string expectedSceneName, string targetLabel);
    public static bool TryReturnToMainMenu(out string message);
    public static bool TryGetRuntimeIdentity(GameObject gameObject, out ScenarioRuntimeIdentity identity);
    public static bool TryGetRuntimeIdentity(Component component, out ScenarioRuntimeIdentity identity);
    public static bool FireTrigger(string triggerId);
    public static bool FireTrigger(string triggerId, string source, out string message);
    public static ScenarioScoreSnapshot GetScoreSnapshot();
    public static void SetScoreSnapshot(ScenarioScoreSnapshot snapshot);
    public static void ClearScoreSnapshot();
}

// Coarse, process-local runtime-preview boundary used by the optional editor.
// The owner must Dispose the session; there is no separate EndPreview API.
public interface IScenarioPreviewSession : IDisposable
{
    ScenarioPreviewResult StartResult { get; }
    ScenarioPreviewResult Refresh(ScenarioDefinition definition, ScenarioPreviewRefreshScope scope);
    bool RestartWorld(ScenarioWorldLaunchRequest request, out string error);
    ScenarioRuntimeSnapshot CaptureRuntimeState();
    void SetExecutionLogging(bool enabled);
    ScenarioRuntimeExecutionEntrySnapshot[] CaptureExecutionLog(int maximumEntries);
    bool TryFireRuntimeElement(string elementId, out string message);
    bool TryGetMinutesUntilNextAuthoredEvent(int maximumMinutes, out int minutes);
    void NotifyGameTimeChanged();
    bool TryPreviewRuntimeSpriteFrame(string targetPath, ScenarioSpriteTargetComponentKind targetKind, Sprite sprite);
    bool TryPlayRuntimeSpriteAnimation(string targetPath, ScenarioSpriteTargetComponentKind targetKind, Sprite[] frames, float[] durations, float speed);
    void StopRuntimeSpriteAnimation(string targetPath, ScenarioSpriteTargetComponentKind targetKind);
    void CaptureRuntimeObjectState(Obj_Base source, ObjectPlacement destination);
    bool IsStationObject(Obj_Base obj);
    void CaptureStationUpgradeState(Obj_Base source, ObjectPlacement destination);
    ScenarioStationUpgradeSnapshot GetStationUpgradeSnapshot(Obj_Base obj, ObjectPlacement placement);
    bool TryChangeStationObjectLevel(Obj_Base obj, ObjectPlacement placement, int delta, out string message);
    bool TryChangeStationUpgradeLevel(Obj_Base obj, ObjectPlacement placement, string pathName, int delta, out string message);
    bool TryChangeStationStat(Obj_Base obj, ObjectPlacement placement, string statName, float delta, out string message);
    bool TryClearStationStat(Obj_Base obj, ObjectPlacement placement, string statName, out string message);
}

[Flags]
public enum ScenarioPreviewRefreshScope
{
    None = 0,
    World = 1,
    SpriteSwaps = 2,
    ScenePlacements = 4,
    MapProjection = 8,
    SceneAssets = SpriteSwaps | ScenePlacements,
    All = World | SceneAssets | MapProjection
}

public sealed class ScenarioWorldLaunchRequest
{
    public string StorageScenarioId { get; set; }
    public SaveEntry StartupSave { get; set; }
    public SaveManager.SaveType SaveType { get; set; }
    public string TargetLabel { get; set; }
    public ScenarioBaseGameMode BaseGameMode { get; set; }
    public ScenarioDefinition Definition { get; set; }
}

public enum ScenarioRuntimeIdentityKind
{
    None = 0,
    SceneSpritePlacement = 1,
    ObjectPlacement = 2
}

public sealed class ScenarioRuntimeIdentity
{
    public ScenarioRuntimeIdentityKind Kind { get; }
    public string PlacementId { get; }
    public string ScenarioObjectId { get; }
    public string RuntimeBindingKey { get; }
    public int GridX { get; }
    public int GridY { get; }
}

public sealed class ScenarioRuntimeSpriteTarget
{
    public string TargetPath { get; }
    public Transform Transform { get; }
    public ScenarioSpriteTargetComponentKind Kind { get; }
    public SpriteRenderer SpriteRenderer { get; }
    public UI2DSprite Ui2DSprite { get; }
    public ParticleSystemRenderer ParticleRenderer { get; }
    public bool IsAlive { get; }
    public Sprite CurrentSprite { get; }
    public string SpriteName { get; }
    public string TextureName { get; }
}

public sealed class ScenarioStationUpgradeSnapshot
{
    public string ObjectType { get; }
    public int Level { get; }
    public int MinLevel { get; }
    public int MaxLevel { get; }
    public ScenarioStationUpgradePathSnapshot[] Paths { get; }
    public ScenarioStationStatSnapshot[] Stats { get; }
}

public sealed class ScenarioStationUpgradePathSnapshot
{
    public string Name { get; }
    public int Level { get; }
    public int CurrentLevel { get; }
    public int MaxLevel { get; }
}

public sealed class ScenarioStationStatSnapshot
{
    public string Name { get; }
    public string Label { get; }
    public float Value { get; }
    public float MinValue { get; }
    public float MaxValue { get; }
    public float Step { get; }
    public bool HasOverride { get; }
    public string Detail { get; }
}

public sealed class ScenarioPreviewResult
{
    public bool Started { get; }
    public int RuntimeRevision { get; }
    public int FamilyChanges { get; }
    public int InventoryChanges { get; }
    public int BunkerChanges { get; }
    public int TriggerChanges { get; }
    public int ConditionChanges { get; }
    public int SpriteSwapChanges { get; }
    public int MapChanges { get; }
    public int ScenePlacementChanges { get; }
    public string[] Messages { get; }
}

public sealed class ScenarioRuntimeSnapshot
{
    public string ScenarioId { get; }
    public string ScenarioVersion { get; }
    public string RuntimeBindingId { get; }
    public string Outcome { get; }
    public string OutcomeConditionId { get; }
    public int LastProcessedDay { get; }
    public int LastProcessedHour { get; }
    public int LastProcessedMinute { get; }
    public ScenarioRuntimeActionSnapshot[] Actions { get; }
    public ScenarioRuntimeFlagSnapshot[] Flags { get; }
}

public sealed class ScenarioRuntimeActionSnapshot
{
    public string ActionKey { get; }
    public string ActionType { get; }
    public int Day { get; }
    public int Hour { get; }
    public int Minute { get; }
    public string Status { get; }
    public string Message { get; }
}

public sealed class ScenarioRuntimeFlagSnapshot
{
    public string Id { get; }
    public string Value { get; }
}

public sealed class ScenarioRuntimeExecutionEntrySnapshot
{
    public int Day { get; }
    public int Hour { get; }
    public int Minute { get; }
    public string ElementId { get; }
    public string DisplayName { get; }
    public string Kind { get; }
    public string Outcome { get; }
    public string ConditionSummary { get; }
    public string Detail { get; }
    public string PlainLanguage { get; }
}

public sealed class ScenarioMapLootEntrySnapshot
{
    public string ItemId { get; }
    public int Quantity { get; }
    public bool Hidden { get; }
    public string HiddenUnlockItemId { get; }
}

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
    public ScenarioLaunchSetupDefinition LaunchSetup { get; set; }
    public StartingInventoryDefinition StartingInventory { get; set; }
    public BunkerEditsDefinition BunkerEdits { get; set; }
    public WinLossConditionsDefinition WinLossConditions { get; set; }
    public ScenarioScoringDefinition Scoring { get; set; }
    public AssetReferencesDefinition AssetReferences { get; set; }
}

public class ScenarioScoringDefinition
{
    public bool Enabled { get; set; }
    public string ScoreLabel { get; set; }
    public bool HigherIsBetter { get; set; }
    public string LeaderboardKey { get; set; }
    public List<ScenarioScoreCategoryDefinition> Categories { get; }
    public List<ScenarioScoreRuleDefinition> Rules { get; }
    public List<ScenarioProperty> Metadata { get; }
}

public class ScenarioScoreCategoryDefinition
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public int SortOrder { get; set; }
}

public class ScenarioScoreRuleDefinition
{
    public string Id { get; set; }
    public string CategoryId { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Source { get; set; }
    public string Operation { get; set; }
    public string OutcomeFilter { get; set; }
    public float Weight { get; set; }
    public List<ScenarioProperty> Properties { get; }
}

public enum ScenarioScoreCompletionState
{
    Unknown = 0,
    InProgress = 1,
    Won = 2,
    Lost = 3,
    Completed = 4,
    Failed = 5,
    Abandoned = 6
}

public class ScenarioScoreSnapshot
{
    public string ScenarioId { get; set; }
    public string ScenarioVersion { get; set; }
    public string RuntimeBindingId { get; set; }
    public ScenarioScoreCompletionState CompletionState { get; set; }
    public string Outcome { get; set; }
    public string OutcomeConditionId { get; set; }
    public bool HasTotalScore { get; set; }
    public int TotalScore { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public List<ScenarioScoreCategorySnapshot> Categories { get; }
    public List<ScenarioScoreRuleSnapshot> Rules { get; }
    public List<ScenarioProperty> Metadata { get; }
}

public class ScenarioScoreCategorySnapshot
{
    public string CategoryId { get; set; }
    public string DisplayName { get; set; }
    public int Score { get; set; }
}

public class ScenarioScoreRuleSnapshot
{
    public string RuleId { get; set; }
    public string CategoryId { get; set; }
    public string DisplayName { get; set; }
    public string Source { get; set; }
    public float Value { get; set; }
    public int Score { get; set; }
}
```

## Mod Registry (`ModAPI.Core`)

```csharp
// ModAPI.Core.ModRegistry
public static bool Find(string modId);
public static ModEntry GetMod(string modId);
public static bool TryGetMod(string modId, out ModEntry entry);
public static List<string> GetLoadedModIds();
```

## ShelteredAPI Trigger Scheduler (`ShelteredAPI.Events`)

```csharp
public enum TimeTriggerCadence { SixHour = 1, Staggered = 2, Both = 3 }
public enum TimeTriggerKind { SixHour = 1, Staggered = 2 }

public static class ShelteredEvents
{
    public static event Action<TimeTriggerBatch> SixHourTick;
    public static event Action<TimeTriggerBatch> StaggeredTick;

    public static int StaggeredMinHours { get; }
    public static int StaggeredMaxHours { get; }

    public static void RegisterTimeTrigger(string triggerId);
    public static void RegisterTimeTrigger(string triggerId, int priority);
    public static void RegisterTimeTrigger(string triggerId, int priority, TimeTriggerCadence cadence);
    public static void RegisterTimeTrigger(string triggerId, int priority, TimeTriggerCadence cadence, Action<TimeTriggerBatch> callback);
    public static bool UnregisterTimeTrigger(string triggerId);
    public static List<TimeTriggerInfo> GetTimeTriggerPriorityList(TimeTriggerCadence cadence);
    public static void ConfigureStaggeredTimeRange(int minInclusiveHours, int maxInclusiveHours);
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

## Background Work (SMM 2.0)

**Status:** Current neutral API. Background delegates must not touch Unity objects; result and error continuations execute on the Unity main thread through `PluginRunner`.

```csharp
// ModAPI.Core
public enum ModThreadStaleResultPolicy
{
    DeliverAll = 0,
    SkipIfSuperseded = 1,
    CancelPreviousAndSkip = 2
}

public enum ScenarioLaunchSetupMode { FullSetup, Direct, Guided }

public sealed class ScenarioLaunchSetupDefinition
{
    public ScenarioLaunchSetupMode Mode { get; set; }
    public List<ScenarioDifficultyCategoryDefinition> Categories { get; }
}

public sealed class ScenarioDifficultyCategoryDefinition
{
    public string Id { get; set; }
    public int AuthoredValue { get; set; }
    public bool PlayerSelectable { get; set; }
}

public sealed class ModThreadOptions
{
    public string SourceId { get; set; }
    public string WorkKey { get; set; }
    public ModThreadStaleResultPolicy StaleResultPolicy { get; set; }
    public int MaxConcurrentPerSource { get; set; }
    public ModThreadOptions();
}

public sealed class ModThreadHandle
{
    public string SourceId { get; }
    public string WorkKey { get; }
    public bool IsCancellationRequested { get; }
    public bool IsRunning { get; }
    public bool IsCompleted { get; }
    public bool WasCanceled { get; }
    public bool WasStale { get; }
    public Exception Error { get; }
    public void Cancel();
}

public sealed class ModThreadSourceReport
{
    public string SourceId { get; }
    public int InFlight { get; }
    public int Waiting { get; }
}

public sealed class ModThreadDiagnosticsReport
{
    public long Queued { get; }
    public long Running { get; }
    public long Completed { get; }
    public long Canceled { get; }
    public long Failed { get; }
    public long StaleSkipped { get; }
    public long Throttled { get; }
    public int Active { get; }
    public int Waiting { get; }
    public ModThreadSourceReport[] Sources { get; }
}

public static void RunAsync(Action action);
public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread);
public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError);
public static ModThreadHandle RunAsync(Action action, ModThreadOptions options);
public static ModThreadHandle RunAsync(Action<ModThreadHandle> action, ModThreadOptions options);
public static ModThreadHandle RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread, ModThreadOptions options);
public static ModThreadHandle RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError, ModThreadOptions options);
public static ModThreadHandle RunAsync<TResult>(Func<ModThreadHandle, TResult> work, Action<TResult> onMainThread, ModThreadOptions options);
public static ModThreadHandle RunAsync<TResult>(Func<ModThreadHandle, TResult> work, Action<TResult> onMainThread, Action<Exception> onError, ModThreadOptions options);
public static ModThreadDiagnosticsReport GetDiagnostics();

// ModAPI.Core.ModManagerBase
protected void RunInBackground<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError = null);
```

`SourceId` scopes `WorkKey` and per-source limits. Use the same `SourceId` and `WorkKey` with `SkipIfSuperseded` when only the newest calculated result should apply; use `CancelPreviousAndSkip` when an older delegate can also stop cooperatively by checking its handle. `MaxConcurrentPerSource` is zero for unlimited dispatch and otherwise defers excess submissions until a source slot becomes available.

## Persistence And Sheltered Saves (`ModAPI.Core`, `ShelteredAPI.Saves`)

```csharp
// ModAPI.dll
public interface ISaveSystem
{
    string GetCurrentSlotPath();
    int ActiveSlotIndex { get; }
    void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class;
}

// ShelteredAPI.dll
public static class ShelteredPersistence
{
    public static ShelteredPersistentList<T> CreateList<T>(string uniqueId);
    public static ShelteredPersistentDictionary<TValue> CreateDictionary<TValue>(string uniqueId);
}

public class ShelteredPersistentList<T> : List<T>, ISaveable
{
    public ShelteredPersistentList(string uniqueId);
}

public class ShelteredPersistentDictionary<TValue> : Dictionary<string, TValue>, ISaveable
{
    public ShelteredPersistentDictionary(string uniqueId);
}

public static class ShelteredSaves
{
    public static SaveEntry[] ListStandard(int page, int pageSize);
    public static SaveEntry[] ListScenario(string scenarioId, int page, int pageSize);
    public static SaveEntry GetStandard(string saveId);
    public static SaveEntry GetScenario(string scenarioId, string saveId);
}
```

## Save Lifecycle (SMM 2.0)

**Status:** Current neutral API. These optional lifecycle hooks operate on registered mod data; save-slot routing remains host-owned through `ISaveRuntimeAdapter`.

```csharp
// ModAPI.Persistence
public interface IModPersistenceLogic
{
    void OnLoaded(IModSaveContext context);
    void OnSaving(IModSaveContext context);
}

public interface IModPersistenceLifecycle
{
    void PrepareForSave(IModSaveContext context);
    void RestoreAfterLoad(IModSaveContext context);
    bool ValidateAfterLoad(IModSaveContext context, out string diagnosticMessage);
}
```

`IModPersistenceLogic` is retained for compatibility: `OnSaving` runs before normal serialization and `OnLoaded` runs for successfully deserialized registered data. `IModPersistenceLifecycle` is additive: `RestoreAfterLoad` and `ValidateAfterLoad` run once per active save context for data that was loaded, successfully migrated through `RegisterModData`, or reset to its registered defaults. Implementing both interfaces invokes both contracts.

The neutral save system reports per registered key in its diagnostics: loaded, missing, migrated, defaulted, validation passed/failed, skipped because no active save context exists, failed serialization/deserialization, and callback failure. It does not expose mutable Sheltered save-manager state.

## ModRandom Deterministic Streams (`ModAPI.Core`)

**Status:** Current neutral API. `ModRandom` is the canonical deterministic random service; do not introduce a parallel random facade.

```csharp
public enum RandomnessMode { XorShift, Legacy }

public static class ModRandom
{
    public static event Action OnSeedChanged;
    public static bool IsDeterministic { get; set; }
    public static int CurrentSeed { get; }
    public static ulong CurrentStep { get; }
    public static bool IsInitialized { get; }

    public static void Initialize(int seed, RandomnessMode mode = RandomnessMode.XorShift);
    public static void ResetForSaveSeed(int seed, RandomnessMode mode = RandomnessMode.XorShift);
    public static void FastForward(ulong steps);
    public static int Range(int minInclusive, int maxExclusive);
    public static float Range(float min, float max);
    public static int RangeUnbiased(int min, int max);
    public static float Value();
    public static double ValueDouble();
    public static bool Bool(float probability);
    public static bool Bool();
    public static T Choose<T>(params T[] items);
    public static void Shuffle<T>(IList<T> list);
    public static float Gaussian(float mean, float stdDev);
    public static T Weighted<T>(T[] items, float[] weights);
    public static ModRandomStream GetStream(string streamName);
    public static ModRandomStream GetStream(string modId, string featureId);
}

public class ModRandomStream
{
    public ModRandomStream(int seed);
    public int Range(int min, int max);
    public float Range(float min, float max);
    public float Value();
    public bool Bool(float probability = 0.5f);
    public T Choose<T>(params T[] items);
    public void Shuffle<T>(IList<T> list);
    public ulong CurrentStep { get; }
}
```

`GetStream(modId, featureId)` produces a stable feature stream isolated from unrelated named-stream consumption. `ResetForSaveSeed` restarts the master sequence, clears named streams, and raises `OnSeedChanged`; exact deterministic restoration rebinds listeners to snapshotted stream instances. `ModRandomState` is internal persistence machinery and is not a mod-author contract. Diagnostics log reset, stream creation, snapshot, and restore boundaries rather than individual draws.

## Expedition Map Context (SMM 2.0)

**Status:** Current Sheltered-owned read-only runtime context, deterministic generation-policy intent, and home-shelter provider surface.

```csharp
// ShelteredAPI.Map
public struct ExpeditionMapGridPosition
{
    public ExpeditionMapGridPosition(int x, int y);
    public int X { get; }
    public int Y { get; }
}

public struct ExpeditionMapWorldPosition
{
    public ExpeditionMapWorldPosition(float x, float y);
    public float X { get; }
    public float Y { get; }
}

public sealed class ExpeditionRouteDistance
{
    public float WorldUnits { get; }
    public float Miles { get; }
    public bool IncludesHomeLegs { get; }
}

public sealed class ExpeditionMapContext
{
    public bool IsAvailable { get; }
    public bool IsValid { get; }
    public string UnavailableReason { get; }
    public int CurrentWidth { get; }
    public int CurrentHeight { get; }
    public int VanillaWidth { get; }
    public int VanillaHeight { get; }
    public float ScaleFactor { get; }
    public bool HasScaleFactor { get; }
    public float DensityMultiplier { get; }
    public bool HasDensityMultiplier { get; }
    public int MapSeed { get; }
    public bool HasMapSeed { get; }
    public ExpeditionMapWorldPosition HomeShelterWorldPosition { get; }
    public ExpeditionMapGridPosition HomeShelterGridPosition { get; }
    public bool HasHomeShelterPosition { get; }
    public float WorldUnitsPerMile { get; }
    public bool HasWorldUnitsPerMile { get; }
    public bool ContainsGridPosition(ExpeditionMapGridPosition position);
    public bool TryWorldToGrid(ExpeditionMapWorldPosition position, out ExpeditionMapGridPosition gridPosition);
    public bool TryGridToWorld(ExpeditionMapGridPosition gridPosition, out ExpeditionMapWorldPosition worldPosition);
    public bool TryGridToWorldCenter(ExpeditionMapGridPosition gridPosition, out ExpeditionMapWorldPosition worldPosition);
    public bool TryCalculateDistance(ExpeditionMapWorldPosition from, ExpeditionMapWorldPosition to, out ExpeditionRouteDistance distance);
    public bool TryCalculateRouteDistance(IList<ExpeditionMapWorldPosition> waypoints, bool includeHomeLegs, out ExpeditionRouteDistance distance);
}

public abstract class ExpeditionMapGenerationPolicy
{
    public string SourceId { get; set; }
    public string PolicyId { get; set; }
    public int Priority { get; set; }
}

public sealed class LocationDensityPolicy : ExpeditionMapGenerationPolicy { public LocationDensityPolicy(); public LocationDensityPolicy(string sourceId, string policyId, float multiplier, int priority); public float Multiplier { get; set; } }
public sealed class TownDensityPolicy : ExpeditionMapGenerationPolicy { public TownDensityPolicy(); public TownDensityPolicy(string sourceId, string policyId, float multiplier, int priority); public float Multiplier { get; set; } }
public sealed class QuestPlacementPolicy : ExpeditionMapGenerationPolicy { public QuestPlacementPolicy(); public QuestPlacementPolicy(string sourceId, string policyId, int minimumHomeDistanceInCells, int? maximumHomeDistanceInCells, int priority); public int MinimumHomeDistanceInCells { get; set; } public int? MaximumHomeDistanceInCells { get; set; } }
public sealed class FactionZonePlacementPolicy : ExpeditionMapGenerationPolicy { public FactionZonePlacementPolicy(); public FactionZonePlacementPolicy(string sourceId, string policyId, int minimumHomeDistanceInCells, int? maximumHomeDistanceInCells, int priority); public int MinimumHomeDistanceInCells { get; set; } public int? MaximumHomeDistanceInCells { get; set; } }
public sealed class HomeShelterPlacementPolicy : ExpeditionMapGenerationPolicy { public HomeShelterPlacementPolicy(); public HomeShelterPlacementPolicy(string sourceId, string policyId, ExpeditionMapGridPosition? preferredGridPosition, int minimumEdgeDistanceInCells, int priority); public ExpeditionMapGridPosition? PreferredGridPosition { get; set; } public int MinimumEdgeDistanceInCells { get; set; } }
public sealed class SpecialItemRegionEligibilityPolicy : ExpeditionMapGenerationPolicy { public SpecialItemRegionEligibilityPolicy(); public SpecialItemRegionEligibilityPolicy(string sourceId, string policyId, int minimumHomeDistanceInCells, int? maximumHomeDistanceInCells, int priority); public int MinimumHomeDistanceInCells { get; set; } public int? MaximumHomeDistanceInCells { get; set; } }

public sealed class MapPolicyRegistrationResult
{
    public bool Success { get; }
    public bool ReplacedExisting { get; }
    public string ErrorMessage { get; }
}

public sealed class MapGenerationPolicySnapshot
{
    public int PolicyCount { get; }
    public float LocationDensityMultiplier { get; }
    public float TownDensityMultiplier { get; }
    public int QuestMinimumHomeDistanceInCells { get; }
    public int? QuestMaximumHomeDistanceInCells { get; }
    public int FactionZoneMinimumHomeDistanceInCells { get; }
    public int? FactionZoneMaximumHomeDistanceInCells { get; }
    public int HomeShelterMinimumEdgeDistanceInCells { get; }
    public bool HasPreferredHomeShelterGridPosition { get; }
    public ExpeditionMapGridPosition PreferredHomeShelterGridPosition { get; }
    public int SpecialItemMinimumHomeDistanceInCells { get; }
    public int? SpecialItemMaximumHomeDistanceInCells { get; }
    public bool HasPolicyConflicts { get; }
    public string PolicyConflictSummary { get; }
    public bool IsQuestPlacementEligible(ExpeditionMapGridPosition home, ExpeditionMapGridPosition candidate);
    public bool IsFactionZonePlacementEligible(ExpeditionMapGridPosition home, ExpeditionMapGridPosition candidate);
    public bool IsSpecialItemRegionEligible(ExpeditionMapGridPosition home, ExpeditionMapGridPosition candidate);
    public bool IsHomeShelterPlacementEligible(ExpeditionMapGridPosition candidate, int mapWidth, int mapHeight);
    public bool IsPreferredHomeShelterPlacementEligible(int mapWidth, int mapHeight);
}

public interface IHomeShelterPlacementProvider
{
    bool TryResolve(HomeShelterPlacementContext context, out HomeShelterPlacementResult result);
}

public sealed class HomeShelterPlacementProviderRegistration
{
    public HomeShelterPlacementProviderRegistration();
    public string SourceId { get; set; }
    public string ProviderId { get; set; }
    public int Priority { get; set; }
    public IHomeShelterPlacementProvider Provider { get; set; }
    public IHomeShelterPlacementResolutionListener ResolutionListener { get; set; }
}

public interface IHomeShelterPlacementResolutionListener
{
    void OnHomeShelterPlacementResolved(HomeShelterPlacementResolution resolution);
}

public sealed class HomeShelterPlacementResolution
{
    public string SourceId { get; }
    public string ProviderId { get; }
    public string RequestReason { get; }
    public HomeShelterPositionSnapshot Snapshot { get; }
}

public sealed class HomeShelterPlacementContext
{
    public int MapWidth { get; }
    public int MapHeight { get; }
    public float WorldWidth { get; }
    public float WorldHeight { get; }
    public bool FromLiveMap { get; }
    public MapGenerationPolicySnapshot Policies { get; }
    public bool IsHomeShelterPlacementEligible(ExpeditionMapGridPosition gridPosition);
    public bool IsInsideMap(ExpeditionMapGridPosition gridPosition);
    public bool TryWorldToGrid(ExpeditionMapWorldPosition worldPosition, out ExpeditionMapGridPosition gridPosition);
    public bool TryGridToWorldCenter(ExpeditionMapGridPosition gridPosition, out ExpeditionMapWorldPosition worldPosition);
}

public sealed class HomeShelterPlacementResult
{
    public HomeShelterPlacementResult();
    public string HomeId { get; set; }
    public string DisplayName { get; set; }
    public int OwnerId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public bool IsOnline { get; set; }
    public bool GenerateStartingLocations { get; set; }
    public ExpeditionMapWorldPosition? WorldPosition { get; set; }
    public ExpeditionMapGridPosition? GridPosition { get; set; }
    public ExpeditionMapPixelPosition? MapPosition { get; set; }
    public int MinimumEdgeDistanceInCells { get; set; }
    public string SourceReason { get; set; }
}

public sealed class HomeShelterPositionRegistration
{
    public HomeShelterPositionRegistration();
    public string SourceId { get; set; }
    public string HomeId { get; set; }
    public string DisplayName { get; set; }
    public int OwnerId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public bool IsOnline { get; set; }
    public bool GenerateStartingLocations { get; set; }
    public int MinimumEdgeDistanceInCells { get; set; }
    public int Priority { get; set; }
    public ExpeditionMapWorldPosition? WorldPosition { get; set; }
    public ExpeditionMapGridPosition? GridPosition { get; set; }
    public ExpeditionMapPixelPosition? MapPosition { get; set; }
    public string SourceReason { get; set; }
}

public sealed class HomeShelterPositionSnapshot
{
    public string SourceId { get; }
    public string HomeId { get; }
    public string DisplayName { get; }
    public int OwnerId { get; }
    public bool IsPrimary { get; }
    public bool IsActive { get; }
    public bool IsVisible { get; }
    public bool IsOnline { get; }
    public bool GenerateStartingLocations { get; }
    public int MinimumEdgeDistanceInCells { get; }
    public int Priority { get; }
    public bool HasWorldPosition { get; }
    public ExpeditionMapWorldPosition WorldPosition { get; }
    public bool HasGridPosition { get; }
    public ExpeditionMapGridPosition GridPosition { get; }
    public bool HasMapPosition { get; }
    public ExpeditionMapPixelPosition MapPosition { get; }
    public string SourceReason { get; }
}

public static class ShelteredMap
{
    public static ExpeditionMapContext Current { get; }
    public static ExpeditionMapContext GetCurrentContext();
    public static MapPolicyRegistrationResult RegisterLocationDensityPolicy(LocationDensityPolicy policy);
    public static MapPolicyRegistrationResult RegisterTownDensityPolicy(TownDensityPolicy policy);
    public static MapPolicyRegistrationResult RegisterQuestPlacementPolicy(QuestPlacementPolicy policy);
    public static MapPolicyRegistrationResult RegisterFactionZonePlacementPolicy(FactionZonePlacementPolicy policy);
    public static MapPolicyRegistrationResult RegisterHomeShelterPlacementPolicy(HomeShelterPlacementPolicy policy);
    public static MapPolicyRegistrationResult RegisterHomeShelterPlacementProvider(HomeShelterPlacementProviderRegistration registration);
    public static int UnregisterHomeShelterPlacementProvider(string sourceId, string providerId);
    public static int ClearHomeShelterPlacementProviders(string sourceId);
    public static bool TryResolveHomeShelterPlacement(string reason, out HomeShelterPositionSnapshot snapshot);
    public static MapPolicyRegistrationResult RegisterHomeShelterPosition(HomeShelterPositionRegistration registration);
    public static int UnregisterHomeShelterPosition(string sourceId, string homeId);
    public static int ClearHomeShelterPositions(string sourceId);
    public static bool TryGetPrimaryHomeShelter(out HomeShelterPositionSnapshot snapshot);
    public static bool TryGetActiveHomeShelter(out HomeShelterPositionSnapshot snapshot);
    public static bool TryWorldToGrid(ExpeditionMapWorldPosition worldPosition, out ExpeditionMapGridPosition gridPosition);
    public static bool TryGridToWorldCenter(ExpeditionMapGridPosition gridPosition, out ExpeditionMapWorldPosition worldPosition);
    public static bool TryWorldToMapPixels(ExpeditionMapWorldPosition worldPosition, out ExpeditionMapPixelPosition mapPosition);
    public static bool TryMapPixelsToWorld(ExpeditionMapPixelPosition mapPosition, out ExpeditionMapWorldPosition worldPosition);
    public static MapPolicyRegistrationResult RegisterSpecialItemRegionEligibilityPolicy(SpecialItemRegionEligibilityPolicy policy);
    public static int UnregisterPolicy(string sourceId, string policyId);
    public static int ClearPolicies(string sourceId);
    public static MapGenerationPolicySnapshot ResolveGenerationPolicies();
}
```

`ShelteredMap.Current` reads `ExpeditionMap` and `ExplorationManager` internally and returns an unavailable/invalid snapshot rather than leaking a runtime object or throwing before a generated map exists. `VanillaWidth` and `VanillaHeight` are the vanilla normal-map baseline (`40 x 16`); `DensityMultiplier` is explicitly unavailable because vanilla exposes resource and faction difficulty knobs, not one authoritative generated-location density value.

Policy registrations declare intent and resolve in `Priority`, `SourceId`, then `PolicyId` order. Registration copies the submitted DTO, so later caller mutations cannot reorder or alter registered intent. An empty snapshot is vanilla/no-op behavior. ShelteredAPI owns the shared `ExpeditionMap` generation hooks for active home-shelter placement, shelter cell stamping, starter-location placement, origin-to-home grid conversion, and home-region sanitization; mods provide placement intent instead of patching those callsites directly. Providers that choose randomized positions must use `ModRandom` deterministic streams.

Home-shelter placement providers register `IHomeShelterPlacementProvider` implementations with `RegisterHomeShelterPlacementProvider(...)`. `TryResolveHomeShelterPlacement(...)` asks registered providers to publish generation intent, the primary/active shelter snapshot, and any available world, grid, or map-pixel coordinates through the shared `ShelteredMap` surface. `ResolutionListener` is optional and exists for providers that need to sync save-compatibility state after ShelteredAPI accepts their placement. Direct `RegisterHomeShelterPosition(...)` remains available for integrations that already own a resolved home fact. Consumers should prefer `TryGetPrimaryHomeShelter(...)` for player-home lookups and `TryGetActiveHomeShelter(...)` for current-context integrations, then check `HasWorldPosition`, `HasGridPosition`, or `HasMapPosition` before reading the matching coordinate. RBP is an example provider: other mods should consume the shared `ShelteredMap` snapshot rather than a provider-specific API.

## Player Queues (SMM 2.0)

**Status:** Current Sheltered-owned queue query, snapshot, conservative restore, and change-notification surface. Capacity is observed metadata only.

```csharp
// ShelteredAPI.Queues
public enum PlayerQueueEntryState { Pending, InTransit, Started, Finished, Unknown }
public enum PlayerQueueCancelState { Active, Cancelled, ForceCancelled, Unknown }
public enum PlayerQueueChangeKind { Added, Removed, ClearedOrCancelled, Reordered, Restored }

public sealed class PlayerQueueOwnerIdentity
{
    public ActorId ActorId { get; }
    public int UniqueMemberId { get; }
    public string DisplayName { get; }
}

public sealed class PlayerQueuePosition
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
}

public sealed class PlayerQueueEntry
{
    public int Index { get; }
    public string JobType { get; }
    public string InteractionType { get; }
    public PlayerQueueEntryState State { get; }
    public PlayerQueueCancelState CancelState { get; }
    public PlayerQueuePosition Target { get; }
    public int TargetObjectId { get; }
}

public sealed class PlayerQueueSnapshot
{
    public bool IsAvailable { get; }
    public PlayerQueueOwnerIdentity Owner { get; }
    public int Capacity { get; }
    public int Count { get; }
    public bool IsFull { get; }
    public IList<PlayerQueueEntry> Entries { get; }
    public string UnavailableReason { get; }
    public string RestoreBlockReason { get; }
    public bool CanRestore { get; }
}

public sealed class PlayerQueueRestoreResult
{
    public bool Success { get; }
    public string Message { get; }
    public PlayerQueueSnapshot Queue { get; }
}

public sealed class PlayerQueueChangedEventArgs : EventArgs
{
    public PlayerQueueChangeKind ChangeKind { get; }
    public PlayerQueueSnapshot Queue { get; }
    public PlayerQueueOwnerIdentity Owner { get; }
}

public static class ShelteredQueues
{
    public static event Action<PlayerQueueChangedEventArgs> QueueChanged;
    public static PlayerQueueSnapshot GetPlayerQueue(ActorId owner);
    public static PlayerQueueSnapshot GetPlayerQueue(ICharacterProxy owner);
    public static PlayerQueueSnapshot GetPlayerQueue(int uniqueMemberId);
    public static PlayerQueueSnapshot SnapshotQueue(ActorId owner);
    public static PlayerQueueSnapshot SnapshotQueue(ICharacterProxy owner);
    public static PlayerQueueSnapshot SnapshotQueue(int uniqueMemberId);
    public static PlayerQueueRestoreResult RestoreQueue(PlayerQueueSnapshot snapshot);
}
```

Snapshots copy entry data and actor identity and do not expose live `Job`, `Obj_Base`, or `FamilyMember` objects. `GetPlayerQueue` is a metadata query; `SnapshotQueue` adds private restore material only for pending base-interaction or movement jobs that do not carry staged external work. `RestoreQueue` requires an empty live player queue and matching observed capacity. It does not set player queue capacity.

## UI Extensions (SMM 2.0)

**Status:** Current focused helper API for deliberate augmentation of existing Sheltered/NGUI objects.

Use `ShelteredUI.CloneElement(...)` when a mod intentionally reuses a vanilla visual template. Safe defaults clear inherited `UIEventListener` delegates and `UIButton.onClick` callbacks without creating a layout framework. Use explicit `UIButtonBindingMode.Replace` or `Append`, and the generic binding overload when each cloned row/button needs its own item context. `UIColorSnapshot` captures label/widget visible colors and `TweenColor` endpoints/current values for later restoration. `SubscribePanelLifecycle(...)` returns an `IDisposable` subscription suitable for restoring state on panel close. Existing `UITakeoverSession.BindTooltip(...)` now releases its hover tooltip binding when `Restore()` is called.

## Patch Diagnostics (`ModAPI.Harmony`)

**Status:** Current neutral patch-registry report surface. Reports are support-bundle-friendly snapshots retained after startup.

```csharp
public enum PatchDomain
{
    Unknown, Bootstrap, SaveFlow, UI, Input, Content, Diagnostics,
    Events, Interactions, Characters, World, Scenarios
}

public enum PatchStartupTiming
{
    BootCritical, MenuCritical, SaveFlowCritical, GameplayDeferred, EditorDeferred, DebugDeferred
}

public enum PatchConflictSeverity { Informational, Warning }

public sealed class PatchPolicyAttribute : Attribute
{
    public PatchPolicyAttribute(PatchDomain domain, string feature);
    public PatchDomain Domain { get; }
    public string Feature { get; }
    public string TargetBehavior { get; set; }
    public string FailureMode { get; set; }
    public string RollbackStrategy { get; set; }
    public bool IsOptional { get; set; }
    public bool DeveloperOnly { get; set; }
    public string ManagerToggleId { get; set; }
    public string ManagerToggleLabel { get; set; }
    public string ManagerToggleDescription { get; set; }
    public bool ManagerToggleDefault { get; set; }
    public bool ManagerToggleRequiresRestart { get; set; }
    public int ManagerToggleSortOrder { get; set; }
    public PatchStartupTiming StartupTiming { get; set; }
}

public sealed class PatchRegistryOptions
{
    public HarmonyUtil.PatchOptions PatchOptions { get; set; }
    public HashSet<PatchDomain> DisabledDomains { get; }
    public HashSet<PatchStartupTiming> IncludedStartupTimings { get; }
    public bool IncludeOptionalPatches { get; set; }
    public string SourceName { get; set; }
    public string TriggerName { get; set; }
}

public sealed class PatchHostReportDto
{
    public string PatchAssemblyName { get; set; }
    public string PatchHostName { get; set; }
    public string SourceName { get; set; }
    public PatchDomain Domain { get; set; }
    public string OwningFeature { get; set; }
    public string TargetBehavior { get; set; }
    public string FailureMode { get; set; }
    public string RollbackStrategy { get; set; }
    public PatchStartupTiming StartupTiming { get; set; }
    public string[] TargetMethods { get; set; }
    public bool HasExplicitPolicy { get; set; }
    public bool IsOptional { get; set; }
    public bool DeveloperOnly { get; set; }
    public bool IsDangerous { get; set; }
}

public sealed class PatchConflictReportDto
{
    public string TargetMethod { get; set; }
    public PatchConflictSeverity Severity { get; set; }
    public string Reason { get; set; }
    public PatchHostReportDto[] PatchHosts { get; set; }
}

public sealed class PatchReportDto
{
    public DateTime CapturedUtc { get; set; }
    public string AssemblyName { get; set; }
    public string SourceName { get; set; }
    public string TriggerName { get; set; }
    public PatchHostReportDto[] Discovered { get; set; }
    public PatchHostReportDto[] Applied { get; set; }
    public PatchHostReportDto[] Skipped { get; set; }
    public PatchHostReportDto[] MissingPolicy { get; set; }
    public PatchConflictReportDto[] Conflicts { get; set; }
}

public sealed class PatchRecord
{
    public Type PatchType;
    public PatchDomain Domain;
    public string Feature;
    public string TargetBehavior;
    public string FailureMode;
    public string RollbackStrategy;
    public bool IsOptional;
    public bool DeveloperOnly;
    public bool IsDangerous;
    public bool HasExplicitPolicy;
    public PatchStartupTiming StartupTiming;
    public List<MethodBase> Targets;
    public string ManagerToggleId;
    public string ManagerToggleLabel;
    public string ManagerToggleDescription;
    public bool ManagerToggleDefault;
    public bool ManagerToggleRequiresRestart;
    public int ManagerToggleSortOrder;
}

public static class PatchRegistry
{
    public static PatchApplyReport ApplyAssembly(HarmonyLib.Harmony harmony, Assembly assembly, PatchRegistryOptions options);
    public static bool ApplyManualModule(HarmonyLib.Harmony harmony, Type moduleType, Action applyAction, PatchRegistryOptions options);
    public static PatchRegistryOptions CreateManagerOptions(HarmonyUtil.PatchOptions patchOptions, string sourceName, Func<string, string> readString);
    public static PatchRegistryOptions CreateTimingOptions(PatchRegistryOptions source, params PatchStartupTiming[] timings);
    public static void ApplyDisabledDomains(HashSet<PatchDomain> domains, string raw);
    public static PatchReportDto[] GetReportHistory();
    public static PatchReportDto GetLatestReport();
}

public sealed class PatchApplyReport
{
    public readonly List<PatchRecord> Discovered;
    public readonly List<PatchRecord> Applied;
    public readonly List<PatchRecord> Skipped;
    public readonly List<PatchRecord> MissingPolicy;
    public readonly List<PatchConflictReportDto> Conflicts;
    public PatchReportDto DiagnosticSnapshot { get; }
}
```

`PatchRegistry` retains the latest 64 snapshot reports. Duplicate target conflicts are classified as informational or warning based on declared policy, domain, feature ownership, and optional status; they never block patch application.

## Map Markers (SMM 2.0)

**Status:** Implemented in `ShelteredAPI.Map`. `MapMarkerSnapshot.Kind` reuses `ShelteredAPI.Scenarios.Domain.Map.MapMarkerKind`; this facade does not define map-generation policy.

```csharp
public struct ExpeditionMapPixelPosition
{
    public ExpeditionMapPixelPosition(float x, float y);
    public float X { get; }
    public float Y { get; }
}

public sealed class ExpeditionRouteSnapshot
{
    public ExpeditionRouteSnapshot(IEnumerable<ExpeditionMapWorldPosition> worldWaypoints);
    public ReadOnlyCollection<ExpeditionMapWorldPosition> WorldWaypoints { get; }
}

public sealed class MapMarkerSnapshot
{
    public MapMarkerSnapshot();
    public string MarkerId { get; set; }
    public string DisplayName { get; set; }
    public MapMarkerKind Kind { get; set; }
    public ActorId ActorId { get; set; }
    public ExpeditionMapPixelPosition? MapPosition { get; set; }
    public ExpeditionMapGridPosition? GridPosition { get; set; }
    public ExpeditionMapWorldPosition? WorldPosition { get; set; }
    public bool IsVisible { get; set; }
    public bool IsDiscovered { get; set; }
    public string SourceModId { get; set; }
    public ExpeditionRouteSnapshot Route { get; set; }
}

public sealed class ExpeditionActorSnapshot
{
    public ExpeditionPartyInfo PartyInfo { get; }
    public ReadOnlyCollection<ActorId> MemberActorIds { get; }
    public MapMarkerSnapshot Marker { get; }
    public ExpeditionRouteSnapshot Route { get; }
}

public static class ShelteredMapMarkers
{
    public static bool RegisterModOwnedMarker(MapMarkerSnapshot marker);
    public static bool UpdateModOwnedMarker(MapMarkerSnapshot marker);
    public static bool RemoveModOwnedMarker(string markerId, string sourceModId);
    public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotModOwnedMarkers();
    public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotModOwnedMarkers(string sourceModId);
    public static MapMarkerSnapshot SnapshotHomeShelter();
    public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotDiscoveredLocations();
    public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotQuestLocations();
    public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotPlayerPartyMarkers();
    public static ReadOnlyCollection<ExpeditionActorSnapshot> SnapshotActiveExpeditionParties();
    public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotFactionPartyMarkers();
    public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotMobileEncounterMarkers();
}
```

Registered markers and returned actor IDs/routes are copied snapshots. `SnapshotFactionPartyMarkers()` and `SnapshotMobileEncounterMarkers()` return empty collections until vanilla exposes a stable enumerable source or a later integration provides one; mods can register owned projections explicitly.

## Save Manifest / Support Bundle (SMM 2.0)

**Status:** Current ShelteredAPI save compatibility and diagnostic export surface. Existing version-1 slot manifests remain readable; newly written manifests include the additive fields below.

```csharp
// ShelteredAPI.Saves persisted models.
public class SaveInfo
{
    // Existing save summary fields are unchanged.
    public int mapSize;
    public bool hasMapSizeMetadata;
}

public class LoadedModInfo
{
    public string modId;
    public string version;
    public string requiredModApiVersion;
    public string requiredShelteredApiVersion;
    public string[] warnings;
}

public class SlotManifest
{
    public int manifestVersion;
    public string lastModified;
    public string family_name;
    public string saveScopeId;
    public string saveId;
    public string customScenarioId;
    public string source;
    public int sourceSlot;
    public uint sourceVanillaCrc32;
    public string sourceVanillaLastWriteUtc;
    public string modApiVersion;
    public string shelteredApiVersion;
    public string mapFactsStatus;
    public bool hasMapSize;
    public int mapSize;
    public string runtimeMapFactsStatus;
    public int runtimeMapWidth;
    public int runtimeMapHeight;
    public string runtimeMapScaleFactor;
    public bool hasMapSeed;
    public int mapSeed;
    public string queueFactsStatus;
    public string queueSummary;
    public string restoreFactsStatus;
    public string restoreLineageId;
    public LoadedModInfo[] lastLoadedMods;
}

// ShelteredAPI.Debugging.
public sealed class SupportBundleRequest
{
    public string saveScopeId;
    public string saveId;
    public int absoluteSlot;
    public int maxLogEntries;
}

public sealed class SupportBundleSection
{
    public string id;
    public string status;
    public string[] facts;
}

public sealed class SupportBundleSnapshot
{
    public int bundleVersion;
    public string capturedAtUtc;
    public string gameVersion;
    public string unityVersion;
    public string architecture;
    public string modApiVersion;
    public string shelteredApiVersion;
    public LoadedModInfo[] activeMods;
    public SlotManifest saveManifest;
    public SupportBundleSection[] diagnostics;
    public string[] logs;
}

public static class ShelteredSupportBundle
{
    public static SupportBundleSnapshot Capture();
    public static SupportBundleSnapshot Capture(SupportBundleRequest request);
    public static string ExportJson();
    public static string ExportJson(SupportBundleRequest request);
}
```

`ShelteredSupportBundle` records available concrete facts and consumes public map, patch-report, and background-work snapshots when those optional surfaces are present. It emits `unknown` or `unavailable` sections when a report is absent; current queue APIs are owner-scoped and therefore do not provide a save-wide queue fact for manifests. It is not a capability registry.

## Documentation Model (SMM 2.0)

**Status:** Current documentation contract for all shared-service feature agents; this section defines no callable API.

- `Current` sections contain exact implemented public signatures only.
- `Reserved` sections name the intended landing point and owner while the implementation is absent; they do not promise a callable API.
- When a feature agent introduces a public `ShelteredAPI` type, it adds exact signatures here and a justified row to `ShelteredAPI_PublicSurface_Baseline.tsv`.
- Facade naming, DTO/result/handle/report naming, typed escape-hatch policy, and unavailable-runtime behavior follow [Shared Facade Conventions](ShelteredAPI_Guide.md#shared-facade-conventions).
