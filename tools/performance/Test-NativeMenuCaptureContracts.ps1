#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
# Synthetic native boundary. No real HWND, desktop image or game process is used.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
namespace ShelteredBenchmark {
    public static class NativeWindow {
        public static int FullMode;
        public static int ClassicMode;
        public static int Calls;
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        public static bool GetWindowRect(IntPtr handle, out Rect rect) {
            rect = new Rect { Right = 100, Bottom = 100 }; return true;
        }
        public static bool PrintWindow(IntPtr handle, IntPtr hdc, uint flags) {
            Calls++;
            int mode = flags == 2 ? FullMode : ClassicMode;
            if (mode < 0) return false;
            using (Graphics graphics = Graphics.FromHdc(hdc)) {
                graphics.Clear(mode == 0 ? Color.Black : Color.White);
            }
            return true;
        }
    }
}
'@
Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1') -Force -DisableNameChecking
$root = Join-Path ([IO.Path]::GetTempPath()) ('smm-native-capture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$passed = 0
function Assert-Capture($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:passed++
}
try {
    [ShelteredBenchmark.NativeWindow]::FullMode = 0
    [ShelteredBenchmark.NativeWindow]::ClassicMode = 0
    $failure = ''
    try { $bitmap = New-NativeWindowBitmap -Handle 1; $bitmap.Dispose() } catch { $failure = $_.Exception.Message }
    Assert-Capture ($failure -match 'no usable frame') 'Two successful blank PrintWindow responses must be rejected.'
    Assert-Capture ([ShelteredBenchmark.NativeWindow]::Calls -eq 2) 'Blank full-content mode must retry classic capture once.'
    [ShelteredBenchmark.NativeWindow]::ClassicMode = 1
    $bitmap = New-NativeWindowBitmap -Handle 1
    try { Assert-Capture ($bitmap.GetPixel(50, 50).R -eq 255) 'Classic capture did not recover the visible frame.' }
    finally { $bitmap.Dispose() }
    [ShelteredBenchmark.NativeWindow]::FullMode = 1
    [ShelteredBenchmark.NativeWindow]::Calls = 0
    $bitmap = New-NativeWindowBitmap -Handle 1
    $reference = Join-Path $root 'visible.png'
    try { $bitmap.Save($reference, [Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
    Assert-Capture ([ShelteredBenchmark.NativeWindow]::Calls -eq 1) 'A usable full-content frame must not retry.'
    Assert-Capture ((Get-NativeFrameRmse -Handle 1 -ReferencePath $reference) -eq 0) 'Identical synthetic frames did not match exactly.'
    $blank = New-Object Drawing.Bitmap 100, 100
    $blankPath = Join-Path $root 'blank.png'
    try { $blank.Save($blankPath, [Drawing.Imaging.ImageFormat]::Png) } finally { $blank.Dispose() }
    $failure = ''
    try { Get-NativeFrameRmse -Handle 1 -ReferencePath $blankPath | Out-Null } catch { $failure = $_.Exception.Message }
    Assert-Capture ($failure -match 'reference is blank') 'A blank reference must not become readiness evidence.'
    $failure = ''
    try { Wait-NativeMenuReady -Process ([Diagnostics.Process]::GetCurrentProcess()) -ReferencePath (Join-Path $root 'missing.png') -TimeoutSeconds 0 } catch { $failure = $_.Exception.Message }
    Assert-Capture ($failure -match 'Configured native menu reference is missing') 'A missing configured reference must not downgrade to a delay fallback.'
    Write-Host "All $passed native menu capture checks passed. Synthetic native boundary only."
}
finally {
    if ([IO.Path]::GetFullPath($root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
