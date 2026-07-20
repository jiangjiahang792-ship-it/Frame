param(
    [string]$BinRoot = 'D:\MyCode\PublicWook\TDJS-Vision\bin\x64\Debug',
    [string]$BuildRoot = 'C:\tmp\TDJSVisionBuild',
    [string]$TemplateName = '',
    [string]$ExtractRoot = ''
)

$ErrorActionPreference = 'Stop'

$runtime = [string](Join-Path $BinRoot 'LargeModelDll')
$modelRoot = [string](Join-Path $BinRoot 'Model')
if ([string]::IsNullOrWhiteSpace($TemplateName)) {
    $templateItem = Get-ChildItem -File -LiteralPath $modelRoot -Filter '*.tdlarge' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
else {
    $templateItem = Get-Item -LiteralPath (Join-Path $modelRoot $TemplateName)
}

$template = [string]$templateItem.FullName
$datasetRoot = [string](Join-Path $BinRoot ('Model\_LargeModelTraining\' + [IO.Path]::GetFileNameWithoutExtension($templateItem.Name) + '\dataset\OK'))
$image = [string](Get-ChildItem -Recurse -File -LiteralPath $datasetRoot | Select-Object -First 1 -ExpandProperty FullName)
if ([string]::IsNullOrWhiteSpace($ExtractRoot)) {
    $ExtractRoot = Join-Path $modelRoot '_LargeModelSmoke'
}
$env:PATH = $BinRoot + ';' + $runtime + ';' + $env:PATH
$resolveDirs = @($BuildRoot, $BinRoot, $runtime)

$handler = [ResolveEventHandler]{
    param($sender, $args)
    $name = New-Object System.Reflection.AssemblyName($args.Name)
    foreach ($dir in $resolveDirs) {
        $dllPath = Join-Path $dir ($name.Name + '.dll')
        if (Test-Path -LiteralPath $dllPath) {
            return [Reflection.Assembly]::LoadFrom($dllPath)
        }

        $exePath = Join-Path $dir ($name.Name + '.exe')
        if (Test-Path -LiteralPath $exePath) {
            return [Reflection.Assembly]::LoadFrom($exePath)
        }
    }

    return $null
}

[AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
try {
    $appPath = Get-ChildItem -File -LiteralPath $BuildRoot -Filter '*.exe' | Select-Object -First 1 -ExpandProperty FullName
    $appAssembly = [Reflection.Assembly]::LoadFrom($appPath)
    $cvAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinRoot 'OpenCvSharp.dll'))

    Write-Output ('is64=' + [Environment]::Is64BitProcess)
    Write-Output ('runtime=' + $runtime)
    Write-Output ('template=' + $template)

    $templatePackage = $appAssembly.GetType('TDJS_Vision.Forms.AiTrainForm.LargeModelTemplatePackage', $true)
    $extract = $templatePackage.GetMethod('Extract').Invoke($null, [object[]]@($template, [string]$ExtractRoot))
    $modelPath = [string]$extract.GetType().GetProperty('ModelPath').GetValue($extract, $null)
    $bankPath = [string]$extract.GetType().GetProperty('BankPath').GetValue($extract, $null)
    $manifest = $extract.GetType().GetProperty('Manifest').GetValue($extract, $null)
    $deviceMode = [int]$manifest.GetType().GetProperty('DeviceMode').GetValue($manifest, $null)
    $imageThreshold = [single]$manifest.GetType().GetProperty('ImageThreshold').GetValue($manifest, $null)
    $areaThreshold = [int]$manifest.GetType().GetProperty('AreaThreshold').GetValue($manifest, $null)

    Write-Output ('model=' + $modelPath)
    Write-Output ('bank=' + $bankPath)
    Write-Output ('deviceMode=' + $deviceMode)

    $detectorType = $appAssembly.GetType('TDJS_Vision.Forms.AiTrainForm.LargeModelDinov2Detector', $true)
    $detectorCtor = $detectorType.GetConstructor([Type[]]@([string]))
    $detector = $detectorCtor.Invoke([object[]]@($runtime))
    try {
        $load = [int]$detectorType.GetMethod('LoadModel').Invoke($detector, [object[]]@($modelPath, $bankPath, $deviceMode))
        Write-Output ('load=' + $load)
        if ($load -ne 0) {
            exit 2
        }

        $cv2 = $cvAssembly.GetType('OpenCvSharp.Cv2', $true)
        $imreadModes = $cvAssembly.GetType('OpenCvSharp.ImreadModes', $true)
        $colorMode = [Enum]::Parse($imreadModes, 'Color')
        $method = $cv2.GetMethods() |
            Where-Object { $_.Name -eq 'ImRead' -and $_.GetParameters().Count -eq 2 -and $_.GetParameters()[0].ParameterType -eq [string] } |
            Select-Object -First 1

        $mat = $method.Invoke($null, [object[]]@($image, $colorMode))
        Write-Output ('image=' + $image)
        $infer = $detectorType.GetMethod('InferBgr').Invoke($detector, [object[]]@($mat, $imageThreshold, $areaThreshold, 128))
        $returnCode = [int]$infer.GetType().GetProperty('ReturnCode').GetValue($infer, $null)
        $score = $infer.GetType().GetProperty('Score').GetValue($infer, $null)
        $boxes = $infer.GetType().GetProperty('Boxes').GetValue($infer, $null)
        Write-Output ('infer=' + $returnCode + ' score=' + $score + ' boxes=' + $boxes.Length)
        if ($returnCode -lt 0) {
            exit 3
        }
    }
    finally {
        if ($detector -is [IDisposable]) {
            $detector.Dispose()
        }
    }
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($handler)
}
