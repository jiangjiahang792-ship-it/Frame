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
Assert-Contains $nodeSource 'camera.OnMatReceived += HandleCameraCallbackFrame;' 'The image source must bind the captured camera callback only while a run is waiting.'
Assert-Contains $nodeSource 'camera.OnMatReceived -= HandleCameraCallbackFrame;' 'The image source must unbind the same captured camera after every wait.'
Assert-Contains $nodeSource 'camera.GrabOne();' 'The soft trigger path must call GrabOne exactly once.'
Assert-Order $nodeSource '_cameraFrameAwaiter.BeginWaitAsync(token)' 'camera.GrabOne();' 'The frame wait must be armed before GrabOne.'
Assert-Contains $nodeSource 'AcquireProductionCameraImageAsync(' 'Production software acquisition must use a workpiece ticket.'
Assert-Contains $nodeSource 'registry.Register(' 'The production ticket must be registered before the camera command.'
Assert-Order $nodeSource '? registry.Register(' 'ticket.IssueSoftwareTrigger(() =>' 'Ticket registration must precede the real software trigger.'
Assert-Contains $nodeSource 'registry.RegisterHardwareWait(' 'Production hardware acquisition must atomically register and arm a hardware ticket.'
Assert-Contains $nodeSource ': Task.CompletedTask;' 'The hardware path must wait for the physical frame without issuing a software command.'
Assert-Contains $nodeSource 'ticket.TriggerKind == CameraTriggerKind.Hardware' 'Hardware acquisition must explicitly select the stop-only wait policy.'
Assert-Contains $nodeSource 'Task.Delay(Timeout.Infinite, timeoutCancellation.Token)' 'Hardware acquisition must wait indefinitely until a frame or stop cancellation arrives.'
Assert-Contains $nodeSource 'DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)' 'Hardware tickets must not expire according to the software acquisition timeout.'
Assert-Contains $nodeSource 'GetProductionCameraTriggerKind(param.TriggerSource)' 'The production image source must classify the configured trigger source explicitly.'
Assert-Contains $nodeSource 'GetEffectiveTriggerSource(param.TriggerSource) == TriggerSource.SOFT' 'The non-production Auto path must issue the same software trigger as SOFT.'
Assert-Contains $nodeSource 'triggerSource == TriggerSource.Auto || triggerSource == TriggerSource.SOFT' 'Auto and SOFT must both follow the software-trigger path.'
Assert-Contains $nodeSource 'Task.WhenAny(' 'The camera command and frame must share an asynchronous timeout.'
Assert-Contains $nodeSource 'registry.FailTriggeredTicket(ticket, failure)' 'A stuck SDK trigger must fault and isolate the camera.'
Assert-Contains $nodeSource 'TryCancelBeforeSdkCommand()' 'A queued trigger must be cancelled atomically before timeout cleanup.'
Assert-Contains $nodeSource 'frameTask.Status == TaskStatus.RanToCompletion' 'A frame already committed at the timeout boundary must win the race.'
Assert-Contains $nodeSource '!(timeoutException is SoftwareTriggerCommandTimeoutException)' 'An in-flight SDK command timeout must not enter ordinary camera restart recovery.'
Assert-Contains $nodeSource 'InitialProductionFrameStartupGraceMilliseconds = 500' 'The first production frame after camera connection must have a bounded cold-start grace.'
Assert-Contains $nodeSource 'TryReserveInitialProductionFrameGrace()' 'The image source must atomically reserve first-frame grace from the camera connection session.'
Assert-Contains $nodeSource 'AddInitialProductionFrameStartupGrace(' 'The first-frame grace must be separate from the configured steady-state timeout.'
Assert-Contains $nodeSource 'RecoverProductionCameraAfterFrameTimeout(' 'A normal software-frame timeout must recover only the affected camera.'
Assert-Order $nodeSource 'camera.StopGrabbing();' 'camera.StartGrabbing();' 'Timeout recovery must stop and drain the old stream before restarting it.'
Assert-Contains $nodeSource '方案继续运行' 'A recovered software-frame timeout must explicitly preserve the solution run.'
Assert-NotContains $nodeSource '硬触发生产帧尚未接入外部触发就绪握手' 'A valid source-node hardware trigger must not be rejected by the production path.'
Assert-NotContains $nodeSource 'GetOneFrameImage()' 'NodeImageSource must not use synchronous camera acquisition.'
Assert-NotContains $nodeSource 'Process.Run(this, false)' 'The camera callback must not start a new process run.'
Assert-Contains $paramSource 'comboBoxTriggerModel.SelectedIndex = param.TriggerModel == TriggerModel.Off ? 1 : 0;' 'Saved trigger mode must be restored without migration.'
Assert-NotContains $paramSource 'param.TriggerModel = TriggerModel.On;' 'Opening a saved continuous mode must not overwrite it.'
Assert-NotContains $paramSource 'IsCameraCallbackMenuEnabled' 'Callback binding must not depend on the process menu.'
Assert-NotContains $paramSource 'SyncCameraCallbackBinding' 'The parameter form must not keep a permanent camera frame subscription.'
Assert-NotContains $designerSource '"Off"' 'The parameter form must not display Off.'
Assert-Contains $designerSource 'this.tableLayoutPanelCamera.Controls.Add(this.comboBoxTriggerModel, 1, 1);' 'The trigger model selector must participate in the designer layout.'
Assert-Contains $designerSource 'this.labelTriggerModel.Text = "触发模式";' 'The trigger mode selection must exist in the designer.'
Assert-Contains $nodeSource 'registry.RegisterContinuousWait(' 'Continuous frames must preserve production ticket and budget ownership.'
Assert-Contains $validatorSource 'public interface IProcessCameraConfigurationValidator' 'A replaceable process camera validator is required.'
Assert-Contains $validatorSource 'ToDisplayText()' 'The validator error must expose Chinese display text.'
Assert-Contains $validatorSource 'IsSoftwareTriggerSource(param.TriggerSource)' 'A software-trigger image source must remain legal after Modbus, PLC, or other upstream nodes.'
Assert-Contains $validatorSource '!HasActiveUpstreamNode(process, imageSource)' 'Only hardware-trigger image sources with upstream nodes must be rejected.'
Assert-NotContains $processPanelSource 'ProcessNew.CameraCallbackTriggeredProcess' 'The legacy callback process menu language binding must be removed.'
Assert-NotContains $processDesignerSource '是否为相机回调触发流程ToolStripMenuItem' 'The legacy callback process menu must be removed from the designer.'
Assert-Contains $processPanelSource 'TryValidateCameraConfiguration' 'ProcessEditPanel must validate before running.'
Assert-Contains $mainSource 'TryValidateCameraConfiguration' 'FormMain must validate before running.'
Assert-NotContains $solutionSource 'TriggerSoftCameraCallbacksForProcesses(groupCopy)' 'The solution loop must not duplicate soft triggers.'
Assert-NotContains $solutionSource '.IsCameraCallbackTriggered' 'Runtime acquisition must not depend on the legacy process compatibility flag.'
Assert-Contains $solutionSource 'startedCallbackCameras = StartCameraCallbackGrabbingForProcesses(AllProcesses);' 'Single and loop solution runs must both start callback grabbing.'

Write-Host 'Camera image-source unified callback regression checks passed.'
