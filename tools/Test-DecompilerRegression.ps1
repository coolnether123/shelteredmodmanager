#requires -Version 5.1
param([string]$GameAssembly = 'H:\SteamLibrary\steamapps\common\Sheltered\Sheltered_Data\Managed\Assembly-CSharp.dll')
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $GameAssembly -PathType Leaf)) { throw "Game assembly not found: $GameAssembly" }
& dotnet run --project "$PSScriptRoot\fixtures\DecompilerRegression\DecompilerRegression.csproj" -c Release -- $GameAssembly
if ($LASTEXITCODE -ne 0) { throw "Decompiler regression failed: $LASTEXITCODE" }
