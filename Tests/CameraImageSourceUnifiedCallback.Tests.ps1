$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        return ''
    }

    return Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-Order {
    param(
        [string]$Text,
        [string]$First,
        [string]$Second,
        [string]$Message
    )

    $firstIndex = $Text.IndexOf($First)
    $secondIndex = $Text.IndexOf($Second)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw $Message
    }
}

$nodeSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\NodeImageSource.cs'
$awaiterSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\CameraFrameAwaiter.cs'
$paramSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\ParamFormImageSource.cs'
$designerSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\ParamFormImageSource.Designer.cs'
$validatorSource = Get-ProjectSource 'ProcessCameraConfigurationValidator.cs'
$processPanelSource = Get-ProjectSource 'Forms\ProcessNew\ProcessEditPanel.cs'
$processDesignerSource = Get-ProjectSource 'Forms\ProcessNew\ProcessEditPanel.Designer.cs'
$solutionSource = Get-ProjectSource 'Solution.cs'
$mainSource = Get-ProjectSource 'FormMain.cs'

Assert-Contains $awaiterSource 'public interface ICameraFrameAwaiter' 'A replaceable camera frame awaiter interface is required.'
Assert-Contains $awaiterSource 'Task<Mat> BeginWaitAsync(CancellationToken cancellationToken)' 'The frame wait API must accept a cancellation token.'
Assert-Contains $awaiterSource 'bool TrySupplyFrame(Mat frame)' 'The callback must use a non-blocking frame delivery API.'
Assert-Contains $nodeSource '_cameraFrameAwaiter.BeginWaitAsync(token)' 'NodeImageSource must await the callback in the current run.'
Assert-Contains $nodeSource 'param.Camera.GrabOne();' 'The soft trigger path must call GrabOne exactly once.'
Assert-Order $nodeSource '_cameraFrameAwaiter.BeginWaitAsync(token)' 'param.Camera.GrabOne();' 'The frame wait must be armed before GrabOne.'
Assert-NotContains $nodeSource 'GetOneFrameImage()' 'NodeImageSource must not use synchronous camera acquisition.'
Assert-NotContains $nodeSource 'Process.Run(this, false)' 'The camera callback must not start a new process run.'
Assert-Contains $paramSource 'if (param.TriggerModel == TriggerModel.Off)' 'Legacy Off parameters must be detected.'
Assert-Contains $paramSource 'param.TriggerModel = TriggerModel.On;' 'Legacy trigger mode must migrate to On.'
Assert-NotContains $paramSource 'IsCameraCallbackMenuEnabled' 'Callback binding must not depend on the process menu.'
Assert-NotContains $designerSource '"Off"' 'The parameter form must not display Off.'
Assert-NotContains $designerSource 'CamerModelComboBox' 'The trigger model control must be removed.'
Assert-Contains $validatorSource 'public interface IProcessCameraConfigurationValidator' 'A replaceable process camera validator is required.'
Assert-Contains $validatorSource 'ToDisplayText()' 'The validator error must expose Chinese display text.'
Assert-NotContains $processPanelSource 'ProcessNew.CameraCallbackTriggeredProcess' 'The legacy callback process menu language binding must be removed.'
Assert-NotContains $processDesignerSource '是否为相机回调触发流程ToolStripMenuItem' 'The legacy callback process menu must be removed from the designer.'
Assert-Contains $processPanelSource 'TryValidateCameraConfiguration' 'ProcessEditPanel must validate before running.'
Assert-Contains $mainSource 'TryValidateCameraConfiguration' 'FormMain must validate before running.'
Assert-NotContains $solutionSource 'TriggerSoftCameraCallbacksForProcesses(groupCopy)' 'The solution loop must not duplicate soft triggers.'
Assert-NotContains $solutionSource '.IsCameraCallbackTriggered' 'Runtime acquisition must not depend on the legacy process compatibility flag.'
Assert-Contains $solutionSource 'startedCallbackCameras = StartCameraCallbackGrabbingForProcesses(AllProcesses);' 'Single and loop solution runs must both start callback grabbing.'

Write-Host 'Camera image-source unified callback regression checks passed.'
