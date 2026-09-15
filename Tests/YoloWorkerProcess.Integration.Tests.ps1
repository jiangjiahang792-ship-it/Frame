param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [string]$ModelPath,

    [string]$ImagePath,

    [string]$TransportImagePath,

    [int]$TransportCycles = 0
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Worker integration executable does not exist: $ExecutablePath"
}

if ([string]::IsNullOrWhiteSpace($ModelPath) -xor [string]::IsNullOrWhiteSpace($ImagePath)) {
    throw "ModelPath and ImagePath must be provided together."
}
if ($TransportCycles -lt 0) {
    throw "TransportCycles cannot be negative."
}
if ($TransportCycles -gt 0 -and [string]::IsNullOrWhiteSpace($TransportImagePath)) {
    throw "TransportImagePath is required when TransportCycles is greater than zero."
}

function Send-WorkerRequest {
    param(
        [System.Diagnostics.Process]$WorkerProcess,
        [System.IO.StreamWriter]$WorkerInput,
        [string]$RequestJson,
        [int]$TimeoutMilliseconds = 10000
    )

    $responseTask = $WorkerProcess.StandardOutput.ReadLineAsync()
    $WorkerInput.WriteLine($RequestJson)
    if (-not $responseTask.Wait($TimeoutMilliseconds)) {
        throw "YOLO worker request timed out."
    }

    $responseText = $responseTask.Result
    if ([string]::IsNullOrWhiteSpace($responseText)) {
        $errorText = $WorkerProcess.StandardError.ReadToEnd()
        throw "YOLO worker exited without a response. stderr=$errorText"
    }

    return $responseText | ConvertFrom-Json
}

function Get-BgrImagePayload {
    param([string]$Path)

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::FromFile($Path)
    try {
        $rectangle = New-Object System.Drawing.Rectangle(0, 0, $bitmap.Width, $bitmap.Height)
        $bitmapData = $bitmap.LockBits(
            $rectangle,
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        try {
            $rowByteCount = $bitmap.Width * 3
            $bytes = New-Object byte[] ($rowByteCount * $bitmap.Height)
            for ($row = 0; $row -lt $bitmap.Height; $row++) {
                $source = [IntPtr]::Add($bitmapData.Scan0, $row * $bitmapData.Stride)
                [System.Runtime.InteropServices.Marshal]::Copy($source, $bytes, $row * $rowByteCount, $rowByteCount)
            }

            return [PSCustomObject]@{
                Rows = $bitmap.Height
                Cols = $bitmap.Width
                MatType = 16
                RowBytes = $rowByteCount
                Bytes = $bytes
            }
        }
        finally {
            $bitmap.UnlockBits($bitmapData)
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $ExecutablePath
$startInfo.Arguments = "--tdjs-yolo-worker"
$startInfo.WorkingDirectory = Split-Path -Parent $ExecutablePath
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true
$startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $startInfo
$imageMap = $null
$imageView = $null

try {
    if (-not $process.Start()) {
        throw "Failed to start YOLO worker integration process."
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $input = New-Object System.IO.StreamWriter($process.StandardInput.BaseStream, $utf8NoBom)
    $input.AutoFlush = $true
    $response = Send-WorkerRequest $process $input '{"Command":"ping"}'
    if (-not $response.Success) {
        throw "YOLO worker ping failed: $($response.Error)"
    }

    $probeBytes = [byte[]](1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18)
    $probeMapName = "TDJS_VISION_YOLO_TEST_$PID`_$([Guid]::NewGuid().ToString('N'))"
    $imageMap = [System.IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew($probeMapName, [long]$probeBytes.Length)
    $imageView = $imageMap.CreateViewAccessor()
    $imageView.WriteArray(0L, $probeBytes, 0, $probeBytes.Length)
    $probeJson = @{
        Command = "validate-image"
        Rows = 2
        Cols = 3
        MatType = 16
        ImageMapName = $probeMapName
        ImageMapCapacity = $probeBytes.Length
        ImageByteCount = $probeBytes.Length
        ImageStride = 9
    } | ConvertTo-Json -Compress
    $probeResponse = Send-WorkerRequest $process $input $probeJson
    if (-not $probeResponse.Success -or [double]$probeResponse.SharedImageValueSum -ne 171.0) {
        throw "YOLO worker shared-image validation failed: $($probeResponse | ConvertTo-Json -Compress)"
    }
    $imageView.Dispose()
    $imageView = $null
    $imageMap.Dispose()
    $imageMap = $null

    if ($TransportCycles -gt 0) {
        if (-not (Test-Path -LiteralPath $TransportImagePath -PathType Leaf)) {
            throw "YOLO transport image does not exist: $TransportImagePath"
        }

        $transportImage = Get-BgrImagePayload $TransportImagePath
        $transportMapName = "TDJS_VISION_YOLO_STRESS_$PID`_$([Guid]::NewGuid().ToString('N'))"
        $imageMap = [System.IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew(
            $transportMapName,
            [long]$transportImage.Bytes.Length)
        $imageView = $imageMap.CreateViewAccessor()
        $transportRequest = @{
            Command = "validate-image"
            Rows = $transportImage.Rows
            Cols = $transportImage.Cols
            MatType = $transportImage.MatType
            ImageMapName = $transportMapName
            ImageMapCapacity = $transportImage.Bytes.Length
            ImageByteCount = $transportImage.Bytes.Length
            ImageStride = $transportImage.RowBytes
        } | ConvertTo-Json -Compress
        [double]$expectedValueSum = 0.0
        foreach ($value in $transportImage.Bytes) {
            $expectedValueSum += $value
        }

        $durations = New-Object double[] $TransportCycles
        for ($cycle = 0; $cycle -lt $TransportCycles; $cycle++) {
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $imageView.WriteArray(0L, [byte[]]$transportImage.Bytes, 0, $transportImage.Bytes.Length)
            $transportResponse = Send-WorkerRequest $process $input $transportRequest
            $watch.Stop()
            if (-not $transportResponse.Success -or
                [Math]::Abs([double]$transportResponse.SharedImageValueSum - $expectedValueSum) -gt 0.01) {
                throw "YOLO shared-image stress mismatch at cycle $cycle."
            }
            $durations[$cycle] = $watch.Elapsed.TotalMilliseconds
        }

        $sorted = @($durations | Sort-Object)
        $average = ($durations | Measure-Object -Average).Average
        $p50 = $sorted[[Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.50) - 1)]
        $p95 = $sorted[[Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.95) - 1)]
        $p99 = $sorted[[Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.99) - 1)]
        $maximum = $sorted[$sorted.Count - 1]
        Write-Host ("YOLO shared-image transport stress passed. cycles={0}, bytes={1}, avg={2:F3}ms, P50={3:F3}ms, P95={4:F3}ms, P99={5:F3}ms, max={6:F3}ms" -f `
            $TransportCycles,
            $transportImage.Bytes.Length,
            $average,
            $p50,
            $p95,
            $p99,
            $maximum)

        $imageView.Dispose()
        $imageView = $null
        $imageMap.Dispose()
        $imageMap = $null
    }

    if (-not [string]::IsNullOrWhiteSpace($ModelPath)) {
        if (-not (Test-Path -LiteralPath $ModelPath -PathType Leaf)) {
            throw "YOLO integration model does not exist: $ModelPath"
        }
        if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
            throw "YOLO integration image does not exist: $ImagePath"
        }

        $initJson = @{
            Command = "init"
            ModelPath = $ModelPath
            ModelType = 0
            DeviceType = 0
            ClassNames = @()
            InputSize = 640
            ScoreThreshold = 0.3
            NmsThreshold = 0.5
            KeyPointNum = 0
        } | ConvertTo-Json -Compress
        $initResponse = Send-WorkerRequest $process $input $initJson 30000
        if (-not $initResponse.Success) {
            throw "YOLO worker model initialization failed: $($initResponse.Error)"
        }

        $image = Get-BgrImagePayload $ImagePath
        $mapName = "TDJS_VISION_YOLO_TEST_$PID`_$([Guid]::NewGuid().ToString('N'))"
        $imageMap = [System.IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew($mapName, [long]$image.Bytes.Length)
        $imageView = $imageMap.CreateViewAccessor()
        $imageView.WriteArray(0L, [byte[]]$image.Bytes, 0, $image.Bytes.Length)
        $detectJson = @{
            Command = "detect"
            ModelType = 0
            DeviceType = 0
            ScoreThreshold = 0.3
            NmsThreshold = 0.5
            Rows = $image.Rows
            Cols = $image.Cols
            MatType = $image.MatType
            ImageMapName = $mapName
            ImageMapCapacity = $image.Bytes.Length
            ImageByteCount = $image.Bytes.Length
            ImageStride = $image.RowBytes
            DeltaX = 0
            DeltaY = 0
            NeedMaskBox = $true
        } | ConvertTo-Json -Compress
        $detectResponse = Send-WorkerRequest $process $input $detectJson 30000
        if (-not $detectResponse.Success) {
            throw "YOLO worker inference failed: $($detectResponse.Error)"
        }

        $resultCount = @($detectResponse.DetResults).Count
        Write-Host "YOLO worker model inference checks passed. Detection count: $resultCount"
    }

    $input.Dispose()
    if (-not $process.WaitForExit(5000)) {
        throw "YOLO worker did not exit after its input pipe was closed."
    }

    if ($process.ExitCode -ne 0) {
        throw "YOLO worker exited with code $($process.ExitCode)."
    }

    Write-Host "YOLO worker process integration checks passed."
}
finally {
    if ($null -ne $imageView) {
        $imageView.Dispose()
    }
    if ($null -ne $imageMap) {
        $imageMap.Dispose()
    }
    if (-not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }

    $process.Dispose()
}
