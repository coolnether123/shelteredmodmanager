# ModAPI + ShelteredAPI v1.3 Beta.3 API Signatures Reference

This is the source-of-truth signature sheet for the current code in this repo. The 1.3 Beta.3 line is a breaking clean API line.

Use this file for copy/paste signatures and type names. For workflow guidance, start with [Documentation Index](README.md).

## Signature Map

| Area | Section |
|------|---------|
| Plugin lifecycle and context | [Plugin Lifecycle](#plugin-lifecycle-modapicore), [Plugin Context](#plugin-context-modapicore) |
| Save and game runtime services | [Save + Game Helpers](#save--game-helpers-modapicore), [Persistence And Sheltered Saves](#persistence-and-sheltered-saves-modapicore-shelteredapisaves) |
| Input actions and Sheltered controls | [Input Actions](#input-actions-modapiinputactions), [Sheltered Input Facade](#sheltered-input-facade-shelteredapiinput) |
| Actors and characters | [Actor System](#actor-system-modapiactors-shelteredapi), [Sheltered Actors And Characters](#sheltered-actors-and-characters-shelteredapiactors-shelteredapicharacters) |
| Settings UI | [Spine Settings](#spine-settings-modapispine-modapiattributes) |
| Harmony and transpilers | [Transpiler Core](#transpiler-core-modapiharmony), [Intent API](#intent-api-modapiharmony), [Cooperative Patching](#cooperative-patching-modapiharmony) |
| Content and assets | [Content + Assets](#content--assets-shelteredapicontent), [Runtime UI + Stores](#runtime-ui--stores-shelteredapiuiruntime-shelteredapistorage-shelteredapiworkstations) |
| Events and registries | [Event + Registry APIs](#event--registry-apis), [ShelteredAPI Trigger Scheduler](#shelteredapi-trigger-scheduler-shelteredapievents), [Mod Registry](#mod-registry-modapicore) |
| Custom scenarios | [Custom Scenarios](#custom-scenarios-modapiscenarios-shelteredapiscenarios) |
| Background work | [Background Processing](#background-processing-v13) |

## Assembly Rule

- Always reference `ModAPI.dll`.
- Reference `ShelteredAPI.dll` when your mod uses Sheltered content, saves, UI, input, events, actors, or scenarios.

> Dev/API-preview warning: the runtime UI store and cooking station contracts are preview APIs in this 1.3 line. Treat the signatures below as the current copy/paste reference, but allow for small changes before this surface is declared stable.

## API Stability Rules

- Public facades are stable mod-author entry points.
- Implementation classes are internal and may move.
- Typed Sheltered escape hatches are explicit in their names and signatures.
- Future migrations should happen behind facades.

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
    public string Tooltip;
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
    public static void ShowShelteredKeybinds();
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
public enum CharacterItemAssignmentKind { Assigned, Reserved, Equipped, Carried, Medical, Food, Tool, Quest }
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
    public string MemberKey { get; set; }
    public string MemberDisplayName { get; set; }
    public string SourceStoreId { get; set; }
    public string SourceStoreName { get; set; }
    public ItemStoreKind SourceStoreKind { get; set; }
    public string ItemId { get; set; }
    public int Quantity { get; set; }
    public CharacterItemAssignmentKind Kind { get; set; }
    public CharacterItemSlot Slot { get; set; }
}

public interface ICharacterItemAssignmentService
{
    CharacterItemAssignment Assign(FamilyMember member, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot);
    bool Unassign(string assignmentId);
    IList<CharacterItemAssignment> GetAssignments(FamilyMember member);
    IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member);
    int GetAssignedCount(FamilyMember member, string itemId);
    int ReleaseAssignmentsForMember(FamilyMember member);
}

public static class ShelteredCharacterItems
{
    public static ICharacterItemAssignmentService Service { get; }
    public static CharacterItemAssignment Assign(FamilyMember member, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot);
    public static bool Unassign(string assignmentId);
    public static IList<CharacterItemAssignment> GetAssignments(FamilyMember member);
    public static IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member);
    public static int GetAssignedCount(FamilyMember member, string itemId);
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

// ShelteredAPI.Scenarios Sheltered scenario authoring/runtime pack
public static class ShelteredScenarios
{
    public static ICustomScenarioService Service { get; }
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
    public static ScenarioDefinition CreateDefinition();
    public static ScenarioDefinition CreateDefinition(ScenarioBaseGameMode baseGameMode);
    public static ScenarioDefinition LoadDefinition(string filePath);
    public static ScenarioDefinition FromXml(string xml);
    public static void SaveDefinition(ScenarioDefinition definition, string filePath);
    public static string ToXml(ScenarioDefinition definition);
    public static ScenarioValidationResult ValidateDefinition(ScenarioDefinition definition, string scenarioFilePath);
    public static ScenarioValidationResult ValidateXmlDefinition(string scenarioId);
    public static bool TryLoadXmlDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation);
    public static ScenarioValidationResult RunFrameworkVerification();
}

public static class ShelteredScenarioRuntime
{
    public static bool FireTrigger(string triggerId);
    public static bool FireTrigger(string triggerId, string source, out string message);
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
    public StartingInventoryDefinition StartingInventory { get; set; }
    public BunkerEditsDefinition BunkerEdits { get; set; }
    public AssetReferencesDefinition AssetReferences { get; set; }
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

## Background Processing (v1.3)

```csharp
// ModAPI.Core.ModThreads
public static void RunAsync(Action action);
public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread);
public static void RunAsync<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError);

// ModAPI.Core.ModManagerBase
protected void RunInBackground<TResult>(Func<TResult> work, Action<TResult> onMainThread, Action<Exception> onError = null);
```

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
