# PR Draft: Custom Scenario Architecture and Sheltered Runtime Bridge

## Summary

This change introduces the first production architecture for custom scenario support. The work is split into two layers: a neutral ModAPI contract that lets mods register, discover, and react to custom scenarios, and a ShelteredAPI implementation that owns every Sheltered-specific concern: `ScenarioDef`, `ScenarioStage`, scenario book navigation, save-slot interaction, and `QuestManager` spawning.

The important design choice is that ModAPI does not become a Sheltered gameplay assembly. ModAPI only knows about scenario metadata, lifecycle state, events, and an opaque game-specific definition object. ShelteredAPI adapts that contract into actual Sheltered runtime behavior.

## PR Description

The PR adds `ModAPI.Scenarios.ICustomScenarioService` as the shared cross-mod entry point. Mods can use `ModAPIRegistry.TryGetAPI(GameRuntimeApiIds.CustomScenarios, out service)` to register scenarios, enumerate registered scenarios, query current custom-scenario state, and subscribe to lifecycle events. That service contract is deliberately neutral. It does not mention `ScenarioDef`, NGUI, `QuestManager`, or any Sheltered-only type.

ShelteredAPI provides the implementation through `ShelteredCustomScenarioService`. It validates registrations, stores them by stable id, exposes ordered scenario metadata to UI consumers, lazily creates Sheltered `ScenarioDef` objects, and marks scenarios selected or spawned. ShelteredAPI also registers the service during `ShelteredApiRuntimeBootstrap.Initialize()` under both the neutral id and a Sheltered alias, matching the existing GameHelper and Actors registration pattern.

The runtime hook layer lives entirely in `ShelteredAPI.Scenarios.ShelteredCustomScenarioPatches`. It takes over the vanilla scenario book flow by cloning existing scenario buttons, creates a custom scenario hub/list, wires button clicks into the service, guards underlying slot controls from stealing UI clicks, clears stale pending state when the user backs out or starts Survival, and spawns the pending custom scenario with `QuestManager.SpawnQuestOrScenario` once the new world is ready.

For mod authors, `IShelteredCustomScenario` and `ShelteredCustomScenarioBase` provide the default overridable interface requested by the user. Since this codebase targets .NET Framework 3.5, C# default interface methods are unavailable; the base class carries the default behavior and the interface carries the shape. Authors override `Id`, `DisplayName`, and `BuildDefinition`, then optionally override `Description`, `Version`, `Order`, `OnSelected`, and `OnSpawned`.

## Design Defense: DRY, SOLID, and Separation of Concerns

The design follows DRY by centralizing each repeated idea in one place. Scenario lifecycle state and registration metadata are expressed once in `ModAPI.Scenarios`, so mods and runtime code share the same vocabulary. Sheltered-specific definition construction is centralized in `ShelteredScenarioDefBuilder`, so private-field reflection for `QuestDefBase`, `ScenarioDef`, and `ScenarioStage` is not copied across mods. Runtime selection, slot guarding, and spawn behavior are grouped in one Sheltered patch host instead of being scattered through unrelated UI, save, or quest files. This makes future changes to the scenario flow local: changing the builder changes scenario construction everywhere, and changing the service changes cross-mod behavior without editing each consumer.

The design follows SOLID most directly through single responsibility and dependency inversion. `ICustomScenarioService` is a narrow abstraction for scenario registration, listing, lifecycle state, and definition creation. It does not also render UI, patch Harmony methods, or know the shape of Sheltered quests. `ShelteredCustomScenarioService` implements that abstraction for Sheltered, while `ShelteredCustomScenarioPatches` handles the external game integration points. Mod authors depend on the abstraction through `ModAPIRegistry` and only reference ShelteredAPI when they intentionally use Sheltered authoring helpers. The service is open for future extension, such as modifiers or richer lifecycle events, without rewriting the UI hook or forcing a breaking `IPluginContext` change.

Interface segregation is preserved by not forcing all mods to implement a large plugin surface. A mod can use the neutral service directly, pass a factory, or subclass `ShelteredCustomScenarioBase`. The class-based path gives defaults for simple scenarios while keeping advanced scenarios free to use the lower-level registration object. The Liskov principle is respected by keeping `ShelteredCustomScenarioBase` methods virtual and side-effect-light: overriding `BuildDefinition`, `OnSelected`, or `OnSpawned` changes scenario behavior without changing registration semantics.

Separation of concerns is the strongest boundary in the PR. ModAPI contains only general mod communication contracts: ids, metadata, events, lifecycle state, and opaque definition factories. ShelteredAPI contains all Sheltered types and behavior: `ScenarioDef`, `ScenarioStage`, `QuestManager`, `ScenarioSelectionPanel`, `SlotSelectionPanel`, NGUI buttons and labels, and reflection against Sheltered private fields. This is why the ModAPI factory returns `object`: the neutral layer should not compile against Sheltered gameplay classes. The concrete Sheltered implementation validates that the object is really a `ScenarioDef` only after crossing into ShelteredAPI.

The implementation also avoids a premature `IPluginContext` change. Adding `IPluginContext.CustomScenarios` would be convenient but would make a central interface change for one feature. Using `ModAPIRegistry` follows the existing runtime-service pattern, reduces blast radius, and keeps older plugins less exposed to interface churn.

## Verification

- Command: `Decompiled\rebuild-manager-errorsonly.cmd`
- Result: Build succeeded with `0 Error(s)` and `1725 Warning(s)`.

## Annotated Diff

### `ModAPI/Core/IGameHelper.cs`

Commentary: Adds the neutral runtime service id used by mods to find the custom scenario service through the existing ModAPIRegistry pattern. This avoids adding a new IPluginContext member and keeps discovery consistent with GameHelper and Actors.

```diff
diff --git a/ModAPI/Core/IGameHelper.cs b/ModAPI/Core/IGameHelper.cs
index b4aa593..fc312f1 100644
--- a/ModAPI/Core/IGameHelper.cs
+++ b/ModAPI/Core/IGameHelper.cs
@@ -38,6 +38,7 @@ namespace ModAPI.Core
         public const string ActorSimulation = "GameRuntime.ActorSimulation";
         public const string ActorEvents = "GameRuntime.ActorEvents";
         public const string ActorSerialization = "GameRuntime.ActorSerialization";
+        public const string CustomScenarios = "GameRuntime.CustomScenarios";
     }
 
     /// <summary>
```

### `ModAPI/Harmony/PatchRegistry.cs`

Commentary: Adds a generic Scenarios patch domain. The enum is framework-level governance, not Sheltered behavior. The concrete patches remain in ShelteredAPI.

```diff
diff --git a/ModAPI/Harmony/PatchRegistry.cs b/ModAPI/Harmony/PatchRegistry.cs
index 312ee5b..396d311 100644
--- a/ModAPI/Harmony/PatchRegistry.cs
+++ b/ModAPI/Harmony/PatchRegistry.cs
@@ -22,7 +22,8 @@ namespace ModAPI.Harmony
         Events,
         Interactions,
         Characters,
-        World
+        World,
+        Scenarios
     }
 
     /// <summary>
```

### `ModAPI/ModAPI.csproj`

Commentary: Includes the new neutral ModAPI scenario contracts in the ModAPI assembly so mods can compile against them without referencing ShelteredAPI unless they need Sheltered authoring helpers.

```diff
diff --git a/ModAPI/ModAPI.csproj b/ModAPI/ModAPI.csproj
index 6159a60..281cdd8 100644
--- a/ModAPI/ModAPI.csproj
+++ b/ModAPI/ModAPI.csproj
@@ -91,6 +91,8 @@
     <Compile Include="Core\SharedAssemblyResolver.cs" />
     <Compile Include="Core\LoggerExtensions.cs" />
     <Compile Include="Core\ContextExtensions.cs" />
+    <Compile Include="Scenarios\CustomScenarioContracts.cs" />
+    <Compile Include="Scenarios\ICustomScenarioService.cs" />
     <Compile Include="Input\InputBinding.cs" />
     <Compile Include="Input\ModInputAction.cs" />
     <Compile Include="Input\InputActionRegistry.cs" />
```

### `ShelteredAPI/Core/ShelteredApiRuntimeBootstrap.cs`

Commentary: Registers the Sheltered implementation of the neutral custom scenario service. This follows the existing ShelteredAPI pattern for GameHelper and Actors.

```diff
diff --git a/ShelteredAPI/Core/ShelteredApiRuntimeBootstrap.cs b/ShelteredAPI/Core/ShelteredApiRuntimeBootstrap.cs
index d13fbe7..6aaa597 100644
--- a/ShelteredAPI/Core/ShelteredApiRuntimeBootstrap.cs
+++ b/ShelteredAPI/Core/ShelteredApiRuntimeBootstrap.cs
@@ -1,7 +1,9 @@
 using ModAPI.Core;
 using ModAPI.Actors;
 using ModAPI.InputServices;
+using ModAPI.Scenarios;
 using ShelteredAPI.Input;
+using ShelteredAPI.Scenarios;
 using UnityEngine;
 
 namespace ShelteredAPI.Core
@@ -75,6 +77,10 @@ namespace ShelteredAPI.Core
             RegisterApi("ShelteredAPI.ActorEvents", (IActorEvents)actors);
             RegisterApi(GameRuntimeApiIds.ActorSerialization, (IActorSerializationService)actors);
             RegisterApi("ShelteredAPI.ActorSerialization", (IActorSerializationService)actors);
+
+            ICustomScenarioService customScenarios = ShelteredCustomScenarioService.Instance;
+            RegisterApi(GameRuntimeApiIds.CustomScenarios, customScenarios);
+            RegisterApi("ShelteredAPI.CustomScenarios", customScenarios);
         }
 
         private static void RegisterApi<T>(string apiId, T implementation) where T : class
```

### `ShelteredAPI/ShelteredAPI.csproj`

Commentary: Includes the Sheltered-specific scenario implementation, authoring helpers, and runtime patches in ShelteredAPI only.

```diff
diff --git a/ShelteredAPI/ShelteredAPI.csproj b/ShelteredAPI/ShelteredAPI.csproj
index 602d599..5c40851 100644
--- a/ShelteredAPI/ShelteredAPI.csproj
+++ b/ShelteredAPI/ShelteredAPI.csproj
@@ -73,6 +73,11 @@
     <Compile Include="Harmony\SettingsInputModePromptPatches.cs" />
     <Compile Include="Harmony\SettingsKeybindsButtonPatches.cs" />
     <Compile Include="Interactions\ObjectButtonInjector.cs" />
+    <Compile Include="Scenarios\IShelteredCustomScenario.cs" />
+    <Compile Include="Scenarios\ShelteredCustomScenarioPatches.cs" />
+    <Compile Include="Scenarios\ShelteredCustomScenarioService.cs" />
+    <Compile Include="Scenarios\ShelteredScenarioDefBuilder.cs" />
+    <Compile Include="Scenarios\ShelteredScenarioRegistration.cs" />
     <Compile Include="Input\ShelteredTouchpadInputRouter.cs" />
     <Compile Include="Input\ShelteredInputActions.cs" />
     <Compile Include="Input\ShelteredVanillaInputActions.cs" />
```

### `documentation/API_Signatures_Reference.md`

Commentary: Documents the new public contract and the Sheltered-specific base class so mod authors can see the intended API shape.

```diff
diff --git a/documentation/API_Signatures_Reference.md b/documentation/API_Signatures_Reference.md
index f53f77a..7433408 100644
--- a/documentation/API_Signatures_Reference.md
+++ b/documentation/API_Signatures_Reference.md
@@ -392,6 +392,57 @@ public static bool IsAPIRegistered(string apiName);
 public static bool UnregisterAPI(string apiName, string providerModId = null);
 public static List<string> GetRegisteredAPIs();
 
+// ModAPI.Scenarios.ICustomScenarioService
+public static class GameRuntimeApiIds
+{
+    public const string CustomScenarios = "GameRuntime.CustomScenarios";
+}
+
+public interface ICustomScenarioService
+{
+    event Action<CustomScenarioEventArgs> ScenarioRegistered;
+    event Action<CustomScenarioEventArgs> ScenarioUnregistered;
+    event Action<CustomScenarioEventArgs> ScenarioSelected;
+    event Action<CustomScenarioEventArgs> ScenarioSpawned;
+    event Action<CustomScenarioEventArgs> StateChanged;
+
+    CustomScenarioState CurrentState { get; }
+    CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration);
+    bool Unregister(string scenarioId);
+    bool TryGet(string scenarioId, out CustomScenarioInfo scenario);
+    CustomScenarioInfo[] List();
+    bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage);
+}
+
+// ShelteredAPI.Scenarios class-based authoring helper
+public interface IShelteredCustomScenario
+{
+    string Id { get; }
+    string DisplayName { get; }
+    string Description { get; }
+    string Version { get; }
+    int Order { get; }
+    object UserData { get; }
+    ScenarioDef BuildDefinition(CustomScenarioBuildContext context);
+    void OnSelected(CustomScenarioEventArgs args);
+    void OnSpawned(CustomScenarioEventArgs args);
+}
+
+public abstract class ShelteredCustomScenarioBase : IShelteredCustomScenario
+{
+    public abstract string Id { get; }
+    public abstract string DisplayName { get; }
+    public virtual string Description { get; }
+    public virtual string Version { get; }
+    public virtual int Order { get; }
+    public virtual object UserData { get; }
+    public abstract ScenarioDef BuildDefinition(CustomScenarioBuildContext context);
+    public virtual void OnSelected(CustomScenarioEventArgs args);
+    public virtual void OnSpawned(CustomScenarioEventArgs args);
+    public CustomScenarioRegistration ToRegistration();
+    public CustomScenarioRegistrationResult Register();
+}
+
 // ModAPI.Core.ModRegistry
 public static bool Find(string modId);
 public static ModEntry GetMod(string modId);
```

### `documentation/ShelteredAPI_Guide.md`

Commentary: Adds an end-to-end authoring example showing registry lookup plus the class-based Sheltered scenario pattern.

```diff
diff --git a/documentation/ShelteredAPI_Guide.md b/documentation/ShelteredAPI_Guide.md
index f4afa8a..fb99931 100644
--- a/documentation/ShelteredAPI_Guide.md
+++ b/documentation/ShelteredAPI_Guide.md
@@ -21,6 +21,7 @@ Content-specific guidance: `documentation/ShelteredAPI_Content_Guide.md`.
   - `ShelteredAPI.ActorSerialization`
 - Sheltered-specific UI and input helpers under `ShelteredAPI.*`
 - Sheltered-specific content registration and runtime injection under `ShelteredAPI.Content`
+- Sheltered-specific custom scenario authoring and runtime hooks under `ShelteredAPI.Scenarios`
 
 ## 2. Referencing It
 
@@ -36,8 +37,10 @@ Common imports:
 using ModAPI.Core;
 using ModAPI.Events;
 using ModAPI.Actors;
+using ModAPI.Scenarios;
 using ShelteredAPI.Adapters;
 using ShelteredAPI.Content;
+using ShelteredAPI.Scenarios;
 ```
 
 ## 3. Usage Example
@@ -71,6 +74,25 @@ public class MyPlugin : IModPlugin
             priority: 50,
             cadence: TimeTriggerCadence.SixHour,
             callback: batch => ctx.Log.Info("Tick seq=" + batch.Sequence + " day=" + batch.Day));
+
+        ICustomScenarioService scenarios;
+        if (ModAPIRegistry.TryGetAPI(GameRuntimeApiIds.CustomScenarios, out scenarios))
+            scenarios.Register(new LongRoadScenario().ToRegistration());
+    }
+}
+
+public sealed class LongRoadScenario : ShelteredCustomScenarioBase
+{
+    public override string Id { get { return "com.mymod.scenario.longroad"; } }
+    public override string DisplayName { get { return "The Long Road"; } }
+    public override string Description { get { return "Gather resources and survive the long road."; } }
+
+    public override ScenarioDef BuildDefinition(CustomScenarioBuildContext context)
+    {
+        return CreateDefinition()
+            .UseInModes(true, false, false)
+            .AddSimpleStage("longroad_intro")
+            .Build();
     }
 }
 ```
@@ -80,6 +102,7 @@ public class MyPlugin : IModPlugin
 - scheduler/events compatibility surfaces such as `GameEvents` and `GameTimeTriggerHelper` are hosted in `ModAPI.dll` in the 1.3 line
 - actor contracts live in `ModAPI.Actors`; `ShelteredAPI` provides the default runtime implementation
 - item, recipe, loot, asset, and content-localization APIs live in `ShelteredAPI.Content`
+- custom scenario contracts live in `ModAPI.Scenarios`; Sheltered `ScenarioDef` creation and in-game hooks live in `ShelteredAPI.Scenarios`
 - the content injector is manager-scoped and will rebind when a new family/session recreates Sheltered runtime managers
 - register triggers and runtime behavior in `Start(...)`, not constructors
 - use unique IDs for triggers, actor bindings, components, and adapters
```

### `ModAPI/Scenarios/CustomScenarioContracts.cs`

Commentary: Defines neutral data contracts, lifecycle state, event args, registration results, and a game-specific definition factory typed as object. The object boundary is intentional: ModAPI must not reference Sheltered ScenarioDef.

```diff
diff --git a/ModAPI/Scenarios/CustomScenarioContracts.cs b/ModAPI/Scenarios/CustomScenarioContracts.cs
new file mode 100644
index 0000000..0000000
--- /dev/null
+++ b/ModAPI/Scenarios/CustomScenarioContracts.cs
@@ -0,0 +1,165 @@
+using System;
+using System.Reflection;
+
+namespace ModAPI.Scenarios
+{
+    /// <summary>
+    /// Builds a game-specific scenario definition when the runtime needs one.
+    /// </summary>
+    public delegate object CustomScenarioDefinitionFactory(CustomScenarioBuildContext context);
+
+    public enum CustomScenarioLifecycleState
+    {
+        None = 0,
+        Pending = 1,
+        Active = 2
+    }
+
+    public enum CustomScenarioEventType
+    {
+        Registered = 0,
+        Unregistered = 1,
+        Selected = 2,
+        Spawned = 3,
+        Cleared = 4
+    }
+
+    /// <summary>
+    /// Mod-authored custom scenario registration data.
+    /// </summary>
+    public class CustomScenarioRegistration
+    {
+        public string Id { get; set; }
+        public string DisplayName { get; set; }
+        public string Description { get; set; }
+        public string Version { get; set; }
+        public int Order { get; set; }
+        public string OwnerModId { get; set; }
+        public Assembly OwnerAssembly { get; set; }
+        public object Definition { get; set; }
+        public CustomScenarioDefinitionFactory DefinitionFactory { get; set; }
+        public Action<CustomScenarioEventArgs> OnSelected { get; set; }
+        public Action<CustomScenarioEventArgs> OnSpawned { get; set; }
+        public object UserData { get; set; }
+    }
+
+    /// <summary>
+    /// Public read model for a registered custom scenario.
+    /// </summary>
+    public class CustomScenarioInfo
+    {
+        public CustomScenarioInfo(
+            string id,
+            string displayName,
+            string description,
+            string version,
+            int order,
+            string ownerModId,
+            bool hasDefinition,
+            bool hasDefinitionFactory)
+        {
+            Id = id;
+            DisplayName = displayName;
+            Description = description;
+            Version = version;
+            Order = order;
+            OwnerModId = ownerModId;
+            HasDefinition = hasDefinition;
+            HasDefinitionFactory = hasDefinitionFactory;
+        }
+
+        public string Id { get; private set; }
+        public string DisplayName { get; private set; }
+        public string Description { get; private set; }
+        public string Version { get; private set; }
+        public int Order { get; private set; }
+        public string OwnerModId { get; private set; }
+        public bool HasDefinition { get; private set; }
+        public bool HasDefinitionFactory { get; private set; }
+    }
+
+    /// <summary>
+    /// Context passed to a scenario definition factory.
+    /// </summary>
+    public class CustomScenarioBuildContext
+    {
+        public string ScenarioId { get; set; }
+        public string OwnerModId { get; set; }
+        public string RequestedByModId { get; set; }
+        public CustomScenarioState State { get; set; }
+        public object UserData { get; set; }
+    }
+
+    /// <summary>
+    /// Current custom scenario state. This intentionally tracks custom scenarios only.
+    /// </summary>
+    public class CustomScenarioState
+    {
+        public CustomScenarioState()
+        {
+            LifecycleState = CustomScenarioLifecycleState.None;
+        }
+
+        public string ScenarioId { get; set; }
+        public CustomScenarioLifecycleState LifecycleState { get; set; }
+        public bool HasCustomScenario
+        {
+            get { return !string.IsNullOrEmpty(ScenarioId) && LifecycleState != CustomScenarioLifecycleState.None; }
+        }
+
+        public static CustomScenarioState None()
+        {
+            return new CustomScenarioState();
+        }
+
+        public CustomScenarioState Copy()
+        {
+            return new CustomScenarioState
+            {
+                ScenarioId = ScenarioId,
+                LifecycleState = LifecycleState
+            };
+        }
+    }
+
+    public class CustomScenarioEventArgs : EventArgs
+    {
+        public CustomScenarioEventArgs(CustomScenarioEventType eventType, CustomScenarioInfo scenario, CustomScenarioState state)
+        {
+            EventType = eventType;
+            Scenario = scenario;
+            State = state != null ? state.Copy() : CustomScenarioState.None();
+        }
+
+        public CustomScenarioEventType EventType { get; private set; }
+        public CustomScenarioInfo Scenario { get; private set; }
+        public CustomScenarioState State { get; private set; }
+    }
+
+    public class CustomScenarioRegistrationResult
+    {
+        public bool Success { get; private set; }
+        public string ScenarioId { get; private set; }
+        public bool ReplacedExisting { get; private set; }
+        public string ErrorMessage { get; private set; }
+
+        public static CustomScenarioRegistrationResult Ok(string scenarioId, bool replacedExisting)
+        {
+            return new CustomScenarioRegistrationResult
+            {
+                Success = true,
+                ScenarioId = scenarioId,
+                ReplacedExisting = replacedExisting
+            };
+        }
+
+        public static CustomScenarioRegistrationResult Failed(string errorMessage)
+        {
+            return new CustomScenarioRegistrationResult
+            {
+                Success = false,
+                ErrorMessage = errorMessage
+            };
+        }
+    }
+}
```

### `ModAPI/Scenarios/ICustomScenarioService.cs`

Commentary: Defines the cross-mod service contract for registering, listing, querying state, and creating game-specific definitions. It does not know about Sheltered UI or game types.

```diff
diff --git a/ModAPI/Scenarios/ICustomScenarioService.cs b/ModAPI/Scenarios/ICustomScenarioService.cs
new file mode 100644
index 0000000..0000000
--- /dev/null
+++ b/ModAPI/Scenarios/ICustomScenarioService.cs
@@ -0,0 +1,25 @@
+using System;
+
+namespace ModAPI.Scenarios
+{
+    /// <summary>
+    /// Shared service for custom scenario registration and custom-scenario lifecycle state.
+    /// The game-specific runtime assembly owns the implementation.
+    /// </summary>
+    public interface ICustomScenarioService
+    {
+        event Action<CustomScenarioEventArgs> ScenarioRegistered;
+        event Action<CustomScenarioEventArgs> ScenarioUnregistered;
+        event Action<CustomScenarioEventArgs> ScenarioSelected;
+        event Action<CustomScenarioEventArgs> ScenarioSpawned;
+        event Action<CustomScenarioEventArgs> StateChanged;
+
+        CustomScenarioState CurrentState { get; }
+
+        CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration);
+        bool Unregister(string scenarioId);
+        bool TryGet(string scenarioId, out CustomScenarioInfo scenario);
+        CustomScenarioInfo[] List();
+        bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage);
+    }
+}
```

### `ShelteredAPI/Scenarios/IShelteredCustomScenario.cs`

Commentary: Adds the default overridable authoring model requested by the user. Because the project targets .NET 3.5, defaults live in the abstract base class rather than default interface methods.

```diff
diff --git a/ShelteredAPI/Scenarios/IShelteredCustomScenario.cs b/ShelteredAPI/Scenarios/IShelteredCustomScenario.cs
new file mode 100644
index 0000000..0000000
--- /dev/null
+++ b/ShelteredAPI/Scenarios/IShelteredCustomScenario.cs
@@ -0,0 +1,79 @@
+using ModAPI.Scenarios;
+
+namespace ShelteredAPI.Scenarios
+{
+    /// <summary>
+    /// Sheltered-specific scenario authoring contract for mods that prefer a class-based scenario definition.
+    /// </summary>
+    public interface IShelteredCustomScenario
+    {
+        string Id { get; }
+        string DisplayName { get; }
+        string Description { get; }
+        string Version { get; }
+        int Order { get; }
+        object UserData { get; }
+
+        ScenarioDef BuildDefinition(CustomScenarioBuildContext context);
+        void OnSelected(CustomScenarioEventArgs args);
+        void OnSpawned(CustomScenarioEventArgs args);
+    }
+
+    /// <summary>
+    /// Default overridable implementation for custom Sheltered scenarios.
+    /// Override only the members your scenario needs.
+    /// </summary>
+    public abstract class ShelteredCustomScenarioBase : IShelteredCustomScenario
+    {
+        public abstract string Id { get; }
+        public abstract string DisplayName { get; }
+
+        public virtual string Description
+        {
+            get { return string.Empty; }
+        }
+
+        public virtual string Version
+        {
+            get { return "1.0"; }
+        }
+
+        public virtual int Order
+        {
+            get { return 0; }
+        }
+
+        public virtual object UserData
+        {
+            get { return null; }
+        }
+
+        public abstract ScenarioDef BuildDefinition(CustomScenarioBuildContext context);
+
+        public virtual void OnSelected(CustomScenarioEventArgs args)
+        {
+        }
+
+        public virtual void OnSpawned(CustomScenarioEventArgs args)
+        {
+        }
+
+        public CustomScenarioRegistration ToRegistration()
+        {
+            return ShelteredScenarioRegistration.FromScenario(this);
+        }
+
+        public CustomScenarioRegistrationResult Register()
+        {
+            return ShelteredCustomScenarioService.Instance.Register(ToRegistration());
+        }
+
+        protected ShelteredScenarioDefBuilder CreateDefinition()
+        {
+            return new ShelteredScenarioDefBuilder()
+                .SetId(Id)
+                .SetNameKey(DisplayName)
+                .SetDescriptionKey(Description);
+        }
+    }
+}
```

### `ShelteredAPI/Scenarios/ShelteredCustomScenarioPatches.cs`

Commentary: Owns all Sheltered-specific navigation and game hooks: scenario book button/list wiring, pending state cleanup, slot-click guarding, and QuestManager spawning.

```diff
diff --git a/ShelteredAPI/Scenarios/ShelteredCustomScenarioPatches.cs b/ShelteredAPI/Scenarios/ShelteredCustomScenarioPatches.cs
new file mode 100644
index 0000000..0000000
--- /dev/null
+++ b/ShelteredAPI/Scenarios/ShelteredCustomScenarioPatches.cs
@@ -0,0 +1,590 @@
+using System;
+using System.Collections;
+using System.Collections.Generic;
+using HarmonyLib;
+using ModAPI.Core;
+using ModAPI.Harmony;
+using ModAPI.Scenarios;
+using UnityEngine;
+
+namespace ShelteredAPI.Scenarios
+{
+    internal static class ShelteredCustomScenarioRuntimeState
+    {
+        private static int _blockSlotClicksUntilFrame;
+
+        public static bool IsSlotClickBlocked
+        {
+            get { return Time.frameCount <= _blockSlotClicksUntilFrame; }
+        }
+
+        public static void BlockSlotClicksBriefly()
+        {
+            _blockSlotClicksUntilFrame = Math.Max(_blockSlotClicksUntilFrame, Time.frameCount + 2);
+        }
+
+        public static bool HasPendingCustomScenario()
+        {
+            CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
+            return state != null && state.LifecycleState == CustomScenarioLifecycleState.Pending && !string.IsNullOrEmpty(state.ScenarioId);
+        }
+
+        public static void ClearPendingCustomScenario()
+        {
+            CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
+            if (state != null && state.LifecycleState == CustomScenarioLifecycleState.Pending)
+                ShelteredCustomScenarioService.Instance.ClearState();
+        }
+    }
+
+    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSelection",
+        TargetBehavior = "Custom scenario entries are surfaced in the vanilla Sheltered scenario selection panel.",
+        FailureMode = "Registered custom scenarios are unavailable from the in-game scenario selection flow.",
+        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario selection patch host.")]
+    [HarmonyPatch(typeof(ScenarioSelectionPanel))]
+    internal static class ShelteredCustomScenarioSelectionPatches
+    {
+        private const string HubLabel = "Custom Scenarios";
+        private static readonly Dictionary<int, bool> ButtonsCreated = new Dictionary<int, bool>();
+        private static readonly Dictionary<int, int> BaseButtonCount = new Dictionary<int, int>();
+        private static readonly Dictionary<int, List<UIButton>> OriginalButtons = new Dictionary<int, List<UIButton>>();
+        private static readonly Dictionary<int, UIButton> HubButtons = new Dictionary<int, UIButton>();
+        private static readonly Dictionary<int, List<UIButton>> CustomButtons = new Dictionary<int, List<UIButton>>();
+        private static readonly HashSet<int> CustomModePanels = new HashSet<int>();
+
+        [HarmonyPostfix]
+        [HarmonyPatch("OnShow")]
+        private static void OnShowPostfix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
+        {
+            try
+            {
+                if (__instance == null || ___m_scenarioButtons == null)
+                    return;
+
+                CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
+                if (scenarios.Length == 0)
+                    return;
+
+                int instanceId = __instance.GetInstanceID();
+                if (ButtonsCreated.ContainsKey(instanceId) && ButtonsCreated[instanceId])
+                    return;
+
+                if (___m_scenarioButtons.Count == 0)
+                    return;
+
+                UIButton sourceButton = ___m_scenarioButtons[___m_scenarioButtons.Count - 1];
+                if (sourceButton == null || sourceButton.gameObject == null)
+                    return;
+
+                if (!OriginalButtons.ContainsKey(instanceId))
+                    OriginalButtons[instanceId] = new List<UIButton>(___m_scenarioButtons);
+
+                float spacingY = MeasureSpacing(___m_scenarioButtons);
+                GameObject hubObject = GameObject.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
+                hubObject.name = "ShelteredAPI_CustomScenarios_HubButton";
+                hubObject.SetActive(true);
+
+                UIButton hubButton = hubObject.GetComponent<UIButton>();
+                if (hubButton == null)
+                    hubButton = hubObject.AddComponent<UIButton>();
+
+                hubButton.transform.localPosition = sourceButton.transform.localPosition + new Vector3(0f, spacingY, 0f);
+                ConfigureButton(hubObject, HubLabel);
+
+                UIEventListener listener = UIEventListener.Get(hubObject);
+                listener.onPress = delegate(GameObject go, bool pressed)
+                {
+                    if (pressed)
+                        ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
+                };
+                listener.onClick = delegate(GameObject go)
+                {
+                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
+                    EnterCustomMode(__instance, ___m_scenarioButtons);
+                };
+
+                ___m_scenarioButtons.Add(hubButton);
+                BaseButtonCount[instanceId] = ___m_scenarioButtons.Count - 1;
+                HubButtons[instanceId] = hubButton;
+                ButtonsCreated[instanceId] = true;
+
+                MMLog.WriteDebug("[ShelteredCustomScenarioSelection] Added custom scenario hub with "
+                    + scenarios.Length + " registered scenario(s).");
+            }
+            catch (Exception ex)
+            {
+                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] OnShow failed: " + ex.Message);
+            }
+        }
+
+        [HarmonyPrefix]
+        [HarmonyPatch("OnScenarioSelected")]
+        private static bool OnScenarioSelectedPrefix(
+            ScenarioSelectionPanel __instance,
+            int ___m_selectedScenario,
+            UILabel ___m_scenarioNameLabel,
+            UILabel ___m_scenarioDescLabel,
+            UILabel ___m_scenarioHighScore,
+            GameObject ___m_stasis_scoreLabelsRoot)
+        {
+            int instanceId = __instance.GetInstanceID();
+            if (!CustomModePanels.Contains(instanceId))
+            {
+                int baseCount = BaseButtonCount.ContainsKey(instanceId) ? BaseButtonCount[instanceId] : 2;
+                if (___m_selectedScenario == baseCount)
+                {
+                    SetScenarioText(___m_scenarioNameLabel, ___m_scenarioDescLabel, ___m_scenarioHighScore, ___m_stasis_scoreLabelsRoot,
+                        HubLabel,
+                        "Browse custom scenarios registered by loaded mods.");
+                    return false;
+                }
+
+                return true;
+            }
+
+            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
+            if (___m_selectedScenario >= 0 && ___m_selectedScenario < scenarios.Length)
+            {
+                CustomScenarioInfo scenario = scenarios[___m_selectedScenario];
+                SetScenarioText(___m_scenarioNameLabel, ___m_scenarioDescLabel, ___m_scenarioHighScore, ___m_stasis_scoreLabelsRoot,
+                    scenario.DisplayName,
+                    scenario.Description);
+            }
+            return false;
+        }
+
+        [HarmonyPostfix]
+        [HarmonyPatch("Update")]
+        private static void UpdatePostfix(
+            ScenarioSelectionPanel __instance,
+            int ___m_selectedScenario,
+            UILabel ___m_scenarioNameLabel,
+            UILabel ___m_scenarioDescLabel,
+            UILabel ___m_scenarioHighScore,
+            GameObject ___m_stasis_scoreLabelsRoot,
+            SlotSelectionPanel ___selectionPanel)
+        {
+            try
+            {
+                int instanceId = __instance.GetInstanceID();
+                if (!CustomModePanels.Contains(instanceId))
+                    return;
+
+                if (___selectionPanel != null)
+                    ___selectionPanel.m_inputEnabled = false;
+
+                if (___m_selectedScenario == -1)
+                {
+                    CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
+                    SetScenarioText(___m_scenarioNameLabel, ___m_scenarioDescLabel, ___m_scenarioHighScore, ___m_stasis_scoreLabelsRoot,
+                        HubLabel,
+                        scenarios.Length + " custom scenario(s) available.");
+                }
+            }
+            catch
+            {
+            }
+        }
+
+        [HarmonyPrefix]
+        [HarmonyPatch("OnScenarioChosen")]
+        private static bool OnScenarioChosenPrefix(
+            ScenarioSelectionPanel __instance,
+            int ___m_selectedScenario,
+            List<UIButton> ___m_scenarioButtons)
+        {
+            int instanceId = __instance.GetInstanceID();
+            if (!CustomModePanels.Contains(instanceId))
+            {
+                int baseCount = BaseButtonCount.ContainsKey(instanceId) ? BaseButtonCount[instanceId] : 2;
+                if (___m_selectedScenario == baseCount)
+                {
+                    EnterCustomMode(__instance, ___m_scenarioButtons);
+                    return false;
+                }
+
+                ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
+                return true;
+            }
+
+            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
+            if (___m_selectedScenario < 0 || ___m_selectedScenario >= scenarios.Length)
+                return false;
+
+            CustomScenarioInfo scenario = scenarios[___m_selectedScenario];
+            if (!ShelteredCustomScenarioService.Instance.MarkSelected(scenario.Id))
+            {
+                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Failed to select custom scenario: " + scenario.Id);
+                return false;
+            }
+
+            ExitCustomMode(__instance, ___m_scenarioButtons);
+            ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
+            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Selected custom scenario: " + scenario.Id);
+            return true;
+        }
+
+        [HarmonyPrefix]
+        [HarmonyPatch("OnCancel")]
+        private static bool OnCancelPrefix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
+        {
+            int instanceId = __instance.GetInstanceID();
+            if (!CustomModePanels.Contains(instanceId))
+                return true;
+
+            ExitCustomMode(__instance, ___m_scenarioButtons);
+            return false;
+        }
+
+        [HarmonyPostfix]
+        [HarmonyPatch("OnDestroy")]
+        private static void OnDestroyPostfix(ScenarioSelectionPanel __instance)
+        {
+            Cleanup(__instance.GetInstanceID());
+        }
+
+        private static void EnterCustomMode(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
+        {
+            if (panel == null || scenarioButtons == null)
+                return;
+
+            int instanceId = panel.GetInstanceID();
+            CustomScenarioInfo[] scenarios = ShelteredCustomScenarioService.Instance.List();
+            if (scenarios.Length == 0)
+                return;
+
+            if (!CustomButtons.ContainsKey(instanceId))
+                CustomButtons[instanceId] = new List<UIButton>();
+
+            DestroyButtons(CustomButtons[instanceId]);
+            CustomButtons[instanceId].Clear();
+
+            List<UIButton> originals;
+            if (OriginalButtons.TryGetValue(instanceId, out originals))
+            {
+                for (int i = 0; i < originals.Count; i++)
+                {
+                    if (originals[i] != null && originals[i].gameObject != null)
+                        originals[i].gameObject.SetActive(false);
+                }
+            }
+
+            UIButton hubButton;
+            if (HubButtons.TryGetValue(instanceId, out hubButton) && hubButton != null && hubButton.gameObject != null)
+                hubButton.gameObject.SetActive(false);
+
+            UIButton source = hubButton;
+            if (source == null && originals != null && originals.Count > 0)
+                source = originals[0];
+            if (source == null)
+                return;
+
+            Vector3 basePosition = source.transform.localPosition;
+            if (originals != null && originals.Count > 0 && originals[0] != null)
+                basePosition = originals[0].transform.localPosition;
+
+            float spacingY = originals != null ? MeasureSpacing(originals) : -60f;
+            scenarioButtons.Clear();
+
+            for (int i = 0; i < scenarios.Length; i++)
+            {
+                CustomScenarioInfo scenario = scenarios[i];
+                GameObject buttonObject = GameObject.Instantiate(source.gameObject, source.transform.parent);
+                buttonObject.name = "ShelteredAPI_CustomScenario_" + SanitizeObjectName(scenario.Id);
+                buttonObject.SetActive(true);
+
+                UIButton button = buttonObject.GetComponent<UIButton>();
+                if (button == null)
+                    button = buttonObject.AddComponent<UIButton>();
+
+                button.transform.localPosition = basePosition + new Vector3(0f, spacingY * i, 0f);
+                ConfigureButton(buttonObject, scenario.DisplayName);
+
+                int capturedIndex = i;
+                UIEventListener listener = UIEventListener.Get(buttonObject);
+                listener.onPress = delegate(GameObject go, bool pressed)
+                {
+                    if (pressed)
+                        ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
+                };
+                listener.onClick = delegate(GameObject go)
+                {
+                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
+                    Traverse.Create(panel).Field("m_selectedScenario").SetValue(capturedIndex);
+                    panel.OnScenarioChosen();
+                };
+
+                CustomButtons[instanceId].Add(button);
+                scenarioButtons.Add(button);
+            }
+
+            CustomModePanels.Add(instanceId);
+            Traverse.Create(panel).Field("m_selectedScenario").SetValue(-1);
+            MMLog.WriteDebug("[ShelteredCustomScenarioSelection] Entered custom scenario list for panel " + instanceId + ".");
+        }
+
+        private static void ExitCustomMode(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
+        {
+            if (panel == null || scenarioButtons == null)
+                return;
+
+            int instanceId = panel.GetInstanceID();
+            List<UIButton> buttons;
+            if (CustomButtons.TryGetValue(instanceId, out buttons))
+            {
+                DestroyButtons(buttons);
+                buttons.Clear();
+            }
+
+            scenarioButtons.Clear();
+            List<UIButton> originals;
+            if (OriginalButtons.TryGetValue(instanceId, out originals))
+            {
+                for (int i = 0; i < originals.Count; i++)
+                {
+                    UIButton button = originals[i];
+                    if (button == null || button.gameObject == null)
+                        continue;
+
+                    button.gameObject.SetActive(true);
+                    scenarioButtons.Add(button);
+                }
+            }
+
+            UIButton hubButton;
+            if (HubButtons.TryGetValue(instanceId, out hubButton) && hubButton != null && hubButton.gameObject != null)
+            {
+                hubButton.gameObject.SetActive(true);
+                scenarioButtons.Add(hubButton);
+            }
+
+            CustomModePanels.Remove(instanceId);
+            Traverse.Create(panel).Field("m_selectedScenario").SetValue(-1);
+            MMLog.WriteDebug("[ShelteredCustomScenarioSelection] Exited custom scenario list for panel " + instanceId + ".");
+        }
+
+        private static void Cleanup(int instanceId)
+        {
+            ButtonsCreated.Remove(instanceId);
+            BaseButtonCount.Remove(instanceId);
+            CustomModePanels.Remove(instanceId);
+            OriginalButtons.Remove(instanceId);
+            HubButtons.Remove(instanceId);
+
+            List<UIButton> buttons;
+            if (CustomButtons.TryGetValue(instanceId, out buttons))
+                DestroyButtons(buttons);
+            CustomButtons.Remove(instanceId);
+        }
+
+        private static void ConfigureButton(GameObject buttonObject, string label)
+        {
+            if (buttonObject == null)
+                return;
+
+            UIButton button = buttonObject.GetComponent<UIButton>();
+            if (button != null && button.onClick != null)
+                button.onClick.Clear();
+
+            UIButtonMessage[] messages = buttonObject.GetComponentsInChildren<UIButtonMessage>(true);
+            for (int i = 0; i < messages.Length; i++)
+            {
+                if (messages[i] != null)
+                    UnityEngine.Object.Destroy(messages[i]);
+            }
+
+            UILocalize[] localizers = buttonObject.GetComponentsInChildren<UILocalize>(true);
+            for (int i = 0; i < localizers.Length; i++)
+            {
+                if (localizers[i] != null)
+                    UnityEngine.Object.Destroy(localizers[i]);
+            }
+
+            UILabel[] labels = buttonObject.GetComponentsInChildren<UILabel>(true);
+            for (int i = 0; i < labels.Length; i++)
+            {
+                if (labels[i] != null)
+                    labels[i].text = label ?? string.Empty;
+            }
+        }
+
+        private static void SetScenarioText(
+            UILabel nameLabel,
+            UILabel descriptionLabel,
+            UILabel highScoreLabel,
+            GameObject stasisScoreLabelsRoot,
+            string name,
+            string description)
+        {
+            if (nameLabel != null)
+                nameLabel.text = name ?? string.Empty;
+            if (descriptionLabel != null)
+                descriptionLabel.text = description ?? string.Empty;
+            if (highScoreLabel != null)
+                highScoreLabel.text = string.Empty;
+            if (stasisScoreLabelsRoot != null)
+                stasisScoreLabelsRoot.SetActive(false);
+        }
+
+        private static float MeasureSpacing(IList<UIButton> buttons)
+        {
+            float spacingY = -60f;
+            try
+            {
+                if (buttons != null && buttons.Count >= 2 && buttons[buttons.Count - 1] != null && buttons[buttons.Count - 2] != null)
+                {
+                    float last = buttons[buttons.Count - 1].transform.localPosition.y;
+                    float previous = buttons[buttons.Count - 2].transform.localPosition.y;
+                    float measured = last - previous;
+                    if (Mathf.Abs(measured) > 1f)
+                        spacingY = measured;
+                }
+
+                if (Mathf.Abs(spacingY) > 140f)
+                    spacingY = Mathf.Sign(spacingY) * 120f;
+            }
+            catch
+            {
+            }
+
+            return spacingY;
+        }
+
+        private static void DestroyButtons(List<UIButton> buttons)
+        {
+            if (buttons == null)
+                return;
+
+            for (int i = 0; i < buttons.Count; i++)
+            {
+                UIButton button = buttons[i];
+                if (button != null && button.gameObject != null)
+                    UnityEngine.Object.Destroy(button.gameObject);
+            }
+        }
+
+        private static string SanitizeObjectName(string value)
+        {
+            if (string.IsNullOrEmpty(value))
+                return "unknown";
+
+            char[] chars = value.ToCharArray();
+            for (int i = 0; i < chars.Length; i++)
+            {
+                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
+                    chars[i] = '_';
+            }
+
+            return new string(chars);
+        }
+    }
+
+    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSpawn",
+        TargetBehavior = "Pending custom scenarios are spawned through Sheltered QuestManager once a new world is ready.",
+        FailureMode = "A selected custom scenario reaches save-slot selection but never starts in the new game.",
+        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario spawn patch host.")]
+    [HarmonyPatch(typeof(QuestManager), "UpdateManager")]
+    internal static class ShelteredCustomScenarioQuestManagerPatches
+    {
+        [HarmonyPostfix]
+        private static void UpdateManagerPostfix()
+        {
+            try
+            {
+                CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
+                if (state == null || state.LifecycleState != CustomScenarioLifecycleState.Pending || string.IsNullOrEmpty(state.ScenarioId))
+                    return;
+
+                if (!IsWorldReadyForCustomScenarioSpawn())
+                    return;
+
+                ScenarioDef definition;
+                string error;
+                if (!ShelteredCustomScenarioService.Instance.TryCreateScenarioDef(state.ScenarioId, null, out definition, out error))
+                {
+                    MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] " + error);
+                    ShelteredCustomScenarioService.Instance.ClearState();
+                    return;
+                }
+
+                QuestInstance instance = QuestManager.instance.SpawnQuestOrScenario(definition);
+                if (instance == null)
+                {
+                    MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] QuestManager failed to spawn custom scenario: " + state.ScenarioId);
+                    ShelteredCustomScenarioService.Instance.ClearState();
+                    return;
+                }
+
+                ShelteredCustomScenarioService.Instance.MarkSpawned(state.ScenarioId);
+                MMLog.WriteInfo("[ShelteredCustomScenarioSpawn] Spawned custom scenario: " + state.ScenarioId);
+            }
+            catch (Exception ex)
+            {
+                MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] UpdateManager hook failed: " + ex.Message);
+            }
+        }
+
+        private static bool IsWorldReadyForCustomScenarioSpawn()
+        {
+            if (SaveManager.instance != null && SaveManager.instance.isLoading)
+                return false;
+            if (CutsceneManager.Instance != null && CutsceneManager.Instance.CutSceneActive)
+                return false;
+            if (QuestManager.instance == null || QuestLibrary.instance == null)
+                return false;
+            if (ExpeditionMap.Instance == null || !ExpeditionMap.Instance.initialised)
+                return false;
+
+            return true;
+        }
+    }
+
+    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioStateCleanup",
+        TargetBehavior = "Pending custom scenario state is cleared when the player leaves custom scenario startup flow.",
+        FailureMode = "A stale pending custom scenario may spawn after the player cancels or starts a vanilla mode.",
+        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario state cleanup patch host.")]
+    internal static class ShelteredCustomScenarioStateCleanupPatches
+    {
+        [HarmonyPatch(typeof(GameModeSelectionPanel), "OnSurvivalModeChosen")]
+        [HarmonyPostfix]
+        private static void SurvivalChosenPostfix()
+        {
+            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
+        }
+
+        [HarmonyPatch(typeof(SlotSelectionPanel), "OnCancel")]
+        [HarmonyPostfix]
+        private static void SlotSelectionCancelPostfix()
+        {
+            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
+        }
+    }
+
+    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSlotClickGuard",
+        TargetBehavior = "Save-slot clicks are briefly blocked while custom scenario UI buttons are being pressed.",
+        FailureMode = "Underlying save-slot controls can steal clicks from the custom scenario hub/list.",
+        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario slot click guard patch host.")]
+    internal static class ShelteredCustomScenarioSlotClickGuardPatches
+    {
+        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotSelected")]
+        [HarmonyPrefix]
+        private static bool OnSlotSelectedPrefix()
+        {
+            return !ShelteredCustomScenarioRuntimeState.IsSlotClickBlocked;
+        }
+
+        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotChosen")]
+        [HarmonyPrefix]
+        private static bool OnSlotChosenPrefix()
+        {
+            return !ShelteredCustomScenarioRuntimeState.IsSlotClickBlocked;
+        }
+
+        [HarmonyPatch(typeof(SaveSlotButton), "OnClick")]
+        [HarmonyPrefix]
+        private static bool SaveSlotButtonClickPrefix()
+        {
+            return !ShelteredCustomScenarioRuntimeState.IsSlotClickBlocked;
+        }
+    }
+}
```

### `ShelteredAPI/Scenarios/ShelteredCustomScenarioService.cs`

Commentary: Implements the neutral service for Sheltered, validates registrations, manages lifecycle state, invokes callbacks, creates ScenarioDef objects, and mirrors save descriptors.

```diff
diff --git a/ShelteredAPI/Scenarios/ShelteredCustomScenarioService.cs b/ShelteredAPI/Scenarios/ShelteredCustomScenarioService.cs
new file mode 100644
index 0000000..0000000
--- /dev/null
+++ b/ShelteredAPI/Scenarios/ShelteredCustomScenarioService.cs
@@ -0,0 +1,458 @@
+using System;
+using System.Collections.Generic;
+using System.Reflection;
+using ModAPI.Core;
+using ModAPI.Saves;
+using ModAPI.Scenarios;
+
+namespace ShelteredAPI.Scenarios
+{
+    /// <summary>
+    /// Sheltered runtime implementation of the neutral custom scenario service contract.
+    /// </summary>
+    public sealed class ShelteredCustomScenarioService : ICustomScenarioService
+    {
+        private sealed class ScenarioRecord
+        {
+            public CustomScenarioRegistration Registration;
+            public CustomScenarioInfo Info;
+        }
+
+        private static readonly ShelteredCustomScenarioService _instance = new ShelteredCustomScenarioService();
+        private readonly Dictionary<string, ScenarioRecord> _registrations = new Dictionary<string, ScenarioRecord>(StringComparer.OrdinalIgnoreCase);
+        private readonly object _sync = new object();
+        private CustomScenarioState _state = CustomScenarioState.None();
+
+        public static ShelteredCustomScenarioService Instance
+        {
+            get { return _instance; }
+        }
+
+        public event Action<CustomScenarioEventArgs> ScenarioRegistered;
+        public event Action<CustomScenarioEventArgs> ScenarioUnregistered;
+        public event Action<CustomScenarioEventArgs> ScenarioSelected;
+        public event Action<CustomScenarioEventArgs> ScenarioSpawned;
+        public event Action<CustomScenarioEventArgs> StateChanged;
+
+        public CustomScenarioState CurrentState
+        {
+            get
+            {
+                lock (_sync)
+                {
+                    return _state.Copy();
+                }
+            }
+        }
+
+        private ShelteredCustomScenarioService()
+        {
+        }
+
+        public CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration)
+        {
+            string error;
+            Assembly callerAssembly = null;
+            try { callerAssembly = Assembly.GetCallingAssembly(); } catch { }
+
+            CustomScenarioRegistration normalized = NormalizeRegistration(registration, callerAssembly, out error);
+            if (normalized == null)
+                return CustomScenarioRegistrationResult.Failed(error);
+
+            ScenarioRecord record = CreateRecord(normalized);
+            bool replacedExisting;
+            lock (_sync)
+            {
+                replacedExisting = _registrations.ContainsKey(record.Info.Id);
+                _registrations[record.Info.Id] = record;
+            }
+
+            MirrorSaveScenarioDescriptor(record.Info);
+            Raise(ScenarioRegistered, CustomScenarioEventType.Registered, record.Info);
+            return CustomScenarioRegistrationResult.Ok(record.Info.Id, replacedExisting);
+        }
+
+        public bool Unregister(string scenarioId)
+        {
+            if (string.IsNullOrEmpty(scenarioId))
+                return false;
+
+            ScenarioRecord removed = null;
+            bool clearedState = false;
+            lock (_sync)
+            {
+                if (!_registrations.TryGetValue(scenarioId, out removed))
+                    return false;
+
+                _registrations.Remove(scenarioId);
+                if (string.Equals(_state.ScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
+                {
+                    _state = CustomScenarioState.None();
+                    clearedState = true;
+                }
+            }
+
+            Raise(ScenarioUnregistered, CustomScenarioEventType.Unregistered, removed.Info);
+            if (clearedState)
+                Raise(StateChanged, CustomScenarioEventType.Cleared, removed.Info);
+            return true;
+        }
+
+        public bool TryGet(string scenarioId, out CustomScenarioInfo scenario)
+        {
+            scenario = null;
+            if (string.IsNullOrEmpty(scenarioId))
+                return false;
+
+            lock (_sync)
+            {
+                ScenarioRecord record;
+                if (!_registrations.TryGetValue(scenarioId, out record))
+                    return false;
+
+                scenario = record.Info;
+                return true;
+            }
+        }
+
+        public CustomScenarioInfo[] List()
+        {
+            List<CustomScenarioInfo> items = new List<CustomScenarioInfo>();
+            lock (_sync)
+            {
+                foreach (KeyValuePair<string, ScenarioRecord> pair in _registrations)
+                    items.Add(pair.Value.Info);
+            }
+
+            items.Sort(CompareScenarioInfo);
+            return items.ToArray();
+        }
+
+        public bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage)
+        {
+            ScenarioDef scenarioDef;
+            bool result = TryCreateScenarioDef(scenarioId, context, out scenarioDef, out errorMessage);
+            definition = scenarioDef;
+            return result;
+        }
+
+        internal bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage)
+        {
+            definition = null;
+            errorMessage = null;
+
+            ScenarioRecord record;
+            lock (_sync)
+            {
+                if (!_registrations.TryGetValue(scenarioId, out record))
+                {
+                    errorMessage = "Custom scenario is not registered: " + scenarioId;
+                    return false;
+                }
+            }
+
+            CustomScenarioRegistration registration = record.Registration;
+            if (registration.Definition != null)
+            {
+                definition = registration.Definition as ScenarioDef;
+                if (definition == null)
+                {
+                    errorMessage = "Registered definition for '" + record.Info.Id + "' is not a Sheltered ScenarioDef.";
+                    return false;
+                }
+
+                return true;
+            }
+
+            if (registration.DefinitionFactory == null)
+            {
+                errorMessage = "Custom scenario has no ScenarioDef or definition factory: " + record.Info.Id;
+                return false;
+            }
+
+            try
+            {
+                CustomScenarioBuildContext buildContext = PrepareBuildContext(record, context);
+                object built = registration.DefinitionFactory(buildContext);
+                definition = built as ScenarioDef;
+                if (definition == null)
+                {
+                    errorMessage = "Definition factory for '" + record.Info.Id + "' did not return a Sheltered ScenarioDef.";
+                    return false;
+                }
+
+                return true;
+            }
+            catch (Exception ex)
+            {
+                errorMessage = "Definition factory for '" + record.Info.Id + "' failed: " + ex.Message;
+                MMLog.WriteError("[ShelteredCustomScenarioService] " + errorMessage);
+                return false;
+            }
+        }
+
+        internal bool MarkSelected(string scenarioId)
+        {
+            ScenarioRecord record;
+            lock (_sync)
+            {
+                if (!_registrations.TryGetValue(scenarioId, out record))
+                    return false;
+
+                _state = new CustomScenarioState
+                {
+                    ScenarioId = record.Info.Id,
+                    LifecycleState = CustomScenarioLifecycleState.Pending
+                };
+            }
+
+            CustomScenarioEventArgs args = CreateArgs(CustomScenarioEventType.Selected, record.Info);
+            InvokeRegistrationCallback(record.Registration.OnSelected, args, record.Info.Id, "OnSelected");
+            Raise(ScenarioSelected, args);
+            Raise(StateChanged, args);
+            return true;
+        }
+
+        internal bool MarkSpawned(string scenarioId)
+        {
+            ScenarioRecord record;
+            lock (_sync)
+            {
+                if (!_registrations.TryGetValue(scenarioId, out record))
+                    return false;
+
+                _state = new CustomScenarioState
+                {
+                    ScenarioId = record.Info.Id,
+                    LifecycleState = CustomScenarioLifecycleState.Active
+                };
+            }
+
+            CustomScenarioEventArgs args = CreateArgs(CustomScenarioEventType.Spawned, record.Info);
+            InvokeRegistrationCallback(record.Registration.OnSpawned, args, record.Info.Id, "OnSpawned");
+            Raise(ScenarioSpawned, args);
+            Raise(StateChanged, args);
+            return true;
+        }
+
+        internal void ClearState()
+        {
+            CustomScenarioInfo previousInfo = null;
+            bool hadState = false;
+            lock (_sync)
+            {
+                if (!string.IsNullOrEmpty(_state.ScenarioId))
+                {
+                    hadState = true;
+                    ScenarioRecord record;
+                    if (_registrations.TryGetValue(_state.ScenarioId, out record))
+                        previousInfo = record.Info;
+                }
+
+                _state = CustomScenarioState.None();
+            }
+
+            if (hadState)
+                Raise(StateChanged, CustomScenarioEventType.Cleared, previousInfo);
+        }
+
+        private static CustomScenarioRegistration NormalizeRegistration(
+            CustomScenarioRegistration registration,
+            Assembly callerAssembly,
+            out string error)
+        {
+            error = null;
+            if (registration == null)
+            {
+                error = "Custom scenario registration cannot be null.";
+                return null;
+            }
+
+            string id = TrimToNull(registration.Id);
+            if (id == null)
+            {
+                error = "Custom scenario id is required.";
+                return null;
+            }
+
+            string displayName = TrimToNull(registration.DisplayName);
+            if (displayName == null)
+            {
+                error = "Custom scenario display name is required for '" + id + "'.";
+                return null;
+            }
+
+            if (registration.Definition == null && registration.DefinitionFactory == null)
+            {
+                error = "Custom scenario '" + id + "' requires a Sheltered ScenarioDef or a definition factory.";
+                return null;
+            }
+
+            if (registration.Definition != null && !(registration.Definition is ScenarioDef))
+            {
+                error = "Custom scenario '" + id + "' definition must be a Sheltered ScenarioDef.";
+                return null;
+            }
+
+            Assembly ownerAssembly = registration.OwnerAssembly ?? callerAssembly;
+            string ownerModId = TrimToNull(registration.OwnerModId) ?? ResolveOwnerModId(ownerAssembly);
+
+            return new CustomScenarioRegistration
+            {
+                Id = id,
+                DisplayName = displayName,
+                Description = registration.Description ?? string.Empty,
+                Version = TrimToNull(registration.Version) ?? "1.0",
+                Order = registration.Order,
+                OwnerModId = ownerModId,
+                OwnerAssembly = ownerAssembly,
+                Definition = registration.Definition,
+                DefinitionFactory = registration.DefinitionFactory,
+                OnSelected = registration.OnSelected,
+                OnSpawned = registration.OnSpawned,
+                UserData = registration.UserData
+            };
+        }
+
+        private static ScenarioRecord CreateRecord(CustomScenarioRegistration registration)
+        {
+            CustomScenarioInfo info = new CustomScenarioInfo(
+                registration.Id,
+                registration.DisplayName,
+                registration.Description,
+                registration.Version,
+                registration.Order,
+                registration.OwnerModId,
+                registration.Definition != null,
+                registration.DefinitionFactory != null);
+
+            return new ScenarioRecord
+            {
+                Registration = registration,
+                Info = info
+            };
+        }
+
+        private CustomScenarioBuildContext PrepareBuildContext(ScenarioRecord record, CustomScenarioBuildContext context)
+        {
+            CustomScenarioBuildContext result = context ?? new CustomScenarioBuildContext();
+            if (string.IsNullOrEmpty(result.ScenarioId))
+                result.ScenarioId = record.Info.Id;
+            if (string.IsNullOrEmpty(result.OwnerModId))
+                result.OwnerModId = record.Info.OwnerModId;
+            if (result.State == null)
+                result.State = CurrentState;
+            if (result.UserData == null)
+                result.UserData = record.Registration.UserData;
+            return result;
+        }
+
+        private static int CompareScenarioInfo(CustomScenarioInfo left, CustomScenarioInfo right)
+        {
+            if (ReferenceEquals(left, right)) return 0;
+            if (left == null) return 1;
+            if (right == null) return -1;
+
+            int order = left.Order.CompareTo(right.Order);
+            if (order != 0) return order;
+
+            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
+            if (name != 0) return name;
+
+            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
+        }
+
+        private static string ResolveOwnerModId(Assembly ownerAssembly)
+        {
+            if (ownerAssembly == null)
+                return null;
+
+            try
+            {
+                ModEntry entry;
+                if (ModRegistry.TryGetModByAssembly(ownerAssembly, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
+                    return entry.Id;
+            }
+            catch
+            {
+            }
+
+            try { return ownerAssembly.GetName().Name; }
+            catch { return null; }
+        }
+
+        private static void MirrorSaveScenarioDescriptor(CustomScenarioInfo info)
+        {
+            if (info == null || string.IsNullOrEmpty(info.Id))
+                return;
+
+            try
+            {
+                ScenarioRegistry.RegisterScenario(new ScenarioDescriptor
+                {
+                    id = info.Id,
+                    displayName = info.DisplayName,
+                    description = info.Description,
+                    version = info.Version
+                });
+            }
+            catch (Exception ex)
+            {
+                MMLog.WarnOnce("ShelteredCustomScenarioService.MirrorSaveScenarioDescriptor." + info.Id, ex.Message);
+            }
+        }
+
+        private CustomScenarioEventArgs CreateArgs(CustomScenarioEventType eventType, CustomScenarioInfo info)
+        {
+            return new CustomScenarioEventArgs(eventType, info, CurrentState);
+        }
+
+        private void Raise(Action<CustomScenarioEventArgs> handler, CustomScenarioEventType eventType, CustomScenarioInfo info)
+        {
+            Raise(handler, CreateArgs(eventType, info));
+        }
+
+        private static void Raise(Action<CustomScenarioEventArgs> handler, CustomScenarioEventArgs args)
+        {
+            if (handler == null)
+                return;
+
+            try
+            {
+                handler(args);
+            }
+            catch (Exception ex)
+            {
+                MMLog.WarnOnce("ShelteredCustomScenarioService.Event." + args.EventType, ex.Message);
+            }
+        }
+
+        private static void InvokeRegistrationCallback(
+            Action<CustomScenarioEventArgs> callback,
+            CustomScenarioEventArgs args,
+            string scenarioId,
+            string callbackName)
+        {
+            if (callback == null)
+                return;
+
+            try
+            {
+                callback(args);
+            }
+            catch (Exception ex)
+            {
+                MMLog.WarnOnce("ShelteredCustomScenarioService." + callbackName + "." + scenarioId, ex.Message);
+            }
+        }
+
+        private static string TrimToNull(string value)
+        {
+            if (string.IsNullOrEmpty(value))
+                return null;
+
+            string trimmed = value.Trim();
+            return trimmed.Length == 0 ? null : trimmed;
+        }
+    }
+}
```

### `ShelteredAPI/Scenarios/ShelteredScenarioDefBuilder.cs`

Commentary: Centralizes reflection required to construct ScenarioDef and ScenarioStage. This keeps private-field knowledge in one Sheltered-specific place instead of duplicating reflection in mods.

```diff
diff --git a/ShelteredAPI/Scenarios/ShelteredScenarioDefBuilder.cs b/ShelteredAPI/Scenarios/ShelteredScenarioDefBuilder.cs
new file mode 100644
index 0000000..0000000
--- /dev/null
+++ b/ShelteredAPI/Scenarios/ShelteredScenarioDefBuilder.cs
@@ -0,0 +1,134 @@
+using System;
+using System.Collections;
+using System.Collections.Generic;
+using System.Reflection;
+
+namespace ShelteredAPI.Scenarios
+{
+    /// <summary>
+    /// Helper for constructing Sheltered ScenarioDef and ScenarioStage objects whose serialized fields are private.
+    /// </summary>
+    public sealed class ShelteredScenarioDefBuilder
+    {
+        private static readonly FieldInfo QuestIdField = typeof(QuestDefBase).GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
+        private static readonly FieldInfo QuestNameKeyField = typeof(QuestDefBase).GetField("m_nameKey", BindingFlags.NonPublic | BindingFlags.Instance);
+        private static readonly FieldInfo QuestDescriptionKeyField = typeof(QuestDefBase).GetField("m_descriptionKey", BindingFlags.NonPublic | BindingFlags.Instance);
+        private static readonly FieldInfo QuestSelectionField = typeof(QuestDefBase).GetField("m_selectionProperties", BindingFlags.NonPublic | BindingFlags.Instance);
+        private static readonly FieldInfo ScenarioStagesField = typeof(ScenarioDef).GetField("m_stages", BindingFlags.NonPublic | BindingFlags.Instance);
+        private static readonly FieldInfo StageIdField = typeof(ScenarioStage).GetField("m_id", BindingFlags.NonPublic | BindingFlags.Instance);
+
+        private static readonly Type QuestSelectionType = typeof(QuestDefBase).GetNestedType("QuestSelection", BindingFlags.Public | BindingFlags.NonPublic);
+        private static readonly FieldInfo SelectionUseSurvivalField = GetSelectionField("m_useInSurvival");
+        private static readonly FieldInfo SelectionUseSurroundedField = GetSelectionField("m_useInSurrounded");
+        private static readonly FieldInfo SelectionUseStasisField = GetSelectionField("m_useInStasis");
+        private static readonly FieldInfo SelectionOnceOnlyField = GetSelectionField("m_onceOnly");
+
+        private readonly ScenarioDef _definition = new ScenarioDef();
+        private readonly List<ScenarioStage> _stages = new List<ScenarioStage>();
+
+        public ShelteredScenarioDefBuilder SetId(string id)
+        {
+            SetStringField(_definition, QuestIdField, id);
+            return this;
+        }
+
+        public ShelteredScenarioDefBuilder SetNameKey(string nameKey)
+        {
+            SetStringField(_definition, QuestNameKeyField, nameKey);
+            return this;
+        }
+
+        public ShelteredScenarioDefBuilder SetDescriptionKey(string descriptionKey)
+        {
+            SetStringField(_definition, QuestDescriptionKeyField, descriptionKey);
+            return this;
+        }
+
+        public ShelteredScenarioDefBuilder UseInModes(bool survival, bool surrounded, bool stasis)
+        {
+            object selection = GetSelection();
+            if (selection == null)
+                return this;
+
+            SetBoolField(selection, SelectionUseSurvivalField, survival);
+            SetBoolField(selection, SelectionUseSurroundedField, surrounded);
+            SetBoolField(selection, SelectionUseStasisField, stasis);
+            return this;
+        }
+
+        public ShelteredScenarioDefBuilder OnceOnly(bool onceOnly)
+        {
+            object selection = GetSelection();
+            if (selection != null)
+                SetBoolField(selection, SelectionOnceOnlyField, onceOnly);
+            return this;
+        }
+
+        public ShelteredScenarioDefBuilder AddSimpleStage(string stageId)
+        {
+            ScenarioStage stage = CreateStage(stageId);
+            if (stage != null)
+                _stages.Add(stage);
+            return this;
+        }
+
+        public ShelteredScenarioDefBuilder AddStage(ScenarioStage stage)
+        {
+            if (stage != null)
+                _stages.Add(stage);
+            return this;
+        }
+
+        public ScenarioDef Build()
+        {
+            IList runtimeStages = ScenarioStagesField != null ? ScenarioStagesField.GetValue(_definition) as IList : null;
+            if (runtimeStages != null)
+            {
+                runtimeStages.Clear();
+                for (int i = 0; i < _stages.Count; i++)
+                    runtimeStages.Add(_stages[i]);
+            }
+
+            return _definition;
+        }
+
+        public static ScenarioStage CreateStage(string stageId)
+        {
+            try
+            {
+                ScenarioStage stage = new ScenarioStage();
+                SetStringField(stage, StageIdField, stageId);
+                return stage;
+            }
+            catch
+            {
+                return null;
+            }
+        }
+
+        private object GetSelection()
+        {
+            if (QuestSelectionField == null)
+                return null;
+
+            return QuestSelectionField.GetValue(_definition);
+        }
+
+        private static FieldInfo GetSelectionField(string fieldName)
+        {
+            return QuestSelectionType != null ? QuestSelectionType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance) : null;
+        }
+
+        private static void SetStringField(object target, FieldInfo field, string value)
+        {
+            if (target != null && field != null)
+                field.SetValue(target, value ?? string.Empty);
+        }
+
+        private static void SetBoolField(object target, FieldInfo field, bool value)
+        {
+            if (target != null && field != null)
+                field.SetValue(target, value);
+        }
+    }
+}
```

### `ShelteredAPI/Scenarios/ShelteredScenarioRegistration.cs`

Commentary: Provides typed Sheltered registration helpers that adapt ScenarioDef, factories, or class-based scenarios into the neutral ModAPI registration object.

```diff
diff --git a/ShelteredAPI/Scenarios/ShelteredScenarioRegistration.cs b/ShelteredAPI/Scenarios/ShelteredScenarioRegistration.cs
new file mode 100644
index 0000000..0000000
--- /dev/null
+++ b/ShelteredAPI/Scenarios/ShelteredScenarioRegistration.cs
@@ -0,0 +1,65 @@
+using System;
+using System.Reflection;
+using ModAPI.Scenarios;
+
+namespace ShelteredAPI.Scenarios
+{
+    public delegate ScenarioDef ShelteredScenarioDefinitionFactory(CustomScenarioBuildContext context);
+
+    /// <summary>
+    /// Typed Sheltered helpers for creating neutral ModAPI custom scenario registrations.
+    /// </summary>
+    public static class ShelteredScenarioRegistration
+    {
+        public static CustomScenarioRegistration FromDefinition(string id, string displayName, ScenarioDef definition)
+        {
+            return new CustomScenarioRegistration
+            {
+                Id = id,
+                DisplayName = displayName,
+                Definition = definition,
+                OwnerAssembly = GetCallerAssembly()
+            };
+        }
+
+        public static CustomScenarioRegistration FromScenario(IShelteredCustomScenario scenario)
+        {
+            if (scenario == null)
+                return null;
+
+            return new CustomScenarioRegistration
+            {
+                Id = scenario.Id,
+                DisplayName = scenario.DisplayName,
+                Description = scenario.Description,
+                Version = scenario.Version,
+                Order = scenario.Order,
+                DefinitionFactory = new CustomScenarioDefinitionFactory(
+                    delegate(CustomScenarioBuildContext context) { return scenario.BuildDefinition(context); }),
+                OnSelected = scenario.OnSelected,
+                OnSpawned = scenario.OnSpawned,
+                UserData = scenario.UserData,
+                OwnerAssembly = GetCallerAssembly()
+            };
+        }
+
+        public static CustomScenarioRegistration FromFactory(string id, string displayName, ShelteredScenarioDefinitionFactory factory)
+        {
+            return new CustomScenarioRegistration
+            {
+                Id = id,
+                DisplayName = displayName,
+                DefinitionFactory = factory != null
+                    ? new CustomScenarioDefinitionFactory(delegate(CustomScenarioBuildContext context) { return factory(context); })
+                    : null,
+                OwnerAssembly = GetCallerAssembly()
+            };
+        }
+
+        private static Assembly GetCallerAssembly()
+        {
+            try { return Assembly.GetCallingAssembly(); }
+            catch { return null; }
+        }
+    }
+}
```


