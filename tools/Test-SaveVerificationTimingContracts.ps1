[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-Method([string]$path, [string]$signature) {
    $source = Get-Content (Join-Path $root $path) -Raw
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production method: $signature" }
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
$read = Read-Method 'ShelteredAPI/Core/SaveProtection.cs' 'private static SaveData ReadPendingLoadData('
$path = 'ShelteredAPI/Saves/Runtime/PlatformLoadOperationService.cs'
$load = Read-Method $path 'internal bool Load('
$get = Read-Method $path 'internal bool GetLoadedData('
# Use the production payload reader and load buffer methods against disposable saves.
Add-Type -TypeDefinition @"
using System;
using System.Reflection;
using System.Text;
public class SaveData {
    public string Text;
    public bool isFinished;
    public SaveData(byte[] bytes) { Text = Encoding.UTF8.GetString(bytes); isFinished = bytes.Length == 0; }
}
public class SaveManager {
    public enum SaveType { Slot1, Slot2 }
    private PlatformSave_Base m_saveScript;
    private SaveData m_data = new SaveData(Encoding.UTF8.GetBytes("previous save"));
    public SaveManager(PlatformSave_Base script) { m_saveScript = script; }
}
public static class AccessTools {
    public static FieldInfo Field(Type type, string name) { return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); }
}
public abstract class PlatformSave_Base {
    public abstract bool IsLoading();
    public abstract bool PlatformLoad(SaveManager.SaveType type);
    public abstract bool PlatformGetLoadedData(out byte[] bytes);
}
public class NativeSave : PlatformSave_Base {
    public bool busy;
    public byte[] payload = Encoding.UTF8.GetBytes("requested save");
    public int reads;
    public override bool IsLoading() { return busy; }
    public override bool PlatformLoad(SaveManager.SaveType type) { payload = Encoding.UTF8.GetBytes(type.ToString()); return true; }
    public override bool PlatformGetLoadedData(out byte[] bytes) { reads++; bytes = payload; return bytes != null; }
}
public static class SaveRuntimeState {
    public class Target { public string Text; }
    public static Target pending;
    public static bool TryGetPendingLoad(SaveManager.SaveType type, out Target target) { target = pending; pending = null; return target != null; }
    public static void ClearActiveCustomSession() {}
}
public static class ModRuntime {
    public static bool IsQuitting;
    public static void MarkSaveExit(string key, string value) {}
}
public static class MMLog { public static void WriteDebug(string text) {} }
public class LoadService : PlatformSave_Base {
    readonly PlatformSave_Base _inner;
    string _customLoadedXml;
    public LoadService(PlatformSave_Base inner) { _inner = inner; }
    bool LoadCustomTarget(SaveManager.SaveType type, SaveRuntimeState.Target target) { _customLoadedXml = target.Text; return true; }
    public override bool IsLoading() { return _inner.IsLoading(); }
    public override bool PlatformLoad(SaveManager.SaveType type) { return Load(type); }
    public override bool PlatformGetLoadedData(out byte[] data) { return GetLoadedData(out data); }
    $load
    $get
}
public static class SaveVerificationTimingContracts {
    $read
    static int passed;
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); passed++; }
    public static int Run() {
        var native = new NativeSave { busy = true };
        var manager = new SaveManager(native);
        Require(ReadPendingLoadData(manager) == null && native.reads == 0, "busy platform payload is not inspected");
        native.busy = false;
        Require(ReadPendingLoadData(manager).Text == "requested save", "validation reads requested bytes rather than previous m_data");
        native.payload = null;
        Require(ReadPendingLoadData(manager) == null, "missing payload uses native failure handling");
        native.payload = new byte[0];
        Require(ReadPendingLoadData(manager) == null, "invalid payload uses native failure handling");
        var proxy = new LoadService(native);
        manager = new SaveManager(proxy);
        SaveRuntimeState.pending = new SaveRuntimeState.Target { Text = "custom A" };
        proxy.PlatformLoad(SaveManager.SaveType.Slot1);
        byte[] bytes;
        Require(ReadPendingLoadData(manager).Text == "custom A" && proxy.PlatformGetLoadedData(out bytes) && Encoding.UTF8.GetString(bytes) == "custom A", "verification leaves custom payload available to vanilla");
        SaveRuntimeState.pending = new SaveRuntimeState.Target { Text = "custom B" };
        proxy.PlatformLoad(SaveManager.SaveType.Slot1);
        Require(ReadPendingLoadData(manager).Text == "custom B", "next custom request replaces previous payload");
        proxy.PlatformLoad(SaveManager.SaveType.Slot2);
        Require(ReadPendingLoadData(manager).Text == "Slot2", "vanilla request clears previous custom payload");
        return passed;
    }
}
"@
$count = [SaveVerificationTimingContracts]::Run()
Write-Host "Save verification timing contracts: $count passed, 0 failed."
