[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '..\ShelteredAPI\Events\GameEvents.cs') -Raw
# Compile the production hook methods against disposable lifecycle fixtures.
function Get-Method([string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing method: $signature" }
    $open = $source.IndexOf('{', $start)
    $depth = 1
    $end = $open + 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $source.Substring($start, $end - $start)
}
$methods = @(
    (Get-Method 'internal static void HookDayEvents()'),
    (Get-Method 'internal static void HookPartyEvents()'),
    (Get-Method 'internal static void CaptureLoadStartState(SaveManager mgr)'),
    (Get-Method 'private static bool ConsumeLoadNewGameSuppression()'),
    (Get-Method 'private static void Postfix(SaveManager __instance, SaveManager.SaveType type, bool __result)'),
    (Get-Method 'private static void Postfix(SaveManager.SaveType type, bool __result)')
) -join "`n"
Add-Type -TypeDefinition @"
using System;
public class SaveData {}
public class SaveManager {
    public enum SaveType { Invalid, GlobalData, Slot1 }
}
public static class GameTime {
    public static event Action newDay;
    public static void Awake() { newDay = null; }
    public static void AdvanceDay() { if (newDay != null) newDay(); }
}
public class ExplorationManager {
    public static ExplorationManager Instance;
    public event Action<int> onPartyReturned;
    public void ReturnParty() { if (onPartyReturned != null) onPartyReturned(1); }
}
public static class GameEvents {
    static ExplorationManager _partyEventManager;
    static SaveData _loadStartData;
    static bool _suppressNextNewGameForLoad;
    static int days, parties, resets;
    static void HandleNewDay() { days++; }
    static void OnPartyReturnedInternal(int id) { parties++; }
    static void WarnOnce(string key, string message) { throw new Exception(message); }
    static void ResetSaveFlags() { resets++; }
    static bool TryGetField<T>(object value, string name, out T result) { result = default(T); return false; }
    $methods
    static void Require(bool ok, string name) { if (!ok) throw new Exception(name); }
    public static int Run() {
        GameTime.Awake(); HookDayEvents(); HookDayEvents(); GameTime.AdvanceDay();
        Require(days == 1, "first session and duplicate day subscription");
        GameTime.Awake(); HookDayEvents(); GameTime.AdvanceDay();
        Require(days == 2, "day subscription after session reset");
        var old = ExplorationManager.Instance = new ExplorationManager();
        HookPartyEvents(); HookPartyEvents(); old.ReturnParty();
        Require(parties == 1, "duplicate party subscription");
        ExplorationManager.Instance = new ExplorationManager(); HookPartyEvents();
        old.ReturnParty(); ExplorationManager.Instance.ReturnParty();
        Require(parties == 2, "replacement manager subscription and old manager detach");
        _suppressNextNewGameForLoad = true;
        Postfix(new SaveManager(), SaveManager.SaveType.GlobalData, true);
        Require(resets == 0 && _suppressNextNewGameForLoad, "global load leaves session bookkeeping intact");
        Postfix(new SaveManager(), SaveManager.SaveType.Slot1, false);
        Require(resets == 0, "rejected load leaves session bookkeeping intact");
        Postfix(new SaveManager(), SaveManager.SaveType.Slot1, true);
        Require(resets == 1 && !ConsumeLoadNewGameSuppression(), "accepted request alone does not suppress new game");
        Postfix(SaveManager.SaveType.Slot1, false);
        Require(resets == 1 && SaveRuntimeState.saves == 0, "rejected save preserves operation bookkeeping");
        Postfix(SaveManager.SaveType.GlobalData, true);
        Require(resets == 1 && SaveRuntimeState.saves == 0, "preference save preserves slot operation bookkeeping");
        Postfix(SaveManager.SaveType.Slot1, true);
        Require(resets == 2 && SaveRuntimeState.saves == 1, "accepted slot save starts operation bookkeeping");
        return 10;
    }
}
public static class SaveRuntimeState {
    public static int saves;
    public static void TryBeginSaveOperation(SaveManager.SaveType type) { saves++; }
}
"@
$count = [GameEvents]::Run()
Write-Host "Game event session contracts: $count passed, 0 failed."
