[CmdletBinding()]
param([string]$RepoRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

$sourceRoot = Join-Path $RepoRoot 'Shared\PixelEditing'
$sourceFiles = @(
    'Rgba32.cs',
    'PixelDocument.cs',
    'PixelSelection.cs',
    'PixelClipboard.cs',
    'PixelEditHistory.cs',
    'PixelEditorContracts.cs',
    'PixelEditorSession.cs'
)

$sourceBodies = $sourceFiles | ForEach-Object {
    (Get-Content -LiteralPath (Join-Path $sourceRoot $_) -Raw) `
        -replace '(?m)^using System;\r?\n', '' `
        -replace '(?m)^using System\.Collections\.Generic;\r?\n', ''
}
$source = @'
using System;
using System.Collections.Generic;
'@ + [Environment]::NewLine + ($sourceBodies -join [Environment]::NewLine)

$compiledTypes = @(Add-Type -TypeDefinition $source -Language CSharp -PassThru)

$failures = New-Object 'System.Collections.Generic.List[string]'
function Assert-True([string]$name, [bool]$condition) {
    if (-not $condition) {
        $failures.Add($name)
    }
}

$pixelNamespace = 'ShelteredModManager.Shared.PixelEditing'
function Get-PixelType([string]$name) {
    $type = @($compiledTypes | Where-Object { $_.FullName -eq "$pixelNamespace.$name" }) | Select-Object -First 1
    if ($null -eq $type) { throw "Compiled pixel type '$name' was not found." }
    return $type
}
function New-PixelObject([string]$name, [object[]]$arguments = @()) {
    return [Activator]::CreateInstance((Get-PixelType $name), $arguments)
}

$transparent = New-PixelObject 'Rgba32' @([byte]0, [byte]0, [byte]0, [byte]0)
$redHalf = New-PixelObject 'Rgba32' @([byte]255, [byte]0, [byte]0, [byte]127)
$blue = New-PixelObject 'Rgba32' @([byte]0, [byte]0, [byte]255, [byte]255)

$document = New-PixelObject 'PixelDocument' @(3, 2)
$document.SetPixel(1, 1, $redHalf)
Assert-True 'RGBA alpha is preserved' ($document.GetPixel(1, 1).Equals($redHalf))
Assert-True 'out-of-bounds try-set is rejected' (-not $document.TrySetPixel(-1, 0, $blue))

$bytes = $document.CopyRgbaBytes()
$bytes[([int]$bytes.Length - 1)] = 0
Assert-True 'exported RGBA bytes are detached' ($document.GetPixel(1, 1).A -eq 127)
$clone = $document.Clone()
$clone.SetPixel(1, 1, $blue)
Assert-True 'document clone is detached' ($document.GetPixel(1, 1).Equals($redHalf))

$selection = New-PixelObject 'PixelSelection' @(-2, 0, 4, 3)
$clipped = $selection.ClipTo(3, 2)
Assert-True 'selection clips x' ($clipped.X -eq 0 -and $clipped.Width -eq 2)
Assert-True 'selection clips y' ($clipped.Y -eq 0 -and $clipped.Height -eq 2)
$reverse = (Get-PixelType 'PixelSelection').GetMethod('FromCorners').Invoke($null, @(2, 1, 0, 0))
Assert-True 'selection corners normalize' ($reverse.X -eq 0 -and $reverse.Y -eq 0 -and $reverse.Width -eq 3 -and $reverse.Height -eq 2)

$clipboard = New-PixelObject 'PixelClipboard'
$document.SetPixel(0, 0, $blue)
Assert-True 'clipboard copies clipped selection' ($clipboard.CopyFrom($document, $clipped))
$pasteTarget = New-PixelObject 'PixelDocument' @(2, 2)
Assert-True 'clipboard paste changes destination' ($clipboard.PasteInto($pasteTarget, 1, 1))
Assert-True 'clipboard paste clips to destination' ($pasteTarget.GetPixel(1, 1).Equals($blue))

$sessionDocument = New-PixelObject 'PixelDocument' @(4, 4)
$session = New-PixelObject 'PixelEditorSession' @($sessionDocument, 2)
$session.ActiveColor = $redHalf
$session.BeginStroke()
[void]$session.PaintPixel(0, 0)
[void]$session.PaintPixel(1, 0)
$session.EndStroke()
Assert-True 'one stroke produces one undo entry' ($session.History.UndoCount -eq 1)
Assert-True 'stroke marks session dirty' $session.Dirty
Assert-True 'stroke undo succeeds' $session.Undo()
Assert-True 'stroke undo restores every pixel' ($session.Document.GetPixel(0, 0).Equals($transparent) -and $session.Document.GetPixel(1, 0).Equals($transparent))
Assert-True 'stroke redo succeeds' $session.Redo()
Assert-True 'stroke redo restores every pixel' ($session.Document.GetPixel(0, 0).Equals($redHalf) -and $session.Document.GetPixel(1, 0).Equals($redHalf))

$session.MarkSaved()
Assert-True 'mark-saved clears dirty state' (-not $session.Dirty)
$session.BeginStroke()
$session.EndStroke()
Assert-True 'empty stroke does not add history' ($session.History.UndoCount -eq 1)
$session.BeginStroke()
[void]$session.SetPixel(3, 3, $blue)
[void]$session.SetPixel(3, 3, $transparent)
$session.EndStroke()
Assert-True 'net-no-op stroke does not add history' ($session.History.UndoCount -eq 1)
Assert-True 'net-no-op stroke stays clean' (-not $session.Dirty)

[void]$session.SetPixel(2, 0, $blue)
[void]$session.SetPixel(3, 0, $blue)
[void]$session.SetPixel(0, 1, $blue)
Assert-True 'history remains bounded' ($session.History.UndoCount -eq 2)

$session.SetSelection((New-PixelObject 'PixelSelection' @(0, 0, 2, 1)))
Assert-True 'session copies selection' $session.CopySelection()
Assert-True 'session pastes selection' $session.Paste(2, 2)
$viewModel = $session.CreateViewModel()
Assert-True 'view model is detached' (-not [Object]::ReferenceEquals($viewModel.Document, $session.Document))
Assert-True 'view model reports clipboard' ($viewModel.HasClipboard -and $viewModel.ClipboardWidth -eq 2 -and $viewModel.ClipboardHeight -eq 1)

$codec = Get-PixelType 'IPixelImageCodec'
$destination = Get-PixelType 'IPixelEditorDestination'
Assert-True 'codec contract is framework-neutral interface' $codec.IsInterface
Assert-True 'destination contract is framework-neutral interface' $destination.IsInterface

if ($failures.Count -gt 0) {
    throw ('Pixel editing contracts failed: ' + ($failures -join ', '))
}

Write-Host 'Pixel editing contracts passed.'
