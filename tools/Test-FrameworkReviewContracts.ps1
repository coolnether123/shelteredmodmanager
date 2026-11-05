[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$script:checks = 0
function Read-Source($path) { Get-Content (Join-Path $root $path) -Raw }
function Assert-Contract($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Read-Method($path, $signature) {
    $text = Read-Source $path
    $start = $text.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing $signature" }
    $open = $text.IndexOf('{', $start)
    $depth = 1; $end = $open + 1
    while ($depth -gt 0) {
        if ($text[$end] -eq '{') { $depth++ }
        if ($text[$end] -eq '}') { $depth-- }
        $end++
    }
    $text.Substring($start, $end - $start)
}
$shared = Read-Source 'ShelteredAPI/Events/SharedGamePatches.cs'
Assert-Contract (([regex]::Matches($shared, 'HarmonyPatch\(typeof\(BaseCharacter\), "Damage"\)')).Count -eq 1) 'Exactly one committed-health death hook'
$death = Read-Method 'ShelteredAPI/Events/GameEvents.cs' 'internal static void RaiseDeath('
Assert-Contract ($death.Contains('victim.health > 0') -and $death.Contains('RemoveEncounterDeath(victim)')) 'Living and settled encounter victims must not emit duplicate death events'
$runtime = Read-Method 'ShelteredAPI/Characters/Internal/CharacterRuntime.cs' 'private void SyncHealth('
Assert-Contract ($runtime.Contains('character.Damage(') -and $runtime.Contains('character.Heal(')) 'Audit synchronization must pass health changes through vanilla timing'
$plugin = Read-Source 'ModAPI/Core/PluginManager.cs'
Assert-Contract ($plugin.IndexOf('plugin.Initialize(ctx)') -lt $plugin.IndexOf('_plugins.Add(plugin)')) 'Failed initialization must not register callbacks'
$overloads = Read-Method 'ModAPI/Harmony/HarmonyHelper.cs' 'public static void PatchAllOverloads('
Assert-Contract ($overloads.Contains('BindingFlags.DeclaredOnly')) 'Overload patching must stay on the declaring type'
$rng = Read-Source 'ModAPI/Core/ModRandomState.cs'
Assert-Contract ($rng -match 'No seed.json[^\n]*\n\s*ModRandom.IsDeterministic = false;') 'Missing seed must reset deterministic mode'
$harmony = Read-Source 'ModAPI/Harmony/HarmonyUtil.cs'
Assert-Contract ($harmony.Contains('foreach (var mb in targetType.GetMethods(')) 'Safety discovery must enumerate overloaded targets'
Assert-Contract ($harmony.Contains('MMLog.WriteWarning("LoadGateRunner: condition timed out')) 'Timed-out work must warn'
$setters = (Read-Method 'ModAPI/Reflection/Safe.cs' 'public static bool SetField(') + "`n" + (Read-Method 'ModAPI/Reflection/Safe.cs' 'public static bool SetProperty(')
Add-Type -TypeDefinition @"
using System;
using System.Reflection;
public static class MMLog { public static void WriteDebug(string s) {} public static void LogOnce(string s, Action a) {} }
public static class SetterFixture {
$setters
static void ResolveTarget(object o, out Type t, out object instance, out BindingFlags flags) { t=o.GetType(); instance=o; flags=BindingFlags.Instance|BindingFlags.Public; }
static bool TryCastManual(object o, Type t, out object value) { value=null; return false; }
static string BuildOnceKey(Type t, string a, string b) { return b; }
static void LogDetailedFailure(Type t, string a, string[] b, BindingFlags f) {}
}
public class Values { public int Number=17; public int Property {get;set;} public Values() { Property=23; } }
"@
$v = New-Object Values
Assert-Contract (!( [SetterFixture]::SetField($v,'Number','invalid')) -and $v.Number -eq 17) 'Failed field conversion must leave the original value'
Assert-Contract (!( [SetterFixture]::SetProperty($v,'Property','invalid')) -and $v.Property -eq 23) 'Failed property conversion must leave the original value'
Assert-Contract ([SetterFixture]::SetField($v,'Number','42') -and $v.Number -eq 42) 'Successful conversion must still work'

$registration = Read-Source 'ModAPI/Core/SettingsProviderRegistration.cs'
Assert-Contract ($registration.Contains('if (!(provider is ISettingsProvider2))') -and $registration.Contains('adapter.Load();')) 'Only plain providers must be adapted and loaded'
Assert-Contract ($registration.Contains('provider.GetSettingsObject(), provider, provider.GetSettings()')) 'Plain providers must retain their declared definitions and holder'
Assert-Contract ((Read-Source 'ModAPI/Spine/SettingsController.cs').Contains('definitions != null ? definitions.ToList() : Scan(owner)')) 'Provider definitions must feed the established persistence format'
$proxy = Read-Method 'ShelteredAPI/Saves/Runtime/PlatformSaveProxy.cs' 'public override void DoesSaveExist('
Assert-Contract ($proxy.Contains('TryGetPendingLoad') -and $proxy.Contains('HasActiveCustomSessionFor') -and $proxy.Contains('DirectoryProvider.EntryPath')) 'Existence must follow custom session storage'
$load = Read-Method 'ShelteredAPI/Saves/Runtime/PlatformLoadOperationService.cs' 'internal bool Load('
Assert-Contract ($load.IndexOf('HasActiveCustomSessionFor') -lt $load.IndexOf('ClearActiveCustomSession')) 'Active custom reload must route before vanilla fallback'
$idle = Read-Method 'ShelteredAPI/Events/GameTimeTriggerHelper.cs' 'internal static void TickFromGameTime('
Assert-Contract ($idle -match '(?s)if \(!HasTimedConsumers\(\)\).*?_forceResync = true;.*?return;') 'Idle time must force a fresh scheduler baseline'
$vanilla = Read-Method 'ShelteredAPI/Saves/SaveRegistryCore.cs' 'public static SaveInfo ReadVanillaSaveInfo('
Assert-Contract ($vanilla -match '(?s)catch \(Exception ex\).*MMLog.WriteError.*return new SaveInfo\(\)') 'Unreadable vanilla files must report and remain occupied'
$injector = Read-Source 'ShelteredAPI/Content/ContentInjector.cs'
Assert-Contract ($injector.Contains('item.CraftStackSize') -and !$injector.Contains('GetField("ResultCount"')) 'Legacy output must use the real item quantity'
Assert-Contract (!$injector.Contains('GetField("CraftTime"') -and $injector.Contains('Recipe craft time is unsupported')) 'Unsupported per-recipe craft time must report its item-owned replacement'
Assert-Contract (!(Read-Source 'ShelteredAPI/Content/Packs/ContentPackMapper.cs').Contains('CraftTimeSeconds = source.craftTimeSeconds')) 'Packs must not advertise a recipe-owned time'
$coordinate = Read-Method 'ShelteredAPI/Map/Internal/ExpeditionMapCoordinateConverter.cs' 'internal static bool TryGridToWorldCenter('
Assert-Contract ($coordinate.Contains('GetGridRefCentreWorldPos')) 'All map cells must use the game center conversion, including edge cells'
$distance = Read-Method 'ShelteredAPI/Map/Internal/HomeShelterExpeditionPatches.cs' 'private static float SurvivalDistanceFromHome('
$defaultsPath = 'ModAPI/Spine/SpineSettingsHelper.cs'
$defaults = (Read-Method $defaultsPath 'internal static object CreateDefaults(') + "`n" + (Read-Method $defaultsPath 'internal static object ReadDefault(')
$atomic = (Read-Source 'ModAPI/Util/AtomicTextFile.cs') -replace '(?m)^using [^;]+;', ''
Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Reflection;
public struct Vector2 {
 public float x,y; public Vector2(float x,float y) {this.x=x;this.y=y;}
 public float magnitude {get {return (float)Math.Sqrt(x*x+y*y);}}
 public static Vector2 operator -(Vector2 a,Vector2 b) {return new Vector2(a.x-b.x,a.y-b.y);}
}
public static class HomeShelterExpeditionRuntime {
 public static bool Active; public static Vector2 Home;
 public static bool TryGetHomeWorld(out Vector2 home) {home=Home;return Active;}
}
public static class ReviewFixture {
 private static class MMLog { public static void WarnOnce(string key,string text) {} }
$distance
$defaults
public static void Write(string path,string text) { ModAPI.Util.AtomicTextFile.Write(path,text); }
public static float Distance(float x,float y) {var v=new Vector2(x,y);return SurvivalDistanceFromHome(ref v);}
public static object Default(object owner,string member) {return ReadDefault(owner.GetType().GetField(member), o=>owner.GetType().GetField(member).GetValue(o),CreateDefaults(owner),owner);}
}
public class Holder { public int Amount=7; }
$atomic
"@ -ReferencedAssemblies System.dll
$holder = New-Object Holder
$holder.Amount = 31
Assert-Contract ([ReviewFixture]::Default($holder,'Amount') -eq 7 -and $holder.Amount -eq 31) 'Defaults must come from a fresh holder without changing loaded values'
[HomeShelterExpeditionRuntime]::Active = $false
Assert-Contract ([ReviewFixture]::Distance(3,4) -eq 5) 'No registered home must preserve vanilla distance'
[HomeShelterExpeditionRuntime]::Active = $true
[HomeShelterExpeditionRuntime]::Home = New-Object Vector2 100,100
Assert-Contract ([ReviewFixture]::Distance(103,104) -eq 5) 'Survival distance must measure from relocated home'
$atomicType = [ReviewFixture].Assembly.GetType('ModAPI.Util.AtomicTextFile')
$write = $atomicType.GetMethod('Write', [Reflection.BindingFlags]'Static,NonPublic')
$tempRoot = Join-Path $root ('.framework-review-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempRoot) | Out-Null
try {
 $path = Join-Path $tempRoot 'state.json'
 [ReviewFixture]::Write($path,'old')
 [ReviewFixture]::Write($path,'new')
 Assert-Contract ([IO.File]::ReadAllText($path) -eq 'new') 'Atomic writer must create and replace'
 Assert-Contract (@([IO.Directory]::GetFiles($tempRoot,'*.tmp')).Count -eq 0) 'Atomic writer must clean temporary files'
} finally { Remove-Item -LiteralPath $tempRoot -Recurse -Force }

$targets = Read-Method 'ModAPI/Harmony/HarmonyUtil.cs' 'public static IEnumerable<MethodBase> GetPatchTargets('
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Reflection;
[AttributeUsage(AttributeTargets.Class, AllowMultiple=true)]
public class HarmonyPatch : Attribute {
 public Info info = new Info();
 public class Info { public Type declaringType; public string methodName; }
 public HarmonyPatch(Type type) { info.declaringType=type; }
 public HarmonyPatch(string name) { info.methodName=name; }
}
public static class TargetFixture {
 private static class MMLog { public static void WarnOnce(string key,string message) {} }
$targets
}
public class OverloadedTarget { public void Run() {} public int Run(int value) {return value;} }
[HarmonyPatch(typeof(OverloadedTarget))]
[HarmonyPatch("Run")]
public class SplitPatch {}
"@
Assert-Contract (@([TargetFixture]::GetPatchTargets([SplitPatch])).Count -eq 2) 'Split Harmony metadata must discover both overloads for safety checks'


$shape = Read-Method 'ShelteredAPI/Map/Internal/HomeShelterExpeditionPatches.cs' 'private static IEnumerable<CodeInstruction> OpenGroundEncounterCheckTranspiler('
Assert-Contract ($shape.IndexOf('matches.Count != 1') -lt $shape.IndexOf('code[matches[0]].operand = distance')) 'Encounter distance rewrite must reject ambiguous shapes before mutation'
Write-Output "Framework review contracts: $script:checks passed, 0 failed."
$deathMethods = @(
 (Read-Method 'ShelteredAPI/Events/GameEvents.cs' 'internal static void RaiseDeath('),
 (Read-Method 'ShelteredAPI/Events/GameEvents.cs' 'private static bool RemoveEncounterDeath('),
 (Read-Method 'ShelteredAPI/Events/GameEvents.cs' 'internal static void ClearEncounterDeath('),
 (Read-Method 'ShelteredAPI/Events/GameEvents.cs' 'internal static void RaiseFatalDamage('),
 (Read-Method 'ShelteredAPI/Events/GameEvents.cs' 'internal static void RaiseFatalDamageStarting(')
) -join "`n"
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
namespace DeathComposition {
 public class BaseCharacter { public enum DamageType { Undefined } public int health=100; }
 public class FamilyMember : BaseCharacter { public bool isAway; }
 public class BreachMan { public static BreachMan instance; public bool inProgress; }
 public class CharacterDeathEventArgs { public BaseCharacter Victim; public BaseCharacter.DamageType Cause; public string Details,Context; }
 public static class GameEvents {
 static readonly List<WeakReference> EncounterDeaths=new List<WeakReference>();
 static event Action<CharacterDeathEventArgs> OnCharacterDied;
 static event Action<FamilyMember,bool> OnCharacterFatalDamage;
 static event Action<FamilyMember> OnCharacterFatalDamageStarting;
$deathMethods
 public static string Exercise(bool allowDeath,bool deferDeath,bool wasteland,bool subscribe) {
  var person=new FamilyMember(); var trace=new List<string>();
  OnCharacterDied=null; OnCharacterFatalDamage=null; OnCharacterFatalDamageStarting=null;
  if (subscribe) {
   OnCharacterFatalDamageStarting += p=>trace.Add("starting:"+p.health);
   OnCharacterFatalDamage += (p,allowed)=>trace.Add("fatal:"+p.health+":"+allowed);
   OnCharacterDied += e=>trace.Add("dead:"+e.Victim.health);
  }
  // Model the game's fatal gate and delayed-death branches around the production event methods.
  bool wasAlive=person.health>0;
  if (!wasteland) { RaiseFatalDamageStarting(person); RaiseFatalDamage(person,allowDeath); }
  if (!wasteland && !allowDeath) person.health=1;
  else if (!deferDeath) person.health=0;
  RaiseDeath(person,wasAlive,BaseCharacter.DamageType.Undefined,"",wasteland);
  RaiseDeath(person,person.health>0,BaseCharacter.DamageType.Undefined,"",wasteland);
  trace.Add("health:"+person.health);
  return string.Join(",",trace.ToArray());
 }
 }
}
namespace ModAPI.Core {
 public static class SubscriberDispatch {
  public static void Invoke<T>(Action<T> action,string name,T value) {if(action!=null)action(value);}
  public static void Invoke<T,U>(Action<T,U> action,string name,T value,U value2) {if(action!=null)action(value,value2);}
 }
}
"@
Assert-Contract ([DeathComposition.GameEvents]::Exercise($true,$false,$false,$true) -eq 'starting:100,fatal:100:True,dead:0,health:0') 'Fatal notifications precede committed death and death fires once'
Assert-Contract ([DeathComposition.GameEvents]::Exercise($false,$false,$false,$true) -eq 'starting:100,fatal:100:False,health:1') 'Unconsciousness must not emit death'
Assert-Contract ([DeathComposition.GameEvents]::Exercise($true,$true,$false,$true) -eq 'starting:100,fatal:100:True,health:100') 'Pending death must wait for committed health'
Assert-Contract ([DeathComposition.GameEvents]::Exercise($true,$false,$true,$true) -eq 'dead:0,health:0') 'Wasteland damage skips the shelter fatal gate'
Assert-Contract ([DeathComposition.GameEvents]::Exercise($true,$false,$false,$false) -eq 'health:0') 'No subscribers must preserve fatal health'
Assert-Contract ([DeathComposition.GameEvents]::Exercise($false,$false,$false,$false) -eq 'health:1') 'No subscribers must preserve unconsciousness health'
Write-Output "Framework review and death composition contracts: $script:checks passed, 0 failed."
