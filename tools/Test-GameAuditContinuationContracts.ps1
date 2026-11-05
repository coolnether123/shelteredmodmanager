[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-Method([string]$path, [string]$signature, [string]$after = '') {
    $text = Get-Content (Join-Path $root $path) -Raw
    if ($after) { $text = $text.Substring($text.IndexOf($after, [StringComparison]::Ordinal)) }
    $start = $text.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production method: $signature" }
    $open = $text.IndexOf('{', $start)
    $depth = 1
    $end = $open + 1
    while ($depth -gt 0) {
        if ($text[$end] -eq '{') { $depth++ }
        if ($text[$end] -eq '}') { $depth-- }
        $end++
    }
    $text.Substring($start, $end - $start)
}
$rngPath = 'ShelteredAPI/Scenarios/Infrastructure/Harmony/ScenarioRngPatches.cs'
$rng = Get-Content (Join-Path $root $rngPath) -Raw
$domain = Read-Method $rngPath 'private static string GetDomainName('
$manifestStart = $rng.IndexOf('private static readonly string[] TargetMethodManifest')
$manifestEnd = $rng.IndexOf('private static readonly MethodInfo RangeII')
$manifest = $rng.Substring($manifestStart, $manifestEnd - $manifestStart)
$guard = Get-Content (Join-Path $root 'ShelteredAPI/Scenarios/Infrastructure/Unity/ScenarioLoadingTransitionGuard.cs') -Raw
$runtimePath = 'ShelteredAPI/Core/LoadingTransitionRuntime.cs'
$reset = @(
    (Read-Method $runtimePath 'public static void ResetAfterFailedTransition('),
    (Read-Method $runtimePath 'private static void ResetLoadingScreen('),
    (Read-Method $runtimePath 'private static void SetPrivateField('),
    (Read-Method $runtimePath 'private static object GetPrivateField(')
) -join "`n"
$launch = Read-Method 'ShelteredAPI/Scenarios/Application/Selection/ScenarioLaunchCoordinator.cs' 'private static bool BeginDirectSceneTransition('
$characterPath = 'ShelteredAPI/Characters/Internal/CharacterEffectSystemImpl.cs'
$character = @(
    (Read-Method $characterPath 'public void SwapEncounterCharacter('),
    (Read-Method $characterPath 'internal void SwapEncounterActorStats('),
    (Read-Method $characterPath 'private static bool TrySetMember(')
) -join "`n"
$factory = @(
    (Read-Method 'ShelteredAPI/Characters/Internal/CharacterRuntime.cs' 'internal static void FillFromFamilyMember('),
    (Read-Method 'ShelteredAPI/Characters/Internal/CharacterRuntime.cs' 'internal static void FillFromVisitor(')
) -join "`n"
$slotPath = 'ShelteredAPI/Saves/Paging/SlotSelectionPatchCoordinator.cs'
$slot = @(
    (Read-Method $slotPath 'internal static bool OnSlotChosenPrefix('),
    (Read-Method $slotPath 'private static void QueueCustomLoad('),
    (Read-Method $slotPath 'internal static bool OnDeleteMessageBoxPrefix(')
) -join "`n"
$pagePath = 'ShelteredAPI/Saves/Paging/PagingManager.cs'
$pageActions = (Read-Method $pagePath 'public static void ChangePage(') + "`n" +
    (Read-Method $pagePath 'private static void ToggleSnapshotSort(')
# Windows PowerShell's fixture compiler predates interpolated strings. Keep the control flow intact.
$pageActions = $pageActions.Replace('MMLog.WriteError($"[PagingManager] Error raising PageChanged event: {ex}");', 'MMLog.WriteError(ex.ToString());').Replace('MMLog.WriteError($"[PagingManager] ChangePage failed: {ex}");', 'MMLog.WriteError(ex.ToString());')
$actorPath = 'ShelteredAPI/Actors/Internal/ActorSystemImpl.cs'
$actorRegistration = (Read-Method $actorPath 'internal void EnsureRegistered(') + "`n" +
    (Read-Method $actorPath 'private static void Postfix(' 'private static class GameTime_Awake').Replace('private static void Postfix(', 'public static void Awake(')
$characterRegistration = (Read-Method $characterPath 'private void EnsureRegistered(').Replace('private void EnsureRegistered(', 'public void EnsureRegistered(') + "`n" +
    (Read-Method $characterPath 'private static void Postfix(' 'private static class GameTime_Awake').Replace('private static void Postfix(', 'public static void Awake(')
$proxyPath = 'ShelteredAPI/Persistence/PersistentDataAPI.cs'
$proxyRegistration = (Read-Method $proxyPath 'public static void EnsureRegistered(') + "`n" +
    (Read-Method $proxyPath 'internal static void RegisterForScene(').Replace('internal static void RegisterForScene(', 'public static void RegisterForScene(')
$collectionRegistration = (Read-Method 'ShelteredAPI/Persistence/ModPersistence.cs' 'private static void Postfix(' 'private static class GameTime_Awake_Patch').Replace('private static void Postfix(', 'public static void Awake(')
$characterPersistence = @(
    (Read-Method $characterPath 'public ICharacterProxy RestoreSyntheticCharacter('),
    (Read-Method $characterPath 'private SyntheticCharacterProxy BuildSynthetic('),
    (Read-Method $characterPath 'private void HandleSessionStarted('),
    (Read-Method $characterPath 'private static CharacterSaveData ToSaveData(')
) -join "`n"
$saveDto = Get-Content (Join-Path $root 'ShelteredAPI/Characters/Models/CharacterSaveData.cs') -Raw
$livePath = 'ShelteredAPI/Characters/Internal/CharacterRuntime.cs'
$liveEffect = @(
    (Read-Method $livePath 'internal void Tick(').Replace('internal void Tick(', 'public void Tick('),
    (Read-Method $livePath 'internal void SyncToGameObject('),
    (Read-Method $livePath 'private void SyncHealth('),
    (Read-Method $livePath 'private static bool TrySetField(')
) -join "`n"
$effectUpdate = (Read-Method $characterPath 'private void Update(').Replace('private void Update(', 'public void Update(')
$actorUpdate = (Read-Method 'ShelteredAPI/Actors/Internal/ActorSystemImpl.Execution.cs' 'internal void Update(').Replace('internal void Update(', 'public void Update(')
$journalReservation = (Read-Method 'ShelteredAPI/Scenarios/Infrastructure/Runtime/ScheduledJournalRuntimeService.cs' 'private static bool TryReserveWrite(').Replace('private static bool TryReserveWrite(', 'public static bool TryReserveWrite(')
$worldPath = 'ShelteredAPI/Scenarios/Infrastructure/Runtime/ScheduledWorldEventRuntimeService.cs'
$visitorAttributes = (Read-Method $worldPath 'private static FamilySpawner.CharacterAttributes CreateAttributes(').Replace('private static FamilySpawner.CharacterAttributes CreateAttributes(', 'public static FamilySpawner.CharacterAttributes CreateAttributes(') + "`n" +
    (Read-Method $worldPath 'private static ScenarioNpcDefinition FindScenarioNpc(')
if ($launch.IndexOf('OwnDirectLaunchTransition(') -gt $launch.IndexOf('ShowLoadingScreen(')) {
    throw 'Transition ownership must be set before the show call can trigger recovery.'
}
Add-Type -TypeDefinition (@"
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using ShelteredAPI.Characters.Models;
using ShelteredAPI.Characters.Abstractions;
using UnityEngine;
using UnityEngine.SceneManagement;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace UnityEngine {
    public class GameObject { public bool active = true; public void SetActive(bool value) { active = value; } }
    public static class Time { public static float timeScale; public static float deltaTime = 1f; }
    public static class AudioListener { public static bool pause; }
}
namespace UnityEngine.SceneManagement {
    public struct Scene { public string name; }
    public static class SceneManager { public static Scene GetActiveScene() { return new Scene { name = "MenuScene" }; } }
}
namespace ModAPI.Core {
    public static class MMLog { public static void WriteInfo(string s) {} public static void WriteWarning(string s) {} public static void WriteError(string s) {} public static void WriteDebug(string s) {} }
    public static class ModPrefs { public static int GetInt(string s, int d) { return 1; } public static void SetInt(string s, int v) {} public static void Save() {} }
}
namespace ShelteredAPI.Saves { public static class Events { public static void RaisePageChanged(int p) {} } }
public class LoadingScreen {
    public static LoadingScreen Instance = new LoadingScreen();
    public static string nextLevel = "ShelterScene";
    private int m_showCount = 1;
    private bool m_showScreen = true;
    private UnityEngine.GameObject m_loadingImage = new UnityEngine.GameObject();
    public bool isShowing { get { return m_showCount > 0; } }
    public bool ImageActive { get { return m_loadingImage.active; } }
    public bool ShowScreen { get { return m_showScreen; } }
    public static void ClearNextLevel() { nextLevel = ""; }
    public void HideLoadingScreen(bool force) {}
}
public class ScreenFade {
    public static ScreenFade instance = new ScreenFade();
    public int clears;
    public void ClearFade(float seconds, bool instant) {
        if (LoadingScreen.nextLevel.Length != 0 || LoadingScreen.Instance.ShowScreen)
            throw new Exception("Clear callbacks must not queue another scene load");
        if (instant) clears++;
    }
}
public class SaveManager {
    public static SaveManager instance;
    public bool isLoading, isSaving, isDeleting;
    public enum SaveType { Invalid, Slot1 }
    public int loads;
    public System.Collections.Generic.List<object> saveables = new System.Collections.Generic.List<object>();
    public System.Collections.Generic.List<object> toLoad = new System.Collections.Generic.List<object>();
    public void RegisterSaveable(object item) {
        if (!saveables.Contains(item)) saveables.Add(item);
        if (isLoading && !toLoad.Contains(item)) toLoad.Add(item);
    }
    public void ClearAll() { saveables.Clear(); toLoad.Clear(); }
    public bool SetSlotToLoad(int slot) { loads++; return true; }
}
public static class ShelteredActors { public static ActorRegistration InternalInstance = new ActorRegistration(); public static object Instance; }
public static class CharacterEffectSystem { public static CharacterRegistration InternalInstance = new CharacterRegistration(); }
public class ActorRegistration {
    private bool _registered;
    $actorRegistration
    public void Tick() { EnsureRegistered(); }
}
public class CharacterRegistration {
    private bool _registered;
    $characterRegistration
}
public static class ProxyRegistration {
    private static bool _registered;
    public static readonly object Instance = new object();
    $proxyRegistration
}
public static class CollectionRegistration {
    private static System.Collections.Generic.List<object> _registeredCollections = new System.Collections.Generic.List<object> { new object(), new object() };
    $collectionRegistration
}
public class UIPanelManager { public static UIPanelManager instance; public bool timePaused; }
public class CutsceneManager { public static CutsceneManager Instance; public bool CutSceneActive; public bool pauseCutsceneManager; }
public class FadeManager { public static FadeManager Instance; public void FadeFromBlack(bool b) {} }
namespace ShelteredAPI.Scenarios.Infrastructure.Unity {
    public static class ScenarioWorldReady { public static bool IsShelterSceneActive() { return false; } }
}
public class CharacterMeshOptions { public class CharacterMeshType {} public class CharacterPreset {} }
public static class RngFixture {
    $manifest
    $domain
    public static string Domain(Type t) { return GetDomainName(t); }
    public static bool Resolves(string entry, Type type, string member) {
        return Array.IndexOf(TargetMethodManifest, entry) >= 0 && Type.GetType(entry.Split('|')[0]) == type
            && entry.Split('|')[1] == member;
    }
}
public static class RecoveryFixture {
    $reset
    private static void ResetSaveManager() {}
}
public class BaseStat {
    public int Level;
    public int LevelCap = 20;
    public void SetInitialLevel(int level, int cap) { LevelCap = cap; Level = Math.Min(level, cap); }
}
public class BaseStats {
    public BaseStat Strength = new BaseStat(), Dexterity = new BaseStat(), Intelligence = new BaseStat(),
        Charisma = new BaseStat(), Perception = new BaseStat();
    public void Initialize() {}
}
public class EncounterCharacter {
    private string firstName = "Old", lastName = "Name";
    private int health = 100, m_maxHealth = 100;
    public int HealthLost = 7;
    public int Health { get { return health; } set { HealthLost += health - value; health = value; } }
    public int maxHealth { get { return m_maxHealth; } }
    public string Name { get { return firstName + " " + lastName; } }
    public BaseStats Stats = new BaseStats();
    public void SetStats(BaseStats stats) { Stats = stats; }
}
public class CharacterDataModel {
    public int UniqueId, Health, MaxHealth, StrengthLevel, DexterityLevel, IntelligenceLevel, CharismaLevel, PerceptionLevel;
    public string FirstName, LastName, MeshId, PersistenceKey, SourceMod;
    public bool IsMale, IsPersistent;
    public float CreatedAtTime;
    public CharacterSource Source;
    public void SetCustomData(string key, object value) {}
    public Dictionary<string, object> SnapshotCustomData() { return new Dictionary<string, object>(); }
}
public interface ICharacterProxy { CharacterDataModel Data { get; } }
public class CharacterProxy : ICharacterProxy {
    public CharacterDataModel Data { get; set; }
}
public class BaseCharacter {
    public string firstName = "Family", lastName = "Name";
    public int health = 82, maxHealth = 120;
    public bool isDead { get { return health <= 0; } }
    public int damageCalls, healCalls;
    public bool preventDeath;
    public void SetMaxHealth(int value) { maxHealth = value; }
    public void Damage(int value) { damageCalls++; health = Math.Max(0, health - value); if (preventDeath && health == 0) health = 1; }
    public void Heal(int value) { healCalls++; health = Math.Min(maxHealth, health + value); }
}
public class FamilyMember : BaseCharacter {
    public BaseStats BaseStats = new BaseStats();
    public int GetId() { return 4; }
}
public class NpcVisitor : FamilyMember { public int npcId = 5; }
public class ItemManager { public enum ItemType { Undefined, Knife } }
public static class InventoryHelper {
    public static bool ResolveItemType(string id, out ItemManager.ItemType type) { type = ItemManager.ItemType.Knife; return id == "knife"; }
}
public class FamilySpawner {
    public class CharacterAttributes {
        public string m_firstName, m_meshId;
        public int m_strengthLevel, m_dexterityLevel, m_charismaLevel, m_perceptionLevel, m_intelligenceLevel;
        public ItemManager.ItemType m_weapon;
        public void Randomize() { m_meshId = "man"; m_firstName = "Random"; m_strengthLevel = 2; }
    }
}
public class ScenarioNpcStats { public int Strength, Dexterity, Charisma, Perception, Intelligence; }
public class ScenarioNpcDefinition { public string CharacterId, DisplayName, PresetId, WeaponItemId; public ScenarioNpcStats Stats; }
public class ScenarioDefinition { public List<ScenarioNpcDefinition> ScenarioCharacters = new List<ScenarioNpcDefinition>(); }
public static class VisitorAttributeFixture { $visitorAttributes }
public class CharacterFixture {
    $character
    $factory
    public static CharacterDataModel Family(FamilyMember m) { var d = new CharacterDataModel(); FillFromFamilyMember(d, m); return d; }
    public static CharacterDataModel Visitor(NpcVisitor m) { var d = new CharacterDataModel(); FillFromVisitor(d, m); return d; }
}
namespace ShelteredAPI.Characters.Abstractions {
    public enum CharacterSource { Synthetic, RealFamily, Visitor }
}
public enum CharacterState { SyntheticAbsent }
public class SyntheticCharacterProxy : ICharacterProxy {
    public SyntheticCharacterProxy(int id) { UniqueId = id; }
    public int UniqueId;
    public CharacterDataModel Data { get; set; }
    public object Effects, Attributes;
    public CharacterState State;
    public bool IsLoadedOnShelterEntry;
    public bool IsActive = true;
    public string PersistenceKey { get { return Data.PersistenceKey; } }
    public string SourceMod { get { return Data.SourceMod; } }
    public CharacterSource Source { get { return Data.Source; } }
    public bool IsPersistent { get { return Data.IsPersistent; } }
}
public class BasicCharacterEffects {
    public BasicCharacterEffects(ICharacterProxy c) {}
    public Action<float> OnTick;
    public int ticks;
    public void Tick(float dt) { ticks++; if (OnTick != null) OnTick(dt); }
    public void RestoreFromSaveData(List<EffectSaveData> data) {}
    public List<EffectSaveData> ToSaveData() { return new List<EffectSaveData>(); }
}
public class BasicCharacterAttributes {
    public int ticks;
    public void Tick(float dt) { ticks++; }
    public void RestoreFromSaveData(List<AttributeSaveData> data) {}
    public List<AttributeSaveData> ToSaveData() { return new List<AttributeSaveData>(); }
}
public static class RuntimeSerialization {
    public static object DeserializeFromString(string s) { return s; }
    public static string SerializeToString(object o) { return ""; }
}
public class LiveCharacterProxy : ICharacterProxy {
    private FamilyMember _member;
    private NpcVisitor _npc = null;
    private CharacterDataModel _data;
    private BasicCharacterEffects _effects;
    private BasicCharacterAttributes _attributes = new BasicCharacterAttributes();
    private Action<ICharacterProxy, string, object> _dataChanged = delegate {};
    private CharacterState State = CharacterState.SyntheticAbsent;
    public CharacterDataModel Data { get { return _data; } }
    private void RefreshState() {}
    $liveEffect
    public LiveCharacterProxy(FamilyMember member) { _member = member; _data = CharacterFixture.Family(member); _effects = new BasicCharacterEffects(this); }
    public void SetHealthOnTick(int value) { _effects.OnTick = delegate { _data.Health = value; }; }
    public void Sync() { SyncToGameObject(); }
}
public class EffectUpdateFixture {
    private object _sync = new object();
    private Dictionary<int, LiveCharacterProxy> _liveFamily = new Dictionary<int, LiveCharacterProxy>(), _liveVisitors = new Dictionary<int, LiveCharacterProxy>();
    private Dictionary<int, SyntheticCharacterProxy> _syntheticById = new Dictionary<int, SyntheticCharacterProxy>();
    public int refreshes;
    private void EnsureRegistered() {}
    private void RefreshLiveCharacters() { refreshes++; }
    private void SynchronizeActors() {}
    $effectUpdate
    public void Add(SyntheticCharacterProxy p) { _syntheticById[p.UniqueId] = p; }
}
public class JournalManager {}
public static class JournalFixture {
    private static readonly HashSet<string> WrittenMinuteKeys = new HashSet<string>(StringComparer.Ordinal);
    private static JournalManager _writtenMinuteManager;
    $journalReservation
}
public class ActorUpdateFixture {
    private object _sync = new object();
    private long _updateSequence = 0, _currentTick = 0;
    public int simulations, refreshes, adapters;
    private void EnsureRegistered() {}
    private static long NowTick() { return 100; }
    private bool AdvanceCurrentTickLocked(long tick) { if (_currentTick == tick) return false; _currentTick = tick; return true; }
    private void RunSystemsSnapshot(long tick, int step, string stream) { simulations++; }
    private bool RefreshLiveActors() { refreshes++; return true; }
    private void RunAdapters(bool changes, bool advanced) { adapters++; }
    $actorUpdate
}
public class CharacterPersistenceFixture {
    private object _sync = new object();
    private Dictionary<int, object> _liveFamily = new Dictionary<int, object>(), _liveVisitors = new Dictionary<int, object>();
    private HashSet<int> _temporarySyntheticIds = new HashSet<int>();
    private Dictionary<int, SyntheticCharacterProxy> _syntheticById = new Dictionary<int, SyntheticCharacterProxy>();
    private Dictionary<string, SyntheticCharacterProxy> _syntheticByKey = new Dictionary<string, SyntheticCharacterProxy>();
    private static float NowSeconds() { return 0; }
    private static void EnsureActorMirror(object actors, ICharacterProxy p) {}
    $characterPersistence
    public void ResetSession() { HandleSessionStarted(); }
    public static CharacterSaveData Save(SyntheticCharacterProxy p) { return ToSaveData(p); }
    public static CharacterSaveData RoundTrip(CharacterSaveData data) {
        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        using (var stream = new System.IO.MemoryStream()) {
            formatter.Serialize(stream, data); stream.Position = 0;
            return (CharacterSaveData)formatter.Deserialize(stream);
        }
    }
}
public class SlotSelectionPanel {
    public bool m_inputEnabled = true, m_loadingSave;
    public int m_chosenSlot = -1;
    public GameObject m_loadingGraphic = new GameObject();
    public void RefreshSaveSlotInfo() {}
}
public class Traverse {
    private object target;
    private string field;
    public static bool fail;
    public static Traverse Create(object o) { if (fail) throw new Exception("fixture reflection failure"); return new Traverse(o); }
    public Traverse(object target) { this.target = target; }
    public Traverse Field(string name) { field = name; return this; }
    public void SetValue(object value) { target.GetType().GetField(field).SetValue(target, value); }
    public T GetValue<T>() { return (T)target.GetType().GetField(field).GetValue(target); }
}
public class SlotPagingScope {
    public string StorageScenarioId = "Standard";
    public bool IsStandard = true;
    public SaveEntry FindByUIPosition(int a, int b, int c, bool d) { return null; }
    public int GetTransportSlotNumber(int index) { return index + 1; }
}
public class SaveEntry { public string id = "save"; public int absoluteSlot = 4; public object saveInfo; }
public static class SaveRuntimeState { public static void QueueLoad(SaveManager.SaveType t, string scenario, string id) {} }
public static class SaveProtectionPatches { public static class LoadGamePatch { public static bool _forceLoad; } }
public class SaveSnapshotBrowserSession { public int PageCount = 3; }
public static class SaveSnapshotBrowserState {
    public static bool IsActive(SlotSelectionPanel panel) { return false; }
    public static bool TryGet(SlotSelectionPanel p, out SaveSnapshotBrowserSession s) { s = null; return false; }
    public static void Exit(SlotSelectionPanel p) {}
    public static int sorts;
    public static void ToggleSort(SlotSelectionPanel p) { sorts++; }
}
public static class SlotPagingScopeResolver { public static SlotPagingScope Resolve(SlotSelectionPanel p) { return new SlotPagingScope(); } }
public static class SaveDeleteRouter { public static bool DeleteAbsoluteSlot(int slot, string why, out string error) { error = null; return true; } public static bool DeleteAbsoluteSlot(string scenario, int slot, string why, out string error) { error = null; return true; } }
public static class CustomSavesWelcomeDialog { public static void Show() {} }
public static class PagingManager { public static int page; public static int GetPage(SlotSelectionPanel p) { return page; } }
public static class PagingFixture {
    public static int page;
    private static bool _suppressWelcomeDialogOnce = false;
    private static int GetPage(SlotSelectionPanel p) { return page; }
    private static void SetPage(SlotSelectionPanel p, int v) { page = v; }
    private static void Update(SlotSelectionPanel p) {}
    $pageActions
    public static void Sort(SlotSelectionPanel p) { ToggleSnapshotSort(p); }
}
public static class SlotFixture {
    $slot
    public static int routed;
    public static int preservationErrors;
    private static bool HandleSnapshotDeleteMessageBox(SlotSelectionPanel p, int response) { return false; }
    private static bool ValidateDeleteIntent(SlotSelectionPanel p, int slot, SaveEntry entry, out string error) { error = null; return true; }
    private static void ShowDeleteConsistencyFailure(string error) {}
    private static void ShowDeletePreservationFailure(string error) { preservationErrors++; }
    private static void ClearDeleteIntent(SlotSelectionPanel p) {}
    private static bool HandleSnapshotSlotChosen(SlotSelectionPanel p) { routed++; return false; }
    private static bool HandleVanillaSlotChosen(SlotSelectionPanel p) { routed++; return false; }
    private static bool HandleCustomSlotChosen(SlotSelectionPanel p, int page) { routed++; return false; }
    private static void ApplyDifficultySettings(object info) {}
    public static bool Choose(SlotSelectionPanel p) { return OnSlotChosenPrefix(p); }
    public static bool Delete(SlotSelectionPanel p, int response) { return OnDeleteMessageBoxPrefix(p, response); }
    public static void Load(SlotSelectionPanel p) { QueueCustomLoad(new Traverse(p), 2, new SlotPagingScope(), SaveManager.SaveType.Slot1, new SaveEntry()); }
}
"@ + [regex]::Replace($guard, '(?m)^using [^;]+;\r?\n', '') +
    [regex]::Replace($saveDto.Replace('internal class ', 'public class '), '(?m)^using [^;]+;\r?\n', '')) -WarningAction SilentlyContinue
$script:passed = 1
function Check([bool]$condition, [string]$name) {
    if (!$condition) { throw "FAIL $name" }
    $script:passed++
}
Check ([RngFixture]::Resolves('CharacterMeshOptions+CharacterMeshType|GetRandomTexture', [CharacterMeshOptions+CharacterMeshType], 'GetRandomTexture')) 'nested texture target'
Check ([RngFixture]::Resolves('CharacterMeshOptions+CharacterPreset|Randomize_DontCallThisOutsideOfCharacterMeshOptions', [CharacterMeshOptions+CharacterPreset], 'Randomize_DontCallThisOutsideOfCharacterMeshOptions')) 'nested preset target'
Check ([RngFixture]::Domain([CharacterMeshOptions+CharacterMeshType]) -eq 'characters') 'texture domain'
Check ([RngFixture]::Domain([CharacterMeshOptions+CharacterPreset]) -eq 'characters') 'preset domain'
Check ([RngFixture]::Domain([CharacterMeshOptions]) -eq 'characters') 'outer domain'
Check ([RngFixture]::Domain($null) -eq 'misc') 'unknown domain'
$guardType = [RngFixture].Assembly.GetType('ShelteredAPI.Scenarios.Infrastructure.Unity.ScenarioLoadingTransitionGuard')
$guardType.GetMethod('OwnManagedLoadingTransition').Invoke($null, @('ShelterScene', 'test')) | Out-Null
$guardType.GetMethod('OwnDirectLaunchTransition').Invoke($null, @('ShelterScene', 'test')) | Out-Null
Check ($guardType.GetMethod('ShouldSuppressVanillaLoadingScreenHide').Invoke($null, @())) 'owned hide suppression'
[RecoveryFixture]::ResetAfterFailedTransition()
Check (!$guardType.GetMethod('ShouldSuppressVanillaLoadingScreenHide').Invoke($null, @())) 'recovery releases hide suppression'
$ownsFade = $guardType.GetMethod('OwnsDirectLaunchFadeForScene', [Reflection.BindingFlags]'Static,NonPublic')
Check (!$ownsFade.Invoke($null, @('ShelterScene'))) 'recovery releases direct fade'
Check (![LoadingScreen]::Instance.isShowing) 'recovery clears show count'
Check (![LoadingScreen]::Instance.ImageActive) 'recovery hides image'
Check ([ScreenFade]::instance.clears -eq 1) 'recovery clears black overlay after cancelling scene request'
Check ([UnityEngine.Time]::timeScale -eq 1 -and ![UnityEngine.AudioListener]::pause) 'recovery resumes time and audio'
$guardType.GetMethod('OwnManagedLoadingTransition').Invoke($null, @('ShelterScene_Stasis', 'retry')) | Out-Null
Check ($guardType.GetMethod('ShouldSuppressVanillaLoadingScreenHide').Invoke($null, @())) 'retry can acquire ownership'
[RecoveryFixture]::ResetAfterFailedTransition()
Check (!$guardType.GetMethod('ShouldSuppressVanillaLoadingScreenHide').Invoke($null, @())) 'repeat recovery releases ownership'
$member = New-Object FamilyMember
$member.BaseStats.Strength.Level = 8
$member.BaseStats.Dexterity.Level = 9
$member.BaseStats.Intelligence.Level = 10
$member.BaseStats.Charisma.Level = 11
$member.BaseStats.Perception.Level = 12
$familyData = [CharacterFixture]::Family($member)
Check ($familyData.StrengthLevel -eq 8 -and $familyData.DexterityLevel -eq 9 -and $familyData.IntelligenceLevel -eq 10 -and $familyData.CharismaLevel -eq 11 -and $familyData.PerceptionLevel -eq 12) 'family stat snapshot'
$visitor = New-Object NpcVisitor
$visitor.BaseStats.Charisma.Level = 13
$visitor.BaseStats.Perception.Level = 7
$visitorData = [CharacterFixture]::Visitor($visitor)
Check ($visitorData.CharismaLevel -eq 13 -and $visitorData.PerceptionLevel -eq 7) 'visitor stat snapshot'
$proxy = New-Object CharacterProxy
$proxy.Data = $familyData
$actor = New-Object EncounterCharacter
$characters = New-Object CharacterFixture
$characters.SwapEncounterCharacter($actor, $proxy, $null)
Check ($actor.Name -eq 'Family Name') 'swap uses actual name fields'
Check ($actor.Health -eq 82 -and $actor.maxHealth -eq 120) 'swap sets health and maximum'
Check ($actor.Stats.Strength.Level -eq 8 -and $actor.Stats.Dexterity.Level -eq 9 -and $actor.Stats.Intelligence.Level -eq 10) 'swap sets combat stats'
Check ($actor.Stats.Charisma.Level -eq 11 -and $actor.Stats.Perception.Level -eq 12) 'swap preserves charisma and perception order'
Check ($actor.HealthLost -eq 7) 'swap does not count replacement health as combat damage'
$proxy.Data.Health = 999
$proxy.Data.MaxHealth = 0
$proxy.Data.StrengthLevel = -1
$proxy.Data.PerceptionLevel = 999
$characters.SwapEncounterCharacter($actor, $proxy, $null)
Check ($actor.Health -eq 1 -and $actor.maxHealth -eq 1) 'swap clamps invalid health'
Check ($actor.Stats.Strength.Level -eq 0 -and $actor.Stats.Perception.Level -eq 20) 'swap clamps stat table bounds'
$proxy.Data = $null
$characters.SwapEncounterCharacter($actor, $proxy, $null)
Check ($actor.Name -eq 'Family Name') 'null data leaves actor intact'
$panel = New-Object SlotSelectionPanel
$panel.m_inputEnabled = $false
[SlotFixture]::Choose($panel) | Out-Null
Check ([SlotFixture]::routed -eq 0) 'disabled vanilla page does not route a load'
$panel.m_inputEnabled = $true
[SaveManager]::instance = New-Object SaveManager
[SaveManager]::instance.isDeleting = $true
[SlotFixture]::Choose($panel) | Out-Null
Check ([SlotFixture]::routed -eq 0) 'deleting vanilla page does not route a load'
[SaveManager]::instance.isDeleting = $false
[SlotFixture]::Choose($panel) | Out-Null
Check ([SlotFixture]::routed -eq 1) 'enabled vanilla page routes a load'
[SlotFixture]::Load($panel)
Check (!$panel.m_inputEnabled -and $panel.m_chosenSlot -eq 2 -and $panel.m_loadingSave) 'queued custom load preserves vanilla panel state'
[SlotFixture]::Choose($panel) | Out-Null
Check ([SlotFixture]::routed -eq 1 -and [SaveManager]::instance.loads -eq 1) 'second click cannot replace pending load'
[PagingManager]::page = 1
[Traverse]::fail = $true
Check (![SlotFixture]::Delete($panel, 1)) 'custom deletion failure cannot fall through to a vanilla slot delete'
Check ([SlotFixture]::preservationErrors -eq 1) 'custom deletion failure reports the preserved save'
[Traverse]::fail = $false
[SaveManager]::instance.isLoading = $false
[PagingFixture]::ChangePage($panel, 1)
[PagingFixture]::Sort($panel)
Check ([PagingFixture]::page -eq 0 -and [SaveSnapshotBrowserState]::sorts -eq 0) 'disabled slot input blocks page and archive sort clicks'
$panel.m_inputEnabled = $true
[SaveManager]::instance.isLoading = $true
[PagingFixture]::ChangePage($panel, 1)
Check ([PagingFixture]::page -eq 0) 'active load keeps its selected page'
[SaveManager]::instance.isLoading = $false
[SaveManager]::instance.isDeleting = $true
[PagingFixture]::ChangePage($panel, 1)
Check ([PagingFixture]::page -eq 0) 'active deletion keeps its selected page'
[SaveManager]::instance.isDeleting = $false
[PagingFixture]::ChangePage($panel, 1)
[PagingFixture]::Sort($panel)
Check ([PagingFixture]::page -eq 1 -and [SaveSnapshotBrowserState]::sorts -eq 1) 'enabled paging and archive sorting remain usable'
[SaveManager]::instance = New-Object SaveManager
function Register-Scene {
    [ActorRegistration]::Awake()
    [CharacterRegistration]::Awake()
    [ProxyRegistration]::RegisterForScene()
    [CollectionRegistration]::Awake()
}
Register-Scene
Check ([SaveManager]::instance.saveables.Count -eq 5) 'all persistent services register in first shelter'
[SaveManager]::instance.ClearAll()
Register-Scene
Check ([SaveManager]::instance.saveables.Count -eq 5) 'all persistent services register again after slot change'
[SaveManager]::instance.isLoading = $true
[SaveManager]::instance.toLoad.Clear()
[ShelteredActors]::InternalInstance.Tick()
[CharacterEffectSystem]::InternalInstance.EnsureRegistered()
[ProxyRegistration]::EnsureRegistered()
Check ([SaveManager]::instance.toLoad.Count -eq 0) 'per-frame registration does not requeue processed saveables'
Register-Scene
Check ([SaveManager]::instance.saveables.Count -eq 5) 'registration remains idempotent for a repeated scene hook'
$characters = New-Object CharacterPersistenceFixture
$dto = New-Object ShelteredAPI.Characters.Models.CharacterSaveData
$dto.UniqueId = 1000001
$dto.PersistenceKey = 'test.character'
$dto.IsPersistent = $true
$dto.FirstName = 'First'
$dto.Health = 0
$dto.MaxHealth = 120
$dto.CharismaLevel = 11
$dto.PerceptionLevel = 7
$restored = $characters.RestoreSyntheticCharacter($dto)
Check ($restored.Data.Health -eq 0) 'loading a zero-health synthetic character does not revive it'
$saved = [CharacterPersistenceFixture]::RoundTrip([CharacterPersistenceFixture]::Save($restored))
Check ($saved.CharismaLevel -eq 11 -and $saved.PerceptionLevel -eq 7) 'synthetic combat stats survive serialization'
$characters.ResetSession()
$dto.FirstName = 'Second'
$dto.Health = 75
$restored = $characters.RestoreSyntheticCharacter($dto)
Check ($restored.Data.FirstName -eq 'Second' -and $restored.Data.Health -eq 75) 'another slot replaces persistent characters with the same key'
$dtoType = [ShelteredAPI.Characters.Models.CharacterSaveData]
$optional = @($dtoType.GetFields([Reflection.BindingFlags]'Instance,NonPublic') | Where-Object { $_.IsDefined([Runtime.Serialization.OptionalFieldAttribute], $false) })
Check ($optional.Count -eq 5) 'added serialized stat fields are optional for older saves'
$member = New-Object FamilyMember
$live = New-Object LiveCharacterProxy -ArgumentList $member
$live.SetHealthOnTick(60)
$live.Tick(1)
Check ($member.health -eq 60 -and $member.damageCalls -eq 1) 'effect health reaches vanilla during the same tick'
$member.preventDeath = $true
$live.SetHealthOnTick(0)
$live.Tick(1)
Check ($member.health -eq 1 -and $live.Data.Health -eq 1) 'native fatal-damage guard remains authoritative'
$live.Data.Health = 50
$live.Sync()
Check ($member.health -eq 50 -and $member.healCalls -eq 1) 'live healing uses vanilla Heal'
$member.health = 0
$live.Data.Health = 100
$live.Sync()
Check ($member.health -eq 0 -and $live.Data.Health -eq 0) 'live sync cannot revive a dead vanilla character'
$update = New-Object EffectUpdateFixture
$synthetic = New-Object SyntheticCharacterProxy -ArgumentList 9
$synthetic.Effects = New-Object BasicCharacterEffects -ArgumentList $synthetic
$synthetic.Attributes = New-Object BasicCharacterAttributes
$update.Add($synthetic)
[LoadingScreen]::Instance = $null
[SaveManager]::instance = New-Object SaveManager
[UnityEngine.Time]::timeScale = 1
$update.Update()
Check ($synthetic.Effects.ticks -eq 1 -and $synthetic.Attributes.ticks -eq 1) 'off-screen synthetic effects and attributes advance'
[SaveManager]::instance.isLoading = $true
$update.Update()
Check ($update.refreshes -eq 1) 'character effects wait for scene data restoration'
[SaveManager]::instance.isLoading = $false
[SaveManager]::instance.isSaving = $true
$update.Update()
Check ($update.refreshes -eq 1) 'character effects cannot mutate an active save'
[SaveManager]::instance.isSaving = $false
[UnityEngine.Time]::timeScale = 0
$update.Update()
Check ($update.refreshes -eq 1) 'paused panels stop character effect callbacks'
[UnityEngine.Time]::timeScale = 1
[CutsceneManager]::Instance = New-Object CutsceneManager
[CutsceneManager]::Instance.CutSceneActive = $true
$update.Update()
Check ($update.refreshes -eq 1) 'cutscenes retain native character update guards'
[CutsceneManager]::Instance = $null
$synthetic.IsActive = $false
$update.Update()
Check ($synthetic.Effects.ticks -eq 1) 'unregistered synthetic characters stop ticking'
$actorUpdateTest = New-Object ActorUpdateFixture
[SaveManager]::instance.isLoading = $true
$actorUpdateTest.Update()
Check ($actorUpdateTest.simulations -eq 0 -and $actorUpdateTest.refreshes -eq 0 -and $actorUpdateTest.adapters -eq 0) 'actor systems and adapters wait for vanilla reconstruction'
[SaveManager]::instance.isLoading = $false
[SaveManager]::instance.isSaving = $true
$actorUpdateTest.Update()
Check ($actorUpdateTest.adapters -eq 0) 'actor adapters cannot mutate an active save'
[SaveManager]::instance.isSaving = $false
[UnityEngine.Time]::timeScale = 0
$actorUpdateTest.Update()
Check ($actorUpdateTest.adapters -eq 0) 'actor adapters honor paused panels'
[UnityEngine.Time]::timeScale = 1
[LoadingScreen]::Instance = New-Object LoadingScreen
$actorUpdateTest.Update()
Check ($actorUpdateTest.refreshes -eq 0) 'actor snapshots wait for the loading screen to finish'
[LoadingScreen]::Instance = $null
[CutsceneManager]::Instance = New-Object CutsceneManager
[CutsceneManager]::Instance.CutSceneActive = $true
$actorUpdateTest.Update()
Check ($actorUpdateTest.adapters -eq 0) 'actor adapters wait for cutscenes'
[CutsceneManager]::Instance = $null
$actorUpdateTest.Update()
Check ($actorUpdateTest.simulations -eq 1 -and $actorUpdateTest.refreshes -eq 1 -and $actorUpdateTest.adapters -eq 1) 'actor updates resume once after restoration'
$firstJournal = New-Object JournalManager
$nextJournal = New-Object JournalManager
Check ([JournalFixture]::TryReserveWrite($firstJournal, 'intro|1:6:0|text')) 'first journal entry reserves its minute key'
Check (![JournalFixture]::TryReserveWrite($firstJournal, 'intro|1:6:0|text')) 'same-session journal retries remain deduplicated'
Check ([JournalFixture]::TryReserveWrite($nextJournal, 'intro|1:6:0|text')) 'new shelter can write the same scheduled journal entry'
$randomVisitor = [VisitorAttributeFixture]::CreateAttributes($null, $null)
Check ($null -ne $randomVisitor -and $randomVisitor.m_meshId -eq 'man') 'unauthored visitors have randomized records for native group count and saving'
$otherRandomVisitor = [VisitorAttributeFixture]::CreateAttributes($null, $null)
Check (![object]::ReferenceEquals($randomVisitor, $otherRandomVisitor)) 'each scheduled visitor gets its own mutable record'
$visitorDefinition = New-Object ScenarioDefinition
$authoredVisitor = New-Object ScenarioNpcDefinition
$authoredVisitor.CharacterId = 'trader'
$authoredVisitor.DisplayName = 'Pat'
$authoredVisitor.PresetId = 'woman'
$authoredVisitor.WeaponItemId = 'knife'
$authoredVisitor.Stats = New-Object ScenarioNpcStats
$authoredVisitor.Stats.Strength = 8
$visitorDefinition.ScenarioCharacters.Add($authoredVisitor)
$visitorRecord = [VisitorAttributeFixture]::CreateAttributes($visitorDefinition, 'trader')
Check ($visitorRecord.m_firstName -eq 'Pat' -and $visitorRecord.m_meshId -eq 'woman' -and $visitorRecord.m_strengthLevel -eq 8 -and $visitorRecord.m_weapon -eq [ItemManager+ItemType]::Knife) 'authored visitor overrides survive fallback randomization'
Write-Output "PASS game audit continuation contracts: $script:passed/$script:passed"

