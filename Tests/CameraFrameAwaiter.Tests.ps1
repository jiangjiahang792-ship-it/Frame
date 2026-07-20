$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$sourcePath = Join-Path $PSScriptRoot '..\Node\1-Acquisition\ImageSource\CameraFrameAwaiter.cs'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw 'CameraFrameAwaiter.cs does not exist.'
}

$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$stub = @'
namespace OpenCvSharp
{
    public sealed class Mat : System.IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
'@

Add-Type -TypeDefinition ($source + [Environment]::NewLine + $stub) -Language CSharp

$awaiter = New-Object TDJS_Vision.Node._1_Acquisition.ImageSource.CameraFrameAwaiter
$tokenSource = New-Object System.Threading.CancellationTokenSource
$frameTask = $awaiter.BeginWaitAsync($tokenSource.Token)
$frame = New-Object OpenCvSharp.Mat

Assert-True ($awaiter.TrySupplyFrame($frame)) 'The active wait must accept one frame.'
Assert-True ($frameTask.Result -eq $frame) 'The supplied frame must complete the active wait.'
Assert-True (-not $awaiter.TrySupplyFrame((New-Object OpenCvSharp.Mat))) 'A late frame must be rejected.'

$cancelSource = New-Object System.Threading.CancellationTokenSource
$cancelTask = $awaiter.BeginWaitAsync($cancelSource.Token)
$cancelSource.Cancel()

try {
    $cancelTask.GetAwaiter().GetResult()
    throw 'Cancellation must cancel the pending frame task.'
}
catch [System.OperationCanceledException] {
}

$awaiter.Dispose()
Write-Host 'Camera frame awaiter behavior checks passed.'
