$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
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

$diagnostics = Get-ProjectSource 'Diagnostics\PerformanceSpikeDiagnostics.cs'
$traceContext = Get-ProjectSource 'Diagnostics\PerformanceTraceContext.cs'
$imageShow = Get-ProjectSource 'Node\1-Acquisition\ImageShow\NodeImageShow.cs'
$singleImage = Get-ProjectSource 'Forms\ImageViewer\FrmSingleImage.cs'
$solutionRun = Get-ProjectSource 'Forms\SolRunParam\SolRunParam.cs'
$showControl = Get-ProjectSource 'Forms\DispShowImage\ShowImageControl.cs'
$saveNode = Get-ProjectSource 'Node\7-ResultProcessing\ImageSave\NodeSaveImage.cs'
$saveForm = Get-ProjectSource 'Node\7-ResultProcessing\ImageSave\ParamFormSaveImage.cs'
$rotateNode = Get-ProjectSource 'Node\2-ImagePreprocessing\ImageRotate\NodeImageRotate.cs'
$overlayNode = Get-ProjectSource 'Node\7-ResultProcessing\ResultOverlayDraw\NodeResultOverlayDraw.cs'
$project = Get-ProjectSource 'TDJS-Vision.csproj'

Assert-Contains $diagnostics 'public static Stopwatch StartStopwatchIfEnabled(MsgLevel level)' 'Diagnostics must expose a Debug-gated stopwatch factory.'
Assert-Contains $diagnostics 'return IsDiagnosticLogEnabled(level) ? Stopwatch.StartNew() : null;' 'Debug-off timing must not allocate a Stopwatch.'
Assert-Contains $traceContext 'public static PerformanceTraceContext CreateIfEnabled(' 'Cross-thread diagnostics must use an explicit Debug-gated context.'
Assert-Contains $traceContext 'process?.CurrentPerformanceTraceId' 'The context must capture the current process TraceId.'
Assert-Contains $traceContext 'process?.CurrentRunId ?? 0' 'The context must capture the current process RunId.'

Assert-Contains $imageShow 'PerformanceTraceContext.CreateIfEnabled(Process, ID, NodeName)' 'The image-show node must create a flow context.'
Assert-Contains $imageShow 'sender is NodeBase sourceNode' 'Shared image-show publishers must derive context for other display node types.'
Assert-Contains $imageShow 'bool bitmapClaimed = PublishImageShowChanged(' 'The image-show node must publish through the ownership-aware event path.'
Assert-Contains $imageShow 'traceContext);' 'The image-show event must carry its originating trace context.'
Assert-NotContains $imageShow 'Stopwatch performanceStopwatch = Stopwatch.StartNew();' 'The image-show node must not time work while Debug is off.'
Assert-Contains $singleImage 'if (!e.TryTakeBitmap(out Bitmap bitmap))' 'The matching UI window must atomically claim frame ownership.'
Assert-Contains $solutionRun 'eventArgs.DisposeUnclaimedBitmap();' 'The continuous-capture publisher must release an unclaimed Bitmap.'
Assert-Contains $singleImage 'SetViewerImage(bitmap, e.DisplayResult, e.TraceContext);' 'The UI window must receive the claimed Bitmap and originating flow context.'
Assert-Contains $singleImage 'frame.TraceContext?.GetElapsedMilliseconds(Stopwatch.GetTimestamp())' 'The UI must report node-to-UI elapsed time from the frame-coupled context.'
Assert-NotContains $showControl 'Stopwatch queueStopwatch = Stopwatch.StartNew();' 'The display control must not allocate queue timers while Debug is off.'

Assert-Contains $saveNode 'public PerformanceTraceContext TraceContext { get; set; }' 'Background save tasks must carry their flow context.'
Assert-Contains $saveNode 'diagnosticEnabled = saveImagedata.TraceContext != null &' 'Save workers must gate diagnostics using the captured context and current Debug setting.'
Assert-Contains $saveNode 'if (diagnosticEnabled)' 'Save memory snapshots must be conditional.'
Assert-Contains $saveNode 'beforeSaveSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();' 'Save memory snapshots must only be captured inside the Debug gate.'
Assert-NotContains $saveNode 'string imageInfo = PerformanceSpikeDiagnostics.GetMatText(saveImagedata.SourceImage);' 'Save workers must not build image summaries while Debug is off.'
Assert-Contains $saveForm 'MemorySnapshot beforeSnapshot = stopwatch == null' 'Manual save rendering snapshots must be Debug-gated.'
Assert-Contains $saveForm 'if (beforeSnapshot == null || !PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug))' 'Manual save diagnostics must exit before taking the second snapshot.'

Assert-Contains $rotateNode 'PerformanceSpikeDiagnostics.LogSlowIfEnabled(' 'Image rotation slow diagnostics must use the lazy diagnostic path.'
Assert-Contains $overlayNode 'performanceDiagnostics = stopwatch == null ? null : new ResultOverlayDrawPerformanceDiagnostics();' 'ROI diagnostics must not allocate their detail object while Debug is off.'
Assert-Contains $overlayNode 'if (performanceDiagnostics != null)' 'ROI image summaries and per-item timers must be guarded.'

Assert-Contains $project '<Compile Include="Diagnostics\PerformanceTraceContext.cs" />' 'The project must compile the cross-thread trace context.'

Write-Host 'UI and save performance diagnostic regression checks passed.'
