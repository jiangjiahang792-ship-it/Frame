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
$roiControlPath = Join-Path $projectRoot 'Forms\ShapeDraw\ImageROIEditControl.cs'
$roiDesignerPath = Join-Path $projectRoot 'Forms\ShapeDraw\ImageROIEditControl.Designer.cs'
$qrFormPath = Join-Path $projectRoot 'Node\3-Detection\QRScan\NodeParamFormQRScan.cs'
$qrDesignerPath = Join-Path $projectRoot 'Node\3-Detection\QRScan\NodeParamFormQRScan.Designer.cs'
$largeModelPath = Join-Path $projectRoot 'Forms\AiTrainForm\LargeModelTrainForm.cs'
$unsupervisedPath = Join-Path $projectRoot 'Forms\AiTrainForm\UnsupervisedTrainForm.cs'

$roiControlSource = Get-Content -LiteralPath $roiControlPath -Raw -Encoding UTF8
$roiDesignerSource = Get-Content -LiteralPath $roiDesignerPath -Raw -Encoding UTF8
$qrFormSource = Get-Content -LiteralPath $qrFormPath -Raw -Encoding UTF8
$qrDesignerSource = Get-Content -LiteralPath $qrDesignerPath -Raw -Encoding UTF8
$largeModelSource = Get-Content -LiteralPath $largeModelPath -Raw -Encoding UTF8
$unsupervisedSource = Get-Content -LiteralPath $unsupervisedPath -Raw -Encoding UTF8

Assert-ContainsText $roiControlSource 'previous?.Dispose();' 'ROI image replacement must dispose the previous bitmap.'
Assert-ContainsText $roiDesignerSource 'ReleaseImageResources();' 'ROI control disposal must release its last owned bitmap.'
Assert-ContainsText $largeModelSource 'old = null;' 'Large-model preview replacement must avoid disposing the bitmap already released by the ROI control.'
Assert-ContainsText $unsupervisedSource 'old = null;' 'Unsupervised preview replacement must avoid disposing the bitmap already released by the ROI control.'
Assert-ContainsText $qrFormSource 'WeChatQRCode qrCode = await _modelLoadTask;' 'QR detection must await the single model-loading task.'
Assert-ContainsText $qrFormSource 'foreach (Mat roiImage in roiImages)' 'QR detection must release every extracted ROI image.'
Assert-ContainsText $qrFormSource 'foreach (Mat box in bbox)' 'QR detection must release every returned bounding-box Mat.'
Assert-ContainsText $qrFormSource 'using (CLAHE claheLimited' 'QR preprocessing must dispose the CLAHE native object.'
Assert-ContainsText $qrFormSource 'ReplacePictureBoxImage(pictureBoxCanny, blurred.ToBitmap());' 'QR preview replacement must release the previous PictureBox image.'
Assert-ContainsText $qrDesignerSource 'ReleaseImageResources();' 'QR form disposal must release model and preview resources.'
Assert-NotContainsText $qrFormSource 'var claheEq = new Mat()' 'QR preprocessing must not allocate an unused Mat.'
Assert-NotContainsText $qrFormSource 'pictureBoxCanny.Image =' 'QR previews must use the owned-image replacement helper.'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8)
$assemblyName = [string](
    $projectXml.Project.PropertyGroup |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.AssemblyName) } |
        Select-Object -First 1
).AssemblyName
$application = Get-Item -LiteralPath (Join-Path $debugDirectory ($assemblyName + '.exe')) -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'The Debug application is required for the ROI image replacement stress check.'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$controlType = $assembly.GetType('TDJS_Vision.Forms.ShapeDraw.ImageROIEditControl', $true)
$control = [Activator]::CreateInstance($controlType)
$control.Width = 320
$control.Height = 240
$control.CreateControl()

$first = New-Object Drawing.Bitmap 320, 240
$second = New-Object Drawing.Bitmap 320, 240
$control.SetImage($first)
$control.SetImage($second)
$firstDisposed = $false
try {
    $unusedHandle = $first.GetHbitmap()
}
catch {
    $firstDisposed = $true
}
Assert-True $firstDisposed 'Replacing an ROI preview must deterministically dispose the previous bitmap.'

$nativeMethods = @'
using System;
using System.Runtime.InteropServices;
public static class RoiImageControlNativeMethods
{
    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr processHandle, int flag);
}
'@
Add-Type -TypeDefinition $nativeMethods -Language CSharp

$process = [Diagnostics.Process]::GetCurrentProcess()
[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$gdiBefore = [RoiImageControlNativeMethods]::GetGuiResources($process.Handle, 0)

for ($i = 0; $i -lt 500; $i++) {
    $bitmap = New-Object Drawing.Bitmap 320, 240
    $control.SetImage($bitmap)
}

[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$gdiAfter = [RoiImageControlNativeMethods]::GetGuiResources($process.Handle, 0)
$gdiGrowth = $gdiAfter - $gdiBefore
$control.Dispose()

Assert-True ($gdiGrowth -le 25) "ROI image replacement leaked GDI resources: growth=$gdiGrowth."
Write-Host "QR and ROI image resource lifecycle checks passed. Replacement cycles=500, GDI growth=$gdiGrowth."
