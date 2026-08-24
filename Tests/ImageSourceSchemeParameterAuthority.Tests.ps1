$ErrorActionPreference = 'Stop'

# The production assembly targets .NET Framework 4.8.
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Get-Source {
    param([string]$RelativePath)
    $path = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

function Get-MethodBody {
    param([string]$Source, [string]$Signature, [string]$NextMarker)
    $start = $Source.IndexOf($Signature, [System.StringComparison]::Ordinal)
    if ($start -lt 0) {
        throw "Method signature not found: $Signature"
    }
    $end = $Source.IndexOf($NextMarker, $start, [System.StringComparison]::Ordinal)
    if ($end -lt 0) {
        throw "Method end marker not found: $NextMarker"
    }
    return $Source.Substring($start, $end - $start)
}

$paramFormSource = Get-Source 'Node\1-Acquisition\ImageSource\ParamFormImageSource.cs'
$designerSource = Get-Source 'Node\1-Acquisition\ImageSource\ParamFormImageSource.Designer.cs'
$solutionSource = Get-Source 'Solution.cs'
$nodeSource = Get-Source 'Node\1-Acquisition\ImageSource\NodeImageSource.cs'
$runParamSource = Get-Source 'Forms\SolRunParam\SolRunParam.cs'
$singleCameraSource = Get-Source 'Forms\CameraAdd\SingleCamera.cs'

$cameraSelectionBody = Get-MethodBody $paramFormSource 'private void comboBoxChoiceCamera_SelectedIndexChanged' 'private void RefreshTriggerSourceItems'
Assert-True (-not $cameraSelectionBody.Contains('GetExposureTime(')) 'Camera selection must not read the current exposure.'
Assert-True (-not $cameraSelectionBody.Contains('GetGain(')) 'Camera selection must not read the current gain.'
Assert-True (-not $cameraSelectionBody.Contains('GetTriggerDelay(')) 'Camera selection must not read the current trigger delay.'
Assert-True ($paramFormSource.Contains('_isRestoringParameters')) 'Scheme restoration must suppress camera selection callbacks.'
Assert-True ($paramFormSource.Contains('ResolveImageSourceCamera(param)')) 'Scheme restoration must resolve the current camera by name.'
Assert-True ($paramFormSource.Contains('SetNumericUpDownValueInRange(numericUpDownExposureTime, (decimal)param.ExposureTime);')) 'The exposure control must use the scheme value.'
Assert-True ($paramFormSource.Contains('SetNumericUpDownValueInRange(numericUpDownGain, (decimal)param.Gain);')) 'The gain control must use the scheme value.'
Assert-True ($paramFormSource.Contains('cameraToConfigure != null && cameraToConfigure.IsOpen')) 'An offline camera must not fail scheme restoration.'
Assert-True ($paramFormSource.Contains('control.Maximum = value;')) 'The numeric control must expand instead of truncating a scheme value.'
Assert-True ($paramFormSource.Contains('camera.StopGrabbing();')) 'A grabbing camera must be paused before scheme parameters are applied.'
Assert-True ($paramFormSource.Contains('camera.StartGrabbing();')) 'Camera grabbing must resume after scheme parameters are applied.'

Assert-True ($designerSource.Contains('10000000')) 'Exposure and delay controls must accept normal industrial values.'
Assert-True ($designerSource.Contains('this.numericUpDownGain.DecimalPlaces = 3;')) 'The gain control must retain decimal precision.'
Assert-True ($designerSource.Contains('this.numericUpDownExposureTime.DecimalPlaces = 3;')) 'The exposure control must retain decimal precision.'

Assert-True ($solutionSource.Contains('public ICamera ResolveImageSourceCamera')) 'Solution must expose camera rebinding for image sources.'
Assert-True ($solutionSource.Contains('string.Equals(camera.UserDefinedName, param.CameraName')) 'Camera rebinding must use the saved scheme name.'
Assert-True ($solutionSource.Contains('camera.SetExposureTime(param.ExposureTime);')) 'Startup must write the scheme exposure to the camera.'
Assert-True ($solutionSource.Contains('camera.SetGain(param.Gain);')) 'Startup must write the scheme gain to the camera.'
Assert-True ($solutionSource.Contains('ValidateSharedCameraParameterConsistency(validationProcessList);')) 'Whole-solution and single-process startup must reject conflicting shared-camera schemes.'
Assert-True ($solutionSource.Contains('.Concat(processList)')) 'Single-process startup validation must include all enabled solution processes.'
Assert-True ($solutionSource.Contains('HaveEquivalentCameraParameters')) 'Camera reconnect must reject conflicting shared-camera schemes.'
Assert-True ($nodeSource.Contains('Solution.Instance.ResolveImageSourceCamera(param)')) 'An image-source run must resolve the current camera object.'
Assert-True ($nodeSource.Contains('AcquireCameraImageAsync(param, camera, token)')) 'A frame wait must capture one stable camera object.'
Assert-True (-not $nodeSource.Contains('param.Camera.OnMatReceived')) 'Frame callback binding must not use a mutable camera reference.'

Assert-True (-not $runParamSource.Contains('nodeParam.Camera.GetExposureTime()')) 'Runtime tuning must not replace the scheme exposure with a camera read.'
Assert-True (-not $runParamSource.Contains('nodeParam.Camera.GetGain()')) 'Runtime tuning must not replace the scheme gain with a camera read.'
Assert-True ($runParamSource.Contains('textBoxExposureTime.Text = nodeParam.ExposureTime.ToString();')) 'Runtime tuning must display the scheme exposure.'
Assert-True ($runParamSource.Contains('textBoxGain.Text = nodeParam.Gain.ToString();')) 'Runtime tuning must display the scheme gain.'
Assert-True ($singleCameraSource.Contains('Solution.Instance.TryApplyImageSourceParametersForCamera(Camera);')) 'Camera reconnect must apply image-source scheme parameters.'

$projectRoot = Split-Path -Parent $PSScriptRoot
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-ChildItem -LiteralPath $debugDirectory -Filter '*.exe' | Select-Object -First 1
Assert-True ($null -ne $application) 'The scheme behavior test requires the latest Debug executable.'
Assert-True ($application.LastWriteTimeUtc -ge (Get-Item -LiteralPath (Join-Path $projectRoot 'Solution.cs')).LastWriteTimeUtc) 'The Debug executable is older than Solution.cs.'

[Environment]::CurrentDirectory = $debugDirectory
$assemblyResolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $assemblyName = New-Object Reflection.AssemblyName($eventArgs.Name)
    $dependencyPath = Join-Path $debugDirectory ($assemblyName.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolveHandler)
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$solutionType = $assembly.GetType('TDJS_Vision.Solution', $true)
$paramType = $assembly.GetType('TDJS_Vision.Node._1_Acquisition.ImageSource.NodeParamImageSoucre', $true)
$triggerSourceType = $assembly.GetType('TDJS_Vision.Device.Camera.TriggerSource', $true)
$triggerEdgeType = $assembly.GetType('TDJS_Vision.Device.Camera.TriggerEdge', $true)
$equivalentMethod = $solutionType.GetMethod('HaveEquivalentCameraParameters', [Reflection.BindingFlags]'Static,NonPublic')

function New-CameraSchemeParam {
    param(
        [string]$TriggerSource,
        [string]$TriggerEdge,
        [double]$Exposure,
        [double]$Gain
    )
    $value = [Activator]::CreateInstance($paramType)
    $value.TriggerSource = [Enum]::Parse($triggerSourceType, $TriggerSource)
    $value.TriggerEdge = [Enum]::Parse($triggerEdgeType, $TriggerEdge)
    $value.TriggerDelay = 10
    $value.ExposureTime = $Exposure
    $value.Gain = $Gain
    $value.TimeOut = 100
    return $value
}

$softAuto = New-CameraSchemeParam 'Auto' 'Rising' 5000 2.5
$softExplicit = New-CameraSchemeParam 'SOFT' 'Falling' 5000 2.5
Assert-True ([bool]$equivalentMethod.Invoke($null, @($softAuto, $softExplicit))) 'Auto and SOFT should be equivalent, and software trigger edges must be ignored.'
$differentExposure = New-CameraSchemeParam 'SOFT' 'Rising' 2000 2.5
Assert-True (-not [bool]$equivalentMethod.Invoke($null, @($softAuto, $differentExposure))) 'Different exposures on one camera must be rejected.'
$hardRising = New-CameraSchemeParam 'LINE0' 'Rising' 5000 2.5
$hardFalling = New-CameraSchemeParam 'LINE0' 'Falling' 5000 2.5
Assert-True (-not [bool]$equivalentMethod.Invoke($null, @($hardRising, $hardFalling))) 'Different hardware trigger edges on one camera must be rejected.'

[AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolveHandler)
Write-Host 'Image-source scheme authority checks passed.'
