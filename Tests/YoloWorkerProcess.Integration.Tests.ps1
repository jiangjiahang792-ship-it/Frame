param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [string]$ModelPath,

    [string]$ImagePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Worker integration executable does not exist: $ExecutablePath"
}

if ([string]::IsNullOrWhiteSpace($ModelPath) -xor [string]::IsNullOrWhiteSpace($ImagePath)) {
    throw "ModelPath and ImagePath must be provided together."
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
                ImageBase64 = [Convert]::ToBase64String($bytes)
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
        $detectJson = @{
            Command = "detect"
            ModelType = 0
            DeviceType = 0
            ScoreThreshold = 0.3
            NmsThreshold = 0.5
            Rows = $image.Rows
            Cols = $image.Cols
            MatType = $image.MatType
            ImageBase64 = $image.ImageBase64
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
    if (-not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }

    $process.Dispose()
}
