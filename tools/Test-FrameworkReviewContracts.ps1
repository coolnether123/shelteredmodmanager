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
Write-Output "Framework review contracts: $script:checks passed, 0 failed."
