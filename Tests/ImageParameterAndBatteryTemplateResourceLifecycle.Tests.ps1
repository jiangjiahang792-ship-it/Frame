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

function Assert-OccurrenceCount {
    param([string]$Content, [string]$Pattern, [int]$Expected, [string]$Message)
    $actual = [regex]::Matches($Content, [regex]::Escape($Pattern)).Count
    if ($actual -ne $Expected) {
        throw ($Message + " Expected=$Expected, Actual=$actual.")
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$splitFormPath = Join-Path $projectRoot 'Node\2-ImagePreprocessing\ImageSplit\ParamFormImageSplit.cs'
$splitDesignerPath = Join-Path $projectRoot 'Node\2-ImagePreprocessing\ImageSplit\ParamFormImageSplit.Designer.cs'
$rotateFormPath = Join-Path $projectRoot 'Node\2-ImagePreprocessing\ImageRotate\NodeParamFormImageRotate.cs'
$rotateDesignerPath = Join-Path $projectRoot 'Node\2-ImagePreprocessing\ImageRotate\NodeParamFormImageRotate.Designer.cs'
$templateFormPath = Join-Path $projectRoot 'Node\3-Detection\BatteryEar\FormNewTemplate.cs'
$templateDesignerPath = Join-Path $projectRoot 'Node\3-Detection\BatteryEar\FormNewTemplate.Designer.cs'
$batteryFormPath = Join-Path $projectRoot 'Node\3-Detection\BatteryEar\NodeParamFormBatteryEar.cs'
$batteryDesignerPath = Join-Path $projectRoot 'Node\3-Detection\BatteryEar\NodeParamFormBatteryEar.Designer.cs'

$splitFormSource = Get-Content -LiteralPath $splitFormPath -Raw -Encoding UTF8
$splitDesignerSource = Get-Content -LiteralPath $splitDesignerPath -Raw -Encoding UTF8
$rotateFormSource = Get-Content -LiteralPath $rotateFormPath -Raw -Encoding UTF8
$rotateDesignerSource = Get-Content -LiteralPath $rotateDesignerPath -Raw -Encoding UTF8
$templateFormSource = Get-Content -LiteralPath $templateFormPath -Raw -Encoding UTF8
$templateDesignerSource = Get-Content -LiteralPath $templateDesignerPath -Raw -Encoding UTF8
$batteryFormSource = Get-Content -LiteralPath $batteryFormPath -Raw -Encoding UTF8
$batteryDesignerSource = Get-Content -LiteralPath $batteryDesignerPath -Raw -Encoding UTF8

Assert-ContainsText $splitFormSource 'private void SetSourceImage(Bitmap image)' 'Image-split source replacement must have an ownership-aware helper.'
Assert-ContainsText $splitFormSource 'if (!ReferenceEquals(previousDisplay, _image))' 'Image-split overlays must not dispose the reusable source image.'
Assert-ContainsText $splitFormSource 'private void ReleaseImageResources()' 'Image-split form must release both source and display images.'
Assert-ContainsText $splitDesignerSource 'ReleaseImageResources();' 'Image-split Designer disposal must release preview images.'
Assert-ContainsText $rotateFormSource 'private void ReleaseImageResources()' 'Image-rotate form must release its last preview image.'
Assert-ContainsText $rotateDesignerSource 'ReleaseImageResources();' 'Image-rotate Designer disposal must release preview images.'

Assert-ContainsText $templateFormSource 'private void EnsureTemplateImagesLoaded(string name)' 'Battery templates must be lazily cached instead of read for every ROI.'
Assert-ContainsText $templateFormSource 'snapshots.Add(template.Clone());' 'Detection must receive independent template snapshots.'
Assert-ContainsText $templateFormSource 'DisposeTemplateSet(previousTemplates);' 'Replacing a template set must release the previous Mat set.'
Assert-ContainsText $templateFormSource 'DisposeTemplateList(roiImages);' 'All ROI Mats from template editing must be released.'
Assert-ContainsText $templateFormSource 'DetachDesignerImageReferences();' 'Shared Designer resource images must be detached without disposal.'
Assert-ContainsText $templateFormSource 'return new Bitmap(source);' 'File-loaded Bitmaps must be detached from their closed stream.'
Assert-ContainsText $templateFormSource 'private void ReleaseTemplateResources()' 'Template form must expose one deterministic cleanup path.'
Assert-ContainsText $templateDesignerSource 'ReleaseTemplateResources();' 'Template form Designer disposal must release Mats and Bitmaps.'
Assert-NotContainsText $templateFormSource 'return (Bitmap)Bitmap.FromStream(ms);' 'Template source images must not depend on a disposed MemoryStream.'
Assert-NotContainsText $templateFormSource 'pictureBoxes[i - 1].Image =' 'Template preview replacement must not overwrite PictureBox images without cleanup.'

Assert-ContainsText $batteryFormSource 'private readonly FormNewTemplate _formNewTemplate;' 'Template forms must be owned per battery node instead of process-wide static state.'
Assert-NotContainsText $batteryFormSource 'private static FormNewTemplate' 'Battery nodes must not share an undisposed static template form.'
Assert-OccurrenceCount $batteryFormSource '_formNewTemplate.GetJiErTemplate()' 1 'JiEr templates must be snapshotted once per detection cycle.'
Assert-OccurrenceCount $batteryFormSource '_formNewTemplate.GetMarkTemplate()' 1 'Mark templates must be snapshotted once per detection cycle.'
Assert-ContainsText $batteryFormSource 'foreach (Mat template in jiErTemplateSnapshots)' 'JiEr template snapshots must be released after each detection cycle.'
Assert-ContainsText $batteryFormSource 'foreach (Mat template in markTemplateSnapshots)' 'Mark template snapshots must be released after each detection cycle.'
Assert-ContainsText $batteryFormSource '_formNewTemplate.LoadImages(preview);' 'Template preview should be generated only when template management opens.'
Assert-NotContainsText $batteryFormSource '_formNewTemplate.LoadImages(outputImage' 'Formal image refresh must not allocate a hidden template-window Bitmap.'
Assert-ContainsText $batteryDesignerSource 'ReleaseImageResources();' 'Battery parameter-form disposal must release its template form.'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8)
$assemblyName = [string](
    $projectXml.Project.PropertyGroup |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.AssemblyName) } |
        Select-Object -First 1
).AssemblyName
$application = Get-Item -LiteralPath (Join-Path $debugDirectory ($assemblyName + '.exe')) -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'The Debug application is required for the battery-template GDI stress check.'

$originalDirectory = [Environment]::CurrentDirectory
$originalPath = $env:PATH
[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$formType = $assembly.GetType('TDJS_Vision.Node._3_Detection.BatteryEar.FormNewTemplate', $true)
$shownMethod = $formType.GetMethod('FormNewTemplate_Shown', [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $shownMethod) 'Battery-template shown handler is required for repeated preview refresh testing.'

$nativeMethods = @'
using System;
using System.Runtime.InteropServices;
public static class BatteryTemplateNativeMethods
{
    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr processHandle, int flag);
}
'@
Add-Type -TypeDefinition $nativeMethods -Language CSharp

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('TDJS-BatteryTemplate-' + [Guid]::NewGuid().ToString('N'))
$templateDirectory = Join-Path $tempRoot 'Template\BatteryEar'
$assemblyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $requestedAssembly = New-Object Reflection.AssemblyName($eventArgs.Name)
    $dependencyPath = Join-Path $debugDirectory ($requestedAssembly.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
$form = $null
$gdiGrowth = [int]::MaxValue
try {
    New-Item -ItemType Directory -Path $templateDirectory -Force | Out-Null
    $seed = New-Object Drawing.Bitmap 64, 64
    try {
        $graphics = [Drawing.Graphics]::FromImage($seed)
        try {
            $graphics.Clear([Drawing.Color]::White)
            $graphics.FillRectangle([Drawing.Brushes]::Black, 8, 8, 24, 24)
        }
        finally {
            $graphics.Dispose()
        }

        $seed.Save((Join-Path $templateDirectory '添加模版.png'), [Drawing.Imaging.ImageFormat]::Png)
        foreach ($name in @('Template_JiEr1.bmp', 'Template_JiEr2.bmp', 'Template_JiEr3.bmp', 'Template_Mark1.bmp', 'Template_Mark2.bmp', 'Template_Mark3.bmp')) {
            $seed.Save((Join-Path $templateDirectory $name), [Drawing.Imaging.ImageFormat]::Bmp)
        }
    }
    finally {
        $seed.Dispose()
    }

    $env:PATH = $debugDirectory + ';' + $originalPath
    [AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolver)
    [Environment]::CurrentDirectory = $tempRoot
    $form = [Activator]::CreateInstance($formType)
    $form.CreateControl()

    $process = [Diagnostics.Process]::GetCurrentProcess()
    $shownMethod.Invoke($form, @($form, [EventArgs]::Empty))
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    $gdiBefore = [BatteryTemplateNativeMethods]::GetGuiResources($process.Handle, 0)

    for ($i = 0; $i -lt 100; $i++) {
        $shownMethod.Invoke($form, @($form, [EventArgs]::Empty))
    }

    for ($i = 0; $i -lt 250; $i++) {
        $jiErSnapshots = $form.GetJiErTemplate()
        $markSnapshots = $form.GetMarkTemplate()
        try {
            Assert-True ($jiErSnapshots.Count -eq 3) 'Every JiEr template file must produce one owned Mat snapshot.'
            Assert-True ($markSnapshots.Count -eq 3) 'Every Mark template file must produce one owned Mat snapshot.'
        }
        finally {
            foreach ($snapshot in $jiErSnapshots) {
                $snapshot.Dispose()
            }
            foreach ($snapshot in $markSnapshots) {
                $snapshot.Dispose()
            }
        }
    }

    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    $gdiAfter = [BatteryTemplateNativeMethods]::GetGuiResources($process.Handle, 0)
    $gdiGrowth = $gdiAfter - $gdiBefore
}
finally {
    if ($null -ne $form) {
        $form.Dispose()
    }
    [Environment]::CurrentDirectory = $originalDirectory
    $env:PATH = $originalPath
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)

    $resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
    $resolvedSystemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedTempRoot.StartsWith($resolvedSystemTemp, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Assert-True ($gdiGrowth -le 25) "Battery-template preview replacement leaked GDI resources: growth=$gdiGrowth."
Write-Host "Image parameter and battery-template lifecycle checks passed. Preview reloads=100, snapshot cycles=250, GDI growth=$gdiGrowth."
