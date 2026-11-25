[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$LegacySmm,
    [Parameter(Mandatory)][string]$ManagedReferences,
    [Parameter(Mandatory)][string]$ManagerExe,
    [Parameter(Mandatory)][string]$CurrentApiDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$IlDasm = 'C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\ildasm.exe'
)

$ErrorActionPreference = 'Stop'
$LegacySmm = (Resolve-Path -LiteralPath $LegacySmm).Path
$ManagedReferences = (Resolve-Path -LiteralPath $ManagedReferences).Path
$CurrentApiDirectory = (Resolve-Path -LiteralPath $CurrentApiDirectory).Path
$ManagerExe = (Resolve-Path -LiteralPath $ManagerExe).Path
$harmony = Join-Path (Split-Path $PSScriptRoot -Parent) 'mods\0Harmony\Assemblies\0Harmony.dll'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework'
$compiler = Join-Path $framework 'v3.5\csc.exe'
$ilAsm = Join-Path $framework 'v4.0.30319\ilasm.exe'
if (Test-Path -LiteralPath $OutputDirectory) { throw "Use a new fixture output directory: $OutputDirectory" }
$work = (New-Item -ItemType Directory -Path $OutputDirectory).FullName

function Invoke-Checked([string]$Command, [string[]]$Arguments, [string]$LogName) {
    $log = Join-Path $work $LogName
    & $Command @Arguments *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 25
        throw "$LogName failed (exit $LASTEXITCODE)."
    }
}

function Set-IlBody([string]$Signature, [string]$EndMarker, [string]$Body) {
    $pattern = '(?ms)^  \.method [^{]*?' + $Signature +
        '[^{]*\{\r?\n(?:(?!^  \} // end of method ).)*^  \} // end of method ' +
        [regex]::Escape($EndMarker) + '\r?$'
    $matches = [regex]::Matches($script:unityIl, $pattern)
    if ($matches.Count -ne 1) { throw "Expected one Unity method: $EndMarker ($($matches.Count) found)" }
    $method = $matches[0]
    $header = $method.Value.Substring(0, $method.Value.IndexOf('{')).Replace(' internalcall', '')
    $replacement = @(($header + '{'), '    .maxstack 2', $Body, "  } // end of method $EndMarker") -join [Environment]::NewLine
    $script:unityIl = $script:unityIl.Remove($method.Index, $method.Length).Insert($method.Index, $replacement)
}

$previousDirectory = [Environment]::CurrentDirectory
Push-Location $work
try {
    Copy-Item -LiteralPath $ManagerExe -Destination 'Manager.exe'
    Copy-Item -LiteralPath (Join-Path $CurrentApiDirectory 'ModAPI.dll'),(Join-Path $CurrentApiDirectory 'ShelteredAPI.dll'),$harmony -Destination .
    Copy-Item -LiteralPath (Join-Path $ManagedReferences 'Assembly-CSharp.dll'),(Join-Path $ManagedReferences 'UnityEngine.UI.dll') -Destination .
    [Environment]::CurrentDirectory = $work

    # Replace the engine boundary in a disposable Unity assembly. The SMM assembly
    # preparation, plugin lifecycle and save comparison run from the Release binaries.
    Invoke-Checked $IlDasm @('/TEXT','/UTF8','/NOBAR','/OUT=UnityEngine.il',(Join-Path $ManagedReferences 'UnityEngine.dll')) 'ildasm.log'
    $script:unityIl = Get-Content -LiteralPath 'UnityEngine.il' -Raw
    Set-IlBody 'Internal_CreateGameObject\(' 'GameObject::Internal_CreateGameObject' '    ret'
    Set-IlBody 'instance class UnityEngine.Transform\s+get_transform\(' 'GameObject::get_transform' @'
    ldtoken UnityEngine.Transform
    call class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
    call object [mscorlib]System.Runtime.Serialization.FormatterServices::GetUninitializedObject(class [mscorlib]System.Type)
    castclass UnityEngine.Transform
    ret
'@
    Set-IlBody 'SetParent\(class UnityEngine.Transform parent,\s+bool ' 'Transform::SetParent' '    ret'
    Set-IlBody 'string\s+get_dataPath\(' 'Application::get_dataPath' @'
    call string [mscorlib]System.IO.Directory::GetCurrentDirectory()
    ldstr "Data"
    call string [mscorlib]System.IO.Path::Combine(string, string)
    ret
'@
    # ILDasm prints these special float values as words. ILAsm accepts their bits.
    $script:unityIl = $script:unityIl -replace '(ldc\.r4\s+)inf\b', '$1 float32(0x7f800000)'
    $script:unityIl = $script:unityIl -replace '(ldc\.r4\s+)-inf\b', '$1 float32(0xff800000)'
    $script:unityIl = $script:unityIl -replace 'float32\(inf\)', 'float32(0x7f800000)'
    $script:unityIl = $script:unityIl -replace 'float32\(-inf\)', 'float32(0xff800000)'
    Set-Content -LiteralPath 'UnityEngine.il' -Value $script:unityIl -Encoding utf8BOM
    Invoke-Checked $ilAsm @('/DLL','/QUIET','/OUTPUT=UnityEngine.dll','UnityEngine.il') 'ilasm.log'

    Set-Content -LiteralPath 'LegacyPlugin.cs' -Value 'public class LegacyPlugin : ModAPI.Content.ItemDefinition {}'
    Set-Content -LiteralPath 'LegacySheltered.cs' -Value 'public class LegacySheltered { public System.Type Cadence() { return typeof(ModAPI.Events.TimeTriggerCadence); } }'
    Set-Content -LiteralPath 'PackageHelper.cs' -Value 'public class PackageHelper {}'
    Set-Content -LiteralPath 'CurrentLibrary.cs' -Value 'public class CurrentLibrary {}'
    Set-Content -LiteralPath 'CurrentPlugin.cs' -Value @'
using System.IO;
using ModAPI.Core;
public class CurrentPlugin : IModPlugin {
    public void Initialize(IPluginContext context) { File.WriteAllText("initialized", "yes"); }
    public void Start(IPluginContext context) { File.WriteAllText("started", "yes"); }
}
'@
    Set-Content -LiteralPath 'FailedPlugin.cs' -Value @'
using System;
using System.IO;
using ModAPI.Core;
public class FailedPlugin : IModPlugin {
    public void Initialize(IPluginContext context) {}
    public void Start(IPluginContext context) {
        File.WriteAllText("failed-start", "yes");
        throw new InvalidOperationException("Fixture startup failure.");
    }
}
'@
    $legacyModApi = Join-Path $LegacySmm 'ModAPI.dll'
    $legacySheltered = Join-Path $LegacySmm 'bin\ShelteredAPI.dll'
    Invoke-Checked $compiler @('/nologo','/target:library',"/r:$legacyModApi",'/r:UnityEngine.dll','LegacyPlugin.cs') 'legacy-plugin.log'
    Invoke-Checked $compiler @('/nologo','/target:library',"/r:$legacySheltered",'/r:UnityEngine.dll','LegacySheltered.cs') 'legacy-sheltered.log'
    Invoke-Checked $compiler @('/nologo','/target:library','PackageHelper.cs') 'helper.log'
    Invoke-Checked $compiler @('/nologo','/target:library','CurrentLibrary.cs') 'library.log'
    Invoke-Checked $compiler @('/nologo','/target:library','/r:UnityEngine.dll','/r:ModAPI.dll','CurrentPlugin.cs') 'neighbour.log'
    Invoke-Checked $compiler @('/nologo','/target:library','/r:UnityEngine.dll','/r:ModAPI.dll','FailedPlugin.cs') 'failed-plugin.log'
    $replacement = Join-Path $work 'replacement'
    New-Item -ItemType Directory -Path "$replacement\About","$replacement\Assemblies" | Out-Null
    Set-Content -LiteralPath "$replacement\About\About.json" -Encoding ascii -Value '{"id":"com.procreation.framework","name":"Family Expansion","version":"2.0","modApiVersion":"2.0.0.0"}'
    Copy-Item -LiteralPath 'CurrentPlugin.dll' -Destination "$replacement\Assemblies"
    $replacementZip = Join-Path $work 'family-expansion-2.0.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($replacement, $replacementZip)
    Invoke-Checked $compiler @('/nologo','/target:exe','/out:Fixture.exe','/r:Manager.exe','/r:ModAPI.dll','/r:UnityEngine.dll',
        '/r:System.Core.dll','/r:System.Windows.Forms.dll',(Join-Path $PSScriptRoot 'LegacyModMigrationFixture.cs')) 'harness-build.log'
    Set-Content -LiteralPath 'Fixture.exe.config' -Value '<configuration><startup><supportedRuntime version="v4.0" /></startup></configuration>'
    Invoke-Checked (Join-Path $work 'Fixture.exe') @($LegacySmm, $replacementZip) 'fixture.log'
    Get-Content -LiteralPath 'fixture.log'
}
finally {
    [Environment]::CurrentDirectory = $previousDirectory
    Pop-Location
}
