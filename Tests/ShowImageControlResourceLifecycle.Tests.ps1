$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if ($Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$controlPath = Join-Path $projectRoot 'Forms\DispShowImage\ShowImageControl.cs'
$designerPath = Join-Path $projectRoot 'Forms\DispShowImage\ShowImageControl.Designer.cs'
$controlSource = Get-Content -LiteralPath $controlPath -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8

Assert-ContainsText $controlSource 'ClearRoiShapes(_staticRois);' 'Static ROI replacement must release disposable mask overlays.'
Assert-ContainsText $controlSource 'public class RoiMaskOverlay : IRoiShape, IDisposable' 'Mask overlay ROI must implement deterministic GDI cleanup.'
Assert-ContainsText $controlSource '_overlayBitmap?.Dispose();' 'Mask overlay ROI must dispose its internal bitmap.'
Assert-ContainsText $controlSource 'private void ReleaseOwnedResources()' 'The display control must expose one owned-resource cleanup path.'
Assert-ContainsText $designerSource 'ReleaseOwnedResources();' 'Control disposal must release the current image, ROI resources, and timer.'
Assert-NotContainsText $controlSource '? Brushes.Black : new SolidBrush' 'Checkerboard painting must not allocate an undisposed brush per tile.'
Assert-NotContainsText $controlSource 'g.FillRectangle(new SolidBrush' 'Painting must not pass an undisposed brush directly to FillRectangle.'
Assert-NotContainsText $controlSource 'g.FillPolygon(new SolidBrush' 'ROI arrows must not pass an undisposed brush directly to FillPolygon.'
Assert-NotContainsText $controlSource 'g.DrawArc(new Pen' 'ROI rotation handles must not pass an undisposed pen directly to DrawArc.'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8)
$assemblyName = [string](
    $projectXml.Project.PropertyGroup |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.AssemblyName) } |
        Select-Object -First 1
).AssemblyName
$application = Get-Item -LiteralPath (Join-Path $debugDirectory ($assemblyName + '.exe')) -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'The Debug application is required for the display-control GDI stress check.'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$controlType = $assembly.GetType('TDJS_Vision.Forms.DispShowImage.ShowImageControl', $true)
$control = [Activator]::CreateInstance($controlType)
$control.Width = 320
$control.Height = 240
$control.CreateControl()

$target = New-Object Drawing.Bitmap 320, 240
$mask = New-Object Drawing.Bitmap 64, 64, ([Drawing.Imaging.PixelFormat]::Format8bppIndexed)
$rect = New-Object Drawing.Rectangle 0, 0, 320, 240

$nativeMethods = @'
using System;
using System.Runtime.InteropServices;
public static class ShowImageControlNativeMethods
{
    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr processHandle, int flag);
}
'@
Add-Type -TypeDefinition $nativeMethods -Language CSharp

$process = [Diagnostics.Process]::GetCurrentProcess()
$control.DrawToBitmap($target, $rect)
[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$gdiBefore = [ShowImageControlNativeMethods]::GetGuiResources($process.Handle, 0)

for ($i = 0; $i -lt 250; $i++) {
    $control.DrawToBitmap($target, $rect)
}

for ($i = 0; $i -lt 100; $i++) {
    $control.AddMaskOverlay($mask, 0, 0, [Drawing.Color]::Lime, 100)
    $control.ClearStaticRoi()
}

[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$gdiAfter = [ShowImageControlNativeMethods]::GetGuiResources($process.Handle, 0)
$gdiGrowth = $gdiAfter - $gdiBefore

$control.Dispose()
$target.Dispose()
$mask.Dispose()

Assert-True ($gdiGrowth -le 25) "ShowImageControl GDI resources grew continuously during repaint: growth=$gdiGrowth."
Write-Host "ShowImageControl resource lifecycle checks passed. Paint cycles=250, mask cycles=100, GDI growth=$gdiGrowth."
