#requires -Version 5.1
[CmdletBinding()]
param([string]$RepoRoot)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$assemblyPath = Join-Path $RepoRoot 'Dist\SMM\ModAPI.dll'
$managerAssemblyPath = Join-Path $RepoRoot 'Dist\SMM\Manager.exe'
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) { throw "Build ModAPI before running this contract: $assemblyPath" }
if (-not (Test-Path -LiteralPath $managerAssemblyPath -PathType Leaf)) { throw "Build Manager before running this contract: $managerAssemblyPath" }

$managerProject = Get-Content -LiteralPath (Join-Path $RepoRoot 'Manager\ManagerGUI.csproj') -Raw
$runtimeProject = Get-Content -LiteralPath (Join-Path $RepoRoot 'ModAPI\ModAPI.csproj') -Raw
foreach ($linkedSource in @('ManagerBooleanOptionContracts.cs', 'ManagerBooleanOptionPolicy.cs')) {
    if (-not $managerProject.Contains($linkedSource) -or -not $runtimeProject.Contains($linkedSource)) {
        throw "Manager and ModAPI must compile the same $linkedSource source."
    }
}

$assembly = [Reflection.Assembly]::LoadFile($assemblyPath)
$managerAssembly = [Reflection.Assembly]::LoadFile($managerAssemblyPath)
$definitionType = $assembly.GetType('ModAPI.Core.ManagerBooleanOptionDefinition', $true)
$descriptorType = $assembly.GetType('ModAPI.Core.ManagerBooleanOptionDescriptor', $true)
$fileType = $assembly.GetType('ModAPI.Core.ManagerBooleanOptionsFile', $true)
$recordType = $assembly.GetType('ModAPI.Core.ManagerBooleanOptionRecord', $true)
$policyType = $assembly.GetType('ModAPI.Core.ManagerBooleanOptionPolicy', $true)
if (-not $definitionType.IsPublic) { throw 'ManagerBooleanOptionDefinition must remain public.' }
if ($null -ne $managerAssembly.GetType('ModAPI.Core.ManagerBooleanOptionDefinition', $false)) { throw 'Manager.exe must not export ModAPI.Core.ManagerBooleanOptionDefinition.' }
foreach ($managerInternalTypeName in @('ManagerBooleanOptionDescriptor', 'ManagerBooleanOptionsFile', 'ManagerBooleanOptionRecord', 'ManagerBooleanOptionPolicy')) {
    $managerInternalType = $managerAssembly.GetType("ModAPI.Core.$managerInternalTypeName", $true)
    if ($managerInternalType.IsPublic) { throw "Manager.exe exported internal type $managerInternalTypeName." }
}
if ($descriptorType.IsPublic -or $fileType.IsPublic -or $recordType.IsPublic -or $policyType.IsPublic) { throw 'Policy input, persisted DTOs, and schema policy must remain internal.' }

$file = [Activator]::CreateInstance($fileType, $true)
$definition = [Activator]::CreateInstance($descriptorType, $true)
$descriptorType.GetField('Id').SetValue($definition, 'Example.Option')
$descriptorType.GetField('Owner').SetValue($definition, 'Example')
$descriptorType.GetField('Label').SetValue($definition, 'Initial label')
$descriptorType.GetField('DefaultValue').SetValue($definition, $true)
$merge = $policyType.GetMethod('MergeDefinition', [Reflection.BindingFlags]'Static, NonPublic')
$findRecord = $policyType.GetMethod('FindRecord', [Reflection.BindingFlags]'Static, NonPublic')
$trySet = $policyType.GetMethod('TrySetValue', [Reflection.BindingFlags]'Static, NonPublic')
if (-not [bool]$merge.Invoke($null, @($file, $definition))) { throw 'A new definition was not added.' }

$record = $findRecord.Invoke($null, @($file, 'example.option'))
if ($null -eq $record -or -not [bool]$recordType.GetField('value').GetValue($record)) { throw 'Case-insensitive lookup did not return the default value.' }
if (-not [bool]$trySet.Invoke($null, @($file, 'EXAMPLE.OPTION', $false))) { throw 'Case-insensitive value update failed.' }
$descriptorType.GetField('Label').SetValue($definition, 'Updated label')
$descriptorType.GetField('DefaultValue').SetValue($definition, $false)
if (-not [bool]$merge.Invoke($null, @($file, $definition))) { throw 'Metadata refresh was not detected.' }
$record = $findRecord.Invoke($null, @($file, 'Example.Option'))
if ($null -eq $record -or [bool]$recordType.GetField('value').GetValue($record)) { throw 'Metadata refresh reset the selected value.' }

$persistedFields = @($recordType.GetFields() | ForEach-Object Name | Sort-Object)
$expectedFields = @('defaultValue', 'description', 'id', 'label', 'owner', 'requiresRestart', 'sortOrder', 'value' | Sort-Object)
if (($persistedFields -join '|') -ne ($expectedFields -join '|')) { throw 'Persisted manager option field names changed.' }
Write-Host 'Manager boolean option contracts passed.'
