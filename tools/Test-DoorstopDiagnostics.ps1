#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '..\Doorstop\Loader.cs') -Raw
$start = $source.IndexOf('namespace Doorstop')
$end = $source.IndexOf('#endregion', $start)
$entry = $source.Substring($start, $end - $start)
$fixture = @"
using System;
using System.IO;
class LoaderDebugLog { public static void Reset(string text) {} public static void Write(string text) {} }
class Loader { public static int Calls; public static void Launch() { Calls++; } }
namespace System.Diagnostics {
 class Process {
  public static bool Fail;
  public static Process GetCurrentProcess() { if (Fail) throw new InvalidOperationException("Synthetic diagnostic failure"); return new Process(); }
  public Process MainModule { get { return this; } }
  public string FileName { get { return "fixture.exe"; } }
 }
}
public class DoorstopDiagnosticsFixture {
 public static void Run() {
  System.Diagnostics.Process.Fail = false;
  Doorstop.Entrypoint.Start();
  if (Loader.Calls != 1) throw new Exception("Normal diagnostic did not launch once.");
  System.Diagnostics.Process.Fail = true;
  Doorstop.Entrypoint.Start();
  if (Loader.Calls != 2) throw new Exception("Failed process diagnostic prevented launch.");
 }
}
"@
Add-Type -TypeDefinition ($fixture + $entry) -IgnoreWarnings -WarningAction SilentlyContinue
[DoorstopDiagnosticsFixture]::Run()
if (-not $source.Contains('Timeout: bootstrap was not triggered after {0} attempts.')) { throw 'Timeout must describe bootstrap state rather than claim no callbacks arrived.' }
Write-Host 'Doorstop diagnostic assertions passed: 3'
